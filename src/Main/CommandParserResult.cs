namespace Main;

using System.Collections.Immutable;

internal record class CommandParserResult(CommandLineCommand Command, ImmutableArray<string> ErrorMessages)
{
    public bool HasErrors => ErrorMessages.Any();
};
