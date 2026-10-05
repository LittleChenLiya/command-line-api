// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace System.CommandLine.Help
{
    /// <summary>
    /// Represents a visibility view over a command tree that determines which
    /// symbols are included in help output for a single help generation operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="HelpVisibility"/> does not modify the underlying command tree.
    /// It only filters what is displayed, so the same command tree can be used to
    /// generate help for callers with different authorization sets while remaining
    /// fully parseable.
    /// </para>
    /// <para>
    /// Because the view is applied only while writing help (it is carried on the
    /// per-generation <see cref="HelpContext"/>), multiple help views can be generated
    /// concurrently from the same command tree without interfering with one another.
    /// </para>
    /// </remarks>
    public sealed class HelpVisibility
    {
        private readonly Func<Symbol, bool> _isVisible;

        /// <summary>
        /// Initializes a new instance of the <see cref="HelpVisibility"/> class.
        /// </summary>
        /// <param name="isVisible">A predicate that returns <see langword="true"/> for symbols that should be visible in help output.</param>
        public HelpVisibility(Func<Symbol, bool> isVisible)
        {
            _isVisible = isVisible ?? throw new ArgumentNullException(nameof(isVisible));
        }

        /// <summary>
        /// Gets a visibility view in which all symbols are visible (subject to <see cref="Symbol.Hidden"/>).
        /// </summary>
        public static HelpVisibility All { get; } = new(_ => true);

        /// <summary>
        /// Determines whether the specified symbol is visible in help output.
        /// </summary>
        /// <param name="symbol">The symbol to check.</param>
        /// <returns><see langword="true"/> if the symbol is visible; otherwise, <see langword="false"/>.</returns>
        public bool IsVisible(Symbol symbol) => _isVisible(symbol);
    }
}
