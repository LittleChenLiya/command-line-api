# Help Visibility Projection API

The `System.CommandLine.Help.HelpVisibility` API provides an explicit visibility
view over a command tree for help generation. It lets you hide commands and
options according to the caller's authorization set **without modifying the
underlying command tree**. The same command tree remains fully parseable and can
be used to generate help for callers with different permissions.

## Overview

By default, help output shows every symbol that is not marked `Hidden`. When
multiple users share the same command tree but have different permissions, you
often want to show different help content to each user. The visibility projection
API solves this by applying a per-generation filter to help output only.

Key properties:

- **Non-mutating**: the command tree (including `Symbol.Hidden`) is never changed,
  so parsing and completion continue to use the original tree.
- **Per-generation**: the view is carried on the `HelpContext` for a single
  `HelpBuilder.Write` call, so concurrent help requests from the same tree can
  use different views without interfering with one another.
- **No leaks**: hidden symbols' aliases, usage lines, and default values are all
  filtered out of the generated help.
- **Preserves help logic**: global option inheritance and argument ordering follow
  the same rules as the built-in help generation.

## Creating a visibility view

A visibility view is a `HelpVisibility` object wrapping a predicate that returns
`true` for symbols that should be visible:

```csharp
using System.CommandLine.Help;

// Show everything except the "secret" option.
var normal = new HelpVisibility(symbol => symbol.Name != "secret");
```

The predicate receives each `Symbol` (command, option, or argument) and returns
`true` when that symbol should appear in help. Symbols that are already
`Hidden` are always hidden regardless of the predicate.

To show all symbols (subject to `Hidden`), use `HelpVisibility.All`:

```csharp
var all = HelpVisibility.All;
```

## Generating a help view

Pass the visibility view to the `HelpBuilder.Write` overload:

```csharp
var command = new RootCommand
{
    new Option<string>("--public") { Description = "Public option" },
    new Option<string>("--secret") { Description = "Secret option" },
};

var normal = new HelpVisibility(symbol => symbol.Name != "secret");

var helpBuilder = new HelpBuilder();
using var writer = new StringWriter();
helpBuilder.Write(command, writer, normal);

string help = writer.ToString();
// help contains "--public" but not "--secret"
```

Because the view does not mutate the tree, you can generate multiple views from
the same command tree:

```csharp
var admin = new HelpVisibility(_ => true);
var normal = new HelpVisibility(symbol => symbol.Name != "secret");

string adminHelp = WriteHelp(command, admin);
string normalHelp = WriteHelp(command, normal);
// adminHelp contains "--secret"; normalHelp does not
```

## Using with the --help action

When help is requested through the `--help` option, the `HelpAction` generates
the output. You can configure the visibility view on the `HelpAction`:

```csharp
var command = new RootCommand
{
    new Option<string>("--public") { Description = "Public option" },
    new Option<string>("--secret") { Description = "Secret option" },
    new HelpOption
    {
        Action = new HelpAction
        {
            Visibility = new HelpVisibility(symbol => symbol.Name != "secret")
        }
    },
};

command.Parse("-h").Invoke();
// Output contains "--public" but not "--secret"
```

> **Note**: the `Visibility` property is set once when the action is configured.
> For per-request visibility (for example, when the authorization set is derived
> from the current user), create a custom `CommandLineAction` that calls
> `HelpBuilder.Write(command, writer, visibility)` with the appropriate view.

## Default values

Default values are filtered by visibility just like symbols. If a hidden option
or argument has a default value, that value is not shown in help:

```csharp
var command = new RootCommand
{
    new Option<string>("--public")
    {
        DefaultValueFactory = _ => "public-default"
    },
    new Option<string>("--secret")
    {
        DefaultValueFactory = _ => "secret-default"
    },
};

var normal = new HelpVisibility(symbol => symbol.Name != "secret");
// help contains "public-default" but not "secret-default"
```

## Nested subcommands

The visibility view applies recursively through nested subcommands. When you
generate help for a command, its subcommands are filtered by the view, and the
same applies when you generate help for a nested subcommand:

```csharp
var command = new RootCommand
{
    new Command("admin")
    {
        new Command("users") { Description = "Manage users" },
    },
    new Command("public")
    {
        new Command("status") { Description = "Show status" },
    },
};

var normal = new HelpVisibility(symbol => symbol.Name != "admin");

// Root help shows "public" but not "admin".
// Help for the "public" subcommand shows its nested "status" subcommand.
```

## Global option inheritance and argument order

The visibility view preserves the built-in help logic:

- **Global option inheritance**: recursive options from parent commands are shown
  on subcommands that inherit them, provided they are visible. Hidden recursive
  options do not appear.
- **Argument order**: arguments are shown in their declaration order, with hidden
  arguments omitted.

## Concurrency

The visibility view is carried on the `HelpContext` for a single `Write` call.
It is never stored on the command tree or shared across requests. This means:

- Multiple threads can generate help views concurrently from the same command
  tree using different visibility views.
- The `Symbol.Hidden` property is never modified, so parsing and completion are
  unaffected.

## API reference

| Member | Description |
| --- | --- |
| `HelpVisibility(Func<Symbol, bool>)` | Initializes a new visibility view with the specified predicate. |
| `HelpVisibility.All` | Gets a view in which all symbols are visible (subject to `Hidden`). |
| `HelpVisibility.IsVisible(Symbol)` | Determines whether the specified symbol is visible. |
| `HelpBuilder.Write(Command, TextWriter, HelpVisibility?)` | Writes help for the command using the specified view. |
| `HelpAction.Visibility` | Gets or sets the visibility view used when generating help through the action. |
