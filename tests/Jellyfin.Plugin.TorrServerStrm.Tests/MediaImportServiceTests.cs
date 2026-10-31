using Jellyfin.Plugin.TorrServerStrm.Configuration;
using Jellyfin.Plugin.TorrServerStrm.Domain;
using Jellyfin.Plugin.TorrServerStrm.Services.Import;
using Jellyfin.Plugin.TorrServerStrm.Services.Index;
using Jellyfin.Plugin.TorrServerStrm.Services.Library;
using Jellyfin.Plugin.TorrServerStrm.Services.Strm;
using Jellyfin.Plugin.TorrServerStrm.Services.Title;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Jellyfin.Plugin.TorrServerStrm.Tests;

public sealed class MediaImportServiceTests
{
    [Fact]
    public async Task Import_WritesFilesAndUpdatesIndex()
    {
        using var temp = new TempDirectory();
        var (service, client, removal, index) = CreateService(temp, withVideo: true);

        var result = await service.ImportAsync(
            Request(temp.Path),
            CancellationToken.None);

        Assert.Equal("Movie", result.Title);
        Assert.Equal(2024, result.Year);
        Assert.Single(result.Files);
        Assert.True(File.Exists(result.Files[0]));
        Assert.Single(index.GetSnapshot().Items);
        Assert.Empty(removal.RemovedHashes);
        Assert.Equal("abc", client.Torrent.Hash);
    }

    [Fact]
    public async Task Import_RollsBackTorrentWhenWritingFails()
    {
        using var temp = new TempDirectory();
        var (service, _, removal, index) = CreateService(temp, withVideo: false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ImportAsync(Request(temp.Path), CancellationToken.None));

        Assert.Contains("abc", removal.RemovedHashes);
        Assert.Empty(index.GetSnapshot().Items);
    }

    [Fact]
    public async Task Import_RejectsInvalidMagnetBeforeCallingTorrServer()
    {
        using var temp = new TempDirectory();
        var (service, client, removal, _) = CreateService(temp, withVideo: true);

        await Assert.ThrowsAsync<MediaImportValidationException>(
            () => service.ImportAsync(
                Request(temp.Path) with { Magnet = "not-a-magnet" },
                CancellationToken.None));

        Assert.Empty(client.AddedHashes);
        Assert.Empty(removal.RemovedHashes);
    }

    private static ImportRequest Request(string libraryPath)
    {
        return new ImportRequest
        {
            Magnet = "magnet:?xt=urn:btih:abc",
            Kind = MediaKind.Movie,
            Title = "Movie",
            Year = 2024,
            LibraryPath = libraryPath
        };
    }

    private static (
        IMediaImportService Service,
        FakeTorrServerClient Client,
        FakeTorrentRemovalService Removal,
        IStrmIndex Index) CreateService(TempDirectory temp, bool withVideo)
    {
        var client = new FakeTorrServerClient
        {
            Torrent = new TorrentInfo
            {
                Hash = "abc",
                Title = "Movie.2024.1080p",
                FileStats = withVideo
                    ? new List<TorrentFile>
                    {
                        new() { Id = 0, Path = "Movie.2024.1080p.mkv", Length = 100 }
                    }
                    : new List<TorrentFile>()
            }
        };
        var removal = new FakeTorrentRemovalService();
        var configurationProvider = new FakeConfigurationProvider(new PluginConfiguration
        {
            TorrServerUrl = "http://torr:8090",
            ScanAfterAdd = false
        });
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager
            .Setup(manager => manager.GetVirtualFolders())
            .Returns(new List<VirtualFolderInfo>
            {
                new()
                {
                    Name = "Movies",
                    Locations = new[] { temp.Path },
                    CollectionType = CollectionTypeOptions.movies
                }
            });

        var titleParser = new TitleParser();
        var index = new StrmIndex(new FakeScanner(), NullLogger<StrmIndex>.Instance);
        var writer = new StrmWriter(configurationProvider, NullLogger<StrmWriter>.Instance);

        var service = new MediaImportService(
            client,
            removal,
            titleParser,
            new LibraryCatalog(libraryManager.Object),
            writer,
            index,
            configurationProvider,
            libraryManager.Object,
            NullLogger<MediaImportService>.Instance);

        return (service, client, removal, index);
    }
}
