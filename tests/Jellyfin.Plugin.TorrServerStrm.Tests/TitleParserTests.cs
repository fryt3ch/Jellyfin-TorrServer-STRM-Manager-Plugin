using Jellyfin.Plugin.TorrServerStrm.Domain;
using Jellyfin.Plugin.TorrServerStrm.Services.Title;

namespace Jellyfin.Plugin.TorrServerStrm.Tests;

public sealed class TitleParserTests
{
    private readonly TitleParser _parser = new();

    [Fact]
    public void ParseMagnet_ExtractsMovieTitleAndYearFromDisplayName()
    {
        var parsed = _parser.ParseMagnet(
            "magnet:?xt=urn:btih:abc&dn=The.Matrix.1999.1080p.BluRay.x264-GROUP");

        Assert.Equal("The Matrix", parsed.Title);
        Assert.Equal(1999, parsed.Year);
        Assert.Equal(MediaKind.Movie, parsed.Kind);
    }

    [Fact]
    public void ParseMagnet_DetectsSeriesFromEpisodeMarker()
    {
        var parsed = _parser.ParseMagnet(
            "magnet:?dn=The.Show.S02E03.1080p.WEB-DL.x264-GROUP");

        Assert.Equal("The Show", parsed.Title);
        Assert.Null(parsed.Year);
        Assert.Equal(MediaKind.Series, parsed.Kind);
    }

    [Fact]
    public void ParseMagnet_PrefersLatinSegmentForBilingualTitle()
    {
        var parsed = _parser.ParseMagnet("magnet:?dn=Фильм / Film 2021");

        Assert.Equal("Film", parsed.Title);
        Assert.Equal(2021, parsed.Year);
    }

    [Fact]
    public void ExtractYear_PrefersBracketedYear()
    {
        Assert.Equal(2020, _parser.ExtractYear("Movie (2020) 1999"));
        Assert.Equal(1999, _parser.ExtractYear("Movie 1999"));
        Assert.Null(_parser.ExtractYear("Movie without a year"));
    }

    [Fact]
    public void ParseName_RemovesReleaseTagsAndGroup()
    {
        var parsed = _parser.ParseName("Some.Movie.2023.2160p.HDR.WEB-DL.DDP5.1.x265-GROUP", MediaKind.Movie);

        Assert.Equal("Some Movie", parsed.Title);
        Assert.Equal(2023, parsed.Year);
    }

    [Fact]
    public void ParseMagnet_ReturnsEmptyForInvalidInput()
    {
        Assert.Equal(string.Empty, _parser.ParseMagnet(null).Title);
        Assert.Equal(string.Empty, _parser.ParseMagnet("not a magnet").Title);
    }
}
