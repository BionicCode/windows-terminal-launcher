namespace Main;

internal enum CommandLineOptionId
{
    Undefined = 0,
    LaunchWindowsTerminal,
    Help,
    Version,
    ListAliases,
    RunAsAdmin,
    /// <summary>
    /// Mode option.
    /// </summary>
    GetOrSetConfigLocation,
    SourcePath,
    DestinationPath,
    /// <summary>
    /// Mode option.
    /// </summary>
    GetOrSetEnvironmentVariable,
    EnvironmentVariableValue,
    EnvironmentVariableScopeUser,
    EnvironmentVariableScopeMachine,
    EnvironmentVariableWriteModeJoin,
    EnvironmentVariableWriteModeJoinDelimiter,
    EnvironmentVariableWriteModeReplace,
    Print,
    FoldPath
}
