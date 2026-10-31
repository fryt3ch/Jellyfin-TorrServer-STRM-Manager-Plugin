using Jellyfin.Plugin.TorrServerStrm.Services;
using MediaBrowser.Controller.Library;
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
    private readonly LibraryCatalog _libraryCatalog;
    private readonly StrmIndex _index;
    private readonly ILogger<TorrServerStrmController> _logger;

    public TorrServerStrmController(
        IHttpClientFactory httpFactory,
        ILibraryManager libraryManager,
        LibraryCatalog libraryCatalog,
        StrmIndex index,
        ILogger<TorrServerStrmController> logger)
    {
        _httpFactory = httpFactory;
        _libraryManager = libraryManager;
        _libraryCatalog = libraryCatalog;
        _index = index;
        _logger = logger;
    }

    [HttpGet("Libraries")]
    public ActionResult GetLibraries()
    {
        return Ok(_libraryCatalog.GetLocations().Select(l => new
        {
            name = l.Name,
            type = l.Type,
            typeLabel = l.TypeLabel,
            path = l.Path
        }));
    }

    [HttpGet("Tracked")]
    public async Task<ActionResult> GetTracked(CancellationToken ct)
    {
        var snapshot = await _index.GetSnapshotAsync(ct).ConfigureAwait(false);
        var items = snapshot.Items.Select(i => new
        {
            type = i.Type,
            title = i.Title,
            year = i.Year,
            rootPath = i.RootPath,
            libraryRoot = i.LibraryRoot,
            files = i.Files.Select(f => new
            {
                path = f.Path,
                hash = f.Hash,
                exists = System.IO.File.Exists(f.Path)
            }).ToArray()
        }).ToList();

        return Ok(new
        {
            loadedAt = snapshot.LoadedAt,
            totalItems = items.Count,
            totalFiles = items.Sum(i => i.files.Length),
            missingFiles = items.Sum(i => i.files.Count(f => !f.exists)),
            items
        });
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
            var selectedLibrary = _libraryCatalog.Resolve(req.LibraryPath, resolvedType);
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

            var (stored, obsolete) = await _index.AddOrMergeAsync(item, ct);
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

}
