namespace Main;

using System.IO;

internal static class CommandHandlerHelpers
{

    public static string GetFileNameIfFile(string path) => IsFilePath(path)
        ? Path.GetFileName(path)
        : string.Empty;

    public static bool IsFilePath(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);

        return !attributes.HasFlag(FileAttributes.Directory);
    }
}