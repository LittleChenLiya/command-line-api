// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;

namespace System.CommandLine.Help
{
    /// <summary>
    /// Provides a projection API that renders a visibility-filtered view of help output for a single
    /// help generation pass, without modifying the underlying symbol tree.
    /// </summary>
    /// <remarks>
    /// The projection is scoped to the generated output: <see cref="Symbol.Hidden"/> is never changed,
    /// the parse tree keeps recognizing the full, original command tree, and concurrent help requests
    /// against the same tree are unaffected. Global (recursive) option inheritance and argument ordering
    /// follow the standard help layout; only the set of visible symbols differs.
    /// </remarks>
    public static class HelpProjection
    {
        /// <summary>
        /// Writes help output for the specified command, including only the symbols accepted by
        /// <paramref name="isVisible"/>.
        /// </summary>
        /// <param name="command">The command for which help is being formatted. The command tree is not modified.</param>
        /// <param name="output">A text writer to write output to.</param>
        /// <param name="isVisible">
        /// A predicate, typically derived from the caller's authorization set, that determines whether a
        /// symbol (command, option, or argument) appears in the generated help. Symbols rejected by the
        /// predicate contribute no aliases, usage lines, or default values to the output. When supplied,
        /// the predicate replaces the default <see cref="Symbol.Hidden"/> check; compose it with
        /// <c>!symbol.Hidden</c> to honor both.
        /// </param>
        /// <param name="maxWidth">The maximum width in characters after which help output is wrapped.</param>
        public static void Write(Command command, TextWriter output, Func<Symbol, bool> isVisible, int maxWidth = int.MaxValue)
        {
            if (command is null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (isVisible is null)
            {
                throw new ArgumentNullException(nameof(isVisible));
            }

            var builder = new HelpBuilder(maxWidth);
            builder.Write(new HelpContext(builder, command, output, isVisible));
        }
    }
}
