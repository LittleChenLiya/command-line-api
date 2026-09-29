// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace System.CommandLine.Execution
{
    /// <summary>
    /// Describes how a single option or argument is going to be supplied when the parsed command is invoked.
    /// </summary>
    public abstract class SymbolExecutionPlan
    {
        private protected SymbolExecutionPlan(
            string canonicalName,
            ParsedValueSource source,
            object? value,
            string? conversionError,
            IReadOnlyList<ParsedTokenValue> values)
        {
            CanonicalName = canonicalName;
            Source = source;
            Value = value;
            ConversionError = conversionError;
            Values = values;
        }

        /// <summary>
        /// The canonical name of the option or argument as defined on the command, regardless of the alias used on the command line.
        /// </summary>
        public string CanonicalName { get; }

        /// <summary>
        /// Indicates whether the value was provided explicitly or taken from a default.
        /// </summary>
        public ParsedValueSource Source { get; }

        /// <summary>
        /// The converted value that will be passed to the command handler, or <see langword="null"/> when conversion failed
        /// or when no value applies. This is the actual parsed object; its <see cref="object.ToString"/> is never called
        /// while building the execution plan.
        /// </summary>
        public object? Value { get; }

        /// <summary>
        /// Gets the error reported while converting the input, or <see langword="null"/> when conversion succeeded.
        /// </summary>
        public string? ConversionError { get; }

        /// <summary>
        /// Gets the individual values read from the input, each one mapped one-to-one to a span of the original input arguments.
        /// The list is empty when <see cref="Source"/> is <see cref="ParsedValueSource.Default"/> or when the symbol takes no value.
        /// </summary>
        public IReadOnlyList<ParsedTokenValue> Values { get; }
    }
}
