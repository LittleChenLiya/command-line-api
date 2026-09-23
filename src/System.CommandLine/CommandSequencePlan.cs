// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace System.CommandLine;

/// <summary>
/// Represents a parsed and validated sequence of command line stages that can be executed exactly once.
/// </summary>
/// <remarks>
/// A plan is created by <see cref="CommandSequence.Parse(Command, IReadOnlyList{string}, ParserConfiguration)"/>.
/// All stages are parsed and validated up front; no stage action runs before the whole sequence is known to be valid.
/// Each stage has its own <see cref="ParseResult"/>, so option and argument values never leak from one stage into the next.
/// </remarks>
public sealed class CommandSequencePlan
{
    private readonly IReadOnlyList<ParseResult> _parseResults;
    private int _executed;

    internal CommandSequencePlan(IReadOnlyList<ParseResult> parseResults, IReadOnlyList<SequenceError> errors)
    {
        _parseResults = parseResults;
        ParseResults = parseResults;
        Errors = errors;
    }

    /// <summary>
    /// Gets the parse result of each stage, in the order the stages appear in the original input.
    /// </summary>
    public IReadOnlyList<ParseResult> ParseResults { get; }

    /// <summary>
    /// Gets the errors found while splitting, parsing, or validating the sequence.
    /// </summary>
    public IReadOnlyList<SequenceError> Errors { get; }

    /// <summary>
    /// Gets a value indicating whether every stage was parsed and validated successfully.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Invokes the action of each stage in order.
    /// </summary>
    /// <param name="configuration">The configuration used to define invocation behaviors.</param>
    /// <returns>The first non-zero exit code produced by a stage, or 0 when every stage succeeds.</returns>
    /// <remarks>
    /// When the plan is not <see cref="IsValid"/>, no action is executed and 1 is returned.
    /// The first stage that returns a non-zero exit code stops the sequence; later stages are not started.
    /// A plan can be executed only once.
    /// </remarks>
    public int Invoke(InvocationConfiguration? configuration = null)
    {
        ThrowIfAlreadyExecuted();

        if (!IsValid)
        {
            WriteErrors(configuration);
            return 1;
        }

        int exitCode = 0;

        for (int i = 0; i < _parseResults.Count; i++)
        {
            exitCode = _parseResults[i].Invoke(configuration);

            if (exitCode != 0)
            {
                break;
            }
        }

        return exitCode;
    }

    /// <summary>
    /// Invokes the action of each stage in order, awaiting asynchronous actions.
    /// </summary>
    /// <param name="configuration">The configuration used to define invocation behaviors.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the invocation.</param>
    /// <returns>The first non-zero exit code produced by a stage, or 0 when every stage succeeds.</returns>
    /// <remarks>
    /// When the plan is not <see cref="IsValid"/>, no action is executed and 1 is returned.
    /// The first stage that returns a non-zero exit code stops the sequence; later stages are not started.
    /// Once cancellation is requested, no further stage is started.
    /// A plan can be executed only once.
    /// </remarks>
    public async Task<int> InvokeAsync(
        InvocationConfiguration? configuration = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfAlreadyExecuted();

        if (!IsValid)
        {
            WriteErrors(configuration);
            return 1;
        }

        int exitCode = 0;

        for (int i = 0; i < _parseResults.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return 1;
            }

            exitCode = await _parseResults[i].InvokeAsync(configuration, cancellationToken).ConfigureAwait(false);

            if (exitCode != 0)
            {
                break;
            }
        }

        return exitCode;
    }

    private void ThrowIfAlreadyExecuted()
    {
        if (Interlocked.Exchange(ref _executed, 1) != 0)
        {
            throw new InvalidOperationException("A command sequence plan can be executed only once. Parse a new plan to run the sequence again.");
        }
    }

    private void WriteErrors(InvocationConfiguration? configuration)
    {
        TextWriter errorWriter = configuration is null ? Console.Error : configuration.Error;

        for (int i = 0; i < Errors.Count; i++)
        {
            errorWriter.WriteLine(Errors[i].ToString());
        }
    }
}
