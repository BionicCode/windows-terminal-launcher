namespace Main;

using System.Collections.Immutable;

internal readonly record struct CommandLineOption(
    CommandLineOptionDescriptor Descriptor,
    string Value)
{
    public const string AliasOptionKey = "<terminal-profile-alias>";
};

internal readonly record struct CommandLineOptionDescriptor(
    string Name,
    string AlternativeName,
    CommandLineOptionId OptionType,
    CommandLineOptionKind Kind,
    string CommandName,
    ImmutableArray<string> DescriptionLines,
    string Example,
    bool IsOptional)
{
    public bool HasAlternativeName => !string.IsNullOrEmpty(AlternativeName);
};
