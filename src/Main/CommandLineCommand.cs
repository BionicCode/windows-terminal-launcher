namespace Main;

using System.Collections.Immutable;

internal readonly record struct CommandLineCommand(Alias Alias, ImmutableHashSet<CommandLineOption> Options, CommandContext Context);
