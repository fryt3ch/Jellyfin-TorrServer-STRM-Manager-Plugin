using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TorrServerStrm.Domain;

public sealed class TorrentFile
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("length")]
    public long Length { get; set; }
}

public sealed class TorrentInfo
{
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("file_stats")]
    public List<TorrentFile>? FileStats { get; set; }
}
