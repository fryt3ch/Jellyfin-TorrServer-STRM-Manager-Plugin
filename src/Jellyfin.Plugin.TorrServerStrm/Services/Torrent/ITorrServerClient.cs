using Jellyfin.Plugin.TorrServerStrm.Domain;

namespace Jellyfin.Plugin.TorrServerStrm.Services.Torrent;

public interface ITorrServerClient
{
    Task<TorrentInfo> AddMagnetAsync(string magnet, string? title, CancellationToken cancellationToken);

    Task AddByHashAsync(string hash, string? title, CancellationToken cancellationToken);

    Task RemoveAsync(string hash, CancellationToken cancellationToken);

    Task<HashSet<string>> GetHashesAsync(CancellationToken cancellationToken);
}
