# Asynchronous validators

Some checks cannot be performed synchronously while parsing: they may need to call a
network service, probe a database, or hit a file share. `Command.AsyncValidators` holds an
explicit set of named, asynchronous validators that run **after** parsing and the synchronous
`Command.Validators` have completed, and **before** the command's action is invoked.

## Defining an asynchronous validator

An `AsyncValidator` receives the `ParseResult` and a `CancellationToken`, and returns
structured `ValidationDiagnostic` values. Returning `null` (or an empty sequence) means the
check passed.

```csharp
RootCommand rootCommand = new();
Option<string> tenantOption = new("--tenant");
rootCommand.Options.Add(tenantOption);

rootCommand.AsyncValidators.Add(new AsyncValidator("tenant-exists",
    async (parseResult, cancellationToken) =>
    {
        string tenant = parseResult.GetValue(tenantOption)!;

        if (!await TenantDirectory.ExistsAsync(tenant, cancellationToken))
        {
            return new[]
            {
                new ValidationDiagnostic($"Tenant '{tenant}' does not exist.")
            };
        }

        return null; // validation passed
    }));

rootCommand.SetAction((parseResult, cancellationToken) =>
{
    // Only runs when every asynchronous validator passed.
    return Task.FromResult(0);
});

int exitCode = await rootCommand.Parse(args).InvokeAsync(cancellationToken: cancellationToken);
```

Behavior summary:

- Asynchronous validators only run when parsing and synchronous validation produced no errors.
- Any reported diagnostic is added to `ParseResult.Errors`, printed like a parse error, and
  prevents the action (and pre-actions) from running. The exit code is 1.
- Diagnostics are reported in the order the validators were registered, not in the order the
  checks happen to complete.
- A validator that throws an exception is converted into a failure diagnostic that names the
  validator, e.g. `Asynchronous validator 'tenant-exists' failed: ...`.
- When cancellation is requested, no new checks are started, checks that are already running
  are awaited so nothing keeps running in the background, and the cancellation is propagated
  instead of invoking the action.

## Limiting concurrency

Validators run in parallel up to `InvocationConfiguration.MaxAsyncValidatorConcurrency`
(defaults to `Environment.ProcessorCount`):

```csharp
InvocationConfiguration configuration = new()
{
    MaxAsyncValidatorConcurrency = 2
};

int exitCode = await rootCommand.Parse(args).InvokeAsync(configuration, cancellationToken);
```

## Synchronous invocation is not supported

Asynchronous validators require the asynchronous invocation path. Calling the synchronous
`ParseResult.Invoke` on a command that has asynchronous validators throws an
`InvalidOperationException` that tells you to use `InvokeAsync`, instead of blocking on the
checks with something like `Task.Result` (which can deadlock):

```csharp
// Throws InvalidOperationException: use InvokeAsync instead.
// rootCommand.Parse(args).Invoke();

// Supported: await the asynchronous invocation path.
int exitCode = await rootCommand.Parse(args).InvokeAsync();
```
