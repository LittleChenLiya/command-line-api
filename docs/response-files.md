# Response files with provenance tracking

`System.CommandLine` can expand `@file` response files before parsing. The opt-in
`ResponseFileExpander` replaces the legacy token replacer and additionally tracks, for
every token, where it came from: the source file, line, column, and the full chain of
`@file` includes that produced it.

Rules:

- `@file` is replaced by the tokens read from the response file.
- `@@name` is treated as the literal token `@name`.
- Tokens after `--` are never expanded.
- Relative paths inside a response file resolve against the directory of the containing
  file; relative paths on the command line resolve against
  `ResponseFileExpansionOptions.BaseDirectory` (or the current directory).
- Quoting follows the same rules as `CommandLineParser.SplitCommandLine`.
- Files are read as UTF-8 (a byte order mark is allowed). Invalid UTF-8 and unterminated
  quotes are reported with the file, line, and column where they occurred.
- Circular includes are detected using normalized full paths, and the maximum include
  depth, total byte count, and total token count are limited across the whole expansion
  (`ResponseFileExpansionOptions.MaxDepth`, `MaxTotalBytes`, `MaxTotalTokens`).
- When expansion fails, the parse result contains errors and the command action is not
  invoked.

## 90-second quickstart

Given `root.rsp` (which includes `sub/child.rsp`, which includes `../common.rsp`):

```text
--name "Jane Doe"
@sub/child.rsp
```

wire up the expander and print where every token came from:

```csharp
using System.CommandLine;
using System.CommandLine.Parsing;

var nameOption = new Option<string>("--name");
var countOption = new Option<int>("--count");
var rootCommand = new RootCommand { nameOption, countOption };

var configuration = new ParserConfiguration
{
    ResponseFileExpander = new ResponseFileExpander(
        new ResponseFileExpansionOptions { BaseDirectory = Directory.GetCurrentDirectory() })
};

ParseResult result = rootCommand.Parse(args, configuration);

// one command shows the source of every successfully parsed token...
foreach (Token token in result.Tokens)
{
    Console.WriteLine($"{token.Value,-20} {token.Source?.FormatIncludeChain()}");
}

// ...and every parse error (unknown option, missing argument, conversion failure)
// carries the file, line, column, and include chain of the offending token
foreach (ParseError error in result.Errors)
{
    Console.WriteLine($"error: {error.Message}");
}

return result.Invoke();
```

Sample output for a successful parse:

```text
--name               <command line> -> C:\app\root.rsp(1,1)
Jane Doe             <command line> -> C:\app\root.rsp(1,8)
--count              <command line> -> C:\app\root.rsp(2,1) -> C:\app\sub\child.rsp(3,1) -> C:\app\common.rsp(1,1)
```

And when `common.rsp` contains `--count abc`, the conversion error shows the whole chain:

```text
error: Cannot parse argument 'abc' for option '--count' as expected type 'System.Int32'. [<command line> -> C:\app\root.rsp(2,1) -> C:\app\sub\child.rsp(3,1) -> C:\app\common.rsp(1,9)]
```

## Testing with an in-memory file system

`ResponseFileExpansionOptions.FileReader` accepts any `IResponseFileReader`, so tests can
expand response files without touching disk:

```csharp
var options = new ResponseFileExpansionOptions
{
    BaseDirectory = @"C:\app",
    FileReader = new MyInMemoryResponseFileReader()
};
```

