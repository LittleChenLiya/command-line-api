// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine
{
    /// <summary>
    /// Describes an error found while splitting or parsing a command sequence
    /// created by <see cref="SequenceParser.ParseSequence"/>.
    /// </summary>
    public sealed class SequenceParseError
    {
        internal SequenceParseError(int stage, int position, string message)
        {
            Stage = stage;
            Position = position;
            Message = message;
        }

        /// <summary>
        /// The 1-based number of the stage the error belongs to.
        /// </summary>
        public int Stage { get; }

        /// <summary>
        /// The 0-based position in the original argument array at which the error was detected.
        /// </summary>
        public int Position { get; }

        /// <summary>
        /// A message explaining the error.
        /// </summary>
        public string Message { get; }

        /// <inheritdoc />
        public override string ToString() => $"Stage {Stage} (argument {Position}): {Message}";
    }
}
