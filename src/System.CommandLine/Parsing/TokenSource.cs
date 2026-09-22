// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// Describes where a token originated from: the command line itself or a specific
    /// location (file, line, column) inside a response file, including the chain of
    /// <c>@file</c> references that led to it.
    /// </summary>
    public sealed class TokenSource
    {
        /// <summary>
        /// The source used for tokens that were passed directly on the command line.
        /// </summary>
        public static TokenSource CommandLine { get; } = new TokenSource(null, 0, 0);

        /// <param name="filePath">The normalized full path of the response file the token was read from, or <see langword="null"/> for the command line.</param>
        /// <param name="line">The 1-based line number inside the response file, or 0 for the command line.</param>
        /// <param name="column">The 1-based column number inside the response file, or 0 for the command line.</param>
        /// <param name="includedFrom">The source of the <c>@file</c> reference that caused this file to be included, if any.</param>
        public TokenSource(string? filePath, int line, int column, TokenSource? includedFrom = null)
        {
            FilePath = filePath;
            Line = line;
            Column = column;
            IncludedFrom = includedFrom;
        }

        /// <summary>
        /// The normalized full path of the response file the token was read from,
        /// or <see langword="null"/> when the token came from the command line.
        /// </summary>
        public string? FilePath { get; }

        /// <summary>
        /// The 1-based line number inside the response file, or 0 when the token came from the command line.
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// The 1-based column number inside the response file, or 0 when the token came from the command line.
        /// </summary>
        public int Column { get; }

        /// <summary>
        /// The source of the <c>@file</c> reference that caused the file containing this token
        /// to be included, or <see langword="null"/> when the token was not included from another file.
        /// </summary>
        public TokenSource? IncludedFrom { get; }

        /// <summary>
        /// Gets the full include chain for this source, ordered from the outermost
        /// source (typically <see cref="CommandLine"/>) to this source.
        /// </summary>
        public IReadOnlyList<TokenSource> IncludeChain
        {
            get
            {
                var chain = new List<TokenSource>();
                for (TokenSource? source = this; source is not null; source = source.IncludedFrom)
                {
                    chain.Add(source);
                }

                chain.Reverse();
                return chain;
            }
        }

        /// <summary>
        /// Formats the full include chain, from the outermost source to this source,
        /// e.g. <c>&lt;command line&gt; -&gt; C:\root.rsp(1,1) -&gt; C:\sub\child.rsp(2,3)</c>.
        /// </summary>
        public string FormatIncludeChain()
        {
            IReadOnlyList<TokenSource> chain = IncludeChain;
            var builder = new StringBuilder();

            for (var i = 0; i < chain.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(" -> ");
                }

                builder.Append(chain[i].ToString());
            }

            return builder.ToString();
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
