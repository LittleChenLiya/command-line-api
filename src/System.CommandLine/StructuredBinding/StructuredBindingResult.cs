// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace System.CommandLine.StructuredBinding
{
    /// <summary>
    /// The outcome of binding a <see cref="StructuredCommandLine"/> with
    /// <see cref="StructuredCommandLineBinder.Bind"/>.
    /// </summary>
    /// <remarks>
    /// The handler of the command is never executed by the binder. A <see cref="ParseResult"/>
    /// and the token-to-source mapping are only produced when the generated tokens pass parse
    /// validation; otherwise <see cref="Errors"/> describes why binding failed.
    /// </remarks>
    public sealed class StructuredBindingResult
    {
        private static readonly IReadOnlyList<string> s_emptyTokens = new string[0];
        private static readonly IReadOnlyList<StructuredTokenSource> s_emptySources = new StructuredTokenSource[0];
        private static readonly IReadOnlyList<StructuredBindingError> s_emptyErrors = new StructuredBindingError[0];

        internal StructuredBindingResult(
            ParseResult? parseResult,
            IReadOnlyList<string>? tokens,
            IReadOnlyList<StructuredTokenSource>? tokenSources,
            IReadOnlyList<StructuredBindingError>? errors)
        {
            ParseResult = parseResult;
            Tokens = tokens ?? s_emptyTokens;
            TokenSources = tokenSources ?? s_emptySources;
            Errors = errors ?? s_emptyErrors;
        }

        /// <summary>
        /// <see langword="true"/> when binding produced no errors and the generated tokens passed
        /// parse validation.
        /// </summary>
        public bool Success => Errors.Count == 0;

        /// <summary>
        /// The parse result for the generated tokens. Only available when <see cref="Success"/> is
        /// <see langword="true"/>; otherwise <see langword="null"/>.
        /// </summary>
        public ParseResult? ParseResult { get; }

        /// <summary>
        /// The tokens that were generated from the structured input and handed to the parser.
        /// Empty when binding failed.
        /// </summary>
        public IReadOnlyList<string> Tokens { get; }

        /// <summary>
        /// The source mapping for <see cref="Tokens"/>: element <c>i</c> describes which structured
        /// input element <see cref="Tokens"/><c>[i]</c> was generated from. Empty when binding failed.
        /// </summary>
        public IReadOnlyList<StructuredTokenSource> TokenSources { get; }

        /// <summary>
        /// The errors produced during binding or parse validation. Empty when <see cref="Success"/>
        /// is <see langword="true"/>.
        /// </summary>
        public IReadOnlyList<StructuredBindingError> Errors { get; }
    }
}
