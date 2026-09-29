// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Completions;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace System.CommandLine
{
    /// <summary>
    /// Describes the results of parsing a command line input based on a specific parser configuration.
    /// </summary>
    public sealed class ParseResult
    {
        private readonly CommandResult _rootCommandResult;
        private readonly IReadOnlyList<Token> _unmatchedTokens;
        private readonly IReadOnlyList<string> _arguments;
        private readonly IReadOnlyList<Token> _allTokens;
        private CompletionContext? _completionContext;
        private readonly CommandLineAction? _action;
        private readonly List<CommandLineAction>? _preActions;
        private InvocationConfiguration? _invocationConfiguration;

        internal ParseResult(
            ParserConfiguration configuration,
            CommandResult rootCommandResult,
            CommandResult commandResult,
            List<Token> tokens,
            List<Token>? unmatchedTokens,
            List<ParseError>? errors,
            string? commandLineText = null,
            IReadOnlyList<string>? arguments = null,
            CommandLineAction? action = null,
            List<CommandLineAction>? preActions = null)
        {
            Configuration = configuration;
            _rootCommandResult = rootCommandResult;
            CommandResult = commandResult;
            _action = action;
            _preActions = preActions;
            _arguments = arguments ?? Array.Empty<string>();

            // skip the root command when populating Tokens property
            if (tokens.Count > 1)
            {
                // Since TokenizeResult.Tokens is not public and not used anywhere after the parsing,
                // we take advantage of its mutability and remove the root command token
                // instead of creating a copy of the whole list.
                _allTokens = tokens.ToArray();
                tokens.RemoveAt(0);
                Tokens = tokens;
            }
            else
            {
                _allTokens = tokens.ToArray();
                Tokens = [];
            }

            CommandLineText = commandLineText;
            _unmatchedTokens = unmatchedTokens is null ? Array.Empty<Token>() : unmatchedTokens;
            Errors = errors is not null ? errors : Array.Empty<ParseError>();
        }

        internal static ParseResult Empty() => new RootCommand().Parse(Array.Empty<string>());

        /// <summary>
        /// Gets a result indicating the command specified in the command line input.
        /// </summary>
        public CommandResult CommandResult { get; }

        /// <summary>
        /// Gets the configuration used to produce the parse result.
        /// </summary>
        public ParserConfiguration Configuration { get; }

        /// <summary>
        /// Gets the configuration used to specify command line run-time behavior.
        /// </summary>
        public InvocationConfiguration InvocationConfiguration
        {
            get => _invocationConfiguration ??= new();
            private set => _invocationConfiguration = value;
        }

        /// <summary>
        /// Gets the root command result.
        /// </summary>
        public CommandResult RootCommandResult => _rootCommandResult;

        /// <summary>
        /// Gets the parse errors found while parsing command-line input.
        /// </summary>
        public IReadOnlyList<ParseError> Errors { get; }

        /// <summary>
        /// Gets the tokens identified while parsing command-line input.
        /// </summary>
        public IReadOnlyList<Token> Tokens { get; }

        /// <summary>
        /// Gets the original arguments passed to the parser, in the same order and without any transformation.
        /// </summary>
        /// <remarks>
        /// Indexes exposed from <see cref="Execution.InputTokenSpan.ArgumentIndex"/> refer to positions in this list.
        /// </remarks>
        public IReadOnlyList<string> Arguments => _arguments;

        /// <summary>
        /// Gets every token produced during tokenization, including the root command token and <c>--</c> terminators.
        /// </summary>
        internal IReadOnlyList<Token> AllTokens => _allTokens;

        /// <summary>
        /// Gets the value of a complete command line input prior to splitting and tokenization, when provided.
        /// </summary>
        /// <remarks>This value will not be set when the parser is called from <c>Program.Main</c>. It is primarily used when calculating suggestions via the <c>dotnet-suggest</c> tool.</remarks>
        internal string? CommandLineText { get; }

        /// <summary>
        /// Gets the list of tokens used on the command line that were not matched by the parser.
        /// </summary>
        public IReadOnlyList<string> UnmatchedTokens
            => _unmatchedTokens.Count == 0 ? Array.Empty<string>() : _unmatchedTokens.Select(t => t.Value).ToArray();

        /// <summary>
        /// Gets the completion context for the parse result.
        /// </summary>
        public CompletionContext GetCompletionContext() =>
            _completionContext ??=
                CommandLineText is null
                    ? new CompletionContext(this)
                    : new TextCompletionContext(this, CommandLineText);

        /// <summary>
        /// Gets the parsed or default value for the specified argument.
        /// </summary>
        /// <param name="argument">The argument for which to get a value.</param>
        /// <returns>The parsed value or a configured default.</returns>
        public T? GetValue<T>(Argument<T> argument)
            => RootCommandResult.GetValue(argument);

        /// <summary>
        /// Gets the parsed or default value for the specified option.
        /// </summary>
        /// <param name="option">The option for which to get a value.</param>
        /// <returns>The parsed value or a configured default.</returns>
        public T? GetValue<T>(Option<T> option)
            => RootCommandResult.GetValue(option);

        /// <summary>
        /// Gets the parsed or default value for the specified symbol name, in the context of parsed command (not entire symbol tree).
        /// </summary>
        /// <param name="name">The name of the Symbol for which to get a value.</param>
        /// <returns>The parsed value or a configured default.</returns>
        /// <exception cref="InvalidOperationException">Parsing resulted in one or more errors.</exception>
        /// <exception cref="ArgumentException">No symbol was defined for the given name for the parsed command.</exception>
        /// <exception cref="InvalidCastException">The parsed result cannot be cast to <typeparamref name="T"/>.</exception>
        public T? GetValue<T>(string name)
            => RootCommandResult.GetValue<T>(name);

        /// <summary>
        /// Gets the parsed or default value for the specified required argument or throws.
        /// </summary>
        /// <param name="argument">The argument for which to get a value.</param>
        /// <returns>The parsed value or a configured default.</returns>
        /// <exception cref="InvalidOperationException">The required argument was not parsed or has no default value configured.</exception>
        public T GetRequiredValue<T>(Argument<T> argument)
            => RootCommandResult.GetRequiredValue(argument);

        /// <summary>
        /// Gets the parsed or default value for the specified required option or throws.
        /// </summary>
        /// <param name="option">The option for which to get a value.</param>
        /// <returns>The parsed value or a configured default.</returns>
        /// <exception cref="InvalidOperationException">The required option was not parsed or has no default value configured.</exception>
        public T GetRequiredValue<T>(Option<T> option)
            => RootCommandResult.GetRequiredValue(option);

        /// <summary>
        /// Gets the parsed or default value for the specified required symbol name in the context of the parsed command (not entire symbol tree).
        /// </summary>
        /// <param name="name">The name of the required symbol for which to get a value.</param>
        /// <returns>The parsed value or a configured default.</returns>
        /// <exception cref="InvalidOperationException">Parsing resulted in one or more errors, or the required symbol was not parsed or has no default value configured.</exception>
        /// <exception cref="ArgumentException">No symbol was defined for the given name for the parsed command.</exception>
        /// <exception cref="InvalidCastException">The parsed result cannot be cast to <typeparamref name="T"/>.</exception>
        public T GetRequiredValue<T>(string name)
            => RootCommandResult.GetRequiredValue<T>(name);

        /// <summary>
        /// Builds a read-only description of how the current parse result is going to be executed: the selected command
        /// chain and, for each command, the options and arguments with their canonical names, converted values,
        /// explicit-vs-default provenance, and the exact spans of the original input tokens from which each value came.
        /// </summary>
        /// <remarks>
        /// The result is intended for logging and confirmation screens. Building it does not call
        /// <see cref="object.ToString"/> on converted values and does not perform any side effects beyond those
        /// already performed while parsing.
        /// </remarks>
        /// <returns>A read-only execution plan for this parse result.</returns>
        public Execution.ExecutionPlan GetExecutionPlan() => Execution.ExecutionPlanBuilder.Build(this);

        /// <inheritdoc />
        public override string ToString() => ParseDiagramAction.Diagram(this).ToString();

        /// <summary>
        /// Gets the result, if any, for the specified argument.
        /// </summary>
        /// <param name="argument">The argument for which to find a result.</param>
        /// <returns>A result for the specified argument, or <see langword="null"/> if it was not provided and no default was configured.</returns>
        public ArgumentResult? GetResult(Argument argument) =>
            _rootCommandResult.GetResult(argument);

        /// <summary>
        /// Gets the result, if any, for the specified command.
        /// </summary>
        /// <param name="command">The command for which to find a result.</param>
        /// <returns>A result for the specified command, or <see langword="null"/> if it was not provided.</returns>
        public CommandResult? GetResult(Command command) =>
            _rootCommandResult.GetResult(command);

        /// <summary>
        /// Gets the result, if any, for the specified option.
        /// </summary>
        /// <param name="option">The option for which to find a result.</param>
        /// <returns>A result for the specified option, or <see langword="null"/> if it was not provided and no default was configured.</returns>
        public OptionResult? GetResult(Option option) =>
            _rootCommandResult.GetResult(option);

        /// <summary>
        /// Gets the result, if any, for the specified directive.
        /// </summary>
        /// <param name="directive">The directive for which to find a result.</param>
        /// <returns>A result for the specified directive, or <see langword="null"/> if it was not provided.</returns>
        public DirectiveResult? GetResult(Directive directive) => _rootCommandResult.GetResult(directive);

        /// <summary>
        /// Gets the result, if any, for the specified symbol.
        /// </summary>
        /// <param name="symbol">The symbol for which to find a result.</param>
        /// <returns>A result for the specified symbol, or <see langword="null"/> if it was not provided and no default was configured.</returns>
        public SymbolResult? GetResult(Symbol symbol)
            => _rootCommandResult.SymbolResultTree.TryGetValue(symbol, out SymbolResult? result) ? result : null;

        /// <summary>
        /// Finds a result for a symbol having the specified name anywhere in the parse tree.
        /// </summary>
        /// <param name="name">The name of the symbol for which to find a result.</param>
        /// <returns>A symbol result if the argument was matched by the parser or has a default value; otherwise, <c>null</c>.</returns>
        public SymbolResult? GetResult(string name) =>
            _rootCommandResult.SymbolResultTree.GetResult(name);

        /// <summary>
        /// Gets completions based on a given parse result.
        /// </summary>
        /// <param name="position">The position at which completions are requested.</param>
        /// <returns>A set of completions for completion.</returns>
        public IEnumerable<CompletionItem> GetCompletions(
            int? position = null)
        {
            SymbolResult currentSymbolResult = SymbolToComplete(position);

            Symbol currentSymbol = currentSymbolResult switch
            {
                ArgumentResult argumentResult => argumentResult.Argument,
                OptionResult optionResult => optionResult.Option,
                DirectiveResult directiveResult => directiveResult.Directive,
                _ => ((CommandResult)currentSymbolResult).Command
            };

            var context = GetCompletionContext();

            if (position is not null &&
                context is TextCompletionContext tcc)
            {
                context = tcc.AtCursorPosition(position.Value);
            }

            var completions = currentSymbol.GetCompletions(context);

            string[] optionsWithArgumentLimitReached = currentSymbolResult is CommandResult commandResult
                                                           ? OptionsWithArgumentLimitReached(commandResult)
                                                           : Array.Empty<string>();

            completions =
                completions.Where(item => optionsWithArgumentLimitReached.All(s => s != item.Label));

            return completions;

            static string[] OptionsWithArgumentLimitReached(CommandResult commandResult) =>
                commandResult
                    .Children
                    .OfType<OptionResult>()
                    .Where(c => c.IsArgumentLimitReached)
                    .Select(o => o.Option)
                    .SelectMany(c => new[] { c.Name }.Concat(c.Aliases))
                    .ToArray();
        }

        /// <summary>
        /// Invokes the appropriate command handler for a parsed command-line input.
        /// </summary>
        /// <param name="configuration">The configuration used to define invocation behaviors.</param>
        /// <param name="cancellationToken">A token that can be used to cancel an invocation.</param>
        /// <returns>A task whose result can be used as a process exit code.</returns>
        public Task<int> InvokeAsync(
            InvocationConfiguration? configuration = null,
            CancellationToken cancellationToken = default)
        {
            if (configuration is not null)
            {
                InvocationConfiguration = configuration;
            }

            return InvocationPipeline.InvokeAsync(this, cancellationToken);
        }

        /// <summary>
        /// Invokes the appropriate command handler for a parsed command-line input.
        /// </summary>
        /// <param name="configuration">The configuration used to define invocation behaviors.</param>
        /// <returns>A value that can be used as a process exit code.</returns>
        public int Invoke(InvocationConfiguration? configuration = null)
        {
            var useAsync = false;

            if (configuration is not null)
            {
                InvocationConfiguration = configuration;
            }

            if (Action is AsynchronousCommandLineAction)
            {
                useAsync = true;
            }
            else if (PreActions is not null)
            {
                for (var i = 0; i < PreActions.Count; i++)
                {
                    var action = PreActions[i];
                    if (action is AsynchronousCommandLineAction)
                    {
                        useAsync = true;
                        break;
                    }
                }
            }

            if (useAsync)
            {
                return InvocationPipeline.InvokeAsync(this, CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            else
            {
                return InvocationPipeline.Invoke(this);
            }
        }

        /// <summary>
        /// Gets the <see cref="CommandLineAction"/> for parsed result. The handler represents the action
        /// that will be performed when the parse result is invoked.
        /// </summary>
        public CommandLineAction? Action => _action ?? CommandResult.Command.Action;

        internal IReadOnlyList<CommandLineAction>? PreActions => _preActions;

        private SymbolResult SymbolToComplete(int? position = null)
        {
            var commandResult = CommandResult;

            var allSymbolResultsForCompletion = AllSymbolResultsForCompletion();

            var currentSymbol = allSymbolResultsForCompletion.Last();

            return currentSymbol;

            IEnumerable<SymbolResult> AllSymbolResultsForCompletion()
            {
                foreach (var item in commandResult.AllSymbolResults())
                {
                    if (item is CommandResult command)
                    {
                        yield return command;
                    }
                    else if (item is OptionResult option)
                    {
                        if (WillAcceptAnArgument(this, position, option))
                        {
                            yield return option;
                        }
                    }
                }
            }

            static bool WillAcceptAnArgument(
                ParseResult parseResult,
                int? position,
                OptionResult optionResult)
            {
                if (optionResult.Implicit)
                {
                    return false;
                }

                if (!optionResult.IsArgumentLimitReached)
                {
                    return true;
                }

                var completionContext = parseResult.GetCompletionContext();

                if (completionContext is TextCompletionContext textCompletionContext)
                {
                    if (position.HasValue)
                    {
                        textCompletionContext = textCompletionContext.AtCursorPosition(position.Value);
                    }

                    if (textCompletionContext.WordToComplete.Length > 0)
                    {
                        var tokenToComplete = parseResult.Tokens.Last(t => t.Value == textCompletionContext.WordToComplete);

                        return optionResult.Tokens.Contains(tokenToComplete);
                    }
                }

                return !optionResult.IsArgumentLimitReached;
            }
        }
    }
}
