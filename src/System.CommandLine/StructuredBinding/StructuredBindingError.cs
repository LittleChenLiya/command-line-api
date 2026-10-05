// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.StructuredBinding
{
    /// <summary>
    /// Identifies which stage of structured binding produced an error.
    /// </summary>
    public enum StructuredBindingErrorKind
    {
        /// <summary>A command path segment did not match a subcommand of the current command.</summary>
        UnknownCommand,

        /// <summary>An option key did not match any option visible from the resolved command.</summary>
        UnknownOption,

        /// <summary>
        /// The same option was specified more than once, for example through two different aliases.
        /// </summary>
        DuplicateOption,

        /// <summary>
        /// The number of values supplied for an option (or a <see langword="null"/> value) violates
        /// the option's arity.
        /// </summary>
        InvalidValueCount,

        /// <summary>
        /// The generated tokens were rejected by the parser, for example because a required option
        /// or argument was missing or a value could not be converted.
        /// </summary>
        ParseFailure
    }

    /// <summary>
    /// Describes a single error produced while binding a <see cref="StructuredCommandLine"/>.
    /// </summary>
    public sealed class StructuredBindingError
    {
        internal StructuredBindingError(StructuredBindingErrorKind kind, string message)
        {
            Kind = kind;
            Message = message;
        }

        /// <summary>
        /// The kind of the error, indicating which binding stage failed.
        /// </summary>
        public StructuredBindingErrorKind Kind { get; }

        /// <summary>
        /// A message explaining the error.
        /// </summary>
        public string Message { get; }

        /// <inheritdoc />
        public override string ToString() => Message;
    }
}
