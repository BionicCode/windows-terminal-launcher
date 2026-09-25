namespace Main;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using YamlDotNet.Core.Tokens;

internal static class CommandLineArgumentParser
{
    /// <summary>
    /// Parses the command line arguments and returns a CommandLineCommand object containing the alias and options.
    /// </summary>
    /// <param name="rawArguments">The command line arguments to parse.</param>
    /// <param name="validOptions">The lookup table of valid command options.</param>
    /// <param name="userConfiguration"></param>
    /// <returns>A <see cref="CommandParserResult"/> object containing the parsed <see cref="CommandLineCommand"/> and a set of error messages if errors have occurred.</returns>
    /// <exception cref="InvalidCommandArgumentException">Thrown when an invalid command argument is encountered.</exception>
    /// <remarks>Expects a command syntax of the form: <c>lit [alias] [options...]</c></remarks>
    public static CommandParserResult CreateCommand(
        string[]? rawArguments, 
        IReadOnlyDictionary<string, CommandLineOptionDescriptor> validOptions, 
        UserConfiguration userConfiguration)
    {
        ArgumentNullException.ThrowIfNull(rawArguments);
        ArgumentNullException.ThrowIfNull(validOptions);

        var options = new Dictionary<CommandLineOptionId, CommandLineOption>(CommandLineOptionIdComparer.Instance);
        if (rawArguments.Length > 0)
        {
            for (int index = 0; index < rawArguments.Length; index++)
            {
                string arg = rawArguments[index];
                if (string.IsNullOrWhiteSpace(arg))
                {
                    continue;
                }

                // Must be an option if it starts with a dash
                if (arg.StartsWith('-'))
                {
                    if (!validOptions.TryGetValue(arg, out CommandLineOptionDescriptor optionDescriptor))
                    {
                        string errorMessage = $"Invalid command option '{arg}' at argument index '{index}'.{Environment.NewLine}Use '[-h | --help]' to see the list of valid options.";
                        return new CommandParserResult(CommandLineCommand.Default, [errorMessage]);
                    }

                    string value = optionDescriptor.Kind is CommandLineOptionKind.Value or CommandLineOptionKind.ModeAndValue
                        ? rawArguments[++index]
                        : string.Empty;

                    if (optionDescriptor.OptionType is CommandLineOptionId.SourcePath or CommandLineOptionId.DestinationPath or CommandLineOptionId.WorkingDirectory)
                    {
                        if (!CommandHandlerHelpers.TryNormalizeWindowsPath(value, out string? normalizedSourcePath))
                        {
                            string errorMessage = $"Invalid source path argument at argument index '{index}'. The path is malformed.";
                            return new CommandParserResult(CommandLineCommand.Default, [errorMessage]);
                        }

                        value = normalizedSourcePath;
                    }

                    var option = new CommandLineOption(optionDescriptor, value);
                    if (!options.TryAdd(optionDescriptor.OptionType, option))
                    {
                        string errorMessage = $"Duplicate command option. The option '{optionDescriptor.OptionType}' can be specified only once.";
                        return new CommandParserResult(CommandLineCommand.Default, [errorMessage]);
                    }
                }
                else
                {
                    // Arguments without leading '-' or '--' are treated as Windows Terminal profile name alias
                    if (!validOptions.TryGetValue(CommandLineOption.AliasOptionKey, out CommandLineOptionDescriptor optionDescriptor))
                    {
                        string errorMessage = $"Command option '{CommandLineOption.AliasOptionKey}' has not been registered properly.";
                        throw new InvalidOperationException(errorMessage);
                    }

                    var option = new CommandLineOption(optionDescriptor, arg);
                    if (!options.TryAdd(optionDescriptor.OptionType, option))
                    {
                        string errorMessage = $"Duplicate command option. A Windows Terminal profile name alias can be specified only once.";
                        return new CommandParserResult(CommandLineCommand.Default, [errorMessage]);
                    }
                }
            }
        }
        else
        {
            // Ensure a command without any arguments is executed as default 'Launch-Windows-Terminal' command
            // that uses the current workiing directory and the terminal's default profile

            if (!validOptions.TryGetValue(CommandLineOption.AliasOptionKey, out CommandLineOptionDescriptor optionDescriptor))
            {
                string errorMessage = $"Command option '{CommandLineOption.AliasOptionKey}' has not been registered properly.";
                throw new InvalidOperationException(errorMessage);
            }

            var option = new CommandLineOption(optionDescriptor, string.Empty);
            if (!options.TryAdd(optionDescriptor.OptionType, option))
            {
                string errorMessage = $"Duplicate command option. A Windows Terminal profile name alias can be specified only once.";
                return new CommandParserResult(CommandLineCommand.Default, [errorMessage]);
            }
        }

        string providedTerminalProfile = options
            .GetValueOrDefault(CommandLineOptionId.LaunchWindowsTerminal)
            .Value;
        var immutableOptionsTable = options.ToImmutableDictionary(CommandLineOptionIdComparer.Instance);
        AliasResolverResult providedProfileAliasLookupResult = AliasResolver.CreateAlias(providedTerminalProfile, userConfiguration);
        if (providedProfileAliasLookupResult.HasError)
        {
            return new CommandParserResult(CommandLineCommand.Default, [providedProfileAliasLookupResult.ErrorMessage]);
        }

        AliasResolverResult defaultProfileLookupResult = AliasResolver.CreateAlias(userConfiguration.DefaultProfileValue, userConfiguration);
        if (defaultProfileLookupResult.HasError)
        {
            return new CommandParserResult(CommandLineCommand.Default, [defaultProfileLookupResult.ErrorMessage]);
        }

        CommandContext context = CreateCommandContext(userConfiguration, immutableOptionsTable);
        var arguments = new CommandArguments(providedProfileAliasLookupResult.TerminalProfile, defaultProfileLookupResult.TerminalProfile, immutableOptionsTable);
        
        return new CommandParserResult(new CommandLineCommand(arguments, context), []);
    }

    private static CommandContext CreateCommandContext(UserConfiguration configuration, ImmutableDictionary<CommandLineOptionId, CommandLineOption> options)
    {
        ExecutionMode executionMode = options.ContainsKey(CommandLineOptionId.RunAsAdmin) 
            ? ExecutionMode.Admin 
            : ExecutionMode.Normal;
        string launchMode = configuration.IsReuseTerminalWindowEnabled 
            ? LaunchModes.LastActiveWindow 
            : LaunchModes.NewWindow;

        return new CommandContext(launchMode, executionMode);
    }
}
