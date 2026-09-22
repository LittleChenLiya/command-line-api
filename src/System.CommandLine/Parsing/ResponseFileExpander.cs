// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Text;

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// Expands <c>@file</c> references in command line arguments, replacing them with the
    /// tokens contained in the referenced response files, and tracks the source
    /// (file, line, column, and include chain) of every produced token.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Response file paths on the command line are resolved relative to
    /// <see cref="ResponseFileExpansionOptions.BaseDirectory"/>; paths nested in a response
    /// file are resolved relative to the file that contains them.</item>
    /// <item><c>@@name</c> is treated as the literal token <c>@name</c>, and tokens after
    /// <c>--</c> are never expanded.</item>
    /// <item>Quoting follows the same rules as <see cref="CommandLineParser.SplitCommandLine(string)"/>.</item>
    /// <item>Response files are read as UTF-8, with or without a byte order mark.</item>
    /// <item>Circular references are detected using normalized full file paths, and the
    /// configured depth, byte, and token limits apply to the expansion as a whole.</item>
    /// </list>
    /// </remarks>
    public static class ResponseFileExpander
    {
        /// <summary>
        /// Expands <c>@file</c> references in the specified arguments.
        /// </summary>
        /// <param name="args">The command line arguments to expand.</param>
        /// <param name="options">The options controlling the expansion.</param>
        /// <returns>The expansion result, containing either expanded arguments or errors.</returns>
        public static ResponseFileExpansionResult Expand(IReadOnlyList<string> args, ResponseFileExpansionOptions options)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var context = new ExpansionContext(options);
            context.ExpandCommandLine(args);
            return new ResponseFileExpansionResult(
                context.Failed ? [] : context.Output,
                context.Errors);
        }

        private sealed class ExpansionContext
        {
            private static readonly StringComparer PathComparer =
                Path.DirectorySeparatorChar == '\\'
                    ? StringComparer.OrdinalIgnoreCase
                    : StringComparer.Ordinal;

            private readonly ResponseFileExpansionOptions _options;
            private readonly List<ExpandedArgument> _output = [];
            private readonly List<ResponseFileExpansionError> _errors = [];
            private readonly HashSet<string> _openFiles;
            private long _totalBytes;
            private bool _foundDoubleDash;

            internal ExpansionContext(ResponseFileExpansionOptions options)
            {
                _options = options;
                _openFiles = new HashSet<string>(PathComparer);
            }

            internal List<ExpandedArgument> Output => _output;

            internal List<ResponseFileExpansionError> Errors => _errors;

            internal bool Failed => _errors.Count > 0;

            internal void ExpandCommandLine(IReadOnlyList<string> args)
            {
                string baseDirectory = _options.BaseDirectory ?? Directory.GetCurrentDirectory();

                for (var i = 0; i < args.Count && !Failed; i++)
                {
                    ProcessToken(args[i], TokenSource.CommandLine, baseDirectory, depth: 0);
                }
            }

            private void ProcessToken(string value, TokenSource source, string currentDirectory, int depth)
            {
                if (Failed)
                {
                    return;
                }

                if (_foundDoubleDash)
                {
                    Emit(value, source);
                    return;
                }

                if (value == "--")
                {
                    _foundDoubleDash = true;
                    Emit(value, source);
                    return;
                }

                if (value.Length > 1 && value[0] == '@')
                {
                    if (value[1] == '@')
                    {
                        // @@name is the literal token @name
                        Emit(value.Substring(1), source);
                        return;
                    }

                    IncludeFile(value.Substring(1), source, currentDirectory, depth);
                    return;
                }

                Emit(value, source);
            }

            private void Emit(string value, TokenSource source)
            {
                _output.Add(new ExpandedArgument(value, source));

                if (_output.Count > _options.MaxTotalTokens)
                {
                    Fail($"Response file expansion exceeded the maximum of {_options.MaxTotalTokens} tokens.", source);
                }
            }

            private void IncludeFile(string rawPath, TokenSource includeSite, string currentDirectory, int depth)
            {
                if (depth + 1 > _options.MaxDepth)
                {
                    Fail($"Response file '{rawPath}' exceeds the maximum include depth of {_options.MaxDepth}.", includeSite);
                    return;
                }

                string fullPath = Path.GetFullPath(
                    Path.IsPathRooted(rawPath)
                        ? rawPath
                        : Path.Combine(currentDirectory, rawPath));

                if (!_openFiles.Add(fullPath))
                {
                    Fail($"Circular response file reference '{fullPath}' detected.", includeSite);
                    return;
                }

                try
                {
                    byte[] bytes = ReadFile(rawPath, fullPath, includeSite);
                    if (Failed)
                    {
                        return;
                    }

                    _totalBytes += bytes.Length;
                    if (_totalBytes > _options.MaxTotalBytes)
                    {
                        Fail($"Response file expansion exceeded the maximum of {_options.MaxTotalBytes} bytes.", includeSite);
                        return;
                    }

                    string content;
                    try
                    {
                        content = DecodeUtf8(bytes);
                    }
                    catch (DecoderFallbackException)
                    {
                        Fail($"Response file '{fullPath}' is not valid UTF-8.", new TokenSource(fullPath, 1, 1, includeSite));
                        return;
                    }

                    ProcessFileContent(content, fullPath, includeSite, depth + 1);
                }
                finally
                {
                    _openFiles.Remove(fullPath);
                }
            }

            private byte[] ReadFile(string rawPath, string fullPath, TokenSource includeSite)
            {
                try
                {
                    if (!_options.FileReader.Exists(fullPath))
                    {
                        Fail(LocalizationResources.ResponseFileNotFound(rawPath), includeSite);
                        return [];
                    }

                    return _options.FileReader.ReadAllBytes(fullPath);
                }
                catch (IOException e)
                {
                    Fail(LocalizationResources.ErrorReadingResponseFile(rawPath, e), includeSite);
                    return [];
                }
                catch (UnauthorizedAccessException e)
                {
                    Fail($"Error reading response file '{rawPath}'. {e.Message}", includeSite);
                    return [];
                }
            }

            private void ProcessFileContent(string content, string filePath, TokenSource includedFrom, int depth)
            {
                string fileDirectory = Path.GetDirectoryName(filePath)!;
                var lineNumber = 0;

                foreach (var line in EnumerateLines(content))
                {
                    lineNumber++;

                    if (Failed)
                    {
                        return;
                    }

                    if (IsBlankOrComment(line))
                    {
                        continue;
                    }

                    foreach (var token in CommandLineParser.SplitCommandLineWithPositions(line))
                    {
                        if (token.Kind == CommandLineParser.CommandLineTokenKind.UnterminatedQuote)
                        {
                            Fail(
                                $"Unterminated quote in response file '{filePath}' at line {lineNumber}, column {token.StartIndex + 1}.",
                                new TokenSource(filePath, lineNumber, token.StartIndex + 1, includedFrom));
                            return;
                        }

                        ProcessToken(
                            token.Value,
                            new TokenSource(filePath, lineNumber, token.StartIndex + 1, includedFrom),
                            fileDirectory,
                            depth);

                        if (Failed)
                        {
                            return;
                        }
                    }
                }
            }

            private void Fail(string message, TokenSource? source) =>
                _errors.Add(new ResponseFileExpansionError(message, source));

            private static bool IsBlankOrComment(string line)
            {
                for (var i = 0; i < line.Length; i++)
                {
                    if (!char.IsWhiteSpace(line[i]))
                    {
                        return line[i] == '#';
                    }
                }

                return true;
            }

            private static string DecodeUtf8(byte[] bytes)
            {
                var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
                var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
                return utf8.GetString(bytes, offset, bytes.Length - offset);
            }

            private static IEnumerable<string> EnumerateLines(string content)
            {
                var start = 0;

                for (var i = 0; i < content.Length; i++)
                {
                    var c = content[i];

                    if (c == '\r' || c == '\n')
                    {
                        yield return content.Substring(start, i - start);

                        if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                        {
                            i++;
                        }

                        start = i + 1;
                    }
                }

                if (start < content.Length)
                {
                    yield return content.Substring(start);
                }
            }
        }
    }
}
