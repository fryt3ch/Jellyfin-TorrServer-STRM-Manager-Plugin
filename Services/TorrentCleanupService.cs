using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrServerStrm.Services;

public sealed class TorrentCleanupService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly StrmIndex _index;
    private readonly ILogger<TorrentCleanupService> _logger;

    public TorrentCleanupService(
        ILibraryManager libraryManager,
        IHttpClientFactory httpFactory,
        StrmIndex index,
        ILogger<TorrentCleanupService> logger)
    {
        _httpFactory = httpFactory;
        _index = index;
        _logger = logger;
        libraryManager.ItemRemoved += OnItemRemoved;
    }

    private void OnItemRemoved(object? sender, ItemChangeEventArgs e)
    {
        var path = e.Item?.Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _ = Task.Run(() => CleanupAsync(path));
    }

    private async Task CleanupAsync(string path)
    {
        try
        {
            var hashes = await _index.RemoveMissingFilesAsync(path, CancellationToken.None).ConfigureAwait(false);
            if (hashes.Count == 0)
            {
                return;
            }

            var config = Plugin.Instance?.Configuration;
            if (config is null)
            {
                return;
            }

            var client = new TorrServerClient(_httpFactory.CreateClient(), config);
            foreach (var hash in hashes)
            {
                try
                {
                    await client.RemoveAsync(hash, CancellationToken.None).ConfigureAwait(false);
                    _logger.LogInformation(
                        "TorrServerStrm: torrent {Hash} removed after deleting {Path}",
                        hash,
                        path);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "TorrServerStrm: failed to remove torrent {Hash}", hash);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TorrServerStrm: cleanup error after deleting {Path}", path);
        }
    }
}
