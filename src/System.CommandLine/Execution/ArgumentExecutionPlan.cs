// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace System.CommandLine.Execution
{
    /// <summary>
    /// Describes how an argument is going to be supplied when the parsed command is invoked.
    /// </summary>
    public sealed class ArgumentExecutionPlan : SymbolExecutionPlan
    {
        internal ArgumentExecutionPlan(
            string canonicalName,
            ParsedValueSource source,
            object? value,
            string? conversionError,
            IReadOnlyList<ParsedTokenValue> values) :
            base(canonicalName, source, value, conversionError, values)
        {
        }

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
