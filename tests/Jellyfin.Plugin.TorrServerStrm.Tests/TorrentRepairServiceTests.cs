using Jellyfin.Plugin.TorrServerStrm.Domain;
using Jellyfin.Plugin.TorrServerStrm.Services.Index;
using Jellyfin.Plugin.TorrServerStrm.Services.Torrent;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.TorrServerStrm.Tests;

public sealed class TorrentRepairServiceTests
{
    [Fact]
    public async Task EnsureMissing_AddsHashesMissingFromTorrServer()
    {
        var scanner = new FakeScanner();
        scanner.Items.Add(new StoredItem
        {
            Title = "Movie",
            Year = 2020,
            Files = new[] { new StoredFile { Hash = "h1" } }
        });

        var client = new FakeTorrServerClient();
        var service = new TorrentRepairService(
            client,
            new StrmIndex(scanner, NullLogger<StrmIndex>.Instance),
            NullLogger<TorrentRepairService>.Instance);

        await service.EnsureMissingAsync(CancellationToken.None);

        var added = Assert.Single(client.AddedHashes);
        Assert.Equal("h1", added.Hash);
        Assert.Equal("Movie (2020)", added.Title);
    }

    [Fact]
    public async Task EnsureMissing_DoesNothingWhenHashesExist()
    {
        var scanner = new FakeScanner();
        scanner.Items.Add(new StoredItem
        {
            Title = "Movie",
            Files = new[] { new StoredFile { Hash = "h1" } }
        });

        var client = new FakeTorrServerClient();
        client.ExistingHashes.Add("h1");
        var service = new TorrentRepairService(
            client,
            new StrmIndex(scanner, NullLogger<StrmIndex>.Instance),
            NullLogger<TorrentRepairService>.Instance);

        await service.EnsureMissingAsync(CancellationToken.None);

        Assert.Empty(client.AddedHashes);
    }
}
