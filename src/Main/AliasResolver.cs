namespace Main;

internal static class AliasResolver
{
    public static async Task<Alias> CreateAliasAsync(string aliasKey, Configuration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        bool isUsingDefaultAlias = false;
        if (string.IsNullOrWhiteSpace(aliasKey))
        {
            aliasKey = configuration.DefaultAlias;
            isUsingDefaultAlias = true;
        }

        Alias? resolvedAlias;
        if (string.IsNullOrWhiteSpace(aliasKey))
        {
            resolvedAlias = CreateDefaultAlias();
        }
        else
        {
            if (!configuration.AliasMap.TryGetValue(aliasKey, out resolvedAlias))
            {
                string message = isUsingDefaultAlias 
                    ? $"The default alias '{aliasKey}' provided in the configuration YAML file is not defined."
                    : $"The provided alias '{aliasKey}' is not defined in the configuration YAML file.";

                throw new InvalidCommandArgumnentException(message);
            }
        }

        return resolvedAlias!;
    }

    public static Alias CreateDefaultAlias() => new(string.Empty, string.Empty, true);
}
