namespace Main;

using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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

    public static bool TryNormalizeWindowsPath(
    string path,
    [NotNullWhen(true)] out string? normalizedPath)
    {
        normalizedPath = null;

        if (string.IsNullOrWhiteSpace(path)
            || !Path.IsPathFullyQualified(path))
        {
            return false;
        }

        try
        {
            string fullPath = Path.GetFullPath(path);

            if (!IsLexicallyValidPath(fullPath))
            {
                return false;
            }

            normalizedPath = fullPath;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }

    public static bool IsLexicallyValidPath(string path)
    {
        // Check for invalid characters
        SearchValues<char> invalidPathChars = SearchValues.Create(Path.GetInvalidPathChars());
        if (path.ContainsAny(invalidPathChars))
        {
            return false;
        }

        // Optional: Check for invalid file name characters in segments
        if (!CommandHandlerHelpers.IsFilePath(path))
        {
            return true;
        }

        string fileName = CommandHandlerHelpers.GetFileNameIfFile(path);
        SearchValues<char> invalidNameChars = SearchValues.Create(Path.GetInvalidFileNameChars());
        return !fileName.ContainsAny(invalidNameChars);
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

    public static void ShowInfoDialog(string message, string title = "lit.exe Help", string header = "lit.exe Command Line Help")
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(message);

        var dialog = new InfoDialog
        {
            Title = title,
            Header = header,
            Body = message.ToString(),
            Icon = Imaging.CreateBitmapSourceFromHIcon(
                SystemIcons.Information.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions())
        };

        dialog.Show();
    }

    public static void ShowErrorDialog(string message, string title = "lit.exe Error", string header = "lit.exe Invalid Command Argument")
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(message);

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

    public static void ShowInteractionDialog(string message, ImageSource? dialogIcon, string title = "lit.exe Info", string header = "lit.exe Info")
    {
        ArgumentNullException.ThrowIfNullOrWhiteSpace(message);

        var dialog = new InteractionDialog
        {
            Title = title,
            Header = header,
            Body = message.ToString(),
            Icon = dialogIcon ?? Imaging.CreateBitmapSourceFromHIcon(SystemIcons.Information.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions())
        };

        dialog.Show();
    }
}