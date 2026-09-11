namespace Main;

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using YamlDotNet.Core.Tokens;
using static System.Runtime.InteropServices.JavaScript.JSType;

internal static class CommandHandler
{
    public static FrozenDictionary<string, CommandLineOption> ValidCommandOptionsTable { get; } =  new KeyValuePair<string, CommandLineOption>[] 
    {
        new KeyValuePair<string, CommandLineOption>("-h", new CommandLineOption("-h", CommandLineOptionType.Help, "Show help e.g. list options and aliases", IsOptional: true)),
        new KeyValuePair<string, CommandLineOption>("--help", new CommandLineOption("--help", CommandLineOptionType.Help, "Show help e.g. show syntax and list options and registered aliases", IsOptional: true)),
        new KeyValuePair<string, CommandLineOption>("-isProfileSpecified", new CommandLineOption("-isProfileSpecified", CommandLineOptionType.Version, "Show tool version", IsOptional: true)),
        new KeyValuePair<string, CommandLineOption>("--version", new CommandLineOption("--version", CommandLineOptionType.Version, "Show tool version", IsOptional: true)),
        new KeyValuePair<string, CommandLineOption>("-l", new CommandLineOption("-l", CommandLineOptionType.ListAliases, "List registered aliases", IsOptional: true)),
        new KeyValuePair<string, CommandLineOption>("--list", new CommandLineOption("--list", CommandLineOptionType.ListAliases, "List registered aliases", IsOptional: true)),
        new KeyValuePair<string, CommandLineOption>("-a", new CommandLineOption("-a", CommandLineOptionType.RunAsAdmin, "Run as administrator", IsOptional: true)),
        new KeyValuePair<string, CommandLineOption>("--admin", new CommandLineOption("--admin", CommandLineOptionType.RunAsAdmin, "Run as administrator", IsOptional: true)),
    }.ToFrozenDictionary();

    public static FrozenDictionary<CommandLineOptionType, CommandLineOption> ValidCommandOptionTypesTable { get; } = ValidCommandOptionsTable.ToFrozenDictionary(kvp => kvp.Value.OptionType, kvp => kvp.Value);

    public static async Task ExecuteCommand(StartupEventArgs startupArgumnets)
    {
        CommandLineCommand command = await CommandLineArgumentParser.CreateCommandAsync(startupArgumnets.Args, ValidCommandOptionsTable);
        switch (command.Options)
        {
            case var options when options.Any(option => option.OptionType == CommandLineOptionType.Help):
                ShowHelp();
                break;
            case var options when options.Any(option => option.OptionType == CommandLineOptionType.Version):
                ShowVersion();
                break;
            case var options when options.Any(option => option.OptionType == CommandLineOptionType.ListAliases):
                ListAliases();
                break;
            default:
                LaunchTerminalWithAlias(command);
                break;
        }
    }

    private static void LaunchTerminalWithAlias(CommandLineCommand command)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "wt.exe",
            UseShellExecute = false
        };

        // Control destination terminal window
        string targetWindow = string.IsNullOrWhiteSpace(command.Context.LaunchMode)
            ? LaunchModes.NewWindow 
            : command.Context.LaunchMode;
        startInfo.ArgumentList.Add("-w");
        startInfo.ArgumentList.Add(targetWindow);

        // Control terminal profile. If ommitted, wt.exe uses the default profile.
        bool isProfileSpecified = !string.IsNullOrWhiteSpace(command.Alias.ResolvedName);
        if (isProfileSpecified)
        {
            startInfo.ArgumentList.Add("-p");
            startInfo.ArgumentList.Add(command.Alias.ResolvedName);
        }

        // Control working directory.
        // This will be the current directory of the Windows Explorer window
        // that launched this application from its address bar.
        startInfo.ArgumentList.Add("-d");
        startInfo.ArgumentList.Add(Environment.CurrentDirectory);

        _ = Process.Start(startInfo);
    }
}
