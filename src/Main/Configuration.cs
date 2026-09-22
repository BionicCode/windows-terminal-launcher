namespace Main;

using System.Collections.Immutable;

internal record Configuration(
    ImmutableHashSet<TerminalProfile> TerminalProfiles, 
    ImmutableDictionary<string, TerminalProfile> TerminalProfileMap, 
    string DefaultProfileValue, 
    bool IsReuseTerminalWindowEnabled,
    string Location);
