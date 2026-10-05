// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// Describes why <see cref="CommandLineText.TryParse"/> failed and where in the text it failed.
    /// </summary>
    public sealed class CommandLineTextError
    {
        internal CommandLineTextError(CommandLineTextErrorKind kind, int position, string message)
        {
            Kind = kind;
            Position = position;
            Message = message;
        }

        /// <summary>
        /// The kind of the error.
        /// </summary>
        public CommandLineTextErrorKind Kind { get; }

        /// <summary>
        /// The UTF-16 offset in the text at which the error was detected. For
        /// <see cref="CommandLineTextErrorKind.UnterminatedQuote"/> this is the position of the
        /// opening quote of the unterminated token; for
        /// <see cref="CommandLineTextErrorKind.InvalidEscape"/> it is the position of the backslash.
        /// </summary>
        public int Position { get; }

        /// <summary>
        /// A human-readable description of the error.
        /// </summary>
        public string Message { get; }

        /// <inheritdoc />
        public override string ToString() => Message;
    }

    /// <summary>
    /// Identifies the kind of a <see cref="CommandLineTextError"/>.
    /// </summary>
    public enum CommandLineTextErrorKind
    {
        /// <summary>
        /// A token's opening double quote has no matching closing double quote.
        /// </summary>
        UnterminatedQuote,

        /// <summary>
        /// A backslash is not followed by a recognized escape character.
        /// </summary>
        InvalidEscape,

        /// <summary>
        /// A character other than whitespace or a double quote appears between tokens.
        /// </summary>
        UnexpectedCharacter
    }
}
