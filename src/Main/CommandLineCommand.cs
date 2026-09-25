namespace Main;

using BionicCode.Utilities.Net;

internal readonly record struct CommandLineCommand(CommandArguments Arguments, CommandContext Context)
{
    public bool HasMode => Arguments.HasMode;
    public bool HasAlias => Arguments.HasAlias;
    public CommandLineOptionId Mode => Arguments.Mode;

    private readonly WriteOnce<CommandLineOption> _commandIdProviderOption = new ();
    public CommandLineOption CommandIdProviderOption => _commandIdProviderOption.IsSet
        ? _commandIdProviderOption
        : _commandIdProviderOption.SetValue(Arguments.OptionsTable.Values.First(option => option.Descriptor.Kind is CommandLineOptionKind.Mode or CommandLineOptionKind.ModeAndValue ;

    private readonly WriteOnce<string> _name = new ();
    public string Name => _name.IsSet
        ? _name
        : _name.SetValue(Arguments.OptionsTable.Values
            .Select(option => option.Descriptor.CommandName)
            .FirstOrDefault(commandName => !string.IsNullOrWhiteSpace(commandName)) ?? string.Empty);

    public static readonly CommandLineCommand Default = new CommandLineCommand(CommandArguments.Default, CommandContext.Default);
};
