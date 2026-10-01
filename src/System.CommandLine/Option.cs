// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Completions;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using System.Text;

namespace System.CommandLine
{
    /// <summary>
    /// A symbol defining a named parameter and a value for that parameter. 
    /// </summary>
    public abstract class Option : Symbol
    {
        internal AliasSet? _aliases;
        private List<Action<OptionResult>>? _validators;

        /// <summary>
        /// Initializes a new instance of the <see cref="Option"/> class.
        /// </summary>
        /// <param name="name">The name of the option. This is used during parsing and is displayed in help.</param>
        /// <param name="aliases">Optional aliases by which the option can be specified on the command line.</param>
        protected Option(string name, string[] aliases) : base(name)
        {
            if (aliases is { Length: > 0 }) 
            {
                _aliases = new(aliases);
            }
        }

        /// <summary>
        /// Gets the <see cref="Argument">argument</see> for the option.
        /// </summary>
        internal abstract Argument Argument { get; }

        /// <summary>
        /// Specifies if a default value is defined for the option.
        /// </summary>
        public bool HasDefaultValue => Argument.HasDefaultValue;

        /// <summary>
        /// Gets or sets the placeholder name shown in usage help for the option's value.
        /// The value will be wrapped in angle brackets (<c>&lt;</c> and <c>&gt;</c>).
        /// </summary>
        /// <remarks>
        /// If <c>null</c>, the <see cref="Symbol.Name"/> of the option will be used,
        /// with leading dashes and slashes removed.
        /// </remarks>
        /// <example>
        /// An option with <see cref="Symbol.Name"/> of <c>--option</c> and a
        /// <see cref="HelpName"/> of <c>Value</c> will be shown in usage help as:
        /// <c>--option &lt;Value&gt;</c>. If <see cref="HelpName"/> is not set,
        /// help output will show: <c>--option &lt;option&gt;</c>.
        /// </example>
        /// <value>
        /// The name to show as the placeholder for the option's value.
        /// </value>
        public string? HelpName
        {
            get => Argument.HelpName;
            set => Argument.HelpName = value;
        }

        /// <summary>
        /// Gets or sets the arity of the option.
        /// </summary>
        public ArgumentArity Arity
        {
            get => Argument.Arity;
            set => Argument.Arity = value;
        }

        /// <summary>
        /// When set to true, this option will be applied to its immediate parent command or commands and recursively to their subcommands.
        /// </summary>
        public bool Recursive { get; set; }

        /// <summary>
        /// Gets the <see cref="Type" /> that the option's parsed tokens will be converted to.
        /// </summary>
        public abstract Type ValueType { get; }

        /// <summary>
        /// Validators that will be called when the option is matched by the parser.
        /// </summary>
        public List<Action<OptionResult>> Validators => _validators ??= new();

        internal bool HasValidators => _validators is not null && _validators.Count > 0;

        /// <summary>
        /// Gets the list of completion sources for the option.
        /// </summary>
        public List<Func<CompletionContext, IEnumerable<CompletionItem>>> CompletionSources => Argument.CompletionSources;

        /// <summary>
        /// Gets a value that indicates whether multiple argument tokens are allowed for each option identifier token.
        /// </summary>
        /// <example>
        /// If set to <see langword="true"/>, the following command line is valid for passing multiple arguments:
        /// <code>
        /// > --opt 1 2 3
        /// </code>
        /// The following is equivalent and is always valid:
        /// <code>
        /// > --opt 1 --opt 2 --opt 3
        /// </code>
        /// </example>
        public bool AllowMultipleArgumentsPerToken { get; set; }

        internal virtual bool Greedy
            => Argument.Arity.MinimumNumberOfValues > 0 && Argument.ValueType != typeof(bool);

        /// <summary>
        /// Indicates whether the option is required when its parent command is invoked.
        /// </summary>
        /// <remarks>When an option is required and its parent command is invoked without it, an error results.</remarks>
        public bool Required { get; set; }

        /// <summary>
        /// Gets or sets the name of an environment variable that provides a fallback value
        /// for the option when the option is not specified on the command line.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When set, the value is resolved with the following precedence: an explicit command
        /// line value wins over the environment variable value, which in turn wins over the
        /// option's default value. An environment variable that is set to an empty string is
        /// treated as an explicitly provided (empty) value, not as a missing value.
        /// </para>
        /// <para>
        /// The environment variable value is converted using the same argument converter as
        /// command line values. Conversion failures are reported as parse errors that identify
        /// the environment variable as the source.
        /// </para>
        /// <para>
        /// The environment is read at most once per parse operation; help and completion
        /// actions do not trigger a read. Options that do not set this property are unaffected.
        /// </para>
        /// </remarks>
        public string? EnvironmentVariableName { get; set; }

        /// <summary>
        /// Gets or sets the strategy used to turn the environment variable value into option
        /// values when <see cref="EnvironmentVariableName"/> is set. The default is
        /// <see cref="CommandLine.EnvironmentVariableParsingStrategy.Auto"/>.
        /// </summary>
        public EnvironmentVariableParsingStrategy EnvironmentVariableParsingStrategy { get; set; }

        /// <summary>
        /// Gets or sets the character used to separate multiple values in an environment
        /// variable value when the effective parsing strategy is
        /// <see cref="CommandLine.EnvironmentVariableParsingStrategy.Delimited"/>.
        /// The default is <c>;</c>.
        /// </summary>
        public char EnvironmentVariableValueDelimiter { get; set; } = ';';

        /// <summary>
        /// Gets or sets the character that escapes the delimiter or itself in an environment
        /// variable value when the effective parsing strategy is
        /// <see cref="CommandLine.EnvironmentVariableParsingStrategy.Delimited"/>.
        /// The escape character only has special meaning when immediately followed by the
        /// delimiter or by another escape character; otherwise it is treated literally.
        /// The default is <c>\</c>.
        /// </summary>
        public char EnvironmentVariableEscapeCharacter { get; set; } = '\\';

        /// <summary>
        /// Gets or sets a value indicating whether the option's value is sensitive.
        /// Sensitive values are never echoed in parse error messages or parse diagrams.
        /// </summary>
        public bool IsSensitive { get; set; }

        /// <summary>
        /// Gets the unique set of strings that can be used on the command line to specify the Option.
        /// </summary>
        /// <remarks>The collection does not contain the <see cref="Symbol.Name"/> of the Option.</remarks>
        public ICollection<string> Aliases => _aliases ??= new();

        /// <summary>
        /// Gets or sets the <see cref="CommandLineAction"/> for the Option. The handler represents the action
        /// that will be performed when the Option is invoked.
        /// </summary>
        public virtual CommandLineAction? Action { get; set; }

        /// <inheritdoc />
        public override IEnumerable<CompletionItem> GetCompletions(CompletionContext context)
        {
            List<CompletionItem>? completions = null;

            foreach (var completion in Argument.GetCompletions(context))
            {
                if (completion.Label.ContainsCaseInsensitive(context.WordToComplete))
                {
                    (completions ??= new List<CompletionItem>()).Add(completion);
                }
            }

            if (completions is null)
            {
                return Array.Empty<CompletionItem>();
            }

            return completions
                   .OrderBy(item => item.SortText.IndexOfCaseInsensitive(context.WordToComplete))
                   .ThenBy(symbol => symbol.Label, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the default value for the option.
        /// </summary>
        /// <returns>Returns the default value for the option, if defined. Null otherwise.</returns>
        public object? GetDefaultValue() => Argument.GetDefaultValue();

        /// <summary>
        /// Splits an environment variable value into individual option values according to
        /// <see cref="EnvironmentVariableParsingStrategy"/>,
        /// <see cref="EnvironmentVariableValueDelimiter"/> and
        /// <see cref="EnvironmentVariableEscapeCharacter"/>.
        /// </summary>
        internal IEnumerable<string> GetEnvironmentVariableValues(string value)
        {
            EnvironmentVariableParsingStrategy strategy = EnvironmentVariableParsingStrategy;

            if (strategy == EnvironmentVariableParsingStrategy.Auto)
            {
                strategy = Arity.MaximumNumberOfValues > 1
                    ? EnvironmentVariableParsingStrategy.Delimited
                    : EnvironmentVariableParsingStrategy.SingleValue;
            }

            if (strategy == EnvironmentVariableParsingStrategy.SingleValue)
            {
                // An explicitly set (even empty) environment variable is a provided value.
                yield return value;
                yield break;
            }

            char delimiter = EnvironmentVariableValueDelimiter;
            char escape = EnvironmentVariableEscapeCharacter;
            StringBuilder current = new(value.Length);

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                if (c == escape &&
                    i + 1 < value.Length &&
                    (value[i + 1] == delimiter || value[i + 1] == escape))
                {
                    current.Append(value[i + 1]);
                    i++;
                }
                else if (c == delimiter)
                {
                    yield return current.ToString();
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            yield return current.ToString();
        }
    }
}
