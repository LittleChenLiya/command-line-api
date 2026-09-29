// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.Linq;

namespace System.CommandLine.Completions;

internal static class EditorCompletion
{
    public static EditorCompletionList GetCompletions(
        Command command,
        string commandLineText,
        int cursorPosition,
        ParserConfiguration? configuration)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (commandLineText is null)
        {
            throw new ArgumentNullException(nameof(commandLineText));
        }

        if (cursorPosition < 0 || cursorPosition > commandLineText.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(cursorPosition));
        }

        if (cursorPosition > 0 &&
            char.IsHighSurrogate(commandLineText[cursorPosition - 1]))
        {
            throw new ArgumentOutOfRangeException(nameof(cursorPosition));
        }

        if (cursorPosition < commandLineText.Length &&
            char.IsLowSurrogate(commandLineText[cursorPosition]) &&
            cursorPosition > 0 &&
            char.IsHighSurrogate(commandLineText[cursorPosition - 1]))
        {
            throw new ArgumentOutOfRangeException(nameof(cursorPosition));
        }

        configuration ??= new ParserConfiguration();

        var completionConfiguration = new ParserConfiguration
        {
            EnablePosixBundling = configuration.EnablePosixBundling,
            ResponseFileTokenReplacer = null
        };

        var words = CommandLineWordParser.Split(commandLineText);
        var hasActiveWord = CommandLineWordParser.TryGetWordAtCursor(words, cursorPosition, out var activeWord);
        var replacementStart = cursorPosition;
        var replacementEnd = cursorPosition;
        var quoted = false;
        var prefixEnd = cursorPosition;
        var hasResponseFileMarker = false;
        var responseFileMarkerLength = 0;
        var wordToCompleteStart = cursorPosition;

        if (hasActiveWord)
        {
            hasResponseFileMarker = activeWord.RawStart < activeWord.RawEnd &&
                                    commandLineText[activeWord.RawStart] == '@';

            var valueStart = activeWord.ValueStart;
            var valueEnd = activeWord.ValueEnd;
            wordToCompleteStart = valueStart;
            var separatorIndex = FindInlineValueSeparator(commandLineText, activeWord);

            if (separatorIndex >= 0 &&
                cursorPosition > separatorIndex &&
                IsOptionIdentifier(command, commandLineText, separatorIndex, completionConfiguration))
            {
                var inlineValueStart = separatorIndex + 1;
                var inlineValueEnd = activeWord.RawEnd;

                if (inlineValueStart < activeWord.RawEnd && commandLineText[inlineValueStart] == '"')
                {
                    quoted = true;
                    valueStart = inlineValueStart + 1;
                }
                else
                {
                    valueStart = inlineValueStart;
                }
                wordToCompleteStart = valueStart;

                if (inlineValueEnd > valueStart && commandLineText[inlineValueEnd - 1] == '"')
                {
                    quoted = true;
                    valueEnd = inlineValueEnd - 1;
                }
                else
                {
                    valueEnd = inlineValueEnd;
                }
                replacementStart = valueStart;
            }
            else
            {
                quoted = activeWord.Quoted;
            }

            if (hasResponseFileMarker && activeWord.Quoted)
            {
                responseFileMarkerLength = 1;
                wordToCompleteStart = activeWord.RawStart + 2;
                valueStart = activeWord.RawStart;
                valueEnd = activeWord.RawEnd;
                quoted = false;
            }
            else if (hasResponseFileMarker)
            {
                responseFileMarkerLength = 1;
                valueStart++;
                wordToCompleteStart = valueStart;
            }

            replacementStart = valueStart;
            replacementEnd = activeWord.HasOpenQuote && !activeWord.HasCloseQuote
                ? activeWord.RawEnd
                : valueEnd;

            if (replacementStart > cursorPosition)
            {
                replacementStart = cursorPosition;
            }

            if (replacementEnd < cursorPosition)
            {
                replacementEnd = cursorPosition;
            }

            prefixEnd = cursorPosition;
        }

        var prefix = commandLineText.Substring(0, prefixEnd);
        var parseResult = CommandLineParser.Parse(command, prefix, completionConfiguration);
        var completeParseResult = CommandLineParser.Parse(command, commandLineText, completionConfiguration);
        var wordToComplete = commandLineText.Substring(wordToCompleteStart, prefixEnd - wordToCompleteStart).Replace("\"", "");
        var symbolResult = hasActiveWord
            ? SelectSymbolResult(completeParseResult, activeWord.TokenIndex, parseResult)
            : parseResult.SymbolToCompleteForEditor();
        var context = TextCompletionContext.Create(parseResult, prefix, prefixEnd, wordToComplete);
        context.PrefixMatch = true;
        var completionItems = parseResult.GetCompletions(symbolResult, context).ToArray();

        var editorItems = new EditorCompletionItem[completionItems.Length];

        for (var i = 0; i < completionItems.Length; i++)
        {
            var item = completionItems[i];
            var insertText = item.InsertText ?? item.Label;

            if (!quoted && NeedsQuoting(insertText))
            {
                insertText = "\"" + insertText + "\"";
            }

            if (hasResponseFileMarker &&
                insertText.Length > 0 &&
                insertText[0] != '@' &&
                !(insertText.Length > 1 && insertText[0] == '"' && insertText[1] == '@') &&
                responseFileMarkerLength > 0)
            {
                insertText = "@" + insertText;
            }

            editorItems[i] = new EditorCompletionItem(
                item,
                new CompletionReplacementRange(replacementStart, replacementEnd),
                insertText);
        }

        return new EditorCompletionList(
            commandLineText,
            cursorPosition,
            editorItems);
    }

    private static int FindInlineValueSeparator(string commandLineText, CommandLineWord word)
    {
        for (var i = word.RawStart; i < word.RawEnd; i++)
        {
            if (commandLineText[i] == '=' || commandLineText[i] == ':')
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsOptionIdentifier(
        Command command,
        string commandLineText,
        int separatorIndex,
        ParserConfiguration configuration)
    {
        var prefix = commandLineText.Substring(0, separatorIndex);
        var parseResult = CommandLineParser.Parse(command, prefix, configuration);
        return parseResult.Tokens.Count > 0 &&
               parseResult.Tokens[parseResult.Tokens.Count - 1].Type == TokenType.Option;
    }

    private static SymbolResult SelectSymbolResult(
        ParseResult parseResult,
        int activeTokenIndex,
        ParseResult prefixParseResult)
    {
        SymbolResult fallback = prefixParseResult.CommandResult;

        for (var i = parseResult.Tokens.Count - 1; i >= 0; i--)
        {
            var token = parseResult.Tokens[i];

            if (token.Implicit || token.Position != activeTokenIndex)
            {
                continue;
            }

            if (token.Type == TokenType.Option)
            {
                var optionResult = parseResult.GetResult((Option)token.Symbol!);
                if (optionResult is not null)
                {
                    return optionResult;
                }
            }

            if (token.Type == TokenType.Argument)
            {
                foreach (var result in parseResult.CommandResult.AllSymbolResults())
                {
                    if (result is OptionResult { Tokens: { } optionTokens } && optionTokens.Contains(token))
                    {
                        return (OptionResult)result;
                    }

                    if (result is ArgumentResult { Tokens: { } argumentTokens } && argumentTokens.Contains(token))
                    {
                        return (ArgumentResult)result;
                    }
                }

                for (var j = i - 1; j >= 0; j--)
                {
                    var previousToken = parseResult.Tokens[j];
                    if (previousToken.Position != token.Position)
                    {
                        break;
                    }

                    if (previousToken.Type == TokenType.Option)
                    {
                        var optionResult = parseResult.GetResult((Option)previousToken.Symbol!);
                        if (optionResult is not null)
                        {
                            return optionResult;
                        }
                    }
                }
            }
        }

        return fallback;
    }

    private static bool NeedsQuoting(string value)
    {
        if (value.Length == 0 || value[0] == '"' || value[^1] == '"')
        {
            return false;
        }

        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsWhiteSpace(value[i]))
            {
                return true;
            }
        }

        return false;
    }
}
