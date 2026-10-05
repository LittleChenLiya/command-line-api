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
        /// <param name="symbolVisibility">
        /// An optional predicate that determines whether a symbol is included in the generated help output.
        /// When <see langword="null"/>, symbols are included unless <see cref="Symbol.Hidden"/> is <see langword="true"/>.
        /// </param>
        public HelpContext(
            HelpBuilder helpBuilder,
            Command command,
            TextWriter output,
            Func<Symbol, bool>? symbolVisibility = null)
        {
            HelpBuilder = helpBuilder ?? throw new ArgumentNullException(nameof(helpBuilder));
            Command = command ?? throw new ArgumentNullException(nameof(command));
            Output = output ?? throw new ArgumentNullException(nameof(output));
            SymbolVisibility = symbolVisibility;
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
        /// Gets the predicate that projects the symbol tree onto a visibility-filtered view for this
        /// help generation pass, or <see langword="null"/> when no projection is applied.
        /// </summary>
        /// <remarks>
        /// The projection only affects the output produced with this context. It never modifies
        /// <see cref="Symbol.Hidden"/>, so the symbol tree and any concurrent help requests are unaffected.
        /// </remarks>
        public Func<Symbol, bool>? SymbolVisibility { get; }

        /// <summary>
        /// Determines whether the specified symbol is visible in the current help view.
        /// </summary>
        /// <param name="symbol">The symbol to check.</param>
        /// <returns>
        /// The result of <see cref="SymbolVisibility"/> when a projection is supplied; otherwise
        /// <see langword="true"/> unless <see cref="Symbol.Hidden"/> is <see langword="true"/>.
        /// </returns>
        public bool IsVisible(Symbol symbol) =>
            SymbolVisibility?.Invoke(symbol) ?? !symbol.Hidden;
    }
}
