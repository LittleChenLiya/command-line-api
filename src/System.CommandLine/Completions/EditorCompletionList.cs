// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace System.CommandLine.Completions;

/// <summary>
/// Provides completions for an editor command line position.
/// </summary>
public sealed class EditorCompletionList
{
    internal EditorCompletionList(
        string commandLineText,
        int cursorPosition,
        IReadOnlyList<EditorCompletionItem> items)
    {
        CommandLineText = commandLineText;
        CursorPosition = cursorPosition;
        Items = items;
    }

    /// <summary>
    /// The complete command line text for which completions were calculated.
    /// </summary>
    public string CommandLineText { get; }

    /// <summary>
    /// The UTF-16 cursor position for which completions were calculated.
    /// </summary>
    public int CursorPosition { get; }

    /// <summary>
    /// The available editor completion items.
    /// </summary>
    public IReadOnlyList<EditorCompletionItem> Items { get; }
}
