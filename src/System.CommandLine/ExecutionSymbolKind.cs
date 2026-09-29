// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine
{
    /// <summary>
    /// Identifies the kind of symbol described by an <see cref="SymbolExecutionDescription"/>.
    /// </summary>
    public enum ExecutionSymbolKind
    {
        /// <summary>
        /// An <see cref="Option"/>.
        /// </summary>
        Option,

        /// <summary>
        /// An <see cref="Argument"/>.
        /// </summary>
        Argument
    }
}
