// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace System.CommandLine.Parsing;

internal static class CommandLineWordParser
{
    public static List<CommandLineWord> Split(string commandLine)
    {
        var words = new List<CommandLineWord>();
        var rawStart = 0;
        var quoteStart = -1;
        var quoteEnd = -1;
        var hasWord = false;

        for (var position = 0; position <= commandLine.Length; position++)
        {
            var atEnd = position == commandLine.Length;
            var current = atEnd ? '\0' : commandLine[position];

            if (!atEnd && !char.IsWhiteSpace(current))
            {
                if (!hasWord)
                {
                    hasWord = true;
                    rawStart = position;
                    quoteStart = quoteEnd = -1;
                }

                if (current == '"')
                {
                    if (quoteStart < 0)
                    {
                        quoteStart = position;
                    }
                    else if (quoteEnd < 0)
                    {
                        quoteEnd = position;
                    }
                    else
                    {
                        quoteEnd = -1;
                    }
                }

                continue;
            }

            if (!hasWord)
            {
                continue;
            }

            var rawEnd = position;
            var quotedContentStart = rawStart;

            if (commandLine[rawStart] == '@' && rawStart + 1 < rawEnd && commandLine[rawStart + 1] == '"')
            {
                quotedContentStart = rawStart + 1;
            }

            var isQuoted = quoteStart == quotedContentStart;
            var valueStart = isQuoted ? quoteStart + 1 : rawStart;
            var valueEnd = isQuoted ? (quoteEnd >= 0 ? quoteEnd : rawEnd) : rawEnd;

            words.Add(new CommandLineWord(
                words.Count,
                rawStart,
                rawEnd,
                valueStart,
                valueEnd,
                isQuoted,
                quoteStart >= 0,
                quoteEnd >= 0));

            hasWord = false;
        }

        return words;
    }

    public static bool TryGetWordAtCursor(
        IReadOnlyList<CommandLineWord> words,
        int cursorPosition,
        out CommandLineWord word)
    {
        for (var i = 0; i < words.Count; i++)
        {
            var candidate = words[i];
            if (cursorPosition >= candidate.RawStart &&
                cursorPosition <= candidate.RawEnd)
            {
                word = candidate;
                return true;
            }
        }

        word = default;
        return false;
    }
}
