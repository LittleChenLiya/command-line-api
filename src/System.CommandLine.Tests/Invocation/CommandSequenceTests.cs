// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace System.CommandLine.Tests.Invocation;

public class CommandSequenceTests
{
    private static (RootCommand root, List<string> log) CreateEmitAndCheckCommands(
        Func<ParseResult, int>? emitAction = null,
        Func<ParseResult, int>? checkAction = null)
    {
        var log = new List<string>();

        var emit = new Command("emit");
        emit.SetAction(parseResult =>
        {
            log.Add("emit");
            return emitAction?.Invoke(parseResult) ?? 0;
        });

        var check = new Command("check");
        check.SetAction(parseResult =>
        {
            log.Add("check");
            return checkAction?.Invoke(parseResult) ?? 0;
        });

        var root = new RootCommand();
        root.Subcommands.Add(emit);
        root.Subcommands.Add(check);

        return (root, log);
    }

    [Fact]
    public void When_a_later_stage_is_invalid_then_no_action_of_any_stage_is_executed()
    {
        var (root, log) = CreateEmitAndCheckCommands();
        var required = new Option<string>("--name") { Required = true };
        root.Subcommands[1].Options.Add(required);

        var plan = CommandSequence.Parse(root, new[] { "emit", "::", "check" });

        plan.IsValid.Should().BeFalse();
        plan.Errors.Should().OnlyContain(error => error.StageNumber == 2);

        var exitCode = plan.Invoke(new InvocationConfiguration { Output = TextWriter.Null, Error = TextWriter.Null });

        exitCode.Should().Be(1);
        log.Should().BeEmpty();
    }

    [Fact]
    public void When_all_stages_are_valid_then_actions_run_in_order()
    {
        var (root, log) = CreateEmitAndCheckCommands();

        var plan = CommandSequence.Parse(root, new[] { "emit", "::", "check" });

        plan.IsValid.Should().BeTrue();
        plan.ParseResults.Should().HaveCount(2);

        var exitCode = plan.Invoke();

        exitCode.Should().Be(0);
        log.Should().Equal("emit", "check");
    }

    [Fact]
    public void Parsing_the_plan_does_not_execute_any_stage_action()
    {
        var (root, log) = CreateEmitAndCheckCommands();

        var plan = CommandSequence.Parse(root, new[] { "emit", "::", "check" });

        log.Should().BeEmpty("parsing and validation must not run any action");

        plan.Invoke();

        log.Should().Equal("emit", "check");
    }

    [Fact]
    public void First_non_zero_exit_code_stops_later_stages()
    {
        var (root, log) = CreateEmitAndCheckCommands(emitAction: _ => 5);

        var plan = CommandSequence.Parse(root, new[] { "emit", "::", "check" });

        var exitCode = plan.Invoke();

        exitCode.Should().Be(5);
        log.Should().Equal("emit");
    }

    [Fact]
    public void Escaped_separator_is_passed_to_the_stage_as_a_literal_argument()
    {
        var argument = new Argument<string>("value");
        var emit = new Command("emit");
        emit.Arguments.Add(argument);
        string? received = null;
        emit.SetAction(parseResult => received = parseResult.GetValue(argument));

        var root = new RootCommand();
        root.Subcommands.Add(emit);

        var plan = CommandSequence.Parse(root, new[] { "emit", "\\::" });

        plan.IsValid.Should().BeTrue();
        plan.ParseResults.Should().HaveCount(1);
        plan.ParseResults[0].GetValue(argument).Should().Be("::");

        plan.Invoke();

        received.Should().Be("::");
    }

    [Fact]
    public void Separator_inside_a_larger_token_is_not_treated_as_a_separator()
    {
        var argument = new Argument<string>("value");
        var emit = new Command("emit");
        emit.Arguments.Add(argument);
        var root = new RootCommand();
        root.Subcommands.Add(emit);

        var plan = CommandSequence.Parse(root, new[] { "emit", "a::b" });

        plan.IsValid.Should().BeTrue();
        plan.ParseResults.Should().HaveCount(1);
        plan.ParseResults[0].GetValue(argument).Should().Be("a::b");
    }

    [Fact]
    public void After_double_dash_everything_is_an_ordinary_argument_and_separators_are_not_recognized()
    {
        var argument = new Argument<string[]>("values");
        var emit = new Command("emit");
        emit.Arguments.Add(argument);
        var log = new List<string>();
        emit.SetAction(_ => log.Add("emit"));

        var check = new Command("check");
        check.SetAction(_ => log.Add("check"));

        var root = new RootCommand();
        root.Subcommands.Add(emit);
        root.Subcommands.Add(check);

        var plan = CommandSequence.Parse(root, new[] { "emit", "--", "::", "check" });

        plan.IsValid.Should().BeTrue();
        plan.ParseResults.Should().HaveCount(1, "no separator is recognized after --");
        plan.ParseResults[0].GetValue(argument).Should().Equal("::", "check");

        plan.Invoke();

        log.Should().Equal("emit");
    }

    [Fact]
    public void Empty_stage_in_the_middle_reports_stage_number_and_position()
    {
        var (root, _) = CreateEmitAndCheckCommands();

        var plan = CommandSequence.Parse(root, new[] { "emit", "::", "::", "check" });

        plan.IsValid.Should().BeFalse();
        plan.Errors.Should().Contain(error =>
            error.StageNumber == 2 &&
            error.ArgumentPosition == 1 &&
            error.Message.Contains("empty"));
    }

    [Fact]
    public void Empty_first_stage_reports_stage_number_and_position()
    {
        var (root, _) = CreateEmitAndCheckCommands();

        var plan = CommandSequence.Parse(root, new[] { "::", "emit" });

        plan.IsValid.Should().BeFalse();
        plan.Errors.Should().Contain(error => error.StageNumber == 1 && error.ArgumentPosition == 0);
    }

    [Fact]
    public void Empty_trailing_stage_reports_stage_number_and_position()
    {
        var (root, _) = CreateEmitAndCheckCommands();

        var plan = CommandSequence.Parse(root, new[] { "emit", "::" });

        plan.IsValid.Should().BeFalse();
        plan.Errors.Should().Contain(error => error.StageNumber == 2 && error.ArgumentPosition == 1);
    }

    [Fact]
    public void Empty_input_is_an_empty_stage_error()
    {
        var (root, _) = CreateEmitAndCheckCommands();

        var plan = CommandSequence.Parse(root, Array.Empty<string>());

        plan.IsValid.Should().BeFalse();
        plan.Errors.Should().Contain(error => error.StageNumber == 1);
    }

    [Fact]
    public void Stage_parse_errors_carry_stage_number_and_original_argument_position()
    {
        var number = new Option<int>("--number");
        var emit = new Command("emit");
        emit.Options.Add(number);
        var check = new Command("check");
        check.Options.Add(number);
        var root = new RootCommand();
        root.Subcommands.Add(emit);
        root.Subcommands.Add(check);

        var plan = CommandSequence.Parse(root, new[] { "emit", "::", "check", "--number", "not-a-number" });

        plan.IsValid.Should().BeFalse();
        plan.Errors.Should().Contain(error => error.StageNumber == 2 && error.ArgumentPosition == 4);
    }

    [Fact]
    public void Default_value_factories_run_at_most_once_per_stage()
    {
        int factoryCalls = 0;

        Option<string> CreateOption() => new("--value")
        {
            DefaultValueFactory = _ =>
            {
                factoryCalls++;
                return "default";
            }
        };

        string? emitValue = null;
        var emitOption = CreateOption();
        var emit = new Command("emit");
        emit.Options.Add(emitOption);
        emit.SetAction(parseResult => emitValue = parseResult.GetValue(emitOption));

        string? checkValue = null;
        var checkOption = CreateOption();
        var check = new Command("check");
        check.Options.Add(checkOption);
        check.SetAction(parseResult => checkValue = parseResult.GetValue(checkOption));

        var root = new RootCommand();
        root.Subcommands.Add(emit);
        root.Subcommands.Add(check);

        var plan = CommandSequence.Parse(root, new[] { "emit", "::", "check" });

        factoryCalls.Should().Be(2, "each stage evaluates its defaults exactly once during validation");

        plan.Invoke();

        factoryCalls.Should().Be(2, "executing the pre-validated plan must not evaluate the factories again");
        emitValue.Should().Be("default");
        checkValue.Should().Be("default");
    }

    [Fact]
    public void Values_provided_in_one_stage_do_not_leak_into_the_next_stage()
    {
        var emitOption = new Option<int>("--opt") { DefaultValueFactory = _ => -1 };
        var emit = new Command("emit");
        emit.Options.Add(emitOption);

        var checkOption = new Option<int>("--opt") { DefaultValueFactory = _ => -1 };
        var check = new Command("check");
        check.Options.Add(checkOption);

        int? observedByCheck = null;
        check.SetAction(parseResult => observedByCheck = parseResult.GetValue(checkOption));

        var root = new RootCommand();
        root.Subcommands.Add(emit);
        root.Subcommands.Add(check);

        var plan = CommandSequence.Parse(root, new[] { "emit", "--opt", "5", "::", "check" });

        plan.ParseResults[0].GetValue(emitOption).Should().Be(5);
        plan.ParseResults[1].GetValue(checkOption).Should().Be(-1, "stage 2 did not provide --opt");

        plan.Invoke();

        observedByCheck.Should().Be(-1);
    }

    [Fact]
    public async Task Asynchronous_actions_are_awaited_in_order()
    {
        var log = new List<string>();

        var emit = new Command("emit");
        emit.SetAction(async (parseResult, cancellationToken) =>
        {
            await Task.Delay(10, cancellationToken);
            log.Add("emit");
            return 0;
        });

        var check = new Command("check");
        check.SetAction(async (parseResult, cancellationToken) =>
        {
            await Task.Delay(10, cancellationToken);
            log.Add("check");
            return 0;
        });

        var root = new RootCommand();
        root.Subcommands.Add(emit);
        root.Subcommands.Add(check);

        var plan = CommandSequence.Parse(root, new[] { "emit", "::", "check" });

        var exitCode = await plan.InvokeAsync();

        exitCode.Should().Be(0);
        log.Should().Equal("emit", "check");
    }

    [Fact]
    public async Task Mixed_synchronous_and_asynchronous_actions_run_in_order()
    {
        var log = new List<string>();

        var emit = new Command("emit");
        emit.SetAction(_ =>
        {
            log.Add("emit");
            return 0;
        });

        var check = new Command("check");
        check.SetAction(async (parseResult, cancellationToken) =>
        {
            await Task.Yield();
            log.Add("check");
            return 0;
        });

        var root = new RootCommand();
        root.Subcommands.Add(emit);
        root.Subcommands.Add(check);

        var plan = CommandSequence.Parse(root, new[] { "emit", "::", "check" });

        var exitCode = await plan.InvokeAsync();

        exitCode.Should().Be(0);
        log.Should().Equal("emit", "check");
    }

    [Fact]
    public async Task Cancellation_stops_later_stages_from_starting()
    {
        var log = new List<string>();
        using var cts = new CancellationTokenSource();

        var emit = new Command("emit");
        emit.SetAction((parseResult, cancellationToken) =>
        {
            log.Add("emit");
            cts.Cancel();
            return Task.FromResult(0);
        });

        var check = new Command("check");
        check.SetAction(_ => log.Add("check"));

        var root = new RootCommand();
        root.Subcommands.Add(emit);
        root.Subcommands.Add(check);

        var plan = CommandSequence.Parse(root, new[] { "emit", "::", "check" });

        var exitCode = await plan.InvokeAsync(cancellationToken: cts.Token);

        exitCode.Should().Be(1);
        log.Should().Equal("emit");
    }

    [Fact]
    public async Task Cancellation_before_execution_starts_no_stage()
    {
        var (root, log) = CreateEmitAndCheckCommands();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var plan = CommandSequence.Parse(root, new[] { "emit", "::", "check" });

        var exitCode = await plan.InvokeAsync(cancellationToken: cts.Token);

        exitCode.Should().Be(1);
        log.Should().BeEmpty();
    }

    [Fact]
    public void A_plan_can_only_be_executed_once()
    {
        var (root, log) = CreateEmitAndCheckCommands();

        var plan = CommandSequence.Parse(root, new[] { "emit", "::", "check" });

        plan.Invoke().Should().Be(0);
        log.Should().Equal("emit", "check");

        Action secondInvoke = () => plan.Invoke();
        secondInvoke.Should().Throw<InvalidOperationException>();

        Func<Task> secondInvokeAsync = () => plan.InvokeAsync();
        secondInvokeAsync.Should().ThrowAsync<InvalidOperationException>();

        log.Should().Equal(new[] { "emit", "check" }, "the second execution attempt must not run any action");
    }

    [Fact]
    public void Modifying_the_input_array_after_parsing_does_not_affect_the_plan()
    {
        var (root, log) = CreateEmitAndCheckCommands();

        var args = new[] { "emit", "::", "check" };
        var plan = CommandSequence.Parse(root, args);

        args[0] = "bogus";
        args[1] = "bogus";
        args[2] = "bogus";

        var exitCode = plan.Invoke();

        exitCode.Should().Be(0);
        log.Should().Equal("emit", "check");
    }

    [Fact]
    public void Exceptions_follow_the_existing_invocation_pipeline_behavior()
    {
        var log = new List<string>();

        var emit = new Command("emit");
        emit.SetAction((Action<ParseResult>)(_ => throw new InvalidOperationException("boom")));

        var check = new Command("check");
        check.SetAction(_ => log.Add("check"));

        var root = new RootCommand();
        root.Subcommands.Add(emit);
        root.Subcommands.Add(check);

        var plan = CommandSequence.Parse(root, new[] { "emit", "::", "check" });

        var exitCode = plan.Invoke(new InvocationConfiguration { Output = TextWriter.Null, Error = TextWriter.Null });

        exitCode.Should().Be(1, "the default exception handler converts the exception to exit code 1");
        log.Should().BeEmpty("a non-zero exit code stops the sequence");
    }

    [Fact]
    public void Exceptions_propagate_when_the_default_exception_handler_is_disabled()
    {
        var emit = new Command("emit");
        emit.SetAction((Action<ParseResult>)(_ => throw new InvalidOperationException("boom")));

        var root = new RootCommand();
        root.Subcommands.Add(emit);

        var plan = CommandSequence.Parse(root, new[] { "emit" });

        Action invoke = () => plan.Invoke(new InvocationConfiguration { EnableDefaultExceptionHandler = false });

        invoke.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Invalid_plan_writes_diagnostics_to_the_configured_error_output()
    {
        var (root, _) = CreateEmitAndCheckCommands();
        var error = new StringWriter();

        var plan = CommandSequence.Parse(root, new[] { "emit", "::", "::", "check" });

        plan.Invoke(new InvocationConfiguration { Output = TextWriter.Null, Error = error });

        error.ToString().Should().Contain("Stage 2");
    }
}
