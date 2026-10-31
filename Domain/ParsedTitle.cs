namespace Jellyfin.Plugin.TorrServerStrm.Domain;

public sealed record ParsedTitle
{
    public string Title { get; init; } = string.Empty;

    public int? Year { get; init; }

    public MediaKind Kind { get; init; } = MediaKind.Movie;
}
