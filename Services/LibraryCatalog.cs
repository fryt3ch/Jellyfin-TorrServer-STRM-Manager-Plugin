using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.TorrServerStrm.Services;

public sealed class LibraryLocation
{
    public string Name { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public string TypeLabel { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;
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
        var seen = new HashSet<string>(PathComparer);

        foreach (var folder in _libraryManager.GetVirtualFolders())
        {
            var type = folder.CollectionType?.ToString() ?? "unknown";
            var typeLabel = TypeLabel(folder.CollectionType);

            foreach (var path in folder.Locations ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(path) || !seen.Add(NormalizePath(path)))
                {
                    continue;
                }

                result.Add(new LibraryLocation
                {
                    Name = string.IsNullOrWhiteSpace(folder.Name) ? path : folder.Name,
                    Type = type,
                    TypeLabel = typeLabel,
                    Path = path
                });
            }
        }

        return result;
    }

    public LibraryLocation Resolve(string? requestedPath, string resolvedType)
    {
        var libraries = GetLocations();
        if (libraries.Count == 0)
        {
            throw new InvalidOperationException("В Jellyfin не найдено ни одной библиотеки.");
        }

        var primaryType = resolvedType == "series" ? "tvshows" : "movies";
        var available = libraries
            .Where(l => !IsIncompatible(l.Type, primaryType))
            .ToList();
        if (available.Count == 0)
        {
            var typeName = resolvedType == "series" ? "Сериалы" : "Фильмы";
            throw new InvalidOperationException($"Нет библиотек, доступных для типа «{typeName}».");
        }

        if (!string.IsNullOrWhiteSpace(requestedPath))
        {
            var requested = NormalizePath(requestedPath!);
            var match = available.FirstOrDefault(
                l => string.Equals(NormalizePath(l.Path), requested, PathComparison));
            if (match is not null)
            {
                return match;
            }
        }

        return available.FirstOrDefault(
                   l => string.Equals(l.Type, primaryType, StringComparison.OrdinalIgnoreCase))
               ?? available[0];
    }

    internal static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    internal static StringComparison PathComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    internal static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static bool IsIncompatible(string libraryType, string primaryType)
    {
        var isMovieOrSeries = libraryType is "movies" or "tvshows";
        return isMovieOrSeries && !string.Equals(libraryType, primaryType, StringComparison.OrdinalIgnoreCase);
    }

    private static string TypeLabel(CollectionTypeOptions? collectionType)
    {
        return collectionType switch
        {
            CollectionTypeOptions.movies => "Фильмы",
            CollectionTypeOptions.tvshows => "Сериалы",
            CollectionTypeOptions.music => "Музыка",
            CollectionTypeOptions.musicvideos => "Клипы",
            CollectionTypeOptions.homevideos => "Домашние видео",
            CollectionTypeOptions.boxsets => "Коллекции",
            CollectionTypeOptions.books => "Книги",
            CollectionTypeOptions.mixed => "Смешанное",
            _ => "Без типа"
        };
    }
}
