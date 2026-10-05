// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace System.CommandLine.StructuredBinding
{
    /// <summary>
    /// A structured, already-tokenized description of a command line invocation: a command path,
    /// a set of option key/value entries, and positional arguments.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use <see cref="StructuredCommandLineBinder.Bind"/> to bind an instance to a <see cref="Command"/>
    /// tree. The binder produces a token list that is passed to the parser without involving any shell,
    /// so values that contain spaces, quotes, or a leading dash are preserved verbatim.
    /// </para>
    /// <para>
    /// Multi-value options are expressed by adding several values to a single
    /// <see cref="StructuredOption"/> entry; the binder emits one <c>--alias=value</c> token per value
    /// (the repeated-alias form), which is valid for any option whose arity allows more than one value.
    /// Boolean (zero-arity) switches are expressed as an entry with no values.
    /// </para>
    /// </remarks>
    public sealed class StructuredCommandLine
    {
        /// <summary>
        /// The path of subcommand names or aliases, in order, descending from the root command.
        /// An empty path targets the root command itself.
        /// </summary>
        public IList<string> CommandPath { get; } = new List<string>();

        /// <summary>
        /// The option entries. Each entry's key must be the name or an alias of an option that is
        /// visible from the resolved command (its own options, plus recursive options of its ancestors).
        /// Each option may be specified at most once; specifying the same option through two different
        /// aliases is rejected before parsing.
        /// </summary>
        public IList<StructuredOption> Options { get; } = new List<StructuredOption>();

        /// <summary>
        /// The positional argument values, in order. They are emitted after a <c>--</c> separator so
        /// that values that look like options (for example <c>-x</c> or <c>--name</c>) are still bound
        /// as arguments.
        /// </summary>
        public IList<string> Arguments { get; } = new List<string>();
    }
}
