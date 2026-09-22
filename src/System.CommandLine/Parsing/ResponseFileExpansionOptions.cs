// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// Configures response file expansion performed by <see cref="ResponseFileExpander"/>.
    /// </summary>
    /// <remarks>
    /// All limits (depth, bytes, tokens) apply to the expansion process as a whole,
    /// spanning every nested response file.
    /// </remarks>
    public sealed class ResponseFileExpansionOptions
    {
        /// <summary>
        /// Gets or sets the base directory used to resolve relative response file paths
        /// that appear on the command line. Paths nested inside a response file are always
        /// resolved relative to the file that contains them. When <see langword="null"/>,
        /// the current working directory is used.
        /// </summary>
        public string? BaseDirectory { get; set; }

        /// <summary>
        /// Gets or sets the maximum response file nesting depth. A response file referenced
        /// directly from the command line is at depth 1.
        /// </summary>
        public int MaxDepth { get; set; } = 16;

        /// <summary>
        /// Gets or sets the maximum total number of bytes read from response files
        /// during a single expansion.
        /// </summary>
        public long MaxTotalBytes { get; set; } = 1024 * 1024;

        /// <summary>
        /// Gets or sets the maximum total number of tokens produced by a single expansion,
        /// including tokens that came from the command line.
        /// </summary>
        public int MaxTotalTokens { get; set; } = 10_000;

        /// <summary>
        /// Gets or sets the reader used to load response files. Defaults to
        /// <see cref="Parsing.FileResponseFileReader.Instance"/>. Inject a custom implementation
        /// to expand response files without touching the file system.
        /// </summary>
        public IResponseFileReader FileReader { get; set; } = FileResponseFileReader.Instance;
    }
}
