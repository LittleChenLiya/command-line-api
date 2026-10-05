# Command-Line Representation Demo

This sample demonstrates the reversible command-line representation protocol
provided by `CommandLineRepresentation`.

## Run

```bash
dotnet run --project CommandLineRepresentationDemo.csproj -- <token1> [token2] ...
```

The demo encodes the given tokens into a display string, decodes it back, and
prints each token with its UTF-16 range in the display text.

## Examples

```bash
# Simple tokens
dotnet run --project CommandLineRepresentationDemo.csproj -- hello world

# Token with a space (wrapped in quotes)
dotnet run --project CommandLineRepresentationDemo.csproj -- "a b"

# Empty string
dotnet run --project CommandLineRepresentationDemo.csproj -- ""

# Backslash and double quote
dotnet run --project CommandLineRepresentationDemo.csproj -- "a\\b" "a\"b"

# Newline
dotnet run --project CommandLineRepresentationDemo.csproj -- "line1\nline2"
```

## Protocol

See [docs/command-line-representation-protocol.md](../docs/command-line-representation-protocol.md)
for the full protocol specification.

