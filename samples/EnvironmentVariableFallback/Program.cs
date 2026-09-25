// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.CommandLine;
using System.CommandLine.Parsing;

Option<int> portOption = new("--port")
{
    Description = "The port to listen on. Falls back to the PORT environment variable.",
    DefaultValueFactory = _ => 80,
    EnvironmentVariableBinding = new EnvironmentVariableBinding("PORT")
};

Option<string[]> tagsOption = new("--tags")
{
    Description = "Tags to apply. Falls back to the TAGS environment variable (semicolon-delimited).",
    EnvironmentVariableBinding = new EnvironmentVariableBinding("TAGS")
};

Option<string> secretOption = new("--secret")
{
    Description = "A secret token. Falls back to the APP_SECRET environment variable.",
    EnvironmentVariableBinding = new EnvironmentVariableBinding("APP_SECRET") { IsSensitive = true }
};

RootCommand rootCommand = new("Prints option values resolved from the command line, environment variables, or defaults.")
{
    portOption,
    tagsOption,
    secretOption
};

rootCommand.SetAction((ParseResult parseResult) =>
{
    PrintValue(parseResult, portOption);
    PrintValue(parseResult, tagsOption);
    PrintValue(parseResult, secretOption);
});

return rootCommand.Parse(args).Invoke();

static void PrintValue<T>(ParseResult parseResult, Option<T> option)
{
    string source = parseResult.GetResult(option) switch
    {
        null => "not provided",
        { EnvironmentVariableSource: { } binding } => $"environment variable '{binding.VariableName}'",
        { Implicit: true } => "default value",
        _ => "command line"
    };

    object? value = parseResult.GetValue(option);

    string display = option.EnvironmentVariableBinding is { IsSensitive: true } && value is not null
        ? "***" // sensitive values are never echoed
        : value switch
        {
            null => "(null)",
            string s => $"'{s}'",
            IEnumerable items => string.Join(", ", items.Cast<object>()),
            _ => Convert.ToString(value) ?? ""
        };

    Console.WriteLine($"{option.Name}: {display} (source: {source})");
}
