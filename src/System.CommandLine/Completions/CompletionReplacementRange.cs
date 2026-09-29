// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.Completions;

/// <summary>
/// Describes the portion of command line text replaced by a completion.
/// </summary>
public sealed class CompletionReplacementRange
{
    internal CompletionReplacementRange(int start, int end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// The UTF-16 index, inclusive, at which replacement starts.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// The UTF-16 index, exclusive, at which replacement ends.
    /// </summary>
    public int End { get; }
}
