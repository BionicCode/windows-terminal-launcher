namespace Main;

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

internal sealed class CommandLineOptionIdComparer : 
    IEqualityComparer<CommandLineOption>,
    IEqualityComparer<CommandLineOptionId>
{
    public static CommandLineOptionIdComparer Instance { get; } = new CommandLineOptionIdComparer();

    private CommandLineOptionIdComparer() { }

    public bool Equals(CommandLineOption x, CommandLineOption y) => Equals(x.Descriptor.OptionType, y.Descriptor.OptionType);
    public bool Equals(CommandLineOptionId x, CommandLineOption y) => Equals(x, y.Descriptor.OptionType);
    public bool Equals(CommandLineOption x, CommandLineOptionId y) => Equals(x.Descriptor.OptionType, y);

    public bool Equals(CommandLineOptionId x, CommandLineOptionId y) => x == y;

    public int GetHashCode(CommandLineOption obj) => GetHashCode(obj.Descriptor.OptionType);

    public int GetHashCode([DisallowNull] CommandLineOptionId obj) => obj.GetHashCode();
}
