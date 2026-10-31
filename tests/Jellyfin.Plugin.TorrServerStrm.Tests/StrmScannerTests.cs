using Jellyfin.Plugin.TorrServerStrm.Domain;
using Jellyfin.Plugin.TorrServerStrm.Infrastructure;
using Jellyfin.Plugin.TorrServerStrm.Services.Index;
using Jellyfin.Plugin.TorrServerStrm.Services.Library;
using Jellyfin.Plugin.TorrServerStrm.Services.Title;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Jellyfin.Plugin.TorrServerStrm.Tests;

public sealed class StrmScannerTests
{
    [Fact]
    public async Task Scan_MovieLibrary_IndexesStrmFile()
    {
        using var temp = new TempDirectory();
        temp.CreateFile(
            Path.Combine("Movie (2024)", "Movie (2024).strm"),
            StrmLink.Build("http://host:8090", "abc", "movie.mkv", 0));
        var scanner = CreateScanner(temp.Path, CollectionTypeOptions.movies);

        var items = await scanner.ScanAsync(CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal(MediaKind.Movie, item.Kind);
        Assert.Equal("Movie (2024)", item.Title);
        Assert.Equal(2024, item.Year);
        Assert.Equal("abc", Assert.Single(item.Files).Hash);
    }

    [Fact]
    public async Task Scan_SeriesLibrary_GroupsByShowFolder()
    {
        using var temp = new TempDirectory();
        var url = StrmLink.Build("http://host:8090", "abc", "episode.mkv", 1);
        temp.CreateFile(Path.Combine("Show", "Season 01", "Show S01E01.strm"), url);
        temp.CreateFile(Path.Combine("Show", "Season 02", "Show S02E01.strm"), url);
        var scanner = CreateScanner(temp.Path, CollectionTypeOptions.tvshows);

        var items = await scanner.ScanAsync(CancellationToken.None);

        var item = Assert.Single(items);
        Assert.Equal(MediaKind.Series, item.Kind);
        Assert.Equal("Show", item.Title);
        Assert.Null(item.Year);
        Assert.Equal(2, item.Files.Count);
    }

    [Fact]
    public async Task Scan_IgnoresStrmFilesWithoutStreamLink()
    {
        using var temp = new TempDirectory();
        temp.CreateFile(Path.Combine("Show", "invalid.strm"), "https://example.com/video.mp4");
        var scanner = CreateScanner(temp.Path, CollectionTypeOptions.movies);

        var items = await scanner.ScanAsync(CancellationToken.None);

        Assert.Empty(items);
    }

    private static StrmScanner CreateScanner(string libraryPath, CollectionTypeOptions collectionType)
    {
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager
            .Setup(manager => manager.GetVirtualFolders())
            .Returns(new List<VirtualFolderInfo>
            {
                new()
                {
                    Name = "Library",
                    Locations = new[] { libraryPath },
                    CollectionType = collectionType
                }
            });

        return new StrmScanner(
            new LibraryCatalog(libraryManager.Object),
            new TitleParser(),
            NullLogger<StrmScanner>.Instance);
    }
}
