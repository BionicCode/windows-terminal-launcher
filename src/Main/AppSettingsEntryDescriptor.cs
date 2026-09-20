namespace Main;

using System.Runtime.CompilerServices;

public class AppSettingsEntryDescriptor<TValue>
    where TValue : notnull
{
    public AppSettingsKeyId Id { get; }
    public string Description { get; }

    private string? _key;
    public string Key => _key ??= Enum.GetName(Id)!;

    public AppSettingsEntryDescriptor(AppSettingsKeyId id, string description)
    {
        ThrowIfEnumIsInvalid(id);
        Id = id;
        Description = description;
    }

    private static void ThrowIfEnumIsInvalid(AppSettingsKeyId settingsKey, [CallerArgumentExpression(nameof(settingsKey))] string? argumentName = null)
    {
        if (!Enum.IsDefined(settingsKey))
        {
            throw new ArgumentOutOfRangeException(nameof(argumentName), $"Invalid argument '{nameof(settingsKey)}'. The enum value '{settingsKey}' is not defined in '{typeof(AppSettingsKeyId).FullName}'.");
        }

        if (settingsKey is AppSettingsKeyId.Undefined)
        {
            throw new ArgumentOutOfRangeException(nameof(settingsKey), $"Invalid argument '{nameof(settingsKey)}'. The enum value '{settingsKey}' is not allowed.");
        }
    }
}

