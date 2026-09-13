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
    private static FrozenDictionary<string, CommandLineOption> ValidCommandOptionsTable { get; } 
        
    static App()
    {
        var table = new Dictionary<string, CommandLineOption>();

        var helpOption = new CommandLineOption("--help", "-h", CommandLineOptionId.Help, "Show help e.g. list options and aliases", IsOptional: true);
        table.Add("-h", helpOption);
        table.Add("--help", helpOption);

        var versionOption = new CommandLineOption("--version", "-v", CommandLineOptionId.Version, "Show tool version", IsOptional: true);
        table.Add("-v", versionOption);
        table.Add("--version", versionOption);

        var listAliasesOption = new CommandLineOption("--list", "-l", CommandLineOptionId.ListAliases, "List registered aliases", IsOptional: true);
        table.Add("-l", listAliasesOption);
        table.Add("--list", listAliasesOption);

        var runAsAdminOption = new CommandLineOption("--admin", "-a", CommandLineOptionId.RunAsAdmin, "Run as administrator", IsOptional: true);
        table.Add("-a", runAsAdminOption);
        table.Add("--admin", runAsAdminOption);

        ValidCommandOptionsTable = table.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    protected async override void OnStartup(StartupEventArgs e)
    {
        string[] commandArgs = e?.Args ?? [];
        CommandLineCommand command;
        try
        {
            command = await CommandLineArgumentParser.CreateCommandAsync(commandArgs, ValidCommandOptionsTable);
        }
        catch (InvalidCommandArgumnentException ex)
        {
            CommandHandler.ShowError(ex.Message);
            return;
        }

        switch (command.Options)
        {
            case var options when options.Contains(CommandLineOptionId.Help):
                base.OnStartup(e);
                await CommandHandler.ShowHelpAsync(ValidCommandOptionsTable);
                break;
            case var options when options.Contains(CommandLineOptionId.Version):
                base.OnStartup(e);
                //ShowVersion();
                break;
            case var options when options.Contains(CommandLineOptionId.ListAliases):
                base.OnStartup(e);
                await CommandHandler.ShowAliasesAsync();
                break;
            default:
                CommandHandler.LaunchTerminalWithAlias(command);
                Shutdown();
                break;
        }
    }
}

