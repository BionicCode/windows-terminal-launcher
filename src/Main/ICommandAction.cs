namespace Main;

internal interface ICommandAction
{
    public CommandExitMode Execute(CommandLineCommand command, IApplicationSettings applicationSettings);
}
