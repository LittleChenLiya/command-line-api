# Asynchronous validators

Some checks cannot be performed synchronously during parsing, for example verifying that a
user name is available in a remote service or that a file share is reachable. For these cases
commands expose `Command.AsyncValidators`, a set of explicit asynchronous validators that run
after parsing and the synchronous `Command.Validators` have completed **without errors**, and
before the command's action is invoked.

## Registering an async validator

An `AsyncValidator` has a name and a delegate that receives the `ParseResult` and a
`CancellationToken`, and returns a `ValidationDiagnostic` when the check fails
(or `null` when it succeeds):

```csharp
using System.CommandLine;
using System.CommandLine.Invocation;

var nameOption = new Option<string>("--name");

var command = new RootCommand
{
    nameOption
};

command.AsyncValidators.Add(new AsyncValidator("name-availability", async (parseResult, cancellationToken) =>
{
    var name = parseResult.GetValue(nameOption);

    if (await UserService.IsTakenAsync(name, cancellationToken))
    {
        return new ValidationDiagnostic($"The name '{name}' is already taken.");
    }

    return null; // null means the check passed
}));

command.SetAction((parseResult, cancellationToken) =>
{
    Console.WriteLine($"Creating user {parseResult.GetValue(nameOption)}...");
    return Task.FromResult(0);
});

// Async validators only run on the asynchronous invocation path:
return await command.Parse(args).InvokeAsync();
```

Behavior guarantees:

- Async validators run only when parsing and synchronous validation produced no errors.
- Diagnostics are reported in registration order (not completion order) as parse errors,
  and any diagnostic prevents the action from running; the exit code is 1.
- Validators run concurrently; the concurrency limit can be configured via
  `InvocationConfiguration.MaxAsyncValidatorConcurrency` (defaults to `Environment.ProcessorCount`).
- A validator that throws an exception produces a failure diagnostic that carries the
  validator's name.
- When cancellation is requested, validators that have not started yet are never started
  (so they cannot produce side effects), validators that are already running are awaited so
  they can finish their cleanup, and the cancellation is then propagated instead of invoking
  the action.

## Synchronous invocation is not supported

`ParseResult.Invoke` is synchronous and cannot await asynchronous validators without blocking
on them (for example via `Task.Result`), which risks deadlocks. Instead of hiding such a
blocking call, `Invoke` throws an `InvalidOperationException` when any async validator is
registered, telling you to use the supported path:

```csharp
// Throws InvalidOperationException: use InvokeAsync instead.
command.Parse(args).Invoke();

// Supported: the validators are awaited asynchronously.
return await command.Parse(args).InvokeAsync(cancellationToken: cancellationToken);
```
