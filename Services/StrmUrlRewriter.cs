using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrServerStrm.Services;

public sealed class StrmUrlRewriter
{
    private readonly LibraryStore _store;
    private readonly ILogger<StrmUrlRewriter> _logger;

    public StrmUrlRewriter(LibraryStore store, ILogger<StrmUrlRewriter> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<int> RewriteAsync(string newBaseUrl, CancellationToken cancellationToken)
    {
        var updated = await _store.RewriteStreamUrlsAsync(newBaseUrl, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "TorrServerStrm: обновлено {Count} .strm на адрес {BaseUrl}",
            updated,
            newBaseUrl);
        return updated;
    }
}
