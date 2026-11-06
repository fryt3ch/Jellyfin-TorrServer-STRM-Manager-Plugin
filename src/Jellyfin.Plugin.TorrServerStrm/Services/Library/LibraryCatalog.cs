using Jellyfin.Plugin.TorrServerStrm.Domain;
using Jellyfin.Plugin.TorrServerStrm.Infrastructure;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.TorrServerStrm.Services.Library;

public sealed record LibraryLocation
{
    public string Name { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public string TypeLabel { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public bool IsMovieCompatible { get; init; }

    public bool IsSeriesCompatible { get; init; }

    public bool IsCompatibleWith(MediaKind kind)
        => kind == MediaKind.Series ? IsSeriesCompatible : IsMovieCompatible;
}

public sealed class LibraryCatalog
{
    private readonly ILibraryManager _libraryManager;

    public LibraryCatalog(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    public List<LibraryLocation> GetLocations()
    {
        var result = new List<LibraryLocation>();
        var seen = new HashSet<string>(PathHelper.Comparer);

        foreach (var folder in _libraryManager.GetVirtualFolders())
        {
            var collectionType = folder.CollectionType;
            var type = collectionType?.ToString() ?? "unknown";

            foreach (var path in folder.Locations ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(path) || !seen.Add(PathHelper.Normalize(path)))
                {
                    continue;
                }

                result.Add(new LibraryLocation
                {
                    Name = string.IsNullOrWhiteSpace(folder.Name) ? path : folder.Name,
                    Type = type,
                    TypeLabel = TypeLabel(collectionType),
                    Path = path,
                    IsMovieCompatible = !IsType(type, MediaKind.Series.ToPrimaryLibraryType()),
                    IsSeriesCompatible = !IsType(type, MediaKind.Movie.ToPrimaryLibraryType())
                });
            }
        }

        return result;
    }

    public LibraryLocation Resolve(string? requestedPath, MediaKind kind)
    {
        var libraries = GetLocations();
        if (libraries.Count == 0)
        {
            throw new InvalidOperationException("No libraries found in Jellyfin.");
        }

        var primaryType = kind.ToPrimaryLibraryType();
        var available = libraries
            .Where(library => library.IsCompatibleWith(kind))
            .ToList();
        if (available.Count == 0)
        {
            throw new InvalidOperationException(
                $"No libraries available for type \"{kind.ToDisplayName()}\".");
        }

        if (!string.IsNullOrWhiteSpace(requestedPath))
        {
            var match = available.FirstOrDefault(
                library => PathHelper.Equals(library.Path, requestedPath));
            if (match is not null)
            {
                return match;
            }
        }

        return available.FirstOrDefault(library => IsType(library.Type, primaryType))
               ?? available[0];
    }

    private static bool IsType(string libraryType, string expected)
        => string.Equals(libraryType, expected, StringComparison.OrdinalIgnoreCase);

    private static string TypeLabel(CollectionTypeOptions? collectionType)
    {
        return collectionType switch
        {
            CollectionTypeOptions.movies => "Movies",
            CollectionTypeOptions.tvshows => "Series",
            CollectionTypeOptions.music => "Music",
            CollectionTypeOptions.musicvideos => "Music videos",
            CollectionTypeOptions.homevideos => "Home videos",
            CollectionTypeOptions.boxsets => "Collections",
            CollectionTypeOptions.books => "Books",
            CollectionTypeOptions.mixed => "Mixed",
            _ => "No type"
        };
    }
}
