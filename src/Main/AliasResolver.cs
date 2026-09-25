namespace Main;

using System.Configuration;

internal static class AliasResolver
{
    public static async Task<AliasResolverResult> CreateAliasAsync(string aliasToResolve, Configuration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        TerminalProfile? resolvedProfile;
        string errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(aliasToResolve))
        {
            resolvedProfile = TerminalProfile.Default;
        }
        else
        {
            if (!configuration.TerminalProfileMap.TryGetValue(aliasToResolve, out resolvedProfile))
            {
                // The provided token is obviiously not the alias but maybe the full profile name instead
                resolvedProfile = configuration.TerminalProfileMap.
                    Select(entry => entry.Value)
                    .FirstOrDefault(terminalProfile => terminalProfile.Name.Equals(aliasToResolve, StringComparison.OrdinalIgnoreCase))
                    ?? TerminalProfile.Default;
                if (resolvedProfile == TerminalProfile.Default)
                { 
                    errorMessage = $"The provided alias '{aliasToResolve}' is not defined in the configuration YAML file.";
                }
            }
        }

        return new AliasResolverResult(resolvedProfile!, errorMessage);
    }
}

internal record class AliasResolverResult(TerminalProfile TerminalProfile, string ErrorMessage)
{
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
};