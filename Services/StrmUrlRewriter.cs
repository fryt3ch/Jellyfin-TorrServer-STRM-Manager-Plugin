using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrServerStrm.Services;

public sealed class StrmUrlRewriter
{
    private readonly StrmIndex _index;
    private readonly ILogger<StrmUrlRewriter> _logger;

    public StrmUrlRewriter(StrmIndex index, ILogger<StrmUrlRewriter> logger)
    {
        _index = index;
        _logger = logger;
    }

    public async Task<int> RewriteAsync(string newBaseUrl, CancellationToken cancellationToken)
    {
        var updated = await _index.RewriteStreamUrlsAsync(newBaseUrl, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "TorrServerStrm: updated {Count} .strm files to {BaseUrl}",
            updated,
            newBaseUrl);
        return updated;
    }
}
