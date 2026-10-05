// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using System.Collections.Generic;
using System.CommandLine.Help;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace System.CommandLine.Tests.Help
{
    /// <summary>
    /// Tests for the help visibility projection: a single command tree can produce
    /// different help views (for example administrator and regular user) without
    /// modifying <see cref="Symbol.Hidden"/> or the parse behavior of the tree.
    /// </summary>
    public class HelpVisibilityTests
    {
        private readonly Option<bool> _verboseOption = new("--verbose", "-v")
        {
            Description = "Verbose output",
            Recursive = true
        };

        private readonly Option<string> _adminSecretOption = new("--admin-secret", "-s")
        {
            Description = "Admin secret",
            Recursive = true,
            DefaultValueFactory = _ => "classified"
        };

        private readonly Option<int> _portOption = new("--port", "-p")
        {
            Description = "Port to listen on",
            DefaultValueFactory = _ => 8080
        };

        private readonly Option<string> _adminTokenOption = new("--admin-token", "-t")
        {
            Description = "Admin token",
            DefaultValueFactory = _ => "tok123"
        };

        private readonly Option<string> _deepSecretOption = new("--deep-secret", "-d")
        {
            Description = "Deep secret",
            DefaultValueFactory = _ => "shh"
        };

        private readonly Option<int> _levelOption = new("--level")
        {
            Description = "Admin level"
        };

        private readonly Argument<string> _pathArgument = new("path")
        {
            Description = "Path to serve",
            DefaultValueFactory = _ => "normal-default"
        };

        private readonly Argument<string> _adminArgument = new("admin-arg")
        {
            Description = "Admin argument",
            DefaultValueFactory = _ => "admin-default"
        };

        private readonly Command _deepCommand;
        private readonly Command _serveCommand;
        private readonly Command _adminCommand;
        private readonly RootCommand _rootCommand;

        public HelpVisibilityTests()
        {
            _deepCommand = new Command("deep", "Deep nested command")
            {
                _deepSecretOption
            };

            _serveCommand = new Command("serve", "Serve something")
            {
                _pathArgument,
                _adminArgument,
                _portOption,
                _adminTokenOption,
                _deepCommand
            };

            _adminCommand = new Command("admin", "Administration")
            {
                _levelOption
            };

            _serveCommand.SetAction(_ => { });
            _adminCommand.SetAction(_ => { });
            _deepCommand.SetAction(_ => { });

            _rootCommand = new RootCommand("Test CLI")
            {
                _verboseOption,
                _adminSecretOption,
                _serveCommand,
                _adminCommand
            };
        }

        /// <summary>
        /// The symbols a regular (non-administrator) caller is not authorized to see.
        /// </summary>
        private IEnumerable<Symbol> AdminOnlySymbols()
        {
            yield return _adminSecretOption;
            yield return _adminTokenOption;
            yield return _deepSecretOption;
            yield return _adminArgument;
            yield return _adminCommand;
            yield return _levelOption;
        }

        private bool RegularUserVisibility(Symbol symbol) => !AdminOnlySymbols().Contains(symbol);

        private static string GetHelp(Command command, Func<Symbol, bool>? visibility)
        {
            var output = new StringWriter();
            HelpProjection.Write(command, output, visibility ?? (_ => true));
            return output.ToString();
        }

        private string RegularHelp(Command command) => GetHelp(command, RegularUserVisibility);

        private string AdminHelp(Command command) => GetHelp(command, null);

        [Fact]
        public void Regular_view_hides_unauthorized_option_and_all_of_its_aliases()
        {
            var help = RegularHelp(_serveCommand);

            help.Should().NotContain("--admin-token");
            help.Should().NotContain("-t,");
            help.Should().NotContain(", -t");
            help.Should().NotContain("Admin token");

            help.Should().Contain("-p, --port");
        }

        [Fact]
        public void Admin_view_shows_all_options_and_aliases()
        {
            var help = AdminHelp(_serveCommand);

            help.Should().Contain("-t, --admin-token");
            help.Should().Contain("-p, --port");
        }

        [Fact]
        public void Regular_view_hides_default_values_of_unauthorized_symbols()
        {
            var help = RegularHelp(_serveCommand);

            help.Should().NotContain("tok123");
            help.Should().Contain("8080");
        }

        [Fact]
        public void Admin_view_shows_default_values()
        {
            var help = AdminHelp(_serveCommand);

            help.Should().Contain("tok123");
            help.Should().Contain("8080");
        }

        [Fact]
        public void Regular_view_hides_unauthorized_subcommands()
        {
            var help = RegularHelp(_rootCommand);

            help.Should().NotContain("admin");
            help.Should().NotContain("Administration");
            help.Should().Contain("serve");
        }

        [Fact]
        public void Regular_view_hides_unauthorized_symbols_on_nested_subcommands()
        {
            var help = RegularHelp(_deepCommand);

            help.Should().NotContain("--deep-secret");
            help.Should().NotContain("-d,");
            help.Should().NotContain(", -d");
            help.Should().NotContain("shh");
        }

        [Fact]
        public void Global_options_are_inherited_according_to_visibility()
        {
            var regularHelp = RegularHelp(_deepCommand);

            // recursive (global) options from the root follow the standard inheritance logic
            regularHelp.Should().Contain("--verbose");
            regularHelp.Should().NotContain("--admin-secret");
            regularHelp.Should().NotContain("classified");

            var adminHelp = AdminHelp(_deepCommand);

            adminHelp.Should().Contain("--verbose");
            adminHelp.Should().Contain("--admin-secret");
            adminHelp.Should().Contain("classified");
        }

        [Fact]
        public void Usage_line_does_not_leak_unauthorized_arguments_or_subcommands()
        {
            var regularHelp = RegularHelp(_serveCommand);

            // the hidden argument must not appear in the usage line
            regularHelp.Should().NotContain("<admin-arg>");
            regularHelp.Should().Contain("<path>");

            // the hidden subcommand of serve must not produce a [command] placeholder
            var helpWithoutVisibleSubcommands = GetHelp(_serveCommand,
                symbol => RegularUserVisibility(symbol) && !ReferenceEquals(symbol, _deepCommand));
            helpWithoutVisibleSubcommands.Should().NotContain("[command]");

            AdminHelp(_serveCommand).Should().Contain("[command]");
        }

        [Fact]
        public void Subcommand_row_does_not_leak_default_values_of_unauthorized_arguments()
        {
            var regularHelp = RegularHelp(_rootCommand);

            regularHelp.Should().Contain("normal-default");
            regularHelp.Should().NotContain("admin-default");

            var adminHelp = AdminHelp(_rootCommand);

            adminHelp.Should().Contain("normal-default");
            adminHelp.Should().Contain("admin-default");
        }

        [Fact]
        public void Parse_still_recognizes_symbols_hidden_from_the_view()
        {
            var byAlias = _rootCommand.Parse("serve --admin-token secret-value");
            byAlias.Errors.Should().BeEmpty();
            byAlias.GetValue(_adminTokenOption).Should().Be("secret-value");

            var byShortAlias = _rootCommand.Parse("serve -t other-value");
            byShortAlias.Errors.Should().BeEmpty();
            byShortAlias.GetValue(_adminTokenOption).Should().Be("other-value");

            var subcommand = _rootCommand.Parse("admin --level 3");
            subcommand.Errors.Should().BeEmpty();
            subcommand.GetValue(_levelOption).Should().Be(3);

            var nested = _rootCommand.Parse("serve deep --deep-secret x");
            nested.Errors.Should().BeEmpty();
            nested.GetValue(_deepSecretOption).Should().Be("x");
        }

        [Fact]
        public void Projection_does_not_modify_the_symbol_tree()
        {
            var symbolsBefore = AllSymbols().Select(s => s.Hidden).ToArray();

            _ = RegularHelp(_rootCommand);
            _ = RegularHelp(_serveCommand);
            _ = RegularHelp(_deepCommand);

            AllSymbols().Select(s => s.Hidden).Should().Equal(symbolsBefore);
            AllSymbols().Should().OnlyContain(s => !s.Hidden);
        }

        [Fact]
        public void Same_tree_can_generate_regular_and_admin_views_in_any_order()
        {
            // simulates interleaved help requests with different authorization sets
            var regular1 = RegularHelp(_serveCommand);
            var admin = AdminHelp(_serveCommand);
            var regular2 = RegularHelp(_serveCommand);

            regular1.Should().Be(regular2);
            regular1.Should().NotContain("--admin-token");
            admin.Should().Contain("--admin-token");
        }

        [Fact]
        public async Task HelpAction_applies_SymbolVisibility_to_generated_help()
        {
            var helpOption = _rootCommand.Options.OfType<HelpOption>().Single();
            helpOption.Action = new HelpAction
            {
                SymbolVisibility = RegularUserVisibility
            };

            var output = new StringWriter();
            var parseResult = _rootCommand.Parse("serve --help");

            await parseResult.InvokeAsync(new() { Output = output }, CancellationToken.None);

            var help = output.ToString();
            help.Should().Contain("--port");
            help.Should().NotContain("--admin-token");
            help.Should().NotContain("tok123");
        }

        [Fact]
        public async Task HelpAction_without_SymbolVisibility_uses_default_hidden_behavior()
        {
            var output = new StringWriter();
            var parseResult = _rootCommand.Parse("serve --help");

            await parseResult.InvokeAsync(new() { Output = output }, CancellationToken.None);

            // nothing is Hidden on the tree, so everything is shown
            output.ToString().Should().Contain("--admin-token");
        }

        [Fact]
        public void Visibility_projection_composes_with_Hidden()
        {
            _adminTokenOption.Hidden = true;
            try
            {
                // even the administrator view honors Hidden when the predicate composes with it
                var help = GetHelp(_serveCommand, symbol => !symbol.Hidden);

                help.Should().NotContain("--admin-token");
                help.Should().Contain("--port");
            }
            finally
            {
                _adminTokenOption.Hidden = false;
            }
        }

        private IEnumerable<Symbol> AllSymbols()
        {
            yield return _rootCommand;
            yield return _verboseOption;
            yield return _adminSecretOption;
            yield return _serveCommand;
            yield return _pathArgument;
            yield return _adminArgument;
            yield return _portOption;
            yield return _adminTokenOption;
            yield return _deepCommand;
            yield return _deepSecretOption;
            yield return _adminCommand;
            yield return _levelOption;
        }
    }
}
