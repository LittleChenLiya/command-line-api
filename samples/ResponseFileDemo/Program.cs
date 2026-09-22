// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;

// This demo builds the following response file layout in a temporary directory:
//
//   <tmp>/root.rsp        contains: --name "hello world" and @sub/child.rsp
//   <tmp>/sub/child.rsp   contains: @../common.rsp   (resolved relative to child.rsp)
//   <tmp>/common.rsp      contains: --number <value>
//
// It then parses "@root.rsp" twice: once with a valid integer (printing the
// source of every token) and once with an invalid integer (printing the parse
// error together with its full include chain).

var directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "scl-response-file-demo"));

try
{
    File.WriteAllText(Path.Combine(directory.FullName, "root.rsp"),
        "--name \"hello world\"" + Environment.NewLine + "@sub/child.rsp" + Environment.NewLine);
    Directory.CreateDirectory(Path.Combine(directory.FullName, "sub"));
    File.WriteAllText(Path.Combine(directory.FullName, "sub", "child.rsp"),
        "@../common.rsp" + Environment.NewLine);

    var commonPath = Path.Combine(directory.FullName, "common.rsp");

    var nameOption = new Option<string>("--name");
    var numberOption = new Option<int>("--number");
    var command = new RootCommand { nameOption, numberOption };

    var configuration = new ParserConfiguration
    {
        ResponseFileExpansion = new ResponseFileExpansionOptions
        {
            BaseDirectory = directory.FullName
        }
    };

    // 1. Successful parse: show where every token came from.
    File.WriteAllText(commonPath, "--number 42" + Environment.NewLine);

    var success = CommandLineParser.Parse(command, ["@root.rsp"], configuration);

    Console.WriteLine("== Successful parse of @root.rsp ==");
    Console.WriteLine($"--name   = {success.GetValue(nameOption)}");
    Console.WriteLine($"--number = {success.GetValue(numberOption)}");
    Console.WriteLine();
    Console.WriteLine("Token sources:");

    foreach (var token in success.Tokens)
    {
        Console.WriteLine($"  '{token.Value}'");
        Console.WriteLine($"    at {token.Source}");
        Console.WriteLine($"    include chain: {FormatChain(token.Source)}");
    }

    // 2. Failing parse: the integer value is invalid, show the error's include chain.
    File.WriteAllText(commonPath, "--number notanumber" + Environment.NewLine);

    var failure = CommandLineParser.Parse(command, ["@root.rsp"], configuration);

    Console.WriteLine();
    Console.WriteLine("== Failing parse of @root.rsp (--number notanumber) ==");

    foreach (var error in failure.Errors)
    {
        Console.WriteLine($"error: {error.Message}");
        Console.WriteLine($"  at {error.Source}");
        Console.WriteLine($"  include chain: {FormatChain(error.Source)}");
    }

    return 0;

    static string FormatChain(TokenSource? source) =>
        source is null
            ? "<unknown>"
            : string.Join(" -> ", source.IncludeChain.Select(s => s.ToString()));
}
finally
{
    directory.Delete(recursive: true);
}
