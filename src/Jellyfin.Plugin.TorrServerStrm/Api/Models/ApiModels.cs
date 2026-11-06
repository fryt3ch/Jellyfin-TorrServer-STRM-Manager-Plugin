using Jellyfin.Plugin.TorrServerStrm.Domain;

namespace Jellyfin.Plugin.TorrServerStrm.Api.Models;

public sealed class AddMediaRequest
{
    public string Magnet { get; set; } = string.Empty;

    public string Type { get; set; } = MediaKind.Movie.ToApiValue();

    public string? Title { get; set; }

    public int? Year { get; set; }

    public bool YearAuto { get; set; }

    public string? LibraryPath { get; set; }
}

public sealed class ParseMagnetRequest
{
    public string Magnet { get; set; } = string.Empty;
}

public sealed record ApiError(string Error);

public sealed record LibraryResponse(
    string Name,
    string Type,
    string TypeLabel,
    string Path,
    bool CanAddMovies,
    bool CanAddSeries);

public sealed record ParseMagnetResponse(string Title, int? Year, string Type);

public sealed record AddMediaResponse(
    string Id,
    string Hash,
    string Title,
    int? Year,
    string Type,
    string Library,
    string LibraryPath,
    IReadOnlyList<string> Files);

public sealed record TrackedFileResponse(string Path, string Hash, bool Exists);

public sealed record TrackedItemResponse(
    string Type,
    string Title,
    int? Year,
    string RootPath,
    string LibraryRoot,
    IReadOnlyList<TrackedFileResponse> Files);

public sealed record TrackedResponse(
    DateTimeOffset LoadedAt,
    int TotalItems,
    int TotalFiles,
    int MissingFiles,
    IReadOnlyList<TrackedItemResponse> Items);
