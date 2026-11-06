namespace Jellyfin.Plugin.TorrServerStrm.Infrastructure;

public static class MediaFileTypes
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".avi", ".mov", ".m4v", ".ts", ".m2ts", ".wmv", ".webm", ".mpg", ".mpeg"
    };

    public static bool IsVideoFile(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && VideoExtensions.Contains(Path.GetExtension(path));
    }
}
