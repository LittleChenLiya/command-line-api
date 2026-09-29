// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Parsing;

namespace System.CommandLine
{
    /// <summary>
    /// A read-only description of how a single option or argument of a selected command
    /// will be populated when a <see cref="ParseResult"/> is invoked.
    /// </summary>
    public sealed class SymbolExecutionDescription
    {
        internal SymbolExecutionDescription(
            ExecutionSymbolKind kind,
            string name,
            IReadOnlyList<ParsedValue> values,
            IReadOnlyList<TokenRange> identifierTokenRanges)
        {
            Kind = kind;
            Name = name;
            Values = values;
            IdentifierTokenRanges = identifierTokenRanges;
        }

        /// <summary>
        /// Gets the kind of the symbol.
        /// </summary>
        public ExecutionSymbolKind Kind { get; }

        /// <summary>
        /// Gets the canonical name of the symbol (the option name including its prefix,
        /// or the argument name).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the values that will be supplied to the symbol, in the order in which they
        /// appeared in the input, followed by the default value when applicable.
        /// </summary>
        public IReadOnlyList<ParsedValue> Values { get; }

        /// <summary>
        /// Gets the ranges of input arguments in which the symbol was explicitly named.
        /// </summary>
        /// <remarks>
        /// This is empty for arguments and contains one range per occurrence for options.
        /// Options unbundled from a single input argument (for example <c>-xv</c>)
        /// report the same range, and each occurrence of a repeated option gets its own range.
        /// </remarks>
        public IReadOnlyList<TokenRange> IdentifierTokenRanges { get; }

        /// <inheritdoc />
        public override string ToString() => $"{Kind}: {Name}";
    }
}
