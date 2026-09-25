namespace Main;

using System.Collections.Immutable;

internal abstract class CommandAction : ICommandAction
{
    protected const int LineIndentation = 4;
    protected const int Padding = 4;
    public CommandLineOptionId TargetCommandType { get; }

    protected CommandAction(CommandLineOptionId targetComandType) => TargetCommandType = targetComandType;

    protected abstract CommandExitMode ExecuteInternal(CommandLineCommand command, IApplicationSettings applicationSettings, UserConfiguration userConfiguration, ImmutableDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validCommandOptionsTable);
    
    public CommandExitMode Execute(CommandLineCommand command, IApplicationSettings applicationSettings, UserConfiguration userConfiguration, ImmutableDictionary<CommandLineOptionId, CommandLineOptionDescriptor> validCommandOptionsTable)
    {
        ExceptionHelpers.ThrowArgumentExceptionIfWrongCommandType(command.CommandIdProviderOption.Descriptor.OptionType, TargetCommandType);
        ArgumentNullException.ThrowIfNull(applicationSettings);
        ArgumentNullException.ThrowIfNull(userConfiguration);
        ArgumentNullException.ThrowIfNull(validCommandOptionsTable);
        ArgumentOutOfRangeException.ThrowIfZero(validCommandOptionsTable.Count);

        return ExecuteInternal(command, applicationSettings, userConfiguration, validCommandOptionsTable);
    }
}