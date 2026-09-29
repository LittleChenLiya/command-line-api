// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

namespace System.CommandLine.Execution
{
    /// <summary>
    /// A read-only description of how a given <see cref="ParseResult"/> is going to be executed: the selected command chain,
    /// the options and arguments that will be applied, their converted values, and the exact original input token spans
    /// from which each value originated.
    /// </summary>
    /// <remarks>
    /// Building the plan does not invoke <see cref="object.ToString"/> on parsed values and does not cause any side effects
    /// in addition to those performed while parsing the input. It is suitable for logging and command confirmation screens.
    /// </remarks>
    public sealed class ExecutionPlan
    {
        internal ExecutionPlan(
            IReadOnlyList<CommandExecutionPlan> commands,
            IReadOnlyList<ParsedTokenValue> unmatchedTokens)
        {
            Commands = commands;
            UnmatchedTokens = unmatchedTokens;
        }

        /// <summary>
        /// Gets the selected commands from the root command down to the innermost command.
        /// </summary>
        public IReadOnlyList<CommandExecutionPlan> Commands { get; }

        /// <summary>
        /// Gets the input tokens that were not matched to any symbol, each mapped to its original input span.
        /// </summary>
        public IReadOnlyList<ParsedTokenValue> UnmatchedTokens { get; }

        /// <summary>
        /// Returns a deterministic, side-effect-free textual rendering of the plan based solely on canonical names and
        /// the original token text. Converted values are never rendered.
        /// </summary>
        public override string ToString() => string.Join(" ", Commands.Select(c => c.ToString()));
    }
}
