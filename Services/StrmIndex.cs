using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrServerStrm.Services;

public sealed class StoredFile
{
    public string Path { get; set; } = string.Empty;

    public string Hash { get; set; } = string.Empty;
}

public sealed class StoredItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Type { get; set; } = "movie";

    public string Title { get; set; } = string.Empty;

    public int? Year { get; set; }

    public string RootPath { get; set; } = string.Empty;

    public string LibraryRoot { get; set; } = string.Empty;

    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<StoredFile> Files { get; set; } = new();
}

public sealed class TrackedFile
{
    public string Path { get; init; } = string.Empty;

    public string Hash { get; init; } = string.Empty;
}

public sealed class TrackedItem
{
    public string Type { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public int? Year { get; init; }

    public string RootPath { get; init; } = string.Empty;

    public string LibraryRoot { get; init; } = string.Empty;

    public List<TrackedFile> Files { get; init; } = new();
}

public sealed class TrackedSnapshot
{
    public DateTimeOffset LoadedAt { get; init; }

    public List<TrackedItem> Items { get; init; } = new();
}

public sealed class StrmIndex
{
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly LibraryCatalog _catalog;
    private readonly ILogger<StrmIndex> _logger;
    private List<StoredItem> _items = new();
    private DateTimeOffset _loadedAt;
    private volatile bool _loaded;

    public StrmIndex(LibraryCatalog catalog, ILogger<StrmIndex> logger)
    {
        _catalog = catalog;
        _logger = logger;
    }

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_loaded)
            {
                return;
            }

            await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        var items = await ScanAsync(cancellationToken).ConfigureAwait(false);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _items = items;
            _loadedAt = DateTimeOffset.UtcNow;
            _loaded = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<TrackedSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return new TrackedSnapshot
            {
                LoadedAt = _loadedAt,
                Items = _items.Select(i => new TrackedItem
                {
                    Type = i.Type,
                    Title = i.Title,
                    Year = i.Year,
                    RootPath = i.RootPath,
                    LibraryRoot = i.LibraryRoot,
                    Files = i.Files.Select(f => new TrackedFile
                    {
                        Path = f.Path,
                        Hash = f.Hash
                    }).ToList()
                }).ToList()
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<(StoredItem Item, IReadOnlyList<string> ObsoleteHashes)> AddOrMergeAsync(
        StoredItem incoming,
        CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = _items.FirstOrDefault(i => PathEquals(i.RootPath, incoming.RootPath));
            var before = existing is null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : existing.Files.Select(f => f.Hash).Where(IsHash).ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (existing is null)
            {
                _items.Add(incoming);
                existing = incoming;
            }
            else
            {
                existing.Files.RemoveAll(f => incoming.Files.Any(n => PathEquals(f.Path, n.Path)));
                existing.Files.AddRange(incoming.Files);
                existing.Type = incoming.Type;
                existing.Title = incoming.Title;
                existing.Year = incoming.Year;
                existing.LibraryRoot = incoming.LibraryRoot;
            }

            var after = _items
                .SelectMany(i => i.Files)
                .Select(f => f.Hash)
                .Where(IsHash)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var obsolete = before.Where(h => !after.Contains(h)).ToList();

            return (existing, obsolete);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<int> RewriteStreamUrlsAsync(string newBaseUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(newBaseUrl))
        {
            return 0;
        }

        var baseUrl = newBaseUrl.TrimEnd('/');
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var updated = 0;

            foreach (var item in _items)
            {
                foreach (var file in item.Files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (string.IsNullOrWhiteSpace(file.Path) || !File.Exists(file.Path))
                    {
                        continue;
                    }

                    string content;
                    try
                    {
                        content = await File.ReadAllTextAsync(file.Path, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to read .strm: {Path}", file.Path);
                        continue;
                    }

                    var streamIndex = content.IndexOf("/stream/", StringComparison.Ordinal);
                    if (streamIndex < 0)
                    {
                        continue;
                    }

                    var rewritten = baseUrl + content[streamIndex..];
                    if (string.Equals(rewritten, content, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    try
                    {
                        await File.WriteAllTextAsync(file.Path, rewritten, new System.Text.UTF8Encoding(false), cancellationToken)
                            .ConfigureAwait(false);
                        updated++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to update .strm: {Path}", file.Path);
                    }
                }
            }

            return updated;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<string>> RemoveMissingFilesAsync(string removedPath, CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var before = _items
                .SelectMany(i => i.Files)
                .Select(f => f.Hash)
                .Where(IsHash)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var changed = false;

            foreach (var item in _items)
            {
                if (!string.IsNullOrWhiteSpace(item.LibraryRoot) && !Directory.Exists(item.LibraryRoot))
                {
                    continue;
                }

                for (var i = item.Files.Count - 1; i >= 0; i--)
                {
                    var file = item.Files[i];
                    if (!IsUnder(file.Path, removedPath) || File.Exists(file.Path))
                    {
                        continue;
                    }

                    item.Files.RemoveAt(i);
                    if (IsHash(file.Hash))
                    {
                        affected.Add(file.Hash);
                    }

                    changed = true;
                }
            }

            if (!changed)
            {
                return Array.Empty<string>();
            }

            _items.RemoveAll(i => i.Files.Count == 0);
            var after = _items
                .SelectMany(i => i.Files)
                .Select(f => f.Hash)
                .Where(IsHash)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return affected.Where(h => !after.Contains(h)).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<List<StoredItem>> ScanAsync(CancellationToken cancellationToken)
    {
        var groups = new Dictionary<string, StoredItem>(LibraryCatalog.PathComparer);
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

            var libraryRoot = LibraryCatalog.NormalizePath(library.Path);
            var isSeries = string.Equals(library.Type, "tvshows", StringComparison.OrdinalIgnoreCase);

            try
            {
                foreach (var file in Directory.EnumerateFiles(library.Path, "*.strm", SearchOption.AllDirectories))
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

                    var hash = TryGetHash(content);
                    if (hash is null)
                    {
                        continue;
                    }

                    found++;
                    var filePath = LibraryCatalog.NormalizePath(file);
                    var rootPath = GetRootPath(libraryRoot, filePath, isSeries);
                    var key = rootPath + "|" + library.Type;

                    if (!groups.TryGetValue(key, out var item))
                    {
                        var title = Path.GetFileName(rootPath);
                        if (string.IsNullOrWhiteSpace(title))
                        {
                            title = Path.GetFileNameWithoutExtension(filePath);
                        }

                        item = new StoredItem
                        {
                            Type = isSeries ? "series" : "movie",
                            Title = title,
                            Year = isSeries ? null : TitleParser.ExtractYear(title),
                            RootPath = rootPath,
                            LibraryRoot = libraryRoot
                        };
                        groups.Add(key, item);
                    }

                    item.Files.Add(new StoredFile { Path = filePath, Hash = hash });
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

        return groups.Values.ToList();
    }

    private static string GetRootPath(string libraryRoot, string filePath, bool isSeries)
    {
        var fileDir = Path.GetDirectoryName(filePath) ?? libraryRoot;
        if (!isSeries)
        {
            return LibraryCatalog.NormalizePath(fileDir);
        }

        var relative = Path.GetRelativePath(libraryRoot, filePath);
        var separatorIndex = relative.IndexOfAny(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
        if (separatorIndex > 0)
        {
            var firstSegment = relative[..separatorIndex];
            return LibraryCatalog.NormalizePath(Path.Combine(libraryRoot, firstSegment));
        }

        return LibraryCatalog.NormalizePath(fileDir);
    }

    private static string? TryGetHash(string content)
    {
        var text = content.Trim();
        if (text.Length == 0 || !text.Contains("/stream/", StringComparison.Ordinal)
            || !Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var query = QueryHelpers.ParseQuery(uri.Query);
        if (!query.TryGetValue("link", out var value)
            || !query.TryGetValue("index", out var index)
            || !query.ContainsKey("play")
            || !int.TryParse(index.ToString(), out _))
        {
            return null;
        }

        var hash = value.ToString().Trim();
        return string.IsNullOrWhiteSpace(hash) ? null : hash;
    }

    private static bool IsUnder(string path, string parent)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(parent))
        {
            return false;
        }

        try
        {
            var file = Path.GetFullPath(path);
            var dir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
            if (string.Equals(file, dir, Comparison))
            {
                return true;
            }

            return file.StartsWith(dir + Path.DirectorySeparatorChar, Comparison)
                || file.StartsWith(dir + Path.AltDirectorySeparatorChar, Comparison);
        }
        catch
        {
            return false;
        }
    }

    private static bool PathEquals(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second))
        {
            return false;
        }

        try
        {
            return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), Comparison);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsHash(string hash) => !string.IsNullOrWhiteSpace(hash);

    private static StringComparison Comparison => LibraryCatalog.PathComparison;
}
