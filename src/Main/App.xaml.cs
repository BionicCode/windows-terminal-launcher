namespace Main;

using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Configuration;
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

        var aliasOption = new CommandLineOptionDescriptor(
            "<terminal-profile-alias>", 
            string.Empty, 
            CommandLineOptionId.LaunchWindowsTerminal, 
            CommandLineOptionKind.ModeAndValue, 
            "Launch-Windows-Terminal", 
            [
                "Launches the Windows Terminal", 
                "with a provided working directory."], 
            "lit ps", 
            IsOptional: false);
        table.Add("<terminal-profile-alias>", aliasOption);

        var helpOption = new CommandLineOptionDescriptor(
            "--help", 
            "-h", 
            CommandLineOptionId.Help, 
            CommandLineOptionKind.Mode, 
            "Show-Help", 
            ["Show help e.g. list options and aliases"], 
            "lit --help", 
            IsOptional: true);
        table.Add("-h", helpOption);
        table.Add("--help", helpOption);

        var versionOption = new CommandLineOptionDescriptor(
            "--version", 
            "-v", 
            CommandLineOptionId.Version,
            CommandLineOptionKind.Mode, 
            "Show-Version", 
            ["Show tool version"], 
            "lit --version", 
            IsOptional: true);
        table.Add("-v", versionOption);
        table.Add("--version", versionOption);

        var listAliasesOption = new CommandLineOptionDescriptor(
            "--list", 
            "-l", CommandLineOptionId.ListAliases, 
            CommandLineOptionKind.Flag, 
            "List-Profile-Alises", 
            ["List registered Windows Terminal", 
                "profiles and aliases"], 
            "lit --list",
            IsOptional: true);
        table.Add("-l", listAliasesOption);
        table.Add("--list", listAliasesOption);

        var runAsAdminOption = new CommandLineOptionDescriptor(
            "--admin", 
            "-a", 
            CommandLineOptionId.RunAsAdmin, 
            CommandLineOptionKind.Flag, 
            string.Empty, 
            ["Run Windows Terminal elevated"], 
            "lit ps --admin", 
            IsOptional: true);
        table.Add("-a", runAsAdminOption);
        table.Add("--admin", runAsAdminOption);

        var setConfigLocationOption = new CommandLineOptionDescriptor(
            "--config", 
            "-c", 
            CommandLineOptionId.GetOrSetConfigLocation,
            CommandLineOptionKind.Mode,
            "GetOrSet-Config-Location", 
            ["Set new or get the location of the", 
                "current configuration file"], 
            @"lit --config --destination ""%USERPROFILE%/.lit""", 
            IsOptional: true);
        table.Add("-c", setConfigLocationOption);
        table.Add("--config", setConfigLocationOption);

        var sourceLocationOption = new CommandLineOptionDescriptor(
            "--source", 
            "-s", 
            CommandLineOptionId.SourcePath, 
            CommandLineOptionKind.Value, 
            string.Empty, 
            ["Specify the source path"], 
            @"lit --config --source ""%TEMP%/config.yaml"" --destination ""%USERPROFILE%/.lit""",
            IsOptional: true);
        table.Add("-s", sourceLocationOption);
        table.Add("--source", sourceLocationOption);

        var destinationLocationOption = new CommandLineOptionDescriptor(
            "--destination", 
            "-d", 
            CommandLineOptionId.DestinationPath, 
            CommandLineOptionKind.Value, 
            string.Empty, 
            ["Specify the destination path"], 
            @"lit --config --destination ""%USERPROFILE%/.lit""",
            IsOptional: true);
        table.Add("-d", destinationLocationOption);
        table.Add("--destination", destinationLocationOption);

        var workingDirectoryOption = new CommandLineOptionDescriptor(
            "--working-directory", 
            "-w", 
            CommandLineOptionId.WorkingDirectory, 
            CommandLineOptionKind.Value, 
            string.Empty, 
            ["Specify the working directory", 
                "of the Windows Terminal instance"], 
            @"lit --working-directory ""%USERPROFILE%/.lit""", 
            IsOptional: true);
        table.Add("-w", workingDirectoryOption);
        table.Add("--working-directory", workingDirectoryOption);

        var printOption = new CommandLineOptionDescriptor(
            "--print", 
            "-p", 
            CommandLineOptionId.Print, 
            CommandLineOptionKind.Flag, 
            string.Empty, 
            ["Print the specified workingDirectoryPath"],
            @"lit --config --print",
            IsOptional: false);
        table.Add("-p", printOption);
        table.Add("--print", printOption);

        var userScopeOption = new CommandLineOptionDescriptor(
            "--user", 
            "-u", 
            CommandLineOptionId.EnvironmentVariableScopeUser,
            CommandLineOptionKind.Flag, 
            string.Empty, 
            ["Specify the scope of the",
                "environment variable as 'user'"], 
            @"lit --variable ""PATH"" --workingDirectoryPath ""C:\Folder"" --user --join", 
            IsOptional: false);
        table.Add("-u", userScopeOption);
        table.Add("--user", userScopeOption);

        var systemScopeOption = new CommandLineOptionDescriptor(
            "--machine", 
            "-m", 
            CommandLineOptionId.EnvironmentVariableScopeMachine,
            CommandLineOptionKind.Flag, 
            string.Empty, 
            ["Specify the scope of the",
                "environment variable as 'system'"], 
            @"lit --variable ""PATH"" --workingDirectoryPath ""C:\Folder"" --machine --join",
            IsOptional: false);
        table.Add("-m", systemScopeOption);
        table.Add("--machine", systemScopeOption);

        var variableScopeOption = new CommandLineOptionDescriptor(
            "--variable", 
            "--var", 
            CommandLineOptionId.GetOrSetEnvironmentVariable,
            CommandLineOptionKind.ModeAndValue, 
            "GetOrSet-Environment-Variable", 
            ["Set or create an environment variable"], 
            @"lit --variable ""PATH"" --workingDirectoryPath ""C:\Folder"" --machine --join",
            IsOptional: false);
        table.Add("--var", variableScopeOption);
        table.Add("--variable", variableScopeOption);

        var variableValueOption = new CommandLineOptionDescriptor(
            "--workingDirectoryPath", 
            "--val", 
            CommandLineOptionId.EnvironmentVariableValue,
            CommandLineOptionKind.Value, 
            string.Empty, 
            ["Specify the new workingDirectoryPath of", 
                "the environment variable"], 
            @"lit --variable ""PATH"" --workingDirectoryPath ""C:\Folder"" --machine --join",
            IsOptional: false);
        table.Add("--workingDirectoryPath", variableValueOption);
        table.Add("--val", variableValueOption);

        var joinWriteModeOption = new CommandLineOptionDescriptor(
            "--join", 
            "-j", 
            CommandLineOptionId.EnvironmentVariableWriteModeJoin, 
            CommandLineOptionKind.Flag, 
            string.Empty, 
            ["Specify that the new workingDirectoryPath of", 
                "the environment variable is appended",
                "to the existing workingDirectoryPath"], 
            @"lit --variable ""PATH"" --workingDirectoryPath ""C:\Folder"" --machine --join",
            IsOptional: false);
        table.Add("-j", joinWriteModeOption);
        table.Add("--join", joinWriteModeOption);

        var replaceWriteModeOption = new CommandLineOptionDescriptor(
            "--replace", 
            "-r", 
            CommandLineOptionId.EnvironmentVariableWriteModeReplace, 
            CommandLineOptionKind.Flag, 
            string.Empty, 
            [
                "Specify that the new workingDirectoryPath of", 
                "the environment variable replaces", 
                "the existing workingDirectoryPath"], 
            @"lit --variable ""TEMP"" --workingDirectoryPath ""C:\Folder"" --machine --replace", 
            IsOptional: false);
        table.Add("-r", replaceWriteModeOption);
        table.Add("--replace", replaceWriteModeOption);

        var delimiterOption = new CommandLineOptionDescriptor(
            "--delimiter", 
            "--del", 
            CommandLineOptionId.EnvironmentVariableWriteModeJoinDelimiter, 
            CommandLineOptionKind.Value, 
            string.Empty, 
            [
                $"Specifies the delimiter used to join", 
                "the values of the environment variable.", 
                "The default is the path separator ';'"], 
            @"lit --variable ""TEMP"" --workingDirectoryPath ""C:\Folder"" --machine --replace --delimiter "";""", 
            IsOptional: true);
        table.Add("--del", delimiterOption);
        table.Add("--delimiter", delimiterOption);

        var foldPathOption = new CommandLineOptionDescriptor(
            "--fold-path", 
            "--fp", 
            CommandLineOptionId.FoldPath, 
            CommandLineOptionKind.Flag, 
            string.Empty, 
            [
                "Fold the supplied path by replacing", 
                "the longest matching path prefix with", 
                "an existing environment variable.", 
                "For the user environment,", 
                "system and user variables", 
                "are used to find a match.", 
                "For system environment,", 
                "only system variables are considered." ], 
            @"lit --var PATH --val ""I:\GitHubRepositories\WindowsTerminalLauncher\artifacts"" -u -j --fold-path", 
            IsOptional: true);
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
        UserConfiguration userConfiguration = await ConfigurationReader.ReadConfigurationAsync(userConfigurationFilePath);

        string[] commandArgs = e?.Args ?? [];
        CommandLineCommand command;
        try
        {
            CommandParserResult commandResult = CommandLineArgumentParser.CreateCommand(commandArgs, ValidCommandOptionsTable, userConfiguration);
            if (commandResult.HasErrors)
            {
                string errorMessage = string.Join(Environment.NewLine, commandResult.ErrorMessages);
                CommandHelpers.ShowErrorDialog(errorMessage);
                return;
            }

            command = commandResult.Command;
        }
        catch (InvalidCommandArgumentException ex)
        {
            CommandHelpers.ShowErrorDialog(ex.Message);
            return;
        }
        
        var idBasedValidCommandOptionsTable = ValidCommandOptionsTable.ToImmutableDictionary(entry => entry.Value.OptionType, entry => entry.Value);
        ValidationResult validationResult = CommandValidator.ValidateCommandSyntax(command, idBasedValidCommandOptionsTable);
        if (validationResult.HasErrors)
        {
            string errorMessage = string.Join(Environment.NewLine, validationResult.ErrorMessages);
            CommandHelpers.ShowErrorDialog(errorMessage, header: "Invalid Command Syntax");
            
            return;
        }

        // Only call for compatiobility. This is essentially a no-op operation
        base.OnStartup(e);

        CommandExitMode exitMode = CommandExitMode.Undefined;
        switch (command.Mode)
        {
            case CommandLineOptionId.LaunchWindowsTerminal:
                var launchTerminalAction = new LaunchWindowsTerminalAction();
                exitMode = launchTerminalAction.Execute(command, s_applicationSettings, userConfiguration, idBasedValidCommandOptionsTable);
                break;
            case CommandLineOptionId.GetOrSetConfigLocation:
                var getOrSetUserConfigLocationAction = new GetOrSetUserConfigLocationAction();
                exitMode = getOrSetUserConfigLocationAction.Execute(command, s_applicationSettings, userConfiguration, idBasedValidCommandOptionsTable);
                break;
            case CommandLineOptionId.GetOrSetEnvironmentVariable:
                var getOrSetEnvironmentVariable = new GetOrSetEnvironmentVariableAction();
                exitMode = getOrSetEnvironmentVariable.Execute(command, s_applicationSettings, userConfiguration, idBasedValidCommandOptionsTable);
                break;           
            case CommandLineOptionId.Help:
            case CommandLineOptionId.ListAliases:
                var commandMetaInfoHandler = new ShowHelpAction();
                exitMode = commandMetaInfoHandler.Execute(command, s_applicationSettings, userConfiguration, idBasedValidCommandOptionsTable);
                break;
            default:
                throw new NotImplementedException($"The mode '{Enum.GetName(command.Mode)} is currently not supported.");
        }

        if (exitMode is CommandExitMode.ShutdownRequired)
        {
            Shutdown();
        }
    }
}
