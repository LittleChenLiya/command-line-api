// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// Parses command line input.
    /// </summary>
    public static class CommandLineParser
    {
        /// <summary>
        /// Parses a list of arguments.
        /// </summary>
        /// <param name="command">The command to use to parse the command line input.</param>
        /// <param name="args">The string array typically passed to a program's <c>Main</c> method.</param>
        /// <param name="configuration">The configuration on which the parser's grammar and behaviors are based.</param>
        /// <returns>A <see cref="ParseResult"/> providing details about the parse operation.</returns>
        public static ParseResult Parse(Command command, IReadOnlyList<string> args, ParserConfiguration? configuration = null)
            => Parse(command, args, null, configuration);

        /// <summary>
        /// Parses a command line string.
        /// </summary>
        /// <param name="command">The command to use to parse the command line input.</param>
        /// <param name="commandLine">The complete command line input prior to splitting and tokenization. This input is not typically available when the parser is called from <c>Program.Main</c>. It is primarily used when calculating completions via the <c>dotnet-suggest</c> tool.</param>
        /// <param name="configuration">The configuration on which the parser's grammar and behaviors are based.</param>
        /// <remarks>The command line string input will be split into tokens as if it had been passed on the command line.</remarks>
        /// <returns>A <see cref="ParseResult"/> providing details about the parse operation.</returns>
        public static ParseResult Parse(Command command, string commandLine, ParserConfiguration? configuration = null)
            => Parse(command, SplitCommandLine(commandLine).ToArray(), commandLine, configuration);

        /// <summary>
        /// Splits a string into a sequence of strings based on whitespace and quotation marks.
        /// </summary>
        /// <param name="commandLine">A command line input string.</param>
        /// <returns>A sequence of strings.</returns>
        public static IEnumerable<string> SplitCommandLine(string commandLine)
        {
            foreach (var token in SplitCommandLineWithPositions(commandLine))
            {
                if (token.Kind == CommandLineTokenKind.Token)
                {
                    yield return token.Value;
                }
            }
        }

        internal static IEnumerable<CommandLineTokenInfo> SplitCommandLineWithPositions(string commandLine)
        {
            var memory = commandLine.AsMemory();

            var startTokenIndex = 0;

            var pos = 0;

            var seeking = Boundary.TokenStart;
            var seekingQuote = Boundary.QuoteStart;

            var tokenStartIndex = 0;
            var quoteStartIndex = -1;

            while (pos < memory.Length)
            {
                var c = memory.Span[pos];

                if (char.IsWhiteSpace(c))
                {
                    if (seekingQuote == Boundary.QuoteStart)
                    {
                        switch (seeking)
                        {
                            case Boundary.WordEnd:
                                yield return CurrentToken();
                                startTokenIndex = pos;
                                seeking = Boundary.TokenStart;
                                break;

                            case Boundary.TokenStart:
                                startTokenIndex = pos;
                                break;
                        }
                    }
                }
                else if (c == '\"')
                {
                    if (seeking == Boundary.TokenStart)
                    {
                        switch (seekingQuote)
                        {
                            case Boundary.QuoteEnd:
                                yield return CurrentToken();
                                startTokenIndex = pos;
                                seekingQuote = Boundary.QuoteStart;
                                break;

                            case Boundary.QuoteStart:
                                startTokenIndex = pos + 1;
                                seekingQuote = Boundary.QuoteEnd;
                                tokenStartIndex = pos;
                                quoteStartIndex = pos;
                                break;
                        }
                    }
                    else
                    {
                        switch (seekingQuote)
                        {
                            case Boundary.QuoteEnd:
                                seekingQuote = Boundary.QuoteStart;
                                break;

                            case Boundary.QuoteStart:
                                seekingQuote = Boundary.QuoteEnd;
                                quoteStartIndex = pos;
                                break;
                        }
                    }
                }
                else if (seeking == Boundary.TokenStart && seekingQuote == Boundary.QuoteStart)
                {
                    seeking = Boundary.WordEnd;
                    startTokenIndex = pos;
                    tokenStartIndex = pos;
                }

                Advance();

                if (IsAtEndOfInput())
                {
                    switch (seeking)
                    {
                        case Boundary.TokenStart:
                            break;
                        default:
                            yield return CurrentToken();
                            break;
                    }

                    if (seekingQuote == Boundary.QuoteEnd && quoteStartIndex >= 0)
                    {
                        yield return CommandLineTokenInfo.UnterminatedQuote(quoteStartIndex);
                    }
                }
            }

            void Advance() => pos++;

            CommandLineTokenInfo CurrentToken()
            {
                var value = memory.Slice(startTokenIndex, IndexOfEndOfToken()).ToString().Replace("\"", "");
                return new CommandLineTokenInfo(value, tokenStartIndex);
            }

            int IndexOfEndOfToken() => pos - startTokenIndex;

            bool IsAtEndOfInput() => pos == memory.Length;
        }

        internal enum CommandLineTokenKind
        {
            Token,
            UnterminatedQuote
        }

        internal readonly struct CommandLineTokenInfo
        {
            internal CommandLineTokenInfo(string value, int startIndex)
            {
                Kind = CommandLineTokenKind.Token;
                Value = value;
                StartIndex = startIndex;
            }

            private CommandLineTokenInfo(int unterminatedQuoteIndex)
            {
                Kind = CommandLineTokenKind.UnterminatedQuote;
                Value = "";
                StartIndex = unterminatedQuoteIndex;
            }

            internal CommandLineTokenKind Kind { get; }

            internal string Value { get; }

            internal int StartIndex { get; }

            internal static CommandLineTokenInfo UnterminatedQuote(int quoteIndex) => new(quoteIndex);
        }

        private static ParseResult Parse(
            Command command,
            IReadOnlyList<string> arguments,
            string? rawInput,
            ParserConfiguration? configuration)
        {
            if (arguments is null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            configuration ??= new ParserConfiguration();

            IReadOnlyList<TokenSource>? tokenSources = null;
            List<ParseError>? errors = null;

            if (configuration.ResponseFileExpansion is { } expansionOptions)
            {
                ResponseFileExpansionResult expansion = ResponseFileExpander.Expand(arguments, expansionOptions);

                if (!expansion.Success)
                {
                    errors = new List<ParseError>(expansion.Errors.Count);

                    for (var i = 0; i < expansion.Errors.Count; i++)
                    {
                        ResponseFileExpansionError error = expansion.Errors[i];
                        errors.Add(new ParseError(error.Message, source: error.Source));
                    }

                    arguments = Array.Empty<string>();
                }
                else
                {
                    var expandedArguments = new string[expansion.Arguments.Count];
                    var sources = new TokenSource[expansion.Arguments.Count];

                    for (var i = 0; i < expandedArguments.Length; i++)
                    {
                        expandedArguments[i] = expansion.Arguments[i].Value;
                        sources[i] = expansion.Arguments[i].Source;
                    }

                    arguments = expandedArguments;
                    tokenSources = sources;
                }
            }

            arguments.Tokenize(
                command,
                configuration,
                inferRootCommand: rawInput is not null,
                out List<Token> tokens,
                out List<string>? tokenizationErrors,
                tokenSources);

            if (tokenizationErrors is not null)
            {
                errors ??= new List<ParseError>(tokenizationErrors.Count);

                for (var i = 0; i < tokenizationErrors.Count; i++)
                {
                    errors.Add(new ParseError(tokenizationErrors[i]));
                }
            }

            var operation = new ParseOperation(
                tokens,
                command,
                configuration,
                errors,
                rawInput);

            return operation.Parse();
        }

        private enum Boundary
        {
            TokenStart,
            WordEnd,
            QuoteStart,
            QuoteEnd
        }
    }
}
