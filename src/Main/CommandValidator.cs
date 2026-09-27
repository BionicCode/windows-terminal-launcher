namespace Main;

using System.Collections.Immutable;
using System.Data;
using System.IO;
using System.Runtime.CompilerServices;

internal static class CommandValidator
{
    public static ValidationResult ValidateCommandSyntax(CommandLineCommand command, ImmutableDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validCommandOptionsTable)
    {
        ArgumentNullException.ThrowIfNull(validCommandOptionsTable);

        // A command with no options is the default 'Launch-Windows-Terminal' command
        if (!command.Arguments.HasOptions)
        {
            return ValidateLaunchWindowsTerminalCommandSyntax(command, validCommandOptionsTable);
        }

        foreach (KeyValuePair<CommandLineOptionId, CommandLineOption> entry in command.Arguments.OptionsTable)
        {
            CommandLineOption option = entry.Value;
            switch (option.Descriptor.OptionType)
            {
                // Mandatory command defining mode arguments (are stand-alone allowed)
                case CommandLineOptionId.GetOrSetConfigLocation:
                    return ValidateGetOrSetConfigLocationCommandSyntax(command, validCommandOptionsTable);
                case CommandLineOptionId.GetOrSetEnvironmentVariable:
                    return ValidateGetOrSetEnvironmentVariableCommandSyntax(command, validCommandOptionsTable);
                case CommandLineOptionId.LaunchWindowsTerminal:
                    return ValidateLaunchWindowsTerminalCommandSyntax(command, validCommandOptionsTable);
                case CommandLineOptionId.Help:
                    return ValidateHelpCommandSyntax(command, validCommandOptionsTable);
                case CommandLineOptionId.Version:
                    return ValidateVersionCommandSyntax(command, validCommandOptionsTable);
                case CommandLineOptionId.ListAliases:
                    return ValidateListAliasesCommandSyntax(command, validCommandOptionsTable);

                // Normal command options (require a mode i.e. are not stand-alone)
                case CommandLineOptionId.Undefined:
                case CommandLineOptionId.RunAsAdmin:
                case CommandLineOptionId.SourcePath:
                case CommandLineOptionId.DestinationPath:
                case CommandLineOptionId.EnvironmentVariableValue:
                case CommandLineOptionId.EnvironmentVariableScopeUser:
                case CommandLineOptionId.EnvironmentVariableScopeMachine:
                case CommandLineOptionId.EnvironmentVariableWriteModeJoin:
                case CommandLineOptionId.EnvironmentVariableWriteModeJoinDelimiter:
                case CommandLineOptionId.EnvironmentVariableWriteModeReplace:
                case CommandLineOptionId.Print:
                case CommandLineOptionId.FoldPath:
                case CommandLineOptionId.WorkingDirectory:
                    if (command.HasMode)
                    {
                        continue;
                    }

                    throw new InvalidCommandArgumentException($"The command is invalid and misses a mode specifier. {CommandHelpers.ErrorMessageHint}");
                default:
                    throw new NotImplementedException($"Command validation not implemented for '{Enum.GetName(option.Descriptor.OptionType)}'.");
            }
        }

        throw new InvalidCommandArgumentException("Con appropriate command validator found.");
    }

    private static ValidationResult ValidateHelpCommandSyntax(CommandLineCommand command, ImmutableDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validCommandOptionsTable)
    {
        ThrowIfCommandTypeIsWrong(CommandLineOptionId.Help, command);

        return CreateErrorMessageIfModeIsWrong(command, CommandLineOptionId.Help, isHintRequired: true, out string errorMessage)
            ? new ValidationResult([errorMessage])
            : CreateErrorMessageIfArgumentCountIsGreaterThan(command.Arguments.OptionsTable.Count, 1, command.Name, isHintRequired: true, out errorMessage)
                ? new ValidationResult([errorMessage])
                : ValidationResult.ValidResult;
    }

    private static ValidationResult ValidateListAliasesCommandSyntax(CommandLineCommand command, ImmutableDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validCommandOptionsTable)
    {
        ThrowIfCommandTypeIsWrong(CommandLineOptionId.ListAliases, command);

        return CreateErrorMessageIfModeIsWrong(command, CommandLineOptionId.ListAliases, isHintRequired: true, out string errorMessage)
            ? new ValidationResult([errorMessage])
            : CreateErrorMessageIfArgumentCountIsGreaterThan(command.Arguments.OptionsTable.Count, 1, command.Name, isHintRequired: true, out errorMessage)
                ? new ValidationResult([errorMessage])
                : ValidationResult.ValidResult;
    }

    private static ValidationResult ValidateVersionCommandSyntax(CommandLineCommand command, ImmutableDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validCommandOptionsTable)
    {
        ThrowIfCommandTypeIsWrong(CommandLineOptionId.Version, command);

        return CreateErrorMessageIfModeIsWrong(command, CommandLineOptionId.Version, isHintRequired: true, out string errorMessage)
            ? new ValidationResult([errorMessage])
            : CreateErrorMessageIfArgumentCountIsGreaterThan(command.Arguments.OptionsTable.Count, 1, command.Name, isHintRequired: true, out errorMessage)
                ? new ValidationResult([errorMessage])
                : ValidationResult.ValidResult;
    }

    private static ValidationResult ValidateLaunchWindowsTerminalCommandSyntax(CommandLineCommand command, ImmutableDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validCommandOptionsTable)
    {
        ThrowIfCommandTypeIsWrong(CommandLineOptionId.LaunchWindowsTerminal, command);
        
        if (CreateErrorMessageIfModeIsWrong(command, CommandLineOptionId.LaunchWindowsTerminal, isHintRequired: false, out string errorMessage))
        {
            return new ValidationResult([errorMessage]);
        }
        
        var invalidOptions = new HashSet<CommandLineOptionId>(command.Arguments.OptionsTable.Keys);
        _ = invalidOptions.Remove(CommandLineOptionId.LaunchWindowsTerminal);

        List<string> errorMessages = [];

        if (command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.WorkingDirectory, out CommandLineOption workingDirectoryOption))
        {
            string workingDirectoryPath = workingDirectoryOption.Value;
            if (string.IsNullOrWhiteSpace(workingDirectoryPath))
            {
                errorMessage = $"The working directory path provided for the option '{workingDirectoryOption.Descriptor.Name} | {workingDirectoryOption.Descriptor.AlternativeName}' is invalid. Found an empty string.'";
                errorMessages.Add(errorMessage);
            }
            else
            {
                if (CommandHelpers.IsFilePath(workingDirectoryPath))
                {
                    errorMessage = $"The working directory provided for the option '{workingDirectoryOption.Descriptor.Name} | {workingDirectoryOption.Descriptor.AlternativeName}' must be a directory. Found file: '{workingDirectoryPath}'.";
                    errorMessages.Add(errorMessage);
                }

                if (!Path.IsPathFullyQualified((workingDirectoryPath)))
                {
                    errorMessage = $"The working directory provided for the option '{workingDirectoryOption.Descriptor.Name} | {workingDirectoryOption.Descriptor.AlternativeName}' must be a fully qualified directory. Found a relative path: '{workingDirectoryPath}'.";
                    errorMessages.Add(errorMessage);
                }
            }

            _ = invalidOptions.Remove(CommandLineOptionId.WorkingDirectory);
        }

        if (command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.RunAsAdmin))
        {
            _ = invalidOptions.Remove(CommandLineOptionId.RunAsAdmin);
        }

        if (CreateErrorMessageIfArgumentCountIsGreaterThan(invalidOptions.Count, 0, command.Name, isHintRequired: false, out errorMessage))
        {
            errorMessages.Add(errorMessage);
        }

        if (errorMessage.Any())
        {
            errorMessages.Add(CommandHelpers.ErrorMessageHint);
            return new ValidationResult(errorMessages.ToImmutableArray());
        }
        else
        {
            return ValidationResult.ValidResult;
        }
    }

    private static ValidationResult ValidateGetOrSetEnvironmentVariableCommandSyntax(CommandLineCommand command, ImmutableDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validCommandOptionsTable)
    {
        ThrowIfCommandTypeIsWrong(CommandLineOptionId.GetOrSetEnvironmentVariable, command);

        if (CreateErrorMessageIfModeIsWrong(command, CommandLineOptionId.GetOrSetEnvironmentVariable, isHintRequired: false, out string errorMessage))
        {
            return new ValidationResult([errorMessage]);
        }

        var invalidOptions = new HashSet<CommandLineOptionId>(command.Arguments.OptionsTable.Keys);
        _ = invalidOptions.Remove(CommandLineOptionId.GetOrSetEnvironmentVariable);

        List<string> errorMessages = [];

        if (command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.EnvironmentVariableScopeMachine))
        {
            _ = invalidOptions.Remove(CommandLineOptionId.EnvironmentVariableScopeMachine);
        }
        else if (command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.EnvironmentVariableScopeUser))
        {
            _ = invalidOptions.Remove(CommandLineOptionId.EnvironmentVariableScopeUser);
        }
        else
        {
            _ = validCommandOptionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableScopeMachine, out CommandLineOptionDescriptor machineScopeDescriptor);
            _ = validCommandOptionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableScopeUser, out CommandLineOptionDescriptor userScopeDescriptor);
            errorMessage = CreateInvalidCommandArgumentErrorMessageForArgumentMissing(command, $"'{machineScopeDescriptor.Name} | {machineScopeDescriptor.AlternativeName}' or {userScopeDescriptor.Name} | {userScopeDescriptor.AlternativeName}'", isHintRequired: false);
            errorMessages.Add(errorMessage);
        }

        bool isPrintOptionProvided = command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.Print);
        if (isPrintOptionProvided)
        {
            _ = invalidOptions.Remove(CommandLineOptionId.Print);
            if (CreateErrorMessageIfArgumentCountIsGreaterThan(invalidOptions.Count, 0, "Show-Environment-Variable", isHintRequired: false, out errorMessage))
            {
                errorMessages.Add(errorMessage);
                return new ValidationResult(errorMessages.ToImmutableArray());
            }

            return errorMessages.Any()
                ? new ValidationResult(errorMessages.ToImmutableArray())
                : ValidationResult.ValidResult;
        }

        if (command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.EnvironmentVariableValue))
        {
            _ = invalidOptions.Remove(CommandLineOptionId.EnvironmentVariableValue);
        }

        

        if (command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.FoldPath))
        {
            _ = invalidOptions.Remove(CommandLineOptionId.FoldPath);
        }

        bool isPathEnvironmentVariable = command.CommandIdProviderOption.Value.Equals(CommandHelpers.VariableName_PATH, StringComparison.OrdinalIgnoreCase);
        if (command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.EnvironmentVariableWriteModeJoinDelimiter))
        {
            if (isPathEnvironmentVariable)
            {
                errorMessage = $"No custom delimiter for the '{CommandHelpers.VariableName_PATH}' environment variable allowed.";
                errorMessages.Add(errorMessage);
            }

            _ = invalidOptions.Remove(CommandLineOptionId.FoldPath);
        }

        if (command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.EnvironmentVariableWriteModeJoin))
        {
            _ = invalidOptions.Remove(CommandLineOptionId.EnvironmentVariableWriteModeJoin);
        }
        else if (command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.EnvironmentVariableWriteModeReplace))
        {
            _ = invalidOptions.Remove(CommandLineOptionId.EnvironmentVariableWriteModeReplace);
        }
        else if (!isPathEnvironmentVariable)
        {
            _ = validCommandOptionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableWriteModeJoin, out CommandLineOptionDescriptor machineScopeDescriptor);
            _ = validCommandOptionsTable.TryGetValue(CommandLineOptionId.EnvironmentVariableWriteModeReplace, out CommandLineOptionDescriptor userScopeDescriptor);
            errorMessage = CreateInvalidCommandArgumentErrorMessageForArgumentMissing(command, $"'{machineScopeDescriptor.Name} | {machineScopeDescriptor.AlternativeName}' or {userScopeDescriptor.Name} | {userScopeDescriptor.AlternativeName}'", isHintRequired: false);
            errorMessages.Add(errorMessage);
        }

        if (CreateErrorMessageIfArgumentCountIsGreaterThan(invalidOptions.Count, 0, "Set-Environment-Variable", isHintRequired: false, out errorMessage))
        {
            errorMessages.Add(errorMessage);
        }

        if (CreateErrorMessageIfProfileAliasFound(command, isHintRequired: false, out errorMessage))
        {
            errorMessages.Add(errorMessage);
        }

        if (errorMessages.Any())
        {
            errorMessages.Add(CommandHelpers.ErrorMessageHint);
            return new ValidationResult(errorMessages.ToImmutableArray());
        }
        else
        {
            return ValidationResult.ValidResult;
        }
    }

    private static ValidationResult ValidateGetOrSetConfigLocationCommandSyntax(CommandLineCommand command, ImmutableDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validCommandOptionsTable)
    {
        ThrowIfCommandTypeIsWrong(CommandLineOptionId.GetOrSetConfigLocation, command);

        if (CreateErrorMessageIfModeIsWrong(command, CommandLineOptionId.GetOrSetConfigLocation, isHintRequired: false, out string errorMessage))
        {
            return new ValidationResult([errorMessage]);
        }

        var invalidOptions = new HashSet<CommandLineOptionId>(command.Arguments.OptionsTable.Keys);
        _ = invalidOptions.Remove(CommandLineOptionId.GetOrSetConfigLocation);

        List<string> errorMessages = [];

        bool isPrintOptionProvided = command.Arguments.OptionsTable.ContainsKey(CommandLineOptionId.Print);
        if (isPrintOptionProvided)
        {
            _ = invalidOptions.Remove(CommandLineOptionId.Print);
            if (CreateErrorMessageIfArgumentCountIsGreaterThan(invalidOptions.Count, 0,"Show-User-Config-Location", isHintRequired: false, out errorMessage))
            {
                errorMessages.Add(errorMessage);
                return new ValidationResult(errorMessages.ToImmutableArray());
            }

            return errorMessages.Any()
                ? new ValidationResult(errorMessages.ToImmutableArray())
                : ValidationResult.ValidResult;
        }

        if (command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.SourcePath, out CommandLineOption sourcePathOption))
        {
            _ = invalidOptions.Remove(CommandLineOptionId.SourcePath);

            string sourcePath = sourcePathOption.Value;
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                errorMessage = $"Invalid command argument. A '{sourcePathOption.Descriptor.Name} | {sourcePathOption.Descriptor.AlternativeName}' option was provided but no path was specified.";
                errorMessages.Add(errorMessage);
            }

            if (!Path.IsPathFullyQualified((sourcePath)))
            {
                errorMessage = $"The source path provided for the option '{sourcePathOption.Descriptor.Name} | {sourcePathOption.Descriptor.AlternativeName}' must be a fully qualified file path. Found a relative path: '{sourcePath}'.";
                errorMessages.Add(errorMessage);
            }

            if (!CommandHelpers.IsFilePath(sourcePath))
            {
                errorMessage = $"Invalid path argument. A source file path must provide the file name of the source.";
                errorMessages.Add(errorMessage);
            }

            if (!Path.HasExtension(sourcePath)
                || !(Path.GetExtension(sourcePath).Equals(".yaml", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(sourcePath).Equals(".yml", StringComparison.OrdinalIgnoreCase)))
            {
                errorMessage = $"Invalid path argument. A '{sourcePathOption.Descriptor.Name} | {sourcePathOption.Descriptor.AlternativeName}' option was provided but the file extension does not match '.yaml' or '.yml'.";
                errorMessages.Add(errorMessage);
            }
        }
        
        if (command.Arguments.OptionsTable.TryGetValue(CommandLineOptionId.DestinationPath, out CommandLineOption destinationPathOption))
        {
            _ = invalidOptions.Remove(CommandLineOptionId.DestinationPath);

            string destinationPath = destinationPathOption.Value;
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                errorMessage = $"Invalid command argument. A '{destinationPathOption.Descriptor.Name} | {destinationPathOption.Descriptor.AlternativeName}' option was provided but no path was specified.";
                errorMessages.Add(errorMessage);
            }

            if (!Path.IsPathFullyQualified((destinationPath)))
            {
                errorMessage = $"The source path provided for the option '{destinationPathOption.Descriptor.Name} | {destinationPathOption.Descriptor.AlternativeName}' must be a fully qualified file path. Found a relative path: '{destinationPath}'.";
                errorMessages.Add(errorMessage);
            }

            // Only validate extension if the path is a file path.
            // Otherwise allow the destination to be a directory.
            if (CommandHelpers.IsFilePath(destinationPath)
                && (!Path.HasExtension(destinationPath)
                || !(Path.GetExtension(destinationPath).Equals(".yaml", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(destinationPath).Equals(".yml", StringComparison.OrdinalIgnoreCase))))
            {
                errorMessage = $"Invalid path argument. A '{destinationPathOption.Descriptor.Name} | {destinationPathOption.Descriptor.AlternativeName}' option was provided but the file extension does not match '.yaml' or '.yml'.";
                errorMessages.Add(errorMessage);
            }
        }

        if (CreateErrorMessageIfArgumentCountIsGreaterThan(invalidOptions.Count, 0, "Set-User-Config-Location", isHintRequired: false, out errorMessage))
        {
            errorMessages.Add(errorMessage);
        }

        if (CreateErrorMessageIfProfileAliasFound(command, isHintRequired: false, out errorMessage))
        {
            errorMessages.Add(errorMessage);
        }

        if (errorMessages.Any())
        {
            errorMessages.Add(CommandHelpers.ErrorMessageHint);
            return new ValidationResult(errorMessages.ToImmutableArray());
        }
        else
        {
            return ValidationResult.ValidResult;
        }
    }

    private static string CreateInvalidCommandArgumentErrorMessageForArgumentMissing(CommandLineCommand command, string missingOptionsString, bool isHintRequired) => $"Invalid argument list. The required option {missingOptionsString} for the '{command.Name}' command is missing.{(isHintRequired ? " " + CommandHelpers.ErrorMessageHint : string.Empty)}";
    
    private static bool CreateErrorMessageIfProfileAliasFound(CommandLineCommand command, bool isHintRequired, out string errorMessage)
    {
        errorMessage = command.HasAlias
            ? $"Command '{command.Name}' is malformed. When selecting the mode '{command.CommandIdProviderOption.Descriptor.Name} | {command.CommandIdProviderOption.Descriptor.AlternativeName}' the command cann't specify a Windows Terminal profile alias.{(isHintRequired ? " " + CommandHelpers.ErrorMessageHint : string.Empty)}"
            : string.Empty;

        return command.HasAlias;
    }

    private static bool CreateErrorMessageIfArgumentCountIsGreaterThan(int invalidOptionsCount, int threshold, string commandName, bool isHintRequired, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (invalidOptionsCount > threshold)
        {
            errorMessage = $"Invalid command argument list for command '{commandName}': too many arguments.{(isHintRequired ? " " + CommandHelpers.ErrorMessageHint : string.Empty)}";
            return true;
        }

        return false;
    }

    private static bool CreateErrorMessageIfModeIsWrong(CommandLineCommand command, CommandLineOptionId expectedModeOption, bool isHintRequired, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!command.HasMode)
        {
            errorMessage = $"Invalid command argument list. A command argument list must contain a single mode option.{(isHintRequired ? " " + CommandHelpers.ErrorMessageHint : string.Empty)}";
            return true;
        }
        else if (command.Arguments.OptionsTable.Select(entry => entry.Value)
            .Count(option => option.Descriptor.Kind is CommandLineOptionKind.Mode or CommandLineOptionKind.ModeAndValue) > 1)
        {
            errorMessage = $"Invalid command argument list. A command can only have a single mode option.{(isHintRequired ? " " + CommandHelpers.ErrorMessageHint : string.Empty)}";
            return true;
        }
        else if (command.CommandIdProviderOption.Descriptor.OptionType != expectedModeOption)
        {
            errorMessage = $"Invalid command argument list. Wrong mode option provided. Expected: '{Enum.GetName(expectedModeOption)}'. Found: '{Enum.GetName(command.CommandIdProviderOption.Descriptor.OptionType)}'.{(isHintRequired ? " " + CommandHelpers.ErrorMessageHint : string.Empty)}";
            return true;
        }

        return false;
    }

    private static void ThrowIfCommandTypeIsWrong(CommandLineOptionId expectedCommandType, CommandLineCommand command, [CallerArgumentExpression(nameof(command))] string? paramName = null)
    {
        if (command.CommandIdProviderOption.Descriptor.OptionType != expectedCommandType)
        {
            throw new ArgumentException($"Invalid argument '{paramName}'. Validator expected a '{command.Name}' command of command type '{Enum.GetName(expectedCommandType)}'. But found: '{command.CommandIdProviderOption.Descriptor.OptionType}'.", paramName);
        }
    }
}
