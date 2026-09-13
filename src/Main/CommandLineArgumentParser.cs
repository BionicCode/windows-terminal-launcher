namespace Main;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

internal static class CommandLineArgumentParser
{
    /// <summary>
    /// Parses the command line arguments and returns a CommandLineCommand object containing the alias and options.
    /// </summary>
    /// <param name="arguments">The command line arguments to parse.</param>
    /// <param name="validOptions">The lookup table of valid command options.</param>
    /// <returns>A <see cref="CommandLineCommand"/> object containing the parsed command arguments like Windows Terminal profile alias and command options.</returns>
    /// <exception cref="InvalidCommandArgumnentException">Thrown when an invalid command argument is encountered.</exception>
    /// <remarks>Expects a command syntax of the form: <c>lit.exe [alias] [options]...</c></remarks>
    public async static Task<CommandLineCommand> CreateCommandAsync(string[]? arguments, IReadOnlyDictionary<string, CommandLineOption> validOptions)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(validOptions);

        string aliasKey = string.Empty;
        if (arguments.Length == 0)
        {
            Alias defaultAlias = AliasResolver.CreateDefaultAlias();
            return new CommandLineCommand(defaultAlias, [], CommandContext.Default);
        }

        var options = new HashSet<CommandLineOption>(CommandLineOptionIdComparer.Instance);
        foreach (string arg in arguments)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                continue;
            }

            // Must be an option if it starts with a dash
            if (arg.StartsWith('-'))
            {
                if (!validOptions.TryGetValue(arg, out CommandLineOption option))
                {
                    throw new InvalidCommandArgumnentException($"Invalid command option '{arg}'. Use '[-h | --help]' to see the list of valid options.");
                }

                _ = options.Add(option);
            }
            else
            {
                // Only one alias can be specified, so if we already have an alias, throw an exception
                if (!string.IsNullOrEmpty(aliasKey))
                {
                    throw new InvalidCommandArgumnentException("Only one alias a rgument can be specified.");
                }

                aliasKey = arg;

            }
        }

        var immutableOptions = options.ToImmutableHashSet(CommandLineOptionIdComparer.Instance);
        Configuration configuration = await ConfigurationReader.ReadConfigurationAsync();
        Alias alias = await AliasResolver.CreateAliasAsync(aliasKey, configuration);
        CommandContext context = CreateCommandContext(configuration, immutableOptions);

        return new CommandLineCommand(alias, immutableOptions, context);
    }

    private static CommandContext CreateCommandContext(Configuration configuration, ImmutableHashSet<CommandLineOption> options)
    {
        ExecutionMode executionMode = options.Contains(CommandLineOptionId.RunAsAdmin) 
            ? ExecutionMode.Admin 
            : ExecutionMode.Normal;
        string launchMode = configuration.ReuseTerminalWindow 
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

    public bool Equals(CommandLineOption x, CommandLineOption y) => Equals(x.OptionType, y.OptionType);
    public bool Equals(CommandLineOptionId x, CommandLineOption y) => Equals(x, y.OptionType);
    public bool Equals(CommandLineOption x, CommandLineOptionId y) => Equals(x.OptionType, y);

    public bool Equals(CommandLineOptionId x, CommandLineOptionId y) => x == y;

    public int GetHashCode(CommandLineOption obj) => GetHashCode(obj.OptionType);

    public int GetHashCode([DisallowNull] CommandLineOptionId obj) => obj.GetHashCode();
}
