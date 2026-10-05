# Command-Line Representation Protocol

The `CommandLineRepresentation` module provides a reversible, shell-agnostic way to
encode a token array into a display string and read it back. Each decoded token is
annotated with its source range (start and end offsets in UTF-16 code units) in the
display text.

The protocol does **not** invoke any operating system shell or system command-line
parser. It is a pure string encoding/decoding format.

## Syntax

The protocol uses a fixed syntax:

- Tokens are separated by whitespace.
- A token that contains special characters is wrapped in double quotes (`"`).
- Special characters inside quoted tokens are introduced with a backslash (`\`).
- An empty token is represented as `""`.

### Escape Sequences

The following escape sequences are supported:

| Sequence | Meaning              |
|----------|----------------------|
| `\\`     | Literal backslash    |
| `\"`     | Literal double quote |
| `\n`     | Line feed (U+000A)   |
| `\r`     | Carriage return      |
| `\t`     | Tab                  |
| `\0`     | Null character       |
| `\uXXXX` | Unicode character    |

Any other escape sequence (e.g., `\x`) is illegal and reports an error at the
position of the backslash.

## Examples

### Basic encoding

```
Token array:  ["hello", "world"]
Display text: hello world
```

### Token with spaces

```
Token array:  ["a b"]
Display text: "a b"
```

### Empty string

```
Token array:  [""]
Display text: ""
```

### Backslash and double quote

```
Token array:  ["a\\b", "a\"b"]
Display text: "a\\b" "a\"b"
```

### Newline

```
Token array:  ["line1\nline2"]
Display text: "line1\nline2"
```

### Mixed array

```
Token array:  ["", "a b", "a\\b", "a\"b"]
Display text: "" "a b" "a\\b" "a\"b"
```

## Source Ranges

Each decoded token has a `Start` and `End` offset (in UTF-16 code units) into the
display text. The range includes the opening and closing quotes (if present).

For the display text `"" "a b" "a\\b" "a\"b"`:

| Token   | Start | End | Length | Value   |
|---------|-------|-----|--------|---------|
| 0       | 0     | 2   | 2      | (empty) |
| 1       | 3     | 8   | 5      | `a b`   |
| 2       | 9     | 15  | 6      | `a\b`   |
| 3       | 16    | 22  | 6      | `a"b`   |

## Error Reporting

When decoding fails, `TryDecode` returns `false` and provides a `RepresentationError`
with:

- `Position`: the UTF-16 offset where the error occurred.
- `Message`: a human-readable description.
- `Kind`: the error type (`IllegalEscape`, `UnclosedQuote`, or `InvalidUnicodeEscape`).

On error, no partial tokens are returned (the token list is empty).

### Unclosed quote

```
Display text: "abc
Error:        Unclosed quote at position 0
```

The error position points to the opening quote of the unclosed token.

### Illegal escape

```
Display text: "a\xb"
Error:        Illegal escape at position 2
```

The error position points to the backslash of the illegal escape.

## Parse Linkage

The decoded token values can be passed directly to `CommandLineParser.Parse`:

```csharp
var command = new RootCommand();
var option = new Option<string>("--name");
command.Add(option);

var tokens = new[] { "--name", "a b" };
var encoded = CommandLineRepresentation.Encode(tokens);
var decoded = CommandLineRepresentation.Decode(encoded);

var parseResult = command.Parse(decoded.Select(t => t.Value).ToArray());
// parseResult.GetValue(option) == "a b"
```

## Running the Demo

The demo app encodes the given command-line arguments into a display string, decodes
it back, and prints each token with its UTF-16 range:

```bash
dotnet run --project samples/CommandLineRepresentationDemo.csproj -- hello "a b" "a\\b" "a\"b"
```

Example output:

```
Encoded display text:
  hello "a b" "a\\b" "a\"b"

Decoded 4 token(s):
  #   Start  End  Length Value
  -   -----  ---  ------ -----
  0   0      5    5      hello
  1   6      11   5      a b
  2   12     18   6      a\b
  3   19     25   6      a"b

Round-trip verification:
  [0] OK  expected="hello"  actual="hello"
  [1] OK  expected="a b"  actual="a b"
  [2] OK  expected="a\b"  actual="a\b"
  [3] OK  expected="a"b"  actual="a"b"
```

