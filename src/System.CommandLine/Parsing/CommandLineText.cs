// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// Represents a list of argument tokens as text that can be read back without invoking a shell,
    /// and reads such text back into tokens while preserving the range of every token.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The grammar is fixed and independent of any system command line parser:
    /// tokens are separated by whitespace, every token is wrapped in double quotes, and inside the
    /// quotes the backslash is the only escape character. The recognized escape sequences are
    /// <c>\\</c> (backslash), <c>\"</c> (double quote), <c>\n</c> (line feed), <c>\r</c> (carriage
    /// return), <c>\t</c> (tab) and <c>\0</c> (null). Because every token is quoted, empty strings
    /// and tokens containing whitespace or newlines round-trip exactly.
    /// </para>
    /// <para>
    /// Token ranges are reported as UTF-16 code unit offsets into the formatted text (that is,
    /// ordinary <see cref="string"/> indices) and cover the token including its surrounding quotes.
    /// </para>
    /// <para>
    /// The values produced by <see cref="TryParse"/> can be passed directly to
    /// <see cref="CommandLineParser.Parse(Command, IReadOnlyList{string}, ParserConfiguration)"/>.
    /// </para>
    /// </remarks>
    public static class CommandLineText
    {
        /// <summary>
        /// Formats a list of argument tokens as a single string from which the same tokens can be
        /// read back with <see cref="TryParse"/>.
        /// </summary>
        /// <param name="tokens">The tokens to format. <see langword="null"/> elements are treated as empty strings.</param>
        /// <returns>
        /// The formatted text. Every token is wrapped in double quotes and tokens are separated by a
        /// single space, so the result never contains a raw newline and an empty token list formats
        /// as <see cref="string.Empty"/>.
        /// </returns>
        public static string Format(IReadOnlyList<string> tokens)
        {
            if (tokens is null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            StringBuilder builder = new();

            for (var i = 0; i < tokens.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(' ');
                }

                AppendToken(builder, tokens[i] ?? "");
            }

            return builder.ToString();

            static void AppendToken(StringBuilder builder, string token)
            {
                builder.Append('"');

                foreach (var c in token)
                {
                    switch (c)
                    {
                        case '"':
                            builder.Append("\\\"");
                            break;
                        case '\\':
                            builder.Append("\\\\");
                            break;
                        case '\n':
                            builder.Append("\\n");
                            break;
                        case '\r':
                            builder.Append("\\r");
                            break;
                        case '\t':
                            builder.Append("\\t");
                            break;
                        case '\0':
                            builder.Append("\\0");
                            break;
                        default:
                            builder.Append(c);
                            break;
                    }
                }

                builder.Append('"');
            }
        }

        /// <summary>
        /// Reads back text produced by <see cref="Format"/>, recovering every token and its range in
        /// the text.
        /// </summary>
        /// <param name="text">The formatted text.</param>
        /// <param name="tokens">
        /// When this method returns <see langword="true"/>, the parsed tokens in order of appearance,
        /// each carrying its UTF-16 range in <paramref name="text"/>. When this method returns
        /// <see langword="false"/>, this is an empty list: no partial tokens are returned.
        /// </param>
        /// <param name="error">
        /// When this method returns <see langword="false"/>, the first error encountered. An
        /// unterminated quote is reported at the position of the opening quote; an invalid escape
        /// sequence is reported at the position of its backslash.
        /// </param>
        /// <returns><see langword="true"/> if the whole text was parsed successfully; otherwise, <see langword="false"/>.</returns>
        public static bool TryParse(
            string text,
            out IReadOnlyList<CommandLineTextToken> tokens,
            out CommandLineTextError? error)
        {
            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            List<CommandLineTextToken> list = new();

            if (ParseCore(text, list) is { } failure)
            {
                tokens = [];
                error = failure;
                return false;
            }

            tokens = list;
            error = null;
            return true;
        }

        private static CommandLineTextError? ParseCore(string text, List<CommandLineTextToken> list)
        {
            var pos = 0;

            while (pos < text.Length)
            {
                var c = text[pos];

                if (char.IsWhiteSpace(c))
                {
                    pos++;
                    continue;
                }

                if (c != '"')
                {
                    return new CommandLineTextError(CommandLineTextErrorKind.UnexpectedCharacter, pos,
                        $"Unexpected character '{c}' at position {pos}; a token must start with a double quote.");
                }

                var tokenStart = pos;
                pos++; // move past the opening quote

                StringBuilder valueBuilder = new();
                var closed = false;

                while (pos < text.Length)
                {
                    var ch = text[pos];

                    if (ch == '"')
                    {
                        closed = true;
                        pos++;
                        break;
                    }

                    if (ch == '\\')
                    {
                        if (pos + 1 >= text.Length)
                        {
                            return new CommandLineTextError(CommandLineTextErrorKind.InvalidEscape, pos,
                                $"Invalid escape sequence at position {pos}: a backslash must be followed by one of '\\', '\"', 'n', 'r', 't' or '0'.");
                        }

                        var escaped = text[pos + 1];
                        switch (escaped)
                        {
                            case '\\':
                                valueBuilder.Append('\\');
                                break;
                            case '"':
                                valueBuilder.Append('"');
                                break;
                            case 'n':
                                valueBuilder.Append('\n');
                                break;
                            case 'r':
                                valueBuilder.Append('\r');
                                break;
                            case 't':
                                valueBuilder.Append('\t');
                                break;
                            case '0':
                                valueBuilder.Append('\0');
                                break;
                            default:
                                return new CommandLineTextError(CommandLineTextErrorKind.InvalidEscape, pos,
                                    $"Invalid escape sequence '\\{escaped}' at position {pos}.");
                        }

                        pos += 2;
                    }
                    else
                    {
                        valueBuilder.Append(ch);
                        pos++;
                    }
                }

                if (!closed)
                {
                    return new CommandLineTextError(CommandLineTextErrorKind.UnterminatedQuote, tokenStart,
                        $"The quoted token starting at position {tokenStart} is missing a closing double quote.");
                }

                list.Add(new CommandLineTextToken(valueBuilder.ToString(), tokenStart, pos - tokenStart));
            }

            return null;
        }
    }
}
