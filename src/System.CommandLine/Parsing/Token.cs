// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// A unit of significant text on the command line.
    /// </summary>
    public sealed class Token : IEquatable<Token>
    {
        internal const int ImplicitPosition = -1;

        /// <param name="value">The string value of the token.</param>
        /// <param name="type">The type of the token.</param>
        /// <param name="symbol">The symbol represented by the token</param>
        public Token(string? value, TokenType type, Symbol symbol)
        {
            Value = value ?? "";
            Type = type;
            Symbol = symbol;
            Position = ImplicitPosition;
            SubstringStart = 0;
            SubstringLength = Value.Length;
        }
       
        internal Token(string? value, TokenType type, Symbol? symbol, int position, int substringStart = 0, int? substringLength = null)
        {
            Value = value ?? "";
            Type = type;
            Symbol = symbol;
            Position = position;
            SubstringStart = substringStart;
            SubstringLength = substringLength ?? Value.Length;
        }

        internal int Position { get; }

        /// <summary>
        /// The zero-based start position of <see cref="Value"/> within the original input argument from which the token was derived.
        /// For tokens produced by short-option bundling or inline option values (e.g. <c>-xfoo</c> or <c>--opt:foo</c>), this points
        /// at the part of the input argument represented by the token; otherwise it is 0.
        /// </summary>
        internal int SubstringStart { get; }

        /// <summary>
        /// The length of the part of the original input argument represented by the token.
        /// </summary>
        internal int SubstringLength { get; }

        /// <summary>
        /// The string value of the token.
        /// </summary>
        public string Value { get; }

        internal bool Implicit => Position == ImplicitPosition;

        /// <summary>
        /// The type of the token.
        /// </summary>
        public TokenType Type { get; }

        /// <summary>
        /// The Symbol represented by the token (if any).
        /// </summary>
        internal Symbol? Symbol { get; set; }

        /// <inheritdoc />
        public override bool Equals(object? obj) => Equals(obj as Token);

        /// <inheritdoc />
        public bool Equals(Token? other) => other is not null && Value == other.Value && Type == other.Type && ReferenceEquals(Symbol, other.Symbol);

        /// <inheritdoc />
        public override int GetHashCode() => Value.GetHashCode() ^ (int)Type;

        /// <inheritdoc />
        public override string ToString() => Value;

        /// <summary>
        /// Checks if two specified <see cref="Token"/> instances have the same value.
        /// </summary>
        /// <param name="left">The first <see cref="Token"/>.</param>
        /// <param name="right">The second <see cref="Token"/>.</param>
        /// <returns><see langword="true" /> if the objects are equal.</returns>
        public static bool operator ==(Token? left, Token? right) => left is null ? right is null : left.Equals(right);

        /// <summary>
        /// Checks if two specified <see cref="Token"/> instances have different values.
        /// </summary>
        /// <param name="left">The first <see cref="Token"/>.</param>
        /// <param name="right">The second <see cref="Token"/>.</param>
        /// <returns><see langword="true" /> if the objects are not equal.</returns>
        public static bool operator !=(Token? left, Token? right) => left is null ? right is not null : !left.Equals(right);
    }
}
