namespace Main;

using System;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

internal static class ConfigurationReader
{
    private static readonly FileStreamOptions s_fileStreamOptions = new FileStreamOptions
    {
        Mode = FileMode.Open,
        Access = FileAccess.Read,
        Share = FileShare.Read,
        Options = FileOptions.Asynchronous | FileOptions.SequentialScan
    };

    public static async Task<UserConfiguration> ReadConfigurationAsync(string configFilePath)
    {
        if (!File.Exists(configFilePath))
        {
            throw new FileNotFoundException($"UserConfiguration file not found at location '{configFilePath}'.");
        }

        await using var configFile = new FileStream(configFilePath, s_fileStreamOptions);
        using var reader = new StreamReader(configFile, Encoding.UTF8);
        string yamlContent = await reader.ReadToEndAsync();
        IDeserializer deserializer = new DeserializerBuilder()
            .WithDuplicateKeyChecking()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .WithEnforceRequiredMembers()
            .IgnoreUnmatchedProperties()
            .Build();

        // Wrap the deserialization in a Task.Run to avoid blocking the calling thread, especially if the YAML content is large.
        // Must do this because YamlDotNet deserializer is not e xpposing an asynchronous API and therefore potentially blocks the GUI noticeable enough.
        YamlConfiguration yamlConfiguration = await Task.Run(() => deserializer.Deserialize<YamlConfiguration>(yamlContent))
            .ConfigureAwait(true);
        
        return CreateConfiguration(yamlConfiguration, configFilePath);
    }

    private static UserConfiguration CreateConfiguration(YamlConfiguration yamlConfiguration, string configFilePath)
    {
        string defaultProfileValue = yamlConfiguration.DefaultTerminalProfile ?? string.Empty;
        var aliases = yamlConfiguration.TerminalProfiles
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
            .Select(kvp => new TerminalProfile(kvp.Key, kvp.Value, IsDefault: StringComparer.OrdinalIgnoreCase.Equals(kvp.Key, defaultProfileValue) || StringComparer.OrdinalIgnoreCase.Equals(kvp.Value, defaultProfileValue)))
            .ToImmutableHashSet();
        return new UserConfiguration(
            aliases, 
            aliases.ToImmutableDictionary(ptofile => ptofile.Alias, alias => alias),
            defaultProfileValue, 
            yamlConfiguration.ReuseTerminalWindow,
            configFilePath);
    }
}
