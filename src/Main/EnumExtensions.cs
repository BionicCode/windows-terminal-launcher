namespace Main;

using System;
using System.Collections.Generic;
using System.Text;

internal static class EnumExtensions
{
    public static string ToDisplayString(this CommandLineOptionKind optionKind) => optionKind switch
    {
        CommandLineOptionKind.Undefined => "Undefined",
        CommandLineOptionKind.Mode => "Mode",
        CommandLineOptionKind.Value => "Key-VariableName_PATH",
        CommandLineOptionKind.Flag => "Flag",
        CommandLineOptionKind.ModeAndValue => "Mode-And-VariableName_PATH",
        _ => throw new NotSupportedException($"The value '{optionKind}' is not a known value of the enum '{nameof(CommandLineOptionId)}''")
    };

    public static string ToDisplayString(this CommandLineOptionId optionId) => optionId switch
    {
        CommandLineOptionId.Undefined => "Undefined",
        CommandLineOptionId.EnvironmentVariableScopeMachine => "machine",
        CommandLineOptionId.EnvironmentVariableScopeUser => "user",
        _ => Enum.IsDefined(optionId) 
            ? Enum.GetName(optionId)!
            : throw new NotSupportedException($"The value '{optionId}' is not a known value of the enum '{nameof(CommandLineOptionId)}''")
    };
}
