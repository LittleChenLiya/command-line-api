// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// Identifies the position of one or more tokens in the sequence of arguments passed to the parser.
    /// </summary>
    /// <remarks>
    /// Positions correspond to zero-based indexes of the arguments passed to <c>Parse</c>,
    /// with the exception of an inferred root command token, in which case the first parsed token
    /// is reported at position 0.
    /// Multiple tokens produced from a single input argument (for example options unbundled from
    /// <c>-xyz</c> or a value attached using <c>=</c>) share the same position.
    /// </remarks>
    public readonly struct TokenRange : IEquatable<TokenRange>
    {
        internal TokenRange(int start, int length)
        {
            Start = start;
            Length = length;
        }

        /// <summary>
        /// Gets the zero-based position of the first input argument associated with the value.
        /// The value is <c>-1</c> when the value did not originate from command-line input.
        /// </summary>
        public int Start { get; }

        /// <summary>
        /// Gets the number of consecutive input arguments, starting at <see cref="Start"/>, associated with the value.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets a value indicating whether the range references an input argument.
        /// </summary>
        public bool HasValue => Start >= 0;

        /// <inheritdoc />
        public bool Equals(TokenRange other) => Start == other.Start && Length == other.Length;

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is TokenRange other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => (Start, Length).GetHashCode();

        /// <inheritdoc />
        public override string ToString() => HasValue ? $"[{Start}..{Start + Length})" : "[none]";

        /// <summary>
        /// Checks whether two <see cref="TokenRange"/> instances are equal.
        /// </summary>
        public static bool operator ==(TokenRange left, TokenRange right) => left.Equals(right);

        /// <summary>
        /// Checks whether two <see cref="TokenRange"/> instances are not equal.
        /// </summary>
        public static bool operator !=(TokenRange left, TokenRange right) => !left.Equals(right);
    }
}
