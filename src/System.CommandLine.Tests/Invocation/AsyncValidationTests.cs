// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
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
        public async Task Diagnostics_are_reported_in_registration_order_regardless_of_completion_order()
        {
            TaskCompletionSource<bool> started1 = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> started2 = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> started3 = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> gate1 = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> gate2 = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> gate3 = new(TaskCreationOptions.RunContinuationsAsynchronously);
            int actionInvocationCount = 0;

            RootCommand command = new();
            command.SetAction(_ => { actionInvocationCount++; });
            command.AsyncValidators.Add(new AsyncValidator("first", async (_, _) =>
            {
                started1.SetResult(true);
                await gate1.Task;
                return new[] { new ValidationDiagnostic("diagnostic-from-first") };
            }));
            command.AsyncValidators.Add(new AsyncValidator("second", async (_, _) =>
            {
                started2.SetResult(true);
                await gate2.Task;
                return new[] { new ValidationDiagnostic("diagnostic-from-second") };
            }));
            command.AsyncValidators.Add(new AsyncValidator("third", async (_, _) =>
            {
                started3.SetResult(true);
                await gate3.Task;
                return new[] { new ValidationDiagnostic("diagnostic-from-third") };
            }));

            StringWriter output = new();
            StringWriter error = new();
            ParseResult parseResult = command.Parse("");

            Task<int> invokeTask = parseResult.InvokeAsync(new InvocationConfiguration
            {
                Output = output,
                Error = error,
                MaxAsyncValidatorConcurrency = 3
            });

            await Task.WhenAll(started1.Task, started2.Task, started3.Task);

            // Complete the validators in the reverse of their registration order.
            gate3.SetResult(true);
            gate2.SetResult(true);
            gate1.SetResult(true);

            int exitCode = await invokeTask;

            exitCode.Should().Be(1);
            actionInvocationCount.Should().Be(0);
            parseResult.Errors.Select(e => e.Message)
                       .Should()
                       .Equal("diagnostic-from-first", "diagnostic-from-second", "diagnostic-from-third");
            error.ToString().Should().ContainAll(
                "diagnostic-from-first", "diagnostic-from-second", "diagnostic-from-third");
        }

        [Fact]
        public async Task Async_validators_do_not_run_when_synchronous_validation_failed()
        {
            bool asyncValidatorRan = false;
            int actionInvocationCount = 0;

            RootCommand command = new();
            command.Validators.Add(result => result.AddError("synchronous failure"));
            command.AsyncValidators.Add(new AsyncValidator("async-check", (_, _) =>
            {
                asyncValidatorRan = true;
                return Task.FromResult<IEnumerable<ValidationDiagnostic>?>(null);
            }));
            command.SetAction(_ => { actionInvocationCount++; });

            StringWriter output = new();
            StringWriter error = new();

            int exitCode = await command.Parse("").InvokeAsync(new InvocationConfiguration
            {
                Output = output,
                Error = error
            });

            exitCode.Should().Be(1);
            asyncValidatorRan.Should().BeFalse();
            actionInvocationCount.Should().Be(0);
            error.ToString().Should().Contain("synchronous failure");
        }

        [Fact]
        public async Task Async_validators_honor_the_configured_concurrency_limit()
        {
            const int validatorCount = 6;
            const int concurrencyLimit = 2;
            object sync = new();
            int running = 0;
            int maxObserved = 0;
            int completed = 0;
            int actionInvocationCount = 0;

            RootCommand command = new();
            command.SetAction(_ => { actionInvocationCount++; });

            for (int i = 0; i < validatorCount; i++)
            {
                command.AsyncValidators.Add(new AsyncValidator($"check-{i}", async (_, _) =>
                {
                    lock (sync)
                    {
                        running++;
                        maxObserved = running > maxObserved ? running : maxObserved;
                    }

                    await Task.Delay(50);

                    lock (sync)
                    {
                        running--;
                        completed++;
                    }

                    return null;
                }));
            }

            int exitCode = await command.Parse("").InvokeAsync(new InvocationConfiguration
            {
                Output = new StringWriter(),
                Error = new StringWriter(),
                MaxAsyncValidatorConcurrency = concurrencyLimit
            });

            exitCode.Should().Be(0);
            completed.Should().Be(validatorCount);
            maxObserved.Should().BeLessThanOrEqualTo(concurrencyLimit);
            actionInvocationCount.Should().Be(1);
        }

        [Fact]
        public async Task A_throwing_validator_produces_a_failure_diagnostic_naming_the_validator()
        {
            int actionInvocationCount = 0;

            RootCommand command = new();
            command.SetAction(_ => { actionInvocationCount++; });
            command.AsyncValidators.Add(new AsyncValidator("flaky-check", (_, _) =>
                throw new InvalidOperationException("boom")));

            StringWriter output = new();
            StringWriter error = new();
            ParseResult parseResult = command.Parse("");

            int exitCode = await parseResult.InvokeAsync(new InvocationConfiguration
            {
                Output = output,
                Error = error
            });

            exitCode.Should().Be(1);
            actionInvocationCount.Should().Be(0);
            parseResult.Errors.Should().ContainSingle()
                       .Which.Message.Should().Contain("flaky-check").And.Contain("boom");
            error.ToString().Should().Contain("flaky-check");
        }

        [Fact]
        public async Task Cancellation_before_invocation_prevents_any_validator_from_starting()
        {
            bool validatorRan = false;
            int actionInvocationCount = 0;

            RootCommand command = new();
            command.SetAction(_ => { actionInvocationCount++; });
            command.AsyncValidators.Add(new AsyncValidator("check", (_, _) =>
            {
                validatorRan = true;
                return Task.FromResult<IEnumerable<ValidationDiagnostic>?>(null);
            }));

            using CancellationTokenSource cts = new();
            cts.Cancel();

            Func<Task> invoke = () => command.Parse("").InvokeAsync(new InvocationConfiguration
            {
                Output = new StringWriter(),
                Error = new StringWriter(),
                EnableDefaultExceptionHandler = false
            }, cts.Token);

            await invoke.Should().ThrowAsync<OperationCanceledException>();

            validatorRan.Should().BeFalse();
            actionInvocationCount.Should().Be(0);
        }

        [Fact]
        public async Task Cancellation_stops_starting_new_validators_and_waits_for_started_ones_to_finish()
        {
            TaskCompletionSource<bool> firstStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> firstGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            bool firstCompleted = false;
            bool secondStarted = false;
            int actionInvocationCount = 0;

            RootCommand command = new();
            command.SetAction(_ => { actionInvocationCount++; });
            command.AsyncValidators.Add(new AsyncValidator("first", async (_, _) =>
            {
                firstStarted.SetResult(true);
                await firstGate.Task;
                firstCompleted = true;
                return null;
            }));
            command.AsyncValidators.Add(new AsyncValidator("second", (_, _) =>
            {
                secondStarted = true;
                return Task.FromResult<IEnumerable<ValidationDiagnostic>?>(null);
            }));

            using CancellationTokenSource cts = new();

            Task<int> invokeTask = command.Parse("").InvokeAsync(new InvocationConfiguration
            {
                Output = new StringWriter(),
                Error = new StringWriter(),
                EnableDefaultExceptionHandler = false,
                MaxAsyncValidatorConcurrency = 1
            }, cts.Token);

            await firstStarted.Task;
            cts.Cancel();

            // The started validator is still winding down, so invocation must not have completed.
            invokeTask.IsCompleted.Should().BeFalse();

            firstGate.SetResult(true);

            Func<Task> awaitInvocation = () => invokeTask;
            await awaitInvocation.Should().ThrowAsync<OperationCanceledException>();

            firstCompleted.Should().BeTrue("started checks are awaited so none is left running in the background");
            secondStarted.Should().BeFalse("no new checks are started once cancellation is requested");
            actionInvocationCount.Should().Be(0);
        }

        [Fact]
        public void Synchronous_Invoke_throws_an_informative_error_when_async_validators_are_registered()
        {
            RootCommand command = new();
            command.SetAction(_ => { });
            command.AsyncValidators.Add(new AsyncValidator("check", (_, _) =>
                Task.FromResult<IEnumerable<ValidationDiagnostic>?>(null)));

            Action invoke = () => command.Parse("").Invoke();

            invoke.Should().Throw<InvalidOperationException>()
                  .Which.Message.Should()
                  .Contain(nameof(ParseResult.InvokeAsync))
                  .And.Contain(nameof(Command.AsyncValidators));
        }

        [Fact]
        public async Task InvokeAsync_runs_the_action_when_all_async_validators_pass()
        {
            int actionInvocationCount = 0;

            RootCommand command = new();
            command.SetAction(_ => { actionInvocationCount++; });
            command.AsyncValidators.Add(new AsyncValidator("passing-check", (_, _) =>
                Task.FromResult<IEnumerable<ValidationDiagnostic>?>(null)));
            command.AsyncValidators.Add(new AsyncValidator("empty-check", (_, _) =>
                Task.FromResult<IEnumerable<ValidationDiagnostic>?>(Array.Empty<ValidationDiagnostic>())));

            int exitCode = await command.Parse("").InvokeAsync(new InvocationConfiguration
            {
                Output = new StringWriter(),
                Error = new StringWriter()
            });

            exitCode.Should().Be(0);
            actionInvocationCount.Should().Be(1);
        }
    }
}
