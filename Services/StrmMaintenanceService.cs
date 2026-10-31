using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrServerStrm.Services;

public sealed class StrmMaintenanceService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly StrmIndex _index;
    private readonly TorrentRepairService _repair;
    private readonly ILogger<StrmMaintenanceService> _logger;

    public StrmMaintenanceService(
        StrmIndex index,
        TorrentRepairService repair,
        ILogger<StrmMaintenanceService> logger)
    {
        _index = index;
        _repair = repair;
        _logger = logger;
    }

    public async Task RunAsync(bool reload, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (reload)
            {
                await _index.ReloadAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _index.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            }

            progress?.Report(20);

            var cfg = Plugin.Instance?.Configuration;
            if (cfg is not null)
            {
                var baseUrl = StrmWriter.ResolveBaseUrl(cfg);
                if (!string.IsNullOrWhiteSpace(baseUrl))
                {
                    var updated = await _index.RewriteStreamUrlsAsync(baseUrl, cancellationToken).ConfigureAwait(false);
                    if (updated > 0)
                    {
                        _logger.LogInformation(
                            "TorrServerStrm: fixed {Count} .strm addresses",
                            updated);
                    }
                }
            }

            progress?.Report(50);

            await _repair.EnsureMissingAsync(cancellationToken).ConfigureAwait(false);

            progress?.Report(100);
        }
        finally
        {
            _gate.Release();
        }
    }
}
