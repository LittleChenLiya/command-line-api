// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// Describes an error that occurs while parsing command line input.
    /// </summary>
    public sealed class ParseError
    {
        private readonly TokenSource? _source;

        internal ParseError(
            string message,
            SymbolResult? symbolResult = null,
            TokenSource? source = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(message));
            }

            Message = message;
            SymbolResult = symbolResult;
            _source = source;
        }

        /// <summary>
        /// A message to explain the error to a user.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// The symbol result detailing the symbol that failed to parse and the tokens involved.
        /// </summary>
        public SymbolResult? SymbolResult { get; }

        /// <summary>
        /// Gets the source location (response file, line, column, and include chain) of the
        /// token that caused the error, when response file expansion is enabled and the
        /// location is known.
        /// </summary>
        public TokenSource? Source
        {
            get
            {
                if (_source is not null)
                {
                    return _source;
                }

                if (SymbolResult is not null)
                {
                    var tokens = SymbolResult.Tokens;

                    for (var i = 0; i < tokens.Count; i++)
                    {
                        if (tokens[i].Source is { } source)
                        {
                            return source;
                        }
                    }
                }

                if (SymbolResult is OptionResult optionResult &&
                    optionResult.IdentifierToken?.Source is { } identifierSource)
                {
                    return identifierSource;
                }

                return null;
            }
        }

        /// <inheritdoc />
        public override string ToString() => Message;
    }
}
