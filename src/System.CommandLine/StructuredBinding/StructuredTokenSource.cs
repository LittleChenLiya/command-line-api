// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.StructuredBinding
{
    /// <summary>
    /// Identifies which part of a <see cref="StructuredCommandLine"/> a generated token originated from.
    /// </summary>
    public enum StructuredTokenSourceKind
    {
        /// <summary>The token is a command path segment (<see cref="StructuredCommandLine.CommandPath"/>).</summary>
        Command,

        /// <summary>The token is the alias of an option, emitted as a standalone token.</summary>
        OptionAlias,

        /// <summary>
        /// The token carries an option value. For non-empty values this is a single
        /// <c>--alias=value</c> token; for empty-string values this is a standalone empty token
        /// following the alias token.
        /// </summary>
        OptionValue,

        /// <summary>The token is a positional argument (<see cref="StructuredCommandLine.Arguments"/>).</summary>
        Argument,

        /// <summary>The token is the <c>--</c> separator emitted before positional arguments.</summary>
        Separator
    }

    /// <summary>
    /// Maps one generated token back to the structured input element it was produced from.
    /// </summary>
    public readonly struct StructuredTokenSource : IEquatable<StructuredTokenSource>
    {
        internal StructuredTokenSource(StructuredTokenSourceKind kind, string? key, int index)
        {
            Kind = kind;
            Key = key;
            Index = index;
        }

        /// <summary>
        /// The kind of input element the token originated from.
        /// </summary>
        public StructuredTokenSourceKind Kind { get; }

        /// <summary>
        /// For <see cref="StructuredTokenSourceKind.OptionAlias"/> and <see cref="StructuredTokenSourceKind.OptionValue"/>
        /// tokens, the option key from the structured input; otherwise <see langword="null"/>.
        /// </summary>
        public string? Key { get; }

        /// <summary>
        /// The index of the source element within its containing list: the command path index,
        /// the value index within the option entry, or the positional argument index.
        /// For <see cref="StructuredTokenSourceKind.OptionAlias"/> and
        /// <see cref="StructuredTokenSourceKind.Separator"/> tokens this is -1.
        /// </summary>
        public int Index { get; }

        /// <inheritdoc />
        public bool Equals(StructuredTokenSource other) =>
            Kind == other.Kind && Key == other.Key && Index == other.Index;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is StructuredTokenSource other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (Kind, Key, Index).GetHashCode();

        /// <summary>Determines whether two instances are equal.</summary>
        public static bool operator ==(StructuredTokenSource left, StructuredTokenSource right) => left.Equals(right);

        /// <summary>Determines whether two instances are not equal.</summary>
        public static bool operator !=(StructuredTokenSource left, StructuredTokenSource right) => !left.Equals(right);
    }
}
