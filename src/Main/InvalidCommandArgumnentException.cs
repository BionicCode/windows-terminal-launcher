namespace Main;

using System;

/// <summary>
/// Exception thrown when an invalid command argument is encountered.
/// </summary>
public class InvalidCommandArgumnentException : Exception
{
    public InvalidCommandArgumnentException(string message) : base(message)
    {
    }

    public InvalidCommandArgumnentException() : this("Invalid command argument.")
    {
    }

    public InvalidCommandArgumnentException(string message, Exception innerException) : base(message, innerException)
    {
    }
}