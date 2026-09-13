namespace Main;

internal readonly record struct CommandContext(string LaunchMode, ExecutionMode ExecutionMode)
{
    /// <summary>
    /// Gets the default command context, which uses the last active window as the launch mode.
    /// </summary>
    public static CommandContext Default => new CommandContext(LaunchModes.LastActiveWindow, ExecutionMode.Normal);
};
