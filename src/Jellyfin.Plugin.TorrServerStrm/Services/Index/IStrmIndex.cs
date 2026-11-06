using Jellyfin.Plugin.TorrServerStrm.Domain;

namespace Jellyfin.Plugin.TorrServerStrm.Services.Index;

public interface IStrmIndex
{
    Task EnsureLoadedAsync(CancellationToken cancellationToken);

    Task ReloadAsync(CancellationToken cancellationToken);

    IndexSnapshot GetSnapshot();

    Task<IndexMergeResult> AddOrMergeAsync(StoredItem incoming, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> RemoveMissingFilesAsync(string removedPath, CancellationToken cancellationToken);
}
