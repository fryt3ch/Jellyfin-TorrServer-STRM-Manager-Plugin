using System.Text;
using Jellyfin.Plugin.TorrServerStrm.Configuration;
using Jellyfin.Plugin.TorrServerStrm.Domain;
using Jellyfin.Plugin.TorrServerStrm.Infrastructure;

namespace Jellyfin.Plugin.TorrServerStrm.Services.Strm;

public sealed class StrmWriter : IStrmWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private readonly IPluginConfigurationProvider _configurationProvider;
    private readonly ILogger<StrmWriter> _logger;

    public StrmWriter(
        IPluginConfigurationProvider configurationProvider,
        ILogger<StrmWriter> logger)
    {
        _configurationProvider = configurationProvider;
        _logger = logger;
    }

    public async Task<WriteResult> WriteAsync(
        StrmWriteRequest request,
        CancellationToken cancellationToken)
    {
        var videos = (request.Torrent.FileStats ?? new List<TorrentFile>())
            .Where(file => MediaFileTypes.IsVideoFile(file.Path))
            .ToList();
        if (videos.Count == 0)
        {
            throw new InvalidOperationException("The torrent contains no video files.");
        }

        var baseUrl = _configurationProvider.RequireConfiguration().ResolveBaseUrl();
        var libraryRoot = Path.GetFullPath(request.LibraryRoot);

        return request.Kind == MediaKind.Movie
            ? await WriteMovieAsync(request, videos, baseUrl, libraryRoot, cancellationToken)
                .ConfigureAwait(false)
            : await WriteSeriesAsync(request, videos, baseUrl, libraryRoot, cancellationToken)
                .ConfigureAwait(false);
    }

    public async Task RollbackAsync(WriteResult result, CancellationToken cancellationToken)
    {
        foreach (var file in result.Files.Reverse())
        {
            try
            {
                if (file.Existed && file.PreviousContent is not null)
                {
                    var directory = Path.GetDirectoryName(file.Path);
                    if (directory is not null)
                    {
                        Directory.CreateDirectory(directory);
                    }

                    await File.WriteAllTextAsync(file.Path, file.PreviousContent, Utf8NoBom, cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (File.Exists(file.Path))
                {
                    File.Delete(file.Path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TorrServerStrm: failed to roll back .strm {Path}", file.Path);
            }
        }

        foreach (var file in result.Files)
        {
            PathHelper.TryDeleteEmptyParents(Path.GetDirectoryName(file.Path), result.LibraryRoot);
        }
    }

    private async Task<WriteResult> WriteMovieAsync(
        StrmWriteRequest request,
        IReadOnlyList<TorrentFile> videos,
        string baseUrl,
        string libraryRoot,
        CancellationToken cancellationToken)
    {
        var main = videos.OrderByDescending(video => video.Length).First();
        var name = SanitizeTitle(AddYear(request.Title, request.Year));
        var directory = PathHelper.SafeCombine(libraryRoot, name);
        var files = new List<WrittenFile>();
        var result = new WriteResult
        {
            RootPath = directory,
            LibraryRoot = libraryRoot,
            Files = files
        };

        try
        {
            files.Add(await WriteFileAsync(
                directory,
                name,
                StrmLink.Build(baseUrl, request.Torrent.Hash, Path.GetFileName(main.Path), main.Id),
                cancellationToken).ConfigureAwait(false));
            return result;
        }
        catch
        {
            await RollbackAsync(result, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<WriteResult> WriteSeriesAsync(
        StrmWriteRequest request,
        IReadOnlyList<TorrentFile> videos,
        string baseUrl,
        string libraryRoot,
        CancellationToken cancellationToken)
    {
        var show = SanitizeTitle(request.IncludeSeriesYear
            ? AddYear(request.Title, request.Year)
            : request.Title);
        var root = PathHelper.SafeCombine(libraryRoot, show);
        var files = new List<WrittenFile>();
        var result = new WriteResult
        {
            RootPath = root,
            LibraryRoot = libraryRoot,
            Files = files
        };

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fallbackCounters = new Dictionary<int, int>();

        try
        {
            foreach (var video in videos.OrderBy(video => video.Path, StringComparer.OrdinalIgnoreCase))
            {
                var (season, episode) = EpisodeNaming.Resolve(video.Path, fallbackCounters);
                var name = EpisodeNaming.MakeUniqueName(show, season, episode, usedNames);
                var directory = Path.Combine(root, $"Season {season:00}");

                files.Add(await WriteFileAsync(
                    directory,
                    name,
                    StrmLink.Build(baseUrl, request.Torrent.Hash, Path.GetFileName(video.Path), video.Id),
                    cancellationToken).ConfigureAwait(false));
            }

            return result;
        }
        catch
        {
            await RollbackAsync(result, CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<WrittenFile> WriteFileAsync(
        string directory,
        string name,
        string url,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name + ".strm");
        var existed = File.Exists(path);
        var previous = existed
            ? await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)
            : null;

        try
        {
            await File.WriteAllTextAsync(path, url, Utf8NoBom, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await RestoreAsync(path, existed, previous, cancellationToken).ConfigureAwait(false);
            throw;
        }

        return new WrittenFile
        {
            Path = path,
            Existed = existed,
            PreviousContent = previous
        };
    }

    private async Task RestoreAsync(
        string path,
        bool existed,
        string? previous,
        CancellationToken cancellationToken)
    {
        try
        {
            if (existed && previous is not null)
            {
                await File.WriteAllTextAsync(path, previous, Utf8NoBom, cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TorrServerStrm: failed to restore .strm {Path}", path);
        }
    }

    private static string AddYear(string title, int? year)
    {
        return year.HasValue && !ContainsYear(title, year.Value)
            ? $"{title} ({year})"
            : title;
    }

    private static bool ContainsYear(string title, int year)
    {
        return TitlePatterns.LooseYear.Matches(title)
            .Cast<Match>()
            .Any(match => int.Parse(match.Groups[1].Value) == year);
    }

    private static string SanitizeTitle(string title)
    {
        var sanitized = PathHelper.SanitizeFileName(title);
        return string.IsNullOrWhiteSpace(sanitized)
            ? throw new InvalidOperationException("The title contains only invalid characters.")
            : sanitized;
    }
}
