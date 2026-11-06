using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrServerStrm.Infrastructure;

public interface IBackgroundTaskRunner
{
    void Run(Func<CancellationToken, Task> operation, string description);
}

public sealed class BackgroundTaskRunner : IBackgroundTaskRunner
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<BackgroundTaskRunner> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public BackgroundTaskRunner(
        IHostApplicationLifetime lifetime,
        ILogger<BackgroundTaskRunner> logger)
    {
        _lifetime = lifetime;
        _logger = logger;
    }

    public void Run(Func<CancellationToken, Task> operation, string description)
    {
        _ = Task.Run(async () =>
        {
            var token = _lifetime.ApplicationStopping;
            try
            {
                await _gate.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    await operation(token).ConfigureAwait(false);
                }
                finally
                {
                    _gate.Release();
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TorrServerStrm: background task failed: {Operation}", description);
            }
        });
    }
}
