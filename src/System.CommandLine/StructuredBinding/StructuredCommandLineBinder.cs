// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.Linq;

namespace System.CommandLine.StructuredBinding
{
    /// <summary>
    /// Binds a <see cref="StructuredCommandLine"/> to a <see cref="Command"/> tree, producing the
    /// token list that the equivalent command line would have produced, and parses it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The binder never executes the command handler; it only produces tokens and runs the parser
    /// on them. Because the tokens are handed to the parser directly (no shell and no command line
    /// splitting are involved), values containing spaces, quotes, or a leading dash are preserved
    /// verbatim and are never mistaken for another option:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Non-empty option values are emitted as a single <c>--alias=value</c> token.</description></item>
    /// <item><description>Empty-string option values are emitted as a standalone empty token following the alias token.</description></item>
    /// <item><description>Positional arguments are emitted after a <c>--</c> separator.</description></item>
    /// </list>
    /// <para>
    /// Binding is performed against the real options of the resolved command: option keys are
    /// matched against names and aliases (recursive options of ancestor commands included), value
    /// counts are checked against each option arity, and specifying the same option through two
    /// different aliases is rejected before any token is parsed.
    /// </para>
    /// </remarks>
    public static class StructuredCommandLineBinder
    {
        /// <summary>
        /// Binds the structured input to <paramref name="rootCommand"/> and parses the generated tokens.
        /// </summary>
        /// <param name="rootCommand">The root command to bind against.</param>
        /// <param name="input">The structured invocation to bind.</param>
        /// <param name="configuration">An optional parser configuration passed to the parser.</param>
        /// <returns>
        /// A <see cref="StructuredBindingResult"/>. When <see cref="StructuredBindingResult.Success"/>
        /// is <see langword="true"/>, <see cref="StructuredBindingResult.ParseResult"/> and the
        /// token-to-source mapping are available; otherwise
        /// <see cref="StructuredBindingResult.Errors"/> describes the failure and no handler was run.
        /// </returns>
        public static StructuredBindingResult Bind(
            Command rootCommand,
            StructuredCommandLine input,
            ParserConfiguration? configuration = null)
        {
            if (rootCommand is null)
            {
                throw new ArgumentNullException(nameof(rootCommand));
            }

            if (input is null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            List<StructuredBindingError> errors = new();
            List<string> tokens = new();
            List<StructuredTokenSource> sources = new();

            // 1. Walk the command path.
            Command current = rootCommand;
            for (int i = 0; i < input.CommandPath.Count; i++)
            {
                string segment = input.CommandPath[i];
                Command? next = null;

                foreach (Command subcommand in current.Subcommands)
                {
                    if (subcommand.Name.Equals(segment, StringComparison.Ordinal) ||
                        subcommand.Aliases.Contains(segment))
                    {
                        next = subcommand;
                        break;
                    }
                }

                if (next is null)
                {
                    errors.Add(new StructuredBindingError(
                        StructuredBindingErrorKind.UnknownCommand,
                        $"Command '{segment}' is not a subcommand of '{current.Name}'."));
                    return Failure(errors);
                }

                tokens.Add(segment);
                sources.Add(new StructuredTokenSource(StructuredTokenSourceKind.Command, null, i));
                current = next;
            }

            // 2. Resolve and emit options.
            Dictionary<string, Option> visibleOptions = GetVisibleOptions(current);
            HashSet<Option> alreadyBound = new();

            foreach (StructuredOption entry in input.Options)
            {
                if (!visibleOptions.TryGetValue(entry.Key, out Option? option))
                {
                    errors.Add(new StructuredBindingError(
                        StructuredBindingErrorKind.UnknownOption,
                        $"Option '{entry.Key}' is not recognized by command '{current.Name}'."));
                    continue;
                }

                if (!alreadyBound.Add(option))
                {
                    errors.Add(new StructuredBindingError(
                        StructuredBindingErrorKind.DuplicateOption,
                        $"Option '{option.Name}' was specified more than once ('{entry.Key}'). " +
                        "Specify each option at most once, even through different aliases."));
                    continue;
                }

                EmitOption(entry, option, errors, tokens, sources);
            }

            // 3. Emit positional arguments behind a "--" separator so that values that look like
            //    options are still bound as arguments.
            if (input.Arguments.Count > 0)
            {
                tokens.Add("--");
                sources.Add(new StructuredTokenSource(StructuredTokenSourceKind.Separator, null, -1));

                for (int i = 0; i < input.Arguments.Count; i++)
                {
                    string argument = input.Arguments[i];

                    if (argument is null)
                    {
                        errors.Add(new StructuredBindingError(
                            StructuredBindingErrorKind.InvalidValueCount,
                            $"Positional argument at index {i} is null."));
                        continue;
                    }

                    tokens.Add(argument);
                    sources.Add(new StructuredTokenSource(StructuredTokenSourceKind.Argument, null, i));
                }
            }

            if (errors.Count > 0)
            {
                return Failure(errors);
            }

            // 4. Parse the generated tokens. Handlers are not invoked here.
            ParseResult parseResult = CommandLineParser.Parse(rootCommand, tokens, configuration);

            if (parseResult.Errors.Count > 0)
            {
                return Failure(parseResult.Errors
                    .Select(error => new StructuredBindingError(StructuredBindingErrorKind.ParseFailure, error.Message))
                    .ToArray());
            }

            return new StructuredBindingResult(parseResult, tokens, sources, null);
        }

        private static StructuredBindingResult Failure(IReadOnlyList<StructuredBindingError> errors)
            => new(parseResult: null, tokens: null, tokenSources: null, errors);

        private static void EmitOption(
            StructuredOption entry,
            Option option,
            List<StructuredBindingError> errors,
            List<string> tokens,
            List<StructuredTokenSource> sources)
        {
            ArgumentArity arity = option.Arity;
            int valueCount = entry.Values.Count;

            if (arity.MaximumNumberOfValues == 0)
            {
                if (valueCount > 0)
                {
                    errors.Add(new StructuredBindingError(
                        StructuredBindingErrorKind.InvalidValueCount,
                        $"Option '{entry.Key}' does not accept values, but {valueCount} were provided."));
                    return;
                }

                EmitAlias(entry.Key, tokens, sources);
                return;
            }

            if (valueCount < arity.MinimumNumberOfValues)
            {
                errors.Add(new StructuredBindingError(
                    StructuredBindingErrorKind.InvalidValueCount,
                    $"Option '{entry.Key}' requires at least {arity.MinimumNumberOfValues} value(s), but {valueCount} were provided."));
                return;
            }

            if (valueCount > arity.MaximumNumberOfValues)
            {
                errors.Add(new StructuredBindingError(
                    StructuredBindingErrorKind.InvalidValueCount,
                    $"Option '{entry.Key}' accepts at most {arity.MaximumNumberOfValues} value(s), but {valueCount} were provided."));
                return;
            }

            if (valueCount == 0)
            {
                // A switch, or an option with an optional value used without one.
                EmitAlias(entry.Key, tokens, sources);
                return;
            }

            for (int i = 0; i < valueCount; i++)
            {
                string value = entry.Values[i];

                if (value is null)
                {
                    errors.Add(new StructuredBindingError(
                        StructuredBindingErrorKind.InvalidValueCount,
                        $"Value at index {i} of option '{entry.Key}' is null."));
                    continue;
                }

                if (value.Length == 0)
                {
                    // "--alias=" would carry no value token at all, so an empty string is emitted
                    // as a standalone empty token following the alias token.
                    EmitAlias(entry.Key, tokens, sources);
                    tokens.Add(string.Empty);
                    sources.Add(new StructuredTokenSource(StructuredTokenSourceKind.OptionValue, entry.Key, i));
                }
                else
                {
                    // The "--alias=value" form keeps the value inside a single token, so spaces,
                    // quotes and leading dashes are preserved and never re-examined as options.
                    tokens.Add(entry.Key + "=" + value);
                    sources.Add(new StructuredTokenSource(StructuredTokenSourceKind.OptionValue, entry.Key, i));
                }
            }
        }

        private static void EmitAlias(string key, List<string> tokens, List<StructuredTokenSource> sources)
        {
            tokens.Add(key);
            sources.Add(new StructuredTokenSource(StructuredTokenSourceKind.OptionAlias, key, -1));
        }

        private static Dictionary<string, Option> GetVisibleOptions(Command command)
        {
            Dictionary<string, Option> options = new(StringComparer.Ordinal);

            AddOptions(command.Options, requireRecursive: false);

            // Recursive (global) options of ancestor commands remain available on the
            // resolved command, mirroring how the parser builds its known-token set.
            Command? parent = command.Parents.OfType<Command>().FirstOrDefault();
            while (parent is not null)
            {
                AddOptions(parent.Options, requireRecursive: true);
                parent = parent.Parents.OfType<Command>().FirstOrDefault();
            }

            return options;

            void AddOptions(IList<Option> candidates, bool requireRecursive)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    Option option = candidates[i];

                    if (requireRecursive && !option.Recursive)
                    {
                        continue;
                    }

                    if (!options.ContainsKey(option.Name))
                    {
                        options.Add(option.Name, option);
                    }

                    foreach (string alias in option.Aliases)
                    {
                        if (!options.ContainsKey(alias))
                        {
                            options.Add(alias, option);
                        }
                    }
                }
            }
        }
    }
}
