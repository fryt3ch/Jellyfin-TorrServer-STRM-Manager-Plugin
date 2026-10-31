using Jellyfin.Plugin.TorrServerStrm.Domain;

namespace Jellyfin.Plugin.TorrServerStrm.Services.Strm;

public interface IStrmWriter
{
    Task<WriteResult> WriteAsync(StrmWriteRequest request, CancellationToken cancellationToken);

    Task RollbackAsync(WriteResult result, CancellationToken cancellationToken);
}
