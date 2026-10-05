# Shell-free argument representation (`CommandLineText`)

`System.CommandLine.Parsing.CommandLineText` represents a list of argument tokens as a single
string that can be displayed, stored, or transmitted and then read back **without invoking a shell
and without any system command line parser**. The read-back side reports the range of every token,
so the representation also works as a lightweight protocol between processes.

## Protocol grammar

```
text    := *(ws token) *ws
token   := '"' *item '"'
item    := escape | literal
escape  := '\' ('\' | '"' | 'n' | 'r' | 't' | '0')
literal := <any char except '"' and '\'>
ws      := <any char for which char.IsWhiteSpace is true>
```

* Tokens are separated by whitespace; every token is wrapped in double quotes.
* Inside the quotes the backslash is the only escape character:
  `\\` (backslash), `\"` (double quote), `\n` (line feed), `\r` (carriage return),
  `\t` (tab), `\0` (null).
* Because every token is quoted, empty strings and tokens containing spaces or newlines
  round-trip exactly. The formatted text itself never contains a raw newline.
* Token ranges (`CommandLineTextToken.Start` / `Length`) are **UTF-16 code unit** offsets into the
  formatted text (ordinary .NET `string` indices) and cover the token including its quotes.
  A supplementary character such as an emoji therefore counts as two units.
* Errors are reported before any token is returned: an unterminated quote points at the position
  of the *opening* quote, an invalid escape points at the backslash, and a bare word between
  tokens points at its first character. On error `TryParse` returns `false` and an empty token
  list — never partial results.

## Examples

| Token list | Formatted text |
| --- | --- |
| `["a", "b c"]` | `"a" "b c"` |
| `[""]` | `""` |
| `["a\"b", "c:\d"]` | `"a\"b" "c:\\d"` |
| `["line1\nline2"]` | `"line1\nline2"` (backslash + `n`, single line) |
| `["🙂"]` | `"🙂"` (range length 4: quote + 2 UTF-16 units + quote) |
| `[]` | *(empty string)* |

Error examples:

| Input | Error | Position |
| --- | --- | --- |
| `"ok" "unterminated` | `UnterminatedQuote` | `5` (the opening quote) |
| `"a\qb"` | `InvalidEscape` | `2` (the backslash) |
| `"a" bare` | `UnexpectedCharacter` | `4` |

## Round-tripping into the parser

The decoded values are ordinary argument strings and can be handed to the original parser
(`CommandLineParser.Parse(command, args)` / `command.Parse(args)`); no system command line
parser is involved at any point:

```csharp
string[] original = ["", "a b", @"back\slash", "quo\"te", "new\nline"];

// encode
var text = CommandLineText.Format(original);

// decode (with ranges)
if (!CommandLineText.TryParse(text, out var tokens, out var error))
{
    Console.Error.WriteLine($"{error.Kind} at {error.Position}: {error.Message}");
    return;
}

var args = tokens.Select(t => t.Value).ToArray(); // same values as `original`
var parseResult = CommandLineParser.Parse(command, args);
```

## Local verification commands

From the repository root (with a .NET SDK available):

```console
dotnet test src/System.CommandLine.Tests/System.CommandLine.Tests.csproj --filter "FullyQualifiedName~CommandLineTextTests"
dotnet test src/System.CommandLine.ApiCompatibility.Tests/System.CommandLine.ApiCompatibility.Tests.csproj
```
