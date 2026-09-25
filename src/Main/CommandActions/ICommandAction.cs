namespace Main;

using System.Collections.Immutable;

internal interface ICommandAction
{
    public CommandExitMode Execute(CommandLineCommand command, IApplicationSettings applicationSettings, UserConfiguration userSettings, ImmutableDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validCommandOptionsTable);
}
