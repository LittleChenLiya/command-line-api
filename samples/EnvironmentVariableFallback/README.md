# Environment variable fallback sample

This sample prints the final value of each option together with its safe
source (command line, environment variable, or default value). Sensitive
values are masked.

Run it (well under 90 seconds):

```console
> dotnet run --project samples/EnvironmentVariableFallback
--port: 80 (source: default value)
--tags: (null) (source: not provided)
--secret: (null) (source: not provided)

> $env:PORT=8080; dotnet run --project samples/EnvironmentVariableFallback -- --port 9090
--port: 9090 (source: command line)
...
```
