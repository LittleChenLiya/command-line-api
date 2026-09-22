// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// Describes an error that occurs while parsing command line input.
    /// </summary>
    public sealed class ParseError
    {
        internal ParseError(
            string message,
            SymbolResult? symbolResult = null,
            TokenSource? source = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(message));
            }

            _message = message;
            SymbolResult = symbolResult;
            Source = source;
        }

        /// <summary>
        /// A message to explain the error to a user.
        /// </summary>
        public string Message =>
            Source is null
                ? _message
                : $"{_message} [{Source.FormatIncludeChain()}]";

        private readonly string _message;

        /// <summary>
        /// The symbol result detailing the symbol that failed to parse and the tokens involved.
        /// </summary>
        public SymbolResult? SymbolResult { get; }

        /// <summary>
        /// The source (file, line, column and include chain) of the token that caused the error,
        /// when a <see cref="ResponseFileExpander"/> was used and the source was tracked;
        /// otherwise <see langword="null"/>.
        /// </summary>
        public TokenSource? Source { get; internal set; }

        /// <inheritdoc />
        public override string ToString() => Message;
    }
}
