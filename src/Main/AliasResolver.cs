namespace Main;

internal static class AliasResolver
{
    public static async Task<Alias> CreateAliasAsync(string aliasKey)
    {
        Alias? resolvedAlias;
        if (string.IsNullOrWhiteSpace(aliasKey))
        {
            resolvedAlias = CreateDefaultAlias();
        }
        else
        {
            Configuration configuration = await ConfigurationReader.ReadConfigurationAsync();
            if (!configuration.AliasMap.TryGetValue(aliasKey, out resolvedAlias))
            {
                throw new InvalidCommandArgumnentException($"Alias '{aliasKey}' not found.");
            }
        }

        return resolvedAlias!;
    }

    public static Alias CreateDefaultAlias() => new(string.Empty, string.Empty, true);
}
