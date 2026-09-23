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
    private const string ConfigYamlFileName = @"config.yaml";
    private static readonly string s_relativeConfigYamlFilePath = Path.Combine("Config", ConfigYamlFileName);
    private static readonly string s_configFilePath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, s_relativeConfigYamlFilePath);
    private static readonly IApplicationSettings s_applicationSettings = new MicrosoftWindowsStorageSettings();

    static App()
    {
        var table = new Dictionary<string, CommandLineOptionDescriptor>();

        var helpOption = new CommandLineOptionDescriptor("--help", "-h", CommandLineOptionId.Help, CommandLineOptionKind.Flag, ["Show help e.g. list options and aliases"], "lit --help", IsOptional: true);
        table.Add("-h", helpOption);
        table.Add("--help", helpOption);

        var versionOption = new CommandLineOptionDescriptor("--version", "-v", CommandLineOptionId.Version, CommandLineOptionKind.Flag, ["Show tool version"], "lit --version", IsOptional: true);
        table.Add("-v", versionOption);
        table.Add("--version", versionOption);

        var listAliasesOption = new CommandLineOptionDescriptor("--list", "-l", CommandLineOptionId.ListAliases, CommandLineOptionKind.Flag, ["List registered Windows Terminal", "profiles and aliases"], "lit --list", IsOptional: true);
        table.Add("-l", listAliasesOption);
        table.Add("--list", listAliasesOption);

        var runAsAdminOption = new CommandLineOptionDescriptor("--admin", "-a", CommandLineOptionId.RunAsAdmin, CommandLineOptionKind.Flag, ["Run Windows Terminal elevated"], "lit ps --admin", IsOptional: true);
        table.Add("-a", runAsAdminOption);
        table.Add("--admin", runAsAdminOption);

        var setConfigLocationOption = new CommandLineOptionDescriptor("--config", "-c", CommandLineOptionId.GetOrSetConfigLocation, CommandLineOptionKind.Mode, ["Set new or get the location of the", "current configuration file"], @"lit --config --destination ""%USERPROFILE%/.lit""", IsOptional: true);
        table.Add("-c", setConfigLocationOption);
        table.Add("--config", setConfigLocationOption);

        var sourceLocationOption = new CommandLineOptionDescriptor("--source", "-s", CommandLineOptionId.SourcePath, CommandLineOptionKind.Value, ["Specify the source path"], @"lit --config --source ""%TEMP%/config.yaml"" --destination ""%USERPROFILE%/.lit""", IsOptional: true);
        table.Add("-s", sourceLocationOption);
        table.Add("--source", sourceLocationOption);

        var destinationLocationOption = new CommandLineOptionDescriptor("--destination", "-d", CommandLineOptionId.DestinationPath, CommandLineOptionKind.Value, ["Specify the destination path"], @"lit --config --destination ""%USERPROFILE%/.lit""", IsOptional: true);
        table.Add("-d", destinationLocationOption);
        table.Add("--destination", destinationLocationOption);

        var printOption = new CommandLineOptionDescriptor("--print", "-p", CommandLineOptionId.Print, CommandLineOptionKind.Flag, ["Print the specified value"], @"lit --config --print", IsOptional: false);
        table.Add("-p", printOption);
        table.Add("--print", printOption);

        var userScopeOption = new CommandLineOptionDescriptor("--user", "-u", CommandLineOptionId.EnvironmentVariableScopeUser, CommandLineOptionKind.Flag, ["Specify the scope of the", "environment variable as 'user'"], @"lit --variable ""PATH"" --value ""C:\Folder"" --user --join", IsOptional: false);
        table.Add("-u", userScopeOption);
        table.Add("--user", userScopeOption);

        var systemScopeOption = new CommandLineOptionDescriptor("--machine", "-m", CommandLineOptionId.EnvironmentVariableScopeMachine, CommandLineOptionKind.Flag, ["Specify the scope of the", "environment variable as 'system'"], @"lit --variable ""PATH"" --value ""C:\Folder"" --machine --join", IsOptional: false);
        table.Add("-m", systemScopeOption);
        table.Add("--machine", systemScopeOption);

        var variableScopeOption = new CommandLineOptionDescriptor("--variable", "--var", CommandLineOptionId.SetEnvironmentVariable, CommandLineOptionKind.ModeAndValue, ["Set or create an environment variable"], @"lit --variable ""PATH"" --value ""C:\Folder"" --machine --join", IsOptional: false);
        table.Add("--var", variableScopeOption);
        table.Add("--variable", variableScopeOption);

        var variableValueOption = new CommandLineOptionDescriptor("--value", "--val", CommandLineOptionId.EnvironmentVariableValue, CommandLineOptionKind.Value, ["Specify the new value of", "the environment variable"], @"lit --variable ""PATH"" --value ""C:\Folder"" --machine --join", IsOptional: false);
        table.Add("--value", variableValueOption);
        table.Add("--val", variableValueOption);

        var joinWriteModeOption = new CommandLineOptionDescriptor("--join", "-j", CommandLineOptionId.EnvironmentVariableWriteModeJoin, CommandLineOptionKind.Flag, ["Specify that the new value of", "the environment variable is appended", "to the existing value"], @"lit --variable ""PATH"" --value ""C:\Folder"" --machine --join", IsOptional: false);
        table.Add("-j", joinWriteModeOption);
        table.Add("--join", joinWriteModeOption);

        var replaceWriteModeOption = new CommandLineOptionDescriptor("--replace", "-r", CommandLineOptionId.EnvironmentVariableWriteModeReplace, CommandLineOptionKind.Flag, ["Specify that the new value of", "the environment variable replaces", "the existing value"], @"lit --variable ""TEMP"" --value ""C:\Folder"" --machine --replace", IsOptional: false);
        table.Add("-r", replaceWriteModeOption);
        table.Add("--replace", replaceWriteModeOption);

        var delimiterOption = new CommandLineOptionDescriptor("--delimiter", "--del", CommandLineOptionId.EnvironmentVariableWriteModeJoinDelimiter, CommandLineOptionKind.Value, [$"Specifies the delimiter used to join", "the values of the environment variable.", "The default is the path separator ';'"], @"lit --variable ""TEMP"" --value ""C:\Folder"" --machine --replace --delimiter "";""", IsOptional: true);
        table.Add("--del", delimiterOption);
        table.Add("--delimiter", delimiterOption);

        ValidCommandOptionsTable = table.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    protected async override void OnStartup(StartupEventArgs e)
    {
        string userConfigurationFilePath = s_applicationSettings.GetOrUpdateValue(
            AppSettingsKeys.UserConfigFileLocationKey, 
            _ => s_configFilePath,
            configPath => !File.Exists(configPath));

        string[] commandArgs = e?.Args ?? [];
        CommandLineCommand command;
        try
        {
            command = await CommandLineArgumentParser.CreateCommandAsync(commandArgs, ValidCommandOptionsTable, userConfigurationFilePath);
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
                await CommandHandler.HandleMode(command, s_applicationSettings);
                break;
            case var options when options.ContainsKey(CommandLineOptionId.Help):
                base.OnStartup(e);
                await CommandHandler.ShowHelpAsync(ValidCommandOptionsTable, userConfigurationFilePath);
                break;
            case var options when options.ContainsKey(CommandLineOptionId.ListAliases):
                base.OnStartup(e);
                await CommandHandler.ShowAliasesAsync(userConfigurationFilePath);
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
                case CommandLineOptionId.GetOrSetConfigLocation:
                    ThrowIfModeAlreadySet();

                    modeOption = option;
                    bool isPrintOptionProvided = command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.Print, out CommandLineOption printOption);
                    CommandLineOption destinationPathOption = default;
                    if (command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.SourcePath, out CommandLineOption sourcePathOption))
                    {
                        if (isPrintOptionProvided)
                        {
                            throw new InvalidCommandArgumentException($"Invalid command argument. Providing the '{option.Descriptor.Name} | {option.Descriptor.AlternativeName}' option together with the '{printOption.Descriptor.Name} | {printOption.Descriptor.AlternativeName}' is not allowed. Use 'lit --help' to get the comamnd syntax.");
                        }

                        string sourcePath = sourcePathOption.Value;
                        if (string.IsNullOrWhiteSpace(sourcePath))
                        {
                            throw new InvalidCommandArgumentException($"Invalid command argument. A '{option.Descriptor.Name} | {option.Descriptor.AlternativeName}' option was provided but no path value.");
                        }

                        if (!CommandHandlerHelpers.IsFilePath(sourcePath))
                        {
                            throw new InvalidCommandArgumentException($"Invalid path argument. A source file path must provide the file name of the source.");
                        }

                        if (!Path.HasExtension(sourcePath)
                            || !(Path.GetExtension(sourcePath).Equals(".yaml", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(sourcePath).Equals(".yml", StringComparison.OrdinalIgnoreCase)))
                        {
                            throw new InvalidCommandArgumentException($"Invalid path argument. A '{option.Descriptor.Name} | {option.Descriptor.AlternativeName}' option was provided but the file extension does not match '.yaml' or '.yml'.");
                        }
                    }
                    else if (command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.DestinationPath, out destinationPathOption))
                    {
                        if (isPrintOptionProvided)
                        {
                            throw new InvalidCommandArgumentException($"Invalid command argument. Providing the '{option.Descriptor.Name} | {option.Descriptor.AlternativeName}' option together with the '{printOption.Descriptor.Name} | {printOption.Descriptor.AlternativeName}' is not allowed. Use 'lit --help' to get the comamnd syntax.");
                        }

                        string destinationPath = option.Value;
                        if (string.IsNullOrWhiteSpace(destinationPath))
                        {
                            throw new InvalidCommandArgumentException($"Invalid command argument. A '{option.Descriptor.Name} | {option.Descriptor.AlternativeName}' option was provided but no path value.");
                        }

                        // Only validate extension if the path is a file path.
                        // Otherwise allow the destination to be a directory.
                        if (CommandHandlerHelpers.IsFilePath(destinationPath)
                            && !Path.HasExtension(destinationPath) 
                            || !(Path.GetExtension(destinationPath).Equals(".yaml", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(destinationPath).Equals(".yml", StringComparison.OrdinalIgnoreCase)))
                        {
                            throw new InvalidCommandArgumentException($"Invalid path argument. A '{option.Descriptor.Name} | {option.Descriptor.AlternativeName}' option was provided but the file extension does not match '.yaml' or '.yml'.");
                        }
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

