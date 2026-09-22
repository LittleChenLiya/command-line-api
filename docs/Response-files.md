# Response file expansion with token source tracking

System.CommandLine can expand `@file` arguments into the tokens contained in
response files, while tracking exactly where every token came from.

## Quick demo

From the repository root, run:

```
dotnet run --project samples/ResponseFileDemo
```

Within seconds it parses `@root.rsp` (which includes `sub/child.rsp`, which in
turn includes `../common.rsp`) and prints:

- the source (file, line, column, and full include chain) of every
  successfully parsed token, including an argument containing spaces, and
- a parse error for an invalid integer value, located back to `common.rsp`
  with its complete include chain.

## Enabling expansion

Set `ParserConfiguration.ResponseFileExpansion` to opt in (the legacy
`ResponseFileTokenReplacer` behavior remains the default and is unchanged):

```csharp
var configuration = new ParserConfiguration
{
    ResponseFileExpansion = new ResponseFileExpansionOptions
    {
        BaseDirectory = "/path/to/project"   // command-line @paths resolve here
    }
};

var result = CommandLineParser.Parse(command, args, configuration);
```

Rules:

- `@file` tokens on the command line are resolved relative to
  `ResponseFileExpansionOptions.BaseDirectory` (or the current directory).
- `@file` tokens nested inside a response file are resolved relative to the
  file that contains them.
- `@@name` is treated as the literal token `@name`.
- Tokens after `--` are never expanded.
- Quoting follows the same rules as `CommandLineParser.SplitCommandLine`.
- Response files are read as UTF-8, with or without a BOM. `#` starts a
  comment line.
- Circular references are detected using normalized full file paths, and
  `MaxDepth`, `MaxTotalBytes`, and `MaxTotalTokens` limit the expansion
  process as a whole.
- Any expansion failure is reported as a parse error, so the command action
  is not invoked.

## Token sources

Every `Token` produced while expansion is enabled carries a `Token.Source`
describing the file, 1-based line and column, and the include chain of nested
`@file` references that led to it. `ParseError.Source` exposes the same
information for unrecognized options, missing arguments, and type conversion
failures:

```csharp
foreach (var token in result.Tokens)
    Console.WriteLine($"'{token.Value}' at {token.Source}");

foreach (var error in result.Errors)
    Console.WriteLine($"{error.Message} (at {error.Source})");
```

`TokenSource.IncludeChain` lists the include locations from the outermost
response file down to the token itself.

## Testing without a file system

`ResponseFileExpansionOptions.FileReader` accepts any `IResponseFileReader`,
so tests can expand response files held in memory. Expansion can also be run
standalone via `ResponseFileExpander.Expand(args, options)`, which returns a
`ResponseFileExpansionResult` containing either the expanded arguments (with
sources) or the errors.
