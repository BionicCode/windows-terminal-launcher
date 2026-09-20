namespace Main;

using System.Collections.Immutable;
using BionicCode.Utilities.Net;

internal readonly record struct CommandArguments(
    Alias Alias, 
    ImmutableDictionary<CommandLineOptionId, CommandLineOption> OptionsTable)
{
    public static readonly CommandArguments Default = new CommandArguments(Alias.Default, ImmutableDictionary<CommandLineOptionId, CommandLineOption>.Empty);

    private readonly WriteOnce<bool> _hasMode = new();
    private readonly WriteOnce<CommandLineOptionId> _mode = new();
    public bool HasAlias => Alias is not null && !string.IsNullOrWhiteSpace(Alias.ResolvedName);
    public bool HasOptions => OptionsTable is not null && OptionsTable.Count > 0;
    public bool HasMode => _hasMode.IsSet
        ? _hasMode
        : _hasMode.SetValue(HasOptions && OptionsTable.Any(option => option.Value.Descriptor.Kind is CommandLineOptionKind.Mode));
    public CommandLineOptionId Mode => _mode.IsSet
        ? _mode
        : _mode.SetValue(HasMode
            ? OptionsTable.First(entry => entry.Value.Descriptor.Kind is CommandLineOptionKind.Mode).Value.Descriptor.OptionType
            : CommandLineOptionId.Undefined);
};