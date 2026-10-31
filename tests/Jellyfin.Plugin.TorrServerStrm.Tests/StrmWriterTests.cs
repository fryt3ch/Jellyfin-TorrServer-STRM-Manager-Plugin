using Jellyfin.Plugin.TorrServerStrm.Configuration;
using Jellyfin.Plugin.TorrServerStrm.Domain;
using Jellyfin.Plugin.TorrServerStrm.Services.Strm;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.TorrServerStrm.Tests;

public sealed class StrmWriterTests
{
    [Fact]
    public async Task WriteMovie_CreatesFileWithYearAndStreamLink()
    {
        using var temp = new TempDirectory();
        var writer = CreateWriter();

        var result = await writer.WriteAsync(
            new StrmWriteRequest
            {
                Title = "Movie",
                Year = 2024,
                Kind = MediaKind.Movie,
                LibraryRoot = temp.Path,
                Torrent = Torrent("abc", ("Movie.2024.1080p.mkv", 100))
            },
            CancellationToken.None);

        var file = Assert.Single(result.Files);
        Assert.Equal(Path.Combine(temp.Path, "Movie (2024)", "Movie (2024).strm"), file.Path);
        Assert.Equal(
            "http://torr:8090/stream/Movie.2024.1080p.mkv?link=abc&index=0&play",
            await File.ReadAllTextAsync(file.Path));
    }

    [Fact]
    public async Task WriteSeries_CreatesSeasonFoldersAndUniqueNames()
    {
        using var temp = new TempDirectory();
        var writer = CreateWriter();

        var result = await writer.WriteAsync(
            new StrmWriteRequest
            {
                Title = "Show",
                Kind = MediaKind.Series,
                LibraryRoot = temp.Path,
                Torrent = Torrent(
                    "abc",
                    ("Show.S01E01.mkv", 100),
                    ("Show.S01E02.mkv", 100))
            },
            CancellationToken.None);

        Assert.Equal(2, result.Files.Count);
        Assert.Contains(
            result.Files,
            file => file.Path == Path.Combine(temp.Path, "Show", "Season 01", "Show S01E01.strm"));
        Assert.Contains(
            result.Files,
            file => file.Path == Path.Combine(temp.Path, "Show", "Season 01", "Show S01E02.strm"));
    }

    [Fact]
    public async Task Rollback_RestoresPreviousContent()
    {
        using var temp = new TempDirectory();
        var writer = CreateWriter();
        var existing = temp.CreateFile(
            Path.Combine("Movie (2024)", "Movie (2024).strm"),
            "old-content");

        var result = await writer.WriteAsync(
            new StrmWriteRequest
            {
                Title = "Movie",
                Year = 2024,
                Kind = MediaKind.Movie,
                LibraryRoot = temp.Path,
                Torrent = Torrent("abc", ("Movie.2024.1080p.mkv", 100))
            },
            CancellationToken.None);

        Assert.NotEqual("old-content", await File.ReadAllTextAsync(existing));

        await writer.RollbackAsync(result, CancellationToken.None);

        Assert.Equal("old-content", await File.ReadAllTextAsync(existing));
    }

    [Fact]
    public async Task Rollback_DeletesNewFiles()
    {
        using var temp = new TempDirectory();
        var writer = CreateWriter();

        var result = await writer.WriteAsync(
            new StrmWriteRequest
            {
                Title = "Movie",
                Year = 2024,
                Kind = MediaKind.Movie,
                LibraryRoot = temp.Path,
                Torrent = Torrent("abc", ("Movie.2024.1080p.mkv", 100))
            },
            CancellationToken.None);

        await writer.RollbackAsync(result, CancellationToken.None);

        Assert.False(File.Exists(result.Files[0].Path));
    }

    [Fact]
    public async Task Write_ThrowsWhenTorrentHasNoVideoFiles()
    {
        using var temp = new TempDirectory();
        var writer = CreateWriter();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => writer.WriteAsync(
                new StrmWriteRequest
                {
                    Title = "Movie",
                    Kind = MediaKind.Movie,
                    LibraryRoot = temp.Path,
                    Torrent = Torrent("abc", ("readme.txt", 10))
                },
                CancellationToken.None));
    }

    private static StrmWriter CreateWriter()
    {
        return new StrmWriter(
            new FakeConfigurationProvider(new PluginConfiguration
            {
                TorrServerUrl = "http://torr:8090"
            }),
            NullLogger<StrmWriter>.Instance);
    }

    private static TorrentInfo Torrent(string hash, params (string Path, long Length)[] files)
    {
        return new TorrentInfo
        {
            Hash = hash,
            FileStats = files
                .Select((file, index) => new TorrentFile
                {
                    Id = index,
                    Path = file.Path,
                    Length = file.Length
                })
                .ToList()
        };
    }
}
