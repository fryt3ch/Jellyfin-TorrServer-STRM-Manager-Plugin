namespace Jellyfin.Plugin.TorrServerStrm.Services.Torrent;

public interface ITorrentRemovalService
{
    Task<bool> RemoveAsync(string hash, CancellationToken cancellationToken);

    Task RemoveManyAsync(IEnumerable<string> hashes, CancellationToken cancellationToken);
}

public sealed class TorrentRemovalService : ITorrentRemovalService
{
    private readonly ITorrServerClient _client;
    private readonly ILogger<TorrentRemovalService> _logger;

    public TorrentRemovalService(
        ITorrServerClient client,
        ILogger<TorrentRemovalService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<bool> RemoveAsync(string hash, CancellationToken cancellationToken)
    {
        try
        {
            await _client.RemoveAsync(hash, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TorrServerStrm: failed to remove torrent {Hash}", hash);
            return false;
        }
    }

    public async Task RemoveManyAsync(IEnumerable<string> hashes, CancellationToken cancellationToken)
    {
        foreach (var hash in hashes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await RemoveAsync(hash, cancellationToken).ConfigureAwait(false);
        }
    }
}
