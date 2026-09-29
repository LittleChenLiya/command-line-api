// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.Threading;
using System.Threading.Tasks;

namespace System.CommandLine.Invocation;

internal static class AsyncValidationPipeline
{
    /// <summary>
    /// Collects the async validators registered on the parsed command and its parents,
    /// ordered from the outermost command to the innermost one, preserving registration order.
    /// </summary>
    internal static List<AsyncValidator>? CollectAsyncValidators(ParseResult parseResult)
    {
        List<AsyncValidator>? validators = null;
        Stack<Command>? chain = null;

        for (CommandResult? result = parseResult.CommandResult; result is not null; result = result.Parent as CommandResult)
        {
            if (result.Command.HasAsyncValidators)
            {
                (chain ??= new()).Push(result.Command);
            }
        }

        if (chain is not null)
        {
            validators = new();

            while (chain.Count > 0)
            {
                validators.AddRange(chain.Pop().AsyncValidators);
            }
        }

        return validators;
    }

    /// <summary>
    /// Runs the validators with the specified concurrency limit and returns the produced
    /// diagnostics in registration order (not completion order).
    /// </summary>
    internal static async Task<IReadOnlyList<ValidationDiagnostic>?> ValidateAsync(
        ParseResult parseResult,
        IReadOnlyList<AsyncValidator> validators,
        int maxConcurrency,
        CancellationToken cancellationToken)
    {
        ValidationDiagnostic?[] diagnostics = new ValidationDiagnostic?[validators.Count];
        Task[] tasks = new Task[validators.Count];

        using SemaphoreSlim concurrencyLimiter = new(maxConcurrency, maxConcurrency);

        for (int i = 0; i < validators.Count; i++)
        {
            tasks[i] = RunValidatorAsync(validators[i], i, parseResult, diagnostics, concurrencyLimiter, cancellationToken);
        }

        try
        {
            // Task.WhenAll awaits every started validator, even when some of them are canceled,
            // so no check is left running in the background after cancellation.
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Validators that were still waiting for a concurrency slot were never started.
            // Already-started validators have been awaited above. Propagate the cancellation.
            throw new OperationCanceledException(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();

        List<ValidationDiagnostic>? results = null;

        for (int i = 0; i < diagnostics.Length; i++)
        {
            if (diagnostics[i] is { } diagnostic)
            {
                (results ??= new()).Add(diagnostic);
            }
        }

        return results;
    }

    private static async Task RunValidatorAsync(
        AsyncValidator validator,
        int index,
        ParseResult parseResult,
        ValidationDiagnostic?[] diagnostics,
        SemaphoreSlim concurrencyLimiter,
        CancellationToken cancellationToken)
    {
        // A validator that is still waiting for a concurrency slot when cancellation is
        // requested is never started, so it cannot produce any side effects.
        await concurrencyLimiter.WaitAsync(cancellationToken).ConfigureAwait(false);

        // On some runtimes the cancellation of the wait above is processed asynchronously,
        // so the slot might have been acquired after cancellation was requested.
        // Re-check before invoking the validator to guarantee that a canceled validator
        // is never started.
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var diagnostic = await validator.Validate(parseResult, cancellationToken).ConfigureAwait(false);

            if (diagnostic is { ValidatorName: null })
            {
                diagnostic = new ValidationDiagnostic(diagnostic.Message, validator.Name);
            }

            diagnostics[index] = diagnostic;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            diagnostics[index] = new ValidationDiagnostic(exception.Message, validator.Name);
        }
        finally
        {
            concurrencyLimiter.Release();
        }
    }
}
