namespace Main;

using System.Collections.Immutable;

internal readonly record struct CommandLineCommand(Alias Alias, ImmutableHashSet<CommandLineOption> Options, CommandContext Context)
{
    public static readonly CommandLineCommand Default = new CommandLineCommand(Alias.Default, ImmutableHashSet<CommandLineOption>.Empty, CommandContext.Default);
};
