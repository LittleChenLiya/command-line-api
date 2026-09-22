// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Xunit;

namespace System.CommandLine.Tests
{
    public class ResponseFileExpansionTests : IDisposable
    {
        private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        public ResponseFileExpansionTests() => Directory.CreateDirectory(_tempDirectory);

        public void Dispose() => Directory.Delete(_tempDirectory, recursive: true);

        private string WriteFile(string relativePath, string content, bool bom = false)
        {
            var fullPath = Path.Combine(_tempDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content, new UTF8Encoding(bom));
            return fullPath;
        }

        private ParserConfiguration CreateConfiguration(ResponseFileExpansionOptions? options = null)
        {
            options ??= new ResponseFileExpansionOptions();
            options.BaseDirectory ??= _tempDirectory;

            return new ParserConfiguration
            {
                ResponseFileExpander = new ResponseFileExpander(options)
            };
        }

        [Fact]
        public void Nested_response_files_are_expanded_and_token_sources_track_the_include_chain()
        {
            WriteFile("root.rsp", "--name \"Jane Doe\"\n@sub/child.rsp\n");
            WriteFile("sub/child.rsp", "--verbose\n@../common.rsp\n");
            WriteFile("common.rsp", "--count 42\n");

            var nameOption = new Option<string>("--name");
            var verboseOption = new Option<bool>("--verbose");
            var countOption = new Option<int>("--count");
            var command = new RootCommand { nameOption, verboseOption, countOption };

            var result = command.Parse(new[] { "@root.rsp" }, CreateConfiguration());

            result.Errors.Should().BeEmpty();
            result.GetValue(nameOption).Should().Be("Jane Doe");
            result.GetValue(verboseOption).Should().BeTrue();
            result.GetValue(countOption).Should().Be(42);

            Token nameToken = result.Tokens.Single(t => t.Value == "Jane Doe");
            nameToken.Source.Should().NotBeNull();
            nameToken.Source!.FilePath.Should().Be(Path.GetFullPath(Path.Combine(_tempDirectory, "root.rsp")));
            nameToken.Source!.Line.Should().Be(1);
            nameToken.Source!.Column.Should().Be(9);

            Token countToken = result.Tokens.Single(t => t.Value == "--count");
            countToken.Source!.FilePath.Should().Be(Path.GetFullPath(Path.Combine(_tempDirectory, "common.rsp")));
            countToken.Source!.Line.Should().Be(1);

            // the include chain leads from the command line through root.rsp and child.rsp to common.rsp
            var chain = countToken.Source!.IncludeChain;
            chain.Should().HaveCount(4);
            chain[0].Should().BeSameAs(TokenSource.CommandLine);
            chain[1].FilePath.Should().Be(Path.GetFullPath(Path.Combine(_tempDirectory, "root.rsp")));
            chain[2].FilePath.Should().Be(Path.GetFullPath(Path.Combine(_tempDirectory, "sub", "child.rsp")));
            chain[3].FilePath.Should().Be(Path.GetFullPath(Path.Combine(_tempDirectory, "common.rsp")));
        }

        [Fact]
        public void Conversion_errors_carry_the_full_include_chain()
        {
            WriteFile("root.rsp", "@sub/child.rsp\n");
            WriteFile("sub/child.rsp", "@../common.rsp\n");
            WriteFile("common.rsp", "--count notanumber\n");

            var countOption = new Option<int>("--count");
            var command = new RootCommand { countOption };

            var result = command.Parse(new[] { "@root.rsp" }, CreateConfiguration());

            var error = result.Errors.Should().ContainSingle().Subject;
            error.Message.Should().Contain("notanumber");
            error.Source.Should().NotBeNull();
            error.Source!.FilePath.Should().Be(Path.GetFullPath(Path.Combine(_tempDirectory, "common.rsp")));

            // the message contains the full include chain
            error.Message.Should().Contain("root.rsp");
            error.Message.Should().Contain("child.rsp");
            error.Message.Should().Contain("common.rsp");
        }

        [Fact]
        public void Circular_reference_between_two_files_is_detected()
        {
            WriteFile("a.rsp", "@b.rsp\n");
            WriteFile("b.rsp", "@a.rsp\n");

            var result = new RootCommand().Parse(new[] { "@a.rsp" }, CreateConfiguration());

            var error = result.Errors.Should().ContainSingle().Subject;
            error.Message.Should().Contain("Circular");
            error.Message.Should().Contain("a.rsp");
            error.Message.Should().Contain("b.rsp");
        }

        [Fact]
        public void Same_file_referenced_with_different_relative_spellings_is_detected_as_circular()
        {
            WriteFile("root.rsp", "@sub/child.rsp\n");
            WriteFile("sub/child.rsp", "@../sub/child.rsp\n");

            var result = new RootCommand().Parse(new[] { "@root.rsp" }, CreateConfiguration());

            result.Errors.Should().ContainSingle(e => e.Message.Contains("Circular"));
        }

        [Fact]
        public void Double_at_is_treated_as_a_literal_token()
        {
            var argument = new Argument<string>("value");
            var command = new RootCommand { argument };

            var result = command.Parse(new[] { "@@literal" }, CreateConfiguration());

            result.Errors.Should().BeEmpty();
            result.GetValue(argument).Should().Be("@literal");
        }

        [Fact]
        public void Tokens_after_double_dash_are_not_expanded()
        {
            var argument = new Argument<string>("value");
            var command = new RootCommand { argument };

            var result = command.Parse(new[] { "--", "@does-not-exist.rsp" }, CreateConfiguration());

            result.Errors.Should().BeEmpty();
            result.GetValue(argument).Should().Be("@does-not-exist.rsp");
        }

        [Fact]
        public void Crlf_line_endings_and_utf8_bom_are_supported()
        {
            WriteFile("root.rsp", "--name\r\n\"Jane Doe\"\r\n", bom: true);

            var nameOption = new Option<string>("--name");
            var command = new RootCommand { nameOption };

            var result = command.Parse(new[] { "@root.rsp" }, CreateConfiguration());

            result.Errors.Should().BeEmpty();
            result.GetValue(nameOption).Should().Be("Jane Doe");
        }

        [Fact]
        public void Exceeding_the_maximum_total_token_count_produces_an_error()
        {
            WriteFile("root.rsp", "one two three four\n");

            var options = new ResponseFileExpansionOptions
            {
                BaseDirectory = _tempDirectory,
                MaxTotalTokens = 3
            };

            var result = new RootCommand().Parse(new[] { "@root.rsp" }, CreateConfiguration(options));

            result.Errors.Should().ContainSingle(e => e.Message.Contains("maximum total token count"));
        }

        [Fact]
        public void Exceeding_the_maximum_include_depth_produces_an_error()
        {
            WriteFile("root.rsp", "@sub/child.rsp\n");
            WriteFile("sub/child.rsp", "--flag\n");

            var options = new ResponseFileExpansionOptions
            {
                BaseDirectory = _tempDirectory,
                MaxDepth = 1
            };

            var result = new RootCommand().Parse(new[] { "@root.rsp" }, CreateConfiguration(options));

            result.Errors.Should().ContainSingle(e => e.Message.Contains("maximum include depth"));
        }

        [Fact]
        public void Exceeding_the_maximum_total_byte_count_produces_an_error()
        {
            WriteFile("root.rsp", new string('x', 100) + "\n");

            var options = new ResponseFileExpansionOptions
            {
                BaseDirectory = _tempDirectory,
                MaxTotalBytes = 10
            };

            var result = new RootCommand().Parse(new[] { "@root.rsp" }, CreateConfiguration(options));

            result.Errors.Should().ContainSingle(e => e.Message.Contains("maximum total response file size"));
        }

        [Fact]
        public void Unterminated_quote_is_located_in_the_source_file()
        {
            WriteFile("root.rsp", "--name\n\"Jane Doe\n");

            var result = new RootCommand().Parse(new[] { "@root.rsp" }, CreateConfiguration());

            var error = result.Errors.Should().Contain(e => e.Message.Contains("Unterminated quote")).Subject;
            error.Message.Should().Contain("Unterminated quote");
            error.Message.Should().Contain("root.rsp");
            error.Message.Should().Contain("line 2");
            error.Source.Should().NotBeNull();
            error.Source!.Line.Should().Be(2);
        }

        [Fact]
        public void Invalid_utf8_is_located_in_the_source_file()
        {
            var path = Path.Combine(_tempDirectory, "root.rsp");
            File.WriteAllBytes(path, new byte[] { 0x2D, 0x2D, 0xC0, 0xAF });

            var result = new RootCommand().Parse(new[] { "@root.rsp" }, CreateConfiguration());

            var error = result.Errors.Should().ContainSingle().Subject;
            error.Message.Should().Contain("not valid UTF-8");
            error.Message.Should().Contain("root.rsp");
        }

        [Fact]
        public void Missing_response_file_produces_an_error_with_source()
        {
            var result = new RootCommand().Parse(new[] { "@missing.rsp" }, CreateConfiguration());

            var error = result.Errors.Should().ContainSingle().Subject;
            error.Message.Should().Contain("missing.rsp");
            error.Source.Should().BeSameAs(TokenSource.CommandLine);
        }

        [Fact]
        public void Unknown_option_from_a_response_file_carries_its_source()
        {
            WriteFile("root.rsp", "--unknown\n");

            var result = new RootCommand().Parse(new[] { "@root.rsp" }, CreateConfiguration());

            var error = result.Errors.Should().ContainSingle().Subject;
            error.Message.Should().Contain("--unknown");
            error.Message.Should().Contain("root.rsp");
            error.Source.Should().NotBeNull();
            error.Source!.FilePath.Should().Be(Path.GetFullPath(Path.Combine(_tempDirectory, "root.rsp")));
        }

        [Fact]
        public void Missing_option_argument_from_a_response_file_carries_its_source()
        {
            WriteFile("root.rsp", "--count\n");

            var countOption = new Option<int>("--count");
            var command = new RootCommand { countOption };

            var result = command.Parse(new[] { "@root.rsp" }, CreateConfiguration());

            var error = result.Errors.Should().ContainSingle().Subject;
            error.Source.Should().NotBeNull();
            error.Source!.FilePath.Should().Be(Path.GetFullPath(Path.Combine(_tempDirectory, "root.rsp")));
        }

        [Fact]
        public void Command_action_is_not_invoked_when_expansion_fails()
        {
            WriteFile("a.rsp", "@b.rsp\n");
            WriteFile("b.rsp", "@a.rsp\n");

            var invocationCount = 0;
            var command = new RootCommand();
            command.SetAction(_ => invocationCount++);

            var exitCode = command.Parse(new[] { "@a.rsp" }, CreateConfiguration()).Invoke();

            invocationCount.Should().Be(0);
            exitCode.Should().NotBe(0);
        }

        [Fact]
        public void Command_action_is_invoked_when_expansion_succeeds()
        {
            WriteFile("root.rsp", "--flag\n");

            var invocationCount = 0;
            var flagOption = new Option<bool>("--flag");
            var command = new RootCommand { flagOption };
            command.SetAction(_ => invocationCount++);

            var exitCode = command.Parse(new[] { "@root.rsp" }, CreateConfiguration()).Invoke();

            invocationCount.Should().Be(1);
            exitCode.Should().Be(0);
        }

        [Fact]
        public void File_reader_can_be_injected()
        {
            var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                [Path.GetFullPath(Path.Combine(_tempDirectory, "virtual.rsp"))] =
                    Encoding.UTF8.GetBytes("--flag\n")
            };

            var options = new ResponseFileExpansionOptions
            {
                BaseDirectory = _tempDirectory,
                FileReader = new InMemoryResponseFileReader(files)
            };

            var flagOption = new Option<bool>("--flag");
            var command = new RootCommand { flagOption };

            var result = command.Parse(new[] { "@virtual.rsp" }, CreateConfiguration(options));

            result.Errors.Should().BeEmpty();
            result.GetValue(flagOption).Should().BeTrue();
        }

        [Fact]
        public void Command_line_tokens_have_the_command_line_source()
        {
            var flagOption = new Option<bool>("--flag");
            var command = new RootCommand { flagOption };

            var result = command.Parse(new[] { "--flag" }, CreateConfiguration());

            result.Errors.Should().BeEmpty();
            result.Tokens.Single(t => t.Value == "--flag").Source.Should().BeSameAs(TokenSource.CommandLine);
        }

        [Fact]
        public void Default_configuration_without_expander_keeps_existing_behavior()
        {
            var responseFile = WriteFile("root.rsp", "--flag\n");

            var flagOption = new Option<bool>("--flag");
            var command = new RootCommand { flagOption };

            var result = command.Parse(new[] { $"@{responseFile}" });

            result.Errors.Should().BeEmpty();
            result.GetValue(flagOption).Should().BeTrue();
            result.Tokens.Should().OnlyContain(t => t.Source == null);
        }

        private sealed class InMemoryResponseFileReader : IResponseFileReader
        {
            private readonly IReadOnlyDictionary<string, byte[]> _files;

            public InMemoryResponseFileReader(IReadOnlyDictionary<string, byte[]> files) => _files = files;

            public byte[] ReadAllBytes(string path) =>
                _files.TryGetValue(path, out var bytes)
                    ? bytes
                    : throw new FileNotFoundException("File not found.", path);
        }
    }
}
