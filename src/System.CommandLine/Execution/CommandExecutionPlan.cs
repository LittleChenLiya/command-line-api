// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

namespace System.CommandLine.Execution
{
    /// <summary>
    /// A read-only description of how a single selected command and its options and arguments are going to be supplied.
    /// </summary>
    public sealed class CommandExecutionPlan
    {
        internal CommandExecutionPlan(
            string canonicalName,
            IReadOnlyList<InputTokenSpan> identifierSpans,
            IReadOnlyList<OptionExecutionPlan> options,
            IReadOnlyList<ArgumentExecutionPlan> arguments)
        {
            CanonicalName = canonicalName;
            IdentifierSpans = identifierSpans;
            Options = options;
            Arguments = arguments;
        }

        /// <summary>
        /// Gets the canonical name of the command.
        /// </summary>
        public string CanonicalName { get; }

        /// <summary>
        /// Gets the spans of the input tokens identifying the command in the command chain. The root command has an empty list when
        /// its name was not present in the input.
        /// </summary>
        public IReadOnlyList<InputTokenSpan> IdentifierSpans { get; }

        /// <summary>
        /// Gets the options that will be applied to this command, in declaration order, including those using default values.
        /// </summary>
        public IReadOnlyList<OptionExecutionPlan> Options { get; }

        /// <summary>
        /// Gets the arguments that will be supplied to this command, in declaration order, including those using default values.
        /// </summary>
        public IReadOnlyList<ArgumentExecutionPlan> Arguments { get; }

        /// <inheritdoc />
        public override string ToString() =>
            string.Join(" ",
                new[] { CanonicalName }
                    .Concat(Options.Select(o => o.ToString()))
                    .Concat(Arguments.Select(a => a.ToString())));
    }
}
