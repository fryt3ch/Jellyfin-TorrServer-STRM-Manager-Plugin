using Microsoft.Extensions.Hosting;

namespace Jellyfin.Plugin.TorrServerStrm.Services.Strm;

public sealed class StrmIndexStartupService : IHostedService
{
    private readonly StrmMaintenanceService _maintenance;

    public StrmIndexStartupService(StrmMaintenanceService maintenance)
    {
        _maintenance = maintenance;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _maintenance.RunSafeAsync(
            "verify and repair .strm files at startup",
            true,
            null,
            cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
