namespace Main;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

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
            return new CommandLineCommand(defaultAlias, []);
        }

        var options = new List<CommandLineOption>();
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

                options.Add(option);
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

        Alias alias = await AliasResolver.CreateAliasAsync(aliasKey);
        return new CommandLineCommand(alias, options.ToImmutableList());
    }
}
