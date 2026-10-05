// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace System.CommandLine.Help
{
    /// <summary>
    /// Supports formatting command line help.
    /// </summary>
    internal class HelpContext
    {
        /// <param name="helpBuilder">The current help builder.</param>
        /// <param name="command">The command for which help is being formatted.</param>
        /// <param name="output">A text writer to write output to.</param>
        /// <param name="visibility">An optional visibility view that filters which symbols are displayed without modifying the command tree.</param>
        public HelpContext(
            HelpBuilder helpBuilder,
            Command command,
            TextWriter output,
            HelpVisibility? visibility = null)
        {
            HelpBuilder = helpBuilder ?? throw new ArgumentNullException(nameof(helpBuilder));
            Command = command ?? throw new ArgumentNullException(nameof(command));
            Output = output ?? throw new ArgumentNullException(nameof(output));
            Visibility = visibility;
        }

        /// <summary>
        /// The help builder for the current operation.
        /// </summary>
        public HelpBuilder HelpBuilder { get; }

        /// <summary>
        /// The command for which help is being formatted.
        /// </summary>
        public Command Command { get; }

        /// <summary>
        /// A text writer to write output to.
        /// </summary>
        public TextWriter Output { get; }

        /// <summary>
        /// Gets the visibility view for the current help generation operation, if any.
        /// </summary>
        /// <remarks>
        /// The visibility view is applied only while writing help. It does not modify
        /// the underlying command tree, so the same tree can be used to generate help
        /// for callers with different authorization sets concurrently.
        /// </remarks>
        public HelpVisibility? Visibility { get; }
    }
}
