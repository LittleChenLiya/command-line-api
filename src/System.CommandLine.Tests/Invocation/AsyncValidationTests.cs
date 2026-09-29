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

namespace System.CommandLine.Tests.Invocation
{
    public class AsyncValidationTests
    {
        [Fact]
        public async Task Diagnostics_are_reported_in_registration_order_not_completion_order_and_action_is_not_invoked()
        {
            var gates = Enumerable.Range(0, 3)
                                  .Select(_ => new TaskCompletionSource<ValidationDiagnostic?>(TaskCreationOptions.RunContinuationsAsynchronously))
                                  .ToArray();
            var started = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            int startedCount = 0;
            int actionCallCount = 0;

            var command = new Command("the-command");
            command.SetAction((ParseResult _) => actionCallCount++);

            string[] names = ["first", "second", "third"];
            for (int i = 0; i < gates.Length; i++)
            {
                var gate = gates[i];
                command.AsyncValidators.Add(new AsyncValidator(names[i], (_, _) =>
                {
                    if (Interlocked.Increment(ref startedCount) == gates.Length)
                    {
                        started.TrySetResult(0);
                    }

                    return gate.Task;
                }));
            }

            StringWriter error = new();
            ParseResult parseResult = command.Parse("the-command");

            Task<int> invokeTask = parseResult.InvokeAsync(new() { Output = new StringWriter(), Error = error });

            await started.Task;

            // complete the validators in the reverse of their registration order
            gates[2].SetResult(new ValidationDiagnostic("third failed"));
            gates[1].SetResult(new ValidationDiagnostic("second failed"));
            gates[0].SetResult(new ValidationDiagnostic("first failed"));

            int exitCode = await invokeTask;

            exitCode.Should().Be(1);
            actionCallCount.Should().Be(0);
            parseResult.Errors.Select(e => e.Message)
                       .Should()
                       .Equal("first: first failed", "second: second failed", "third: third failed");
            error.ToString().Should().Contain("first: first failed");
        }

        [Fact]
        public async Task Validators_that_produce_no_diagnostics_allow_the_action_to_run()
        {
            int actionCallCount = 0;

            var command = new Command("the-command");
            command.SetAction((ParseResult _) => actionCallCount++);
            command.AsyncValidators.Add(new AsyncValidator("one", (_, _) => Task.FromResult<ValidationDiagnostic?>(null)));
            command.AsyncValidators.Add(new AsyncValidator("two", (_, _) => Task.FromResult<ValidationDiagnostic?>(null)));

            int exitCode = await command.Parse("the-command")
                                        .InvokeAsync(new() { Output = new StringWriter(), Error = new StringWriter() });

            exitCode.Should().Be(0);
            actionCallCount.Should().Be(1);
        }

        [Fact]
        public async Task Async_validators_do_not_run_when_parsing_or_synchronous_validation_failed()
        {
            int validatorCallCount = 0;
            int actionCallCount = 0;

            var command = new Command("the-command");
            command.SetAction((ParseResult _) => actionCallCount++);
            command.Validators.Add(result => result.AddError("synchronous validation failed"));
            command.AsyncValidators.Add(new AsyncValidator("async", (_, _) =>
            {
                Interlocked.Increment(ref validatorCallCount);
                return Task.FromResult<ValidationDiagnostic?>(null);
            }));

            int exitCode = await command.Parse("the-command")
                                        .InvokeAsync(new() { Output = new StringWriter(), Error = new StringWriter() });

            exitCode.Should().Be(1);
            validatorCallCount.Should().Be(0);
            actionCallCount.Should().Be(0);
        }

        [Fact]
        public async Task Async_validator_concurrency_is_limited_by_configuration()
        {
            int currentConcurrency = 0;
            int maxObservedConcurrency = 0;

            var command = new Command("the-command");
            command.SetAction((ParseResult _) => { });

            for (int i = 0; i < 6; i++)
            {
                command.AsyncValidators.Add(new AsyncValidator($"validator-{i}", async (_, _) =>
                {
                    int current = Interlocked.Increment(ref currentConcurrency);
                    UpdateMax(current);

                    await Task.Delay(100);

                    Interlocked.Decrement(ref currentConcurrency);
                    return null;
                }));
            }

            int exitCode = await command.Parse("the-command")
                                        .InvokeAsync(new()
                                        {
                                            Output = new StringWriter(),
                                            Error = new StringWriter(),
                                            MaxAsyncValidatorConcurrency = 2
                                        });

            exitCode.Should().Be(0);
            maxObservedConcurrency.Should().Be(2);

            void UpdateMax(int value)
            {
                int observed;
                while ((observed = maxObservedConcurrency) < value &&
                       Interlocked.CompareExchange(ref maxObservedConcurrency, value, observed) != observed)
                {
                }
            }
        }

        [Fact]
        public async Task Async_validator_exception_is_converted_to_a_diagnostic_with_the_validator_name()
        {
            int actionCallCount = 0;

            var command = new Command("the-command");
            command.SetAction((ParseResult _) => actionCallCount++);
            command.AsyncValidators.Add(new AsyncValidator("the-validator", (_, _) =>
                throw new InvalidOperationException("boom")));

            ParseResult parseResult = command.Parse("the-command");

            int exitCode = await parseResult.InvokeAsync(new() { Output = new StringWriter(), Error = new StringWriter() });

            exitCode.Should().Be(1);
            actionCallCount.Should().Be(0);
            parseResult.Errors.Should().ContainSingle()
                       .Which.Message.Should().Be("the-validator: boom");
        }

        [Fact]
        public async Task Cancellation_before_validation_starts_prevents_any_validator_from_starting()
        {
            int validatorCallCount = 0;
            int actionCallCount = 0;

            var command = new Command("the-command");
            command.SetAction((ParseResult _) => actionCallCount++);
            command.AsyncValidators.Add(new AsyncValidator("one", (_, _) =>
            {
                Interlocked.Increment(ref validatorCallCount);
                return Task.FromResult<ValidationDiagnostic?>(null);
            }));
            command.AsyncValidators.Add(new AsyncValidator("two", (_, _) =>
            {
                Interlocked.Increment(ref validatorCallCount);
                return Task.FromResult<ValidationDiagnostic?>(null);
            }));

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Func<Task> invoke = () => command.Parse("the-command").InvokeAsync(
                new()
                {
                    Output = new StringWriter(),
                    Error = new StringWriter(),
                    EnableDefaultExceptionHandler = false
                },
                cts.Token);

            await invoke.Should().ThrowAsync<OperationCanceledException>();

            validatorCallCount.Should().Be(0);
            actionCallCount.Should().Be(0);
        }

        [Fact]
        public async Task Cancellation_stops_starting_new_validators_and_waits_for_started_ones_to_finish()
        {
            var firstValidatorStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstValidatorGate = new TaskCompletionSource<ValidationDiagnostic?>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool firstValidatorCleanedUp = false;
            int laterValidatorsStarted = 0;
            int actionCallCount = 0;

            var command = new Command("the-command");
            command.SetAction((ParseResult _) => actionCallCount++);
            command.AsyncValidators.Add(new AsyncValidator("started", async (_, _) =>
            {
                firstValidatorStarted.TrySetResult(true);
                var diagnostic = await firstValidatorGate.Task;
                firstValidatorCleanedUp = true;
                return diagnostic;
            }));
            command.AsyncValidators.Add(new AsyncValidator("not-started-1", (_, _) =>
            {
                Interlocked.Increment(ref laterValidatorsStarted);
                return Task.FromResult<ValidationDiagnostic?>(null);
            }));
            command.AsyncValidators.Add(new AsyncValidator("not-started-2", (_, _) =>
            {
                Interlocked.Increment(ref laterValidatorsStarted);
                return Task.FromResult<ValidationDiagnostic?>(null);
            }));

            using var cts = new CancellationTokenSource();

            Task<int> invokeTask = command.Parse("the-command").InvokeAsync(
                new()
                {
                    Output = new StringWriter(),
                    Error = new StringWriter(),
                    EnableDefaultExceptionHandler = false,
                    MaxAsyncValidatorConcurrency = 1
                },
                cts.Token);

            await firstValidatorStarted.Task;

            cts.Cancel();
            firstValidatorGate.SetResult(null);

            Func<Task> awaitInvocation = () => invokeTask;
            await awaitInvocation.Should().ThrowAsync<OperationCanceledException>();

            firstValidatorCleanedUp.Should().BeTrue("already-started validators must be awaited so they can finish their cleanup");
            laterValidatorsStarted.Should().Be(0, "validators that never started must not produce side effects");
            actionCallCount.Should().Be(0);
        }

        [Fact]
        public void Synchronous_Invoke_throws_an_informative_error_when_async_validators_are_registered()
        {
            var command = new Command("the-command");
            command.SetAction((ParseResult _) => { });
            command.AsyncValidators.Add(new AsyncValidator("async", (_, _) => Task.FromResult<ValidationDiagnostic?>(null)));

            Action invoke = () => command.Parse("the-command").Invoke();

            invoke.Should().Throw<InvalidOperationException>()
                  .Which.Message.Should().Contain(nameof(ParseResult.InvokeAsync));
        }

        [Fact]
        public void Synchronous_Invoke_works_when_no_async_validators_are_registered()
        {
            int actionCallCount = 0;

            var command = new Command("the-command");
            command.SetAction((ParseResult _) => actionCallCount++);

            int exitCode = command.Parse("the-command").Invoke(new() { Output = new StringWriter(), Error = new StringWriter() });

            exitCode.Should().Be(0);
            actionCallCount.Should().Be(1);
        }
    }
}
