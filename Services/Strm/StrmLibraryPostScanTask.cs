using MediaBrowser.Controller.Library;

namespace Jellyfin.Plugin.TorrServerStrm.Services.Strm;

public sealed class StrmLibraryPostScanTask : ILibraryPostScanTask
{
    private readonly StrmMaintenanceService _maintenance;

    public StrmLibraryPostScanTask(StrmMaintenanceService maintenance)
    {
        _maintenance = maintenance;
    }

    public async Task Run(IProgress<double> progress, CancellationToken cancellationToken)
    {
        await _maintenance.RunSafeAsync(
            "verify and repair .strm files after library scan",
            true,
            progress,
            cancellationToken).ConfigureAwait(false);
    }
}
