namespace Main;

using System.Collections.Generic;

internal sealed class YamlConfiguration
{
    public required Dictionary<string, string> TerminalProfiles { get; set; } = [];

    public bool ReuseTerminalWindow { get; set; } = true;

    public string? DefaultAlias { get; set; } = string.Empty;
}
