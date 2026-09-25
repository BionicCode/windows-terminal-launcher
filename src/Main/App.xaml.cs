namespace Main;

using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Immutable;
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

        var aliasOption = new CommandLineOptionDescriptor(string.Empty, string.Empty, CommandLineOptionId.LaunchWindowsTerminal, CommandLineOptionKind.ModeAndValue, "Launch-Windows-Terminal", ["Launches the Windows Terminal", "with a provided working directory."], "lit ps", IsOptional: false);
        table.Add("alias", aliasOption);

        var helpOption = new CommandLineOptionDescriptor("--help", "-h", CommandLineOptionId.Help, CommandLineOptionKind.Mode, "Show-Help", ["Show help e.g. list options and aliases"], "lit --help", IsOptional: true);
        table.Add("-h", helpOption);
        table.Add("--help", helpOption);

        var versionOption = new CommandLineOptionDescriptor("--version", "-v", CommandLineOptionId.Version, CommandLineOptionKind.Mode, "Show-Version", ["Show tool version"], "lit --version", IsOptional: true);
        table.Add("-v", versionOption);
        table.Add("--version", versionOption);

        var listAliasesOption = new CommandLineOptionDescriptor("--list", "-l", CommandLineOptionId.ListAliases, CommandLineOptionKind.Flag, "List-Profile-Alises", ["List registered Windows Terminal", "profiles and aliases"], "lit --list", IsOptional: true);
        table.Add("-l", listAliasesOption);
        table.Add("--list", listAliasesOption);

        var runAsAdminOption = new CommandLineOptionDescriptor("--admin", "-a", CommandLineOptionId.RunAsAdmin, CommandLineOptionKind.Flag, string.Empty, ["Run Windows Terminal elevated"], "lit ps --admin", IsOptional: true);
        table.Add("-a", runAsAdminOption);
        table.Add("--admin", runAsAdminOption);

        var setConfigLocationOption = new CommandLineOptionDescriptor("--config", "-c", CommandLineOptionId.GetOrSetConfigLocation, CommandLineOptionKind.Mode, "GetOrSet-Config-Location", ["Set new or get the location of the", "current configuration file"], @"lit --config --destination ""%USERPROFILE%/.lit""", IsOptional: true);
        table.Add("-c", setConfigLocationOption);
        table.Add("--config", setConfigLocationOption);

        var sourceLocationOption = new CommandLineOptionDescriptor("--source", "-s", CommandLineOptionId.SourcePath, CommandLineOptionKind.Value, string.Empty, ["Specify the source path"], @"lit --config --source ""%TEMP%/config.yaml"" --destination ""%USERPROFILE%/.lit""", IsOptional: true);
        table.Add("-s", sourceLocationOption);
        table.Add("--source", sourceLocationOption);

        var destinationLocationOption = new CommandLineOptionDescriptor("--destination", "-d", CommandLineOptionId.DestinationPath, CommandLineOptionKind.Value, string.Empty, ["Specify the destination path"], @"lit --config --destination ""%USERPROFILE%/.lit""", IsOptional: true);
        table.Add("-d", destinationLocationOption);
        table.Add("--destination", destinationLocationOption);

        var printOption = new CommandLineOptionDescriptor("--print", "-p", CommandLineOptionId.Print, CommandLineOptionKind.Flag, string.Empty, ["Print the specified value"], @"lit --config --print", IsOptional: false);
        table.Add("-p", printOption);
        table.Add("--print", printOption);

        var userScopeOption = new CommandLineOptionDescriptor("--user", "-u", CommandLineOptionId.EnvironmentVariableScopeUser, CommandLineOptionKind.Flag, string.Empty, ["Specify the scope of the", "environment variable as 'user'"], @"lit --variable ""PATH"" --value ""C:\Folder"" --user --join", IsOptional: false);
        table.Add("-u", userScopeOption);
        table.Add("--user", userScopeOption);

        var systemScopeOption = new CommandLineOptionDescriptor("--machine", "-m", CommandLineOptionId.EnvironmentVariableScopeMachine, CommandLineOptionKind.Flag, string.Empty, ["Specify the scope of the", "environment variable as 'system'"], @"lit --variable ""PATH"" --value ""C:\Folder"" --machine --join", IsOptional: false);
        table.Add("-m", systemScopeOption);
        table.Add("--machine", systemScopeOption);

        var variableScopeOption = new CommandLineOptionDescriptor("--variable", "--var", CommandLineOptionId.GetOrSetEnvironmentVariable, CommandLineOptionKind.ModeAndValue, "GetOrSet-Environment-Variable", ["Set or create an environment variable"], @"lit --variable ""PATH"" --value ""C:\Folder"" --machine --join", IsOptional: false);
        table.Add("--var", variableScopeOption);
        table.Add("--variable", variableScopeOption);

        var variableValueOption = new CommandLineOptionDescriptor("--value", "--val", CommandLineOptionId.EnvironmentVariableValue, CommandLineOptionKind.Value, string.Empty, ["Specify the new value of", "the environment variable"], @"lit --variable ""PATH"" --value ""C:\Folder"" --machine --join", IsOptional: false);
        table.Add("--value", variableValueOption);
        table.Add("--val", variableValueOption);

        var joinWriteModeOption = new CommandLineOptionDescriptor("--join", "-j", CommandLineOptionId.EnvironmentVariableWriteModeJoin, CommandLineOptionKind.Flag, string.Empty, ["Specify that the new value of", "the environment variable is appended", "to the existing value"], @"lit --variable ""PATH"" --value ""C:\Folder"" --machine --join", IsOptional: false);
        table.Add("-j", joinWriteModeOption);
        table.Add("--join", joinWriteModeOption);

        var replaceWriteModeOption = new CommandLineOptionDescriptor("--replace", "-r", CommandLineOptionId.EnvironmentVariableWriteModeReplace, CommandLineOptionKind.Flag, string.Empty, ["Specify that the new value of", "the environment variable replaces", "the existing value"], @"lit --variable ""TEMP"" --value ""C:\Folder"" --machine --replace", IsOptional: false);
        table.Add("-r", replaceWriteModeOption);
        table.Add("--replace", replaceWriteModeOption);

        var delimiterOption = new CommandLineOptionDescriptor("--delimiter", "--del", CommandLineOptionId.EnvironmentVariableWriteModeJoinDelimiter, CommandLineOptionKind.Value, string.Empty, [$"Specifies the delimiter used to join", "the values of the environment variable.", "The default is the path separator ';'"], @"lit --variable ""TEMP"" --value ""C:\Folder"" --machine --replace --delimiter "";""", IsOptional: true);
        table.Add("--del", delimiterOption);
        table.Add("--delimiter", delimiterOption);

        var foldPathOption = new CommandLineOptionDescriptor("--fold-path", "--fp", CommandLineOptionId.FoldPath, CommandLineOptionKind.Flag, string.Empty, ["Fold the supplied path by replacing", "the longest matching path prefix with", "an existing environment variable."], @"lit --var PATH --val ""I:\GitHubRepositories\WindowsTerminalLauncher\artifacts"" -u -j --fold-path", IsOptional: true);
        table.Add("--fp", foldPathOption);
        table.Add("--fold-path", foldPathOption);

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
            CommandParserResult commandResult = await CommandLineArgumentParser.CreateCommandAsync(commandArgs, ValidCommandOptionsTable, userConfigurationFilePath);
            if (commandResult.HasErrors)
            {
                string errorMessage = string.Join(Environment.NewLine, commandResult.ErrorMessages);
                CommandHandler.ShowError(errorMessage);
                return;
            }

            command = commandResult.Command;
        }
        catch (InvalidCommandArgumentException ex)
        {
            CommandHandler.ShowError(ex.Message);
            return;
        }
        
        var idBasedValidCommandOptionsTable = ValidCommandOptionsTable.ToImmutableDictionary(entry => entry.Value.OptionType, entry => entry.Value);
        ValidationResult validationResult = CommandValidator.ValidateCommandSyntax(command, idBasedValidCommandOptionsTable);
        if (validationResult.HasErrors)
        {
            string errorMessage = string.Join(Environment.NewLine, validationResult.ErrorMessages);
            CommandHandler.ShowError(errorMessage);
        }

        switch (command.Arguments.OptionsTable)
        {
            case var _ when command.HasMode:
                CommandExitMode exitMode = await CommandHandler.HandleMode(command, s_applicationSettings);
                if (exitMode is CommandExitMode.ShutdownRequired)
                {
                    Shutdown();
                }

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
    private const string VariableName_PATH = "PATH";

    public static ValidationResult ValidateCommandSyntax(CommandLineCommand command, ImmutableDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validCommandOptionsTable)
    {
        ArgumentNullException.ThrowIfNull(validCommandOptionsTable);

        CommandLineOption? modeOption = null;

        foreach (KeyValuePair<CommandLineOptionId, CommandLineOption> entry in command.Arguments.OptionsTable)
        {
            CommandLineOption option = entry.Value;
            switch (option.Descriptor.OptionType)
            {
                case CommandLineOptionId.GetOrSetConfigLocation:
                    return ValidateGetOrSetConfigLocationCommandSyntax(command, ref modeOption, option, validCommandOptionsTable);
                case CommandLineOptionId.GetOrSetEnvironmentVariable:
                    return ValidateGetOrSetEnvironmentVariableCommandSyntax(command, ref modeOption, option, validCommandOptionsTable);
                case CommandLineOptionId.LaunchWindowsTerminal:
                    break;
                case CommandLineOptionId.Help:
                    break;
                case CommandLineOptionId.Version:
                    break;
                case CommandLineOptionId.ListAliases:
                    break;
                case CommandLineOptionId.Undefined:
                case CommandLineOptionId.RunAsAdmin:
                case CommandLineOptionId.SourcePath:
                case CommandLineOptionId.DestinationPath:
                case CommandLineOptionId.EnvironmentVariableValue:
                case CommandLineOptionId.EnvironmentVariableScopeUser:
                case CommandLineOptionId.EnvironmentVariableScopeMachine:
                case CommandLineOptionId.EnvironmentVariableWriteModeJoin:
                case CommandLineOptionId.EnvironmentVariableWriteModeJoinDelimiter:
                case CommandLineOptionId.EnvironmentVariableWriteModeReplace:
                case CommandLineOptionId.Print:
                case CommandLineOptionId.FoldPath:
                    if (command.HasMode)
                    {
                        continue;
                    }

                    throw new InvalidCommandArgumentException("The command is invalid and misses a mode specifier. Use 'lit --help' to get a list of mode options.");
                default:
                    throw new NotImplementedException($"Command validation not implemented for '{Enum.GetName(option.Descriptor.OptionType)}'.");
            }
        }
    }

    private static ValidationResult ValidateGetOrSetEnvironmentVariableCommandSyntax(CommandLineCommand command, ref CommandLineOption? modeOption, CommandLineOption option, ImmutableDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validCommandOptionsTable)
    {
        if (!command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.GetOrSetEnvironmentVariable, out CommandLineOption variableNameOption))
        {
            throw new ArgumentException($"Invalid argument '{nameof(command)}'. Validator expected a '{command.Name}' command");
        }

        if (CreateErrorMessageIfModeAlreadySet(ref modeOption, out string errorMessage))
        {
            return new ValidationResult([errorMessage]);
        }

        modeOption = option;

        var invalidOptions = new HashSet<CommandLineOptionId>(command.Arguments.OptionsTable.Keys);
        _ = invalidOptions.Remove(CommandLineOptionId.GetOrSetEnvironmentVariable);

        List<string> errorMessages = [];
                
        if (command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableScopeMachine, out _))
        {
            _ = invalidOptions.Remove(CommandLineOptionId.EnvironmentVariableScopeMachine);
        }
        else if (command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableScopeUser, out _))
        {
            _ = invalidOptions.Remove(CommandLineOptionId.EnvironmentVariableScopeUser);
        }
        else
        {
            _ = validCommandOptionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableScopeMachine, out CommandLineOptionDescriptor machineScopeDescriptor);
            _ = validCommandOptionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableScopeUser, out CommandLineOptionDescriptor userScopeDescriptor);
            errorMessage = CreateInvalidCommandArgumentErrorMessageForArgumentMissing(command.Name, $"'{machineScopeDescriptor.Name} | {machineScopeDescriptor.AlternativeName}' or {userScopeDescriptor.Name} | {userScopeDescriptor.AlternativeName}'");
            errorMessages.Add(errorMessage);
        }

        bool isPrintOptionProvided = command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.Print, out _);
        if (isPrintOptionProvided)
        {
            _ = invalidOptions.Remove(CommandLineOptionId.Print);
            if (CreateErrorMessageIfGreaterThan(invalidOptions.Count, 0, "Show-Variable", out errorMessage))
            {
                errorMessages.Add(errorMessage);
                return new ValidationResult(errorMessages.ToImmutableArray());
            }

            return errorMessages.Any()
                ? new ValidationResult(errorMessages.ToImmutableArray())
                : ValidationResult.ValidResult;
        }

        if (command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableValue, out _))
        {
            _ = invalidOptions.Remove(CommandLineOptionId.EnvironmentVariableValue);
        }

        if (command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.FoldPath, out _))
        {
            _ = invalidOptions.Remove(CommandLineOptionId.FoldPath);
        }

        if (command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableWriteModeJoinDelimiter, out _))
        {
            if (variableNameOption.Value.Equals(VariableName_PATH, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"No custom delimiter for the '{VariableName_PATH}' environment variable allowed.";
                errorMessages.Add(errorMessage);
            }

            _ = invalidOptions.Remove(CommandLineOptionId.FoldPath);
        }

        if (command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableWriteModeJoin, out _))
        {
            _ = invalidOptions.Remove(CommandLineOptionId.EnvironmentVariableWriteModeJoin);
        }
        else if (command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableWriteModeReplace, out _))
        {
            _ = invalidOptions.Remove(CommandLineOptionId.EnvironmentVariableWriteModeReplace);
        }
        else
        {
            _ = validCommandOptionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableWriteModeJoin, out CommandLineOptionDescriptor machineScopeDescriptor);
            _ = validCommandOptionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableWriteModeReplace, out CommandLineOptionDescriptor userScopeDescriptor);
            errorMessage = CreateInvalidCommandArgumentErrorMessageForArgumentMissing(command.Name, $"'{machineScopeDescriptor.Name} | {machineScopeDescriptor.AlternativeName}' or {userScopeDescriptor.Name} | {userScopeDescriptor.AlternativeName}'");
            errorMessages.Add(errorMessage);
        }

        if (CreateErrorMessageIfGreaterThan(invalidOptions.Count, 0, command.Name, out errorMessage))
        {
            errorMessages.Add(errorMessage);
        }

        if (CreateErrorMessageIfProfileAliasFound(command, out errorMessage))
        {
            errorMessages.Add(errorMessage);
        }

        return errorMessages.Any()
            ? new ValidationResult(errorMessages.ToImmutableArray())
            : ValidationResult.ValidResult;
    }

    private static ValidationResult ValidateGetOrSetConfigLocationCommandSyntax(CommandLineCommand command, ref CommandLineOption? modeOption, CommandLineOption option, ImmutableDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validCommandOptionsTable)
    {
        if (!command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.GetOrSetConfigLocation, out _))
        {
            throw new ArgumentException($"Invalid argument '{nameof(command)}'. Validator expected a '{command.Name}' command");
        }

        if (CreateErrorMessageIfModeAlreadySet(ref modeOption, out string errorMessage))
        {
            return new ValidationResult([errorMessage]);
        }

        modeOption = option;

        var invalidOptions = new HashSet<CommandLineOptionId>(command.Arguments.OptionsTable.Keys);
        _ = invalidOptions.Remove(CommandLineOptionId.GetOrSetConfigLocation);

        List<string> errorMessages = [];

        bool isPrintOptionProvided = command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.Print, out _);
        if (isPrintOptionProvided)
        {
            _ = invalidOptions.Remove(CommandLineOptionId.Print);
            if (CreateErrorMessageIfGreaterThan(invalidOptions.Count, 0,command.Name, out errorMessage))
            {
                errorMessages.Add(errorMessage);
                return new ValidationResult(errorMessages.ToImmutableArray());
            }

            return errorMessages.Any()
                ? new ValidationResult(errorMessages.ToImmutableArray())
                : ValidationResult.ValidResult;
        }

        if (command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.SourcePath, out CommandLineOption sourcePathOption))
        {
            _ = invalidOptions.Remove(CommandLineOptionId.SourcePath);

            string sourcePath = sourcePathOption.Value;
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                errorMessage = $"Invalid command argument. A '{option.Descriptor.Name} | {option.Descriptor.AlternativeName}' option was provided but no path value.";
                errorMessages.Add(errorMessage);
            }

            if (!CommandHandlerHelpers.IsFilePath(sourcePath))
            {
                errorMessage = $"Invalid path argument. A source file path must provide the file name of the source.";
                errorMessages.Add(errorMessage);
            }

            if (!Path.HasExtension(sourcePath)
                || !(Path.GetExtension(sourcePath).Equals(".yaml", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(sourcePath).Equals(".yml", StringComparison.OrdinalIgnoreCase)))
            {
                errorMessage = $"Invalid path argument. A '{option.Descriptor.Name} | {option.Descriptor.AlternativeName}' option was provided but the file extension does not match '.yaml' or '.yml'.";
                errorMessages.Add(errorMessage);
            }
        }
        
        if (command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.DestinationPath, out CommandLineOption destinationPathOption))
        {
            _ = invalidOptions.Remove(CommandLineOptionId.DestinationPath);

            string destinationPath = destinationPathOption.Value;
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                errorMessage = $"Invalid command argument. A '{option.Descriptor.Name} | {option.Descriptor.AlternativeName}' option was provided but no path value.";
                errorMessages.Add(errorMessage);
            }

            // Only validate extension if the path is a file path.
            // Otherwise allow the destination to be a directory.
            if (CommandHandlerHelpers.IsFilePath(destinationPath)
                && (!Path.HasExtension(destinationPath)
                || !(Path.GetExtension(destinationPath).Equals(".yaml", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(destinationPath).Equals(".yml", StringComparison.OrdinalIgnoreCase))))
            {
                errorMessage = $"Invalid path argument. A '{option.Descriptor.Name} | {option.Descriptor.AlternativeName}' option was provided but the file extension does not match '.yaml' or '.yml'.";
                errorMessages.Add(errorMessage);
            }
        }

        if (CreateErrorMessageIfGreaterThan(invalidOptions.Count, 0, command.Name, out errorMessage))
        {
            errorMessages.Add(errorMessage);
        }

        if (CreateErrorMessageIfProfileAliasFound(command, out errorMessage))
        {
            errorMessages.Add(errorMessage);
        }

        return errorMessages.Any()
            ? new ValidationResult(errorMessages.ToImmutableArray())
            : ValidationResult.ValidResult;
    }

    private static string CreateInvalidCommandArgumentErrorMessageForArgumentMissing(string commandName, string missingOptionsString) => $"Invalid argument list. The required option {missingOptionsString} for the '{commandName}' command is missing. Use 'lit --help' to get the comamnd syntax.";
    
    private static bool CreateErrorMessageIfProfileAliasFound(CommandLineCommand command, out string errorMessage)
    {
        errorMessage = command.HasAlias
            ? $"Command '{command.Name}' is malformed. When selecting the mode '{command.CommandIdProviderOption.Descriptor.Name} | {command.CommandIdProviderOption.Descriptor.AlternativeName}' the command cann't specify a Windows Terminal profile alias. Use 'lit --help' to get the comamnd syntax."
            : string.Empty;

        return command.HasAlias;
    }

    private static bool CreateErrorMessageIfGreaterThan(int invalidOptionsCount, int threshold, string commandName, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (invalidOptionsCount > threshold)
        {
            errorMessage = $"Invalid command argument list for command '{commandName}': too many arguments. Use 'lit --help' to get the comamnd syntax.";
            return true;
        }

        return false;
    }

    private static bool CreateErrorMessageIfModeAlreadySet(ref readonly CommandLineOption? modeOption, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (modeOption is not null)
        {
            errorMessage = "Invalid command argument. A command can only have a single mode option. Use 'lit --help' to get the comamnd syntax.";
            return true;
        }

        return false;
    }
}
