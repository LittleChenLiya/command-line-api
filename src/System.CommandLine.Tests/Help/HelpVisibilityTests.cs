// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using System.Collections.Generic;
using System.CommandLine.Help;
using System.IO;
using System.Linq;
using Xunit;

namespace System.CommandLine.Tests.Help
{
    public class HelpVisibilityTests
    {
        private readonly HelpBuilder _helpBuilder;

        public HelpVisibilityTests()
        {
            _helpBuilder = new HelpBuilder(200);
        }

        private string GetHelp(Command command, HelpVisibility? visibility = null)
        {
            using var writer = new StringWriter();
            _helpBuilder.Write(command, writer, visibility);
            return writer.ToString();
        }

        [Fact]
        public void Same_tree_generates_admin_and_normal_views()
        {
            var command = new Command("app")
            {
                new Option<string>("--public", "-p") { Description = "Public option" },
                new Option<string>("--secret", "-s") { Description = "Secret option" },
                new Option<string>("--classified", "-c") { Description = "Classified option" },
            };

            var admin = new HelpVisibility(_ => true);
            var normal = new HelpVisibility(symbol => symbol.Name != "--secret" && symbol.Name != "--classified");

            var adminHelp = GetHelp(command, admin);
            var normalHelp = GetHelp(command, normal);

            adminHelp.Should().Contain("--public");
            adminHelp.Should().Contain("--secret");
            adminHelp.Should().Contain("--classified");

            normalHelp.Should().Contain("--public");
            normalHelp.Should().NotContain("--secret");
            normalHelp.Should().NotContain("--classified");
        }

        [Fact]
        public void Normal_help_does_not_leak_hidden_option_aliases()
        {
            var command = new Command("app")
            {
                new Option<string>("--public", "-p") { Description = "Public option" },
                new Option<string>("--secret", "-s") { Description = "Secret option" },
            };

            var normal = new HelpVisibility(symbol => symbol.Name != "--secret");

            var help = GetHelp(command, normal);

            help.Should().NotContain("--secret");
            help.Should().NotContain("-s");
            help.Should().Contain("--public");
            help.Should().Contain("-p");
        }

        [Fact]
        public void Hidden_option_aliases_are_not_visible_in_any_form()
        {
            var command = new Command("app")
            {
                new Option<string>("--secret", "-s", "/s") { Description = "Secret option" },
            };

            var normal = new HelpVisibility(symbol => symbol.Name != "--secret");

            var help = GetHelp(command, normal);

            help.Should().NotContain("--secret");
            help.Should().NotContain("-s");
            help.Should().NotContain("/s");
        }

        [Fact]
        public void Parse_still_recognizes_original_tree_with_hidden_options()
        {
            var command = new Command("app")
            {
                new Option<string>("--public", "-p"),
                new Option<string>("--secret", "-s"),
            };

            var normal = new HelpVisibility(symbol => symbol.Name != "--secret");

            _ = GetHelp(command, normal);

            var result = command.Parse("--secret value --public pub");

            result.Errors.Should().BeEmpty();
            result.GetValue<string>("--secret").Should().Be("value");
            result.GetValue<string>("--public").Should().Be("pub");
        }

        [Fact]
        public void Visibility_view_does_not_mutate_Hidden_property()
        {
            var command = new Command("app")
            {
                new Option<string>("--public", "-p"),
                new Option<string>("--secret", "-s"),
            };

            var normal = new HelpVisibility(symbol => symbol.Name != "--secret");

            _ = GetHelp(command, normal);

            command.Options.First(o => o.Name == "--secret").Hidden.Should().BeFalse();
            command.Options.First(o => o.Name == "--public").Hidden.Should().BeFalse();
        }

        [Fact]
        public void Default_values_are_filtered_by_visibility()
        {
            var command = new Command("app")
            {
                new Option<string>("--public", "-p")
                {
                    Description = "Public option",
                    DefaultValueFactory = _ => "public-default"
                },
                new Option<string>("--secret", "-s")
                {
                    Description = "Secret option",
                    DefaultValueFactory = _ => "secret-default"
                },
            };

            var normal = new HelpVisibility(symbol => symbol.Name != "--secret");

            var help = GetHelp(command, normal);

            help.Should().Contain("public-default");
            help.Should().NotContain("secret-default");
        }

        [Fact]
        public void Default_values_for_hidden_command_arguments_are_not_shown()
        {
            var command = new Command("outer")
            {
                new Command("inner")
                {
                    new Argument<string>("visible-arg") { DefaultValueFactory = _ => "visible-value" },
                    new Argument<string>("hidden-arg") { DefaultValueFactory = _ => "hidden-value" },
                }
            };

            var normal = new HelpVisibility(symbol => symbol.Name != "hidden-arg");

            var help = GetHelp(command, normal);

            help.Should().Contain("visible-value");
            help.Should().NotContain("hidden-value");
        }

        [Fact]
        public void Nested_subcommands_are_filtered_by_visibility()
        {
            var command = new Command("app")
            {
                new Command("admin")
                {
                    new Command("users") { Description = "Manage users" },
                    new Command("settings") { Description = "Manage settings" },
                },
                new Command("public")
                {
                    new Command("status") { Description = "Show status" },
                },
            };

            var normal = new HelpVisibility(symbol => symbol.Name != "admin" && symbol.Name != "users" && symbol.Name != "settings");

            // Viewing the public command's help shows its nested subcommands
            var publicCmd = command.Subcommands.First(c => c.Name == "public");
            var help = GetHelp(publicCmd, normal);

            help.Should().Contain("status");
            help.Should().NotContain("users");
            help.Should().NotContain("settings");
        }

        [Fact]
        public void Nested_subcommand_help_filters_options_by_visibility()
        {
            var command = new Command("app")
            {
                new Command("admin")
                {
                    new Option<string>("--secret", "-s") { Description = "Secret option" },
                    new Option<string>("--public", "-p") { Description = "Public option" },
                },
            };

            var normal = new HelpVisibility(symbol => symbol.Name != "--secret");

            // Viewing the admin command's help shows its options
            var admin = command.Subcommands.First(c => c.Name == "admin");
            var help = GetHelp(admin, normal);

            help.Should().NotContain("--secret");
            help.Should().NotContain("-s");
            help.Should().Contain("--public");
        }

        [Fact]
        public void Global_option_inheritance_follows_original_help_logic()
        {
            var command = new RootCommand
            {
                new Option<string>("--global", "-g") { Description = "Global option", Recursive = true },
                new Command("sub")
                {
                    new Option<string>("--local", "-l") { Description = "Local option" },
                },
            };

            var normal = new HelpVisibility(symbol => symbol.Name != "--global");

            // Viewing the sub command's help shows its options plus inherited recursive options
            var sub = command.Subcommands.First();
            var help = GetHelp(sub, normal);

            help.Should().NotContain("--global");
            help.Should().NotContain("-g");
            help.Should().Contain("--local");
            help.Should().Contain("-l");
        }

        [Fact]
        public void Argument_order_follows_original_help_logic()
        {
            var command = new Command("app")
            {
                new Argument<string>("first") { Description = "First argument" },
                new Argument<string>("second") { Description = "Second argument" },
                new Argument<string>("hidden") { Description = "Hidden argument" },
            };

            var normal = new HelpVisibility(symbol => symbol.Name != "hidden");

            var help = GetHelp(command, normal);

            help.Should().Contain("first");
            help.Should().Contain("second");
            help.Should().NotContain("hidden");

            var firstIndex = help.IndexOf("first");
            var secondIndex = help.IndexOf("second");
            firstIndex.Should().BeLessThan(secondIndex);
        }

        [Fact]
        public void Concurrent_help_requests_with_different_views_do_not_interfere()
        {
            var command = new Command("app")
            {
                new Option<string>("--public", "-p") { Description = "Public option" },
                new Option<string>("--secret", "-s") { Description = "Secret option" },
            };

            var admin = new HelpVisibility(_ => true);
            var normal = new HelpVisibility(symbol => symbol.Name != "--secret");

            string adminHelp = null;
            string normalHelp = null;
            System.Threading.Tasks.Parallel.Invoke(
                () => adminHelp = GetHelp(command, admin),
                () => normalHelp = GetHelp(command, normal));

            adminHelp.Should().Contain("--secret");
            normalHelp.Should().NotContain("--secret");
            normalHelp.Should().Contain("--public");
        }

        [Fact]
        public void HelpVisibility_All_shows_all_symbols()
        {
            var command = new Command("app")
            {
                new Option<string>("--public", "-p") { Description = "Public option" },
                new Option<string>("--secret", "-s") { Description = "Secret option" },
            };

            var help = GetHelp(command, HelpVisibility.All);

            help.Should().Contain("--public");
            help.Should().Contain("--secret");
        }

        [Fact]
        public void HelpOption_uses_configured_visibility()
        {
            var command = new Command("app")
            {
                new Option<string>("--public", "-p") { Description = "Public option" },
                new Option<string>("--secret", "-s") { Description = "Secret option" },
                new HelpOption
                {
                    Action = new HelpAction
                    {
                        Visibility = new HelpVisibility(symbol => symbol.Name != "--secret")
                    }
                },
            };

            var output = new StringWriter();
            command.Parse("-h").Invoke(new() { Output = output });

            var help = output.ToString();
            help.Should().Contain("--public");
            help.Should().NotContain("--secret");
        }

        [Fact]
        public void Usage_section_does_not_show_hidden_subcommand_or_options()
        {
            var command = new Command("app")
            {
                new Option<string>("--public", "-p") { Description = "Public option" },
                new Option<string>("--secret", "-s") { Description = "Secret option", Recursive = true },
                new Command("admin") { Description = "Admin command" },
                new Command("public") { Description = "Public command" },
            };

            var normal = new HelpVisibility(symbol => symbol.Name != "--secret" && symbol.Name != "admin");

            var help = GetHelp(command, normal);

            help.Should().Contain("public");
            help.Should().NotContain("admin");
            help.Should().NotContain("--secret");
        }
    }
}
