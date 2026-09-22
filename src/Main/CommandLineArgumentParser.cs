namespace Main;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;

internal static class CommandLineArgumentParser
{
    /// <summary>
    /// Parses the command line arguments and returns a CommandLineCommand object containing the alias and options.
    /// </summary>
    /// <param name="rawArguments">The command line arguments to parse.</param>
    /// <param name="validOptions">The lookup table of valid command options.</param>
    /// <returns>A <see cref="CommandLineCommand"/> object containing the parsed command arguments like Windows Terminal profile alias and command options.</returns>
    /// <exception cref="InvalidCommandArgumentException">Thrown when an invalid command argument is encountered.</exception>
    /// <remarks>Expects a command syntax of the form: <c>lit [alias] [options...]</c></remarks>
    public async static Task<CommandLineCommand> CreateCommandAsync(
        string[]? rawArguments, 
        IReadOnlyDictionary<string, CommandLineOptionDescriptor> validOptions, 
        string configFilePath)
    {
        ArgumentNullException.ThrowIfNull(rawArguments);
        ArgumentNullException.ThrowIfNull(validOptions);

        var options = new Dictionary<CommandLineOptionId, CommandLineOption>(CommandLineOptionIdComparer.Instance);
        string providedTerminalProfile = string.Empty;
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
                        throw new InvalidCommandArgumentException($"Invalid command option '{arg}' at argument index '{index}'.{Environment.NewLine}Use '[-h | --help]' to see the list of valid options.");
                    }

                    string value = optionDescriptor.Kind is CommandLineOptionKind.Value or CommandLineOptionKind.ModeAndValue
                        ? rawArguments[++index]
                        : string.Empty;

                    if (optionDescriptor.OptionType is CommandLineOptionId.SourcePath or CommandLineOptionId.DestinationPath)
                    {
                        if (!TryNormalizeWindowsPath(value, out string? normalizedSourcePath))
                        {
                            throw new InvalidCommandArgumentException($"Invalid source path argument at argument index '{index}'. The path is malformed.");
                        }

                        value = normalizedSourcePath;
                    }

                    var option = new CommandLineOption(optionDescriptor, value);
                    options.Add(optionDescriptor.OptionType, option);
                }
                else
                {
                    // Only one alias can be specified, so if we already have an alias, throw an exception
                    if (!string.IsNullOrEmpty(providedTerminalProfile))
                    {
                        throw new InvalidCommandArgumentException($"Malformed command line arguments.{Environment.NewLine}Only one alias argument can be specified.");
                    }

                    providedTerminalProfile = arg;

                }
            }
        }

        var immutableOptionsTable = options.ToImmutableDictionary(CommandLineOptionIdComparer.Instance);
        Configuration configuration = await ConfigurationReader.ReadConfigurationAsync(configFilePath);
        TerminalProfile providedProfile = await AliasResolver.CreateAliasAsync(providedTerminalProfile, configuration);
        TerminalProfile defaultProfile = await AliasResolver.CreateAliasAsync(configuration.DefaultProfileValue, configuration);
        CommandContext context = CreateCommandContext(configuration, immutableOptionsTable);
        var arguments = new CommandArguments(providedProfile, defaultProfile, immutableOptionsTable);
        
        return new CommandLineCommand(arguments, context);
    }

    private static bool TryNormalizeWindowsPath(
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

    private static bool IsLexicallyValidPath(string path)
    {
        // Check for invalid characters
        SearchValues<char> invalidPathChars = SearchValues.Create(Path.GetInvalidPathChars());
        if (path.ContainsAny(invalidPathChars))
        {
            return false;
        }

        // Optional: Check for invalid file name characters in segments
        string[] segments = path.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        SearchValues<char> invalidNameChars = SearchValues.Create(Path.GetInvalidFileNameChars());
        foreach (string segment in segments)
        {
            if (segment.ContainsAny(invalidNameChars))
            {
                return false;
            }
        }

        return true;
    }

    private static CommandContext CreateCommandContext(Configuration configuration, ImmutableDictionary<CommandLineOptionId, CommandLineOption> options)
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

internal sealed class CommandLineOptionIdComparer : 
    IEqualityComparer<CommandLineOption>,
    IEqualityComparer<CommandLineOptionId>
{
    public static CommandLineOptionIdComparer Instance { get; } = new CommandLineOptionIdComparer();

    private CommandLineOptionIdComparer() { }

    public bool Equals(CommandLineOption x, CommandLineOption y) => Equals(x.Descriptor.OptionType, y.Descriptor.OptionType);
    public bool Equals(CommandLineOptionId x, CommandLineOption y) => Equals(x, y.Descriptor.OptionType);
    public bool Equals(CommandLineOption x, CommandLineOptionId y) => Equals(x.Descriptor.OptionType, y);

    public bool Equals(CommandLineOptionId x, CommandLineOptionId y) => x == y;

    public int GetHashCode(CommandLineOption obj) => GetHashCode(obj.Descriptor.OptionType);

    public int GetHashCode([DisallowNull] CommandLineOptionId obj) => obj.GetHashCode();
}
