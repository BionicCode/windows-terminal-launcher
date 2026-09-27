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

    internal static void ThrowArgumentExceptionIfWrongCommandType(CommandLineOptionId currentCommandType, HashSet<CommandLineOptionId> expectedCommandTypes)
    {
        ArgumentNullException.ThrowIfNull(expectedCommandTypes);

        if (!expectedCommandTypes.Contains(currentCommandType))
        {
            string allowedValuesString = string.Join(", ", expectedCommandTypes.Select(expectedCommandType => Enum.GetName(expectedCommandType)));
            throw new ArgumentException($"Wrong command type. Expected: '{allowedValuesString}'. Found: '{Enum.GetName(currentCommandType)}.");
        }
    }
}
