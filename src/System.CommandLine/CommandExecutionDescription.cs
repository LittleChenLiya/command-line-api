// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Parsing;

namespace System.CommandLine
{
    /// <summary>
    /// A read-only description of a command that was selected while parsing, including
    /// the options and arguments that will be supplied to it when a <see cref="ParseResult"/>
    /// is invoked.
    /// </summary>
    public sealed class CommandExecutionDescription
    {
        internal CommandExecutionDescription(
            string name,
            TokenRange identifierTokenRange,
            IReadOnlyList<SymbolExecutionDescription> symbols,
            IReadOnlyList<string> unmatchedTokens)
        {
            Name = name;
            IdentifierTokenRange = identifierTokenRange;
            Symbols = symbols;
            UnmatchedTokens = unmatchedTokens;
        }

        /// <summary>
        /// Gets the canonical name of the command as it was defined.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the range of the input argument that selected the command.
        /// </summary>
        public TokenRange IdentifierTokenRange { get; }

        /// <summary>
        /// Gets descriptions of the options and arguments that were explicitly provided
        /// or that received a default value, in parser order.
        /// </summary>
        public IReadOnlyList<SymbolExecutionDescription> Symbols { get; }

        /// <summary>
        /// Gets the tokens that were not matched while parsing this command.
        /// </summary>
        public IReadOnlyList<string> UnmatchedTokens { get; }

        /// <inheritdoc />
        public override string ToString() => Name;
    }
}
