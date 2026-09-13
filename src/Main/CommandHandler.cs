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

        using Process? process = Process.Start(startInfo);
    }
}
