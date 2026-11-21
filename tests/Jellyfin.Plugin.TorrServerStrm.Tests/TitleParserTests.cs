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
    public void ParseMagnet_ExtractsMovieTitleFromRutrackerStyleName()
    {
        const string magnet = "magnet:?xt=urn:btih:D99E6D97B7761E0F42CD1EFE3AA027FB3DCF6A5A"
            + "&tr=http%3A%2F%2Fbt3.t-ru.org%2Fann%3Fmagnet"
            + "&dn=%D0%9E%D0%B4%D0%B5%D1%80%D0%B6%D0%B8%D0%BC%D0%BE%D1%81%D1%82%D1%8C"
            + "%20%2F%20Whiplash%20(%D0%94%D1%8D%D0%BC%D1%8C%D0%B5%D0%BD%20%D0%A8%D0%B0%D0%B7%D0%B5%D0%BB%D0%BB"
            + "%20%2F%20Damien%20Chazelle)%20%5B2014%2C%20%D0%A1%D0%A8%D0%90%2C%20%D0%B4%D1%80%D0%B0%D0%BC%D0%B0"
            + "%2C%20%D0%BC%D1%83%D0%B7%D1%8B%D0%BA%D0%B0%2C%20UHD%20BDRemux%202160p%2C%20HDR10%2C%20Dolby%20Vision%5D"
            + "%20%5BHybrid%5D%202x%20MVO%20%2B%20AVO%20%2B%202x";

        var parsed = _parser.ParseMagnet(magnet);

        Assert.Equal("Whiplash", parsed.Title);
        Assert.Equal(2014, parsed.Year);
        Assert.Equal(MediaKind.Movie, parsed.Kind);
    }

    [Fact]
    public void ParseName_PreservesLeadingParentheticalTitle()
    {
        var parsed = _parser.ParseName("(500) Days of Summer / 500 дней лета (2009)", MediaKind.Movie);

        Assert.Equal("(500) Days of Summer", parsed.Title);
        Assert.Equal(2009, parsed.Year);
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
