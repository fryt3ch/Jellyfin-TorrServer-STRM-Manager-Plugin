using System.Text;
using Jellyfin.Plugin.TorrServerStrm.Infrastructure;
using Jellyfin.Plugin.TorrServerStrm.Services.Index;

namespace Jellyfin.Plugin.TorrServerStrm.Services.Strm;

public sealed class StrmUrlRewriter : IStrmUrlRewriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IStrmIndex _index;
    private readonly ILogger<StrmUrlRewriter> _logger;

    public StrmUrlRewriter(
        IStrmIndex index,
        ILogger<StrmUrlRewriter> logger)
    {
        _index = index;
        _logger = logger;
    }

    public async Task<int> RewriteAsync(string newBaseUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(newBaseUrl))
        {
            return 0;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _index.EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            var snapshot = _index.GetSnapshot();
            var updated = 0;

            foreach (var file in snapshot.Items.SelectMany(item => item.Files))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(file.Path) || !File.Exists(file.Path))
                {
                    continue;
                }

                string content;
                try
                {
                    content = await File.ReadAllTextAsync(file.Path, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read .strm: {Path}", file.Path);
                    continue;
                }

                var rewritten = StrmLink.RewriteBaseUrl(content, newBaseUrl);
                if (string.Equals(rewritten, content, StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    await File.WriteAllTextAsync(file.Path, rewritten, Utf8NoBom, cancellationToken)
                        .ConfigureAwait(false);
                    updated++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update .strm: {Path}", file.Path);
                }
            }

            _logger.LogInformation(
                "TorrServerStrm: updated {Count} .strm files to {BaseUrl}",
                updated,
                newBaseUrl);
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }
}
