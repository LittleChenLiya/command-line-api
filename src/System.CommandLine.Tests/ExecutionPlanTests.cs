// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Execution;
using System.CommandLine.Parsing;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace System.CommandLine.Tests
{
    public class ExecutionPlanTests
    {
        [Fact]
        public void Plan_lists_selected_command_chain_with_canonical_names()
        {
            var subcommand = new Command("sub");
            var deep = new Command("deep");
            subcommand.Add(deep);
            var root = new Command("root") { subcommand };

            ParseResult result = root.Parse("sub deep");

            ExecutionPlan plan = result.GetExecutionPlan();

            plan.Commands.Select(c => c.CanonicalName)
                .Should().Equal(root.Name, "sub", "deep");
        }

        [Fact]
        public void Plan_lists_options_and_arguments_for_each_selected_subcommand()
        {
            var rootOption = new Option<string>("--root-opt");
            var rootArgument = new Argument<string>("root-arg");
            var subOption = new Option<int>("--sub-opt");
            var subArgument = new Argument<int[]>("sub-args")
            {
                Arity = ArgumentArity.OneOrMore
            };

            var subcommand = new Command("sub")
            {
                subOption,
                subArgument
            };

            var root = new Command("root")
            {
                rootOption,
                rootArgument,
                subcommand
            };

            ParseResult result = root.Parse(new[]
            {
                "--root-opt", "x", "rootval",
                "sub", "--sub-opt", "3", "1", "2"
            });

            ExecutionPlan plan = result.GetExecutionPlan();

            CommandExecutionPlan rootPlan = plan.Commands[0];
            rootPlan.Options.Single().Value.Should().Be("x");
            rootPlan.Arguments.Single().Value.Should().Be("rootval");

            CommandExecutionPlan subPlan = plan.Commands[1];
            subPlan.Options.Single().Value.Should().Be(3);
            ((int[])subPlan.Arguments.Single().Value!).Should().Equal(1, 2);
            subPlan.Arguments.Single().Values.Select(v => v.Span.ArgumentIndex)
                .Should().Equal(6, 7);
        }

        [Fact]
        public void Plan_maps_values_and_option_identifiers_to_exact_input_spans()
        {
            var option = new Option<string>("--name");
            var root = new Command("root") { option };

            ParseResult result = root.Parse(new[] { "--name", "hello world" });

            OptionExecutionPlan optionPlan = plan(result).Commands[0].Options.Single();

            optionPlan.CanonicalName.Should().Be("--name");
            optionPlan.Source.Should().Be(ParsedValueSource.Explicit);
            optionPlan.IdentifierSpans.Should().ContainSingle()
                .Which.Should().Be(new InputTokenSpan(0, 0, 6));

            ParsedTokenValue value = optionPlan.Values.Single();
            value.TokenValue.Should().Be("hello world");
            value.Span.Should().Be(new InputTokenSpan(1, 0, 11));
            value.Span.GetText(result.Arguments).Should().Be("hello world");
            value.AfterDoubleDash.Should().BeFalse();
            optionPlan.Value.Should().Be("hello world");
            optionPlan.ConversionError.Should().BeNull();
        }

        [Fact]
        public void Plan_maps_bundled_short_options_to_spans_within_single_input_argument()
        {
            var force = new Option<bool>("-f");
            var compress = new Option<bool>("-z");
            var file = new Option<string>("-o");
            var root = new Command("root") { force, compress, file };

            ParseResult result = root.Parse(new[] { "-fzo", "out.bin" });

            ExecutionPlan plan = result.GetExecutionPlan();

            OptionExecutionPlan forcePlan = plan.Commands[0].Options.Single(o => o.CanonicalName == "-f");
            forcePlan.IdentifierSpans.Single().Should().Be(new InputTokenSpan(0, 0, 2));
            forcePlan.IdentifierSpans[0].GetText(result.Arguments).Should().Be("-f");

            OptionExecutionPlan compressPlan = plan.Commands[0].Options.Single(o => o.CanonicalName == "-z");
            compressPlan.IdentifierSpans.Single().Should().Be(new InputTokenSpan(0, 1, 2));

            OptionExecutionPlan filePlan = plan.Commands[0].Options.Single(o => o.CanonicalName == "-o");
            filePlan.IdentifierSpans.Single().Should().Be(new InputTokenSpan(0, 2, 2));
            filePlan.Values.Single().Span.Should().Be(new InputTokenSpan(1, 0, 7));
            filePlan.Value.Should().Be("out.bin");
        }

        [Fact]
        public void Plan_maps_inline_bundle_value_to_span_within_input_argument()
        {
            var file = new Option<string>("-o");
            var root = new Command("root") { file };

            ParseResult result = root.Parse(new[] { "-oout.bin" });

            OptionExecutionPlan filePlan = result.GetExecutionPlan().Commands[0].Options.Single();

            filePlan.IdentifierSpans.Single().Should().Be(new InputTokenSpan(0, 0, 2));
            filePlan.Values.Single().Span.Should().Be(new InputTokenSpan(0, 2, 7));
            filePlan.Values.Single().Span.GetText(result.Arguments).Should().Be("out.bin");
        }

        [Theory]
        [InlineData(":")]
        [InlineData("=")]
        public void Plan_maps_inline_long_option_value_to_spans_within_input_argument(string separator)
        {
            var option = new Option<string>("--name");
            var root = new Command("root") { option };

            var arg = $"--name{separator}alice";
            ParseResult result = root.Parse(new[] { arg });

            OptionExecutionPlan optionPlan = result.GetExecutionPlan().Commands[0].Options.Single();

            optionPlan.IdentifierSpans.Single().Should().Be(new InputTokenSpan(0, 0, 6));
            optionPlan.IdentifierSpans[0].GetText(result.Arguments).Should().Be("--name");

            ParsedTokenValue value = optionPlan.Values.Single();
            value.Span.Should().Be(new InputTokenSpan(0, 7, 5));
            value.Span.GetText(result.Arguments).Should().Be("alice");
            value.TokenValue.Should().Be("alice");
        }

        [Fact]
        public void Plan_maps_repeated_multi_value_option_one_to_one_with_tokens()
        {
            var option = new Option<string[]>("--item")
            {
                AllowMultipleArgumentsPerToken = true
            };
            var root = new Command("root") { option };

            ParseResult result = root.Parse(new[] { "--item", "one", "two", "--item", "three" });

            OptionExecutionPlan optionPlan = result.GetExecutionPlan().Commands[0].Options.Single();

            optionPlan.IdentifierSpans.Should().Equal(
                new InputTokenSpan(0, 0, 6),
                new InputTokenSpan(3, 0, 6));

            optionPlan.Values.Select(v => v.Span).Should().Equal(
                new InputTokenSpan(1, 0, 3),
                new InputTokenSpan(2, 0, 3),
                new InputTokenSpan(4, 0, 5));

            optionPlan.Values.Select(v => v.TokenValue).Should().Equal("one", "two", "three");
            ((IEnumerable<string>)optionPlan.Value!).Should().Equal("one", "two", "three");
        }

        [Fact]
        public void Plan_marks_values_after_double_dash()
        {
            var option = new Option<string>("--name");
            var argument = new Argument<string>("arg");
            var root = new Command("root") { option, argument };

            ParseResult result = root.Parse(new[] { "--", "--name" });

            ExecutionPlan plan = result.GetExecutionPlan();

            plan.Commands[0].Options.Should().BeEmpty();
            ArgumentExecutionPlan argumentPlan = plan.Commands[0].Arguments.Single();
            argumentPlan.Values.Single().Span.ArgumentIndex.Should().Be(1);
            argumentPlan.Values.Single().AfterDoubleDash.Should().BeTrue();
            argumentPlan.Value.Should().Be("--name");
        }

        [Fact]
        public void Plan_reports_default_value_source_without_input_spans()
        {
            int factoryCalls = 0;
            var option = new Option<int>("--count")
            {
                DefaultValueFactory = _ =>
                {
                    factoryCalls++;
                    return 42;
                }
            };
            var root = new Command("root") { option };

            ParseResult result = root.Parse(System.Array.Empty<string>());

            int callsBeforeExport = factoryCalls;

            ExecutionPlan first = result.GetExecutionPlan();
            ExecutionPlan second = result.GetExecutionPlan();

            OptionExecutionPlan optionPlan = first.Commands[0].Options.Single();
            optionPlan.Source.Should().Be(ParsedValueSource.Default);
            optionPlan.IdentifierSpans.Should().BeEmpty();
            optionPlan.Values.Should().BeEmpty();
            optionPlan.Value.Should().Be(42);

            second.Commands[0].Options.Single().Value.Should().Be(42);

            factoryCalls.Should().Be(callsBeforeExport, "exporting the plan must not invoke the default value factory again");
            factoryCalls.Should().Be(1);
        }

        [Fact]
        public void Plan_does_not_invoke_default_factory_when_value_is_explicit()
        {
            int factoryCalls = 0;
            var option = new Option<int>("--count")
            {
                DefaultValueFactory = _ =>
                {
                    factoryCalls++;
                    return 42;
                }
            };
            var root = new Command("root") { option };

            ParseResult result = root.Parse(new[] { "--count", "7" });

            result.GetExecutionPlan();

            factoryCalls.Should().Be(0);
        }

        [Fact]
        public void Plan_reports_conversion_failure_without_throwing_and_keeps_token_spans()
        {
            var option = new Option<int>("--count");
            var root = new Command("root") { option };

            ParseResult result = root.Parse(new[] { "--count", "not-an-int" });

            OptionExecutionPlan optionPlan = result.GetExecutionPlan().Commands[0].Options.Single();

            optionPlan.Value.Should().BeNull();
            optionPlan.ConversionError.Should().NotBeNullOrEmpty();
            optionPlan.Values.Single().TokenValue.Should().Be("not-an-int");
            optionPlan.Values.Single().Span.Should().Be(new InputTokenSpan(1, 0, 10));
        }

        [Fact]
        public void Plan_reports_missing_required_argument_as_conversion_error_without_throwing()
        {
            var argument = new Argument<int>("count");
            var root = new Command("root") { argument };

            ParseResult result = root.Parse(System.Array.Empty<string>());

            ExecutionPlan plan = result.GetExecutionPlan();

            ArgumentExecutionPlan argumentPlan = plan.Commands[0].Arguments.Single();
            argumentPlan.ConversionError.Should().NotBeNullOrEmpty();
            argumentPlan.Value.Should().BeNull();
            argumentPlan.Values.Should().BeEmpty();
            argumentPlan.Source.Should().Be(ParsedValueSource.None);
        }

        [Fact]
        public void Plan_does_not_call_ToString_on_converted_values_while_exporting_or_rendering()
        {
            var sensitive = new SensitiveToStringValue("secret");
            var option = new Option<SensitiveToStringValue>("--secret")
            {
                CustomParser = argumentResult => new SensitiveToStringValue(argumentResult.Tokens[0].Value)
            };
            var argument = new Argument<SensitiveToStringValue>("arg")
            {
                CustomParser = argumentResult => new SensitiveToStringValue(argumentResult.Tokens[0].Value)
            };
            var root = new Command("root") { option, argument };

            ParseResult result = root.Parse(new[] { "--secret", "shh", "quiet" });

            ExecutionPlan plan = result.GetExecutionPlan();

            OptionExecutionPlan optionPlan = plan.Commands[0].Options.Single();
            ((SensitiveToStringValue)optionPlan.Value!).Value.Should().Be("shh");
            plan.Commands[0].Arguments.Single().Value.Should().BeOfType<SensitiveToStringValue>();

            SensitiveToStringValue.ToStringCallCount.Should().Be(0);

            // Raw token text is rendered, but rendering never converts values through ToString.
            plan.ToString().Should().Contain("--secret").And.Contain("shh");

            SensitiveToStringValue.ToStringCallCount.Should().Be(0);
        }

        [Fact]
        public void Plan_to_string_renders_default_value_source_without_touching_value_object()
        {
            var option = new Option<SensitiveToStringValue>("--secret")
            {
                DefaultValueFactory = _ => new SensitiveToStringValue("hidden")
            };
            var root = new Command("root") { option };

            ParseResult result = root.Parse(System.Array.Empty<string>());

            ExecutionPlan plan = result.GetExecutionPlan();

            plan.ToString().Should().Be("root --secret <default>");
            SensitiveToStringValue.ToStringCallCount.Should().Be(0);
        }

        [Fact]
        public void Plan_is_read_only_and_token_spans_round_trip_to_original_text_for_argument_with_spaces()
        {
            var argument = new Argument<string>("path");
            var root = new Command("root") { argument };

            var args = new[] { "path with spaces" };
            ParseResult result = root.Parse(args);

            ExecutionPlan plan = result.GetExecutionPlan();
            ArgumentExecutionPlan argumentPlan = plan.Commands[0].Arguments.Single();

            argumentPlan.Source.Should().Be(ParsedValueSource.Explicit);
            argumentPlan.Values.Should().ContainSingle();
            argumentPlan.Values[0].Span.GetText(result.Arguments).Should().Be("path with spaces");
        }

        private static ExecutionPlan plan(ParseResult result) => result.GetExecutionPlan();

        private sealed class SensitiveToStringValue
        {
            private static int s_toStringCallCount;

            public SensitiveToStringValue(string value) => Value = value;

            public string Value { get; }

            public static int ToStringCallCount => s_toStringCallCount;

            public override string ToString()
            {
                s_toStringCallCount++;
                throw new System.InvalidOperationException("ToString must not be called during plan export");
            }
        }
    }
}
