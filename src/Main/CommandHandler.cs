namespace Main;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Security;
using System.Security.AccessControl;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.VisualBasic.FileIO;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using YamlDotNet.Core.Tokens;
using static System.Runtime.InteropServices.JavaScript.JSType;

internal static class CommandHandler
{
    private const int LineIndentation = 4;
    private const int Padding = 4;

    public static void LaunchTerminalWithAlias(CommandLineCommand command)
    {
        bool isElevatedExeutionRequested = command.Context.ExecutionMode is ExecutionMode.Admin;
        var startInfo = new ProcessStartInfo
        {
            FileName = "wt.exe",
            UseShellExecute = isElevatedExeutionRequested,
        };

        if (isElevatedExeutionRequested)
        {
            startInfo.Verb = "runas";
        }

        // Control destination terminal window
        string targetWindow = string.IsNullOrWhiteSpace(command.Context.LaunchMode)
            ? LaunchModes.NewWindow
            : command.Context.LaunchMode;
        startInfo.ArgumentList.Add("-w");
        startInfo.ArgumentList.Add(targetWindow);

        // Control terminal profile. If ommitted, wt.exe uses the default profile.
        string selectedTerminalProfile = command.Arguments.TerminalProfile.HasName
            ? command.Arguments.TerminalProfile.Name
            : command.Arguments.DefaultTerminalProfile.Name;
        bool isProfileSpecified = !string.IsNullOrWhiteSpace(selectedTerminalProfile);
        if (isProfileSpecified)
        {
            startInfo.ArgumentList.Add("-p");
            startInfo.ArgumentList.Add(command.Arguments.TerminalProfile.Name);
        }

        // Control working directory.
        // This will be the current directory of the Windows Explorer window
        // that launched this application from its address bar.
        startInfo.ArgumentList.Add("-d");
        startInfo.ArgumentList.Add(Environment.CurrentDirectory);

        using Process? process = Process.Start(startInfo);
    }

    public static void SetOrGetEnvirnomentVariable(CommandLineCommand command)
    {
        if (command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableName, out CommandLineOption variableNameOption)
            && (command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.EnvironmentVariableScopeMachine)
                && Enum.TryParse(CommandLineOptionId.EnvironmentVariableScopeMachine.ToDisplayString(), out EnvironmentVariableTarget environmentVariableTarget)
            || command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.EnvironmentVariableScopeUser)
                && Enum.TryParse(CommandLineOptionId.EnvironmentVariableScopeUser.ToDisplayString(), out environmentVariableTarget)))
        {
            string newValue = command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableValue, out CommandLineOption variableValueOption)
                ? variableValueOption.Value
                : Environment.CurrentDirectory;

            // We have to use the registry here because 'Environment.SetEnvironmentVariable' will resolve variables like "%Temp%\Folder".
            // But we don't want to erase such variabled lineIndex.e. folded paths. Using the registry manually allows us to preserve "%TEMP%".
            using RegistryKey? key = (environmentVariableTarget is EnvironmentVariableTarget.User
                ? Registry.CurrentUser.OpenSubKey(
                    "Environment",
                    RegistryKeyPermissionCheck.Default,
                    RegistryRights.CreateSubKey | RegistryRights.SetValue | RegistryRights.QueryValues)
                : Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment",
                    RegistryKeyPermissionCheck.Default,
                    RegistryRights.CreateSubKey | RegistryRights.SetValue | RegistryRights.QueryValues)) 
                ?? throw new InvalidOperationException("Environment registry key is missing.");

            string variableName = variableNameOption.Value;
            object? rawValue = key.GetValue(
                variableName,
                null,
                RegistryValueOptions.DoNotExpandEnvironmentNames);
            
            string currentValue = rawValue as string ?? string.Empty;            
            if (!string.IsNullOrWhiteSpace(currentValue)
                && command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.EnvironmentVariableWriteModeJoin))
            {
                string delimiter = command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableWriteModeJoinDelimiter, out CommandLineOption delimiterOption)
                    ? delimiterOption.Value
                    : Path.PathSeparator.ToString();
                newValue = string.Join(delimiter, currentValue, newValue);
            }
            
            RegistryValueKind kind = rawValue is null
                ? RegistryValueKind.String
                : key.GetValueKind(variableName);
            key.SetValue(variableName, newValue, kind);

            BroadcastEnvironmentChange();
        }
    }

    private static unsafe void BroadcastEnvironmentChange()
    {
        const string environment = "Environment";

        fixed (char* environmentPtr = environment)
        {
            _ = PInvoke.SendMessageTimeout(
                HWND.HWND_BROADCAST,
                PInvoke.WM_SETTINGCHANGE,
                0,
                (nint)environmentPtr,
                SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG,
                5000,
                null);
        }
    }

    public static async Task ShowHelpAsync(IReadOnlyDictionary<string, CommandLineOptionDescriptor> validOptionsTable, string userConfigurationFilePath)
    {
        ArgumentNullException.ThrowIfNull(validOptionsTable);

        Configuration configuration = await ConfigurationReader.ReadConfigurationAsync(userConfigurationFilePath);
        var helpMessageBuilder = new StringBuilder();
        CreateUsageMessage(helpMessageBuilder);
        helpMessageBuilder = CreateAliasesMessage(configuration, helpMessageBuilder);
        helpMessageBuilder = CreateOptionsMessage(validOptionsTable, helpMessageBuilder);

        ShowInfoDialog(helpMessageBuilder);
    }

    public static async Task ShowAliasesAsync(string userConfigurationFilePath)
    {
        Configuration configuration = await ConfigurationReader.ReadConfigurationAsync(userConfigurationFilePath);
        var helpMessageBuilder = new StringBuilder();
        helpMessageBuilder = CreateAliasesMessage(configuration, helpMessageBuilder);

        ShowInfoDialog(helpMessageBuilder);
    }

    public static void ShowOptions(IReadOnlyDictionary<string, CommandLineOptionDescriptor> validOptionsTable)
    {
        ArgumentNullException.ThrowIfNull(validOptionsTable);

        var helpMessageBuilder = new StringBuilder();
        helpMessageBuilder = CreateOptionsMessage(validOptionsTable, helpMessageBuilder);

        ShowInfoDialog(helpMessageBuilder);
    }

    private static StringBuilder CreateOptionsMessage(IReadOnlyDictionary<string, CommandLineOptionDescriptor> validOptionsTable, StringBuilder messageBuilder)
    {
        _ = messageBuilder.AppendLine()
            .AppendLine("Options:");

        IEnumerable<CommandLineOptionDescriptor> availableOptions = validOptionsTable.Values.Distinct();
        string optionsSeparator = ", ";
        int optionsSeparatorLength = optionsSeparator.Length;
        int maxOptionLength = availableOptions.Max(descriptor => descriptor.Name.Length + (descriptor.HasAlternativeName ? descriptor.AlternativeName.Length + optionsSeparatorLength : 0)) + Padding;
        int maxKindLength = availableOptions.Max(descriptor => descriptor.Kind.ToDisplayString().Length) + Padding;
        int maxDescriptionLength = availableOptions
            .SelectMany(descriptor => descriptor.DescriptionLines)
            .Max(descriptioLine => descriptioLine.Length);

        string nameColumnName = "Option";
        string kindColumnName = "Option Type";
        string descriptionColumnName = "DescriptionLines";
        int cell2Padding = maxOptionLength - nameColumnName.Length;
        int cell3Padding = maxKindLength - kindColumnName.Length;
        _ = messageBuilder.Append(' ', LineIndentation)
            .Append(nameColumnName)
            .Append(' ', cell2Padding)
            .Append(kindColumnName)
            .Append(' ', cell3Padding)
            .AppendLine(descriptionColumnName)
            .Append(' ', LineIndentation)
            .Append('-', maxOptionLength + maxKindLength + maxDescriptionLength)
            .AppendLine();

        foreach (CommandLineOptionDescriptor descriptor in availableOptions.OrderBy(o => o.Name))
        {
            cell2Padding = maxOptionLength - (descriptor.Name.Length + (descriptor.HasAlternativeName ? descriptor.AlternativeName.Length + optionsSeparatorLength : 0));
            cell3Padding = maxKindLength - descriptor.Kind.ToDisplayString().Length;
            _ = messageBuilder.Append(' ', LineIndentation);
            if (descriptor.HasAlternativeName)
            { 
                _ = messageBuilder.AppendJoin(optionsSeparator, descriptor.Name, descriptor.AlternativeName);
            }
            else
            {
                _ = messageBuilder.Append(descriptor.Name);
            }

            _ = messageBuilder.Append(' ', cell2Padding)
                .Append(descriptor.Kind.ToDisplayString())
                .Append(' ', cell3Padding);

            for (int lineIndex = 0; lineIndex < descriptor.DescriptionLines.Length; lineIndex++)
            {
                string descriptionLine = descriptor.DescriptionLines[lineIndex];
                _ = messageBuilder.AppendLine(descriptionLine);

                if (lineIndex + 1 < descriptor.DescriptionLines.Length)
                {
                    _ = messageBuilder.Append(' ', LineIndentation + maxOptionLength + maxKindLength);
                }
            }
        }

        return messageBuilder;
    }

    private static StringBuilder CreateAliasesMessage(Configuration configuration, StringBuilder messageBuilder)
    {
        _ = messageBuilder.AppendLine("Aliases:")
            .Append(' ', LineIndentation)
            .AppendLine("Note: If no alias is provided, the default alias as specified in the")
            .Append(' ', LineIndentation)
            .AppendLine("user configuration YAML file will be used.")
            .Append(' ', LineIndentation)
            .AppendLine("If no such default alias was specified, the default profile of")
            .Append(' ', LineIndentation)
            .AppendLine("the Windows Terminal will be used.")
            .Append(' ', LineIndentation)
            .AppendLine("Furthermore, if the options of type 'Mode' e.g., '--config' or '--variable'")
            .Append(' ', LineIndentation)
            .AppendLine("are specified, the alias argument will be ignored.")
            .AppendLine();

        int maxAliasLength = configuration.TerminalProfiles.Max(profile => profile.Alias.Length);
        foreach (TerminalProfile terminalProfile in configuration.TerminalProfiles.OrderBy(profile => profile.Alias))
        {
            int padding = maxAliasLength - terminalProfile.Alias.Length + Padding;
            _ = messageBuilder.Append(' ', LineIndentation)
                .Append(terminalProfile.Alias)
                .Append(' ', padding)
                .Append(terminalProfile.Name);

            if (terminalProfile.IsDefault)
            {
                _ = messageBuilder.Append(" (default)");
            }

            _ = messageBuilder.AppendLine();
        }

        return messageBuilder.AppendLine()
            .Append(' ', LineIndentation)
            .AppendLine(@">> Note: Edit the user configuration file to manage aliases.")
            .Append(' ', LineIndentation)
            .AppendLine(@"   Use 'lit -c -p' to show the current location.");
    }

    private static void CreateUsageMessage(StringBuilder messageBuilder)
    {
        _ = messageBuilder.AppendLine("Usage:")
            .Append(' ', LineIndentation)
            .AppendLine("Launch Windows Terminal at the current working directory (explorer older):")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine("lit [<alias>] [<options>...]")
            .AppendLine()
            .Append(' ', LineIndentation)
            .AppendLine("Show the current location of the user configuration file:")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine("lit (-c | --config) (-p | --print)")
            .AppendLine()
            .Append(' ', LineIndentation)
            .AppendLine("Set the new location of the user configuration file:")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine("lit (-c | --config)")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine("[(-s | --source) <source-path>]")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine("[(-d | --destination) <destination-path>]")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine(">> Note: If '-s' is not provided the current location")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine("         of the config will be used.")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine("         If '-d' is not provided the current explorer location")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine("         (lineIndex.e. working directory) will be used.")
            .AppendLine()
            .Append(' ', LineIndentation)
            .AppendLine("Set a specified environment variable:")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine("lit (--var | --variable)")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine("[(--val | --value) <value>]")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine("((-j | --join) | (-r | --replace) [--del | --delimiter]")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine("(-m | --machine) | (-u | --user)")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine(">> Note: If '--val' is not provided the current working directory")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine("         will be used as the new value of the envirnoment variable.")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine("         If the variable doesn't exist it will create it.")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine("         If '--del' is not provided then the default path limiter")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine("         ';' will be used.")
            .AppendLine();
    }

    public static void ShowError(string message) => ShowErrorDialog(message ?? "An unknown error occurred.");

    private static void ShowInfoDialog(StringBuilder messageBuilder, string title = "lit.exe Help", string header = "lit.exe Command Line Help")
    {
        messageBuilder = new StringBuilder()
            .AppendLine("This application is a command line utility for launching a Windows Terminal tab or")
            .AppendLine("instance from the Windows Explorer's address bar with a specified Windows Terminal")
            .AppendLine("profile. The terminal's working directory is set to the path of the currently")
            .AppendLine("navigated Windows Explorer folder.")
            .AppendLine()
            .AppendLine("For more information, please visit the GitHub repository:")
            .AppendLine("https://github.com/BionicCode/windows-terminal-launcher")
            .AppendLine()
            .Append(messageBuilder);
        var dialog = new InfoDialog
        {
            Title = title,
            Header = header,
            Body = messageBuilder.ToString(),
            Icon = Imaging.CreateBitmapSourceFromHIcon(
                SystemIcons.Information.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions())
        };

        dialog.Show();
    }

    private static void ShowErrorDialog(string message, string title = "lit.exe Error", string header = "lit.exe Invalid Command Argument")
    {
        var dialog = new InfoDialog
        {
            Title = title,
            Header = header,
            Body = message.ToString(),
            Icon = Imaging.CreateBitmapSourceFromHIcon(
                SystemIcons.Error.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions())
        };

        dialog.Show();
    }

    internal static async Task HandleMode(CommandLineCommand command, IApplicationSettings applicationSettings)
    {
        ArgumentNullException.ThrowIfNull(applicationSettings);

        switch (command.Mode)
        {
            case CommandLineOptionId.GetOrSetConfigLocation:
                _ = applicationSettings.TryGet(
                    AppSettingsKeys.UserConfigFileLocationKey,
                    out string currentConfigFilePath);

                if (command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.Print))
                {
                    string message = string.IsNullOrWhiteSpace(currentConfigFilePath)
                        ? "No location set. Please set a location first. See '--help' or '-h'."
                        : currentConfigFilePath;

                    var dialog = new InfoDialog
                    {
                        Title = "lit.exe user configuration file location",
                        Header = "The user configuration YAML file is located at:",
                        Body = message,
                        Icon = Imaging.CreateBitmapSourceFromHIcon(
                            SystemIcons.Information.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions())
                    };

                    dialog.Show();
                }
                else
                {
                    _ = TryGetpath(CommandLineOptionId.SourcePath, command, out string sourcePath, currentConfigFilePath);

                    string fallbackFileName = CommandHandlerHelpers.GetFileNameIfFile(sourcePath);
                    _ = TryGetpath(CommandLineOptionId.DestinationPath, command, out string destinationPath, Environment.CurrentDirectory,  fallbackFileName);
                    if (sourcePath.Equals(destinationPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    if (File.Exists(destinationPath))
                    {
                        var dialog = new InteractionDialog
                        {
                            Title = "File exists",
                            Header = "File Exists:",
                            Body = $"The file '{destinationPath}'{Environment.NewLine}already exists. Overwrite the existing file?",
                            Icon = Imaging.CreateBitmapSourceFromHIcon(
                                SystemIcons.Warning.Handle,
                                Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions())
                        };

                        bool? dialogResult = dialog.ShowDialog();
                        if (dialogResult == false)
                        {
                            return;
                        }
                    }

                    SetConfigLocationAsync(sourcePath, destinationPath);
                    applicationSettings.AddOrUpdate(AppSettingsKeys.UserConfigFileLocationKey, destinationPath);
                }

                break;
            case CommandLineOptionId.SetEnvironmentVariable:
                SetOrGetEnvirnomentVariable(command);
                break;
            default:
                throw new NotImplementedException($"The mode '{Enum.GetName(command.Mode)} is currently not supported.");
        }
    }

    private static bool TryGetpath(
        CommandLineOptionId pathId, 
        CommandLineCommand command, 
        [NotNullWhen(true)] out string path, 
        string? fallbackPath = null, 
        string? fileName = null)
    {
        if (pathId is not CommandLineOptionId.SourcePath and not CommandLineOptionId.DestinationPath)
        {
            throw new ArgumentException($"Provided option ID '{Enum.GetName(pathId)}' is n ot a path ID.");
        }

        path = string.Empty;
        if (command.Arguments.OptionsTable.TryGetValue(pathId, out CommandLineOption option))
        {
            path = option.Value;
        }
        else if (!string.IsNullOrWhiteSpace(fallbackPath))
        {
            path = fallbackPath;
        }

        if (string.IsNullOrWhiteSpace(path)
            && !CommandHandlerHelpers.IsFilePath(path)
            && !string.IsNullOrWhiteSpace(fileName))
        {
            path = Path.Combine(path, fileName);
        }

        return !string.IsNullOrWhiteSpace(path);
    }

    private static void SetConfigLocationAsync(string sourcePath, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        try
        {
            File.Copy(sourcePath, destinationPath, true);
        }
        catch (Exception ex) when (ex
            is UnauthorizedAccessException
            or PathTooLongException
            or SecurityException
            or IOException
            or DirectoryNotFoundException)
        {
            var dialog = new InfoDialog
            {
                Title = "lit.exe Error",
                Header = "The copy operation failed:",
                Body = ex.Message,
                Icon = Imaging.CreateBitmapSourceFromHIcon(
                    SystemIcons.Information.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions())
            };

            dialog.Show();
            return;
        }
    }
}
