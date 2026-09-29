// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.Parsing;

internal readonly struct CommandLineWord
{
    public CommandLineWord(
        int tokenIndex,
        int rawStart,
        int rawEnd,
        int valueStart,
        int valueEnd,
        bool quoted,
        bool hasOpenQuote,
        bool hasCloseQuote)
    {
        TokenIndex = tokenIndex;
        RawStart = rawStart;
        RawEnd = rawEnd;
        ValueStart = valueStart;
        ValueEnd = valueEnd;
        Quoted = quoted;
        HasOpenQuote = hasOpenQuote;
        HasCloseQuote = hasCloseQuote;
    }

    public int TokenIndex { get; }

    public int RawStart { get; }

    public int RawEnd { get; }

    public int ValueStart { get; }

    public int ValueEnd { get; }

    public bool Quoted { get; }

    public bool HasOpenQuote { get; }

    public bool HasCloseQuote { get; }
}
