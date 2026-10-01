# Environment variable fallback for options

Options can opt in to reading a fallback value from an environment variable when the
option is not specified on the command line.

## Value precedence

For an option bound to an environment variable, the value is resolved in this order:

1. An explicit command line value (highest priority).
2. The environment variable value.
3. The option's default value (`DefaultValueFactory`), if one is defined.

An environment variable that is set to an empty string counts as an explicitly
provided (empty) value, not as a missing value. Only a variable that is not set at
all falls through to the default value.

    Option<int> portOption = new("--port")
    {
        EnvironmentVariableName = "PORT",
        DefaultValueFactory = _ => 80
    };

With `PORT=8080` in the environment, `--port 9090` parses to `9090`, no command line
value parses to `8080`, and with `PORT` unset the value is `80`.

## Multiple values, delimiters, and escaping

`EnvironmentVariableParsingStrategy` controls how the environment variable value is
turned into option values:

- `Auto` (default): options that accept multiple values use `Delimited`; all other
  options use `SingleValue`.
- `SingleValue`: the whole value is a single option value, delimiters included.
- `Delimited`: the value is split on `EnvironmentVariableValueDelimiter`
  (default `;`). `EnvironmentVariableEscapeCharacter` (default `\`) escapes the
  delimiter or itself when it immediately precedes one of them; anywhere else it is
  treated literally (so Windows paths such as `C:\temp` keep their backslashes).

For example, with the defaults, the value `a;b\;c;d\\e` produces the three values
`a`, `b;c`, and `d\e`.

## Conversion and errors

Environment variable values are converted with the same argument converter (built-in
or `CustomParser`) and validators as command line values. A conversion failure is
reported as a parse error that names the environment variable as the source, for
example:

    Cannot parse value 'abc' of environment variable 'PORT' for option '--port' as expected type 'System.Int32'.

Options marked with `IsSensitive = true` never have their values echoed in parse
errors or parse diagrams; diagrams show `<***>` instead.

## Read behavior

- Each `Parse` call takes a single snapshot of the environment: a variable is read
  at most once per parse, and consecutive parses observe consecutive environment
  states.
- Help and completion actions do not trigger environment reads.
- Options that do not set `EnvironmentVariableName` are completely unaffected.

## Inspecting the value source

`OptionResult.ValueSource` reports where a value came from
(`CommandLine`, `EnvironmentVariable`, `DefaultValue`, or `NotProvided`), which
makes it safe to log the source without risking leaking a sensitive value:

    command.SetAction(parseResult =>
    {
        OptionResult result = parseResult.GetResult(portOption)!;
        Console.WriteLine($"port={parseResult.GetValue(portOption)} source={result.ValueSource}");
    });
