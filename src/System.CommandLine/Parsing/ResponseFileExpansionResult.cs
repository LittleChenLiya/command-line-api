// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// A single argument produced by response file expansion, together with its source.
    /// </summary>
    public sealed class ExpandedArgument
    {
        /// <param name="value">The argument value.</param>
        /// <param name="source">The source the argument was read from.</param>
        public ExpandedArgument(string value, TokenSource source)
        {
            Value = value;
            Source = source;
        }

        /// <summary>
        /// Gets the argument value.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Gets the source the argument was read from.
        /// </summary>
        public TokenSource Source { get; }

        /// <inheritdoc />
        public override string ToString() => Value;
    }

    /// <summary>
    /// Describes an error that occurred during response file expansion.
    /// </summary>
    public sealed class ResponseFileExpansionError
    {
        /// <param name="message">A message explaining the error.</param>
        /// <param name="source">The source location at which the error occurred, if available.</param>
        public ResponseFileExpansionError(string message, TokenSource? source)
        {
            Message = message;
            Source = source;
        }

        /// <summary>
        /// Gets a message explaining the error.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the source location at which the error occurred, if available.
        /// </summary>
        public TokenSource? Source { get; }

        /// <inheritdoc />
        public override string ToString() => Source is null ? Message : $"{Source}: {Message}";
    }

    /// <summary>
    /// The result of expanding <c>@file</c> references in command line arguments.
    /// </summary>
    public sealed class ResponseFileExpansionResult
    {
        internal ResponseFileExpansionResult(
            IReadOnlyList<ExpandedArgument> arguments,
            IReadOnlyList<ResponseFileExpansionError> errors)
        {
            Arguments = arguments;
            Errors = errors;
        }

        /// <summary>
        /// Gets the expanded arguments. Empty when expansion failed.
        /// </summary>
        public IReadOnlyList<ExpandedArgument> Arguments { get; }

        /// <summary>
        /// Gets the errors encountered during expansion.
        /// </summary>
        public IReadOnlyList<ResponseFileExpansionError> Errors { get; }

        /// <summary>
        /// Gets a value indicating whether expansion completed without errors.
        /// </summary>
        public bool Success => Errors.Count == 0;
    }
}
