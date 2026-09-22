// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// The result of expanding <c>@file</c> response file references in a list of arguments
    /// performed by <see cref="ResponseFileExpander"/>.
    /// </summary>
    public sealed class ResponseFileExpansionResult
    {
        internal ResponseFileExpansionResult(
            IReadOnlyList<string> arguments,
            IReadOnlyList<TokenSource?> sources,
            IReadOnlyList<ParseError> errors)
        {
            Arguments = arguments;
            Sources = sources;
            Errors = errors;
        }

        /// <summary>
        /// The arguments after expansion. When expansion produced errors, this contains the
        /// arguments that were successfully processed before the first error.
        /// </summary>
        public IReadOnlyList<string> Arguments { get; }

        /// <summary>
        /// The provenance of each argument in <see cref="Arguments"/>, aligned by index.
        /// </summary>
        public IReadOnlyList<TokenSource?> Sources { get; }

        /// <summary>
        /// The errors encountered during expansion, if any. Each error carries a
        /// <see cref="ParseError.Source"/> that identifies the response file location
        /// (and include chain) where the problem occurred.
        /// </summary>
        public IReadOnlyList<ParseError> Errors { get; }

        /// <summary>
        /// Indicates whether the expansion completed without errors.
        /// </summary>
        public bool Success => Errors.Count == 0;
    }
}
