namespace Main;

using System.IO;

internal static class CommandHandlerHelpers
{

    public static string GetFileNameIfFile(string path) => IsFilePath(path)
        ? string.Empty
        : Path.GetFileName(path);

    public static bool IsFilePath(string path)
    {
        FileAttributes attributes = File.GetAttributes(path);

        return !attributes.HasFlag(FileAttributes.Directory);
    }
}