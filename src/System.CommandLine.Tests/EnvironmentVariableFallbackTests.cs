// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace System.CommandLine.Tests
{
    public class EnvironmentVariableFallbackTests
    {
        private static ParserConfiguration ConfigurationWithEnvironment(
            Dictionary<string, string?> variables,
            Action<string>? onRead = null)
            => new()
            {
                EnvironmentVariableReader = name =>
                {
                    onRead?.Invoke(name);
                    return variables.TryGetValue(name, out string? value) ? value : null;
                }
            };

        [Fact]
        public void Command_line_value_takes_precedence_over_environment_and_default()
        {
            Option<int> option = new("--port")
            {
                DefaultValueFactory = _ => 80,
                EnvironmentVariableBinding = new("PORT")
            };
            RootCommand command = new() { option };
            ParserConfiguration configuration = ConfigurationWithEnvironment(new() { ["PORT"] = "8080" });

            ParseResult result = command.Parse("--port 9090", configuration);

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Be(9090);
            result.GetResult(option)!.EnvironmentVariableSource.Should().BeNull();
        }

        [Fact]
        public void Environment_value_is_used_when_option_is_not_on_command_line()
        {
            Option<int> option = new("--port")
            {
                DefaultValueFactory = _ => 80,
                EnvironmentVariableBinding = new("PORT")
            };
            RootCommand command = new() { option };
            ParserConfiguration configuration = ConfigurationWithEnvironment(new() { ["PORT"] = "8080" });

            ParseResult result = command.Parse("", configuration);

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Be(8080);
            result.GetResult(option)!.EnvironmentVariableSource.Should().NotBeNull();
            result.GetResult(option)!.EnvironmentVariableSource!.VariableName.Should().Be("PORT");
        }

        [Fact]
        public void Default_value_is_used_when_environment_variable_is_not_set()
        {
            Option<int> option = new("--port")
            {
                DefaultValueFactory = _ => 80,
                EnvironmentVariableBinding = new("PORT")
            };
            RootCommand command = new() { option };
            ParserConfiguration configuration = ConfigurationWithEnvironment(new());

            ParseResult result = command.Parse("", configuration);

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Be(80);
            result.GetResult(option)!.EnvironmentVariableSource.Should().BeNull();
        }

        [Fact]
        public void Invalid_environment_value_produces_a_parse_error_that_identifies_the_source()
        {
            Option<int> option = new("--port")
            {
                EnvironmentVariableBinding = new("PORT")
            };
            RootCommand command = new() { option };
            ParserConfiguration configuration = ConfigurationWithEnvironment(new() { ["PORT"] = "not-a-number" });

            ParseResult result = command.Parse("", configuration);

            ParseError error = result.Errors.Should().ContainSingle().Subject;
            error.Message.Should().Contain("PORT");
            error.Message.Should().Contain("--port");
            error.Message.Should().Contain("not-a-number");
            error.SymbolResult.Should().BeOfType<OptionResult>()
                .Which.EnvironmentVariableSource.Should().NotBeNull();
        }

        [Fact]
        public void Sensitive_option_value_is_not_echoed_in_parse_errors()
        {
            Option<int> option = new("--secret")
            {
                EnvironmentVariableBinding = new("APP_SECRET") { IsSensitive = true }
            };
            RootCommand command = new() { option };
            ParserConfiguration configuration = ConfigurationWithEnvironment(new() { ["APP_SECRET"] = "hunter2" });

            ParseResult result = command.Parse("", configuration);

            ParseError error = result.Errors.Should().ContainSingle().Subject;
            error.Message.Should().Contain("APP_SECRET");
            error.Message.Should().NotContain("hunter2");
        }

        [Fact]
        public void Sensitive_option_value_is_masked_in_parse_diagram()
        {
            Option<string> option = new("--secret")
            {
                EnvironmentVariableBinding = new("APP_SECRET") { IsSensitive = true }
            };
            RootCommand command = new() { option };
            ParserConfiguration configuration = ConfigurationWithEnvironment(new() { ["APP_SECRET"] = "hunter2" });

            ParseResult result = command.Parse("", configuration);

            string diagram = result.Diagram();
            diagram.Should().NotContain("hunter2");
            diagram.Should().Contain("<***>");
        }

        [Fact]
        public void Non_sensitive_environment_value_is_shown_in_parse_diagram()
        {
            Option<int> option = new("--port")
            {
                EnvironmentVariableBinding = new("PORT")
            };
            RootCommand command = new() { option };
            ParserConfiguration configuration = ConfigurationWithEnvironment(new() { ["PORT"] = "8080" });

            ParseResult result = command.Parse("", configuration);

            result.Diagram().Should().Contain("<8080>");
        }

        [Fact]
        public void Environment_value_satisfies_a_required_option()
        {
            Option<int> option = new("--port")
            {
                Required = true,
                EnvironmentVariableBinding = new("PORT")
            };
            RootCommand command = new() { option };
            ParserConfiguration configuration = ConfigurationWithEnvironment(new() { ["PORT"] = "8080" });

            ParseResult result = command.Parse("", configuration);

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Be(8080);
        }

        [Fact]
        public void Repeated_option_on_command_line_keeps_existing_behavior()
        {
            Option<int> boundOption = new("--port")
            {
                EnvironmentVariableBinding = new("PORT")
            };
            Option<int> unboundOption = new("--port");
            RootCommand boundCommand = new() { boundOption };
            RootCommand unboundCommand = new() { unboundOption };
            ParserConfiguration configuration = ConfigurationWithEnvironment(new() { ["PORT"] = "8080" });

            ParseResult boundResult = boundCommand.Parse("--port 1 --port 2", configuration);
            ParseResult unboundResult = unboundCommand.Parse("--port 1 --port 2");

            boundResult.Errors.Select(e => e.Message)
                .Should().BeEquivalentTo(unboundResult.Errors.Select(e => e.Message));
        }

        [Fact]
        public void Repeated_option_on_command_line_wins_over_environment_for_collections()
        {
            Option<string[]> option = new("--tag")
            {
                EnvironmentVariableBinding = new("TAGS")
            };
            RootCommand command = new() { option };
            ParserConfiguration configuration = ConfigurationWithEnvironment(new() { ["TAGS"] = "x;y" });

            ParseResult result = command.Parse("--tag a --tag b", configuration);

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().BeEquivalentTo("a", "b");
        }

        [Fact]
        public void Multiple_values_are_split_using_delimiter_and_escape_character()
        {
            Option<string[]> option = new("--tag")
            {
                EnvironmentVariableBinding = new("TAGS")
            };
            RootCommand command = new() { option };
            ParserConfiguration configuration = ConfigurationWithEnvironment(
                new() { ["TAGS"] = "a;b\\;c;d\\\\e;f\\\\" });

            ParseResult result = command.Parse("", configuration);

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().BeEquivalentTo("a", "b;c", "d\\e", "f\\");
        }

        [Fact]
        public void Custom_delimiter_and_escape_character_are_honored()
        {
            Option<string[]> option = new("--tag")
            {
                EnvironmentVariableBinding = new("TAGS") { Delimiter = '|', EscapeCharacter = '%' }
            };
            RootCommand command = new() { option };
            ParserConfiguration configuration = ConfigurationWithEnvironment(
                new() { ["TAGS"] = "a|b%|c|d%e" });

            ParseResult result = command.Parse("", configuration);

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().BeEquivalentTo("a", "b|c", "de");
        }

        [Fact]
        public void Explicit_empty_environment_value_is_not_treated_as_unset_for_collections()
        {
            Option<string[]> option = new("--tag")
            {
                DefaultValueFactory = _ => new[] { "default" },
                EnvironmentVariableBinding = new("TAGS")
            };
            RootCommand command = new() { option };
            ParserConfiguration configuration = ConfigurationWithEnvironment(new() { ["TAGS"] = "" });

            ParseResult result = command.Parse("", configuration);

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().BeEmpty();
            result.GetResult(option)!.EnvironmentVariableSource.Should().NotBeNull();
        }

        [Fact]
        public void Explicit_empty_environment_value_is_not_treated_as_unset_for_scalar_options()
        {
            Option<string> option = new("--name")
            {
                DefaultValueFactory = _ => "default",
                EnvironmentVariableBinding = new("NAME")
            };
            RootCommand command = new() { option };
            ParserConfiguration configuration = ConfigurationWithEnvironment(new() { ["NAME"] = "" });

            ParseResult result = command.Parse("", configuration);

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Be("");
        }

        [Fact]
        public void Each_parse_reads_a_fresh_snapshot_of_the_environment()
        {
            Option<int> option = new("--port")
            {
                EnvironmentVariableBinding = new("PORT")
            };
            RootCommand command = new() { option };
            Dictionary<string, string?> variables = new() { ["PORT"] = "1" };
            ParserConfiguration configuration = ConfigurationWithEnvironment(variables);

            command.Parse("", configuration).GetValue(option).Should().Be(1);

            variables["PORT"] = "2";

            command.Parse("", configuration).GetValue(option).Should().Be(2);
        }

        [Fact]
        public void Within_a_single_parse_each_variable_is_read_only_once()
        {
            Option<int> first = new("--first")
            {
                EnvironmentVariableBinding = new("SHARED")
            };
            Option<int> second = new("--second")
            {
                EnvironmentVariableBinding = new("SHARED")
            };
            RootCommand command = new() { first, second };
            List<string> reads = new();
            ParserConfiguration configuration = ConfigurationWithEnvironment(
                new() { ["SHARED"] = "5" },
                onRead: reads.Add);

            ParseResult result = command.Parse("", configuration);

            result.GetValue(first).Should().Be(5);
            result.GetValue(second).Should().Be(5);
            reads.Should().Equal("SHARED");
        }

        [Fact]
        public void Help_does_not_trigger_environment_reads()
        {
            Option<int> option = new("--port")
            {
                EnvironmentVariableBinding = new("PORT")
            };
            RootCommand command = new() { option };
            List<string> reads = new();
            ParserConfiguration configuration = ConfigurationWithEnvironment(
                new() { ["PORT"] = "8080" },
                onRead: reads.Add);

            command.Parse("--help", configuration);

            reads.Should().BeEmpty();
        }

        [Fact]
        public void Completions_do_not_trigger_environment_reads()
        {
            Option<int> option = new("--port")
            {
                EnvironmentVariableBinding = new("PORT")
            };
            RootCommand command = new() { option };
            List<string> reads = new();
            ParserConfiguration configuration = ConfigurationWithEnvironment(
                new() { ["PORT"] = "8080" },
                onRead: reads.Add);

            command.Parse("[suggest]", configuration);

            reads.Should().BeEmpty();
        }

        [Fact]
        public void Options_without_a_binding_do_not_read_the_environment()
        {
            Option<int> option = new("--port")
            {
                DefaultValueFactory = _ => 80
            };
            RootCommand command = new() { option };
            List<string> reads = new();
            ParserConfiguration configuration = ConfigurationWithEnvironment(
                new() { ["PORT"] = "8080" },
                onRead: reads.Add);

            ParseResult result = command.Parse("", configuration);

            reads.Should().BeEmpty();
            result.GetValue(option).Should().Be(80);
        }

        [Fact]
        public void Environment_fallback_applies_to_options_of_the_invoked_subcommand()
        {
            Option<int> option = new("--port")
            {
                EnvironmentVariableBinding = new("PORT")
            };
            Command subcommand = new("serve") { option };
            RootCommand command = new() { subcommand };
            ParserConfiguration configuration = ConfigurationWithEnvironment(new() { ["PORT"] = "8080" });

            ParseResult result = command.Parse("serve", configuration);

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Be(8080);
        }

        [Fact]
        public void Environment_values_are_converted_with_the_existing_argument_converter()
        {
            Option<ConsoleColor> option = new("--color")
            {
                EnvironmentVariableBinding = new("COLOR")
            };
            RootCommand command = new() { option };
            ParserConfiguration configuration = ConfigurationWithEnvironment(new() { ["COLOR"] = "Cyan" });

            ParseResult result = command.Parse("", configuration);

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Be(ConsoleColor.Cyan);
        }

        [Fact]
        public void Boolean_option_value_can_come_from_the_environment()
        {
            Option<bool> option = new("--verbose")
            {
                EnvironmentVariableBinding = new("VERBOSE")
            };
            RootCommand command = new() { option };
            ParserConfiguration configuration = ConfigurationWithEnvironment(new() { ["VERBOSE"] = "true" });

            ParseResult result = command.Parse("", configuration);

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().BeTrue();
        }
    }
}
