# Structured Call Binding

The `System.CommandLine.StructuredBinding` namespace provides a structured call binder that turns
a canonical command path, a set of option key/value pairs, and a positional argument array into a
token list suitable for the real `Parse` method — **without executing any handler**.

## Overview

The binder inspects the real `Option` symbols in the command tree to honor their arity and alias
rules. It validates everything it can before handing the token list to the real parser, and only
returns the `ParseResult` and a token-source mapping when the final parse validation passes.

```csharp
using System.CommandLine.StructuredBinding;

var root = new Command("dotnet")
{
    new Command("test")
    {
        new Option<string[]>("--name", "-n"),
        new Option<bool>("--no-build"),
        new Argument<string[]>("args")
    }
};

var call = new StructuredCall
{
    CommandPath = new[] { "dotnet", "test" },
    Options = new Dictionary<string, StructuredOptionValue>
    {
        ["--name"] = new StructuredOptionValue
        {
            Values = new[] { "two words", "", "third" }
        },
        ["--no-build"] = new StructuredOptionValue { Values = Array.Empty<string>() }
    },
    Arguments = new[] { "positional-one" }
};

var result = StructuredCallBinder.Bind(root, call);

if (result.IsSuccess)
{
    // result.ParseResult  — the real ParseResult
    // result.Mapping      — token → structured input source mapping
}
else
{
    // result.Errors      — binding or parse error messages
}
```

## Multi-value input format

Multi-valued options are expressed by supplying more than one item in
`StructuredOptionValue.Values`. Each item is preserved item-by-item; values are never split by a
shell.

| Input | Representation |
|-------|----------------|
| Single value | `Values = new[] { "value" }` or implicit `["--name"] = "value"` |
| Multiple values | `Values = new[] { "one", "two", "three" }` |
| Boolean switch | `Values = Array.Empty<string>()` |
| Empty string | `Values = new[] { "" }` (preserved as a separate empty token) |
| Value with spaces | `Values = new[] { "two words" }` |
| Value with quotes | `Values = new[] { "say \"hello\"" }` |
| Value starting with a dash | `Values = new[] { "-dash-value" }` |

### How values are emitted as tokens

- **Non-empty values** use the `=` separator (`--name=value`). This prevents the value from being
  split by a shell or mistaken for another option, even when the value contains spaces, quotes, or
  starts with a dash.
- **Empty strings** are emitted as a separate empty token after the option token. The tokenizer
  preserves the empty value as an `Argument` token.
- **Boolean switches** (empty value list) emit just the option token.
- **Positional arguments** are emitted after a `--` separator, so values that start with a dash are
  never mistaken for options.

### Implicit conversions

`StructuredOptionValue` supports implicit conversions from `string`, `string[]`, and `List<string>`:

```csharp
Options = new Dictionary<string, StructuredOptionValue>
{
    ["--name"] = "single",                          // string
    ["--framework"] = new[] { "net8.0", "net9.0" }, // string[]
    ["--filter"] = new List<string> { "a", "b" }    // List<string>
};
```

## Binding error paths

The binder validates the structured input in several stages. When any stage fails,
`StructuredCallResult.IsSuccess` is `false`, `ParseResult` and `Mapping` are `null`, and `Errors`
describes the failures. The binder never invokes a handler.

### 1. Command path resolution

The command path is walked against the real command tree. The first element may be the root command
name (it is implicit in the tokenizer and is not emitted as a token).

**Error:** `Command 'nonexistent' is not a valid subcommand of 'dotnet'.`

### 2. Option resolution

Each option key is resolved against the options visible to the resolved command — the command's own
options plus recursive options from parent commands.

**Error:** `Option '--unknown' is not a valid option for command 'test'.`

### 3. Duplicate alias rejection

The same option specified through two different aliases is rejected **before parsing**. Each option
may be specified only once.

**Error:** `Option '-n' refers to the same option as '--name' (--name). Specify each option only once.`

### 4. Duplicate key rejection

The same key specified more than once is rejected.

**Error:** `Option '--name' was specified more than once.`

### 5. Arity validation

The number of values is validated against the real `Option.Arity`.

- Too few values: `Option '--framework' requires at least 1 value(s) but 0 were supplied.`
- Too many values: `Option '--framework' accepts at most 1 value(s) but 2 were supplied.`

### 6. Required option validation

Required options that were not provided are rejected.

**Error:** `Required option '--required' was not provided.`

### 7. Final parse validation

After all binding-stage checks pass, the generated token list is handed to the real `Parse` method.
If the final parse produces errors, the result is a failure with the parse error messages.

## Token-source mapping

When binding succeeds, `Mapping` provides a mapping from each final `Token` to the piece of
structured input that produced it:

| Property | Description |
|----------|-------------|
| `Token` | The final token from the real parser |
| `SourceKind` | `command`, `option`, `optionValue`, `separator`, or `argument` |
| `CommandPathIndex` | Index into `StructuredCall.CommandPath` for command tokens |
| `OptionKey` | The option key (name or alias) as supplied in the input |
| `OptionName` | The canonical name of the resolved `Option` |
| `ValueIndex` | Zero-based index of the value within the option's value list |
| `ArgumentIndex` | Zero-based index of the positional argument |

The root command name (index 0 when it matches the root) is implicit and is not emitted as a token,
so the first command token maps to `CommandPathIndex` 1 or later.
