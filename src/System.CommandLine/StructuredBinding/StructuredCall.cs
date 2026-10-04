// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace System.CommandLine.StructuredBinding
{
    /// <summary>
    /// Represents a structured invocation of a command line, described by a canonical
    /// command path, a set of option key/value pairs, and a positional argument array.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="StructuredCall"/> is turned into a token list (suitable for the real
    /// <c>Parse</c> method) by the <see cref="StructuredCallBinder"/>. The binder inspects the
    /// real <see cref="Option"/> symbols in the command tree to honor their arity and alias rules.
    /// </para>
    /// <para>
    /// <strong>Multi-value format.</strong> Multi-valued options are expressed by supplying more
    /// than one item in <see cref="StructuredOptionValue.Values"/>. Each item is preserved
    /// item-by-item; values are never split by a shell. Non-empty values are emitted with the
    /// <c>=</c> separator (e.g. <c>--name=value</c>) so they cannot be split or mistaken for
    /// another option; empty strings are emitted as a separate empty token. A boolean switch is an
    /// option with an empty value list.
    /// </para>
    /// <para>
    /// <strong>Error paths.</strong> The binder validates the command path, option resolution,
    /// duplicate aliases, arity, and required options before parsing. Only when all binding-stage
    /// checks pass does it hand the token list to the real parser; the <see cref="ParseResult"/>
    /// and token-source mapping are returned only when the final parse validation succeeds. See
    /// <see cref="StructuredCallResult"/> for the outcome.
    /// </para>
    /// </remarks>
    public sealed class StructuredCall
    {
        /// <summary>
        /// Gets or sets the canonical command path, i.e. the sequence of command names (or aliases)
        /// that identifies the subcommand to invoke. The first element may be the root command name.
        /// </summary>
        public IReadOnlyList<string> CommandPath { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the option key/value pairs. Each key is an option name or alias; each value
        /// carries zero or more string values.
        /// </summary>
        public IReadOnlyDictionary<string, StructuredOptionValue> Options { get; init; }
            = new Dictionary<string, StructuredOptionValue>();

        /// <summary>
        /// Gets or sets the positional argument array.
        /// </summary>
        public IReadOnlyList<string> Arguments { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Describes the value(s) supplied for a single option in a <see cref="StructuredCall"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Values"/> is an ordered list of strings. An empty list denotes a boolean switch
    /// (the option is present with no values). A single-item list denotes a scalar option. A
    /// multi-item list denotes a multi-valued option; each item is preserved item-by-item and is
    /// never split by a shell.
    /// </para>
    /// <para>
    /// Non-empty values are emitted with the <c>=</c> separator (e.g. <c>--name=value</c>) so
    /// values containing spaces, quotes, or leading dashes cannot be split or mistaken for another
    /// option. Empty strings are emitted as a separate empty token after the option token.
    /// </para>
    /// </remarks>
    public sealed class StructuredOptionValue
    {
        /// <summary>
        /// Gets the ordered list of values for the option. An empty list denotes a boolean switch.
        /// </summary>
        public IReadOnlyList<string> Values { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Implicitly converts a single <see cref="string"/> to a <see cref="StructuredOptionValue"/>.
        /// </summary>
        public static implicit operator StructuredOptionValue(string value)
            => new() { Values = new[] { value } };

        /// <summary>
        /// Implicitly converts a <see cref="string"/> array to a <see cref="StructuredOptionValue"/>.
        /// </summary>
        public static implicit operator StructuredOptionValue(string[] values)
            => new() { Values = values };

        /// <summary>
        /// Implicitly converts a <see cref="List{T}"/> of <see cref="string"/> to a <see cref="StructuredOptionValue"/>.
        /// </summary>
        public static implicit operator StructuredOptionValue(List<string> values)
            => new() { Values = values };
    }
}
