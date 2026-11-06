using System.Text;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.TorrServerStrm.Infrastructure;

public static class PathHelper
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    public static StringComparison Comparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public static StringComparer Comparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static string Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    public static bool Equals(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        try
        {
            return string.Equals(
                TrimEndingSeparator(Path.GetFullPath(first)),
                TrimEndingSeparator(Path.GetFullPath(second)),
                Comparison);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsUnder(string? path, string? parent)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(parent))
        {
            return false;
        }

        try
        {
            var file = Path.GetFullPath(path);
            var dir = TrimEndingSeparator(Path.GetFullPath(parent));
            if (string.Equals(file, dir, Comparison))
            {
                return true;
            }

            var prefix = EnsureTrailingSeparator(dir);
            return file.StartsWith(prefix, Comparison)
                || file.StartsWith(
                    prefix.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Comparison);
        }
        catch
        {
            return false;
        }
    }

    public static string SafeCombine(string libraryRoot, params string[] parts)
    {
        if (string.IsNullOrWhiteSpace(libraryRoot))
        {
            throw new InvalidOperationException("Library folder is not specified.");
        }

        var root = TrimEndingSeparator(Path.GetFullPath(libraryRoot));
        var path = Path.GetFullPath(Path.Combine(new[] { root }.Concat(parts).ToArray()));
        if (string.Equals(path, root, Comparison)
            || !path.StartsWith(EnsureTrailingSeparator(root), Comparison))
        {
            throw new InvalidOperationException("Invalid library path.");
        }

        return path;
    }

    public static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            sb.Append(invalid.Contains(c) || c == ':' ? ' ' : c);
        }

        return Whitespace.Replace(sb.ToString(), " ").Trim().TrimEnd('.');
    }

    public static void TryDeleteEmptyParents(string? directory, string? libraryRoot)
    {
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(libraryRoot))
        {
            return;
        }

        try
        {
            var root = TrimEndingSeparator(Path.GetFullPath(libraryRoot));
            var current = TrimEndingSeparator(Path.GetFullPath(directory));
            var rootPrefix = EnsureTrailingSeparator(root);
            while (!string.Equals(current, root, Comparison)
                   && current.StartsWith(rootPrefix, Comparison))
            {
                if (Directory.EnumerateFileSystemEntries(current).Any())
                {
                    break;
                }

                Directory.Delete(current);
                var parent = Path.GetDirectoryName(current);
                if (parent is null)
                {
                    break;
                }

                current = TrimEndingSeparator(parent);
            }
        }
        catch
        {
        }
    }

    private static string TrimEndingSeparator(string path)
        => Path.TrimEndingDirectorySeparator(path);

    private static string EnsureTrailingSeparator(string path)
        => Path.EndsInDirectorySeparator(path)
            ? path
            : path + Path.DirectorySeparatorChar;
}
