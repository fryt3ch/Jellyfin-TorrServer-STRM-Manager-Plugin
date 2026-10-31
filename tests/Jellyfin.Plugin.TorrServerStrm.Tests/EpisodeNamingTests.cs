using Jellyfin.Plugin.TorrServerStrm.Infrastructure;

namespace Jellyfin.Plugin.TorrServerStrm.Tests;

public sealed class EpisodeNamingTests
{
    [Theory]
    [InlineData("Show.S02E05.1080p.mkv", 2, 5)]
    [InlineData("Show 3x04.mkv", 3, 4)]
    public void Resolve_ParsesEpisodeMarkers(string fileName, int expectedSeason, int expectedEpisode)
    {
        var (season, episode) = EpisodeNaming.Resolve(fileName, new Dictionary<int, int>());

        Assert.Equal(expectedSeason, season);
        Assert.Equal(expectedEpisode, episode);
    }

    [Fact]
    public void Resolve_UsesSeasonDirectoryAndFallbackCounters()
    {
        var counters = new Dictionary<int, int>();
        var path = Path.Combine("Show", "Season 2", "episode.mkv");

        var first = EpisodeNaming.Resolve(path, counters);
        var second = EpisodeNaming.Resolve(path, counters);

        Assert.Equal((2, 1), first);
        Assert.Equal((2, 2), second);
    }

    [Fact]
    public void MakeUniqueName_AddsSuffixForDuplicates()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Show S01E01" };

        var name = EpisodeNaming.MakeUniqueName("Show", 1, 1, used);

        Assert.Equal("Show S01E01 - 2", name);
    }
}
