using Jellyfin.Plugin.TorrServerStrm.Infrastructure;
using Jellyfin.Plugin.TorrServerStrm.Services.Index;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Plugin.TorrServerStrm.Services.Torrent;

public sealed class TorrentCleanupService : IHostedService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IStrmIndex _index;
    private readonly ITorrentRemovalService _torrentRemoval;
    private readonly IBackgroundTaskRunner _backgroundTasks;
    private readonly ILogger<TorrentCleanupService> _logger;

    public TorrentCleanupService(
        ILibraryManager libraryManager,
        IStrmIndex index,
        ITorrentRemovalService torrentRemoval,
        IBackgroundTaskRunner backgroundTasks,
        ILogger<TorrentCleanupService> logger)
    {
        _libraryManager = libraryManager;
        _index = index;
        _torrentRemoval = torrentRemoval;
        _backgroundTasks = backgroundTasks;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemRemoved += OnItemRemoved;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemRemoved -= OnItemRemoved;
        return Task.CompletedTask;
    }

    private void OnItemRemoved(object? sender, ItemChangeEventArgs args)
    {
        var path = args.Item?.Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _backgroundTasks.Run(
            token => CleanupAsync(path, token),
            "remove torrents after deleted media");
    }

    private async Task CleanupAsync(string path, CancellationToken cancellationToken)
    {
        var hashes = await _index
            .RemoveMissingFilesAsync(path, cancellationToken)
            .ConfigureAwait(false);
        if (hashes.Count == 0)
        {
            return;
        }

        foreach (var hash in hashes)
        {
            if (await _torrentRemoval.RemoveAsync(hash, cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation(
                    "TorrServerStrm: torrent {Hash} removed after deleting {Path}",
                    hash,
                    path);
            }
        }
    }
}
