namespace Jellyfin.Plugin.TorrServerStrm.Domain;

public sealed record StrmWriteRequest
{
    public string Title { get; init; } = string.Empty;

    public int? Year { get; init; }

    public MediaKind Kind { get; init; } = MediaKind.Movie;

    public TorrentInfo Torrent { get; init; } = new();

    public string LibraryRoot { get; init; } = string.Empty;

    public bool IncludeSeriesYear { get; init; }
}
