namespace Main;

internal readonly record struct CommandLineOption(
    string Name, 
    string AlternativeName,
    CommandLineOptionId OptionType, 
    string Description, 
    bool IsOptional)
{
    public static implicit operator CommandLineOptionId(CommandLineOption option) => option.OptionType;
    public static implicit operator CommandLineOption(CommandLineOptionId optionType) => new(string.Empty, string.Empty, optionType, string.Empty, false);
};
