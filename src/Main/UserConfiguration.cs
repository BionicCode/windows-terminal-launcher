namespace Main;

using System.Collections.Immutable;

internal record UserConfiguration(
    ImmutableHashSet<TerminalProfile> TerminalProfiles, 
    ImmutableDictionary<string, TerminalProfile> TerminalProfileMap, 
    string DefaultProfileValue, 
    bool IsReuseTerminalWindowEnabled,
    string Location);
