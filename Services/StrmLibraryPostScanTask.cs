using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrServerStrm.Services;

public sealed class StrmLibraryPostScanTask : ILibraryPostScanTask
{
    private readonly StrmMaintenanceService _maintenance;
    private readonly ILogger<StrmLibraryPostScanTask> _logger;

    public StrmLibraryPostScanTask(
        StrmMaintenanceService maintenance,
        ILogger<StrmLibraryPostScanTask> logger)
    {
        _maintenance = maintenance;
        _logger = logger;
    }

    public async Task Run(IProgress<double> progress, CancellationToken cancellationToken)
    {
        try
        {
            await _maintenance.RunAsync(true, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TorrServerStrm: failed to verify and repair .strm files after library scan");
        }
    }
}
