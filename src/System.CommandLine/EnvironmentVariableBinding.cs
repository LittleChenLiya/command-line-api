// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Text;

namespace System.CommandLine
{
    /// <summary>
    /// Configures an <see cref="Option"/> to fall back to an environment variable when the option
    /// is not specified on the command line.
    /// </summary>
    /// <remarks>
    /// When an option with an <see cref="EnvironmentVariableBinding"/> is not provided on the command line,
    /// the parser reads the configured environment variable once per parse operation and converts its value
    /// using the same argument converter that is used for command line values. The precedence is:
    /// an explicit command line value first, then the environment variable value, and finally the
    /// option's default value. An environment variable that is set to an empty string is treated as an
    /// explicitly provided (empty) value, not as an unset variable.
    /// </remarks>
    public sealed class EnvironmentVariableBinding
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EnvironmentVariableBinding"/> class.
        /// </summary>
        /// <param name="variableName">The name of the environment variable to read when the option is not specified on the command line.</param>
        /// <exception cref="ArgumentException"><paramref name="variableName"/> is null, empty, or whitespace.</exception>
        public EnvironmentVariableBinding(string variableName)
        {
            if (string.IsNullOrWhiteSpace(variableName))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(variableName));
            }

            VariableName = variableName;
        }

        /// <summary>
        /// Gets the name of the environment variable that provides the fallback value.
        /// </summary>
        public string VariableName { get; }

        /// <summary>
        /// Gets or sets the character used to separate individual values when the option accepts
        /// multiple arguments. Defaults to <c>;</c>.
        /// </summary>
        /// <remarks>
        /// The delimiter is only applied to options whose arity allows more than one value.
        /// Options that accept a single value always receive the environment variable value verbatim.
        /// </remarks>
        public char Delimiter { get; set; } = ';';

        /// <summary>
        /// Gets or sets the character used to escape the <see cref="Delimiter"/> (or any other character)
        /// so that it is treated as literal text rather than as a separator. Defaults to <c>\</c>.
        /// </summary>
        /// <remarks>
        /// When the escape character precedes any character, that character is appended literally.
        /// For example, with the default settings, <c>a\;b</c> produces the single value <c>a;b</c>
        /// and <c>a\\b</c> produces the single value <c>a\b</c>. A trailing escape character is kept literally.
        /// </remarks>
        public char EscapeCharacter { get; set; } = '\\';

        /// <summary>
        /// Gets or sets a value that indicates whether the option's value is sensitive.
        /// </summary>
        /// <remarks>
        /// When set to <see langword="true"/>, parse errors caused by the environment-provided value do not
        /// echo the value, and the parse diagram masks the value. The environment variable name is still
        /// reported so that the source of the problem remains visible.
        /// </remarks>
        public bool IsSensitive { get; set; }

        /// <summary>
        /// Splits an environment variable value into individual argument values using the configured
        /// <see cref="Delimiter"/> and <see cref="EscapeCharacter"/>.
        /// </summary>
        internal IReadOnlyList<string> SplitValue(string value, bool allowMultipleValues)
        {
            if (!allowMultipleValues)
            {
                return new[] { value };
            }

            if (value.Length == 0)
            {
                // An explicitly empty variable provides an empty collection, not an unset value.
                return Array.Empty<string>();
            }

            List<string> values = new();
            StringBuilder current = new(value.Length);
            bool escaped = false;

            foreach (char c in value)
            {
                if (escaped)
                {
                    current.Append(c);
                    escaped = false;
                }
                else if (c == EscapeCharacter)
                {
                    escaped = true;
                }
                else if (c == Delimiter)
                {
                    values.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            if (escaped)
            {
                current.Append(EscapeCharacter);
            }

            values.Add(current.ToString());

            return values;
        }
    }
}
