using Jellyfin.Plugin.TorrServerStrm.Api.Models;
using Jellyfin.Plugin.TorrServerStrm.Domain;
using Jellyfin.Plugin.TorrServerStrm.Infrastructure;
using Jellyfin.Plugin.TorrServerStrm.Services.Import;
using Jellyfin.Plugin.TorrServerStrm.Services.Index;
using Jellyfin.Plugin.TorrServerStrm.Services.Library;
using Jellyfin.Plugin.TorrServerStrm.Services.Title;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.TorrServerStrm.Api;

[ApiController]
[Authorize(Policy = "RequiresElevation")]
[Route("TorrServerStrm")]
public sealed class TorrServerStrmController : ControllerBase
{
    private readonly LibraryCatalog _libraryCatalog;
    private readonly IStrmIndex _index;
    private readonly ITitleParser _titleParser;
    private readonly IMediaImportService _importService;
    private readonly ILogger<TorrServerStrmController> _logger;

    public TorrServerStrmController(
        LibraryCatalog libraryCatalog,
        IStrmIndex index,
        ITitleParser titleParser,
        IMediaImportService importService,
        ILogger<TorrServerStrmController> logger)
    {
        _libraryCatalog = libraryCatalog;
        _index = index;
        _titleParser = titleParser;
        _importService = importService;
        _logger = logger;
    }

    [HttpGet("Libraries")]
    public ActionResult<IReadOnlyList<LibraryResponse>> GetLibraries()
    {
        var libraries = _libraryCatalog.GetLocations()
            .Select(library => new LibraryResponse(
                library.Name,
                library.Type,
                library.TypeLabel,
                library.Path,
                library.IsMovieCompatible,
                library.IsSeriesCompatible))
            .ToList();

        return Ok(libraries);
    }

    [HttpGet("Tracked")]
    public async Task<ActionResult<TrackedResponse>> GetTracked(CancellationToken cancellationToken)
    {
        await _index.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        var snapshot = _index.GetSnapshot();
        var items = snapshot.Items
            .Select(item => new TrackedItemResponse(
                item.Kind.ToApiValue(),
                item.Title,
                item.Year,
                item.RootPath,
                item.LibraryRoot,
                item.Files
                    .Select(file => new TrackedFileResponse(
                        file.Path,
                        file.Hash,
                        System.IO.File.Exists(file.Path)))
                    .ToList()))
            .ToList();

        return Ok(new TrackedResponse(
            snapshot.LoadedAt,
            items.Count,
            items.Sum(item => item.Files.Count),
            items.Sum(item => item.Files.Count(file => !file.Exists)),
            items));
    }

    [HttpPost("ParseMagnet")]
    public ActionResult<ParseMagnetResponse> ParseMagnet([FromBody] ParseMagnetRequest request)
    {
        if (!MagnetLink.IsValid(request.Magnet))
        {
            return BadRequest(new ApiError("A magnet link is required."));
        }

        var parsed = _titleParser.ParseMagnet(request.Magnet);
        return Ok(new ParseMagnetResponse(parsed.Title, parsed.Year, parsed.Kind.ToApiValue()));
    }

    [HttpPost("Add")]
    public async Task<ActionResult<AddMediaResponse>> Add(
        [FromBody] AddMediaRequest request,
        CancellationToken cancellationToken)
    {
        var kind = MediaKind.Movie;
        if (!string.IsNullOrWhiteSpace(request.Type)
            && !MediaKindExtensions.TryParseApiValue(request.Type, out kind))
        {
            return BadRequest(new ApiError("Type must be movie or series."));
        }

        try
        {
            var result = await _importService.ImportAsync(
                new ImportRequest
                {
                    Magnet = request.Magnet,
                    Kind = kind,
                    Title = request.Title,
                    Year = request.Year,
                    YearAuto = request.YearAuto,
                    LibraryPath = request.LibraryPath
                },
                cancellationToken).ConfigureAwait(false);

            return Ok(new AddMediaResponse(
                result.Id,
                result.Hash,
                result.Title,
                result.Year,
                result.Kind.ToApiValue(),
                result.LibraryName,
                result.LibraryPath,
                result.Files));
        }
        catch (MediaImportValidationException ex)
        {
            return BadRequest(new ApiError(ex.Message));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TorrServerStrm: add failed");
            return StatusCode(500, new ApiError("Failed to add the torrent."));
        }
    }
}
