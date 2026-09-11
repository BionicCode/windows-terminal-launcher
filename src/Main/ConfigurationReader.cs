namespace Main;

using System;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

internal static class ConfigurationReader
{
    private const string ConfigYamlFileName = @"config.yaml";
    private const string RelativeConfigYamlFilePath = @"\Config\config.yaml";
    private static readonly string s_configFilePath = Path.Combine(Environment.ProcessPath ?? Environment.CurrentDirectory, RelativeConfigYamlFilePath);
    private static readonly FileStreamOptions s_fileStreamOptions = new FileStreamOptions
    {
        Mode = FileMode.Open,
        Access = FileAccess.Read,
        Share = FileShare.Read,
        Options = FileOptions.Asynchronous | FileOptions.SequentialScan
    };

    public static async Task<Configuration> ReadConfigurationAsync()
    {
        if (!File.Exists(s_configFilePath))
        {
            throw new FileNotFoundException($"Configuration file '{ConfigYamlFileName}' not found at location '{s_configFilePath}'.");
        }

        await using var configFile = new FileStream(s_configFilePath, s_fileStreamOptions);
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
        
        return CreateConfiguration(yamlConfiguration);
    }

    private static Configuration CreateConfiguration(YamlConfiguration yamlConfiguration)
    {
        string defaultAlias = yamlConfiguration.DefaultAlias ?? string.Empty;
        var aliases = yamlConfiguration.TerminalProfiles
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && !string.IsNullOrWhiteSpace(kvp.Value))
            .Select(kvp => new Alias(kvp.Key, kvp.Value, IsDefault: StringComparer.OrdinalIgnoreCase.Equals(kvp.Key, defaultAlias)))
            .ToImmutableHashSet();
        return new Configuration(
            aliases, 
            aliases.ToImmutableDictionary(alias => alias.Name, alias => alias),
            defaultAlias, 
            yamlConfiguration.ReuseTerminalWindow);
    }
}
