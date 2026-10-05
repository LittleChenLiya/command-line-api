// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Parsing;

// This demo shows how to use the CommandLineRepresentation protocol to encode a token
// array into a display string and read it back, preserving each token's source range.
//
// Run with:
//   dotnet run --project samples/CommandLineRepresentationDemo.csproj -- <token1> <token2> ...

if (args.Length == 0)
{
    Console.WriteLine("Usage: CommandLineRepresentationDemo <token1> [token2] ...");
    Console.WriteLine();
    Console.WriteLine("Encodes the given tokens into a display string, then decodes it back");
    Console.WriteLine("and prints each token with its UTF-16 range in the display text.");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet run --project samples/CommandLineRepresentationDemo.csproj -- hello \"a b\" \"a\\\\b\" \"a\\\"b\"");
    Console.WriteLine("  dotnet run --project samples/CommandLineRepresentationDemo.csproj -- \"\" \"line1\\nline2\"");
    return 1;
}

// Encode the token array into a display string.
string encoded = CommandLineRepresentation.Encode(args);

Console.WriteLine($"Encoded display text:");
Console.WriteLine($"  {encoded}");
Console.WriteLine();

// Decode the display string back into tokens with their source ranges.
if (!CommandLineRepresentation.TryDecode(encoded, out var tokens, out var error))
{
    Console.WriteLine($"Decode error at position {error!.Position}: {error.Message}");
    return 1;
}

Console.WriteLine($"Decoded {tokens.Count} token(s):");
Console.WriteLine($"  {"#",-3} {"Start",-6} {"End",-6} {"Length",-7} Value");
Console.WriteLine($"  {"-",-3} {"-----",-6} {"---",-6} {"------",-7} -----");

for (int i = 0; i < tokens.Count; i++)
{
    var t = tokens[i];
    string display = t.Value
        .Replace("\\", "\\\\")
        .Replace("\"", "\\\"")
        .Replace("\n", "\\n")
        .Replace("\r", "\\r")
        .Replace("\t", "\\t")
        .Replace("\0", "\\0");
    Console.WriteLine($"  {i,-3} {t.Start,-6} {t.End,-6} {t.Length,-7} {display}");
}

Console.WriteLine();
Console.WriteLine("Round-trip verification:");
for (int i = 0; i < args.Length; i++)
{
    bool match = args[i] == tokens[i].Value;
    Console.WriteLine($"  [{i}] {(match ? "OK" : "MISMATCH")}  expected=\"{args[i]}\"  actual=\"{tokens[i].Value}\"");
}

return 0;
