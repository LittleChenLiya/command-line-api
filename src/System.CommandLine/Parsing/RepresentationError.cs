// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// Describes an error encountered while decoding a command-line representation.
    /// </summary>
    public sealed class RepresentationError
    {
        internal RepresentationError(int position, string message, RepresentationErrorKind kind)
        {
            Position = position;
            Message = message;
            Kind = kind;
        }

        /// <summary>
        /// Gets the UTF-16 offset in the source text where the error occurred.
        /// </summary>
        public int Position { get; }

        /// <summary>
        /// Gets a human-readable message describing the error.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the kind of error encountered.
        /// </summary>
        public RepresentationErrorKind Kind { get; }

        /// <inheritdoc />
        public override string ToString() => $"({Position}) {Message}";
    }

    /// <summary>
    /// Identifies the kind of error encountered while decoding a command-line representation.
    /// </summary>
    public enum RepresentationErrorKind
    {
        /// <summary>
        /// An escape sequence that is not supported by the protocol was encountered.
        /// </summary>
        IllegalEscape,

        /// <summary>
        /// A quoted token was not closed before the end of the input.
        /// </summary>
        UnclosedQuote,

        /// <summary>
        /// A <c>\u</c> escape was not followed by exactly four hexadecimal digits.
        /// </summary>
        InvalidUnicodeEscape
    }
}
