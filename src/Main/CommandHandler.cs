namespace Main;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Security;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.VisualBasic.FileIO;
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

    public static void ExecutePowerShellScript(CommandLineCommand command, bool isElevated)
    {
        string directory = Environment.CurrentDirectory;

        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh.exe",
            UseShellExecute = true,
            Verb = "runas"
        };

        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(
            $"[Environment]::SetEnvironmentVariable('Path'," +
            $"[Environment]::GetEnvironmentVariable('Path','Machine') + ';{directory.Replace("'", "''")}'," +
            $"'Machine')");

        Process.Start(startInfo);
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
        int maxOptionLength = availableOptions.Max(option => option.Name.Length + option.AlternativeName.Length) + optionsSeparatorLength;
        int maxKindLength = availableOptions.Max(option => option.Kind.ToDisplayString().Length);
        int maxDescriptionLength = availableOptions.Max(option => option.Description.Length);

        string nameColumnName = "Option";
        string kindColumnName = "Option Type";
        string descriptionColumnName = "Description";
        int cell2Padding = maxOptionLength - nameColumnName.Length + Padding;
        int cell3Padding = maxKindLength - kindColumnName.Length + Padding;
        _ = messageBuilder.Append(' ', LineIndentation)
            .Append(nameColumnName)
            .Append(' ', cell2Padding)
            .Append(kindColumnName)
            .Append(' ', cell3Padding)
            .AppendLine(descriptionColumnName)
            .Append(' ', LineIndentation)
            .Append('-', maxOptionLength + maxKindLength + maxDescriptionLength + (2 * Padding))
            .AppendLine();

        foreach (CommandLineOptionDescriptor option in availableOptions.OrderBy(o => o.Name))
        {
            cell2Padding = maxOptionLength - (option.Name.Length + option.AlternativeName.Length + optionsSeparatorLength) + Padding;
            cell3Padding = maxKindLength - option.Kind.ToDisplayString().Length + Padding;
            _ = messageBuilder.Append(' ', LineIndentation)
                .AppendJoin(optionsSeparator, option.Name, option.AlternativeName)
                .Append(' ', cell2Padding)
                .Append(option.Kind.ToDisplayString())
                .Append(' ', cell3Padding)
                .AppendLine(option.Description);
        }

        return messageBuilder;
    }

    private static StringBuilder CreateAliasesMessage(Configuration configuration, StringBuilder messageBuilder)
    {
        _ = messageBuilder.AppendLine("Aliases:")
            .Append(' ', LineIndentation)
            .AppendLine("Note: If no alias is provided, the default alias")
            .Append(' ', LineIndentation)
            .AppendLine("as specified in the YAML configuration file will be used.")
            .Append(' ', LineIndentation)
            .AppendLine("If no such default alias was specified, the default")
            .Append(' ', LineIndentation)
            .AppendLine("profile of the Windows Terminal will be used.")
            .Append(' ', LineIndentation)
            .AppendLine("Furthermore, if the option '-c' or '--config' is specified,")
            .Append(' ', LineIndentation)
            .AppendLine("the alias argument will be ignored.")
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
            // TODO::Make path point to configured location!!!
            .AppendLine(@">> Edit the user configuration file to manage aliases.")
            .Append(' ', LineIndentation)
            .AppendLine(@"   Use 'lit -c -p' to get the location.");
    }

    private static void CreateUsageMessage(StringBuilder messageBuilder)
    {
        _ = messageBuilder.AppendLine("Usage:")
            .Append(' ', LineIndentation)
            .AppendLine(@"lit [<alias>] [<options>...]")
            .Append(' ', LineIndentation)
            .AppendLine(@"lit (-c | --config) (-p | --print)")
            .Append(' ', LineIndentation)
            .AppendLine(@"lit (-c | --config)")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine(@"[(-s | --source) <source-path>]")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine(@"[-d | --destination) <destination-path>]")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine("==> Note: If '-s' is not provided the current file location will be used.")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine("          If '-d' is not provided the current explorer location")
            .Append(' ', LineIndentation)
            .Append(' ', LineIndentation)
            .AppendLine("          (i.e. working directory) will be used.");
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
                    string sourcePath = command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.SourcePath, out CommandLineOption option)
                        ? option.Value
                        : currentConfigFilePath;

                    string destinationPath;
                    string destinationFileName;
                    if (command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.DestinationPath, out option))
                    {
                        destinationPath = option.Value;
                    }
                    else
                    {
                        destinationPath = Environment.CurrentDirectory;
                    }

                    if (!CommandHandlerHelpers.IsFilePath(destinationPath))
                    {
                        string sourceFileName = Path.GetFileName(sourcePath);
                        destinationPath = Path.Combine(destinationPath, sourceFileName);
                    }

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
            default:
                throw new NotImplementedException($"The mode '{Enum.GetName(command.Mode)} is currently not supported.");
        }
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
