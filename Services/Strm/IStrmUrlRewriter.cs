namespace Jellyfin.Plugin.TorrServerStrm.Services.Strm;

public interface IStrmUrlRewriter
{
    Task<int> RewriteAsync(string newBaseUrl, CancellationToken cancellationToken);
}
