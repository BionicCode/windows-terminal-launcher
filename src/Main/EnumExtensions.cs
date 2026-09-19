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
        CommandLineOptionKind.Value => "Key-Value",
        CommandLineOptionKind.Flag => "Flag",
        _ => throw new NotSupportedException($"The value '{optionKind}' is not a known value of the enum '{nameof(CommandLineOptionId)}''")
    };
}
