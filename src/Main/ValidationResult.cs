namespace Main;

using System.Collections.Immutable;

internal class ValidationResult
{
    public static ValidationResult ValidResult { get; } = new ValidationResult([]);
    public ImmutableArray<string> ErrorMessages { get; }

    public bool HasErrors => !ErrorMessages.IsEmpty;

    public ValidationResult(ImmutableArray<string> errorMessages) => ErrorMessages = errorMessages;
}