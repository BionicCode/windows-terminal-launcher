namespace Main;

using System;
using System.Collections.Immutable;
using System.Drawing;
using System.IO;
using System.Security.AccessControl;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

internal class GetOrSetEnvironmentVariableAction : ICommandAction
{
    private const string VariableName_PATH = "PATH";
    private const string UserEnvironmentRegistryKey = "Environment";
    private const string SystemEnvironmentRegistryKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";

    public CommandExitMode Execute(CommandLineCommand command, IApplicationSettings applicationSettings)
    {
        if (!command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.GetOrSetEnvironmentVariable, out CommandLineOption variableNameOption)
            || (!command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.EnvironmentVariableScopeMachine)
                || !Enum.TryParse(CommandLineOptionId.EnvironmentVariableScopeMachine.ToDisplayString(), ignoreCase: true, out EnvironmentVariableTarget environmentVariableTarget))
            && (!command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.EnvironmentVariableScopeUser)
                || !Enum.TryParse(CommandLineOptionId.EnvironmentVariableScopeUser.ToDisplayString(), ignoreCase: true, out environmentVariableTarget)))
        {
            throw new InvalidCommandArgumentException("The command is malformed");
        }

        if (command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.Print))
        {
            return ShowEnvironmentVariabelValue(variableNameOption, environmentVariableTarget);
        }
        else
        {

            if (environmentVariableTarget is EnvironmentVariableTarget.Machine
                && !CommandHandlerHelpers.IsCurrentProcessElevated())
            {
                _ = CommandHandlerHelpers.RelaunchElevated();
                return CommandExitMode.ShutdownRequired;
            }

            return SetEnvironmentVariabelValue(variableNameOption, command.Arguments.OptionsTable, environmentVariableTarget);
        }
    }

    private static CommandExitMode SetEnvironmentVariabelValue(CommandLineOption variableNameOption, ImmutableDictionary<CommandLineOptionId, CommandLineOption> optionsTable, EnvironmentVariableTarget environmentVariableTarget)
    {
        string newValue = optionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableValue, out CommandLineOption variableValueOption)
            ? variableValueOption.Value
            : Environment.CurrentDirectory;

        // TODO::Attempt to replace its longest leading path prefix with an existing environment-variable reference.

        // We have to use the registry here because 'Environment.GetOrSetEnvironmentVariable' will resolve variables like "%Temp%\Folder".
        // But we don't want to erase such variabled lineIndex.e. folded paths. Using the registry manually allows us to preserve "%TEMP%".
        using RegistryKey registryKey = (environmentVariableTarget is EnvironmentVariableTarget.User
            ? Registry.CurrentUser.OpenSubKey(
                UserEnvironmentRegistryKey,
    RegistryKeyPermissionCheck.ReadWriteSubTree,
                RegistryRights.SetValue | RegistryRights.QueryValues)
            : Registry.LocalMachine.OpenSubKey(
                SystemEnvironmentRegistryKey,
    RegistryKeyPermissionCheck.ReadWriteSubTree,
                RegistryRights.SetValue | RegistryRights.QueryValues))
            ?? throw new InvalidOperationException("Environment registry key is missing.");

        if (optionsTable.ContainsKey(CommandLineOptionId.FoldPath))
        {
            _ = TryFoldNewValue(ref newValue, environmentVariableTarget, registryKey);
        }

        string variableName = variableNameOption.Value;
        object? rawValue = registryKey.GetValue(
            variableName,
            null,
            RegistryValueOptions.DoNotExpandEnvironmentNames);

        string currentValue = rawValue as string ?? string.Empty;
        string delimiter = variableName.Equals(VariableName_PATH, StringComparison.OrdinalIgnoreCase) 
            || !optionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableWriteModeJoinDelimiter, out CommandLineOption delimiterOption)
                ? Path.PathSeparator.ToString()
                : delimiterOption.Value;
        if (!string.IsNullOrWhiteSpace(currentValue) 
            && optionsTable.ContainsKey(CommandLineOptionId.EnvironmentVariableWriteModeJoin))
        {
            newValue = string.Join(delimiter, currentValue, newValue);
        }

        RegistryValueKind kind = rawValue is null
            ? RegistryValueKind.String
            : registryKey.GetValueKind(variableName);
        registryKey.SetValue(variableName, newValue, kind);

        BroadcastEnvironmentChange();

        return CommandExitMode.ShutdownRequired;
    }

    private static bool TryFoldNewValue(ref string newValue, EnvironmentVariableTarget environmentVariableTarget, RegistryKey registryKey)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (environmentVariableTarget is EnvironmentVariableTarget.User)
        {            
            using RegistryKey systemEnvironmentRegistryKey = Registry.LocalMachine.OpenSubKey(
                SystemEnvironmentRegistryKey,
                RegistryKeyPermissionCheck.ReadWriteSubTree,
                RegistryRights.SetValue | RegistryRights.QueryValues) 
                ?? throw new InvalidOperationException("Environment registry key is missing.");
        }

        // TODO::Because user variables have precedence over system variables,
        // the obtained user variables must replace existing system variable in the 'variables' dictionary
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

    private static CommandExitMode ShowEnvironmentVariabelValue(CommandLineOption variableNameOption, EnvironmentVariableTarget environmentVariableTarget)
    {
        // We have to use the registry here because 'Environment.GetOrSetEnvironmentVariable' will resolve variables like "%Temp%\Folder".
        // But we don't want to erase such variabled lineIndex.e. folded paths. Using the registry manually allows us to preserve "%TEMP%".
        using RegistryKey? key = (environmentVariableTarget is EnvironmentVariableTarget.User
            ? Registry.CurrentUser.OpenSubKey(
                "Environment",
    RegistryKeyPermissionCheck.ReadSubTree,
                RegistryRights.QueryValues)
            : Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment",
    RegistryKeyPermissionCheck.ReadSubTree,
                RegistryRights.QueryValues))
            ?? throw new InvalidOperationException("Environment registry key is missing.");

        string variableName = variableNameOption.Value;
        object? rawValue = key.GetValue(
            variableName,
            null,
            RegistryValueOptions.DoNotExpandEnvironmentNames);
        string message;
        if (rawValue is null)
        {
            message = $"Variable '{variableName}' not found.";
        }
        else if (variableName.Equals(VariableName_PATH, StringComparison.OrdinalIgnoreCase))
        {
            string variableValue = rawValue as string 
                ?? rawValue.ToString() 
                ?? string.Empty;
            string[] pathValues = variableValue.Split(Path.PathSeparator, StringSplitOptions.None);
            string presentationDelimiter = $"{Environment.NewLine}{new string(' ', 4)}";
            message = $"UserEnvironmentRegistryKey{presentationDelimiter}{variableName}{Environment.NewLine}Value{presentationDelimiter}{string.Join(presentationDelimiter, pathValues)}";
        }
        else
        {
            string variableValue = rawValue as string
                ?? rawValue.ToString()
                ?? string.Empty;
            message = variableValue;
        }

        var dialog = new InfoDialog
        {
            Title = "lit.exe Print Environment Variable",
            Header = "Print Environment Variable",
            Body = message,
            Icon = Imaging.CreateBitmapSourceFromHIcon(
                SystemIcons.Information.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions())
        };

        dialog.Show();
        return CommandExitMode.Auto;
    }
}
