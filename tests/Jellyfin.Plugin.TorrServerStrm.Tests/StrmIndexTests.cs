using Jellyfin.Plugin.TorrServerStrm.Domain;
using Jellyfin.Plugin.TorrServerStrm.Services.Index;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.TorrServerStrm.Tests;

public sealed class StrmIndexTests
{
    [Fact]
    public async Task AddOrMerge_ReplacesFilesAndReturnsObsoleteHashes()
    {
        using var temp = new TempDirectory();
        var index = CreateIndex();
        var root = Path.Combine(temp.Path, "Movie");
        var file = Path.Combine(root, "Movie.strm");

        var first = await index.AddOrMergeAsync(
            Item(root, temp.Path, ("h1", file)),
            CancellationToken.None);
        var second = await index.AddOrMergeAsync(
            Item(root, temp.Path, ("h2", file)),
            CancellationToken.None);

        Assert.Empty(first.ObsoleteHashes);
        Assert.Equal(new[] { "h1" }, second.ObsoleteHashes);
        Assert.Equal("h2", Assert.Single(second.Item.Files).Hash);
        Assert.Single(index.GetSnapshot().Items);
    }

    [Fact]
    public async Task RemoveMissingFiles_ReturnsHashesThatAreNoLongerReferenced()
    {
        using var temp = new TempDirectory();
        var index = CreateIndex();
        var fileA = temp.CreateFile(Path.Combine("Show", "a.strm"), "a");
        var fileB = temp.CreateFile(Path.Combine("Show", "b.strm"), "b");
        var root = Path.Combine(temp.Path, "Show");

        await index.AddOrMergeAsync(
            Item(root, temp.Path, ("h1", fileA), ("h2", fileB)),
            CancellationToken.None);
        File.Delete(fileA);

        var obsolete = await index.RemoveMissingFilesAsync(temp.Path, CancellationToken.None);

        Assert.Equal(new[] { "h1" }, obsolete);
        var remaining = Assert.Single(index.GetSnapshot().Items);
        Assert.Equal("h2", Assert.Single(remaining.Files).Hash);
    }

    [Fact]
    public async Task RemoveMissingFiles_ReturnsEmptyWhenNothingChanged()
    {
        using var temp = new TempDirectory();
        var index = CreateIndex();
        var file = temp.CreateFile(Path.Combine("Show", "a.strm"), "a");

        await index.AddOrMergeAsync(
            Item(Path.Combine(temp.Path, "Show"), temp.Path, ("h1", file)),
            CancellationToken.None);

        var obsolete = await index.RemoveMissingFilesAsync(temp.Path, CancellationToken.None);

        Assert.Empty(obsolete);
    }

    private static StrmIndex CreateIndex()
        => new(new FakeScanner(), NullLogger<StrmIndex>.Instance);

    private static StoredItem Item(
        string rootPath,
        string libraryRoot,
        params (string Hash, string Path)[] files)
    {
        return new StoredItem
        {
            Kind = MediaKind.Movie,
            Title = "Movie",
            RootPath = rootPath,
            LibraryRoot = libraryRoot,
            Files = files
                .Select(file => new StoredFile { Hash = file.Hash, Path = file.Path })
                .ToList()
        };
    }
}
