// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Parsing;

namespace System.CommandLine
{
    /// <summary>
    /// Represents the configuration used by the <see cref="CommandLineParser"/>.
    /// </summary>
    public class ParserConfiguration
    {
        /// <summary>
        /// Gets or sets a value that indicates whether the parser recognizes and expands POSIX-style bundled options.
        /// </summary>
        /// <value><see langword="true"/> to parse POSIX bundles; otherwise, <see langword="false"/>.</value>
        /// <remarks>
        /// POSIX conventions recommend that single-character options be allowed to be specified together after a single <c>-</c> prefix. When <see cref="EnablePosixBundling"/> is set to <see langword="true"/>, the following command lines are equivalent:
        ///
        /// <code>
        ///     &gt; myapp -a -b -c
        ///     &gt; myapp -abc
        /// </code>
        ///
        /// If an argument is provided after an option bundle, it applies to the last option in the bundle. When <see cref="EnablePosixBundling"/> is set to <see langword="true"/>, all of the following command lines are equivalent:
        /// <code>
        ///     &gt; myapp -a -b -c arg
        ///     &gt; myapp -abc arg
        ///     &gt; myapp -abcarg
        /// </code>
        ///
        /// </remarks>
        public bool EnablePosixBundling { get; set; } = true;

        /// <summary>
        /// Gets or sets the response file token replacer.
        /// </summary>
        /// <value><see langword="null"/> to disable response files support. By default, the response file token replacer is enabled.</value>
        /// <remarks>
        /// When enabled, any token prefixed with <c>@</c> can be replaced with zero or more other tokens. This is mostly commonly used to expand tokens from response files and interpolate them into a command line prior to parsing.
        /// </remarks>
        public TryReplaceToken? ResponseFileTokenReplacer { get; set; } = StringExtensions.TryReadResponseFile;

        /// <summary>
        /// Gets or sets the response file expander used to expand <c>@file</c> references
        /// before parsing, tracking the source (file, line, column and include chain) of
        /// every token.
        /// </summary>
        /// <value><see langword="null"/> to use <see cref="ResponseFileTokenReplacer"/> instead. By default, no expander is configured.</value>
        /// <remarks>
        /// When an expander is configured, it takes precedence over <see cref="ResponseFileTokenReplacer"/>:
        /// <c>@file</c> references are expanded up front, <c>@@name</c> is treated as the literal
        /// token <c>@name</c>, and tokens after <c>--</c> are not expanded. Tokens and parse errors
        /// produced this way carry a <see cref="TokenSource"/> describing where they came from.
        /// </remarks>
        public ResponseFileExpander? ResponseFileExpander { get; set; }
    }
}
