// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace System.CommandLine.Parsing
{
    /// <summary>
    /// Expands <c>@file</c> response file references in a list of arguments, tracking the
    /// source (file, line, column and include chain) of every token produced.
    /// </summary>
    /// <remarks>
    /// <para>Rules:</para>
    /// <list type="bullet">
    /// <item><description>A token of the form <c>@file</c> is replaced by the tokens read from the response file.</description></item>
    /// <item><description>A token of the form <c>@@name</c> is treated as the literal token <c>@name</c>.</description></item>
    /// <item><description>Tokens appearing after a <c>--</c> token are never expanded.</description></item>
    /// <item><description>Relative paths inside a response file are resolved against the directory of the containing file; relative paths on the command line are resolved against <see cref="ResponseFileExpansionOptions.BaseDirectory"/>.</description></item>
    /// <item><description>Quoting follows the same rules as <see cref="CommandLineParser.SplitCommandLine(string)"/>.</description></item>
    /// <item><description>Response files are read as UTF-8 (a byte order mark is allowed).</description></item>
    /// </list>
    /// </remarks>
    public sealed class ResponseFileExpander
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        private readonly ResponseFileExpansionOptions _options;
        private readonly IResponseFileReader _fileReader;
        private readonly StringComparer _pathComparer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResponseFileExpander"/> class.
        /// </summary>
        /// <param name="options">The options controlling the expansion, or <see langword="null"/> to use defaults.</param>
        public ResponseFileExpander(ResponseFileExpansionOptions? options = null)
        {
            _options = options ?? new ResponseFileExpansionOptions();
            _fileReader = _options.FileReader ?? new FileSystemResponseFileReader();
            _pathComparer = Path.DirectorySeparatorChar == '\\'
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
        }

        /// <summary>
        /// Expands any <c>@file</c> response file references in the specified arguments.
        /// </summary>
        /// <param name="args">The arguments to expand.</param>
        /// <returns>A <see cref="ResponseFileExpansionResult"/> containing the expanded arguments,
        /// the provenance of each argument, and any errors encountered.</returns>
        public ResponseFileExpansionResult Expand(IReadOnlyList<string> args)
        {
            if (args is null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            var state = new ExpansionState(this);

            for (var i = 0; i < args.Count; i++)
            {
                state.ProcessArgument(args[i], TokenSource.CommandLine, depth: 0);
            }

            return new ResponseFileExpansionResult(state.Arguments, state.Sources, state.Errors);
        }

        private string ResolvePath(string path, TokenSource? source)
        {
            if (!Path.IsPathRooted(path))
            {
                if (source?.FilePath is { } containingFile)
                {
                    path = Path.Combine(Path.GetDirectoryName(containingFile)!, path);
                }
                else
                {
                    path = Path.Combine(_options.BaseDirectory ?? Directory.GetCurrentDirectory(), path);
                }
            }

            return Path.GetFullPath(path);
        }

        private sealed class ExpansionState
        {
            private readonly ResponseFileExpander _expander;
            private readonly List<string> _arguments = new();
            private readonly List<TokenSource?> _sources = new();
            private readonly List<ParseError> _errors = new();
            private readonly List<string> _includeStack = new();
            private long _totalBytes;
            private int _totalTokens;
            private bool _doubleDashSeen;

            public ExpansionState(ResponseFileExpander expander) => _expander = expander;

            public IReadOnlyList<string> Arguments => _arguments;

            public IReadOnlyList<TokenSource?> Sources => _sources;

            public IReadOnlyList<ParseError> Errors => _errors;

            private bool Failed => _errors.Count > 0;

            public void ProcessArgument(string arg, TokenSource source, int depth)
            {
                if (Failed)
                {
                    return;
                }

                if (_doubleDashSeen)
                {
                    Emit(arg, source);
                    return;
                }

                if (arg == "--")
                {
                    _doubleDashSeen = true;
                    Emit(arg, source);
                    return;
                }

                if (arg.Length > 1 && arg[0] == '@')
                {
                    if (arg[1] == '@')
                    {
                        // @@name is an escaped literal @name
                        Emit(arg.Substring(1), source);
                        return;
                    }

                    ExpandFile(arg.Substring(1), source, depth);
                    return;
                }

                Emit(arg, source);
            }

            private void Emit(string value, TokenSource? source)
            {
                _arguments.Add(value);
                _sources.Add(source);
            }

            private void ExpandFile(string path, TokenSource referenceSource, int depth)
            {
                string fullPath;
                try
                {
                    fullPath = _expander.ResolvePath(path, referenceSource);
                }
                catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException)
                {
                    AddError($"Response file path '{path}' is not valid: {e.Message}", referenceSource);
                    return;
                }

                if (_includeStack.Contains(fullPath, _expander._pathComparer))
                {
                    AddError($"Circular response file reference detected: '{fullPath}'.", referenceSource);
                    return;
                }

                if (depth + 1 > _expander._options.MaxDepth)
                {
                    AddError($"Response file '{fullPath}' exceeds the maximum include depth of {_expander._options.MaxDepth}.", referenceSource);
                    return;
                }

                byte[] bytes;
                try
                {
                    bytes = _expander._fileReader.ReadAllBytes(fullPath);
                }
                catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException)
                {
                    AddError(LocalizationResources.ResponseFileNotFound(fullPath), referenceSource);
                    return;
                }
                catch (IOException e)
                {
                    AddError(LocalizationResources.ErrorReadingResponseFile(fullPath, e), referenceSource);
                    return;
                }
                catch (UnauthorizedAccessException e)
                {
                    AddError($"Error reading response file '{fullPath}': {e.Message}", referenceSource);
                    return;
                }

                _totalBytes += bytes.Length;
                if (_totalBytes > _expander._options.MaxTotalBytes)
                {
                    AddError($"Response file '{fullPath}' exceeds the maximum total response file size of {_expander._options.MaxTotalBytes} bytes.", referenceSource);
                    return;
                }

                string content;
                try
                {
                    // skip the UTF-8 byte order mark if present
                    var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
                    content = StrictUtf8.GetString(bytes, offset, bytes.Length - offset);
                }
                catch (DecoderFallbackException e)
                {
                    AddError($"Response file '{fullPath}' is not valid UTF-8: {e.Message}", referenceSource);
                    return;
                }

                _includeStack.Add(fullPath);
                try
                {
                    TokenizeFileContent(content, fullPath, referenceSource, depth + 1);
                }
                finally
                {
                    _includeStack.RemoveAt(_includeStack.Count - 1);
                }
            }

            private void TokenizeFileContent(string content, string fullPath, TokenSource referenceSource, int depth)
            {
                var lineNumber = 0;
                var lineStart = 0;

                while (lineStart <= content.Length)
                {
                    if (Failed)
                    {
                        return;
                    }

                    lineNumber++;

                    int lineEnd = content.IndexOf('\n', lineStart);
                    if (lineEnd < 0)
                    {
                        lineEnd = content.Length;
                    }

                    string line = content.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r');

                    TokenizeLine(line, lineNumber, fullPath, referenceSource, depth);

                    if (lineEnd == content.Length)
                    {
                        break;
                    }

                    lineStart = lineEnd + 1;
                }
            }

            private void TokenizeLine(string line, int lineNumber, string fullPath, TokenSource referenceSource, int depth)
            {
                string trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed[0] == '#')
                {
                    return;
                }

                // Uses the same quoting rules as CommandLineParser.SplitCommandLine,
                // additionally tracking the column of each token and detecting unterminated quotes.
                var seeking = Boundary.TokenStart;
                var seekingQuote = Boundary.QuoteStart;
                var startTokenIndex = 0;
                var openQuoteIndex = -1;

                for (var pos = 0; pos <= line.Length; pos++)
                {
                    if (pos < line.Length && char.IsWhiteSpace(line[pos]))
                    {
                        if (seekingQuote == Boundary.QuoteStart)
                        {
                            if (seeking == Boundary.WordEnd)
                            {
                                EmitFileToken(line, startTokenIndex, pos, lineNumber, fullPath, referenceSource, depth);
                                if (Failed)
                                {
                                    return;
                                }

                                seeking = Boundary.TokenStart;
                            }

                            startTokenIndex = pos;
                        }
                    }
                    else if (pos < line.Length && line[pos] == '"')
                    {
                        if (seeking == Boundary.TokenStart)
                        {
                            if (seekingQuote == Boundary.QuoteEnd)
                            {
                                EmitFileToken(line, startTokenIndex, pos, lineNumber, fullPath, referenceSource, depth);
                                if (Failed)
                                {
                                    return;
                                }

                                startTokenIndex = pos;
                                seekingQuote = Boundary.QuoteStart;
                                openQuoteIndex = -1;
                            }
                            else
                            {
                                startTokenIndex = pos + 1;
                                seekingQuote = Boundary.QuoteEnd;
                                openQuoteIndex = pos;
                            }
                        }
                        else
                        {
                            if (seekingQuote == Boundary.QuoteEnd)
                            {
                                seekingQuote = Boundary.QuoteStart;
                                openQuoteIndex = -1;
                            }
                            else
                            {
                                seekingQuote = Boundary.QuoteEnd;
                                openQuoteIndex = pos;
                            }
                        }
                    }
                    else if (pos < line.Length && seeking == Boundary.TokenStart && seekingQuote == Boundary.QuoteStart)
                    {
                        seeking = Boundary.WordEnd;
                        startTokenIndex = pos;
                    }

                    if (pos == line.Length)
                    {
                        if (seekingQuote == Boundary.QuoteEnd)
                        {
                            AddError(
                                $"Unterminated quote in response file '{fullPath}' at line {lineNumber}, column {openQuoteIndex + 1}.",
                                new TokenSource(fullPath, lineNumber, openQuoteIndex + 1, referenceSource));
                            return;
                        }

                        if (seeking != Boundary.TokenStart)
                        {
                            EmitFileToken(line, startTokenIndex, pos, lineNumber, fullPath, referenceSource, depth);
                        }
                    }
                }
            }

            private void EmitFileToken(string line, int startIndex, int endIndex, int lineNumber, string fullPath, TokenSource referenceSource, int depth)
            {
                string value = line.Substring(startIndex, endIndex - startIndex).Replace("\"", "");

                _totalTokens++;
                if (_totalTokens > _expander._options.MaxTotalTokens)
                {
                    AddError(
                        $"Response file '{fullPath}' exceeds the maximum total token count of {_expander._options.MaxTotalTokens}.",
                        new TokenSource(fullPath, lineNumber, startIndex + 1, referenceSource));
                    return;
                }

                ProcessArgument(value, new TokenSource(fullPath, lineNumber, startIndex + 1, referenceSource), depth);
            }

            private void AddError(string message, TokenSource source) =>
                _errors.Add(new ParseError(message, source: source));

            private enum Boundary
            {
                TokenStart,
                WordEnd,
                QuoteStart,
                QuoteEnd
            }
        }

        private sealed class FileSystemResponseFileReader : IResponseFileReader
        {
            public byte[] ReadAllBytes(string path) => File.ReadAllBytes(path);
        }
    }
}
