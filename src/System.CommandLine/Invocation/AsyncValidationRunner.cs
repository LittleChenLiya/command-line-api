// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace System.CommandLine.Invocation
{
    internal static class AsyncValidationRunner
    {
        /// <summary>
        /// Runs the asynchronous validators registered on the parsed command, honoring
        /// <see cref="InvocationConfiguration.MaxAsyncValidatorConcurrency"/>.
        /// Diagnostics are returned in validator registration order, not completion order.
        /// </summary>
        internal static async Task<IReadOnlyList<ValidationDiagnostic>> ValidateAsync(
            ParseResult parseResult,
            CancellationToken cancellationToken)
        {
            List<AsyncValidator> validators = parseResult.CommandResult.Command.AsyncValidators;
            int maxConcurrency = parseResult.InvocationConfiguration.MaxAsyncValidatorConcurrency;

            using SemaphoreSlim concurrencyLimit = new(maxConcurrency, maxConcurrency);
            List<Task<IReadOnlyList<ValidationDiagnostic>?>> startedTasks = new(validators.Count);

            for (int i = 0; i < validators.Count; i++)
            {
                try
                {
                    await concurrencyLimit.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Cancellation stops new validators from being started.
                    break;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    concurrencyLimit.Release();
                    break;
                }

                startedTasks.Add(RunValidatorAsync(validators[i], parseResult, concurrencyLimit, cancellationToken));
            }

            // Wait for every started validator to wind down, so that no check is left
            // running (and potentially mutating state) in the background.
            IReadOnlyList<ValidationDiagnostic>?[] results = await Task.WhenAll(startedTasks).ConfigureAwait(false);

            // Cancellation is propagated rather than reporting partial diagnostics.
            cancellationToken.ThrowIfCancellationRequested();

            List<ValidationDiagnostic>? diagnostics = null;

            for (int i = 0; i < results.Length; i++)
            {
                if (results[i] is { Count: > 0 } validatorDiagnostics)
                {
                    diagnostics ??= new();
                    diagnostics.AddRange(validatorDiagnostics);
                }
            }

            return (IReadOnlyList<ValidationDiagnostic>?)diagnostics ?? Array.Empty<ValidationDiagnostic>();
        }

        private static async Task<IReadOnlyList<ValidationDiagnostic>?> RunValidatorAsync(
            AsyncValidator validator,
            ParseResult parseResult,
            SemaphoreSlim concurrencyLimit,
            CancellationToken cancellationToken)
        {
            try
            {
                IEnumerable<ValidationDiagnostic>? diagnostics =
                    await validator.ValidateAsync(parseResult, cancellationToken).ConfigureAwait(false);

                return diagnostics as IReadOnlyList<ValidationDiagnostic> ?? diagnostics?.ToArray();
            }
            catch (Exception exception)
            {
                // A throwing validator is turned into a failure diagnostic that names the validator.
                return new ValidationDiagnostic[]
                {
                    new($"Asynchronous validator '{validator.Name}' failed: {exception.Message}", validator.Name)
                };
            }
            finally
            {
                concurrencyLimit.Release();
            }
        }
    }
}
