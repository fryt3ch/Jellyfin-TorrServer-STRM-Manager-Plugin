using Jellyfin.Plugin.TorrServerStrm.Infrastructure;

namespace Jellyfin.Plugin.TorrServerStrm.Tests;

public sealed class StrmLinkTests
{
    [Fact]
    public void Build_CreatesExpectedUrl()
    {
        var url = StrmLink.Build("http://host:8090/", "abc123", "my file.mkv", 3);

        Assert.Equal(
            "http://host:8090/stream/my%20file.mkv?link=abc123&index=3&play",
            url);
    }

    [Fact]
    public void TryParse_ReadsHashIndexAndFileName()
    {
        var url = StrmLink.Build("http://host:8090", "abc123", "my file.mkv", 3);

        Assert.True(StrmLink.TryParse(url, out var link));
        Assert.Equal("abc123", link.Hash);
        Assert.Equal(3, link.Index);
        Assert.Equal("my file.mkv", link.FileName);
    }

    [Fact]
    public void TryParse_RejectsLinksWithoutRequiredParameters()
    {
        Assert.False(StrmLink.TryParse("http://host/stream/file.mkv?link=abc&index=1", out _));
        Assert.False(StrmLink.TryParse("http://host/no-stream?link=abc&index=1&play", out _));
        Assert.False(StrmLink.TryParse("not a url", out _));
    }

    [Fact]
    public void RewriteBaseUrl_PreservesStreamPathAndQuery()
    {
        var url = StrmLink.Build("http://old:8090", "abc123", "file.mkv", 0);

        var rewritten = StrmLink.RewriteBaseUrl(url, "http://new:8090/");

        Assert.Equal("http://new:8090/stream/file.mkv?link=abc123&index=0&play", rewritten);
    }
}
