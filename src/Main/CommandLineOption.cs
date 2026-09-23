namespace Main;

using System.Collections.Immutable;

internal readonly record struct CommandLineOption(
    CommandLineOptionDescriptor Descriptor,
    string Value)
{
    public static implicit operator CommandLineOptionId(CommandLineOption option) => option.Descriptor.OptionType;
    public static implicit operator CommandLineOption(CommandLineOptionId optionType) => new(new (string.Empty, string.Empty, optionType, CommandLineOptionKind.Undefined, [], string.Empty, false), string.Empty);
};

internal readonly record struct CommandLineOptionDescriptor(
    string Name,
    string AlternativeName,
    CommandLineOptionId OptionType,
    CommandLineOptionKind Kind,
    ImmutableArray<string> DescriptionLines,
    string Example,
    bool IsOptional)
{
    public bool HasAlternativeName => !string.IsNullOrEmpty(AlternativeName);
};
