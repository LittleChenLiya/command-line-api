// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Generic;
using System.CommandLine.Binding;
using System.Linq;

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// Builds a side-effect-free description of how a <see cref="ParseResult"/> is going to be executed.
    /// </summary>
    internal static class ExecutionPlanBuilder
    {
        internal static ExecutionPlan Build(ParseResult parseResult)
        {
            var chain = GetCommandChain(parseResult.CommandResult);

            // All occurrences of each option identifier token, in input order.
            // A single option object can be shared (e.g. recursive options), but the parse
            // tree contains at most one OptionResult per option object.
            var identifiersByOption = new Dictionary<Option, List<Token>>();

            for (var i = 0; i < parseResult.Tokens.Count; i++)
            {
                Token token = parseResult.Tokens[i];

                if (token.Type == TokenType.Option && token.Symbol is Option option)
                {
                    if (!identifiersByOption.TryGetValue(option, out List<Token>? list))
                    {
                        list = [];
                        identifiersByOption[option] = list;
                    }

                    list.Add(token);
                }
            }

            var commands = new CommandExecutionDescription[chain.Count];

            for (var i = 0; i < chain.Count; i++)
            {
                CommandResult commandResult = chain[i];

                var symbols = new List<SymbolExecutionDescription>();

                foreach (SymbolResult child in commandResult.Children)
                {
                    switch (child)
                    {
                        case OptionResult optionResult:
                            AddOption(symbols, optionResult, identifiersByOption);
                            break;

                        case ArgumentResult argumentResult:
                            AddArgument(symbols, argumentResult);
                            break;
                    }
                }

                IReadOnlyList<string> unmatched =
                    i == chain.Count - 1 ? parseResult.UnmatchedTokens : Array.Empty<string>();

                commands[i] = new CommandExecutionDescription(
                    commandResult.Command.Name,
                    RangeOf(commandResult.IdentifierToken),
                    symbols,
                    unmatched);
            }

            return new ExecutionPlan(commands, parseResult.Errors, parseResult.Tokens);
        }

        private static List<CommandResult> GetCommandChain(CommandResult innermost)
        {
            var chain = new List<CommandResult>();

            CommandResult? current = innermost;
            while (current is not null)
            {
                chain.Add(current);
                current = current.Parent as CommandResult;
            }

            chain.Reverse();
            return chain;
        }

        private static void AddOption(
            List<SymbolExecutionDescription> descriptions,
            OptionResult optionResult,
            Dictionary<Option, List<Token>> identifiersByOption)
        {
            Option option = optionResult.Option;

            identifiersByOption.TryGetValue(option, out List<Token>? identifiers);
            identifiers ??= [];

            var identifierRanges = identifiers.Select(RangeOf).ToArray();

            ArgumentResult argumentResult =
                optionResult.SymbolResultTree.GetResult(option.Argument) is { } result
                    ? result
                    : throw new InvalidOperationException($"Missing argument result for option \"{option.Name}\".");

            IReadOnlyList<ParsedValue> values;

            if (optionResult.Implicit)
            {
                values = BuildValues(argumentResult, ParseValueOrigin.Default);
            }
            else if (option.Argument.IsBoolean())
            {
                // A flag: every identifier occurrence contributes the converted (boolean) value.
                object? convertedValue = GetConvertedValue(argumentResult);

                values = identifiers
                    .Select(token => new ParsedValue(
                        null,
                        convertedValue,
                        ParseValueOrigin.Explicit,
                        RangeOf(token),
                        null))
                    .ToArray();
            }
            else
            {
                values = BuildValues(argumentResult, ParseValueOrigin.Explicit);
            }

            descriptions.Add(new SymbolExecutionDescription(
                ExecutionSymbolKind.Option,
                option.Name,
                values,
                identifierRanges));
        }

        private static void AddArgument(
            List<SymbolExecutionDescription> descriptions,
            ArgumentResult argumentResult)
        {
            ParseValueOrigin origin = argumentResult.Implicit
                                           ? ParseValueOrigin.Default
                                           : ParseValueOrigin.Explicit;

            IReadOnlyList<ParsedValue> values = BuildValues(argumentResult, origin);

            descriptions.Add(new SymbolExecutionDescription(
                ExecutionSymbolKind.Argument,
                argumentResult.Argument.Name,
                values,
                Array.Empty<TokenRange>()));
        }

        private static IReadOnlyList<ParsedValue> BuildValues(
            ArgumentResult argumentResult,
            ParseValueOrigin origin)
        {
            ArgumentConversionResult conversion = GetConversionResult(argumentResult);

            bool success = conversion.Result == ArgumentConversionResultType.Successful ||
                           conversion.Result == ArgumentConversionResultType.NoArgument;

            string? error = success ? null : conversion.ErrorMessage;

            if (origin == ParseValueOrigin.Default)
            {
                // NoArgument for an implicit symbol means there is simply no value to report.
                if (conversion.Result == ArgumentConversionResultType.NoArgument)
                {
                    return Array.Empty<ParsedValue>();
                }

                return new[]
                {
                    new ParsedValue(
                        null,
                        success ? conversion.Value : null,
                        ParseValueOrigin.Default,
                        NoRange,
                        error)
                };
            }

            var tokens = argumentResult.Tokens;

            if (tokens.Count == 0)
            {
                return Array.Empty<ParsedValue>();
            }

            var values = new ParsedValue[tokens.Count];

            // When all tokens were converted successfully, the aggregate conversion result
            // already holds the converted items (or a single scalar). When conversion failed
            // part-way through, convert the individual tokens to report exactly which ones
            // failed without relying on parser-internal token redistribution.
            if (success &&
                conversion.Value is IEnumerable convertedEnumerable &&
                conversion.Value is not string)
            {
                IEnumerator enumerator = convertedEnumerable.GetEnumerator();

                using (enumerator as IDisposable)
                {
                    for (var i = 0; i < tokens.Count; i++)
                    {
                        values[i] = CreateExplicitValue(
                            tokens[i],
                            enumerator.MoveNext() ? enumerator.Current : null,
                            null);
                    }
                }
            }
            else
            {
                for (var i = 0; i < tokens.Count; i++)
                {
                    if (success)
                    {
                        values[i] = CreateExplicitValue(tokens[i], conversion.Value, null);
                    }
                    else
                    {
                        ArgumentConversionResult singleConversion =
                            ArgumentConverter.ConvertToken(argumentResult, tokens[i]);

                        values[i] = CreateExplicitValue(
                            tokens[i],
                            singleConversion.Result == ArgumentConversionResultType.Successful
                                ? singleConversion.Value
                                : null,
                            singleConversion.Result == ArgumentConversionResultType.Successful
                                ? null
                                : (singleConversion.ErrorMessage ?? error));
                    }
                }
            }

            return values;
        }

        private static ParsedValue CreateExplicitValue(
            Token token,
            object? convertedValue,
            string? conversionError) =>
            new(
                token.Value,
                convertedValue,
                ParseValueOrigin.Explicit,
                RangeOf(token),
                conversionError);

        /// <summary>
        /// Returns the conversion result computed while parsing, or converts the explicitly
        /// provided tokens using a detached result so that reporting never invokes user code
        /// (custom parsers, validators, or default value factories) or mutates the parse result.
        /// </summary>
        private static ArgumentConversionResult GetConversionResult(ArgumentResult argumentResult)
        {
            if (argumentResult.CachedConversionResult is { } cached)
            {
                return cached;
            }

            // The value was not converted while parsing. Reporting must not run user code
            // (custom parsers can have side effects), so only built-in string conversion is
            // attempted on a detached result. Custom-parsed values are reported as raw input
            // without a converted value.
            if (argumentResult.Argument.HasCustomParser)
            {
                return ArgumentConversionResult.NotConverted(argumentResult);
            }

            var detachedTree = new SymbolResultTree();
            SymbolResult detachedParent = argumentResult.Parent switch
            {
                OptionResult optionResult => new OptionResult(optionResult.Option, detachedTree),
                CommandResult commandResult => new CommandResult(commandResult.Command, commandResult.IdentifierToken, detachedTree),
                _ => throw new InvalidOperationException("Unexpected symbol result parent.")
            };

            var detachedArgumentResult = new ArgumentResult(argumentResult.Argument, detachedTree, detachedParent);

            for (var i = 0; i < argumentResult.Tokens.Count; i++)
            {
                detachedArgumentResult.AddToken(argumentResult.Tokens[i]);
            }

            ArgumentConverter.TryConvertArgument(detachedArgumentResult, out object? value);

            return (ArgumentConversionResult)value!;
        }

        private static object? GetConvertedValue(ArgumentResult argumentResult)
        {
            ArgumentConversionResult conversion = GetConversionResult(argumentResult);

            return conversion.Result switch
            {
                ArgumentConversionResultType.Successful => conversion.Value,
                ArgumentConversionResultType.NoArgument => true,
                _ => null
            };
        }

        private static TokenRange RangeOf(Token? token) =>
            token is { Position: >= 0 } positionToken
                ? new TokenRange(positionToken.Position, 1)
                : NoRange;

        private static TokenRange NoRange => new(-1, 0);
    }
}
