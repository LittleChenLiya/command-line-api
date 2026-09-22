// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// Configures how <see cref="ResponseFileExpander"/> expands <c>@file</c> response file references.
    /// </summary>
    public sealed class ResponseFileExpansionOptions
    {
        /// <summary>
        /// Gets or sets the base directory that relative <c>@file</c> paths found on the command
        /// line are resolved against. When <see langword="null"/>, the current working directory
        /// is used. Relative <c>@file</c> paths found inside a response file are always resolved
        /// against the directory of the file that contains them.
        /// </summary>
        public string? BaseDirectory { get; set; }

        /// <summary>
        /// Gets or sets the maximum response file nesting depth. The default is 32.
        /// </summary>
        public int MaxDepth { get; set; } = 32;

        /// <summary>
        /// Gets or sets the maximum total number of bytes read from response files during a
        /// single expansion. The default is 10 MB (10 * 1024 * 1024).
        /// </summary>
        public long MaxTotalBytes { get; set; } = 10 * 1024 * 1024;

        /// <summary>
        /// Gets or sets the maximum total number of tokens read from response files during a
        /// single expansion. The default is 100,000.
        /// </summary>
        public int MaxTotalTokens { get; set; } = 100_000;

        /// <summary>
        /// Gets or sets the reader used to load response file contents. When <see langword="null"/>,
        /// files are read from the file system.
        /// </summary>
        public IResponseFileReader? FileReader { get; set; }
    }
}
