using Jellyfin.Plugin.TorrServerStrm.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrServerStrm.Api;

public class AddRequest
{
    public string Magnet { get; set; } = string.Empty;
    public string Type { get; set; } = "movie";
    public string? Title { get; set; }
    public int? Year { get; set; }
    public bool YearAuto { get; set; }
    public string? LibraryPath { get; set; }
}

public class LibraryLocation
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}

public class ParseMagnetRequest
{
    public string Magnet { get; set; } = string.Empty;
}

[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("TorrServerStrm")]
public class TorrServerStrmController : ControllerBase
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILibraryManager _libraryManager;
    private readonly LibraryStore _store;
    private readonly ILogger<TorrServerStrmController> _logger;

    public TorrServerStrmController(
        IHttpClientFactory httpFactory,
        ILibraryManager libraryManager,
        LibraryStore store,
        ILogger<TorrServerStrmController> logger)
    {
        _httpFactory = httpFactory;
        _libraryManager = libraryManager;
        _store = store;
        _logger = logger;
    }

    [HttpGet("Libraries")]
    public ActionResult GetLibraries()
    {
        return Ok(GetLibraryLocations());
    }

    [HttpPost("ParseMagnet")]
    public ActionResult ParseMagnet([FromBody] ParseMagnetRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Magnet)
            || !req.Magnet.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Нужна magnet-ссылка." });

        var parsed = TitleParser.ParseMagnet(req.Magnet);
        return Ok(new { title = parsed.Title, year = parsed.Year, type = parsed.Type });
    }

    [HttpPost("Add")]
    public async Task<ActionResult> Add([FromBody] AddRequest req, CancellationToken ct)
    {
        var cfg = Plugin.Instance!.Configuration;

        if (string.IsNullOrWhiteSpace(req.Magnet)
            || !req.Magnet.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "Нужна magnet-ссылка." });

        var requestedType = string.IsNullOrWhiteSpace(req.Type) ? "movie" : req.Type;
        if (requestedType != "movie" && requestedType != "series")
            return BadRequest(new { error = "Тип: movie или series." });

        TorrServerClient? client = null;
        TorrentInfo? torrent = null;
        WriteResult? written = null;

        try
        {
            client = new TorrServerClient(_httpFactory.CreateClient(), cfg);
            torrent = await client.AddAsync(req.Magnet, req.Title, ct);

            var resolvedType = requestedType;
            var title = req.Title?.Trim() ?? string.Empty;
            var manualYear = req.Year.HasValue && !req.YearAuto;
            var year = req.Year;

            if (string.IsNullOrWhiteSpace(title) || (!year.HasValue && resolvedType != "series"))
            {
                var detected = TitleParser.DetectFromTorrent(torrent, resolvedType);
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = detected.Title;
                }

                if (!year.HasValue && resolvedType != "series")
                {
                    year = detected.Year;
                }
            }

            if (resolvedType == "series" && req.YearAuto)
            {
                year = null;
            }

            if (resolvedType != "series" && !year.HasValue)
            {
                year = TitleParser.ExtractYear(title);
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new InvalidOperationException("Не удалось определить название. Укажите название вручную.");
            }

            var includeSeriesYear = resolvedType == "series" && manualYear;
            var selectedLibrary = ResolveLibrary(req.LibraryPath, resolvedType);
            written = StrmWriter.Write(cfg, resolvedType, title, year, torrent, selectedLibrary.Path, includeSeriesYear);

            var item = new StoredItem
            {
                Type = resolvedType,
                Title = title,
                Year = year,
                RootPath = written.RootPath,
                LibraryRoot = written.LibraryRoot,
                Files = written.Files
                    .Select(f => new StoredFile { Path = f.Path, Hash = torrent.Hash })
                    .ToList()
            };

            var (stored, obsolete) = await _store.AddOrMergeAsync(item, ct);
            foreach (var hash in obsolete)
            {
                try
                {
                    await client.RemoveAsync(hash, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "TorrServerStrm: не удалось удалить устаревший торрент {Hash}", hash);
                }
            }

            _logger.LogInformation("TorrServerStrm: создано {Count} .strm для {Title}", written.Files.Count, title);

            if (cfg.ScanAfterAdd)
            {
                try
                {
                    _libraryManager.QueueLibraryScan();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Не удалось запустить сканирование библиотеки");
                }
            }

            return Ok(new
            {
                id = stored.Id,
                hash = torrent.Hash,
                title,
                year,
                type = resolvedType,
                library = selectedLibrary.Name,
                libraryPath = selectedLibrary.Path,
                files = written.Files.Select(f => f.Path).ToArray()
            });
        }
        catch (Exception ex)
        {
            if (written is not null)
            {
                StrmWriter.Rollback(written);
            }

            if (torrent is not null && client is not null)
            {
                try
                {
                    await client.RemoveAsync(torrent.Hash, CancellationToken.None);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "TorrServerStrm: не удалось откатить торрент {Hash}", torrent.Hash);
                }
            }

            _logger.LogError(ex, "TorrServerStrm: ошибка добавления");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private List<LibraryLocation> GetLibraryLocations()
    {
        var result = new List<LibraryLocation>();
        var seen = new HashSet<string>(PathComparer);

        foreach (var folder in _libraryManager.GetVirtualFolders())
        {
            var type = folder.CollectionType?.ToString() ?? "unknown";
            var typeLabel = TypeLabel(folder.CollectionType);

            foreach (var path in folder.Locations ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(path) || !seen.Add(NormalizePath(path)))
                {
                    continue;
                }

                result.Add(new LibraryLocation
                {
                    Name = string.IsNullOrWhiteSpace(folder.Name) ? path : folder.Name,
                    Type = type,
                    TypeLabel = typeLabel,
                    Path = path
                });
            }
        }

        return result;
    }

    private LibraryLocation ResolveLibrary(string? requestedPath, string resolvedType)
    {
        var libraries = GetLibraryLocations();
        if (libraries.Count == 0)
        {
            throw new InvalidOperationException("В Jellyfin не найдено ни одной библиотеки.");
        }

        var primaryType = resolvedType == "series" ? "tvshows" : "movies";
        var available = libraries
            .Where(l => !IsIncompatible(l.Type, primaryType))
            .ToList();
        if (available.Count == 0)
        {
            var typeName = resolvedType == "series" ? "Сериалы" : "Фильмы";
            throw new InvalidOperationException($"Нет библиотек, доступных для типа «{typeName}».");
        }

        if (!string.IsNullOrWhiteSpace(requestedPath))
        {
            var requested = NormalizePath(requestedPath!);
            var match = available.FirstOrDefault(
                l => string.Equals(NormalizePath(l.Path), requested, PathComparison));
            if (match is not null)
            {
                return match;
            }
        }

        return available.FirstOrDefault(
                   l => string.Equals(l.Type, primaryType, StringComparison.OrdinalIgnoreCase))
               ?? available[0];
    }

    private static bool IsIncompatible(string libraryType, string primaryType)
    {
        var isMovieOrSeries = libraryType is "movies" or "tvshows";
        return isMovieOrSeries && !string.Equals(libraryType, primaryType, StringComparison.OrdinalIgnoreCase);
    }

    private static string TypeLabel(CollectionTypeOptions? collectionType)
    {
        return collectionType switch
        {
            CollectionTypeOptions.movies => "Фильмы",
            CollectionTypeOptions.tvshows => "Сериалы",
            CollectionTypeOptions.music => "Музыка",
            CollectionTypeOptions.musicvideos => "Клипы",
            CollectionTypeOptions.homevideos => "Домашние видео",
            CollectionTypeOptions.boxsets => "Коллекции",
            CollectionTypeOptions.books => "Книги",
            CollectionTypeOptions.mixed => "Смешанное",
            _ => "Без типа"
        };
    }

    private static string NormalizePath(string path)
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

    private static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
