// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine;

/// <summary>
/// Describes an error found while splitting, parsing, or validating a single stage of a command sequence.
/// </summary>
public sealed class SequenceError
{
    internal SequenceError(int stageNumber, int argumentPosition, string message)
    {
        StageNumber = stageNumber;
        ArgumentPosition = argumentPosition;
        Message = message;
    }

    /// <summary>
    /// Gets the 1-based number of the stage the error belongs to.
    /// </summary>
    public int StageNumber { get; }

    /// <summary>
    /// Gets the 0-based position within the original argument array at which the error was detected.
    /// </summary>
    public int ArgumentPosition { get; }

    /// <summary>
    /// A message to explain the error to a user.
    /// </summary>
    public string Message { get; }

    /// <inheritdoc />
    public override string ToString() => $"Stage {StageNumber} (argument {ArgumentPosition}): {Message}";
}
