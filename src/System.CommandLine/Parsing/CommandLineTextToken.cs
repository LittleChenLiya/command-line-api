// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// A single argument token read back by <see cref="CommandLineText.TryParse"/>, together with
    /// the range it occupies in the formatted text.
    /// </summary>
    public readonly struct CommandLineTextToken : IEquatable<CommandLineTextToken>
    {
        /// <param name="value">The decoded value of the token.</param>
        /// <param name="start">The UTF-16 offset of the token's first character (its opening quote) in the formatted text.</param>
        /// <param name="length">The length of the token in UTF-16 code units, including the surrounding quotes.</param>
        public CommandLineTextToken(string value, int start, int length)
        {
            Value = value ?? "";
            Start = start;
            Length = length;
        }

        /// <summary>
        /// The decoded value of the token, with quotes removed and escape sequences resolved. This
        /// value can be passed to
        /// <see cref="CommandLineParser.Parse(Command, System.Collections.Generic.IReadOnlyList{string}, ParserConfiguration)"/>.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// The UTF-16 offset of the token's first character (its opening quote) in the formatted text.
        /// </summary>
        public int Start { get; }

        /// <summary>
        /// The length of the token in UTF-16 code units, including the surrounding quotes.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// The UTF-16 offset one past the token's last character in the formatted text.
        /// </summary>
        public int End => Start + Length;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is CommandLineTextToken other && Equals(other);

        /// <inheritdoc />
        public bool Equals(CommandLineTextToken other) =>
            Value == other.Value && Start == other.Start && Length == other.Length;

        /// <inheritdoc />
        public override int GetHashCode() => Value.GetHashCode() ^ Start ^ (Length << 16);

        /// <inheritdoc />
        public override string ToString() => $"[{Start}..{End}) {Value}";

        /// <summary>
        /// Checks if two specified <see cref="CommandLineTextToken"/> instances have the same value and range.
        /// </summary>
        /// <param name="left">The first <see cref="CommandLineTextToken"/>.</param>
        /// <param name="right">The second <see cref="CommandLineTextToken"/>.</param>
        /// <returns><see langword="true" /> if the objects are equal.</returns>
        public static bool operator ==(CommandLineTextToken left, CommandLineTextToken right) => left.Equals(right);

        /// <summary>
        /// Checks if two specified <see cref="CommandLineTextToken"/> instances have different values or ranges.
        /// </summary>
        /// <param name="left">The first <see cref="CommandLineTextToken"/>.</param>
        /// <param name="right">The second <see cref="CommandLineTextToken"/>.</param>
        /// <returns><see langword="true" /> if the objects are not equal.</returns>
        public static bool operator !=(CommandLineTextToken left, CommandLineTextToken right) => !left.Equals(right);
    }
}
