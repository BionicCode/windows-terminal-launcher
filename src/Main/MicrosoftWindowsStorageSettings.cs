namespace Main;

using System.Diagnostics.CodeAnalysis;
using Microsoft.Windows.Storage;

internal class MicrosoftWindowsStorageSettings : IApplicationSettings
{
    private static readonly ApplicationDataContainer s_settings = ApplicationData.GetForUnpackaged(
        publisher: "BionicCode",
        product: "lit").LocalSettings;

    private static readonly object s_syncRoot = new();

    public TValue GetOrSetValue<TValue>(AppSettingsEntryDescriptor<TValue> settingsKeyDescriptor, Func<AppSettingsEntryDescriptor<TValue>, TValue?> valueFactory)
        where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(settingsKeyDescriptor);
        ArgumentNullException.ThrowIfNull(valueFactory);

        lock (s_syncRoot)
        {
            if (!s_settings.Values.TryGetValue(settingsKeyDescriptor.Key, out object? value))
            {
                // Produce new value
                value = valueFactory.Invoke(settingsKeyDescriptor) ?? throw new InvalidOperationException($"The argument '{nameof(valueFactory)}' must not return NULL.");
                if (value is not TValue typedValue)
                {
                    throw new InvalidOperationException(
                        $"Application setting '{settingsKeyDescriptor.Key}' contains a value of type " +
                        $"'{value?.GetType().FullName ?? "<null>"}', but " +
                        $"'{typeof(TValue).FullName}' was requested.");
                }

                if (!s_settings.Values.TryAdd(settingsKeyDescriptor.Key, typedValue))
                {
                    throw new InvalidOperationException("Application initializuation error. Unable to configure application settings");
                }
            }

            return (TValue)value;
        }
    }

    public bool TryAdd<TValue>(AppSettingsEntryDescriptor<TValue> settingsKeyDescriptor, TValue value)
        where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(settingsKeyDescriptor);
        ArgumentNullException.ThrowIfNull(value);

        lock (s_syncRoot)
        {
            if (s_settings.Values.TryGetValue(settingsKeyDescriptor.Key, out _))
            {
                return false;
            }

            if (!s_settings.Values.TryAdd(settingsKeyDescriptor.Key, value))
            {
                throw new InvalidOperationException("Application initializuation error. Unable to configure application settings");
            }

            return true;
        }
    }

    public void AddOrUpdate<TValue>(AppSettingsEntryDescriptor<TValue> settingsKeyDescriptor, TValue value)
        where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(settingsKeyDescriptor);
        ArgumentNullException.ThrowIfNull(value);

        lock (s_syncRoot)
        {
            s_settings.Values[settingsKeyDescriptor.Key] = value;
        }
    }

    public bool TryGet<TValue>(AppSettingsEntryDescriptor<TValue> settingsKeyDescriptor, [NotNullWhen(true)] out TValue value) where TValue : notnull
    {
        ArgumentNullException.ThrowIfNull(settingsKeyDescriptor);

        lock (s_syncRoot)
        {
            value = default;
            if (s_settings.Values.TryGetValue(settingsKeyDescriptor.Key, out object? rawValue)
                && rawValue is TValue typedValue)
            {
                value = typedValue;

                return true;
            }

            return false;
        }
    }
}

