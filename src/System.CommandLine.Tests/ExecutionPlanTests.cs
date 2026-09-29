// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;
using FluentAssertions;
using Xunit;

namespace System.CommandLine.Tests
{
    public class ExecutionPlanTests
    {
        private static RootCommand CreateRootWithInner(
            out Command inner,
            out Option<bool> flagA,
            out Option<bool> flagB,
            out Option<string[]> many,
            out Argument<string> theArg,
            out Argument<string[]> rest,
            out Option<string> outerOpt,
            out Argument<string> outerArg)
        {
            flagA = new Option<bool>("-a");
            flagB = new Option<bool>("-b");
            many = new Option<string[]>("--many");
            theArg = new Argument<string>("the-arg");
            rest = new Argument<string[]>("rest");

            inner = new Command("inner")
            {
                flagA,
                flagB,
                many,
                theArg,
                rest
            };

            outerOpt = new Option<string>("--outer-opt");
            outerArg = new Argument<string>("outer-arg");

            return new RootCommand
            {
                inner,
                outerOpt,
                outerArg
            };
        }

        [Fact]
        public void Plan_lists_the_selected_command_chain_from_root_to_innermost()
        {
            var root = CreateRootWithInner(
                out var inner,
                out _, out _, out _, out _, out _, out _, out _);

            var plan = root.Parse("inner").GetExecutionPlan();

            plan.Commands.Select(c => c.Name).Should().Equal(root.Name, inner.Name);
            plan.SelectedCommand.Name.Should().Be(inner.Name);
            plan.Commands[0].IdentifierTokenRange.HasValue.Should().BeFalse();
            plan.Commands[1].IdentifierTokenRange.Start.Should().Be(0);
            plan.Commands[1].IdentifierTokenRange.Length.Should().Be(1);
        }

        [Fact]
        public void Plan_reports_bundled_short_options_at_the_same_token_position()
        {
            var root = CreateRootWithInner(
                out _, out var flagA, out var flagB, out _, out _, out _, out _, out _);

            var plan = root.Parse("inner -ab").GetExecutionPlan();

            var innerCmd = plan.SelectedCommand;

            var a = innerCmd.Symbols.Single(s => s.Name == flagA.Name);
            var b = innerCmd.Symbols.Single(s => s.Name == flagB.Name);

            a.IdentifierTokenRanges.Should().ContainSingle().Which.Start.Should().Be(1);
            b.IdentifierTokenRanges.Should().ContainSingle().Which.Start.Should().Be(1);

            ParsedValue value = a.Values.Should().ContainSingle().Subject;
            value.Origin.Should().Be(ParseValueOrigin.Explicit);
            value.ConvertedValue.Should().Be(true);
            value.TokenRange.Start.Should().Be(1);
        }

        [Fact]
        public void Plan_maps_each_value_of_a_repeated_multi_value_option_to_its_token()
        {
            var root = CreateRootWithInner(
                out _, out _, out _, out var many, out _, out _, out _, out _);

            var plan = root.Parse("inner --many one --many two").GetExecutionPlan();

            var manyDescription = plan.SelectedCommand.Symbols.Single(s => s.Name == many.Name);

            manyDescription.IdentifierTokenRanges.Select(r => r.Start).Should().Equal(1, 3);

            manyDescription.Values.Should().HaveCount(2);
            manyDescription.Values[0].RawValue.Should().Be("one");
            manyDescription.Values[0].ConvertedValue.Should().Be("one");
            manyDescription.Values[0].TokenRange.Start.Should().Be(2);
            manyDescription.Values[0].Origin.Should().Be(ParseValueOrigin.Explicit);
            manyDescription.Values[1].RawValue.Should().Be("two");
            manyDescription.Values[1].TokenRange.Start.Should().Be(4);
        }

        [Fact]
        public void Plan_preserves_an_argument_value_containing_spaces_and_its_token_position()
        {
            var root = CreateRootWithInner(
                out _, out _, out _, out _, out var theArg, out _, out _, out _);

            var plan = root.Parse("inner \"hello world\"").GetExecutionPlan();

            var arg = plan.SelectedCommand.Symbols.Single(s => s.Name == theArg.Name);

            arg.Values.Should().ContainSingle();
            arg.Values[0].RawValue.Should().Be("hello world");
            arg.Values[0].ConvertedValue.Should().Be("hello world");
            arg.Values[0].TokenRange.Start.Should().Be(1);
        }

        [Fact]
        public void Plan_reports_a_value_produced_by_a_default_value_factory_as_a_default()
        {
            var defaultOption = new Option<int>("-n") { DefaultValueFactory = _ => 42 };
            var command = new RootCommand
            {
                defaultOption
            };

            var plan = command.Parse("").GetExecutionPlan();

            var option = plan.SelectedCommand.Symbols.Single(s => s.Name == defaultOption.Name);

            option.IdentifierTokenRanges.Should().BeEmpty();
            option.Values.Should().ContainSingle();
            option.Values[0].Origin.Should().Be(ParseValueOrigin.Default);
            option.Values[0].ConvertedValue.Should().Be(42);
            option.Values[0].RawValue.Should().BeNull();
            option.Values[0].TokenRange.HasValue.Should().BeFalse();
        }

        [Fact]
        public void Building_a_plan_does_not_invoke_the_default_value_factory_again()
        {
            var factoryCount = 0;
            var defaultOption = new Option<int>("-n")
            {
                DefaultValueFactory = _ =>
                {
                    factoryCount++;
                    return 7;
                }
            };
            var command = new RootCommand
            {
                defaultOption
            };

            var parseResult = command.Parse("");

            factoryCount.Should().Be(1);

            parseResult.GetExecutionPlan();
            parseResult.GetExecutionPlan();

            factoryCount.Should().Be(1);
            parseResult.GetValue(defaultOption).Should().Be(7);
            factoryCount.Should().Be(1);
        }

        [Fact]
        public void Plan_reports_a_conversion_failure_without_throwing()
        {
            var number = new Option<int>("-n");
            var command = new RootCommand
            {
                number
            };

            var parseResult = command.Parse("-n not-a-number");

            parseResult.Errors.Should().NotBeEmpty();

            var plan = parseResult.GetExecutionPlan();

            plan.Errors.Should().NotBeEmpty();

            var option = plan.SelectedCommand.Symbols.Single(s => s.Name == number.Name);

            option.Values.Should().ContainSingle();
            option.Values[0].RawValue.Should().Be("not-a-number");
            option.Values[0].ConvertedValue.Should().BeNull();
            option.Values[0].IsSuccess.Should().BeFalse();
            option.Values[0].ConversionError.Should().Contain("not-a-number");
            option.Values[0].TokenRange.Start.Should().Be(1);
        }

        [Fact]
        public void Plan_reports_per_token_success_and_failure_for_a_multi_value_option()
        {
            var ints = new Option<int[]>("--ints") { AllowMultipleArgumentsPerToken = true };
            var command = new RootCommand
            {
                ints
            };

            var plan = command.Parse("--ints 1 bad 3").GetExecutionPlan();

            var values = plan.SelectedCommand.Symbols.Single(s => s.Name == ints.Name).Values;

            values.Should().HaveCount(3);

            values[0].ConvertedValue.Should().Be(1);
            values[0].IsSuccess.Should().BeTrue();

            values[1].RawValue.Should().Be("bad");
            values[1].ConvertedValue.Should().BeNull();
            values[1].IsSuccess.Should().BeFalse();
            values[1].ConversionError.Should().Contain("bad");

            values[2].ConvertedValue.Should().Be(3);
            values[2].IsSuccess.Should().BeTrue();
        }

        [Fact]
        public void Plan_maps_tokens_after_double_dash_as_arguments()
        {
            var root = CreateRootWithInner(
                out _, out _, out _, out _, out var theArg, out var rest, out _, out _);

            var plan = root.Parse("inner -- -a \"--many whatever\"").GetExecutionPlan();

            var theArgDescription = plan.SelectedCommand.Symbols.Single(s => s.Name == theArg.Name);
            theArgDescription.Values[0].RawValue.Should().Be("-a");
            theArgDescription.Values[0].TokenRange.Start.Should().Be(2);

            var restDescription = plan.SelectedCommand.Symbols.Single(s => s.Name == rest.Name);
            restDescription.Values.Should().ContainSingle();
            restDescription.Values[0].RawValue.Should().Be("--many whatever");
            restDescription.Values[0].TokenRange.Start.Should().Be(3);
            restDescription.Values[0].ConvertedValue.Should().Be("--many whatever");
        }

        private sealed class SensitiveValue
        {
            public override string ToString() => throw new InvalidOperationException("ToString is not allowed");
        }

        [Fact]
        public void Building_a_plan_never_calls_ToString_on_converted_values()
        {
            var customParsed = new SensitiveValue();
            var option = new Option<SensitiveValue>("--secret")
            {
                CustomParser = _ => customParsed
            };
            var command = new RootCommand
            {
                option
            };

            var parseResult = command.Parse("--secret value");

            parseResult.Errors.Should().BeEmpty();

            var plan = parseResult.GetExecutionPlan();

            var value = plan.SelectedCommand.Symbols
                               .Single(s => s.Name == option.Name)
                               .Values.Should().ContainSingle().Subject;

            value.ConvertedValue.Should().BeSameAs(customParsed);
            value.RawValue.Should().Be("value");

            plan.Invoking(p => p.ToString()).Should().NotThrow();
            value.Invoking(v => v.ToString()).Should().NotThrow();
        }

        [Fact]
        public void Plan_reports_a_default_argument_value_from_a_factory()
        {
            var count = 0;
            var argument = new Argument<string>("the-arg")
            {
                DefaultValueFactory = _ =>
                {
                    count++;
                    return "generated";
                }
            };
            var command = new RootCommand
            {
                argument
            };

            var parseResult = command.Parse("");

            count.Should().Be(1);

            var plan = parseResult.GetExecutionPlan();
            var description = plan.SelectedCommand.Symbols.Single(s => s.Name == argument.Name);

            description.Kind.Should().Be(ExecutionSymbolKind.Argument);
            description.Values.Should().ContainSingle();
            description.Values[0].Origin.Should().Be(ParseValueOrigin.Default);
            description.Values[0].ConvertedValue.Should().Be("generated");
            description.IdentifierTokenRanges.Should().BeEmpty();

            count.Should().Be(1);
        }

        [Fact]
        public void An_attached_option_value_shares_its_identifier_token_range()
        {
            var port = new Option<int>("-p");
            var command = new RootCommand
            {
                port
            };

            var plan = command.Parse(new[] { "-p8080" }).GetExecutionPlan();

            var option = plan.SelectedCommand.Symbols.Single(s => s.Name == port.Name);

            option.IdentifierTokenRanges.Should().ContainSingle().Which.Start.Should().Be(0);
            option.Values.Should().ContainSingle();
            option.Values[0].RawValue.Should().Be("8080");
            option.Values[0].ConvertedValue.Should().Be(8080);
            option.Values[0].TokenRange.Start.Should().Be(0);
        }

        [Fact]
        public void Plan_reports_the_canonical_option_and_command_names_when_aliases_are_used()
        {
            var verbose = new Option<bool>("--verbose", "-v");
            var doStuff = new Command("do-stuff")
            {
                verbose
            };
            doStuff.Aliases.Add("ds");
            var root = new RootCommand
            {
                doStuff
            };

            var plan = root.Parse(new[] { "ds", "-v" }).GetExecutionPlan();

            plan.Commands.Select(c => c.Name).Should().Equal(root.Name, "do-stuff");

            var option = plan.SelectedCommand.Symbols.Single(s => s.Name == verbose.Name);
            option.Name.Should().Be("--verbose");
            option.Kind.Should().Be(ExecutionSymbolKind.Option);
        }

        [Fact]
        public void Building_a_plan_for_a_default_command_argument_does_not_invoke_its_factory_again()
        {
            var count = 0;
            var argument = new Argument<int[]>("numbers")
            {
                DefaultValueFactory = _ =>
                {
                    count++;
                    return new[] { 1, 2, 3 };
                }
            };
            var command = new RootCommand
            {
                argument
            };

            var parseResult = command.Parse("");

            count.Should().Be(1);

            var plan = parseResult.GetExecutionPlan();
            var value = plan.SelectedCommand.Symbols
                               .Single(s => s.Name == argument.Name)
                               .Values.Should().ContainSingle().Subject;

            value.Origin.Should().Be(ParseValueOrigin.Default);
            count.Should().Be(1);
        }

        [Fact]
        public void Plan_for_values_passed_as_an_array_uses_input_array_positions()
        {
            var root = CreateRootWithInner(
                out _, out var flagA, out _, out var many, out var theArg, out _, out _, out _);

            var args = new[] { "inner", "-a", "--many", "one", "--many", "two", "with space" };

            var plan = root.Parse(args).GetExecutionPlan();

            var selected = plan.SelectedCommand;
            selected.IdentifierTokenRange.Start.Should().Be(0);

            selected.Symbols.Single(s => s.Name == flagA.Name)
                    .IdentifierTokenRanges.Should().ContainSingle().Which.Start.Should().Be(1);

            var manyDescription = selected.Symbols.Single(s => s.Name == many.Name);
            manyDescription.Values.Select(v => v.TokenRange.Start).Should().Equal(3, 5);

            selected.Symbols.Single(s => s.Name == theArg.Name)
                    .Values[0].TokenRange.Start.Should().Be(6);
            selected.Symbols.Single(s => s.Name == theArg.Name)
                    .Values[0].RawValue.Should().Be("with space");
        }

        [Fact]
        public void GetExecutionPlan_returns_the_same_instance_for_a_parse_result()
        {
            var command = new RootCommand
            {
                new Option<string>("--opt")
            };

            var parseResult = command.Parse("--opt value");

            parseResult.GetExecutionPlan().Should().BeSameAs(parseResult.GetExecutionPlan());
        }

        [Fact]
        public void Plan_reports_explicit_options_on_parent_commands_without_running_custom_code()
        {
            var customParserCalls = 0;
            var outerOpt = new Option<Uri>("--outer-opt")
            {
                CustomParser = result =>
                {
                    customParserCalls++;
                    return new Uri(result.Tokens[0].Value);
                }
            };
            var inner = new Command("inner");
            var root = new RootCommand
            {
                outerOpt,
                inner
            };

            var parseResult = root.Parse("--outer-opt http://example inner");

            var plan = parseResult.GetExecutionPlan();

            var outerCommand = plan.Commands[0];
            var option = outerCommand.Symbols.Single(s => s.Name == outerOpt.Name);

            option.Values.Should().ContainSingle();
            option.Values[0].RawValue.Should().Be("http://example");
            option.Values[0].TokenRange.Start.Should().Be(1);
            option.Values[0].Origin.Should().Be(ParseValueOrigin.Explicit);
            option.Values[0].ConvertedValue.Should().BeNull();
            option.Values[0].IsSuccess.Should().BeTrue();

            customParserCalls.Should().Be(0);
        }
    }
}
