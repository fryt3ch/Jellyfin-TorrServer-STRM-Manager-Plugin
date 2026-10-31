using Jellyfin.Plugin.TorrServerStrm.Domain;
using Jellyfin.Plugin.TorrServerStrm.Infrastructure;
using Jellyfin.Plugin.TorrServerStrm.Services.Index;
using Jellyfin.Plugin.TorrServerStrm.Services.Strm;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.TorrServerStrm.Tests;

public sealed class StrmUrlRewriterTests
{
    [Fact]
    public async Task Rewrite_UpdatesOnlyStreamAddress()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile(
            "Movie.strm",
            StrmLink.Build("http://old:8090", "abc", "movie.mkv", 0));
        var scanner = new FakeScanner();
        scanner.Items.Add(new StoredItem
        {
            Files = new[] { new StoredFile { Path = path, Hash = "abc" } }
        });
        var index = new StrmIndex(scanner, NullLogger<StrmIndex>.Instance);
        var rewriter = new StrmUrlRewriter(index, NullLogger<StrmUrlRewriter>.Instance);

        var updated = await rewriter.RewriteAsync("http://new:8090", CancellationToken.None);

        Assert.Equal(1, updated);
        Assert.Equal(
            "http://new:8090/stream/movie.mkv?link=abc&index=0&play",
            await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Rewrite_IgnoresFilesWithoutStreamLink()
    {
        using var temp = new TempDirectory();
        var path = temp.CreateFile("Movie.strm", "https://example.com/video.mp4");
        var scanner = new FakeScanner();
        scanner.Items.Add(new StoredItem
        {
            Files = new[] { new StoredFile { Path = path, Hash = "abc" } }
        });
        var index = new StrmIndex(scanner, NullLogger<StrmIndex>.Instance);
        var rewriter = new StrmUrlRewriter(index, NullLogger<StrmUrlRewriter>.Instance);

        var updated = await rewriter.RewriteAsync("http://new:8090", CancellationToken.None);

        Assert.Equal(0, updated);
        Assert.Equal("https://example.com/video.mp4", await File.ReadAllTextAsync(path));
    }
}
