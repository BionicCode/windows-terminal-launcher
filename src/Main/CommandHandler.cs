namespace Main;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
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
        bool isProfileSpecified = !string.IsNullOrWhiteSpace(command.Arguments.Alias.ResolvedName);
        if (isProfileSpecified)
        {
            startInfo.ArgumentList.Add("-p");
            startInfo.ArgumentList.Add(command.Arguments.Alias.ResolvedName);
        }

        // Control working directory.
        // This will be the current directory of the Windows Explorer window
        // that launched this application from its address bar.
        startInfo.ArgumentList.Add("-d");
        startInfo.ArgumentList.Add(Environment.CurrentDirectory);

        using Process? process = Process.Start(startInfo);
    }

    public static async Task ShowHelpAsync(IReadOnlyDictionary<string, CommandLineOptionDescriptor> validOptionsTable)
    {
        ArgumentNullException.ThrowIfNull(validOptionsTable);

        Configuration configuration = await ConfigurationReader.ReadConfigurationAsync();
        var helpMessageBuilder = new StringBuilder();
        helpMessageBuilder = CreateAliasesMessage(configuration, helpMessageBuilder);
        helpMessageBuilder = CreateOptionsMessage(validOptionsTable, helpMessageBuilder);

        ShowInfoDialog(helpMessageBuilder);
    }

    public static async Task ShowAliasesAsync()
    {
        Configuration configuration = await ConfigurationReader.ReadConfigurationAsync();
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
        int maxOptionLength = availableOptions.Max(option => option.Name.Length + option.AlternativeName.Length + optionsSeparatorLength);
        foreach (CommandLineOptionDescriptor option in availableOptions.OrderBy(o => o.Name))
        {
            int padding = maxOptionLength - (option.Name.Length + option.AlternativeName.Length + optionsSeparatorLength) + Padding;
            _ = messageBuilder.Append(' ', LineIndentation)
                .AppendJoin(optionsSeparator, option.Name, option.AlternativeName)
                .Append(' ', padding)
                .AppendLine(option.Description);
        }

        return messageBuilder;
    }

    private static StringBuilder CreateAliasesMessage(Configuration configuration, StringBuilder messageBuilder)
    {
        _ = messageBuilder.AppendLine("Usage:")
            .Append(' ', LineIndentation)
            .AppendLine(@"lt [alias] [options...] [""<location>""]")
            .AppendLine()
            .AppendLine("Aliases:");

        _ = messageBuilder.Append(' ', LineIndentation)
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

        int maxAliasLength = configuration.Aliases.Max(alias => alias.Name.Length);
        foreach (Alias alias in configuration.Aliases.OrderBy(a => a.Name))
        {
            int padding = maxAliasLength - alias.Name.Length + Padding;
            _ = messageBuilder.Append(' ', LineIndentation)
                .Append(alias.Name)
                .Append(' ', padding)
                .AppendLine(alias.ResolvedName);
        }

        return messageBuilder.AppendLine()
            .Append(' ', LineIndentation)
            .AppendLine(@">> Edit the ""Config/config.yaml"" file to manage aliases.");
    }

    public static void ShowError(string message) => ShowErrorDialog(message ?? "An unknown error occurred.");

    private static void ShowInfoDialog(StringBuilder messageBuilder, string title = "lit.exe Help", string header = "lit.exe Command Line Help")
    {
        messageBuilder = new StringBuilder()
            .AppendLine("This application is a command line utility for launching a Windows Terminal")
            .AppendLine("tab or instance from the Windows Explorer's address bar with a specified")
            .AppendLine("Windows Terminal profile. The terminal's working directory is set to the")
            .AppendLine("path of the currently navigated Windows Explorer folder.")
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

    internal static async Task HandleMode(CommandLineCommand command)
    {
        CommandLineOptionId commandMode = command.Arguments.OptionsTable
            .Single(option => option.Descriptor.Kind is CommandLineOptionKind.Mode)
            .Descriptor.OptionType;
        switch (commandMode)
        {
            case CommandLineOptionId.SetConfigLocation:
                SetConfigLocationAsync(command.Arguments.OptionsTable);
                break;
            default:
                break;
        }
    }

    private static void SetConfigLocationAsync(ImmutableHashSet<CommandLineOption> options)
    {
        throw new NotImplementedException();
    }
}
