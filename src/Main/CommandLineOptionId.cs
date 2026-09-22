namespace Main;

internal enum CommandLineOptionId
{
    Undefined = 0,
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
    SetEnvironmentVariable,
    EnvironmentVariableName,
    EnvironmentVariableValue,
    EnvironmentVariableScopeUser,
    EnvironmentVariableScopeMachine,
    EnvironmentVariableWriteModeJoin,
    EnvironmentVariableWriteModeJoinDelimiter,
    EnvironmentVariableWriteModeReplace,
    Print
}
