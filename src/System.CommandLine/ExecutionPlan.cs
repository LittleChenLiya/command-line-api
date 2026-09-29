// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Parsing;

namespace System.CommandLine
{
    /// <summary>
    /// A read-only description of how a parsed command line is going to be executed.
    /// It lists each selected command along with the options and arguments that will be
    /// supplied to it, their parsed and default values, and the positions of the tokens
    /// they came from. It is suitable for logging and confirmation user interfaces.
    /// </summary>
    public sealed class ExecutionPlan
    {
        internal ExecutionPlan(
            IReadOnlyList<CommandExecutionDescription> commands,
            IReadOnlyList<ParseError> errors,
            IReadOnlyList<Token> tokens)
        {
            Commands = commands;
            Errors = errors;
            Tokens = tokens;
        }

        /// <summary>
        /// Gets the selected commands in selection order, beginning with the root command
        /// and ending with the innermost selected command.
        /// </summary>
        public IReadOnlyList<CommandExecutionDescription> Commands { get; }

        /// <summary>
        /// Gets the innermost selected command.
        /// </summary>
        public CommandExecutionDescription SelectedCommand => Commands[Commands.Count - 1];

        /// <summary>
        /// Gets the parse errors found while parsing the command-line input.
        /// </summary>
        public IReadOnlyList<ParseError> Errors { get; }

        /// <summary>
        /// Gets the tokens identified while parsing command-line input, in input order.
        /// </summary>
        public IReadOnlyList<Token> Tokens { get; }

        /// <inheritdoc />
        public override string ToString() => SelectedCommand.Name;
    }
}
