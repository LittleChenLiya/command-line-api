// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Binding;
using System.CommandLine.Parsing;

namespace System.CommandLine.Execution
{
    internal static class ExecutionPlanBuilder
    {
        internal static ExecutionPlan Build(ParseResult parseResult)
        {
            IReadOnlyList<Token> allTokens = parseResult.AllTokens;

            int doubleDashIndex = -1;
            for (var i = 0; i < allTokens.Count; i++)
            {
                if (allTokens[i].Type == TokenType.DoubleDash)
                {
                    doubleDashIndex = i;
                }
            }

            var chain = new List<CommandResult>();
            CommandResult? current = parseResult.CommandResult;
            while (current is not null)
            {
                chain.Add(current);
                current = current.Parent as CommandResult;
            }

            chain.Reverse();

            var commands = new List<CommandExecutionPlan>(chain.Count);

            foreach (CommandResult commandResult in chain)
            {
                var options = new List<OptionExecutionPlan>();

                foreach (SymbolResult child in commandResult.Children)
                {
                    if (child is OptionResult optionResult)
                    {
                        options.Add(BuildOption(optionResult, allTokens, doubleDashIndex));
                    }
                }

                var arguments = new List<ArgumentExecutionPlan>();

                foreach (Argument argument in commandResult.Command.Arguments)
                {
                    ArgumentResult? argumentResult = parseResult.GetResult(argument);

                    if (argumentResult is not null && ReferenceEquals(argumentResult.Parent, commandResult))
                    {
                        arguments.Add(BuildArgument(argumentResult, allTokens, doubleDashIndex));
                    }
                }

                commands.Add(new CommandExecutionPlan(
                    commandResult.Command.Name,
                    GetIdentifierSpans(commandResult),
                    options,
                    arguments));
            }

            var unmatchedTokens = new List<ParsedTokenValue>();

            List<Token>? unmatched = parseResult.RootCommandResult.SymbolResultTree.UnmatchedTokens;

            if (unmatched is not null)
            {
                foreach (Token token in unmatched)
                {
                    unmatchedTokens.Add(ToParsedTokenValue(token, allTokens, doubleDashIndex));
                }
            }

            return new ExecutionPlan(commands, unmatchedTokens);
        }

        private static OptionExecutionPlan BuildOption(
            OptionResult optionResult,
            IReadOnlyList<Token> allTokens,
            int doubleDashIndex)
        {
            ArgumentResult argumentResult = (ArgumentResult)optionResult.SymbolResultTree[optionResult.Option.Argument];

            var identifierSpans = new List<InputTokenSpan>(optionResult.IdentifierTokens.Count);

            foreach (Token token in optionResult.IdentifierTokens)
            {
                identifierSpans.Add(ToSpan(token));
            }

            object? value = GetConvertedValue(argumentResult, out string? conversionError);

            return new OptionExecutionPlan(
                optionResult.Option.Name,
                GetSource(optionResult.Implicit, argumentResult),
                value,
                conversionError,
                ToParsedTokenValues(argumentResult.Tokens, allTokens, doubleDashIndex),
                identifierSpans);
        }

        private static ArgumentExecutionPlan BuildArgument(
            ArgumentResult argumentResult,
            IReadOnlyList<Token> allTokens,
            int doubleDashIndex)
        {
            object? value = GetConvertedValue(argumentResult, out string? conversionError);

            return new ArgumentExecutionPlan(
                argumentResult.Argument.Name,
                GetSource(argumentResult.Implicit, argumentResult),
                value,
                conversionError,
                ToParsedTokenValues(argumentResult.Tokens, allTokens, doubleDashIndex));
        }

        private static object? GetConvertedValue(ArgumentResult argumentResult, out string? conversionError)
        {
            conversionError = null;

            ArgumentConversionResult conversionResult = argumentResult.GetArgumentConversionResult();

            if (conversionResult.Result is ArgumentConversionResultType.Successful or ArgumentConversionResultType.NoArgument)
            {
                return conversionResult.Value;
            }

            conversionError = conversionResult.ErrorMessage;
            return null;
        }

        private static ParsedValueSource GetSource(bool implicitResult, ArgumentResult argumentResult) =>
            implicitResult
                ? ParsedValueSource.Default
                : argumentResult.Tokens.Count == 0
                    ? ParsedValueSource.None
                    : ParsedValueSource.Explicit;

        private static IReadOnlyList<ParsedTokenValue> ToParsedTokenValues(
            IReadOnlyList<Token> tokens,
            IReadOnlyList<Token> allTokens,
            int doubleDashIndex)
        {
            var values = new List<ParsedTokenValue>(tokens.Count);

            foreach (Token token in tokens)
            {
                values.Add(ToParsedTokenValue(token, allTokens, doubleDashIndex));
            }

            return values;
        }

        private static ParsedTokenValue ToParsedTokenValue(
            Token token,
            IReadOnlyList<Token> allTokens,
            int doubleDashIndex)
        {
            int index = IndexOf(allTokens, token);
            bool afterDoubleDash = doubleDashIndex >= 0 && index > doubleDashIndex;
            return new ParsedTokenValue(token.Value, ToSpan(token), afterDoubleDash);
        }

        private static int IndexOf(IReadOnlyList<Token> allTokens, Token token)
        {
            for (var i = 0; i < allTokens.Count; i++)
            {
                if (ReferenceEquals(allTokens[i], token))
                {
                    return i;
                }
            }

            return -1;
        }

        private static IReadOnlyList<InputTokenSpan> GetIdentifierSpans(CommandResult commandResult)
        {
            Token identifier = commandResult.IdentifierToken;

            return identifier.Position == Token.ImplicitPosition
                ? System.Array.Empty<InputTokenSpan>()
                : new[] { ToSpan(identifier) };
        }

        private static InputTokenSpan ToSpan(Token token) =>
            new(token.Position, token.SubstringStart, token.SubstringLength);
    }
}
