namespace Main;

using System;
using System.Collections.Generic;
using System.Text;

internal static class ExceptionHelpers
{
    public static void ThrowArgumentExceptionIfWrongCommandType(CommandLineOptionId currentCommandType, CommandLineOptionId expectedCommandType)
    {
        if (currentCommandType != expectedCommandType)
        {
            throw new ArgumentException($"Wrong command type. Expected: '{Enum.GetName(expectedCommandType)}'. Found: '{Enum.GetName(currentCommandType)}.");
        }
    }
}
