// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine
{
    /// <summary>
    /// Indicates where the value reported in an execution description came from.
    /// </summary>
    public enum ParseValueOrigin
    {
        /// <summary>
        /// The value was explicitly provided in the parsed command-line input.
        /// </summary>
        Explicit,

        /// <summary>
        /// The value was produced by the symbol's configured default value factory
        /// because no input was provided.
        /// </summary>
        Default
    }
}
