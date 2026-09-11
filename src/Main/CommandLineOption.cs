namespace Main;

internal readonly record struct CommandLineOption(string Name, CommandLineOptionType OptionType, string Description, bool IsOptional)
{
    public static implicit operator CommandLineOptionType(CommandLineOption option) => option.OptionType;
    public static implicit operator CommandLineOption(CommandLineOptionType optionType) => new(string.Empty, optionType, string.Empty, false);
};
