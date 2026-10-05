# Structured invocation binding

`System.CommandLine.StructuredBinding.StructuredCommandLineBinder` turns a *structured*
description of an invocation — a command path, option key/value entries, and positional
arguments — into the token list that the equivalent command line would have produced, and
parses it with the regular parser. The command's handler is **never** executed by the binder;
a `ParseResult` and a token-to-source mapping are returned only when the generated tokens pass
parse validation.

## Input model

```csharp
var input = new StructuredCommandLine();
input.CommandPath.Add("test");                                   // subcommand names or aliases, in order
input.Options.Add(new StructuredOption("--filter", "a b"));      // one entry per option
input.Options.Add(new StructuredOption("--logger", "console;verbosity=detailed", "trx"));
input.Options.Add(new StructuredOption("--list-tests"));         // no values => switch
input.Arguments.Add("src/App.Tests.csproj");                     // positional arguments, in order

StructuredBindingResult result = StructuredCommandLineBinder.Bind(rootCommand, input);
if (result.Success)
{
    ParseResult parseResult = result.ParseResult!;
    // result.Tokens / result.TokenSources map every generated token back to its input element
}
```

### Multi-value input format

Each option appears **at most once**, as a single `StructuredOption` entry whose `Values`
collection holds the values in order:

* **Switch (zero-arity or boolean flag):** an entry with no values, emitted as the bare alias
  (`--verbose`). A boolean option may also be given one explicit value (`--quiet=false`).
* **Single-value option:** one value, emitted as one `--alias=value` token.
* **Multi-value option:** several values, emitted in the repeated-alias form
  (`--logger=a --logger=b`), which is valid for any option whose arity allows more than one
  value and does not depend on `AllowMultipleArgumentsPerToken`.
* **Empty strings** are preserved: an empty value is emitted as a standalone empty token
  following the alias token, because `--alias=` would carry no value token at all.

The number of values is checked against the option's real `Arity` before any token is parsed.

### Verbatim values (no shell involved)

The generated tokens are handed to `Parse(IReadOnlyList<string>)` directly, so no shell
splitting or quoting rules apply:

* Values containing **spaces** or **quotes** stay inside a single `--alias=value` token.
* Values **starting with a dash** (including values identical to another option's alias, e.g.
  `--name` = `--mode`) cannot be mistaken for options, because everything after the first `=`
  is treated as the value.
* **Positional arguments** are emitted after a `--` separator, so argument values such as
  `-x` or `--other` are still bound as arguments.

### Command path and global options

`CommandPath` segments are matched against subcommand names and aliases, descending from the
root command. Options are resolved against the *resolved* (innermost) command: its own options
plus the `Recursive` (global) options of its ancestor commands remain available, mirroring the
parser's known-token set.

## Binding error paths

Binding fails — without running any handler — in two stages, reported through
`StructuredBindingResult.Errors`:

| Stage | `StructuredBindingErrorKind` | Cause |
|-------|------------------------------|-------|
| Binding | `UnknownCommand` | A `CommandPath` segment is not a subcommand of the current command. |
| Binding | `UnknownOption` | An option key matches no option visible from the resolved command. |
| Binding | `DuplicateOption` | The same option was specified twice, e.g. through two different aliases (`--name` and `-n`). Rejected *before* parsing. |
| Binding | `InvalidValueCount` | The value count violates the option's arity (too few, too many, values for a zero-arity option, or a `null` value). |
| Parse | `ParseFailure` | The generated tokens were rejected by the parser, e.g. a `Required` option or a required positional argument is missing, or a value fails conversion. The parser's error messages are preserved. |

On any failure, `Success` is `false`, `ParseResult` is `null`, `Tokens`/`TokenSources` are
empty, and the handler invocation count stays at zero. On success, `Errors` is empty and
`ParseResult`, `Tokens`, and `TokenSources` describe the parsed invocation.
