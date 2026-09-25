namespace Main;

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

internal sealed class GetOrSetUserConfigLocationAction : CommandAction
{
    public GetOrSetUserConfigLocationAction() : base(CommandLineOptionId.GetOrSetConfigLocation)
    {        
    }

    protected override CommandExitMode ExecuteInternal(CommandLineCommand command, IApplicationSettings applicationSettings, UserConfiguration userSettings, ImmutableDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validCommandOptionsTable)
    {
        ArgumentNullException.ThrowIfNull(applicationSettings);

        _ = applicationSettings.TryGet(AppSettingsKeys.UserConfigFileLocationKey, out string currentConfigFilePath);

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
            return CommandExitMode.Auto;
        }
        else
        {
            _ = TryGetpath(CommandLineOptionId.SourcePath, command, out string sourcePath, currentConfigFilePath);

            string fallbackFileName = CommandHandlerHelpers.GetFileNameIfFile(sourcePath);
            _ = TryGetpath(CommandLineOptionId.DestinationPath, command, out string destinationPath, Environment.CurrentDirectory, fallbackFileName);
            if (sourcePath.Equals(destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                return CommandExitMode.ShutdownRequired;
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
                    return CommandExitMode.ShutdownRequired;
                }
            }

            SetConfigLocationAsync(sourcePath, destinationPath);
            applicationSettings.AddOrUpdate(AppSettingsKeys.UserConfigFileLocationKey, destinationPath);

            return CommandExitMode.ShutdownRequired;
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

        if (!string.IsNullOrWhiteSpace(path)
            && !string.IsNullOrWhiteSpace(fileName)
            && !CommandHandlerHelpers.IsFilePath(path))
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