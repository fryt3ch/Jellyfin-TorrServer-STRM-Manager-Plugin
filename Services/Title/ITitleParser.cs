using Jellyfin.Plugin.TorrServerStrm.Domain;

namespace Jellyfin.Plugin.TorrServerStrm.Services.Title;

public interface ITitleParser
{
    ParsedTitle ParseMagnet(string? magnet);

    ParsedTitle DetectFromTorrent(TorrentInfo torrent, MediaKind kind);

    ParsedTitle ParseName(string? raw, MediaKind? forcedKind);

    MediaKind DetectKind(TorrentInfo torrent);

    int? ExtractYear(string? text);
}
