// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.Linq;

namespace System.CommandLine.StructuredBinding
{
    /// <summary>
    /// Binds a <see cref="StructuredCall"/> to a <see cref="Command"/> by generating a token list
    /// that can be handed to the real <c>Parse</c> method, without invoking any handler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The binder resolves the canonical command path against the real command tree, resolves each
    /// option key to the real <see cref="Option"/> symbol (honoring aliases and recursive parent
    /// options), and validates value counts against each option's <see cref="Option.Arity"/>.
    /// </para>
    /// <para>
    /// A token list is generated and passed to the real parser. Values containing spaces, quotes, or
    /// leading dashes are emitted as a single token (using the <c>=</c> separator for non-empty values
    /// so they cannot be split or mistaken for another option; empty strings are emitted as a separate
    /// empty token). The binder returns the <see cref="ParseResult"/> and a token-source mapping only
    /// when the final parse succeeds.
    /// </para>
    /// </remarks>
    public static class StructuredCallBinder
    {
        /// <summary>
        /// Binds the specified structured call to the specified command.
        /// </summary>
        /// <param name="rootCommand">The root command of the command line grammar.</param>
        /// <param name="call">The structured call to bind.</param>
        /// <returns>A <see cref="StructuredCallResult"/> describing the outcome.</returns>
        public static StructuredCallResult Bind(Command rootCommand, StructuredCall call)
        {
            var errors = new List<string>();

            // 1. Resolve the canonical command path against the real command tree.
            //    The tokenizer treats the root command name as implicit (it is never emitted as a
            //    token), so startIndex tells GenerateTokens where the first real command token is.
            var (innermostCommand, commandPathErrors, startIndex) = ResolveCommandPath(rootCommand, call.CommandPath);
            errors.AddRange(commandPathErrors);

            if (innermostCommand is null)
            {
                return StructuredCallResult.Failure(errors);
            }

            // 2. Collect the options visible to the resolved command (own + recursive parent options).
            var visibleOptions = CollectVisibleOptions(innermostCommand);

            // 3. Resolve and validate each option key/value pair.
            var resolvedOptions = new List<ResolvedOption>();
            var specifiedSymbols = new HashSet<Option>();
            var specifiedKeys = new HashSet<string>(System.StringComparer.Ordinal);

            foreach (var kvp in call.Options)
            {
                var key = kvp.Key;
                var values = kvp.Value?.Values ?? System.Array.Empty<string>();

                if (!visibleOptions.TryGetValue(key, out var option))
                {
                    errors.Add(BuildUnknownOptionError(innermostCommand, key));
                    continue;
                }

                // Reject the same option specified through two different aliases before parsing.
                if (!specifiedSymbols.Add(option))
                {
                    errors.Add(BuildDuplicateAliasError(key, option, resolvedOptions));
                    continue;
                }

                // Reject the same key specified more than once.
                if (!specifiedKeys.Add(key))
                {
                    errors.Add($"Option '{key}' was specified more than once.");
                    continue;
                }

                // Validate the value count against the real option arity.
                var arity = option.Arity;
                if (values.Count < arity.MinimumNumberOfValues)
                {
                    errors.Add(
                        $"Option '{key}' requires at least {arity.MinimumNumberOfValues} value(s) " +
                        $"but {values.Count} were supplied.");
                    continue;
                }

                if (values.Count > arity.MaximumNumberOfValues)
                {
                    errors.Add(
                        $"Option '{key}' accepts at most {arity.MaximumNumberOfValues} value(s) " +
                        $"but {values.Count} were supplied.");
                    continue;
                }

                resolvedOptions.Add(new ResolvedOption(key, option, values));
            }

            // 4. Validate required options.
            foreach (var option in visibleOptions.Values.Distinct())
            {
                if (option.Required && !specifiedSymbols.Contains(option))
                {
                    errors.Add($"Required option '{option.Name}' was not provided.");
                }
            }

            if (errors.Count > 0)
            {
                return StructuredCallResult.Failure(errors);
            }

            // 5. Generate the token list and the expected token descriptors.
            var (elements, descriptors) = GenerateTokens(call, resolvedOptions, startIndex);

            // 6. Hand the token list to the real parser (no handler is invoked).
            var parseResult = rootCommand.Parse(elements);

            // 7. Only return when the final parse validation passes.
            if (parseResult.Errors.Count > 0)
            {
                return StructuredCallResult.Failure(
                    parseResult.Errors.Select(e => e.Message).ToList());
            }

            // 8. Build the token-source mapping aligned to the final tokens.
            var mapping = BuildMapping(descriptors, parseResult.Tokens);

            return StructuredCallResult.Success(parseResult, mapping);
        }

        private static (Command? command, List<string> errors, int startIndex) ResolveCommandPath(
            Command rootCommand,
            IReadOnlyList<string> path)
        {
            var errors = new List<string>();
            var current = rootCommand;
            var startIndex = 0;

            // Allow the path to include the root command name as the first element. The tokenizer
            // treats the root command name as implicit, so it is never emitted as a token; startIndex
            // tells GenerateTokens where the first real command token is.
            if (path.Count > 0 &&
                (path[0] == rootCommand.Name || rootCommand.Aliases.Contains(path[0])))
            {
                startIndex = 1;
            }

            for (var i = startIndex; i < path.Count; i++)
            {
                var segment = path[i];
                var child = current.Subcommands.FirstOrDefault(c =>
                    c.Name == segment || c.Aliases.Contains(segment));

                if (child is null)
                {
                    errors.Add(
                        $"Command '{segment}' is not a valid subcommand of '{current.Name}'.");
                    return (null, errors, startIndex);
                }

                current = child;
            }

            return (current, errors, startIndex);
        }

        private static Dictionary<string, Option> CollectVisibleOptions(Command command)
        {
            var result = new Dictionary<string, Option>(System.StringComparer.Ordinal);

            foreach (var current in EnumerateCommandChain(command))
            {
                foreach (var option in current.Options)
                {
                    // The command's own options are always visible; recursive options from parents too.
                    if (current == command || option.Recursive)
                    {
                        AddOption(result, option);
                    }
                }
            }

            return result;
        }

        private static IEnumerable<Command> EnumerateCommandChain(Command command)
        {
            var current = command;
            while (current is not null)
            {
                yield return current;
                current = current.Parents.OfType<Command>().FirstOrDefault();
            }
        }

        private static void AddOption(Dictionary<string, Option> result, Option option)
        {
            if (!result.ContainsKey(option.Name))
            {
                result[option.Name] = option;
            }

            foreach (var alias in option.Aliases)
            {
                if (!result.ContainsKey(alias))
                {
                    result[alias] = option;
                }
            }
        }

        private static (List<string> elements, List<TokenDescriptor> descriptors) GenerateTokens(
            StructuredCall call,
            List<ResolvedOption> resolvedOptions,
            int startIndex)
        {
            var elements = new List<string>();
            var descriptors = new List<TokenDescriptor>();

            // Command path tokens. The root command name (index 0 when it matches the root) is
            // implicit in the tokenizer and must not be emitted as a token.
            for (var i = startIndex; i < call.CommandPath.Count; i++)
            {
                var segment = call.CommandPath[i];
                elements.Add(segment);
                descriptors.Add(new TokenDescriptor(
                    "command",
                    commandPathIndex: i,
                    optionKey: null,
                    optionName: null,
                    valueIndex: null,
                    argumentIndex: null,
                    expectedValue: segment,
                    expectedType: TokenType.Command));
            }

            // Option tokens.
            foreach (var resolved in resolvedOptions)
            {
                var optionName = resolved.Option.Name;

                if (resolved.Values.Count == 0)
                {
                    // Boolean switch: emit just the option token.
                    AddOptionToken(elements, descriptors, resolved, optionName);
                }
                else
                {
                    for (var i = 0; i < resolved.Values.Count; i++)
                    {
                        var value = resolved.Values[i];

                        if (value.Length == 0)
                        {
                            // Empty string: emit the option token followed by a separate empty token.
                            // The tokenizer preserves the empty value as an Argument token.
                            AddOptionToken(elements, descriptors, resolved, optionName);
                            elements.Add(string.Empty);
                            descriptors.Add(new TokenDescriptor(
                                "optionValue",
                                commandPathIndex: null,
                                optionKey: resolved.Key,
                                optionName: optionName,
                                valueIndex: i,
                                argumentIndex: null,
                                expectedValue: string.Empty,
                                expectedType: TokenType.Argument));
                        }
                        else
                        {
                            // Non-empty value: use the '=' separator so the value cannot be split by a
                            // shell or mistaken for another option (leading dashes are safe).
                            elements.Add(optionName + "=" + value);
                            descriptors.Add(new TokenDescriptor(
                                "option",
                                commandPathIndex: null,
                                optionKey: resolved.Key,
                                optionName: optionName,
                                valueIndex: null,
                                argumentIndex: null,
                                expectedValue: optionName,
                                expectedType: TokenType.Option));
                            descriptors.Add(new TokenDescriptor(
                                "optionValue",
                                commandPathIndex: null,
                                optionKey: resolved.Key,
                                optionName: optionName,
                                valueIndex: i,
                                argumentIndex: null,
                                expectedValue: value,
                                expectedType: TokenType.Argument));
                        }
                    }
                }
            }

            // Positional argument tokens. A leading '--' ensures values that start with a dash are
            // never mistaken for options.
            if (call.Arguments.Count > 0)
            {
                elements.Add("--");
                descriptors.Add(new TokenDescriptor(
                    "separator",
                    commandPathIndex: null,
                    optionKey: null,
                    optionName: null,
                    valueIndex: null,
                    argumentIndex: null,
                    expectedValue: "--",
                    expectedType: TokenType.DoubleDash));

                for (var i = 0; i < call.Arguments.Count; i++)
                {
                    var value = call.Arguments[i];
                    elements.Add(value);
                    descriptors.Add(new TokenDescriptor(
                        "argument",
                        commandPathIndex: null,
                        optionKey: null,
                        optionName: null,
                        valueIndex: null,
                        argumentIndex: i,
                        expectedValue: value,
                        expectedType: TokenType.Argument));
                }
            }

            return (elements, descriptors);
        }

        private static void AddOptionToken(
            List<string> elements,
            List<TokenDescriptor> descriptors,
            ResolvedOption resolved,
            string optionName)
        {
            elements.Add(optionName);
            descriptors.Add(new TokenDescriptor(
                "option",
                commandPathIndex: null,
                optionKey: resolved.Key,
                optionName: optionName,
                valueIndex: null,
                argumentIndex: null,
                expectedValue: optionName,
                expectedType: TokenType.Option));
        }

        private static List<TokenSourceMapping> BuildMapping(
            List<TokenDescriptor> descriptors,
            IReadOnlyList<Token> finalTokens)
        {
            var mapping = new List<TokenSourceMapping>(finalTokens.Count);

            // Align descriptors with final tokens. The tokenizer deterministically turns each
            // generated element into one or two tokens, so the order is preserved.
            var index = 0;
            foreach (var descriptor in descriptors)
            {
                if (index < finalTokens.Count)
                {
                    var token = finalTokens[index];
                    mapping.Add(new TokenSourceMapping(
                        token,
                        descriptor.SourceKind,
                        descriptor.CommandPathIndex,
                        descriptor.OptionKey,
                        descriptor.OptionName,
                        descriptor.ValueIndex,
                        descriptor.ArgumentIndex));
                    index++;
                }
            }

            return mapping;
        }

        private static string BuildUnknownOptionError(Command command, string key)
        {
            var message = $"Option '{key}' is not a valid option for command '{command.Name}'.";

            var suggestion = command.Options
                .Select(o => o.Name)
                .Concat(command.Options.SelectMany(o => o.Aliases))
                .Where(n => n is not null && n.StartsWith("-"))
                .Distinct()
                .FirstOrDefault(n => string.Equals(n, key, System.StringComparison.OrdinalIgnoreCase));

            if (suggestion is not null)
            {
                message += $" Did you mean '{suggestion}'?";
            }

            return message;
        }

        private static string BuildDuplicateAliasError(
            string key,
            Option option,
            List<ResolvedOption> resolvedOptions)
        {
            var previous = resolvedOptions.FirstOrDefault(r => r.Option == option);
            var previousKey = previous?.Key ?? option.Name;
            return $"Option '{key}' refers to the same option as '{previousKey}' ({option.Name}). " +
                   "Specify each option only once.";
        }

        private sealed record ResolvedOption(string Key, Option Option, IReadOnlyList<string> Values);

        private sealed class TokenDescriptor
        {
            public TokenDescriptor(
                string sourceKind,
                int? commandPathIndex,
                string? optionKey,
                string? optionName,
                int? valueIndex,
                int? argumentIndex,
                string expectedValue,
                TokenType expectedType)
            {
                SourceKind = sourceKind;
                CommandPathIndex = commandPathIndex;
                OptionKey = optionKey;
                OptionName = optionName;
                ValueIndex = valueIndex;
                ArgumentIndex = argumentIndex;
                ExpectedValue = expectedValue;
                ExpectedType = expectedType;
            }

            public string SourceKind { get; }
            public int? CommandPathIndex { get; }
            public string? OptionKey { get; }
            public string? OptionName { get; }
            public int? ValueIndex { get; }
            public int? ArgumentIndex { get; }
            public string ExpectedValue { get; }
            public TokenType ExpectedType { get; }
        }
    }
}
