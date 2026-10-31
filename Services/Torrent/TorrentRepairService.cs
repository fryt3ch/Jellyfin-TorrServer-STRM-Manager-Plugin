using Jellyfin.Plugin.TorrServerStrm.Domain;
using Jellyfin.Plugin.TorrServerStrm.Services.Index;

namespace Jellyfin.Plugin.TorrServerStrm.Services.Torrent;

public sealed class TorrentRepairService : ITorrentRepairService
{
    private const int MaxConcurrency = 6;
    private static readonly TimeSpan ListTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan AddTimeout = TimeSpan.FromSeconds(15);

    private readonly ITorrServerClient _client;
    private readonly IStrmIndex _index;
    private readonly ILogger<TorrentRepairService> _logger;

    public TorrentRepairService(
        ITorrServerClient client,
        IStrmIndex index,
        ILogger<TorrentRepairService> logger)
    {
        _client = client;
        _index = index;
        _logger = logger;
    }

    public async Task EnsureMissingAsync(CancellationToken cancellationToken)
    {
        await _index.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        var known = BuildKnownHashes(_index.GetSnapshot());
        if (known.Count == 0)
        {
            return;
        }

        HashSet<string> existing;
        try
        {
            using var timeout = CreateTimeout(cancellationToken, ListTimeout);
            existing = await _client.GetHashesAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "TorrServerStrm: TorrServer did not respond within {Seconds} s, torrent check skipped",
                ListTimeout.TotalSeconds);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TorrServerStrm: failed to get the TorrServer torrent list");
            return;
        }

        var missing = known.Where(pair => !existing.Contains(pair.Key)).ToList();
        if (missing.Count == 0)
        {
            _logger.LogInformation(
                "TorrServerStrm: checked {Count} hashes, no missing torrents",
                known.Count);
            return;
        }

        _logger.LogInformation(
            "TorrServerStrm: found {Count} hashes missing from TorrServer",
            missing.Count);

        var added = 0;
        var failed = 0;
        using var gate = new SemaphoreSlim(MaxConcurrency, MaxConcurrency);

        var tasks = missing.Select(async pair =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var timeout = CreateTimeout(cancellationToken, AddTimeout);
                await _client.AddByHashAsync(pair.Key, pair.Value, timeout.Token).ConfigureAwait(false);
                Interlocked.Increment(ref added);
                _logger.LogInformation("TorrServerStrm: torrent {Hash} added to TorrServer", pair.Key);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failed);
                _logger.LogWarning(ex, "TorrServerStrm: failed to add torrent {Hash}", pair.Key);
            }
            finally
            {
                gate.Release();
            }
        }).ToList();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        _logger.LogInformation(
            "TorrServerStrm: torrent check complete, added {Added}, failed {Failed}",
            added,
            failed);
    }

    private static Dictionary<string, string?> BuildKnownHashes(IndexSnapshot snapshot)
    {
        var known = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in snapshot.Items)
        {
            foreach (var file in item.Files)
            {
                if (string.IsNullOrWhiteSpace(file.Hash) || known.ContainsKey(file.Hash))
                {
                    continue;
                }

                known[file.Hash] = string.IsNullOrWhiteSpace(item.Title)
                    ? null
                    : item.Year.HasValue
                        ? $"{item.Title} ({item.Year})"
                        : item.Title;
            }
        }

        return known;
    }

    private static CancellationTokenSource CreateTimeout(
        CancellationToken cancellationToken,
        TimeSpan timeout)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(timeout);
        return source;
    }
}
