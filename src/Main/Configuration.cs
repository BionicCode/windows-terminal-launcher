namespace Main;

using System.Collections.Immutable;

internal record Configuration(
    ImmutableHashSet<Alias> Aliases, 
    ImmutableDictionary<string, Alias> AliasMap, 
    string DefaultAlias, 
    bool ReuseTerminalWindow);
