using Microsoft.AspNetCore.WebUtilities;

namespace Jellyfin.Plugin.TorrServerStrm.Infrastructure;

public readonly record struct StrmLinkInfo(string FileName, string Hash, int Index);

public static class StrmLink
{
    private const string StreamSegment = "/stream/";

    public static string Build(string baseUrl, string hash, string fileName, int index)
    {
        var escapedName = Uri.EscapeDataString(fileName);
        return $"{baseUrl.TrimEnd('/')}{StreamSegment}{escapedName}?link={hash}&index={index}&play";
    }

    public static bool TryParse(string? content, out StrmLinkInfo link)
    {
        link = default;
        var text = content?.Trim();
        if (string.IsNullOrEmpty(text)
            || !text.Contains(StreamSegment, StringComparison.Ordinal)
            || !Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var query = QueryHelpers.ParseQuery(uri.Query);
        if (!query.TryGetValue("link", out var linkValue)
            || !query.TryGetValue("index", out var indexValue)
            || !query.ContainsKey("play")
            || !int.TryParse(indexValue.ToString(), out var index))
        {
            return false;
        }

        var hash = linkValue.ToString().Trim();
        if (string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        var fileName = Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath));
        link = new StrmLinkInfo(fileName, hash, index);
        return true;
    }

    public static bool TryGetHash(string? content, out string hash)
    {
        if (TryParse(content, out var link))
        {
            hash = link.Hash;
            return true;
        }

        hash = string.Empty;
        return false;
    }

    public static string RewriteBaseUrl(string content, string baseUrl)
    {
        var streamIndex = content.IndexOf(StreamSegment, StringComparison.Ordinal);
        if (streamIndex < 0)
        {
            return content;
        }

        return baseUrl.TrimEnd('/') + content[streamIndex..];
    }
}
