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
                ? "No location set. Please set a location first. {CommandHelpers.ErrorMessageHint}"
                : currentConfigFilePath;

            CommandHelpers.ShowInfoDialog(
                message,
          "lit.exe user configuration file location",
                "The user configuration YAML file is located at:");

            return CommandExitMode.Auto;
        }
        else
        {
            _ = TryGetPath(CommandLineOptionId.SourcePath, command, out string sourcePath, currentConfigFilePath);

            string fallbackFileName = CommandHelpers.GetFileNameIfFile(sourcePath);
            _ = TryGetPath(CommandLineOptionId.DestinationPath, command, out string destinationPath, Environment.CurrentDirectory, fallbackFileName);
            
            // Special case where the source file is the new user config file
            // and remains located at its original location (no copy or move operation)
            if (sourcePath.Equals(destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                // New location and file is the current location and file
                if (sourcePath.Equals(currentConfigFilePath, StringComparison.OrdinalIgnoreCase))
                {
                    CommandHelpers.ShowInfoDialog(
                        $"Path arguments specify the current user config file location. Therefore no action was performed.{Environment.NewLine}Current location: '{currentConfigFilePath}'",
                        "lit.exe Notification",
                        "Set New User Config Path");
                    
                    return CommandExitMode.Auto;
                }

                applicationSettings.AddOrUpdate(AppSettingsKeys.UserConfigFileLocationKey, sourcePath);
                return CommandExitMode.ShutdownRequired;
            }

            if (File.Exists(destinationPath))
            {
                bool? dialogResult = CommandHelpers.ShowInteractionDialog(
                    $"The file '{destinationPath}'{Environment.NewLine}already exists. Overwrite the existing file?",
                    Imaging.CreateBitmapSourceFromHIcon(SystemIcons.Warning.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()),
                    "File exists",
                    "File exists");

                if (dialogResult == false)
                {
                    return CommandExitMode.ShutdownRequired;
                }
            }

            if (TrySetConfigLocationAsync(sourcePath, destinationPath))
            {
                applicationSettings.AddOrUpdate(AppSettingsKeys.UserConfigFileLocationKey, destinationPath);
            }

            return CommandExitMode.ShutdownRequired;
        }
    }

    private static bool TryGetPath(
        CommandLineOptionId pathId,
        CommandLineCommand command,
        [NotNullWhen(true)] out string path,
        string? fallbackPath = null,
        string? fileName = null)
    {
        if (pathId is not CommandLineOptionId.SourcePath and not CommandLineOptionId.DestinationPath)
        {
            throw new ArgumentException($"Provided option ID '{Enum.GetName(pathId)}' is not a path ID.");
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
            && !CommandHelpers.IsFilePath(path))
        {
            path = Path.Combine(path, fileName);
        }

        return !string.IsNullOrWhiteSpace(path);
    }

    private static bool TrySetConfigLocationAsync(string sourcePath, string destinationPath)
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
            CommandHelpers.ShowErrorDialog(
                ex.Message, 
                header: $"Copying the user configuration file failed:{Environment.NewLine}Source: '{sourcePath}'{Environment.NewLine}Destination: '{destinationPath}'");

            return false;
        }

        return true;
    }
}