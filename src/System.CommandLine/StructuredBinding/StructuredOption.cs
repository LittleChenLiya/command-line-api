// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace System.CommandLine.StructuredBinding
{
    /// <summary>
    /// A single structured option entry: an option key (name or alias) and its values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An entry with no values represents a switch (for example a boolean flag). An entry with
    /// several values represents a multi-value option; the values are bound in order and empty
    /// strings are preserved as distinct values.
    /// </para>
    /// </remarks>
    public sealed class StructuredOption
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StructuredOption"/> class.
        /// </summary>
        /// <param name="key">The option name or alias as it would appear on a command line, for example <c>--name</c> or <c>-n</c>.</param>
        /// <param name="values">The values for the option. May be empty for switches.</param>
        public StructuredOption(string key, params string[] values)
            : this(key, (IReadOnlyList<string>)values)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StructuredOption"/> class.
        /// </summary>
        /// <param name="key">The option name or alias as it would appear on a command line, for example <c>--name</c> or <c>-n</c>.</param>
        /// <param name="values">The values for the option. May be empty for switches.</param>
        public StructuredOption(string key, IReadOnlyList<string> values)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Values = values ?? throw new ArgumentNullException(nameof(values));
        }

        /// <summary>
        /// The option name or alias used to identify the option.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// The values supplied for the option, in order. Empty for switches.
        /// </summary>
        public IReadOnlyList<string> Values { get; }
    }
}
