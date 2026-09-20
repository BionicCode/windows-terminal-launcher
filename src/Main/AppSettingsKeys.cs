namespace Main;

internal static class AppSettingsKeys
{
    public static readonly AppSettingsEntryDescriptor<string> UserConfigFileLocationKey = new (AppSettingsKeyId.UserConfigFileLocation, "Path to the user configuration file.");
}

