namespace Main;

using System;

/// <summary>
/// Exception thrown when an invalid command argument is encountered.
/// </summary>
public class InvalidCommandArgumentException : Exception
{
    public InvalidCommandArgumentException(string message) : base(message)
    {
    }

    public InvalidCommandArgumentException() : this("Invalid command argument.")
    {
    }

    public InvalidCommandArgumentException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
