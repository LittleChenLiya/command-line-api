// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.CommandLine.Tests.Utility;
using System.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace System.CommandLine.Tests
{
    public class CommandLineRepresentationTests
    {
        private readonly ITestOutputHelper _output;

        public CommandLineRepresentationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // ---------------------------------------------------------------
        // Encoding tests
        // ---------------------------------------------------------------

        [Fact]
        public void Encode_empty_string_produces_empty_quotes()
        {
            CommandLineRepresentation.Encode("").Should().Be("\"\"");
        }

        [Fact]
        public void Encode_simple_token_returns_it_unchanged()
        {
            CommandLineRepresentation.Encode("hello").Should().Be("hello");
        }

        [Fact]
        public void Encode_token_with_space_wraps_in_quotes()
        {
            CommandLineRepresentation.Encode("a b").Should().Be("\"a b\"");
        }

        [Fact]
        public void Encode_token_with_backslash_escapes_it()
        {
            CommandLineRepresentation.Encode("a\\b").Should().Be("\"a\\\\b\"");
        }

        [Fact]
        public void Encode_token_with_double_quote_escapes_it()
        {
            CommandLineRepresentation.Encode("a\"b").Should().Be("\"a\\\"b\"");
        }

        [Fact]
        public void Encode_token_with_newline_escapes_it()
        {
            CommandLineRepresentation.Encode("a\nb").Should().Be("\"a\\nb\"");
        }

        [Fact]
        public void Encode_token_with_tab_escapes_it()
        {
            CommandLineRepresentation.Encode("a\tb").Should().Be("\"a\\tb\"");
        }

        [Fact]
        public void Encode_token_with_carriage_return_escapes_it()
        {
            CommandLineRepresentation.Encode("a\rb").Should().Be("\"a\\rb\"");
        }

        [Fact]
        public void Encode_token_with_null_escapes_it()
        {
            CommandLineRepresentation.Encode("a\0b").Should().Be("\"a\\0b\"");
        }

        [Fact]
        public void Encode_multiple_tokens_joins_with_space()
        {
            var tokens = new[] { "one", "two", "three" };
            CommandLineRepresentation.Encode(tokens).Should().Be("one two three");
        }

        [Fact]
        public void Encode_empty_list_returns_empty_string()
        {
            CommandLineRepresentation.Encode(System.Array.Empty<string>()).Should().Be("");
        }

        // ---------------------------------------------------------------
        // Decoding tests
        // ---------------------------------------------------------------

        [Fact]
        public void Decode_empty_string_returns_empty_list()
        {
            CommandLineRepresentation.Decode("").Should().BeEmpty();
        }

        [Fact]
        public void Decode_whitespace_only_returns_empty_list()
        {
            CommandLineRepresentation.Decode("   \t  ").Should().BeEmpty();
        }

        [Fact]
        public void Decode_simple_token()
        {
            var tokens = CommandLineRepresentation.Decode("hello");
            tokens.Should().ContainSingle();
            tokens[0].Value.Should().Be("hello");
        }

        [Fact]
        public void Decode_quoted_token_with_space()
        {
            var tokens = CommandLineRepresentation.Decode("\"a b\"");
            tokens.Should().ContainSingle();
            tokens[0].Value.Should().Be("a b");
        }

        [Fact]
        public void Decode_quoted_token_with_escaped_backslash()
        {
            var tokens = CommandLineRepresentation.Decode("\"a\\\\b\"");
            tokens.Should().ContainSingle();
            tokens[0].Value.Should().Be("a\\b");
        }

        [Fact]
        public void Decode_quoted_token_with_escaped_quote()
        {
            var tokens = CommandLineRepresentation.Decode("\"a\\\"b\"");
            tokens.Should().ContainSingle();
            tokens[0].Value.Should().Be("a\"b");
        }

        [Fact]
        public void Decode_quoted_token_with_newline_escape()
        {
            var tokens = CommandLineRepresentation.Decode("\"a\\nb\"");
            tokens.Should().ContainSingle();
            tokens[0].Value.Should().Be("a\nb");
        }

        [Fact]
        public void Decode_multiple_tokens()
        {
            var tokens = CommandLineRepresentation.Decode("one \"a b\" three");
            tokens.Should().HaveCount(3);
            tokens.Select(t => t.Value).Should().BeEquivalentSequenceTo("one", "a b", "three");
        }

        // ---------------------------------------------------------------
        // Range tests (UTF-16 offsets)
        // ---------------------------------------------------------------

        [Fact]
        public void Range_simple_token()
        {
            var text = "hello";
            var tokens = CommandLineRepresentation.Decode(text);
            tokens.Should().ContainSingle();
            tokens[0].Start.Should().Be(0);
            tokens[0].End.Should().Be(5);
            tokens[0].Length.Should().Be(5);
        }

        [Fact]
        public void Range_quoted_token_includes_quotes()
        {
            var text = "\"a b\"";
            var tokens = CommandLineRepresentation.Decode(text);
            tokens.Should().ContainSingle();
            tokens[0].Start.Should().Be(0);
            tokens[0].End.Should().Be(5);
            tokens[0].Length.Should().Be(5);
        }

        [Fact]
        public void Range_multiple_tokens()
        {
            var text = "one \"a b\" three";
            var tokens = CommandLineRepresentation.Decode(text);
            tokens.Should().HaveCount(3);

            tokens[0].Start.Should().Be(0);
            tokens[0].End.Should().Be(3);

            tokens[1].Start.Should().Be(4);
            tokens[1].End.Should().Be(9);

            tokens[2].Start.Should().Be(10);
            tokens[2].End.Should().Be(15);
        }

        [Fact]
        public void Range_empty_string_token()
        {
            var text = "\"\"";
            var tokens = CommandLineRepresentation.Decode(text);
            tokens.Should().ContainSingle();
            tokens[0].Start.Should().Be(0);
            tokens[0].End.Should().Be(2);
            tokens[0].Length.Should().Be(2);
        }

        [Fact]
        public void Range_with_leading_and_trailing_whitespace()
        {
            var text = "  hello  ";
            var tokens = CommandLineRepresentation.Decode(text);
            tokens.Should().ContainSingle();
            tokens[0].Start.Should().Be(2);
            tokens[0].End.Should().Be(7);
        }

        [Fact]
        public void Range_escaped_backslash()
        {
            var text = "\"a\\\\b\"";
            var tokens = CommandLineRepresentation.Decode(text);
            tokens.Should().ContainSingle();
            tokens[0].Start.Should().Be(0);
            tokens[0].End.Should().Be(6);
            tokens[0].Length.Should().Be(6);
        }

        // ---------------------------------------------------------------
        // Round-trip tests
        // ---------------------------------------------------------------

        [Fact]
        public void Roundtrip_empty_string()
        {
            var tokens = new[] { "" };
            var encoded = CommandLineRepresentation.Encode(tokens);
            var decoded = CommandLineRepresentation.Decode(encoded);
            decoded.Select(t => t.Value).Should().BeEquivalentSequenceTo(tokens);
        }

        [Fact]
        public void Roundtrip_space_token()
        {
            var tokens = new[] { "a b" };
            var encoded = CommandLineRepresentation.Encode(tokens);
            var decoded = CommandLineRepresentation.Decode(encoded);
            decoded.Select(t => t.Value).Should().BeEquivalentSequenceTo(tokens);
        }

        [Fact]
        public void Roundtrip_backslash_token()
        {
            var tokens = new[] { "a\\b" };
            var encoded = CommandLineRepresentation.Encode(tokens);
            var decoded = CommandLineRepresentation.Decode(encoded);
            decoded.Select(t => t.Value).Should().BeEquivalentSequenceTo(tokens);
        }

        [Fact]
        public void Roundtrip_double_quote_token()
        {
            var tokens = new[] { "a\"b" };
            var encoded = CommandLineRepresentation.Encode(tokens);
            var decoded = CommandLineRepresentation.Decode(encoded);
            decoded.Select(t => t.Value).Should().BeEquivalentSequenceTo(tokens);
        }

        [Fact]
        public void Roundtrip_newline_token()
        {
            var tokens = new[] { "a\nb" };
            var encoded = CommandLineRepresentation.Encode(tokens);
            var decoded = CommandLineRepresentation.Decode(encoded);
            decoded.Select(t => t.Value).Should().BeEquivalentSequenceTo(tokens);
        }

        [Fact]
        public void Roundtrip_mixed_array()
        {
            // Array containing empty string, "a b", backslash, and double quote.
            var tokens = new[] { "", "a b", "a\\b", "a\"b" };
            var encoded = CommandLineRepresentation.Encode(tokens);
            _output.WriteLine($"Encoded: {encoded}");
            var decoded = CommandLineRepresentation.Decode(encoded);
            decoded.Select(t => t.Value).Should().BeEquivalentSequenceTo(tokens);
        }

        [Fact]
        public void Roundtrip_all_special_characters()
        {
            var tokens = new[] { "", " ", "\t", "\n", "\r", "\\", "\"", "a b", "a\\b", "a\"b", "line1\nline2" };
            var encoded = CommandLineRepresentation.Encode(tokens);
            _output.WriteLine($"Encoded: {encoded}");
            var decoded = CommandLineRepresentation.Decode(encoded);
            decoded.Select(t => t.Value).Should().BeEquivalentSequenceTo(tokens);
        }

        [Fact]
        public void Roundtrip_unicode_escape()
        {
            var tokens = new[] { "café" };
            var encoded = CommandLineRepresentation.Encode(tokens);
            var decoded = CommandLineRepresentation.Decode(encoded);
            decoded.Select(t => t.Value).Should().BeEquivalentSequenceTo(tokens);
        }

        // ---------------------------------------------------------------
        // Error reporting tests
        // ---------------------------------------------------------------

        [Fact]
        public void TryDecode_unclosed_quote_reports_start_position()
        {
            var text = "\"abc";
            var result = CommandLineRepresentation.TryDecode(text, out var tokens, out var error);
            result.Should().BeFalse();
            tokens.Should().BeEmpty();
            error.Should().NotBeNull();
            error!.Position.Should().Be(0);
            error.Kind.Should().Be(RepresentationErrorKind.UnclosedQuote);
        }

        [Fact]
        public void TryDecode_unclosed_quote_after_simple_token()
        {
            var text = "hello \"abc";
            var result = CommandLineRepresentation.TryDecode(text, out var tokens, out var error);
            result.Should().BeFalse();
            tokens.Should().BeEmpty();
            error.Should().NotBeNull();
            error!.Position.Should().Be(6);
            error.Kind.Should().Be(RepresentationErrorKind.UnclosedQuote);
        }

        [Fact]
        public void TryDecode_illegal_escape_reports_backslash_position()
        {
            var text = "\"a\\xb\"";
            var result = CommandLineRepresentation.TryDecode(text, out var tokens, out var error);
            result.Should().BeFalse();
            tokens.Should().BeEmpty();
            error.Should().NotBeNull();
            error!.Position.Should().Be(2);
            error.Kind.Should().Be(RepresentationErrorKind.IllegalEscape);
        }

        [Fact]
        public void TryDecode_backslash_at_end_reports_position()
        {
            var text = "\"abc\\";
            var result = CommandLineRepresentation.TryDecode(text, out var tokens, out var error);
            result.Should().BeFalse();
            tokens.Should().BeEmpty();
            error.Should().NotBeNull();
            error!.Position.Should().Be(4);
            error.Kind.Should().Be(RepresentationErrorKind.IllegalEscape);
        }

        [Fact]
        public void TryDecode_incomplete_unicode_escape()
        {
            var text = "\"a\\u00\"";
            var result = CommandLineRepresentation.TryDecode(text, out var tokens, out var error);
            result.Should().BeFalse();
            tokens.Should().BeEmpty();
            error.Should().NotBeNull();
            error!.Position.Should().Be(2);
            error.Kind.Should().Be(RepresentationErrorKind.InvalidUnicodeEscape);
        }

        [Fact]
        public void TryDecode_invalid_unicode_escape()
        {
            var text = "\"a\\uXXXX\"";
            var result = CommandLineRepresentation.TryDecode(text, out var tokens, out var error);
            result.Should().BeFalse();
            tokens.Should().BeEmpty();
            error.Should().NotBeNull();
            error!.Position.Should().Be(2);
            error.Kind.Should().Be(RepresentationErrorKind.InvalidUnicodeEscape);
        }

        [Fact]
        public void Decode_throws_on_unclosed_quote()
        {
            var action = () => CommandLineRepresentation.Decode("\"abc");
            action.Should().Throw<FormatException>();
        }

        [Fact]
        public void Decode_throws_on_illegal_escape()
        {
            var action = () => CommandLineRepresentation.Decode("\"a\\xb\"");
            action.Should().Throw<FormatException>();
        }

        // ---------------------------------------------------------------
        // Parse linkage tests
        // ---------------------------------------------------------------

        [Fact]
        public void Decoded_tokens_can_be_passed_to_parse()
        {
            var command = new RootCommand();
            var option = new Option<string>("--name");
            command.Add(option);

            var tokens = new[] { "--name", "a b" };
            var encoded = CommandLineRepresentation.Encode(tokens);
            var decoded = CommandLineRepresentation.Decode(encoded);

            var parseResult = command.Parse(decoded.Select(t => t.Value).ToArray());

            parseResult.GetValue(option).Should().Be("a b");
        }

        [Fact]
        public void Decoded_tokens_with_special_chars_can_be_passed_to_parse()
        {
            var command = new RootCommand();
            var option = new Option<string>("--value");
            command.Add(option);

            var tokens = new[] { "--value", "a\\b\"c" };
            var encoded = CommandLineRepresentation.Encode(tokens);
            var decoded = CommandLineRepresentation.Decode(encoded);

            var parseResult = command.Parse(decoded.Select(t => t.Value).ToArray());

            parseResult.GetValue(option).Should().Be("a\\b\"c");
        }

        [Fact]
        public void Decoded_tokens_with_empty_string_can_be_passed_to_parse()
        {
            var command = new RootCommand();
            var option = new Option<string>("--value");
            command.Add(option);

            var tokens = new[] { "--value", "" };
            var encoded = CommandLineRepresentation.Encode(tokens);
            var decoded = CommandLineRepresentation.Decode(encoded);

            var parseResult = command.Parse(decoded.Select(t => t.Value).ToArray());

            parseResult.GetValue(option).Should().Be("");
        }

        [Fact]
        public void Decoded_tokens_with_newline_can_be_passed_to_parse()
        {
            var command = new RootCommand();
            var option = new Option<string>("--value");
            command.Add(option);

            var tokens = new[] { "--value", "line1\nline2" };
            var encoded = CommandLineRepresentation.Encode(tokens);
            var decoded = CommandLineRepresentation.Decode(encoded);

            var parseResult = command.Parse(decoded.Select(t => t.Value).ToArray());

            parseResult.GetValue(option).Should().Be("line1\nline2");
        }

        [Fact]
        public void Decoded_tokens_match_splitcommandline_output()
        {
            // The decoded tokens should be consistent with what the original Parse produces
            // when given the same display text via the string overload.
            var command = new RootCommand();
            var option = new Option<string>("--name");
            command.Add(option);

            var text = "--name \"a b\"";
            var decoded = CommandLineRepresentation.Decode(text);

            var parseFromTokens = command.Parse(decoded.Select(t => t.Value).ToArray());
            var parseFromString = command.Parse(text);

            parseFromTokens.GetValue(option).Should().Be(parseFromString.GetValue(option));
        }

        [Fact]
        public void Decoded_tokens_ranges_align_with_display_text()
        {
            // Verify that the ranges can be used to extract the original token text
            // from the display string.
            var text = "one \"a b\" three";
            var tokens = CommandLineRepresentation.Decode(text);

            tokens[0].Start.Should().Be(0);
            tokens[0].End.Should().Be(3);
            text.Substring(tokens[0].Start, tokens[0].Length).Should().Be("one");

            tokens[1].Start.Should().Be(4);
            tokens[1].End.Should().Be(9);
            text.Substring(tokens[1].Start, tokens[1].Length).Should().Be("\"a b\"");

            tokens[2].Start.Should().Be(10);
            tokens[2].End.Should().Be(15);
            text.Substring(tokens[2].Start, tokens[2].Length).Should().Be("three");
        }
    }
}
