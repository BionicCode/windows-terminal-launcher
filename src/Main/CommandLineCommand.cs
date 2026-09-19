namespace Main;

using BionicCode.Utilities.Net;

internal readonly record struct CommandLineCommand(CommandArguments Arguments, CommandContext Context)
{
    public bool HasMode => Arguments.HasMode;
    public bool HasAlias => Arguments.HasAlias;

    public static readonly CommandLineCommand Default = new CommandLineCommand(CommandArguments.Default, CommandContext.Default);
};
