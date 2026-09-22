namespace Main;

internal static class AliasResolver
{
    public static async Task<TerminalProfile> CreateAliasAsync(string aliasToResolve, Configuration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        TerminalProfile? resolvedProfile;
        if (string.IsNullOrWhiteSpace(aliasToResolve))
        {
            resolvedProfile = CreateDefaultAlias();
        }
        else
        {
            if (!configuration.TerminalProfileMap.TryGetValue(aliasToResolve, out resolvedProfile))
            {
                // The provided token is obviiously not the alias but maybe the full profile name instead
                resolvedProfile = configuration.TerminalProfileMap.
                    Select(entry => entry.Value)
                    .FirstOrDefault(terminalProfile => terminalProfile.Name.Equals(aliasToResolve, StringComparison.OrdinalIgnoreCase));
                if (resolvedProfile is null)
                { 
                    string message = $"The provided alias '{aliasToResolve}' is not defined in the configuration YAML file.";
                    throw new InvalidCommandArgumentException(message);
                }
            }
        }

        return resolvedProfile!;
    }

    public static TerminalProfile CreateDefaultAlias() => new(string.Empty, string.Empty, true);
}