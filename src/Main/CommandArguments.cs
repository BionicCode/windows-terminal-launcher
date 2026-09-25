namespace Main;

using System.Collections.Immutable;
using BionicCode.Utilities.Net;

internal readonly record struct CommandArguments(
    TerminalProfile TerminalProfile, 
    TerminalProfile DefaultTerminalProfile,
    ImmutableDictionary<CommandLineOptionId, CommandLineOption> OptionsTable)
{
    public static readonly CommandArguments Default = new CommandArguments(TerminalProfile.Default, TerminalProfile.Default, ImmutableDictionary<CommandLineOptionId, CommandLineOption>.Empty);

    private readonly WriteOnce<bool> _hasMode = new();
    private readonly WriteOnce<CommandLineOptionId> _mode = new();
    public bool HasAlias => OptionsTable.ContainsKey(CommandLineOptionId.LaunchWindowsTerminal);
    public bool HasOptions => OptionsTable is not null && OptionsTable.Count > 0;
    public bool HasMode => _hasMode.IsSet
        ? _hasMode
        : _hasMode.SetValue(HasOptions && OptionsTable.Any(option => option.Value.Descriptor.Kind is CommandLineOptionKind.Mode or CommandLineOptionKind.ModeAndValue));
    public CommandLineOptionId Mode => _mode.IsSet
        ? _mode
        : _mode.SetValue(HasMode
            ? OptionsTable.First(entry => entry.Value.Descriptor.Kind is CommandLineOptionKind.Mode or CommandLineOptionKind.ModeAndValue).Value.Descriptor.OptionType
            : CommandLineOptionId.Undefined);
};