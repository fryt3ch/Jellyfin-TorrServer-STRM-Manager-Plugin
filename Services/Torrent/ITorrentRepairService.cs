namespace Jellyfin.Plugin.TorrServerStrm.Services.Torrent;

public interface ITorrentRepairService
{
    Task EnsureMissingAsync(CancellationToken cancellationToken);
}
