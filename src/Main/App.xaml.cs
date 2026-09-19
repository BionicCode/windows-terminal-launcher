namespace Main;

using System.Buffers;
using System.Collections.Frozen;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using Microsoft.VisualBasic.FileIO;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static FrozenDictionary<string, CommandLineOptionDescriptor> ValidCommandOptionsTable { get; } 
        
    static App()
    {
        var table = new Dictionary<string, CommandLineOptionDescriptor>();

        var helpOption = new CommandLineOptionDescriptor("--help", "-h", CommandLineOptionId.Help, CommandLineOptionKind.Flag, "Show help e.g. list options and aliases", IsOptional: true);
        table.Add("-h", helpOption);
        table.Add("--help", helpOption);

        var versionOption = new CommandLineOptionDescriptor("--version", "-v", CommandLineOptionId.Version, CommandLineOptionKind.Flag, "Show tool version", IsOptional: true);
        table.Add("-v", versionOption);
        table.Add("--version", versionOption);

        var listAliasesOption = new CommandLineOptionDescriptor("--list", "-l", CommandLineOptionId.ListAliases, CommandLineOptionKind.Flag, "List registered aliases", IsOptional: true);
        table.Add("-l", listAliasesOption);
        table.Add("--list", listAliasesOption);

        var runAsAdminOption = new CommandLineOptionDescriptor("--admin", "-a", CommandLineOptionId.RunAsAdmin, CommandLineOptionKind.Flag, "Run as administrator", IsOptional: true);
        table.Add("-a", runAsAdminOption);
        table.Add("--admin", runAsAdminOption);

        var setConfigLocationOption = new CommandLineOptionDescriptor("--config", "-c", CommandLineOptionId.SetConfigLocation, CommandLineOptionKind.Mode, "Set configuration file location", IsOptional: true);
        table.Add("-c", setConfigLocationOption);
        table.Add("--config", setConfigLocationOption);

        var sourceLocationOption = new CommandLineOptionDescriptor("--source", "-s", CommandLineOptionId.SourcePath, CommandLineOptionKind.Value, "Specifies the source path", IsOptional: true);
        table.Add("-s", sourceLocationOption);
        table.Add("--source", sourceLocationOption);

        var destinationLocationOption = new CommandLineOptionDescriptor("--destination", "-d", CommandLineOptionId.DestinationPath, CommandLineOptionKind.Value, "Specifies the source path", IsOptional: false);
        table.Add("-d", destinationLocationOption);
        table.Add("--destination", destinationLocationOption);

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
        catch (InvalidCommandArgumentException ex)
        {
            CommandHandler.ShowError(ex.Message);
            return;
        }

        CommandValidator.ThrowIfCommandSyntaxIsInvalid(command);

        switch (command.Arguments.OptionsTable)
        {
            case var _ when command.HasMode:
                await CommandHandler.HandleMode(command);
                break;
            case var options when options.ContainsKey(CommandLineOptionId.Help):
                base.OnStartup(e);
                await CommandHandler.ShowHelpAsync(ValidCommandOptionsTable);
                break;
            case var options when options.ContainsKey(CommandLineOptionId.ListAliases):
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

internal static class CommandValidator
{
    public static void ThrowIfCommandSyntaxIsInvalid(CommandLineCommand command)
    {
        CommandLineOption? modeOption = null;
        void ThrowIfModeAlreadySet()
        {
            if (modeOption is not null)
            {
                throw new InvalidCommandArgumentException("Invalid command argument. A command can only have a single mode option. Use 'lit --help' to get a list of mode options.");
            }
        }

        foreach (KeyValuePair<CommandLineOptionId, CommandLineOption> entry in command.Arguments.OptionsTable)
        {
            CommandLineOption option = entry.Value;
            switch (option.Descriptor.OptionType)
            {
                case CommandLineOptionId.SetConfigLocation:
                    ThrowIfModeAlreadySet();

                    modeOption = option;
                    CommandLineOption destinationPathOption = default;
                    if (command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.SourcePath, out CommandLineOption sourcePathOption))
                    {
                        string sourcePath = sourcePathOption.Value;
                        if (string.IsNullOrWhiteSpace(sourcePath))
                        {
                            throw new InvalidCommandArgumentException($"Invalid command argument. A '{option.Descriptor.Name} | {option.Descriptor.AlternativeName}' option was provided but no path value.");
                        }

                        if (Path.HasExtension(sourcePath)
                            && !(Path.GetExtension(sourcePath).Equals(".yaml", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(sourcePath).Equals(".yml", StringComparison.OrdinalIgnoreCase)))
                        {
                            throw new InvalidCommandArgumentException($"Invalid path argument. A '{option.Descriptor.Name} | {option.Descriptor.AlternativeName}' option was provided but the file extension does not match '.yaml' or '.yml'.");
                        }
                    }
                    else if (command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.DestinationPath, out destinationPathOption))
                    {
                        string destinationPath = option.Value;
                        if (string.IsNullOrWhiteSpace(destinationPath))
                        {
                            throw new InvalidCommandArgumentException($"Invalid command argument. A '{option.Descriptor.Name} | {option.Descriptor.AlternativeName}' option was provided but no path value.");
                        }

                        if (Path.HasExtension(destinationPath) 
                            && !(Path.GetExtension(destinationPath).Equals(".yaml", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(destinationPath).Equals(".yml", StringComparison.OrdinalIgnoreCase)))
                        {
                            throw new InvalidCommandArgumentException($"Invalid path argument. A '{option.Descriptor.Name} | {option.Descriptor.AlternativeName}' option was provided but the file extension does not match '.yaml' or '.yml'.");
                        }
                    }

                    if (destinationPathOption == default)
                    {
                        throw new InvalidCommandArgumentException($"Invalid command form. When selecting the mode '{option.Descriptor.Name} | {option.Descriptor.AlternativeName}' the command requires at least a destination path. The destination path is not optional. Use 'lit --help' to get the comamnd syntax a list of mode options.");
                    }

                    if (command.HasAlias)
                    {
                        throw new InvalidCommandArgumentException($"Invalid command form. When selecting the mode '{option.Descriptor.Name} | {option.Descriptor.AlternativeName}' the command cann't specify an alias. Use 'lit --help' to get the comamnd syntax.");
                    }

                    break;
            }
        }
    }
}

