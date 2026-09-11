namespace Main;

using System.Collections.Immutable;

internal readonly record struct CommandLineCommand(Alias Alias, IImmutableList<CommandLineOption> Options, CommandContext Context);
