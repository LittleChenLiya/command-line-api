// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// Describes where a token came from: the command line, or a response file
    /// (including the chain of nested response file includes that led to it).
    /// </summary>
    public sealed class TokenSource
    {
        private IReadOnlyList<TokenSource>? _includeChain;

        /// <param name="filePath">The normalized full path of the response file, or <see langword="null"/> for the command line.</param>
        /// <param name="line">The 1-based line number, or 0 when not applicable.</param>
        /// <param name="column">The 1-based column number, or 0 when not applicable.</param>
        /// <param name="includedFrom">The location of the <c>@file</c> reference that included <paramref name="filePath"/>.</param>
        public TokenSource(string? filePath, int line, int column, TokenSource? includedFrom = null)
        {
            FilePath = filePath;
            Line = line;
            Column = column;
            IncludedFrom = includedFrom;
        }

        /// <summary>
        /// Gets the normalized full path of the response file the token was read from,
        /// or <see langword="null"/> when the token came from the command line.
        /// </summary>
        public string? FilePath { get; }

        /// <summary>
        /// Gets the 1-based line number within <see cref="FilePath"/>, or 0 when not applicable.
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// Gets the 1-based column number within <see cref="FilePath"/>, or 0 when not applicable.
        /// </summary>
        public int Column { get; }

        /// <summary>
        /// Gets the location of the <c>@file</c> reference that included <see cref="FilePath"/>,
        /// or <see langword="null"/> when the token came from the command line.
        /// </summary>
        public TokenSource? IncludedFrom { get; }

        /// <summary>
        /// Gets the chain of nested response file locations that led to this source,
        /// outermost file first, ending with this source itself. Each intermediate entry
        /// is the location of the <c>@file</c> reference that included the next file in
        /// the chain. The command line origin, if any, is reachable via
        /// <see cref="IncludedFrom"/> of the first entry.
        /// </summary>
        public IReadOnlyList<TokenSource> IncludeChain => _includeChain ??= BuildIncludeChain();

        /// <summary>
        /// Gets a value indicating whether the token came from the command line rather than a response file.
        /// </summary>
        public bool IsCommandLine => FilePath is null;

        internal static TokenSource CommandLine { get; } = new(filePath: null, line: 0, column: 0);

        private IReadOnlyList<TokenSource> BuildIncludeChain()
        {
            if (IncludedFrom is null)
            {
                return IsCommandLine ? [] : [this];
            }

            List<TokenSource> chain = [.. IncludedFrom.IncludeChain];
            chain.Add(this);
            return chain;
        }

        /// <inheritdoc />
        public override string ToString() =>
            FilePath is null
                ? "<command line>"
                : Line > 0
                    ? $"{FilePath}({Line},{Column})"
                    : FilePath;
    }
}
