namespace Jellyfin.Plugin.TorrServerStrm.Domain;

public sealed record ImportRequest
{
    public string Magnet { get; init; } = string.Empty;

    public MediaKind Kind { get; init; } = MediaKind.Movie;

    public string? Title { get; init; }

    public int? Year { get; init; }

    public bool YearAuto { get; init; }

    public string? LibraryPath { get; init; }
}
