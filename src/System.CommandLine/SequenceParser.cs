// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace System.CommandLine
{
    /// <summary>
    /// Parses a command line containing multiple stages separated by a standalone <c>::</c> argument
    /// into a <see cref="SequenceInvocationPlan"/>.
    /// </summary>
    /// <remarks>
    /// <para>A standalone <c>::</c> argument separates two stages. A standalone <c>::</c> argument
    /// produces a literal <c>::</c> argument within the current stage. After a standalone <c>--</c>
    /// argument, all remaining arguments of the current stage are treated as ordinary arguments and
    /// separators are no longer recognized.</para>
    /// <para>Every stage is parsed and validated before any stage action runs, and each stage gets an
    /// independent <see cref="ParseResult"/>, even when the same <see cref="Command"/> instance is used.</para>
    /// </remarks>
    public static class SequenceParser
    {
        /// <summary>
        /// Splits <paramref name="args"/> into stages and parses and validates every stage up front.
        /// </summary>
        /// <param name="command">The command used to parse each stage.</param>
        /// <param name="args">The argument array typically passed to a program's <c>Main</c> method.
        /// The array is copied; later modifications do not affect the returned plan.</param>
        /// <param name="configuration">The configuration on which the parser's grammar and behaviors are based.</param>
        /// <returns>A <see cref="SequenceInvocationPlan"/> that can be executed exactly once.</returns>
        public static SequenceInvocationPlan ParseSequence(
            this Command command,
            IReadOnlyList<string> args,
            ParserConfiguration? configuration = null)
        {
            if (command is null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            // Snapshot the input so later modifications of the caller's array cannot affect the plan.
            var snapshot = new string[args.Count];
            for (var i = 0; i < args.Count; i++)
            {
                snapshot[i] = args[i];
            }

            var errors = new List<SequenceParseError>();
            var stageArgs = new List<string[]>();
            var stageNumbers = new List<int>();
            var stagePositions = new List<int>();

            var current = new List<string>();
            var currentStart = 0;
            var stageNumber = 1;
            var afterDoubleDash = false;

            for (var i = 0; i < snapshot.Length; i++)
            {
                var arg = snapshot[i];

                if (!afterDoubleDash)
                {
                    if (arg == "--")
                    {
                        afterDoubleDash = true;
                        current.Add(arg);
                        continue;
                    }

                    if (arg == "::")
                    {
                        CloseStage(i);
                        continue;
                    }

                    if (arg == @"\::")
                    {
                        current.Add("::");
                        continue;
                    }
                }

                current.Add(arg);
            }

            CloseStage(snapshot.Length);

            void CloseStage(int separatorPosition)
            {
                if (current.Count == 0)
                {
                    errors.Add(new SequenceParseError(stageNumber, separatorPosition, "The stage is empty."));
                }
                else
                {
                    stageArgs.Add(current.ToArray());
                    stageNumbers.Add(stageNumber);
                    stagePositions.Add(currentStart);
                }

                stageNumber++;
                currentStart = separatorPosition + 1;
                current.Clear();
                afterDoubleDash = false;
            }

            var stages = new ParseResult[stageArgs.Count];

            for (var i = 0; i < stageArgs.Count; i++)
            {
                var parseResult = command.Parse(stageArgs[i], configuration);
                stages[i] = parseResult;

                foreach (var parseError in parseResult.Errors)
                {
                    errors.Add(new SequenceParseError(
                        stageNumbers[i],
                        GetErrorPosition(parseError, stagePositions[i]),
                        parseError.Message));
                }
            }

            return new SequenceInvocationPlan(stages, errors.ToArray());

            static int GetErrorPosition(Parsing.ParseError parseError, int stageStart)
            {
                if (parseError.SymbolResult is { } symbolResult)
                {
                    var tokens = symbolResult.Tokens;

                    for (var i = 0; i < tokens.Count; i++)
                    {
                        var position = tokens[i].Position;

                        if (position >= 0)
                        {
                            return stageStart + position;
                        }
                    }
                }

                return stageStart;
            }
        }
    }
}
