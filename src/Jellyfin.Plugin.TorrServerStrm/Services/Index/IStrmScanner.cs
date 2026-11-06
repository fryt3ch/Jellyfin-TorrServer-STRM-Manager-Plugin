using Jellyfin.Plugin.TorrServerStrm.Domain;

namespace Jellyfin.Plugin.TorrServerStrm.Services.Index;

public interface IStrmScanner
{
    Task<IReadOnlyList<StoredItem>> ScanAsync(CancellationToken cancellationToken);
}
