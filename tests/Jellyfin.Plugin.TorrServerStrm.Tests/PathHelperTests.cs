using Jellyfin.Plugin.TorrServerStrm.Infrastructure;

namespace Jellyfin.Plugin.TorrServerStrm.Tests;

public sealed class PathHelperTests
{
    [Fact]
    public void SafeCombine_ReturnsPathUnderRoot()
    {
        using var temp = new TempDirectory();

        var combined = PathHelper.SafeCombine(temp.Path, "Movie", "Movie.strm");

        Assert.Equal(Path.Combine(temp.Path, "Movie", "Movie.strm"), combined);
    }

    [Fact]
    public void SafeCombine_RejectsTraversalOutsideRoot()
    {
        using var temp = new TempDirectory();

        Assert.Throws<InvalidOperationException>(
            () => PathHelper.SafeCombine(temp.Path, "..", "escape"));
    }

    [Fact]
    public void IsUnder_DetectsNestedFiles()
    {
        using var temp = new TempDirectory();
        var nested = Path.Combine(temp.Path, "Show", "Season 01", "episode.strm");

        Assert.True(PathHelper.IsUnder(nested, temp.Path));
        Assert.False(PathHelper.IsUnder(temp.Path, nested));
    }

    [Fact]
    public void SanitizeFileName_ReplacesInvalidCharacters()
    {
        Assert.Equal("A B", PathHelper.SanitizeFileName("A/B"));
        Assert.Equal("A B", PathHelper.SanitizeFileName("  A   B  "));
    }

    [Fact]
    public void Equals_NormalizesPaths()
    {
        using var temp = new TempDirectory();

        Assert.True(PathHelper.Equals(temp.Path, temp.Path + Path.DirectorySeparatorChar));
        Assert.False(PathHelper.Equals(temp.Path, Path.Combine(temp.Path, "other")));
    }
}
