namespace Jellyfin.Plugin.TorrServerStrm.Domain;

public sealed record ImportResult
{
    public string Id { get; init; } = string.Empty;

    public string Hash { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public int? Year { get; init; }

    public MediaKind Kind { get; init; } = MediaKind.Movie;

    public string LibraryName { get; init; } = string.Empty;

    public string LibraryPath { get; init; } = string.Empty;

    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
}
