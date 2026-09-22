// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.CommandLine.Parsing;
using System.CommandLine.Tests.Utility;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Xunit;

namespace System.CommandLine.Tests
{
    public class ResponseFileExpansionTests : IDisposable
    {
        private readonly string _rootDirectory;
        private readonly ResponseFileExpansionOptions _options;
        private readonly ParserConfiguration _configuration;

        public ResponseFileExpansionTests()
        {
            _rootDirectory = Path.Combine(Path.GetTempPath(), "scl-rsp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_rootDirectory);
            _options = new ResponseFileExpansionOptions { BaseDirectory = _rootDirectory };
            _configuration = new ParserConfiguration { ResponseFileExpansion = _options };
        }

        public void Dispose() => Directory.Delete(_rootDirectory, recursive: true);

        private string WriteFile(string relativePath, string content, bool bom = false, string? newLine = null)
        {
            var path = Path.Combine(_rootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            if (newLine is not null)
            {
                content = content.Replace("\n", newLine);
            }

            File.WriteAllText(path, content, new UTF8Encoding(bom));
            return path;
        }

        private ParseResult Parse(Command command, params string[] args) =>
            CommandLineParser.Parse(command, args, _configuration);

        [Fact]
        public void Nested_response_files_resolve_relative_to_including_file_and_track_sources()
        {
            WriteFile("common.rsp", "--number 42");
            WriteFile("sub/child.rsp", "@../common.rsp");
            WriteFile("root.rsp", "--name \"hello world\"\n@sub/child.rsp");

            var nameOption = new Option<string>("--name");
            var numberOption = new Option<int>("--number");
            var command = new RootCommand { nameOption, numberOption };

            var result = Parse(command, "@root.rsp");

            result.Errors.Should().BeEmpty();
            result.GetValue(nameOption).Should().Be("hello world");
            result.GetValue(numberOption).Should().Be(42);

            // an argument containing spaces keeps its source location in root.rsp
            var nameToken = result.Tokens.Single(t => t.Value == "hello world");
            nameToken.Source.Should().NotBeNull();
            nameToken.Source!.FilePath.Should().Be(Path.Combine(_rootDirectory, "root.rsp"));
            nameToken.Source.Line.Should().Be(1);
            nameToken.Source.Column.Should().Be(8);

            // a token from common.rsp carries the full include chain
            var numberToken = result.Tokens.Single(t => t.Value == "42");
            numberToken.Source!.FilePath.Should().Be(Path.Combine(_rootDirectory, "common.rsp"));
            numberToken.Source.Line.Should().Be(1);
            numberToken.Source.IncludeChain
                  .Select(s => s.FilePath)
                  .Should()
                  .BeEquivalentSequenceTo(
                      Path.Combine(_rootDirectory, "root.rsp"),
                      Path.Combine(_rootDirectory, "sub", "child.rsp"),
                      Path.Combine(_rootDirectory, "common.rsp"));
        }

        [Fact]
        public void Command_line_tokens_are_sourced_to_the_command_line()
        {
            var argument = new Argument<string>("arg");
            var command = new RootCommand { argument };

            var result = Parse(command, "value");

            result.Errors.Should().BeEmpty();
            result.Tokens.Single(t => t.Value == "value").Source!.IsCommandLine.Should().BeTrue();
        }

        [Fact]
        public void Conversion_errors_carry_source_with_full_include_chain()
        {
            WriteFile("common.rsp", "--number notanumber");
            WriteFile("sub/child.rsp", "@../common.rsp");
            WriteFile("root.rsp", "@sub/child.rsp");

            var numberOption = new Option<int>("--number");
            var command = new RootCommand { numberOption };

            var result = Parse(command, "@root.rsp");

            var error = result.Errors.Should().ContainSingle().Subject;
            error.Source.Should().NotBeNull();
            error.Source!.FilePath.Should().Be(Path.Combine(_rootDirectory, "common.rsp"));
            error.Source.Line.Should().Be(1);
            error.Source.IncludeChain
                  .Select(s => s.FilePath)
                  .Should()
                  .BeEquivalentSequenceTo(
                      Path.Combine(_rootDirectory, "root.rsp"),
                      Path.Combine(_rootDirectory, "sub", "child.rsp"),
                      Path.Combine(_rootDirectory, "common.rsp"));
        }

        [Fact]
        public void Unrecognized_option_errors_carry_source()
        {
            WriteFile("root.rsp", "--unknown");

            var result = Parse(new RootCommand(), "@root.rsp");

            var error = result.Errors.Should().ContainSingle().Subject;
            error.Message.Should().Contain("--unknown");
            error.Source.Should().NotBeNull();
            error.Source!.FilePath.Should().Be(Path.Combine(_rootDirectory, "root.rsp"));
            error.Source.Line.Should().Be(1);
            error.Source.Column.Should().Be(1);
        }

        [Fact]
        public void Missing_option_argument_errors_carry_source()
        {
            WriteFile("root.rsp", "--number");

            var numberOption = new Option<int>("--number");
            var command = new RootCommand { numberOption };

            var result = Parse(command, "@root.rsp");

            var error = result.Errors.Should().ContainSingle().Subject;
            error.Source.Should().NotBeNull();
            error.Source!.FilePath.Should().Be(Path.Combine(_rootDirectory, "root.rsp"));
            error.Source.Line.Should().Be(1);
        }

        [Fact]
        public void Circular_references_are_detected_using_normalized_paths()
        {
            WriteFile("a.rsp", "@b.rsp");
            WriteFile("b.rsp", "@./a.rsp");

            var result = Parse(new RootCommand(), "@a.rsp");

            result.Errors.Should().ContainSingle()
                  .Which.Message.Should().Contain("Circular");
        }

        [Fact]
        public void Same_file_referenced_with_different_relative_spelling_is_detected_as_cycle()
        {
            WriteFile("sub/self.rsp", "@../sub/self.rsp");

            var result = Parse(new RootCommand(), "@sub/self.rsp");

            result.Errors.Should().ContainSingle()
                  .Which.Message.Should().Contain("Circular");
        }

        [Fact]
        public void Double_at_prefix_produces_literal_at_token()
        {
            var argument = new Argument<string[]>("arg");
            var command = new RootCommand { argument };

            var result = Parse(command, "@@literal");

            result.Errors.Should().BeEmpty();
            result.GetValue(argument).Should().BeEquivalentSequenceTo("@literal");
        }

        [Fact]
        public void Double_at_prefix_produces_literal_at_token_inside_response_files()
        {
            WriteFile("root.rsp", "@@fromfile");

            var argument = new Argument<string[]>("arg");
            var command = new RootCommand { argument };

            var result = Parse(command, "@root.rsp");

            result.Errors.Should().BeEmpty();
            result.GetValue(argument).Should().BeEquivalentSequenceTo("@fromfile");
        }

        [Fact]
        public void Tokens_after_double_dash_are_not_expanded()
        {
            var argument = new Argument<string[]>("arg");
            var command = new RootCommand { argument };

            var result = Parse(command, "--", "@missing.rsp");

            result.Errors.Should().BeEmpty();
            result.GetValue(argument).Should().BeEquivalentSequenceTo("@missing.rsp");
        }

        [Fact]
        public void Tokens_after_double_dash_inside_response_files_are_not_expanded()
        {
            WriteFile("root.rsp", "-- @missing.rsp");

            var argument = new Argument<string[]>("arg");
            var command = new RootCommand { argument };

            var result = Parse(command, "@root.rsp");

            result.Errors.Should().BeEmpty();
            result.GetValue(argument).Should().BeEquivalentSequenceTo("@missing.rsp");
        }

        [Fact]
        public void Response_files_with_crlf_and_bom_are_supported()
        {
            WriteFile("bom.rsp", "--flag\n\"value with spaces\"\n", bom: true, newLine: "\r\n");

            var option = new Option<string>("--flag");
            var command = new RootCommand { option };

            var result = Parse(command, "@bom.rsp");

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Be("value with spaces");
        }

        [Fact]
        public void Total_token_limit_applies_to_the_whole_expansion()
        {
            WriteFile("common.rsp", "a b c");
            WriteFile("root.rsp", "x y @common.rsp");
            _options.MaxTotalTokens = 4;

            var result = Parse(new RootCommand(), "@root.rsp");

            result.Errors.Should().ContainSingle()
                  .Which.Message.Should().Contain("maximum of 4 tokens");
        }

        [Fact]
        public void Max_depth_limit_applies_to_the_whole_expansion()
        {
            WriteFile("sub/child.rsp", "value");
            WriteFile("root.rsp", "@sub/child.rsp");
            _options.MaxDepth = 1;

            var result = Parse(new RootCommand(), "@root.rsp");

            result.Errors.Should().ContainSingle()
                  .Which.Message.Should().Contain("maximum include depth of 1");
        }

        [Fact]
        public void Total_byte_limit_applies_to_the_whole_expansion()
        {
            WriteFile("common.rsp", "some content");
            WriteFile("root.rsp", "@common.rsp");
            _options.MaxTotalBytes = 5;

            var result = Parse(new RootCommand(), "@root.rsp");

            result.Errors.Should().ContainSingle()
                  .Which.Message.Should().Contain("maximum of 5 bytes");
        }

        [Fact]
        public void Unterminated_quotes_are_located_in_the_source_file()
        {
            WriteFile("root.rsp", "--name \"unclosed");

            var result = Parse(new RootCommand(), "@root.rsp");

            var error = result.Errors.Should().ContainSingle().Subject;
            error.Message.Should().Contain("Unterminated quote");
            error.Source.Should().NotBeNull();
            error.Source!.FilePath.Should().Be(Path.Combine(_rootDirectory, "root.rsp"));
            error.Source.Line.Should().Be(1);
            error.Source.Column.Should().Be(8);
        }

        [Fact]
        public void Invalid_encoding_is_located_in_the_source_file()
        {
            var path = Path.Combine(_rootDirectory, "invalid.rsp");
            File.WriteAllBytes(path, [0x2D, 0x2D, 0x61, 0x20, 0xFF, 0xFE, 0x00]);

            var result = Parse(new RootCommand(), "@invalid.rsp");

            var error = result.Errors.Should().ContainSingle().Subject;
            error.Message.Should().Contain("UTF-8");
            error.Source.Should().NotBeNull();
            error.Source!.FilePath.Should().Be(path);
        }

        [Fact]
        public void File_reader_can_be_injected()
        {
            var reader = new InMemoryResponseFileReader();
            var rootPath = Path.Combine(_rootDirectory, "root.rsp");
            var childPath = Path.Combine(_rootDirectory, "sub", "child.rsp");
            reader.Add(rootPath, "--flag @sub/child.rsp");
            reader.Add(childPath, "123");
            _options.FileReader = reader;

            var option = new Option<int>("--flag");
            var command = new RootCommand { option };

            var result = Parse(command, "@root.rsp");

            result.Errors.Should().BeEmpty();
            result.GetValue(option).Should().Be(123);
            result.Tokens.Single(t => t.Value == "123").Source!.FilePath.Should().Be(childPath);
            File.Exists(rootPath).Should().BeFalse();
        }

        [Fact]
        public void Command_action_is_not_invoked_when_expansion_fails()
        {
            WriteFile("a.rsp", "@b.rsp");
            WriteFile("b.rsp", "@a.rsp");

            var invocationCount = 0;
            var command = new RootCommand();
            command.SetAction(_ => invocationCount++);

            var result = Parse(command, "@a.rsp");
            var exitCode = result.Invoke();

            result.Errors.Should().NotBeEmpty();
            invocationCount.Should().Be(0);
            exitCode.Should().Be(1);
        }

        [Fact]
        public void Command_action_is_invoked_when_expansion_succeeds()
        {
            WriteFile("root.rsp", "--flag");

            var invocationCount = 0;
            var option = new Option<bool>("--flag");
            var command = new RootCommand { option };
            command.SetAction(_ => invocationCount++);

            var result = Parse(command, "@root.rsp");
            var exitCode = result.Invoke();

            result.Errors.Should().BeEmpty();
            invocationCount.Should().Be(1);
            exitCode.Should().Be(0);
        }

        private sealed class InMemoryResponseFileReader : IResponseFileReader
        {
            private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

            public void Add(string path, string content) =>
                _files[Path.GetFullPath(path)] = Encoding.UTF8.GetBytes(content);

            public bool Exists(string path) => _files.ContainsKey(path);

            public byte[] ReadAllBytes(string path) => _files[path];
        }
    }
}
