// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.Completions;

/// <summary>
/// Provides a completion item and the editor text edit used to apply it.
/// </summary>
public sealed class EditorCompletionItem
{
    internal EditorCompletionItem(
        CompletionItem completionItem,
        CompletionReplacementRange replacementRange,
        string insertText)
    {
        CompletionItem = completionItem;
        ReplacementRange = replacementRange;
        InsertText = insertText;
    }

    /// <summary>
    /// The completion item returned by the configured completion source.
    /// </summary>
    public CompletionItem CompletionItem { get; }

    /// <summary>
    /// The text displayed to the user.
    /// </summary>
    public string DisplayText => CompletionItem.Label;

    /// <summary>
    /// The UTF-16 range in the original command line that <see cref="InsertText"/> replaces.
    /// </summary>
    public CompletionReplacementRange ReplacementRange { get; }

    /// <summary>
    /// The text to insert at <see cref="ReplacementRange"/>.
    /// </summary>
    public string InsertText { get; }
}
