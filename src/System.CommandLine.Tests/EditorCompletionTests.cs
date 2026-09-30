// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Completions;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace System.CommandLine.Tests;

public class EditorCompletionTests
{
    [Fact]
    public void Mid_option_value_completion_replaces_only_the_current_value_fragment()
    {
        var nameOption = new Option<string>("--name");
        nameOption.CompletionSources.Add("alpha", "alice smith");
        var command = new Command("tool")
        {
            nameOption,
            new Option<bool>("--flag")
        };
        var commandLine = "tool --name abcd --flag";
        var cursorPosition = commandLine.IndexOf("b");

        var completions = command.GetEditorCompletions(commandLine, cursorPosition);
        var completion = completions.Items.Should().ContainSingle(item => item.DisplayText == "alpha").Subject;

        completion.ReplacementRange.Start.Should().Be(commandLine.IndexOf("abcd"));
        completion.ReplacementRange.End.Should().Be(commandLine.IndexOf(" --flag"));
        completion.InsertText.Should().Be("alpha");

        var completed = commandLine.Substring(0, completion.ReplacementRange.Start) +
                        completion.InsertText +
                        commandLine.Substring(completion.ReplacementRange.End);

        completed.Should().Be("tool --name alpha --flag");
    }

    [Fact]
    public void Value_containing_whitespace_is_quoted_when_inserted_unquoted()
    {
        var nameOption = new Option<string>("--name");
        nameOption.CompletionSources.Add("alpha", "alice smith");
        var command = new Command("tool") { nameOption };
        var commandLine = "tool --name al";

        var completion = command.GetEditorCompletions(commandLine, commandLine.Length)
                              .Items.Should().ContainSingle(item => item.DisplayText == "alice smith").Subject;

        completion.InsertText.Should().Be("\"alice smith\"");
        completion.ReplacementRange.Start.Should().Be(commandLine.IndexOf("al"));
        completion.ReplacementRange.End.Should().Be(commandLine.Length);
    }

    [Fact]
    public void Cursor_position_inside_quoted_value_preserves_existing_quote()
    {
        var nameOption = new Option<string>("--name");
        nameOption.CompletionSources.Add("alpha", "alice smith");
        var command = new Command("tool") { nameOption };
        var commandLine = "tool --name \"al\" --flag";
        var cursorPosition = commandLine.IndexOf("al") + 1;

        var completion = command.GetEditorCompletions(commandLine, cursorPosition)
                              .Items.Should().ContainSingle(item => item.DisplayText == "alice smith").Subject;

        completion.ReplacementRange.Start.Should().Be(commandLine.IndexOf("al"));
        completion.ReplacementRange.End.Should().Be(commandLine.LastIndexOf('"'));
        completion.InsertText.Should().Be("alice smith");

        var completed = commandLine.Substring(0, completion.ReplacementRange.Start) +
                        completion.InsertText +
                        commandLine.Substring(completion.ReplacementRange.End);

        completed.Should().Be("tool --name \"alice smith\" --flag");
    }

    [Fact]
    public void Inline_option_value_completion_replaces_text_after_delimiter()
    {
        var nameOption = new Option<string>("--name");
        nameOption.CompletionSources.Add("alpha", "alice smith");
        var command = new Command("tool") { nameOption };
        var commandLine = "tool --name=a";

        var completion = command.GetEditorCompletions(commandLine, commandLine.Length)
                              .Items.Should().ContainSingle(item => item.DisplayText == "alpha").Subject;

        completion.ReplacementRange.Start.Should().Be(commandLine.LastIndexOf("a"));
        completion.ReplacementRange.End.Should().Be(commandLine.Length);
        completion.InsertText.Should().Be("alpha");
    }

    [Fact]
    public void Inline_option_value_completion_supports_colon_delimiter()
    {
        var nameOption = new Option<string>("--name");
        nameOption.CompletionSources.Add("alpha", "alice smith");
        var command = new Command("tool") { nameOption };
        var commandLine = "tool --name:a";

        var completion = command.GetEditorCompletions(commandLine, commandLine.Length)
                              .Items.Should().ContainSingle(item => item.DisplayText == "alpha").Subject;

        completion.ReplacementRange.Start.Should().Be(commandLine.LastIndexOf("a"));
        completion.ReplacementRange.End.Should().Be(commandLine.Length);
    }

    [Fact]
    public void Partial_subcommand_uses_subcommand_completion_provider()
    {
        var command = new Command("tool")
        {
            new Command("alpha")
        };
        var commandLine = "tool al";

        var completion = command.GetEditorCompletions(commandLine, commandLine.Length)
                              .Items.Should().ContainSingle(item => item.DisplayText == "alpha").Subject;

        completion.ReplacementRange.Should().BeEquivalentTo(new
        {
            Start = commandLine.IndexOf("al"),
            End = commandLine.Length
        });
    }

    [Fact]
    public void Mid_argument_completion_does_not_let_right_text_change_symbol()
    {
        var command = new Command("tool")
        {
            new Argument<string>("first"),
            new Argument<string>("second")
        };
        command.Arguments[0].CompletionSources.Add("alpha");
        var commandLine = "tool alph rest";
        var cursorPosition = commandLine.IndexOf("rest") - 1;

        var completion = command.GetEditorCompletions(commandLine, cursorPosition)
                              .Items.Should().ContainSingle(item => item.DisplayText == "alpha").Subject;
        var completed = commandLine.Substring(0, completion.ReplacementRange.Start) +
                        completion.InsertText +
                        commandLine.Substring(completion.ReplacementRange.End);

        completed.Should().Be("tool alpha rest");
    }

    [Fact]
    public void Empty_word_has_empty_replacement_range()
    {
        var nameOption = new Option<string>("--name");
        nameOption.CompletionSources.Add("alpha", "alice smith");
        var command = new Command("tool") { nameOption };
        var commandLine = "tool --name ";

        var completion = command.GetEditorCompletions(commandLine, commandLine.Length)
                              .Items.Should().ContainSingle(item => item.DisplayText == "alpha").Subject;

        completion.ReplacementRange.Start.Should().Be(commandLine.Length);
        completion.ReplacementRange.End.Should().Be(commandLine.Length);
    }

    [Fact]
    public void Backslash_is_preserved_and_not_treated_as_an_escape()
    {
        var command = new Command("tool") { new Argument<string>("path") };
        command.Arguments.Single().CompletionSources.Add("a\\b", "c:\\d e");
        var commandLine = "tool a" + "\\b";

        var completions = command.GetEditorCompletions(commandLine, commandLine.Length).Items;

        completions.Should().ContainSingle(item => item.DisplayText == "a\\b")
                   .Which.InsertText.Should().Be("a\\b");
        completions.Should().ContainSingle(item => item.DisplayText == "c:\\d e")
                   .Which.InsertText.Should().Be("\"c:\\d e\"");
    }

    [Fact]
    public void Chinese_text_uses_UTF16_cursor_positions()
    {
        var command = new Command("tool") { new Argument<string>("word") };
        command.Arguments.Single().CompletionSources.Add("中文候选");
        var commandLine = "tool 中";

        var completion = command.GetEditorCompletions(commandLine, commandLine.Length)
                              .Items.Should().ContainSingle(item => item.DisplayText == "中文候选").Subject;

        completion.ReplacementRange.Start.Should().Be(5);
        completion.ReplacementRange.End.Should().Be(6);
        completion.InsertText.Should().Be("中文候选");
    }

    [Fact]
    public void Cursor_cannot_split_a_UTF16_surrogate_pair()
    {
        var command = new Command("tool");
        var commandLine = "tool 😀";

        Action act = () => command.GetEditorCompletions(commandLine, commandLine.Length - 1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Response_file_marker_is_preserved_in_replacement_text()
    {
        var command = new Command("tool") { new Argument<string[]>("args") };
        command.Arguments.Single().CompletionSources.Add("file with space.rsp");
        var commandLine = "tool @fi";

        var completion = command.GetEditorCompletions(commandLine, commandLine.Length)
                              .Items.Should().ContainSingle(item => item.DisplayText == "file with space.rsp").Subject;

        completion.ReplacementRange.Start.Should().Be(commandLine.IndexOf("fi"));
        completion.InsertText.Should().Be("@\"file with space.rsp\"");
    }

    [Fact]
    public void Quoted_response_file_marker_is_preserved_in_replacement_text()
    {
        var command = new Command("tool") { new Argument<string[]>("args") };
        command.Arguments.Single().CompletionSources.Add("file with space.rsp");
        var commandLine = "tool @\"fi";

        var completion = command.GetEditorCompletions(commandLine, commandLine.Length)
                              .Items.Should().ContainSingle(item => item.DisplayText == "file with space.rsp").Subject;

        completion.ReplacementRange.Start.Should().Be(commandLine.IndexOf("@"));
        completion.InsertText.Should().Be("@\"file with space.rsp\"");
    }
}
