// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.CommandLine.Tests.Utility;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace System.CommandLine.Tests.Parsing
{
    public class CommandLineTextTests
    {
        [Fact]
        public void Tokens_round_trip_through_format_and_parse()
        {
            string[] original =
            [
                "",
                "a b",
                @"C:\temp files\",
                "say \"hi\"",
                "line1\nline2",
                "carriage\rreturn",
                "tab\there",
                "both \\ and \" together",
                "\U0001F600 emoji", // surrogate pair: two UTF-16 code units
                "trailing\\",
                "\""
            ];

            var text = CommandLineText.Format(original);

            CommandLineText.TryParse(text, out var tokens, out var error).Should().BeTrue();
            error.Should().BeNull();
            tokens.Select(t => t.Value).Should().BeEquivalentSequenceTo(original);
        }

        [Fact]
        public void An_empty_token_list_formats_as_an_empty_string()
        {
            CommandLineText.Format([])
                   .Should()
                   .BeEmpty();

            CommandLineText.TryParse("", out var tokens, out var error)
                   .Should().BeTrue();
            tokens.Should().BeEmpty();
            error.Should().BeNull();
        }

        [Fact]
        public void Format_quotes_every_token_and_escapes_special_characters()
        {
            CommandLineText.Format(["", "a b", "c:\\d", "e\"f", "g\nh"])
                   .Should()
                   .Be("\"\" \"a b\" \"c:\\\\d\" \"e\\\"f\" \"g\\nh\"");
        }

        [Fact]
        public void Formatted_text_never_contains_a_raw_newline()
        {
            var text = CommandLineText.Format(["line1\nline2", "a\rb", "c\td"]);

            text.Should().NotContain("\n");
            text.Should().NotContain("\r");
            text.Should().NotContain("\t");
        }

        [Fact]
        public void Token_ranges_are_utf16_offsets_into_the_formatted_text()
        {
            string[] original = ["", "a b", "\U0001F600", "x"];

            var text = CommandLineText.Format(original);

            CommandLineText.TryParse(text, out var tokens, out _).Should().BeTrue();

            tokens.Select(t => t.Start)
                  .Should()
                  .BeEquivalentSequenceTo(0, 2, 7, 13);

            // the emoji is two UTF-16 code units, so its quoted range is four units long
            tokens.Select(t => t.Length)
                  .Should()
                  .BeEquivalentSequenceTo(2, 5, 4, 3);

            foreach (var token in tokens)
            {
                // every range spans the token's quoted representation in the display text
                var slice = text.Substring(token.Start, token.Length);
                slice.Should().Be(CommandLineText.Format([token.Value]));
            }
        }

        [Fact]
        public void Ranges_cover_consecutive_non_overlapping_regions()
        {
            string[] original = ["one", "two words", "", "three"];

            var text = CommandLineText.Format(original);

            CommandLineText.TryParse(text, out var tokens, out _).Should().BeTrue();

            for (var i = 1; i < tokens.Count; i++)
            {
                tokens[i].Start.Should().BeGreaterThan(tokens[i - 1].End);
            }
        }

        [Fact]
        public void An_unterminated_quote_is_reported_at_the_position_of_the_opening_quote()
        {
            var text = "\"ok\" \"unterminated";

            CommandLineText.TryParse(text, out var tokens, out var error)
                   .Should().BeFalse();

            error.Should().NotBeNull();
            error!.Kind.Should().Be(CommandLineTextErrorKind.UnterminatedQuote);
            error.Position.Should().Be(5); // the opening quote of the second token
            tokens.Should().BeEmpty(); // no partial tokens are returned
        }

        [Fact]
        public void A_lone_opening_quote_is_reported_at_position_zero()
        {
            CommandLineText.TryParse("\"", out var tokens, out var error)
                   .Should().BeFalse();

            error!.Kind.Should().Be(CommandLineTextErrorKind.UnterminatedQuote);
            error.Position.Should().Be(0);
            tokens.Should().BeEmpty();
        }

        [Theory]
        [InlineData("\"a\\qb\"", 2)] // unknown escape character
        [InlineData("\"ok\" \"x\\", 7)] // backslash at end of input
        public void An_invalid_escape_is_reported_at_the_position_of_the_backslash(string text, int expectedPosition)
        {
            CommandLineText.TryParse(text, out var tokens, out var error)
                   .Should().BeFalse();

            error.Should().NotBeNull();
            error!.Kind.Should().Be(CommandLineTextErrorKind.InvalidEscape);
            error.Position.Should().Be(expectedPosition);
            tokens.Should().BeEmpty(); // no partial tokens are returned
        }

        [Fact]
        public void A_bare_word_between_tokens_is_reported_at_its_position()
        {
            CommandLineText.TryParse("\"a\" bare", out var tokens, out var error)
                   .Should().BeFalse();

            error!.Kind.Should().Be(CommandLineTextErrorKind.UnexpectedCharacter);
            error.Position.Should().Be(4);
            tokens.Should().BeEmpty();
        }

        [Fact]
        public void Whitespace_of_any_kind_separates_tokens()
        {
            CommandLineText.TryParse(" \"a\" \t \"b\"\r\n\"c\" ", out var tokens, out var error)
                   .Should().BeTrue();

            tokens.Select(t => t.Value).Should().BeEquivalentSequenceTo("a", "b", "c");
        }

        [Fact]
        public void Decoded_tokens_can_be_passed_to_Parse()
        {
            string[] original =
            [
                "",
                "a b",
                @"back\slash",
                "quo\"te",
                "new\nline"
            ];

            var argument = new Argument<string[]>("args");

            var command = new Command("the-command")
            {
                argument
            };

            var text = CommandLineText.Format(original);

            CommandLineText.TryParse(text, out var tokens, out var error)
                   .Should()
                   .BeTrue(because: error?.ToString() ?? "no error");

            IReadOnlyList<string> args = tokens.Select(t => t.Value).ToArray();

            var parseResult = CommandLineParser.Parse(command, args);

            parseResult.Errors.Should().BeEmpty();
            parseResult.GetValue(argument).Should().BeEquivalentSequenceTo(original);
        }

        [Fact]
        public void Parse_integration_preserves_tokens_that_look_like_options()
        {
            string[] original = ["--not-an-option", "-x", "[not-a-directive]"];

            var argument = new Argument<string[]>("args");

            var command = new Command("the-command")
            {
                argument
            };

            var text = CommandLineText.Format(original);

            CommandLineText.TryParse(text, out var tokens, out _).Should().BeTrue();

            // "--" stops option parsing, so option-like values still reach the argument
            var args = new[] { "--" }.Concat(tokens.Select(t => t.Value)).ToArray();

            var parseResult = CommandLineParser.Parse(command, args);

            parseResult.Errors.Should().BeEmpty();
            parseResult.GetValue(argument).Should().BeEquivalentSequenceTo(original);
        }
    }
}
