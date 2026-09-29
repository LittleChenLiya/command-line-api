// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.Execution
{
    /// <summary>
    /// Identifies a span of text within an original input argument, as returned by <see cref="ParseResult.Arguments"/>.
    /// </summary>
    /// <remarks>
    /// A single input argument can contribute more than one token, for example when short options are
    /// bundled (<c>-xzf</c>) or when an option value is passed inline (<c>--name:value</c> or <c>--name=value</c>).
    /// <see cref="Start"/> and <see cref="Length"/> pinpoint the exact span of the token in such cases.
    /// </remarks>
    public readonly struct InputTokenSpan : IEquatable<InputTokenSpan>
    {
        /// <param name="argumentIndex">The zero-based index of the input argument in <see cref="ParseResult.Arguments"/>.</param>
        /// <param name="start">The zero-based offset of the first character of the token within the input argument.</param>
        /// <param name="length">The number of characters of the input argument covered by the token.</param>
        public InputTokenSpan(int argumentIndex, int start, int length)
        {
            ArgumentIndex = argumentIndex;
            Start = start;
            Length = length;
        }

        /// <summary>
        /// The zero-based index of the input argument in <see cref="ParseResult.Arguments"/> containing the token.
        /// </summary>
        public int ArgumentIndex { get; }

        /// <summary>
        /// The zero-based offset of the first character of the token within the input argument.
        /// </summary>
        public int Start { get; }

        /// <summary>
        /// The number of characters of the input argument covered by the token.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Returns the original text represented by the span from the specified arguments.
        /// </summary>
        /// <param name="arguments">The original arguments, typically <see cref="ParseResult.Arguments"/>.</param>
        /// <returns>The substring of the input argument covered by this span.</returns>
        public string GetText(System.Collections.Generic.IReadOnlyList<string> arguments)
        {
            if (arguments is null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            return arguments[ArgumentIndex].Substring(Start, Length);
        }

        /// <inheritdoc />
        public bool Equals(InputTokenSpan other) =>
            ArgumentIndex == other.ArgumentIndex && Start == other.Start && Length == other.Length;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is InputTokenSpan other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => ArgumentIndex ^ (Start * 397) ^ Length;

        /// <summary>
        /// Checks whether two <see cref="InputTokenSpan"/> instances are equal.
        /// </summary>
        public static bool operator ==(InputTokenSpan left, InputTokenSpan right) => left.Equals(right);

        /// <summary>
        /// Checks whether two <see cref="InputTokenSpan"/> instances are not equal.
        /// </summary>
        public static bool operator !=(InputTokenSpan left, InputTokenSpan right) => !left.Equals(right);

        /// <inheritdoc />
        public override string ToString() => $"[{ArgumentIndex}]{Start}+{Length}";
    }
}
