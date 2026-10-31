using Jellyfin.Plugin.TorrServerStrm.Configuration;
using Jellyfin.Plugin.TorrServerStrm.Domain;
using Jellyfin.Plugin.TorrServerStrm.Infrastructure;
using Jellyfin.Plugin.TorrServerStrm.Services.Index;
using Jellyfin.Plugin.TorrServerStrm.Services.Torrent;

namespace Jellyfin.Plugin.TorrServerStrm.Tests;

internal sealed class FakeConfigurationProvider : IPluginConfigurationProvider
{
    public FakeConfigurationProvider(PluginConfiguration configuration)
    {
        Current = configuration;
    }

    public PluginConfiguration? Current { get; }
}

internal sealed class FakeScanner : IStrmScanner
{
    public List<StoredItem> Items { get; } = new();

    public Task<IReadOnlyList<StoredItem>> ScanAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<StoredItem>>(Items);
}

internal sealed class FakeTorrServerClient : ITorrServerClient
{
    public TorrentInfo Torrent { get; set; } = new();

    public HashSet<string> ExistingHashes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> RemovedHashes { get; } = new();

    public List<(string Hash, string? Title)> AddedHashes { get; } = new();

    public Task<TorrentInfo> AddMagnetAsync(string magnet, string? title, CancellationToken cancellationToken)
        => Task.FromResult(Torrent);

    public Task AddByHashAsync(string hash, string? title, CancellationToken cancellationToken)
    {
        AddedHashes.Add((hash, title));
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string hash, CancellationToken cancellationToken)
    {
        RemovedHashes.Add(hash);
        return Task.CompletedTask;
    }

    public Task<HashSet<string>> GetHashesAsync(CancellationToken cancellationToken)
        => Task.FromResult(ExistingHashes);
}

internal sealed class FakeTorrentRemovalService : ITorrentRemovalService
{
    public List<string> RemovedHashes { get; } = new();

    public Task<bool> RemoveAsync(string hash, CancellationToken cancellationToken)
    {
        RemovedHashes.Add(hash);
        return Task.FromResult(true);
    }

    public Task RemoveManyAsync(IEnumerable<string> hashes, CancellationToken cancellationToken)
    {
        foreach (var hash in hashes)
        {
            RemovedHashes.Add(hash);
        }

        return Task.CompletedTask;
    }
}

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "TorrServerStrmTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string CreateFile(string relativePath, string content)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
        catch
        {
        }
    }
}
