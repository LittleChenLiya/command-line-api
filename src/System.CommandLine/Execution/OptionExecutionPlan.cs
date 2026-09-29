// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace System.CommandLine.Execution
{
    /// <summary>
    /// Describes how an option is going to be supplied when the parsed command is invoked.
    /// </summary>
    public sealed class OptionExecutionPlan : SymbolExecutionPlan
    {
        internal OptionExecutionPlan(
            string canonicalName,
            ParsedValueSource source,
            object? value,
            string? conversionError,
            IReadOnlyList<ParsedTokenValue> values,
            IReadOnlyList<InputTokenSpan> identifierSpans) :
            base(canonicalName, source, value, conversionError, values)
        {
            IdentifierSpans = identifierSpans;
        }

        /// <summary>
        /// Gets the spans of the input tokens used to identify the option (for example <c>--name</c> or the <c>-n</c> part of a bundle),
        /// one entry per occurrence of the option.
        /// </summary>
        public IReadOnlyList<InputTokenSpan> IdentifierSpans { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            if (Values.Count == 0)
            {
                return Source switch
                {
                    ParsedValueSource.Default => $"{CanonicalName} <default>",
                    ParsedValueSource.None => $"{CanonicalName} <missing>",
                    _ => CanonicalName
                };
            }

            return $"{CanonicalName} {string.Join(" ", Values)}";
        }
    }
}
