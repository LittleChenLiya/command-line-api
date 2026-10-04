// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using FluentAssertions;
using System.Linq;
using Xunit;

namespace System.CommandLine.StructuredBinding.Tests
{
    public class StructuredCallBinderTests
    {
        private static Command BuildDotnetTestCommand()
        {
            var nameOption = new Option<string[]>("--name", "-n")
            {
                AllowMultipleArgumentsPerToken = true
            };
            var frameworkOption = new Option<string>("--framework", "-f");
            var filterOption = new Option<string>("--filter");
            var loggerOption = new Option<string>("--logger");
            var noBuildOption = new Option<bool>("--no-build");
            var resultsDirOption = new Option<string>("--results-directory");

            var testCommand = new Command("test", "Run tests")
            {
                nameOption,
                frameworkOption,
                filterOption,
                loggerOption,
                noBuildOption,
                resultsDirOption,
                new Argument<string[]>("args")
            };

            var dotnetCommand = new Command("dotnet")
            {
                testCommand
            };

            return dotnetCommand;
        }

        [Fact]
        public void Bind_preserves_multi_values_including_spaces_and_empty_strings()
        {
            var root = BuildDotnetTestCommand();

            var call = new StructuredCall
            {
                CommandPath = new[] { "dotnet", "test" },
                Options = new Dictionary<string, StructuredOptionValue>
                {
                    ["--name"] = new StructuredOptionValue
                    {
                        Values = new[] { "two words", "", "third" }
                    }
                },
                Arguments = new[] { "positional-one", "positional-two" }
            };

            var result = StructuredCallBinder.Bind(root, call);

            result.IsSuccess.Should().BeTrue(because: string.Join("; ", result.Errors));
            result.ParseResult.Should().NotBeNull();

            var nameOption = root.Subcommands.First().Options.First(o => o.Name == "--name");
            var values = result.ParseResult!.GetResult(nameOption).Tokens.Select(t => t.Value);
            values.Should().BeEquivalentTo(new[] { "two words", "", "third" },
                options => options.WithStrictOrdering());

            result.Mapping.Should().NotBeNull();
            result.Mapping!.Count.Should().Be(result.ParseResult.Tokens.Count);
        }

        [Fact]
        public void Bind_rejects_same_option_specified_through_two_aliases()
        {
            var root = BuildDotnetTestCommand();
            var handlerCalls = 0;

            var call = new StructuredCall
            {
                CommandPath = new[] { "dotnet", "test" },
                Options = new Dictionary<string, StructuredOptionValue>
                {
                    ["--name"] = "value-one",
                    ["-n"] = "value-two"
                }
            };

            var result = StructuredCallBinder.Bind(root, call);

            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("--name") && e.Contains("-n"));
            result.ParseResult.Should().BeNull();

            // The binder never invokes a handler.
            handlerCalls.Should().Be(0);
        }

        [Fact]
        public void Bind_preserves_values_starting_with_a_dash()
        {
            var root = BuildDotnetTestCommand();

            var call = new StructuredCall
            {
                CommandPath = new[] { "dotnet", "test" },
                Options = new Dictionary<string, StructuredOptionValue>
                {
                    ["--name"] = new StructuredOptionValue
                    {
                        Values = new[] { "-dash-value", "--double-dash-value", "-v" }
                    }
                }
            };

            var result = StructuredCallBinder.Bind(root, call);

            result.IsSuccess.Should().BeTrue(because: string.Join("; ", result.Errors));

            var nameOption = root.Subcommands.First().Options.First(o => o.Name == "--name");
            var values = result.ParseResult!.GetResult(nameOption).Tokens.Select(t => t.Value);
            values.Should().BeEquivalentTo(
                new[] { "-dash-value", "--double-dash-value", "-v" },
                options => options.WithStrictOrdering());
        }

        [Fact]
        public void Bind_preserves_values_containing_quotes()
        {
            var root = BuildDotnetTestCommand();

            var call = new StructuredCall
            {
                CommandPath = new[] { "dotnet", "test" },
                Options = new Dictionary<string, StructuredOptionValue>
                {
                    ["--name"] = new StructuredOptionValue
                    {
                        Values = new[] { "say \"hello\"", "it's" }
                    }
                }
            };

            var result = StructuredCallBinder.Bind(root, call);

            result.IsSuccess.Should().BeTrue(because: string.Join("; ", result.Errors));

            var nameOption = root.Subcommands.First().Options.First(o => o.Name == "--name");
            var values = result.ParseResult!.GetResult(nameOption).Tokens.Select(t => t.Value);
            values.Should().BeEquivalentTo(
                new[] { "say \"hello\"", "it's" },
                options => options.WithStrictOrdering());
        }

        [Fact]
        public void Bind_handles_boolean_switch_with_no_values()
        {
            var root = BuildDotnetTestCommand();

            var call = new StructuredCall
            {
                CommandPath = new[] { "dotnet", "test" },
                Options = new Dictionary<string, StructuredOptionValue>
                {
                    ["--no-build"] = new StructuredOptionValue { Values = System.Array.Empty<string>() }
                }
            };

            var result = StructuredCallBinder.Bind(root, call);

            result.IsSuccess.Should().BeTrue(because: string.Join("; ", result.Errors));

            var noBuildOption = root.Subcommands.First().Options.First(o => o.Name == "--no-build");
            result.ParseResult!.GetResult(noBuildOption).Should().NotBeNull();
            result.ParseResult.GetValue<bool>("--no-build").Should().BeTrue();
        }

        [Fact]
        public void Bind_resolves_parent_command_recursive_options()
        {
            var recursiveOption = new Option<string>("--verbosity") { Recursive = true };
            var testCommand = new Command("test")
            {
                new Option<string>("--name")
            };
            var root = new Command("dotnet")
            {
                recursiveOption,
                testCommand
            };

            var call = new StructuredCall
            {
                CommandPath = new[] { "dotnet", "test" },
                Options = new Dictionary<string, StructuredOptionValue>
                {
                    ["--verbosity"] = "detailed"
                }
            };

            var result = StructuredCallBinder.Bind(root, call);

            result.IsSuccess.Should().BeTrue(because: string.Join("; ", result.Errors));
            result.ParseResult!.GetValue(recursiveOption).Should().Be("detailed");
        }

        [Fact]
        public void Bind_rejects_unknown_option()
        {
            var root = BuildDotnetTestCommand();

            var call = new StructuredCall
            {
                CommandPath = new[] { "dotnet", "test" },
                Options = new Dictionary<string, StructuredOptionValue>
                {
                    ["--unknown"] = "value"
                }
            };

            var result = StructuredCallBinder.Bind(root, call);

            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("--unknown"));
        }

        [Fact]
        public void Bind_rejects_wrong_arity_for_scalar_option()
        {
            var root = BuildDotnetTestCommand();

            var call = new StructuredCall
            {
                CommandPath = new[] { "dotnet", "test" },
                Options = new Dictionary<string, StructuredOptionValue>
                {
                    ["--framework"] = new StructuredOptionValue
                    {
                        Values = new[] { "net8.0", "net9.0" }
                    }
                }
            };

            var result = StructuredCallBinder.Bind(root, call);

            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("--framework"));
        }

        [Fact]
        public void Bind_rejects_invalid_command_path()
        {
            var root = BuildDotnetTestCommand();

            var call = new StructuredCall
            {
                CommandPath = new[] { "dotnet", "nonexistent" }
            };

            var result = StructuredCallBinder.Bind(root, call);

            result.IsSuccess.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("nonexistent"));
        }

        [Fact]
        public void Bind_returns_token_source_mapping()
        {
            var root = BuildDotnetTestCommand();

            var call = new StructuredCall
            {
                CommandPath = new[] { "dotnet", "test" },
                Options = new Dictionary<string, StructuredOptionValue>
                {
                    ["--name"] = new StructuredOptionValue
                    {
                        Values = new[] { "two words", "" }
                    }
                },
                Arguments = new[] { "arg-one" }
            };

            var result = StructuredCallBinder.Bind(root, call);

            result.IsSuccess.Should().BeTrue(because: string.Join("; ", result.Errors));
            result.Mapping.Should().NotBeNull();

            var mapping = result.Mapping!;
            mapping.Count.Should().Be(result.ParseResult!.Tokens.Count);

            // Every mapping entry references a token that is present in the parse result.
            mapping.Select(m => m.Token).Should().BeSubsetOf(result.ParseResult.Tokens);

            // The root command name (index 0, "dotnet") is implicit in the tokenizer and is not
            // emitted as a token. The first real command token is "test" at command path index 1.
            var firstCommandMapping = mapping.First(m => m.SourceKind == "command");
            firstCommandMapping.Token.Value.Should().Be("test");
            firstCommandMapping.CommandPathIndex.Should().Be(1);

            // Option values are mapped with their value index.
            var optionValueMappings = mapping.Where(m => m.SourceKind == "optionValue").ToList();
            optionValueMappings.Should().Contain(m => m.ValueIndex == 0 && m.Token.Value == "two words");
            optionValueMappings.Should().Contain(m => m.ValueIndex == 1 && m.Token.Value == "");

            // Positional arguments are mapped with their argument index.
            var argMappings = mapping.Where(m => m.SourceKind == "argument").ToList();
            argMappings.Should().Contain(m => m.ArgumentIndex == 0 && m.Token.Value == "arg-one");
        }

        [Fact]
        public void Bind_does_not_invoke_handler()
        {
            var root = BuildDotnetTestCommand();
            var invoked = false;
            root.Subcommands.First().Action = new TestAction(() => invoked = true);

            var call = new StructuredCall
            {
                CommandPath = new[] { "dotnet", "test" },
                Options = new Dictionary<string, StructuredOptionValue>
                {
                    ["--name"] = "value"
                }
            };

            var result = StructuredCallBinder.Bind(root, call);

            result.IsSuccess.Should().BeTrue(because: string.Join("; ", result.Errors));
            invoked.Should().BeFalse();
        }

        private sealed class TestAction : SynchronousCommandLineAction
        {
            private readonly System.Action _action;

            public TestAction(System.Action action) => _action = action;

            public override int Invoke(ParseResult parseResult)
            {
                _action();
                return 0;
            }
        }
    }
}

