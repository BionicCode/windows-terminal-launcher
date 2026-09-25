namespace Main;

using System;
using System.Collections.Immutable;
using System.Diagnostics;

internal sealed class LaunchWindowsTerminalAction : CommandAction
{
    public LaunchWindowsTerminalAction() : base(CommandLineOptionId.LaunchWindowsTerminal)
    {
    }

    protected override CommandExitMode ExecuteInternal(CommandLineCommand command, IApplicationSettings applicationSettings, UserConfiguration userSettings, ImmutableDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validCommandOptionsTable)
    {
        LaunchTerminalWithAlias(command);
        return CommandExitMode.ShutdownRequired;
    }

    private static void LaunchTerminalWithAlias(CommandLineCommand command)
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
        // that launched this application from its address bar or provided using the -w or --working-diractory option.
        string workingDirectory = command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.WorkingDirectory, out CommandLineOption workingDirectoryOption)
            ? workingDirectoryOption.Value
            : Environment.CurrentDirectory;
        startInfo.ArgumentList.Add("-d");
        startInfo.ArgumentList.Add(workingDirectory);

        using Process? process = Process.Start(startInfo);
    }
}