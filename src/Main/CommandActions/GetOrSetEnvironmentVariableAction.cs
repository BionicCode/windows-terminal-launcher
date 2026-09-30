namespace Main;

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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

internal sealed class GetOrSetEnvironmentVariableAction : CommandAction
{
    private const string UserEnvironmentRegistryKey = "Environment";
    private const string SystemEnvironmentRegistryKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";

    public GetOrSetEnvironmentVariableAction() : base(CommandLineOptionId.GetOrSetEnvironmentVariable) 
    {        
    }

    protected override CommandExitMode ExecuteInternal(CommandLineCommand command, IApplicationSettings applicationSettings, UserConfiguration userSettings, ImmutableDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validCommandOptionsTable)
    {
        if (!command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.GetOrSetEnvironmentVariable, out CommandLineOption variableNameOption)
            || (!command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.EnvironmentVariableScopeMachine)
                || !Enum.TryParse(CommandLineOptionId.EnvironmentVariableScopeMachine.ToDisplayString(), ignoreCase: true, out EnvironmentVariableTarget environmentVariableTarget))
            && (!command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.EnvironmentVariableScopeUser)
                || !Enum.TryParse(CommandLineOptionId.EnvironmentVariableScopeUser.ToDisplayString(), ignoreCase: true, out environmentVariableTarget)))
        {
            throw new InvalidCommandArgumentException("The command is malformed. Check command validator whether the current case is handled correctly.");
        }

        if (command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.Print))
        {
            return ShowEnvironmentVariabelValue(variableNameOption, environmentVariableTarget);
        }
        else
        {

            if (environmentVariableTarget is EnvironmentVariableTarget.Machine
                && !CommandHelpers.IsCurrentProcessElevated())
            {
                _ = CommandHelpers.RelaunchElevated();
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

        string variableName = variableNameOption.Value;
        bool isVariablePathVariable = variableName.Equals(CommandHelpers.VariableName_PATH, StringComparison.OrdinalIgnoreCase);

        bool wasFolded = optionsTable.ContainsKey(CommandLineOptionId.FoldPath)
            && TryFoldNewValue(
                ref newValue,
                variableName,
                environmentVariableTarget,
                registryKey,
                isVariablePathVariable);

        object? rawValue = registryKey.GetValue(
            variableName,
            null,
            RegistryValueOptions.DoNotExpandEnvironmentNames);

        string currentValue = rawValue as string ?? string.Empty;
        string delimiter = isVariablePathVariable 
            || !optionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableWriteModeJoinDelimiter, out CommandLineOption delimiterOption)
                ? Path.PathSeparator.ToString()
                : delimiterOption.Value;
        if (!string.IsNullOrWhiteSpace(currentValue) 
            && (optionsTable.ContainsKey(CommandLineOptionId.EnvironmentVariableWriteModeJoin)
                || isVariablePathVariable))
        {
            newValue = string.Join(delimiter, currentValue, newValue);
        }

        RegistryValueKind kind = rawValue is null
            ? RegistryValueKind.String
            : registryKey.GetValueKind(variableName);
        if (wasFolded
            && kind is RegistryValueKind.String)
        {
            kind = RegistryValueKind.ExpandString;
        }

        registryKey.SetValue(variableName, newValue, kind);
        BroadcastEnvironmentChange();

        return CommandExitMode.ShutdownRequired;
    }

    private static bool TryFoldNewValue(
        ref string newValue, 
        string targetVariableName, 
        EnvironmentVariableTarget environmentVariableTarget, 
        RegistryKey registryKey,
        bool isFileSystemPath)
    {
        string trimmedCurrentValue = string.Empty;
        Dictionary<string, FoldCandidate> candidates = new(StringComparer.OrdinalIgnoreCase);
        if (environmentVariableTarget is EnvironmentVariableTarget.User)
        {
            using RegistryKey systemEnvironmentRegistryKey = Registry.LocalMachine.OpenSubKey(
                SystemEnvironmentRegistryKey,
                RegistryKeyPermissionCheck.ReadSubTree,
                RegistryRights.QueryValues)
                ?? throw new InvalidOperationException("Environment registry key is missing.");

            AddEntries(newValue, targetVariableName, systemEnvironmentRegistryKey, isOverrideMachineVariable: false, isFileSystemPath, candidates);
        }

        // Important for User-over-Machine precedence:
        // even a nonmatching User definition must shadow the same Machine variable.
        AddEntries(newValue, targetVariableName, registryKey, isOverrideMachineVariable: environmentVariableTarget is EnvironmentVariableTarget.User, isFileSystemPath, candidates);
        FoldCandidate? selectedCandidate = candidates.Values
            .OrderByDescending(entry => entry.NormalizedVariableValue.Length)
            .FirstOrDefault(entry => !string.IsNullOrWhiteSpace(entry.NormalizedVariableValue));
        if (selectedCandidate is not null)
        {
            string remainingNewValue = newValue[selectedCandidate.NormalizedVariableValue.Length..];
            newValue = $"%{selectedCandidate.VariableName}%{remainingNewValue}";

            return true;
        }

        return false;
    }

    private static void AddEntries(
        string newValue, 
        string targetVariablename,
        RegistryKey registryKey, 
        bool isOverrideMachineVariable,
        bool isFileSystemPath, 
        Dictionary<string, FoldCandidate> candidates)
    {
        string[] variableNames = registryKey.GetValueNames();
        foreach (string variableName in variableNames)
        {
            if (variableName.Equals(targetVariablename, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (isOverrideMachineVariable)
            {
                _ = candidates.Remove(variableName);
            }

            string? rawValue = registryKey.GetValue(
                variableName,
                null,
                RegistryValueOptions.None) as string;
            string normalizedNewValue = newValue;
            if (rawValue is not string currentValue
                || !IsPrefixMatch(ref normalizedNewValue, ref currentValue, isFileSystemPath))
            {
                continue;
            }

            var candidate = new FoldCandidate(
                variableName, 
                rawValue, 
                currentValue,
                newValue,
                normalizedNewValue,
                isFileSystemPath);
            candidates.Add(variableName, candidate);

            // Already found best candidate
            if (currentValue.Length == newValue.Length)
            {
                break;
            }
        }
    }

    private static bool IsPrefixMatch(ref string value, ref string candidate, bool isFileSystemPath)
    {
        // If we are dealing with a file system path we should normalize
        // the path's directory separators to make matching reliable
        if (isFileSystemPath)
        {
            // Normalize path for separators and to compact path
            // e.g., removing redundant segments like removing "Temp" from "C:\Folder\Temp\..\Subfolder"
            // to produce "C:\Folder\Subfolder" after normalization
            
            value = Path.GetFullPath(value);
            candidate = Path.GetFullPath(candidate);
        }

        if (!value.StartsWith(
            candidate,
            StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return value.Length == candidate.Length
            || Path.EndsInDirectorySeparator(candidate)
            // The next charact of the new value after the candidate match must be a directory separator
            // (if the match is not of the same length and not already ending with a directory separator)
            || value[candidate.Length] == Path.DirectorySeparatorChar 
            || value[candidate.Length] == Path.AltDirectorySeparatorChar;
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
        else if (variableName.Equals(CommandHelpers.VariableName_PATH, StringComparison.OrdinalIgnoreCase))
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

        CommandHelpers.ShowInfoDialog(
            message,
            "lit.exe Print Environment Variable",
            "Print Environment Variable");

        return CommandExitMode.Auto;
    }
}
