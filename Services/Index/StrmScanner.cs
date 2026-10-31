using Jellyfin.Plugin.TorrServerStrm.Domain;
using Jellyfin.Plugin.TorrServerStrm.Infrastructure;
using Jellyfin.Plugin.TorrServerStrm.Services.Library;
using Jellyfin.Plugin.TorrServerStrm.Services.Title;

namespace Jellyfin.Plugin.TorrServerStrm.Services.Index;

public sealed class StrmScanner : IStrmScanner
{
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true
    };

    private readonly LibraryCatalog _catalog;
    private readonly ITitleParser _titleParser;
    private readonly ILogger<StrmScanner> _logger;

    public StrmScanner(
        LibraryCatalog catalog,
        ITitleParser titleParser,
        ILogger<StrmScanner> logger)
    {
        _catalog = catalog;
        _titleParser = titleParser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<StoredItem>> ScanAsync(CancellationToken cancellationToken)
    {
        var groups = new Dictionary<string, Group>(PathHelper.Comparer);
        var libraries = _catalog.GetLocations();
        var scanned = 0;
        var found = 0;

        foreach (var library in libraries)
        {
            if (!Directory.Exists(library.Path))
            {
                _logger.LogWarning("TorrServerStrm: library folder is unavailable: {Path}", library.Path);
                continue;
            }

            var libraryRoot = PathHelper.Normalize(library.Path);
            var kind = string.Equals(
                library.Type,
                MediaKind.Series.ToPrimaryLibraryType(),
                StringComparison.OrdinalIgnoreCase)
                ? MediaKind.Series
                : MediaKind.Movie;

            try
            {
                foreach (var file in Directory.EnumerateFiles(library.Path, "*.strm", EnumerationOptions))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    scanned++;

                    string content;
                    try
                    {
                        content = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "TorrServerStrm: failed to read .strm {Path}", file);
                        continue;
                    }

                    if (!StrmLink.TryGetHash(content, out var hash))
                    {
                        continue;
                    }

                    found++;
                    var filePath = PathHelper.Normalize(file);
                    var rootPath = GetRootPath(libraryRoot, filePath, kind);
                    var key = rootPath + "|" + library.Type;

                    if (!groups.TryGetValue(key, out var group))
                    {
                        var title = Path.GetFileName(rootPath);
                        if (string.IsNullOrWhiteSpace(title))
                        {
                            title = Path.GetFileNameWithoutExtension(filePath);
                        }

                        group = new Group
                        {
                            Kind = kind,
                            Title = title,
                            RootPath = rootPath,
                            LibraryRoot = libraryRoot
                        };
                        groups.Add(key, group);
                    }

                    group.Files.Add(new StoredFile { Path = filePath, Hash = hash });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TorrServerStrm: error scanning library {Path}", library.Path);
            }
        }

        _logger.LogInformation(
            "TorrServerStrm: indexed {Scanned} .strm files, found {Found}, libraries {Libraries}",
            scanned,
            found,
            libraries.Count);

        return groups.Values
            .Select(group => new StoredItem
            {
                Kind = group.Kind,
                Title = group.Title,
                Year = group.Kind == MediaKind.Series ? null : _titleParser.ExtractYear(group.Title),
                RootPath = group.RootPath,
                LibraryRoot = group.LibraryRoot,
                Files = group.Files
            })
            .ToList();
    }

    private static string GetRootPath(string libraryRoot, string filePath, MediaKind kind)
    {
        var fileDirectory = Path.GetDirectoryName(filePath) ?? libraryRoot;
        if (kind == MediaKind.Movie)
        {
            return PathHelper.Normalize(fileDirectory);
        }

        var relative = Path.GetRelativePath(libraryRoot, filePath);
        var separatorIndex = relative.IndexOfAny(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
        if (separatorIndex > 0)
        {
            var firstSegment = relative[..separatorIndex];
            return PathHelper.Normalize(Path.Combine(libraryRoot, firstSegment));
        }

        return PathHelper.Normalize(fileDirectory);
    }

    private sealed class Group
    {
        public MediaKind Kind { get; init; }

        public string Title { get; init; } = string.Empty;

        public string RootPath { get; init; } = string.Empty;

        public string LibraryRoot { get; init; } = string.Empty;

        public List<StoredFile> Files { get; } = new();
    }
}
