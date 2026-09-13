namespace Main;

internal readonly record struct CommandLineOption(string Name, CommandLineOptionId OptionType, string Description, bool IsOptional)
{
    public static implicit operator CommandLineOptionId(CommandLineOption option) => option.OptionType;
    public static implicit operator CommandLineOption(CommandLineOptionId optionType) => new(string.Empty, optionType, string.Empty, false);
};
