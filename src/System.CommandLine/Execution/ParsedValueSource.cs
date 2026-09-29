// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.Execution
{
    /// <summary>
    /// Indicates whether a parsed value originated from explicit command-line input or from a configured default.
    /// </summary>
    public enum ParsedValueSource
    {
        /// <summary>
        /// No value was provided on the command line and no default value is configured (for example a missing required symbol).
        /// </summary>
        None,

        /// <summary>
        /// The value was explicitly provided on the command line.
        /// </summary>
        Explicit,

        /// <summary>
        /// No value was provided on the command line, so a configured default value is going to be used.
        /// </summary>
        Default
    }
}
