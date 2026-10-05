# Help visibility projection

`System.CommandLine` can render an explicit *visibility view* of the help output: the same
command tree produces different help text for different callers (for example, administrators
and regular users) based on the caller's authorization set, while the parse object keeps the
original, complete tree.

The projection:

* **Hides symbols per caller** — commands, options, and arguments rejected by the visibility
  predicate are omitted from the generated help.
* **Does not leak hidden symbols** — a hidden symbol contributes no aliases (first column),
  no usage-line fragments (`Usage:` section), and no default values (`[default: ...]`) to
  the output.
* **Preserves the standard layout** — global (`Recursive`) option inheritance from parent
  commands and argument ordering follow the original help logic; only the set of visible
  symbols differs.
* **Is scoped to a single generation pass** — `Symbol.Hidden` is never read-modified, so
  concurrent help requests against the same tree are unaffected, and `Parse` still
  recognizes every symbol, including the ones hidden from the view.

## Projection API

### `HelpProjection.Write` (one-off generation)

```csharp
using System.CommandLine.Help;

var root = BuildCommandTree();

// The authorization set of the current caller.
var granted = GetGrantedSymbolsForCurrentUser();

// Renders help showing only authorized symbols. The tree is not modified.
HelpProjection.Write(root, Console.Out, symbol => granted.Contains(symbol));
```

`HelpProjection.Write(Command, TextWriter, Func<Symbol, bool>, int maxWidth)` renders help
for a single pass. When the predicate is supplied it replaces the default `Hidden` check;
compose it with `!symbol.Hidden` to honor both:

```csharp
HelpProjection.Write(root, Console.Out, symbol => !symbol.Hidden && granted.Contains(symbol));
```

### `HelpAction.SymbolVisibility` (`--help` integration)

To apply a projection whenever help is requested through the `--help` option, set
`HelpAction.SymbolVisibility`:

```csharp
var helpOption = root.Options.OfType<HelpOption>().Single();
helpOption.Action = new HelpAction
{
    SymbolVisibility = symbol => granted.Contains(symbol)
};
```

When `SymbolVisibility` is `null` (the default), help falls back to the standard behavior:
symbols are shown unless `Symbol.Hidden` is `true`.

## What is filtered

| Output element            | Visibility behavior                                                        |
|---------------------------|----------------------------------------------------------------------------|
| Subcommand rows           | Only visible subcommands are listed.                                       |
| Option rows               | Only visible options are listed, including inherited `Recursive` options.  |
| Argument rows             | Only visible arguments are listed.                                         |
| Aliases / usage labels    | Hidden symbols contribute no aliases or parameter labels.                  |
| `Usage:` line             | Hidden arguments, options, and subcommands are omitted (no `[command]` placeholder when no visible subcommand exists). |
| Default values            | `[default: ...]` is rendered only for visible arguments and options.       |

## Parsing is unaffected

The projection only affects help text generation. Parsing still recognizes the full tree:

```csharp
HelpProjection.Write(root, Console.Out, symbol => symbol.Name != "--admin-token");

// Still parses and binds, even though --admin-token was hidden from the view.
var result = root.Parse("serve --admin-token secret");
```
