// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// Provides a reversible, shell-agnostic representation of command-line tokens.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The representation protocol uses a fixed syntax: tokens are separated by whitespace,
    /// tokens that contain special characters are wrapped in double quotes, and special
    /// characters inside quoted tokens are introduced with a backslash. The protocol is
    /// independent of the operating system shell and does not invoke any system command-line
    /// parser.
    /// </para>
    /// <para>
    /// The following escape sequences are supported inside quoted tokens:
    /// <list type="table">
    /// <listheader><term>Sequence</term><description>Meaning</description></listheader>
    /// <item><term><c>\\</c></term><description>Literal backslash (<c>\</c>)</description></item>
    /// <item><term><c>\"</c></term><description>Literal double quote (<c>"</c>)</description></item>
    /// <item><term><c>\n</c></term><description>Line feed (U+000A)</description></item>
    /// <item><term><c>\r</c></term><description>Carriage return (U+000D)</description></item>
    /// <item><term><c>\t</c></term><description>Tab (U+0009)</description></item>
    /// <item><term><c>\0</c></term><description>Null character (U+0000)</description></item>
    /// <item><term><c>\uXXXX</c></term><description>Unicode character by code point (4 hex digits)</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    public static class CommandLineRepresentation
    {
        /// <summary>
        /// Encodes a list of tokens into a single display string that can be read back by
        /// <see cref="Decode"/> or <see cref="TryDecode"/>.
        /// </summary>
        /// <param name="tokens">The tokens to encode.</param>
        /// <returns>A display string representing the tokens.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tokens"/> is <see langword="null"/>.</exception>
        public static string Encode(IReadOnlyList<string> tokens)
        {
            if (tokens is null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            var sb = new StringBuilder();

            for (var i = 0; i < tokens.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(Encode(tokens[i]));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Encodes a single token into a display string that can be read back by
        /// <see cref="Decode"/> or <see cref="TryDecode"/>.
        /// </summary>
        /// <param name="token">The token to encode.</param>
        /// <returns>A display string representing the token.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="token"/> is <see langword="null"/>.</exception>
        public static string Encode(string token)
        {
            if (token is null)
            {
                throw new ArgumentNullException(nameof(token));
            }

            if (token.Length == 0)
            {
                return "\"\"";
            }

            if (!NeedsQuoting(token))
            {
                return token;
            }

            var sb = new StringBuilder();
            sb.Append('"');

            foreach (var c in token)
            {
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\0':
                        sb.Append("\\0");
                        break;
                    default:
                        if (char.IsControl(c))
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
        }

        /// <summary>
        /// Decodes a display string previously produced by <see cref="Encode(IReadOnlyList{string})"/>
        /// into a list of tokens, each annotated with its source range.
        /// </summary>
        /// <param name="text">The display string to decode.</param>
        /// <returns>A list of decoded tokens.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
        /// <exception cref="FormatException">
        /// The text contains an illegal escape sequence or an unclosed quote.
        /// </exception>
        public static IReadOnlyList<RepresentedToken> Decode(string text)
        {
            if (!TryDecode(text, out var tokens, out var error))
            {
                throw new FormatException(error!.Message);
            }

            return tokens;
        }

        /// <summary>
        /// Attempts to decode a display string previously produced by
        /// <see cref="Encode(IReadOnlyList{string})"/> into a list of tokens, each annotated
        /// with its source range.
        /// </summary>
        /// <param name="text">The display string to decode.</param>
        /// <param name="tokens">When successful, the list of decoded tokens.</param>
        /// <param name="error">When unsuccessful, describes the first error encountered.</param>
        /// <returns><see langword="true"/> if the text was decoded successfully; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
        public static bool TryDecode(string text, out IReadOnlyList<RepresentedToken> tokens, out RepresentationError? error)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var result = new List<RepresentedToken>();
            error = null;

            var pos = 0;
            var length = text.Length;

            while (pos < length)
            {
                // Skip leading whitespace.
                while (pos < length && char.IsWhiteSpace(text[pos]))
                {
                    pos++;
                }

                if (pos >= length)
                {
                    break;
                }

                var tokenStart = pos;
                int? quoteStart = null;
                var value = new StringBuilder();
                var inQuote = false;

                while (pos < length)
                {
                    var c = text[pos];

                    if (inQuote)
                    {
                        if (c == '"')
                        {
                            inQuote = false;
                            quoteStart = null;
                            pos++;
                        }
                        else if (c == '\\')
                        {
                            if (!TryProcessEscape(text, ref pos, value, out error))
                            {
                                tokens = System.Array.Empty<RepresentedToken>();
                                return false;
                            }
                        }
                        else
                        {
                            value.Append(c);
                            pos++;
                        }
                    }
                    else
                    {
                        if (char.IsWhiteSpace(c))
                        {
                            break;
                        }

                        if (c == '"')
                        {
                            inQuote = true;
                            quoteStart = pos;
                            pos++;
                        }
                        else if (c == '\\')
                        {
                            if (!TryProcessEscape(text, ref pos, value, out error))
                            {
                                tokens = System.Array.Empty<RepresentedToken>();
                                return false;
                            }
                        }
                        else
                        {
                            value.Append(c);
                            pos++;
                        }
                    }
                }

                if (inQuote)
                {
                    // Unclosed quote: report the position of the opening quote.
                    var errorPosition = quoteStart ?? tokenStart;
                    error = new RepresentationError(
                        errorPosition,
                        "Unclosed quote: a quoted token was not closed before the end of the input.",
                        RepresentationErrorKind.UnclosedQuote);
                    tokens = System.Array.Empty<RepresentedToken>();
                    return false;
                }

                var tokenEnd = pos;
                result.Add(new RepresentedToken(value.ToString(), tokenStart, tokenEnd));
            }

            tokens = result;
            return true;
        }

        private static bool NeedsQuoting(string token)
        {
            foreach (var c in token)
            {
                if (char.IsWhiteSpace(c) || c == '"' || c == '\\' || char.IsControl(c))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryProcessEscape(
            string text,
            ref int pos,
            StringBuilder value,
            out RepresentationError? error)
        {
            var escapeStart = pos;
            pos++; // Move past the backslash.

            if (pos >= text.Length)
            {
                error = new RepresentationError(
                    escapeStart,
                    "Illegal escape: a backslash at the end of the input is not a valid escape sequence.",
                    RepresentationErrorKind.IllegalEscape);
                return false;
            }

            var c = text[pos];

            switch (c)
            {
                case '\\':
                    value.Append('\\');
                    pos++;
                    break;

                case '"':
                    value.Append('"');
                    pos++;
                    break;

                case 'n':
                    value.Append('\n');
                    pos++;
                    break;

                case 'r':
                    value.Append('\r');
                    pos++;
                    break;

                case 't':
                    value.Append('\t');
                    pos++;
                    break;

                case '0':
                    value.Append('\0');
                    pos++;
                    break;

                case 'u':
                    if (!TryProcessUnicodeEscape(text, ref pos, value, out error))
                    {
                        return false;
                    }

                    break;

                default:
                    error = new RepresentationError(
                        escapeStart,
                        $"Illegal escape: '\\{c}' is not a supported escape sequence.",
                        RepresentationErrorKind.IllegalEscape);
                    return false;
            }

            error = null;
            return true;
        }

        private static bool TryProcessUnicodeEscape(
            string text,
            ref int pos,
            StringBuilder value,
            out RepresentationError? error)
        {
            // pos is at 'u'. We need exactly four hexadecimal digits after it.
            var escapeStart = pos - 1; // The backslash.

            if (pos + 5 > text.Length)
            {
                error = new RepresentationError(
                    escapeStart,
                    "Illegal escape: a '\\u' escape must be followed by exactly four hexadecimal digits.",
                    RepresentationErrorKind.InvalidUnicodeEscape);
                return false;
            }

            var hex = text.Substring(pos + 1, 4);

            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
            {
                error = new RepresentationError(
                    escapeStart,
                    $"Illegal escape: '{hex}' is not a valid hexadecimal number.",
                    RepresentationErrorKind.InvalidUnicodeEscape);
                return false;
            }

            value.Append((char)code);
            pos += 5; // Move past 'u' and the four hex digits.

            error = null;
            return true;
        }
    }
}
