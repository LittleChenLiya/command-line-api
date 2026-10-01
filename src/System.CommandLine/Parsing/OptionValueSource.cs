// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// Identifies where the value of an <see cref="OptionResult"/> came from.
    /// </summary>
    public enum OptionValueSource
    {
        /// <summary>
        /// No value was provided for the option.
        /// </summary>
        NotProvided,

        /// <summary>
        /// The value was provided explicitly on the command line.
        /// </summary>
        CommandLine,

        /// <summary>
        /// The value was read from the environment variable named by
        /// <see cref="Option.EnvironmentVariableName"/>.
        /// </summary>
        EnvironmentVariable,

        /// <summary>
        /// The value is the option's default value.
        /// </summary>
        DefaultValue
    }
}
