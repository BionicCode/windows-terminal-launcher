namespace Main;

using System.Collections.Frozen;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Windows;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static FrozenDictionary<string, CommandLineOption> ValidCommandOptionsTable { get; } = new KeyValuePair<string, CommandLineOption>[]
    {
        new KeyValuePair<string, CommandLineOption>("-h", new CommandLineOption("-h", CommandLineOptionId.Help, "Show help e.g. list options and aliases", IsOptional: true)),
        new KeyValuePair<string, CommandLineOption>("--help", new CommandLineOption("--help", CommandLineOptionId.Help, "Show help e.g. show syntax and list options and registered aliases", IsOptional: true)),
        new KeyValuePair<string, CommandLineOption>("-v", new CommandLineOption("-v", CommandLineOptionId.Version, "Show tool version", IsOptional: true)),
        new KeyValuePair<string, CommandLineOption>("--version", new CommandLineOption("--version", CommandLineOptionId.Version, "Show tool version", IsOptional: true)),
        new KeyValuePair<string, CommandLineOption>("-l", new CommandLineOption("-l", CommandLineOptionId.ListAliases, "List registered aliases", IsOptional: true)),
        new KeyValuePair<string, CommandLineOption>("--list", new CommandLineOption("--list", CommandLineOptionId.ListAliases, "List registered aliases", IsOptional: true)),
        new KeyValuePair<string, CommandLineOption>("-a", new CommandLineOption("-a", CommandLineOptionId.RunAsAdmin, "Run as administrator", IsOptional: true)),
        new KeyValuePair<string, CommandLineOption>("--admin", new CommandLineOption("--admin", CommandLineOptionId.RunAsAdmin, "Run as administrator", IsOptional: true)),
    }.ToFrozenDictionary();

    protected async override void OnStartup(StartupEventArgs e)
    {
        string[] commandArgs = e?.Args ?? [];
        CommandLineCommand command = await CommandLineArgumentParser.CreateCommandAsync(commandArgs, ValidCommandOptionsTable);
        switch (command.Options)
        {
            case var options when options.Contains(CommandLineOptionId.Help):
                base.OnStartup(e);
                //ShowHelp();
                break;
            case var options when options.Contains(CommandLineOptionId.Version):
                base.OnStartup(e);
                //ShowVersion();
                break;
            case var options when options.Contains(CommandLineOptionId.ListAliases):
                base.OnStartup(e);
                //ListAliases();
                break;
            default:
                CommandHandler.LaunchTerminalWithAlias(command);
                //Shutdown();
                break;
        }
    }
}

