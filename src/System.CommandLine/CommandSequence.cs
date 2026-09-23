// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.Linq;

namespace System.CommandLine;

/// <summary>
/// Parses a command line input as a sequence of stages that are validated up front and executed in order.
/// </summary>
/// <remarks>
/// Stages are separated by a standalone <c>::</c> argument. A standalone <c>::</c> argument can be used to pass
/// a literal <c>::</c> to a stage (a backslash followed by two colons). Within a stage, a standalone <c>--</c> argument stops separator recognition:
/// every subsequent argument, including <c>::</c>, is passed to that stage as an ordinary argument.
/// </remarks>
public static class CommandSequence
{
    /// <summary>
    /// The standalone argument that separates two stages of a command sequence.
    /// </summary>
    public const string Separator = "::";

    /// <summary>
    /// The standalone argument that is replaced with a literal <see cref="Separator"/> before parsing a stage.
    /// </summary>
    public const string EscapedSeparator = "\\::";

    /// <summary>
    /// Splits, parses, and validates a command line input as a sequence of stages without executing any action.
    /// </summary>
    /// <param name="command">The command used to parse each stage of the sequence.</param>
    /// <param name="args">The string array typically passed to a program's <c>Main</c> method.</param>
    /// <param name="configuration">The configuration on which the parser's grammar and behaviors are based.</param>
    /// <returns>A <see cref="CommandSequencePlan"/> that can be executed exactly once.</returns>
    /// <remarks>
    /// Every stage is parsed with its own <see cref="ParseResult"/>, so values provided in one stage are never
    /// treated as provided in another stage, even when the same <paramref name="command"/> is reused.
    /// The input is copied before parsing; later modifications of <paramref name="args"/> do not affect the plan.
    /// </remarks>
    public static CommandSequencePlan Parse(
        Command command,
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

        // Snapshot the input so that later modifications of the caller's array cannot affect the plan.
        string[] snapshot = args.ToArray();

        configuration ??= new ParserConfiguration();

        SplitStages(snapshot, out List<string[]> stageArguments, out List<int> stageStartPositions);

        var errors = new List<SequenceError>();
        var parseResults = new List<ParseResult>(stageArguments.Count);

        for (int stageIndex = 0; stageIndex < stageArguments.Count; stageIndex++)
        {
            int stageNumber = stageIndex + 1;
            int stageStart = stageStartPositions[stageIndex];
            string[] stageArgs = stageArguments[stageIndex];

            if (stageArgs.Length == 0)
            {
                // For an empty stage, point at the "::" separator that created it.
                int position = stageIndex == 0 ? 0 : stageStart - 1;
                errors.Add(new SequenceError(stageNumber, position, $"Stage {stageNumber} is empty."));
            }

            ParseResult parseResult = command.Parse(stageArgs, configuration);
            parseResults.Add(parseResult);

            for (int i = 0; i < parseResult.Errors.Count; i++)
            {
                ParseError error = parseResult.Errors[i];
                errors.Add(new SequenceError(stageNumber, GetOriginalPosition(error, stageStart), error.Message));
            }
        }

        return new CommandSequencePlan(parseResults, errors);
    }

    /// <summary>
    /// Splits, parses, and validates a command line string as a sequence of stages without executing any action.
    /// </summary>
    /// <param name="command">The command used to parse each stage of the sequence.</param>
    /// <param name="commandLine">The complete command line input prior to splitting and tokenization.</param>
    /// <param name="configuration">The configuration on which the parser's grammar and behaviors are based.</param>
    /// <returns>A <see cref="CommandSequencePlan"/> that can be executed exactly once.</returns>
    public static CommandSequencePlan Parse(
        Command command,
        string commandLine,
        ParserConfiguration? configuration = null)
    {
        if (commandLine is null)
        {
            throw new ArgumentNullException(nameof(commandLine));
        }

        return Parse(command, CommandLineParser.SplitCommandLine(commandLine).ToArray(), configuration);
    }

    private static void SplitStages(
        string[] args,
        out List<string[]> stageArguments,
        out List<int> stageStartPositions)
    {
        stageArguments = new List<string[]>();
        stageStartPositions = new List<int>();

        List<string> currentStage = new();
        int currentStageStart = 0;
        bool afterDoubleDash = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (!afterDoubleDash)
            {
                switch (arg)
                {
                    case "--":
                        // Everything after "--" is an ordinary argument for the current stage;
                        // separators are no longer recognized.
                        afterDoubleDash = true;
                        currentStage.Add(arg);
                        continue;

                    case Separator:
                        stageArguments.Add(currentStage.ToArray());
                        stageStartPositions.Add(currentStageStart);
                        currentStage = new List<string>();
                        currentStageStart = i + 1;
                        continue;

                    case EscapedSeparator:
                        currentStage.Add(Separator);
                        continue;
                }
            }

            currentStage.Add(arg);
        }

        stageArguments.Add(currentStage.ToArray());
        stageStartPositions.Add(currentStageStart);
    }

    private static int GetOriginalPosition(ParseError error, int stageStart)
    {
        if (error.SymbolResult is not null)
        {
            int minPosition = int.MaxValue;
            IReadOnlyList<Token> tokens = error.SymbolResult.Tokens;

            for (int i = 0; i < tokens.Count; i++)
            {
                if (tokens[i].Position >= 0 && tokens[i].Position < minPosition)
                {
                    minPosition = tokens[i].Position;
                }
            }

            if (minPosition != int.MaxValue)
            {
                return stageStart + minPosition;
            }
        }

        return stageStart;
    }
}
