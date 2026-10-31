using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrServerStrm.Services;

public sealed class TorrentRepairService
{
    private const int MaxConcurrency = 6;
    private static readonly TimeSpan ListTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan AddTimeout = TimeSpan.FromSeconds(15);

    private readonly IHttpClientFactory _httpFactory;
    private readonly StrmIndex _index;
    private readonly ILogger<TorrentRepairService> _logger;

    public TorrentRepairService(
        IHttpClientFactory httpFactory,
        StrmIndex index,
        ILogger<TorrentRepairService> logger)
    {
        _httpFactory = httpFactory;
        _index = index;
        _logger = logger;
    }

    public async Task EnsureMissingAsync(CancellationToken cancellationToken)
    {
        var snapshot = await _index.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        var known = BuildKnownHashes(snapshot);
        if (known.Count == 0)
        {
            return;
        }

        var cfg = Plugin.Instance?.Configuration;
        if (cfg is null)
        {
            _logger.LogWarning("TorrServerStrm: plugin configuration is unavailable, torrent check skipped");
            return;
        }

        var client = new TorrServerClient(_httpFactory.CreateClient(), cfg);

        HashSet<string> existing;
        try
        {
            using var cts = CreateTimeout(cancellationToken, ListTimeout);
            existing = await client.GetExistingHashesAsync(cts.Token).ConfigureAwait(false);
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

        var missing = known.Where(kv => !existing.Contains(kv.Key)).ToList();
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

        var tasks = missing.Select(async kv =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var cts = CreateTimeout(cancellationToken, AddTimeout);
                await client.AddByHashAsync(kv.Key, kv.Value, cts.Token).ConfigureAwait(false);
                Interlocked.Increment(ref added);
                _logger.LogInformation("TorrServerStrm: torrent {Hash} added to TorrServer", kv.Key);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failed);
                _logger.LogWarning(ex, "TorrServerStrm: failed to add torrent {Hash}", kv.Key);
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

    private static Dictionary<string, string?> BuildKnownHashes(TrackedSnapshot snapshot)
    {
        var known = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in snapshot.Items)
        {
            foreach (var file in item.Files)
            {
                if (string.IsNullOrWhiteSpace(file.Hash))
                {
                    continue;
                }

                if (known.ContainsKey(file.Hash))
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

    private static CancellationTokenSource CreateTimeout(CancellationToken cancellationToken, TimeSpan timeout)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);
        return cts;
    }
}
