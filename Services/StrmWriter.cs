using System.Text;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.TorrServerStrm.Configuration;

namespace Jellyfin.Plugin.TorrServerStrm.Services;

public sealed class WrittenFile
{
    public string Path { get; init; } = string.Empty;

    public bool Existed { get; init; }

    public string? PreviousContent { get; init; }
}

public sealed class WriteResult
{
    public string RootPath { get; init; } = string.Empty;

    public string LibraryRoot { get; init; } = string.Empty;

    public List<WrittenFile> Files { get; init; } = new();
}

public static class StrmWriter
{
    private static readonly HashSet<string> VideoExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".avi", ".mov", ".m4v", ".ts", ".m2ts", ".wmv", ".webm", ".mpg", ".mpeg"
    };

    private static readonly Regex SxE = new(@"[Ss](\d{1,2})[\s._-]*[Ee](\d{1,3})", RegexOptions.Compiled);
    private static readonly Regex NxM = new(@"(?<!\d)(\d{1,2})x(\d{2,3})(?!\d)", RegexOptions.Compiled);
    private static readonly Regex SeasonDir = new(@"(?:[Ss]eason|[Сс]езон|\b[Ss])[\s._-]*(\d{1,2})\b", RegexOptions.Compiled);

    public static WriteResult Write(
        PluginConfiguration cfg,
        string type,
        string title,
        int? year,
        TorrentInfo torrent,
        bool includeSeriesYear = false)
    {
        var videos = (torrent.FileStats ?? new()).Where(f => VideoExt.Contains(System.IO.Path.GetExtension(f.Path))).ToList();
        if (videos.Count == 0)
        {
            throw new InvalidOperationException("В торренте нет видеофайлов.");
        }

        var baseUrl = (string.IsNullOrWhiteSpace(cfg.StreamBaseUrl) ? cfg.TorrServerUrl : cfg.StreamBaseUrl).TrimEnd('/');
        var libraryRoot = type == "movie" ? cfg.MoviesPath : cfg.SeriesPath;
        var safeLibraryRoot = System.IO.Path.GetFullPath(libraryRoot);

        if (type == "movie")
        {
            var main = videos.OrderByDescending(v => v.Length).First();
            var withYear = year.HasValue && !HasYear(title, year.Value) ? $"{title} ({year})" : title;
            var name = Sanitize(withYear);
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Название содержит только недопустимые символы.");
            }

            var dir = SafeCombine(safeLibraryRoot, name);
            var movieResult = new WriteResult
            {
                RootPath = dir,
                LibraryRoot = safeLibraryRoot
            };

            try
            {
                movieResult.Files.Add(WriteFile(dir, name, BuildUrl(baseUrl, torrent.Hash, main)));
                return movieResult;
            }
            catch
            {
                Rollback(movieResult);
                throw;
            }
        }

        var withSeriesYear = includeSeriesYear && year.HasValue && !HasYear(title, year.Value)
            ? $"{title} ({year})"
            : title;
        var show = Sanitize(withSeriesYear);
        if (string.IsNullOrWhiteSpace(show))
        {
            throw new InvalidOperationException("Название содержит только недопустимые символы.");
        }

        var root = SafeCombine(safeLibraryRoot, show);
        var result = new WriteResult
        {
            RootPath = root,
            LibraryRoot = safeLibraryRoot
        };
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fallbackCounters = new Dictionary<int, int>();

        try
        {
            foreach (var f in videos.OrderBy(v => v.Path, StringComparer.OrdinalIgnoreCase))
            {
                var fileName = System.IO.Path.GetFileNameWithoutExtension(f.Path);
                int season, episode;

                var m = SxE.Match(fileName);
                if (!m.Success) m = NxM.Match(fileName);

                if (m.Success)
                {
                    season = int.Parse(m.Groups[1].Value);
                    episode = int.Parse(m.Groups[2].Value);
                }
                else
                {
                    season = 1;
                    var dirPart = System.IO.Path.GetDirectoryName(f.Path) ?? string.Empty;
                    var sm = SeasonDir.Match(dirPart);
                    if (sm.Success) season = int.Parse(sm.Groups[1].Value);
                    fallbackCounters[season] = fallbackCounters.GetValueOrDefault(season) + 1;
                    episode = fallbackCounters[season];
                }

                var epName = $"{show} S{season:00}E{episode:00}";
                var unique = epName;
                for (var n = 2; !used.Add(unique); n++) unique = $"{epName} - {n}";

                var dir = System.IO.Path.Combine(root, $"Season {season:00}");
                result.Files.Add(WriteFile(dir, unique, BuildUrl(baseUrl, torrent.Hash, f)));
            }

            return result;
        }
        catch
        {
            Rollback(result);
            throw;
        }
    }

    public static void Rollback(WriteResult result)
    {
        foreach (var file in result.Files.AsEnumerable().Reverse())
        {
            try
            {
                if (file.Existed && file.PreviousContent is not null)
                {
                    var dir = System.IO.Path.GetDirectoryName(file.Path);
                    if (dir is not null)
                    {
                        Directory.CreateDirectory(dir);
                    }

                    File.WriteAllText(file.Path, file.PreviousContent, new UTF8Encoding(false));
                }
                else if (File.Exists(file.Path))
                {
                    File.Delete(file.Path);
                }
            }
            catch
            {
            }
        }

        foreach (var file in result.Files)
        {
            TryDeleteEmptyParents(System.IO.Path.GetDirectoryName(file.Path), result.LibraryRoot);
        }
    }

    private static string BuildUrl(string baseUrl, string hash, TorrentFile file)
    {
        var name = Uri.EscapeDataString(System.IO.Path.GetFileName(file.Path));
        return $"{baseUrl}/stream/{name}?link={hash}&index={file.Id}&play";
    }

    private static WrittenFile WriteFile(string dir, string name, string url)
    {
        Directory.CreateDirectory(dir);
        var path = System.IO.Path.Combine(dir, name + ".strm");
        var existed = File.Exists(path);
        var previous = existed ? File.ReadAllText(path) : null;

        try
        {
            File.WriteAllText(path, url, new UTF8Encoding(false));
        }
        catch
        {
            try
            {
                if (existed && previous is not null)
                {
                    File.WriteAllText(path, previous, new UTF8Encoding(false));
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }

            throw;
        }

        return new WrittenFile
        {
            Path = path,
            Existed = existed,
            PreviousContent = previous
        };
    }

    private static string SafeCombine(string libraryRoot, params string[] parts)
    {
        if (string.IsNullOrWhiteSpace(libraryRoot))
        {
            throw new InvalidOperationException("Не задана папка библиотеки.");
        }

        var root = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(libraryRoot));
        var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(new[] { root }.Concat(parts).ToArray()));
        if (string.Equals(path, root, Comparison)
            || !path.StartsWith(root + System.IO.Path.DirectorySeparatorChar, Comparison))
        {
            throw new InvalidOperationException("Недопустимый путь библиотеки.");
        }

        return path;
    }

    private static void TryDeleteEmptyParents(string? dir, string libraryRoot)
    {
        if (string.IsNullOrWhiteSpace(dir) || string.IsNullOrWhiteSpace(libraryRoot))
        {
            return;
        }

        try
        {
            var root = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(libraryRoot));
            var current = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(dir));
            while (!string.Equals(current, root, Comparison)
                   && current.StartsWith(root + System.IO.Path.DirectorySeparatorChar, Comparison))
            {
                if (Directory.EnumerateFileSystemEntries(current).Any())
                {
                    break;
                }

                Directory.Delete(current);
                var parent = System.IO.Path.GetDirectoryName(current);
                if (parent is null)
                {
                    break;
                }

                current = System.IO.Path.TrimEndingDirectorySeparator(parent);
            }
        }
        catch
        {
        }
    }

    private static bool HasYear(string title, int year)
    {
        return Regex.IsMatch(title, $@"(?<!\d){year}(?!\d)");
    }

    private static string Sanitize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            sb.Append(System.IO.Path.GetInvalidFileNameChars().Contains(c) || c == ':' ? ' ' : c);
        }

        return Regex.Replace(sb.ToString(), @"\s+", " ").Trim().TrimEnd('.');
    }

    private static StringComparison Comparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}
