using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrServerStrm.Services;

public sealed class StrmIndexStartupService : IHostedService
{
    private readonly StrmIndex _index;
    private readonly ILogger<StrmIndexStartupService> _logger;

    public StrmIndexStartupService(StrmIndex index, ILogger<StrmIndexStartupService> logger)
    {
        _index = index;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _index.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TorrServerStrm: не удалось построить индекс .strm при старте");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
