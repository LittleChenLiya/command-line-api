// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.CommandLine
{
    /// <summary>
    /// Defines how the value of an environment variable is turned into option values
    /// when <see cref="Option.EnvironmentVariableName"/> is set.
    /// </summary>
    public enum EnvironmentVariableParsingStrategy
    {
        /// <summary>
        /// The strategy is chosen based on the option's arity: options that accept
        /// multiple values use <see cref="Delimited"/>, all other options use
        /// <see cref="SingleValue"/>.
        /// </summary>
        Auto,

        /// <summary>
        /// The entire environment variable value is treated as a single option value,
        /// even when it contains delimiter characters.
        /// </summary>
        SingleValue,

        /// <summary>
        /// The environment variable value is split into multiple option values using
        /// <see cref="Option.EnvironmentVariableValueDelimiter"/> as the separator and
        /// <see cref="Option.EnvironmentVariableEscapeCharacter"/> as the escape character.
        /// </summary>
        Delimited
    }
}
