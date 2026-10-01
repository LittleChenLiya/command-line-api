// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace System.CommandLine.Tests
{
    public class EnvironmentVariableFallbackTests : IDisposable
    {
        private readonly List<string> _environmentVariableNames = new();

        public void Dispose()
        {
            foreach (string name in _environmentVariableNames)
            {
                Environment.SetEnvironmentVariable(name, null);
            }
        }

        private string NewEnvironmentVariable()
        {
            string name = "SCL_TEST_" + Guid.NewGuid().ToString("N");
            _environmentVariableNames.Add(name);
            return name;
        }

        private void SetEnvironmentVariable(string name, string? value)
            => Environment.SetEnvironmentVariable(name, value);

        [Fact]
        public void Explicit_command_line_value_wins_over_environment_variable()
        {
            string envVar = NewEnvironmentVariable();
            SetEnvironmentVariable(envVar, "8080");

            Option<int> option = new("--port") { EnvironmentVariableName = envVar };
            RootCommand command = new() { option };

            ParseResult result = command.Parse("--port 9090");

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Be(9090);
            result.GetResult(option)!.ValueSource.Should().Be(OptionValueSource.CommandLine);
        }

        [Fact]
        public void Environment_variable_is_used_when_option_is_not_on_command_line()
        {
            string envVar = NewEnvironmentVariable();
            SetEnvironmentVariable(envVar, "8080");

            Option<int> option = new("--port") { EnvironmentVariableName = envVar };
            RootCommand command = new() { option };

            ParseResult result = command.Parse("");

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Be(8080);
            result.GetResult(option)!.ValueSource.Should().Be(OptionValueSource.EnvironmentVariable);
        }

        [Fact]
        public void Default_value_is_used_when_environment_variable_is_not_set()
        {
            Option<int> option = new("--port")
            {
                EnvironmentVariableName = NewEnvironmentVariable(),
                DefaultValueFactory = _ => 80
            };
            RootCommand command = new() { option };

            ParseResult result = command.Parse("");

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Be(80);
            result.GetResult(option)!.ValueSource.Should().Be(OptionValueSource.DefaultValue);
        }

        [Fact]
        public void Precedence_is_command_line_then_environment_variable_then_default_value()
        {
            string envVar = NewEnvironmentVariable();
            Option<int> option = new("--port")
            {
                EnvironmentVariableName = envVar,
                DefaultValueFactory = _ => 80
            };
            RootCommand command = new() { option };

            SetEnvironmentVariable(envVar, "8080");
            command.Parse("--port 9090").GetValue(option).Should().Be(9090);
            command.Parse("").GetValue(option).Should().Be(8080);

            SetEnvironmentVariable(envVar, null);
            command.Parse("").GetValue(option).Should().Be(80);
        }

        [Fact]
        public void Invalid_environment_variable_value_produces_a_parse_error_naming_the_source()
        {
            string envVar = NewEnvironmentVariable();
            SetEnvironmentVariable(envVar, "not-an-integer");

            Option<int> option = new("--port") { EnvironmentVariableName = envVar };
            RootCommand command = new() { option };

            ParseResult result = command.Parse("");

            ParseError error = result.Errors.Should().ContainSingle().Subject;
            error.Message.Should().Contain(envVar);
            error.Message.Should().Contain("--port");
            error.Message.Should().Contain("not-an-integer");
        }

        [Fact]
        public void Sensitive_option_value_is_not_echoed_in_parse_errors()
        {
            string envVar = NewEnvironmentVariable();
            SetEnvironmentVariable(envVar, "super-secret-value");

            Option<int> option = new("--secret") { EnvironmentVariableName = envVar, IsSensitive = true };
            RootCommand command = new() { option };

            ParseResult result = command.Parse("");

            ParseError error = result.Errors.Should().ContainSingle().Subject;
            error.Message.Should().Contain(envVar);
            error.Message.Should().Contain("--secret");
            error.Message.Should().NotContain("super-secret-value");
        }

        [Fact]
        public void Sensitive_option_value_from_command_line_is_not_echoed_in_parse_errors()
        {
            Option<int> option = new("--secret") { IsSensitive = true };
            RootCommand command = new() { option };

            ParseResult result = command.Parse("--secret not-an-integer");

            ParseError error = result.Errors.Should().ContainSingle().Subject;
            error.Message.Should().NotContain("not-an-integer");
        }

        [Fact]
        public void Sensitive_option_value_is_not_shown_in_parse_diagram()
        {
            string envVar = NewEnvironmentVariable();
            SetEnvironmentVariable(envVar, "12345");

            Option<int> option = new("--secret") { EnvironmentVariableName = envVar, IsSensitive = true };
            RootCommand command = new() { option };

            ParseResult result = command.Parse("");

            result.GetValue(option).Should().Be(12345);
            result.Diagram().Should().NotContain("12345");
            result.Diagram().Should().Contain("<***>");
        }

        [Fact]
        public void Non_sensitive_environment_value_is_shown_in_parse_diagram()
        {
            string envVar = NewEnvironmentVariable();
            SetEnvironmentVariable(envVar, "8080");

            Option<int> option = new("--port") { EnvironmentVariableName = envVar };
            RootCommand command = new() { option };

            ParseResult result = command.Parse("");

            result.Diagram().Should().Contain("<8080>");
        }

        [Fact]
        public void Repeated_option_on_command_line_takes_precedence_over_environment_variable()
        {
            string envVar = NewEnvironmentVariable();
            SetEnvironmentVariable(envVar, "7;8;9");

            Option<int[]> option = new("--nums") { EnvironmentVariableName = envVar };
            RootCommand command = new() { option };

            ParseResult result = command.Parse("--nums 1 --nums 2");

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Equal(1, 2);
            result.GetResult(option)!.ValueSource.Should().Be(OptionValueSource.CommandLine);
        }

        [Fact]
        public void Repeated_single_value_option_behaves_the_same_with_and_without_environment_variable()
        {
            string envVar = NewEnvironmentVariable();

            Option<int> option = new("--port") { EnvironmentVariableName = envVar };
            RootCommand command = new() { option };

            SetEnvironmentVariable(envVar, null);
            string[] withoutEnvironment = command.Parse("--port 1 --port 2").Errors.Select(e => e.Message).ToArray();

            SetEnvironmentVariable(envVar, "8080");
            ParseResult withEnvironment = command.Parse("--port 1 --port 2");

            withEnvironment.Errors.Select(e => e.Message).Should().Equal(withoutEnvironment);
            withEnvironment.Errors.Should().NotBeEmpty();
        }

        [Fact]
        public void Multiple_values_are_split_using_delimiter_and_escape_rules()
        {
            string envVar = NewEnvironmentVariable();
            // env value: a;b\;c;d\\e  ->  "a", "b;c", "d\e"
            SetEnvironmentVariable(envVar, "a;b\\;c;d\\\\e");

            Option<string[]> option = new("--items") { EnvironmentVariableName = envVar };
            RootCommand command = new() { option };

            ParseResult result = command.Parse("");

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Equal("a", "b;c", "d\\e");
        }

        [Fact]
        public void Custom_delimiter_and_escape_character_are_honored()
        {
            string envVar = NewEnvironmentVariable();
            SetEnvironmentVariable(envVar, "a!,b,c");

            Option<string[]> option = new("--items")
            {
                EnvironmentVariableName = envVar,
                EnvironmentVariableValueDelimiter = ',',
                EnvironmentVariableEscapeCharacter = '!'
            };
            RootCommand command = new() { option };

            ParseResult result = command.Parse("");

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Equal("a,b", "c");
        }

        [Fact]
        public void Escape_character_is_literal_when_not_followed_by_delimiter_or_escape()
        {
            string envVar = NewEnvironmentVariable();
            SetEnvironmentVariable(envVar, "C:\\temp;D:\\data");

            Option<string[]> option = new("--items") { EnvironmentVariableName = envVar };
            RootCommand command = new() { option };

            ParseResult result = command.Parse("");

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Equal("C:\\temp", "D:\\data");
        }

        [Fact]
        public void Explicit_empty_environment_variable_is_treated_as_a_provided_value()
        {
#if NET // On .NET Framework, setting an environment variable to an empty string deletes it.
            string envVar = NewEnvironmentVariable();
            SetEnvironmentVariable(envVar, "");

            Option<string> option = new("--name")
            {
                EnvironmentVariableName = envVar,
                DefaultValueFactory = _ => "fallback"
            };
            RootCommand command = new() { option };

            ParseResult result = command.Parse("");

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Be("");
            result.GetResult(option)!.ValueSource.Should().Be(OptionValueSource.EnvironmentVariable);
#endif
        }

        [Fact]
        public void Explicit_empty_environment_variable_yields_single_empty_element_for_collections()
        {
#if NET // On .NET Framework, setting an environment variable to an empty string deletes it.
            string envVar = NewEnvironmentVariable();
            SetEnvironmentVariable(envVar, "");

            Option<string[]> option = new("--items") { EnvironmentVariableName = envVar };
            RootCommand command = new() { option };

            ParseResult result = command.Parse("");

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Equal("");
#endif
        }

        [Fact]
        public void Consecutive_parses_observe_consecutive_environment_states()
        {
            string envVar = NewEnvironmentVariable();
            Option<int> option = new("--port") { EnvironmentVariableName = envVar };
            RootCommand command = new() { option };

            SetEnvironmentVariable(envVar, "1");
            command.Parse("").GetValue(option).Should().Be(1);

            SetEnvironmentVariable(envVar, "2");
            command.Parse("").GetValue(option).Should().Be(2);

            SetEnvironmentVariable(envVar, null);
            command.Parse("").GetResult(option).Should().BeNull();
        }

        [Fact]
        public void Options_without_environment_binding_are_not_affected_by_environment_variables()
        {
            string envVar = NewEnvironmentVariable();
            SetEnvironmentVariable(envVar, "8080");

            Option<int> option = new("--port");
            RootCommand command = new() { option };

            ParseResult result = command.Parse("");

            result.Errors.Should().BeEmpty();
            result.GetResult(option).Should().BeNull();
            result.GetValue(option).Should().Be(0);
        }

        [Fact]
        public void Help_does_not_trigger_environment_variable_reads()
        {
            string envVar = NewEnvironmentVariable();
            SetEnvironmentVariable(envVar, "not-an-integer");

            Option<int> option = new("--port") { EnvironmentVariableName = envVar };
            RootCommand command = new() { option };

            ParseResult result = command.Parse("--help");

            result.Errors.Should().BeEmpty();
            result.GetResult(option).Should().BeNull();
        }

        [Fact]
        public void Completions_do_not_trigger_environment_variable_reads()
        {
            string envVar = NewEnvironmentVariable();
            SetEnvironmentVariable(envVar, "not-an-integer");

            Option<int> option = new("--port") { EnvironmentVariableName = envVar };
            RootCommand command = new() { option };

            ParseResult result = command.Parse("[suggest:2] \"--\"");

            result.Errors.Should().BeEmpty();
            result.GetResult(option).Should().BeNull();
        }

        [Fact]
        public void Required_option_is_satisfied_by_environment_variable()
        {
            string envVar = NewEnvironmentVariable();
            SetEnvironmentVariable(envVar, "8080");

            Option<int> option = new("--port") { Required = true, EnvironmentVariableName = envVar };
            RootCommand command = new() { option };

            ParseResult result = command.Parse("");

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Be(8080);
        }

        [Fact]
        public void Required_option_still_errors_when_environment_variable_is_not_set()
        {
            Option<int> option = new("--port") { Required = true, EnvironmentVariableName = NewEnvironmentVariable() };
            RootCommand command = new() { option };

            ParseResult result = command.Parse("");

            result.Errors.Should().ContainSingle()
                  .Which.Message.Should().Contain("--port");
        }

        [Fact]
        public void Environment_variable_value_uses_the_existing_argument_converter()
        {
            string envVar = NewEnvironmentVariable();
            SetEnvironmentVariable(envVar, "21");

            Option<int> option = new("--port") { EnvironmentVariableName = envVar };
            option.CustomParser = argumentResult => int.Parse(argumentResult.Tokens[0].Value) * 2;
            RootCommand command = new() { option };

            ParseResult result = command.Parse("");

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Be(42);
        }

        [Fact]
        public void Single_value_options_treat_delimiters_literally()
        {
            string envVar = NewEnvironmentVariable();
            SetEnvironmentVariable(envVar, "a;b");

            Option<string> option = new("--name") { EnvironmentVariableName = envVar };
            RootCommand command = new() { option };

            ParseResult result = command.Parse("");

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Be("a;b");
        }

        [Fact]
        public void Recursive_option_reads_environment_variable_for_subcommands()
        {
            string envVar = NewEnvironmentVariable();
            SetEnvironmentVariable(envVar, "8080");

            Option<int> option = new("--port") { Recursive = true, EnvironmentVariableName = envVar };
            RootCommand command = new() { option, new Command("sub") };

            ParseResult result = command.Parse("sub");

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Be(8080);
        }

        [Fact]
        public void Demo_command_prints_final_value_and_safe_source()
        {
            string envVar = NewEnvironmentVariable();
            Option<int> port = new("--port")
            {
                EnvironmentVariableName = envVar,
                DefaultValueFactory = _ => 80
            };
            RootCommand command = new() { port };

            StringWriter output = new();
            command.SetAction(parseResult =>
            {
                OptionResult optionResult = parseResult.GetResult(port)!;
                parseResult.InvocationConfiguration.Output.WriteLine(
                    $"port={parseResult.GetValue(port)} source={optionResult.ValueSource}");
            });

            SetEnvironmentVariable(envVar, "8080");

            command.Parse("--port 9090").Invoke(new() { Output = output });
            output.ToString().Trim().Should().Be("port=9090 source=CommandLine");

            output = new StringWriter();
            command.Parse("").Invoke(new() { Output = output });
            output.ToString().Trim().Should().Be("port=8080 source=EnvironmentVariable");

            SetEnvironmentVariable(envVar, null);
            output = new StringWriter();
            command.Parse("").Invoke(new() { Output = output });
            output.ToString().Trim().Should().Be("port=80 source=DefaultValue");
        }
    }
}
