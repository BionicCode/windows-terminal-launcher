namespace Main;

using System.Diagnostics;
using System.IO;
using System.Security.Principal;

internal static class CommandHandlerHelpers
{

    public static string GetFileNameIfFile(string path) => IsFilePath(path)
        ? Path.GetFileName(path)
        : string.Empty;

    public static bool IsFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        FileAttributes attributes = File.GetAttributes(path);

        return !attributes.HasFlag(FileAttributes.Directory);
    }

    public static bool IsCurrentProcessElevated()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);

        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static int RelaunchElevated()
    {
        string executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "Unable to determine the executable path.");

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = true,
            Verb = "runas",

            // Important if Environment.CurrentDirectory has semantic meaning
            // to the command.
            WorkingDirectory = Environment.CurrentDirectory,
        };

        string[] arguments = Environment.GetCommandLineArgs();

        // [0] is the executable itself.
        for (int index = 1; index < arguments.Length; index++)
        {
            startInfo.ArgumentList.Add(arguments[index]);
        }

        using Process process =
            Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Unable to start elevated process.");

        process.WaitForExit();

        return process.ExitCode;
    }
}