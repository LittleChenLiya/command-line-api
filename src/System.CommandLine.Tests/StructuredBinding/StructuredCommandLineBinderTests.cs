// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.StructuredBinding;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace System.CommandLine.Tests.StructuredBinding
{
    public class StructuredCommandLineBinderTests
    {
        [Fact]
        public void Values_with_spaces_and_empty_string_list_items_round_trip_verbatim()
        {
            var name = new Option<string>("--name", "-n");
            var item = new Option<string[]>("--item");
            var rootCommand = new RootCommand { name, item };

            var input = new StructuredCommandLine();
            input.Options.Add(new StructuredOption("--name", "two words"));
            input.Options.Add(new StructuredOption("--item", "first", "", "third value"));

            StructuredBindingResult result = StructuredCommandLineBinder.Bind(rootCommand, input);

            result.Success.Should().BeTrue(string.Join(";", result.Errors.Select(e => e.Message)));
            ParseResult parseResult = result.ParseResult!;
            parseResult.GetValue(name).Should().Be("two words");
            parseResult.GetValue(item).Should().Equal("first", "", "third value");
        }

        [Fact]
        public void Values_with_quotes_and_leading_dashes_are_not_split_or_mistaken_for_options()
        {
            var pattern = new Option<string>("--pattern");
            var other = new Option<bool>("--other");
            var arg = new Argument<string[]>("args");
            var rootCommand = new RootCommand { pattern, other, arg };

            var input = new StructuredCommandLine();
            input.Options.Add(new StructuredOption("--pattern", "-not-an-option"));
            input.Arguments.Add("--other");
            input.Arguments.Add("say \"hi\" now");

            StructuredBindingResult result = StructuredCommandLineBinder.Bind(rootCommand, input);

            result.Success.Should().BeTrue(string.Join(";", result.Errors.Select(e => e.Message)));
            ParseResult parseResult = result.ParseResult!;
            parseResult.GetValue(pattern).Should().Be("-not-an-option");
            // "--other" appears after the "--" separator, so it is bound as an argument
            // and the actual --other option is not set.
            parseResult.GetValue(other).Should().BeFalse();
            parseResult.GetValue(arg).Should().Equal("--other", "say \"hi\" now");
        }

        [Fact]
        public void Value_identical_to_another_options_alias_round_trips_as_a_value()
        {
            var name = new Option<string>("--name");
            var mode = new Option<bool>("--mode");
            var rootCommand = new RootCommand { name, mode };

            var input = new StructuredCommandLine();
            input.Options.Add(new StructuredOption("--name", "--mode"));

            StructuredBindingResult result = StructuredCommandLineBinder.Bind(rootCommand, input);

            result.Success.Should().BeTrue(string.Join(";", result.Errors.Select(e => e.Message)));
            result.ParseResult!.GetValue(name).Should().Be("--mode");
            result.ParseResult!.GetValue(mode).Should().BeFalse();
        }

        [Fact]
        public void Specifying_the_same_option_through_two_aliases_is_rejected_and_handler_is_not_invoked()
        {
            var name = new Option<string>("--name", "-n");
            var rootCommand = new RootCommand { name };
            int handlerCallCount = 0;
            rootCommand.SetAction(_ => handlerCallCount++);

            var input = new StructuredCommandLine();
            input.Options.Add(new StructuredOption("--name", "one"));
            input.Options.Add(new StructuredOption("-n", "two"));

            StructuredBindingResult result = StructuredCommandLineBinder.Bind(rootCommand, input);

            result.Success.Should().BeFalse();
            result.ParseResult.Should().BeNull();
            result.Errors.Should().ContainSingle(e => e.Kind == StructuredBindingErrorKind.DuplicateOption);
            handlerCallCount.Should().Be(0);
        }

        [Fact]
        public void Boolean_switches_can_be_emitted_bare_or_with_an_explicit_value()
        {
            var verbose = new Option<bool>("--verbose");
            var quiet = new Option<bool>("--quiet");
            var rootCommand = new RootCommand { verbose, quiet };

            var input = new StructuredCommandLine();
            input.Options.Add(new StructuredOption("--verbose"));
            input.Options.Add(new StructuredOption("--quiet", "false"));

            StructuredBindingResult result = StructuredCommandLineBinder.Bind(rootCommand, input);

            result.Success.Should().BeTrue(string.Join(";", result.Errors.Select(e => e.Message)));
            result.ParseResult!.GetValue(verbose).Should().BeTrue();
            result.ParseResult!.GetValue(quiet).Should().BeFalse();
        }

        [Fact]
        public void Recursive_option_of_parent_command_is_available_on_subcommand()
        {
            var verbose = new Option<bool>("--verbose") { Recursive = true };
            var sub = new Command("sub");
            var arg = new Argument<string>("arg");
            sub.Add(arg);
            var rootCommand = new RootCommand { verbose, sub };

            var input = new StructuredCommandLine();
            input.CommandPath.Add("sub");
            input.Options.Add(new StructuredOption("--verbose"));
            input.Arguments.Add("the-value");

            StructuredBindingResult result = StructuredCommandLineBinder.Bind(rootCommand, input);

            result.Success.Should().BeTrue(string.Join(";", result.Errors.Select(e => e.Message)));
            result.ParseResult!.GetValue(verbose).Should().BeTrue();
            result.ParseResult!.GetValue(arg).Should().Be("the-value");
        }

        [Fact]
        public void Missing_required_argument_and_required_option_fail_binding_with_parse_errors()
        {
            var required = new Option<string>("--required") { Required = true };
            var arg = new Argument<string>("arg");
            var sub = new Command("sub") { required, arg };
            var rootCommand = new RootCommand { sub };
            int handlerCallCount = 0;
            sub.SetAction(_ => handlerCallCount++);

            var input = new StructuredCommandLine();
            input.CommandPath.Add("sub");
            // neither the required option nor the required positional argument is provided

            StructuredBindingResult result = StructuredCommandLineBinder.Bind(rootCommand, input);

            result.Success.Should().BeFalse();
            result.ParseResult.Should().BeNull();
            result.Errors.Should().OnlyContain(e => e.Kind == StructuredBindingErrorKind.ParseFailure);
            result.Errors.Should().HaveCountGreaterThanOrEqualTo(2);
            handlerCallCount.Should().Be(0);
        }

        [Fact]
        public void Arity_violations_are_rejected_before_parsing()
        {
            var name = new Option<string>("--name");
            var flag = new Option<bool>("--flag") { Arity = ArgumentArity.Zero };
            var rootCommand = new RootCommand { name, flag };

            var tooMany = new StructuredCommandLine();
            tooMany.Options.Add(new StructuredOption("--name", "one", "two"));

            StructuredCommandLineBinder.Bind(rootCommand, tooMany).Errors
                .Should().ContainSingle(e => e.Kind == StructuredBindingErrorKind.InvalidValueCount);

            var valueForZeroArity = new StructuredCommandLine();
            valueForZeroArity.Options.Add(new StructuredOption("--flag", "nope"));

            StructuredCommandLineBinder.Bind(rootCommand, valueForZeroArity).Errors
                .Should().ContainSingle(e => e.Kind == StructuredBindingErrorKind.InvalidValueCount);
        }

        [Fact]
        public void Unknown_command_and_unknown_option_are_rejected_before_parsing()
        {
            var rootCommand = new RootCommand { new Command("sub") };

            var badCommand = new StructuredCommandLine();
            badCommand.CommandPath.Add("nope");
            StructuredCommandLineBinder.Bind(rootCommand, badCommand).Errors
                .Should().ContainSingle(e => e.Kind == StructuredBindingErrorKind.UnknownCommand);

            var badOption = new StructuredCommandLine();
            badOption.Options.Add(new StructuredOption("--nope", "x"));
            StructuredCommandLineBinder.Bind(rootCommand, badOption).Errors
                .Should().ContainSingle(e => e.Kind == StructuredBindingErrorKind.UnknownOption);
        }

        [Fact]
        public void Generated_tokens_carry_a_source_mapping()
        {
            var filter = new Option<string>("--filter");
            var project = new Argument<string>("project");
            var test = new Command("test") { filter, project };
            var rootCommand = new RootCommand { test };

            var input = new StructuredCommandLine();
            input.CommandPath.Add("test");
            input.Options.Add(new StructuredOption("--filter", "a b"));
            input.Arguments.Add("app.csproj");

            StructuredBindingResult result = StructuredCommandLineBinder.Bind(rootCommand, input);

            result.Success.Should().BeTrue(string.Join(";", result.Errors.Select(e => e.Message)));
            result.Tokens.Should().Equal("test", "--filter=a b", "--", "app.csproj");
            result.TokenSources.Select(s => (s.Kind, s.Key, s.Index)).Should().Equal(
                (StructuredTokenSourceKind.Command, (string?)null, 0),
                (StructuredTokenSourceKind.OptionValue, (string?)"--filter", 0),
                (StructuredTokenSourceKind.Separator, (string?)null, -1),
                (StructuredTokenSourceKind.Argument, (string?)null, 0));
        }

        [Fact]
        public void Dotnet_test_style_invocation_binds_and_parses()
        {
            // Models a local "dotnet test" invocation: subcommand, multi-value option,
            // values with spaces/semicolons, a boolean switch and a positional project path.
            var filter = new Option<string>("--filter");
            var logger = new Option<string[]>("--logger");
            var listTests = new Option<bool>("--list-tests");
            var project = new Argument<string>("project") { Arity = ArgumentArity.ZeroOrOne };
            var test = new Command("test") { filter, logger, listTests, project };
            var dotnet = new RootCommand("dotnet") { test };

            var input = new StructuredCommandLine();
            input.CommandPath.Add("test");
            input.Options.Add(new StructuredOption("--filter", "FullyQualifiedName~Structured Binding"));
            input.Options.Add(new StructuredOption("--logger", "console;verbosity=detailed", "trx"));
            input.Options.Add(new StructuredOption("--list-tests"));
            input.Arguments.Add("src/App.Tests.csproj");

            StructuredBindingResult result = StructuredCommandLineBinder.Bind(dotnet, input);

            result.Success.Should().BeTrue(string.Join(";", result.Errors.Select(e => e.Message)));
            ParseResult parseResult = result.ParseResult!;
            parseResult.CommandResult.Command.Should().BeSameAs(test);
            parseResult.GetValue(filter).Should().Be("FullyQualifiedName~Structured Binding");
            parseResult.GetValue(logger).Should().Equal("console;verbosity=detailed", "trx");
            parseResult.GetValue(listTests).Should().BeTrue();
            parseResult.GetValue(project).Should().Be("src/App.Tests.csproj");
        }
    }
}
