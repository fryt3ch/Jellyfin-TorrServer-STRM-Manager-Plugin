using Jellyfin.Plugin.TorrServerStrm.Domain;
using Jellyfin.Plugin.TorrServerStrm.Infrastructure;
using Jellyfin.Plugin.TorrServerStrm.Services.Index;
using Jellyfin.Plugin.TorrServerStrm.Services.Library;
using Jellyfin.Plugin.TorrServerStrm.Services.Strm;
using Jellyfin.Plugin.TorrServerStrm.Services.Title;
using Jellyfin.Plugin.TorrServerStrm.Services.Torrent;
using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.TorrServerStrm.Services.Import;

public sealed class MediaImportService : IMediaImportService
{
    private readonly ITorrServerClient _torrentClient;
    private readonly ITorrentRemovalService _torrentRemoval;
    private readonly ITitleParser _titleParser;
    private readonly LibraryCatalog _libraryCatalog;
    private readonly IStrmWriter _strmWriter;
    private readonly IStrmIndex _index;
    private readonly IPluginConfigurationProvider _configurationProvider;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<MediaImportService> _logger;

    public MediaImportService(
        ITorrServerClient torrentClient,
        ITorrentRemovalService torrentRemoval,
        ITitleParser titleParser,
        LibraryCatalog libraryCatalog,
        IStrmWriter strmWriter,
        IStrmIndex index,
        IPluginConfigurationProvider configurationProvider,
        ILibraryManager libraryManager,
        ILogger<MediaImportService> logger)
    {
        _torrentClient = torrentClient;
        _torrentRemoval = torrentRemoval;
        _titleParser = titleParser;
        _libraryCatalog = libraryCatalog;
        _strmWriter = strmWriter;
        _index = index;
        _configurationProvider = configurationProvider;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public async Task<ImportResult> ImportAsync(
        ImportRequest request,
        CancellationToken cancellationToken)
    {
        if (!MagnetLink.IsValid(request.Magnet))
        {
            throw new MediaImportValidationException("A magnet link is required.");
        }

        var configuration = _configurationProvider.RequireConfiguration();
        var selectedLibrary = _libraryCatalog.Resolve(request.LibraryPath, request.Kind);
        TorrentInfo? torrent = null;
        WriteResult? written = null;

        try
        {
            torrent = await _torrentClient
                .AddMagnetAsync(request.Magnet, request.Title, cancellationToken)
                .ConfigureAwait(false);

            var (title, year, includeSeriesYear) = ResolveMetadata(request, torrent);
            written = await _strmWriter.WriteAsync(
                new StrmWriteRequest
                {
                    Title = title,
                    Year = year,
                    Kind = request.Kind,
                    Torrent = torrent,
                    LibraryRoot = selectedLibrary.Path,
                    IncludeSeriesYear = includeSeriesYear
                },
                cancellationToken).ConfigureAwait(false);

            var item = new StoredItem
            {
                Kind = request.Kind,
                Title = title,
                Year = year,
                RootPath = written.RootPath,
                LibraryRoot = written.LibraryRoot,
                Files = written.Files
                    .Select(file => new StoredFile { Path = file.Path, Hash = torrent.Hash })
                    .ToList()
            };

            var merge = await _index.AddOrMergeAsync(item, cancellationToken).ConfigureAwait(false);
            await _torrentRemoval
                .RemoveManyAsync(merge.ObsoleteHashes, CancellationToken.None)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "TorrServerStrm: created {Count} .strm file(s) for {Title}",
                written.Files.Count,
                title);

            if (configuration.ScanAfterAdd)
            {
                try
                {
                    _libraryManager.QueueLibraryScan();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to start a library scan");
                }
            }

            return new ImportResult
            {
                Id = merge.Item.Id,
                Hash = torrent.Hash,
                Title = title,
                Year = year,
                Kind = request.Kind,
                LibraryName = selectedLibrary.Name,
                LibraryPath = selectedLibrary.Path,
                Files = written.Files.Select(file => file.Path).ToArray()
            };
        }
        catch (Exception ex)
        {
            if (written is not null)
            {
                await _strmWriter.RollbackAsync(written, CancellationToken.None).ConfigureAwait(false);
            }

            if (torrent is not null)
            {
                await _torrentRemoval
                    .RemoveAsync(torrent.Hash, CancellationToken.None)
                    .ConfigureAwait(false);
            }

            if (ex is not MediaImportValidationException and not OperationCanceledException)
            {
                _logger.LogError(ex, "TorrServerStrm: add failed");
            }

            throw;
        }
    }

    private (string Title, int? Year, bool IncludeSeriesYear) ResolveMetadata(
        ImportRequest request,
        TorrentInfo torrent)
    {
        var title = request.Title?.Trim() ?? string.Empty;
        var manualYear = request.Year.HasValue && !request.YearAuto;
        var year = request.Year;

        if (string.IsNullOrWhiteSpace(title) || (!year.HasValue && request.Kind != MediaKind.Series))
        {
            var detected = _titleParser.DetectFromTorrent(torrent, request.Kind);
            if (string.IsNullOrWhiteSpace(title))
            {
                title = detected.Title;
            }

            if (!year.HasValue && request.Kind != MediaKind.Series)
            {
                year = detected.Year;
            }
        }

        if (request.Kind == MediaKind.Series && request.YearAuto)
        {
            year = null;
        }

        if (request.Kind != MediaKind.Series && !year.HasValue)
        {
            year = _titleParser.ExtractYear(title);
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new MediaImportValidationException(
                "Could not determine the title. Enter the title manually.");
        }

        return (title, year, request.Kind == MediaKind.Series && manualYear);
    }
}
