namespace Main;

using System.Diagnostics.CodeAnalysis;

internal interface IApplicationSettings
{
    void AddOrUpdate<TValue>(AppSettingsEntryDescriptor<TValue> settingsKeyDescriptor, TValue value) where TValue : notnull;
    TValue GetOrSetValue<TValue>(AppSettingsEntryDescriptor<TValue> settingsKeyDescriptor, Func<AppSettingsEntryDescriptor<TValue>, TValue?> valueFactory) where TValue : notnull;
    bool TryAdd<TValue>(AppSettingsEntryDescriptor<TValue> settingsKeyDescriptor, TValue value) where TValue : notnull;
    bool TryGet<TValue>(AppSettingsEntryDescriptor<TValue> settingsKeyDescriptor, [NotNullWhen(true)] out TValue value) where TValue : notnull;
}