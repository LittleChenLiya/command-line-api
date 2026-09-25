# Environment variable fallback for options

Options can opt in to falling back to an environment variable when they are not
specified on the command line. The feature is configured per option through
`Option.EnvironmentVariableBinding`; options without a binding are never
affected by environment variables.

## Precedence

For an option with an `EnvironmentVariableBinding`, the effective value is
resolved in the following order:

1. An explicit command line value, if the option was specified.
2. The environment variable value, if the variable is set (including when it is
   set to an empty string - an explicitly empty value is not treated as unset).
3. The option's existing default value, if one is defined.

A bound environment variable also satisfies a `Required` option.

## Defining a binding

```csharp
Option<int> portOption = new("--port")
{
    DefaultValueFactory = _ => 80,
    EnvironmentVariableBinding = new EnvironmentVariableBinding("PORT")
};
```

With `PORT=8080` in the environment:

- `myapp --port 9090` uses `9090` (command line wins).
- `myapp` uses `8080` (environment wins over the default of `80`).
- Without `PORT` set, `myapp` uses `80`.

## Parsing strategy for collections

Options whose arity allows more than one value (for example `Option<string[]>`)
split the environment variable value into multiple arguments using a delimiter
and an escape character, both configurable on the binding:

| Property | Default | Purpose |
|----------|---------|---------|
| `Delimiter` | `;` | Separates individual values. |
| `EscapeCharacter` | `\` | Escapes the next character so it is treated literally. |

Rules:

- An unescaped delimiter separates values. Empty segments between delimiters
  are kept as empty values.
- The escape character escapes any following character, including the delimiter
  and itself: `a\;b` produces the single value `a;b`, and `a\\b`
  produces `a\b`. A trailing escape character is kept literally.
- Options that accept a single value always receive the variable value
  verbatim; no splitting or unescaping is applied.
- A variable set to an empty string provides an empty collection (for
  multi-value options) or an empty string (for single-value options); the
  default value is not used.

## Conversion and errors

Environment-provided values are converted with the same argument converter
(including `CustomParser`) that is used for command line values. A value that
cannot be converted produces a parse error that identifies the environment
variable as the source, for example:

```
Cannot parse the value 'abc' provided by environment variable 'PORT' for option '--port' as expected type 'System.Int32'.
```

The `OptionResult.EnvironmentVariableSource` property exposes the binding that
supplied the value, so handlers can report a safe source (the variable name)
without inspecting the value.

## Sensitive values

Set `EnvironmentVariableBinding.IsSensitive` to `true` for secrets. Sensitive
values are never echoed:

- Conversion error messages name the variable and the option but omit the value.
- The `[diagram]` directive masks the value as `<***>`.

## Environment read snapshot

Each parse operation reads every bound variable at most once and caches the
value for the remainder of that parse, so a single parse always observes a
consistent snapshot. Consecutive parses read the environment again and observe
changes made between them. Computing help (`--help`) or completions
(`[suggest]`) never reads the environment.

The reader can be replaced (for example in tests) through
`ParserConfiguration.EnvironmentVariableReader`.

## Sample

A runnable sample that prints the final value and its safe source is available
under [samples/EnvironmentVariableFallback](../samples/EnvironmentVariableFallback):

```console
> dotnet run --project samples/EnvironmentVariableFallback
port: 80 (source: default value)
```
