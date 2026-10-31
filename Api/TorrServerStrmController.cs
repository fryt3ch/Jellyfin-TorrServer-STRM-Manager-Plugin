using Jellyfin.Plugin.TorrServerStrm.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrServerStrm.Api;

public class AddRequest
{
    public string Magnet { get; set; } = string.Empty;
    public string Type { get; set; } = "auto";
    public string? Title { get; set; }
    public int? Year { get; set; }
    public bool YearAuto { get; set; }
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

        var requestedType = string.IsNullOrWhiteSpace(req.Type) ? "auto" : req.Type;
        if (requestedType != "auto" && requestedType != "movie" && requestedType != "series")
            return BadRequest(new { error = "Тип: auto, movie или series." });

        TorrServerClient? client = null;
        TorrentInfo? torrent = null;
        WriteResult? written = null;

        try
        {
            client = new TorrServerClient(_httpFactory.CreateClient(), cfg);
            torrent = await client.AddAsync(req.Magnet, req.Title, ct);

            var resolvedType = requestedType == "auto" ? TitleParser.DetectType(torrent) : requestedType;
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
            written = StrmWriter.Write(cfg, resolvedType, title, year, torrent, includeSeriesYear);

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
