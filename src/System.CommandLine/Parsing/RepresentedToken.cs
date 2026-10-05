// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// A token decoded from a command-line representation, annotated with its source range.
    /// </summary>
    /// <remarks>
    /// The range is measured in UTF-16 code units (<see cref="char"/> offsets) into the
    /// original display text. <see cref="Start"/> is the offset of the first character of
    /// the token (including the opening quote, if any) and <see cref="End"/> is the offset
    /// immediately after the last character (including the closing quote, if any).
    /// </remarks>
    public sealed class RepresentedToken
    {
        internal RepresentedToken(string value, int start, int end)
        {
            Value = value;
            Start = start;
            End = end;
        }

        /// <summary>
        /// Gets the decoded string value of the token.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Gets the UTF-16 offset of the start of the token in the source text.
        /// </summary>
        public int Start { get; }

        /// <summary>
        /// Gets the UTF-16 offset immediately after the end of the token in the source text.
        /// </summary>
        public int End { get; }

        /// <summary>
        /// Gets the length of the token in the source text, in UTF-16 code units.
        /// </summary>
        public int Length => End - Start;

        /// <inheritdoc />
        public override string ToString() => Value;
    }
}
