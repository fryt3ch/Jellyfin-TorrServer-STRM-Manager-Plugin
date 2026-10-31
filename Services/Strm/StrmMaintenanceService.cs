using Jellyfin.Plugin.TorrServerStrm.Configuration;
using Jellyfin.Plugin.TorrServerStrm.Infrastructure;
using Jellyfin.Plugin.TorrServerStrm.Services.Index;
using Jellyfin.Plugin.TorrServerStrm.Services.Torrent;

namespace Jellyfin.Plugin.TorrServerStrm.Services.Strm;

public sealed class StrmMaintenanceService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IStrmIndex _index;
    private readonly IStrmUrlRewriter _urlRewriter;
    private readonly ITorrentRepairService _repair;
    private readonly IPluginConfigurationProvider _configurationProvider;
    private readonly ILogger<StrmMaintenanceService> _logger;

    public StrmMaintenanceService(
        IStrmIndex index,
        IStrmUrlRewriter urlRewriter,
        ITorrentRepairService repair,
        IPluginConfigurationProvider configurationProvider,
        ILogger<StrmMaintenanceService> logger)
    {
        _index = index;
        _urlRewriter = urlRewriter;
        _repair = repair;
        _configurationProvider = configurationProvider;
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

            var configuration = _configurationProvider.Current;
            if (configuration is not null)
            {
                var baseUrl = configuration.ResolveBaseUrl();
                if (!string.IsNullOrWhiteSpace(baseUrl))
                {
                    var updated = await _urlRewriter.RewriteAsync(baseUrl, cancellationToken).ConfigureAwait(false);
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

    public async Task RunSafeAsync(
        string operation,
        bool reload,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            await RunAsync(reload, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TorrServerStrm: {Operation} failed", operation);
        }
    }
}
