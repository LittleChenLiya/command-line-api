// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.Execution
{
    /// <summary>
    /// A single value read from the original command-line input together with the exact span of the input argument it came from.
    /// </summary>
    public sealed class ParsedTokenValue
    {
        internal ParsedTokenValue(string tokenValue, InputTokenSpan span, bool afterDoubleDash)
        {
            TokenValue = tokenValue;
            Span = span;
            AfterDoubleDash = afterDoubleDash;
        }

        /// <summary>
        /// The value of the token as recognized by the parser.
        /// </summary>
        public string TokenValue { get; }

        /// <summary>
        /// The span of the original input argument from which <see cref="TokenValue"/> was read.
        /// </summary>
        public InputTokenSpan Span { get; }

        /// <summary>
        /// Gets a value indicating whether the token appeared after a <c>--</c> terminator.
        /// </summary>
        public bool AfterDoubleDash { get; }

        /// <inheritdoc />
        public override string ToString() => TokenValue;
    }
}
