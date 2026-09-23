// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.Threading;
using System.Threading.Tasks;

namespace System.CommandLine
{
    /// <summary>
    /// Represents a validated sequence of parse results that can be invoked exactly once,
    /// one stage at a time and in order. Created by <see cref="SequenceParser.ParseSequence"/>.
    /// </summary>
    /// <remarks>
    /// All stages are parsed and validated up front. If any stage fails to parse or validate,
    /// <see cref="IsValid"/> is <see langword="false"/> and invoking the plan does not run any
    /// stage action. Each stage owns an independent <see cref="ParseResult"/>, so option values
    /// and default values never leak from one stage into the next.
    /// </remarks>
    public sealed class SequenceInvocationPlan
    {
        private readonly ParseResult[] _stages;
        private int _executed;

        internal SequenceInvocationPlan(ParseResult[] stages, SequenceParseError[] errors)
        {
            _stages = stages;
            Errors = errors;
        }

        /// <summary>
        /// Gets the parse result of each non-empty stage, in the order the stages appear on the command line.
        /// </summary>
        public IReadOnlyList<ParseResult> Stages => _stages;

        /// <summary>
        /// Gets the errors found while splitting, parsing, or validating the sequence.
        /// </summary>
        public IReadOnlyList<SequenceParseError> Errors { get; }

        /// <summary>
        /// Gets a value indicating whether every stage was parsed and validated successfully.
        /// </summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// Invokes the stage actions in order.
        /// </summary>
        /// <param name="configuration">The configuration used to define invocation behaviors.</param>
        /// <returns>The exit code of the first stage that returns a non-zero exit code;
        /// 1 when the plan is invalid; otherwise the exit code of the last stage.</returns>
        /// <exception cref="InvalidOperationException">The plan has already been executed.</exception>
        public int Invoke(InvocationConfiguration? configuration = null)
        {
            MarkExecuted();

            if (!IsValid)
            {
                WriteDiagnostics(configuration);
                return 1;
            }

            var useAsync = false;

            foreach (var stage in _stages)
            {
                if (stage.Action is AsynchronousCommandLineAction)
                {
                    useAsync = true;
                    break;
                }

                if (stage.PreActions is not null)
                {
                    foreach (var preAction in stage.PreActions)
                    {
                        if (preAction is AsynchronousCommandLineAction)
                        {
                            useAsync = true;
                            break;
                        }
                    }

                    if (useAsync)
                    {
                        break;
                    }
                }
            }

            if (useAsync)
            {
                return InvokeAsyncCore(configuration, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
            }

            int result = 0;

            foreach (var stage in _stages)
            {
                result = stage.Invoke(configuration);

                if (result != 0)
                {
                    return result;
                }
            }

            return result;
        }

        /// <summary>
        /// Invokes the stage actions in order, awaiting asynchronous actions.
        /// </summary>
        /// <param name="configuration">The configuration used to define invocation behaviors.</param>
        /// <param name="cancellationToken">A token that can be used to cancel the invocation. Once cancellation
        /// is observed, no further stage is started.</param>
        /// <returns>A task whose result is the exit code of the first stage that returns a non-zero exit code;
        /// 1 when the plan is invalid or the sequence was cancelled before completing;
        /// otherwise the exit code of the last stage.</returns>
        /// <exception cref="InvalidOperationException">The plan has already been executed.</exception>
        public Task<int> InvokeAsync(InvocationConfiguration? configuration = null, CancellationToken cancellationToken = default)
        {
            MarkExecuted();

            if (!IsValid)
            {
                WriteDiagnostics(configuration);
                return Task.FromResult(1);
            }

            return InvokeAsyncCore(configuration, cancellationToken);
        }

        private async Task<int> InvokeAsyncCore(InvocationConfiguration? configuration, CancellationToken cancellationToken)
        {
            int result = 0;

            foreach (var stage in _stages)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    // Do not start a new stage once cancellation has been requested.
                    return 1;
                }

                result = await stage.InvokeAsync(configuration, cancellationToken).ConfigureAwait(false);

                if (result != 0)
                {
                    return result;
                }
            }

            return result;
        }

        private void MarkExecuted()
        {
            if (Interlocked.Exchange(ref _executed, 1) != 0)
            {
                throw new InvalidOperationException(
                    $"A {nameof(SequenceInvocationPlan)} can only be executed once. Call {nameof(SequenceParser)}.{nameof(SequenceParser.ParseSequence)} again to create a new plan.");
            }
        }

        private void WriteDiagnostics(InvocationConfiguration? configuration)
        {
            var error = configuration?.Error ?? Console.Error;

            foreach (var sequenceError in Errors)
            {
                error.WriteLine(sequenceError.ToString());
            }
        }
    }
}
