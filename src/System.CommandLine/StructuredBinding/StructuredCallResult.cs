// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Parsing;

namespace System.CommandLine.StructuredBinding
{
    /// <summary>
    /// Describes the outcome of binding a <see cref="StructuredCall"/> to a <see cref="Command"/>.
    /// </summary>
    /// <remarks>
    /// When <see cref="IsSuccess"/> is <see langword="true"/>, <see cref="ParseResult"/> and
    /// <see cref="Mapping"/> are populated. Otherwise <see cref="Errors"/> describes the binding or
    /// parse failures. The binder never invokes a handler; it only parses.
    /// </remarks>
    public sealed class StructuredCallResult
    {
        private StructuredCallResult(
            bool isSuccess,
            IReadOnlyList<string> errors,
            ParseResult? parseResult,
            IReadOnlyList<TokenSourceMapping>? mapping)
        {
            IsSuccess = isSuccess;
            Errors = errors;
            ParseResult = parseResult;
            Mapping = mapping;
        }

        /// <summary>
        /// Gets a value indicating whether the structured call bound and parsed without errors.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the binding or parse error messages. Empty when <see cref="IsSuccess"/> is true.
        /// </summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>
        /// Gets the underlying <see cref="ParseResult"/> produced by the real parser.
        /// </summary>
        public ParseResult? ParseResult { get; }

        /// <summary>
        /// Gets the mapping from each final <see cref="Token"/> to its structured input source.
        /// </summary>
        public IReadOnlyList<TokenSourceMapping>? Mapping { get; }

        internal static StructuredCallResult Success(
            ParseResult parseResult,
            IReadOnlyList<TokenSourceMapping> mapping)
            => new(true, System.Array.Empty<string>(), parseResult, mapping);

        internal static StructuredCallResult Failure(IReadOnlyList<string> errors)
            => new(false, errors, null, null);
    }

    /// <summary>
    /// Maps a single final <see cref="Token"/> to the piece of structured input that produced it.
    /// </summary>
    public sealed class TokenSourceMapping
    {
        internal TokenSourceMapping(
            Token token,
            string sourceKind,
            int? commandPathIndex,
            string? optionKey,
            string? optionName,
            int? valueIndex,
            int? argumentIndex)
        {
            Token = token;
            SourceKind = sourceKind;
            CommandPathIndex = commandPathIndex;
            OptionKey = optionKey;
            OptionName = optionName;
            ValueIndex = valueIndex;
            ArgumentIndex = argumentIndex;
        }

        /// <summary>
        /// Gets the final token.
        /// </summary>
        public Token Token { get; }

        /// <summary>
        /// Gets the kind of source that produced the token: <c>command</c>, <c>option</c>,
        /// <c>optionValue</c>, <c>separator</c>, or <c>argument</c>.
        /// </summary>
        public string SourceKind { get; }

        /// <summary>
        /// Gets the index into <see cref="StructuredCall.CommandPath"/> for a command token.
        /// </summary>
        public int? CommandPathIndex { get; }

        /// <summary>
        /// Gets the option key (name or alias) as supplied in the structured input.
        /// </summary>
        public string? OptionKey { get; }

        /// <summary>
        /// Gets the canonical name of the resolved <see cref="Option"/>.
        /// </summary>
        public string? OptionName { get; }

        /// <summary>
        /// Gets the zero-based index of the value within the option's value list.
        /// </summary>
        public int? ValueIndex { get; }

        /// <summary>
        /// Gets the zero-based index of the positional argument.
        /// </summary>
        public int? ArgumentIndex { get; }
    }
}
