using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrServerStrm.Services;

public sealed class StrmIndexStartupService : IHostedService
{
    private readonly StrmMaintenanceService _maintenance;
    private readonly ILogger<StrmIndexStartupService> _logger;

    public StrmIndexStartupService(
        StrmMaintenanceService maintenance,
        ILogger<StrmIndexStartupService> logger)
    {
        _maintenance = maintenance;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _maintenance.RunAsync(true, null, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TorrServerStrm: failed to verify and repair .strm files at startup");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
