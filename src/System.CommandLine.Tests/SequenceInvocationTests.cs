// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace System.CommandLine.Tests
{
    public class SequenceInvocationTests
    {
        private static readonly InvocationConfiguration SilentConfiguration = new()
        {
            Output = TextWriter.Null,
            Error = TextWriter.Null
        };

        private static (RootCommand root, List<string> log) CreateEmitAndCheckCommands()
        {
            var log = new List<string>();

            var emit = new Command("emit");
            emit.SetAction(_ => log.Add("emit"));

            var check = new Command("check");
            check.Options.Add(new Option<int>("--number"));
            check.SetAction(_ => log.Add("check"));

            var root = new RootCommand { emit, check };

            return (root, log);
        }

        [Fact]
        public void Invalid_stage_prevents_all_actions_from_running()
        {
            var (root, log) = CreateEmitAndCheckCommands();

            var plan = root.ParseSequence(new[] { "emit", "::", "check", "--number", "notanumber" });

            plan.IsValid.Should().BeFalse();
            plan.Errors.Should().ContainSingle(e => e.Stage == 2);

            var exitCode = plan.Invoke(SilentConfiguration);

            exitCode.Should().Be(1);
            log.Should().BeEmpty();
        }

        [Fact]
        public void Valid_sequence_invokes_stages_in_order()
        {
            var (root, log) = CreateEmitAndCheckCommands();

            var plan = root.ParseSequence(new[] { "emit", "::", "check", "--number", "3" });

            plan.IsValid.Should().BeTrue();
            plan.Stages.Should().HaveCount(2);

            var exitCode = plan.Invoke(SilentConfiguration);

            exitCode.Should().Be(0);
            log.Should().Equal("emit", "check");
        }

        [Fact]
        public void First_nonzero_exit_code_stops_the_sequence()
        {
            var log = new List<string>();

            var emit = new Command("emit");
            emit.SetAction(_ =>
            {
                log.Add("emit");
                return 5;
            });

            var check = new Command("check");
            check.SetAction(_ => log.Add("check"));

            var root = new RootCommand { emit, check };

            var plan = root.ParseSequence(new[] { "emit", "::", "check" });

            var exitCode = plan.Invoke(SilentConfiguration);

            exitCode.Should().Be(5);
            log.Should().Equal("emit");
        }

        [Fact]
        public void Escaped_separator_is_a_literal_argument()
        {
            var argument = new Argument<string>("value");
            var root = new RootCommand { argument };

            var plan = root.ParseSequence(new[] { @"\::" });

            plan.IsValid.Should().BeTrue();
            plan.Stages.Should().HaveCount(1);
            plan.Stages[0].GetValue(argument).Should().Be("::");
        }

        [Fact]
        public void Double_dash_stops_separator_recognition_until_end_of_stage()
        {
            var argument = new Argument<string[]>("values");
            var root = new RootCommand { argument };

            var plan = root.ParseSequence(new[] { "--", "::", @"\::" });

            plan.IsValid.Should().BeTrue();
            plan.Stages.Should().HaveCount(1);
            plan.Stages[0].GetValue(argument).Should().Equal("::", @"\::");
        }

        [Fact]
        public void Empty_stage_is_an_error_with_stage_number_and_position()
        {
            var (root, _) = CreateEmitAndCheckCommands();

            var plan = root.ParseSequence(new[] { "emit", "::", "::", "check" });

            plan.IsValid.Should().BeFalse();
            var error = plan.Errors.Should().ContainSingle().Which;
            error.Stage.Should().Be(2);
            error.Position.Should().Be(2);
            error.Message.Should().Contain("empty");
            error.ToString().Should().Contain("Stage 2");
        }

        [Fact]
        public void Trailing_separator_produces_an_empty_stage_error()
        {
            var (root, _) = CreateEmitAndCheckCommands();

            var plan = root.ParseSequence(new[] { "emit", "::" });

            plan.IsValid.Should().BeFalse();
            var error = plan.Errors.Should().ContainSingle().Which;
            error.Stage.Should().Be(2);
            error.Position.Should().Be(2);
        }

        [Fact]
        public void Stage_parse_errors_carry_stage_number_and_original_position()
        {
            var (root, _) = CreateEmitAndCheckCommands();

            var plan = root.ParseSequence(new[] { "emit", "::", "check", "--number", "notanumber" });

            var error = plan.Errors.Should().ContainSingle().Which;
            error.Stage.Should().Be(2);
            error.Position.Should().BeGreaterThanOrEqualTo(2);
            error.Position.Should().BeLessThan(5);
        }

        [Fact]
        public void Default_value_factory_runs_at_most_once_per_stage()
        {
            var factoryCalls = 0;
            var option = new Option<string>("--name")
            {
                DefaultValueFactory = _ =>
                {
                    factoryCalls++;
                    return "default";
                }
            };
            var argument = new Argument<string>("value");
            var root = new RootCommand { option, argument };

            var observed = new List<string>();
            root.SetAction(parseResult => observed.Add(parseResult.GetValue(option)));

            var plan = root.ParseSequence(new[] { "--name", "explicit", "v1", "::", "v2" });

            plan.IsValid.Should().BeTrue();

            var exitCode = plan.Invoke(SilentConfiguration);

            exitCode.Should().Be(0);
            observed.Should().Equal("explicit", "default");
            // Only the second stage relies on the default, and parsing up front plus
            // execution must not evaluate the factory more than once for that stage.
            factoryCalls.Should().Be(1);
        }

        [Fact]
        public void Values_do_not_leak_from_one_stage_into_the_next()
        {
            var option = new Option<string>("--name");
            var argument = new Argument<string>("value");
            var root = new RootCommand { option, argument };

            var observed = new List<string>();
            root.SetAction(parseResult => observed.Add(parseResult.GetValue(option)));

            var plan = root.ParseSequence(new[] { "--name", "from-first-stage", "v1", "::", "v2" });

            plan.Invoke(SilentConfiguration).Should().Be(0);

            observed.Should().Equal("from-first-stage", null);
            plan.Stages[1].GetResult(option).Should().BeNull();
        }

        [Fact]
        public async Task Cancellation_prevents_later_stages_from_starting()
        {
            var log = new List<string>();
            using var cts = new CancellationTokenSource();

            var emit = new Command("emit");
            emit.SetAction((parseResult, cancellationToken) =>
            {
                log.Add("emit");
                cts.Cancel();
                return Task.CompletedTask;
            });

            var check = new Command("check");
            check.SetAction(_ => log.Add("check"));

            var root = new RootCommand { emit, check };

            var plan = root.ParseSequence(new[] { "emit", "::", "check" });

            var exitCode = await plan.InvokeAsync(SilentConfiguration, cts.Token);

            exitCode.Should().Be(1);
            log.Should().Equal("emit");
        }

        [Fact]
        public async Task Cancellation_before_invocation_starts_no_stage()
        {
            var (root, log) = CreateEmitAndCheckCommands();

            var plan = root.ParseSequence(new[] { "emit", "::", "check" });

            var exitCode = await plan.InvokeAsync(SilentConfiguration, new CancellationToken(canceled: true));

            exitCode.Should().Be(1);
            log.Should().BeEmpty();
        }

        [Fact]
        public async Task Asynchronous_actions_are_awaited_in_order()
        {
            var log = new List<string>();

            var emit = new Command("emit");
            emit.SetAction(async (parseResult, cancellationToken) =>
            {
                log.Add("emit-start");
                await Task.Yield();
                log.Add("emit-end");
            });

            var check = new Command("check");
            check.SetAction(_ => log.Add("check"));

            var root = new RootCommand { emit, check };

            var plan = root.ParseSequence(new[] { "emit", "::", "check" });

            var exitCode = await plan.InvokeAsync(SilentConfiguration);

            exitCode.Should().Be(0);
            log.Should().Equal("emit-start", "emit-end", "check");
        }

        [Fact]
        public void Synchronous_Invoke_supports_asynchronous_actions()
        {
            var log = new List<string>();

            var emit = new Command("emit");
            emit.SetAction(async (parseResult, cancellationToken) =>
            {
                await Task.Yield();
                log.Add("emit");
            });

            var check = new Command("check");
            check.SetAction(_ => log.Add("check"));

            var root = new RootCommand { emit, check };

            var plan = root.ParseSequence(new[] { "emit", "::", "check" });

            var exitCode = plan.Invoke(SilentConfiguration);

            exitCode.Should().Be(0);
            log.Should().Equal("emit", "check");
        }

        [Fact]
        public async Task Plan_can_only_be_executed_once()
        {
            var (root, log) = CreateEmitAndCheckCommands();

            var plan = root.ParseSequence(new[] { "emit", "::", "check" });

            plan.Invoke(SilentConfiguration).Should().Be(0);

            var invokeAgain = () => plan.Invoke(SilentConfiguration);
            invokeAgain.Should().Throw<InvalidOperationException>();

            Func<Task> invokeAsyncAgain = () => plan.InvokeAsync(SilentConfiguration);
            await invokeAsyncAgain.Should().ThrowAsync<InvalidOperationException>();

            log.Should().Equal("emit", "check");
        }

        [Fact]
        public void Mutating_the_input_array_after_parsing_does_not_affect_the_plan()
        {
            var (root, log) = CreateEmitAndCheckCommands();

            var args = new[] { "emit", "::", "check" };
            var plan = root.ParseSequence(args);

            args[0] = "check";
            args[2] = "emit";

            plan.Invoke(SilentConfiguration).Should().Be(0);

            log.Should().Equal("emit", "check");
        }

        [Fact]
        public void Exceptions_follow_the_existing_invocation_pipeline_behavior()
        {
            var log = new List<string>();

            var emit = new Command("emit");
            emit.SetAction(new System.Action<ParseResult>(_ =>
            {
                log.Add("emit");
                throw new InvalidOperationException("boom");
            }));

            var check = new Command("check");
            check.SetAction(_ => log.Add("check"));

            var root = new RootCommand { emit, check };

            var plan = root.ParseSequence(new[] { "emit", "::", "check" });

            // The default exception handler of the invocation pipeline turns the exception into exit code 1,
            // which stops the sequence.
            var exitCode = plan.Invoke(SilentConfiguration);

            exitCode.Should().Be(1);
            log.Should().Equal("emit");
        }

        [Fact]
        public void Example_preflight_failure_and_successful_sequence()
        {
            var (root, log) = CreateEmitAndCheckCommands();

            // Preflight failure: the second stage does not parse, so no action runs at all.
            var invalidPlan = root.ParseSequence(new[] { "emit", "::", "check", "--number", "notanumber" });
            var failureExitCode = invalidPlan.Invoke(SilentConfiguration);

            failureExitCode.Should().Be(1);
            log.Should().BeEmpty();
            invalidPlan.Errors.Should().ContainSingle(e => e.Stage == 2);

            // Successful sequence: both stages run in order.
            var validPlan = root.ParseSequence(new[] { "emit", "::", "check", "--number", "3" });
            var successExitCode = validPlan.Invoke(SilentConfiguration);

            successExitCode.Should().Be(0);
            log.Should().Equal("emit", "check");
        }
    }
}
