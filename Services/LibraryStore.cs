using System.Text;
using System.Text.Json;
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

internal sealed class LibraryManifest
{
    public List<StoredItem> Items { get; set; } = new();
}

public sealed class LibraryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<LibraryStore> _logger;

    public LibraryStore(ILogger<LibraryStore> logger)
    {
        _logger = logger;
    }

    public async Task<(StoredItem Item, IReadOnlyList<string> ObsoleteHashes)> AddOrMergeAsync(
        StoredItem incoming,
        CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var manifest = await LoadInternalAsync(cancellationToken).ConfigureAwait(false);
            var existing = manifest.Items.FirstOrDefault(i => PathEquals(i.RootPath, incoming.RootPath));
            var before = existing is null
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : existing.Files.Select(f => f.Hash).Where(IsHash).ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (existing is null)
            {
                manifest.Items.Add(incoming);
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

            var after = manifest.Items
                .SelectMany(i => i.Files)
                .Select(f => f.Hash)
                .Where(IsHash)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var obsolete = before.Where(h => !after.Contains(h)).ToList();

            await SaveInternalAsync(manifest, cancellationToken).ConfigureAwait(false);
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
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var manifest = await LoadInternalAsync(cancellationToken).ConfigureAwait(false);
            var updated = 0;

            foreach (var item in manifest.Items)
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
                        _logger.LogWarning(ex, "Не удалось прочитать .strm: {Path}", file.Path);
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
                        await File.WriteAllTextAsync(file.Path, rewritten, new UTF8Encoding(false), cancellationToken)
                            .ConfigureAwait(false);
                        updated++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Не удалось обновить .strm: {Path}", file.Path);
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
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var manifest = await LoadInternalAsync(cancellationToken).ConfigureAwait(false);
            var before = manifest.Items
                .SelectMany(i => i.Files)
                .Select(f => f.Hash)
                .Where(IsHash)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var changed = false;

            foreach (var item in manifest.Items)
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

            manifest.Items.RemoveAll(i => i.Files.Count == 0);
            var after = manifest.Items
                .SelectMany(i => i.Files)
                .Select(f => f.Hash)
                .Where(IsHash)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var obsolete = affected.Where(h => !after.Contains(h)).ToList();
            await SaveInternalAsync(manifest, cancellationToken).ConfigureAwait(false);
            return obsolete;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<LibraryManifest> LoadInternalAsync(CancellationToken cancellationToken)
    {
        var path = GetManifestPath();
        if (!File.Exists(path))
        {
            return new LibraryManifest();
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var manifest = await JsonSerializer.DeserializeAsync<LibraryManifest>(stream, JsonOptions, cancellationToken).ConfigureAwait(false)
                ?? new LibraryManifest();
            manifest.Items ??= new List<StoredItem>();
            foreach (var item in manifest.Items)
            {
                item.Files ??= new List<StoredFile>();
            }

            return manifest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось прочитать манифест TorrServerStrm: {Path}", path);
            try
            {
                File.Move(path, path + ".corrupt-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss"), true);
            }
            catch (Exception moveEx)
            {
                _logger.LogWarning(moveEx, "Не удалось сохранить повреждённый манифест TorrServerStrm");
            }

            return new LibraryManifest();
        }
    }

    private async Task SaveInternalAsync(LibraryManifest manifest, CancellationToken cancellationToken)
    {
        var path = GetManifestPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";

        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, manifest, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(temp, path, true);
    }

    private static string GetManifestPath()
    {
        var dataFolder = Plugin.Instance?.DataFolderPath;
        if (string.IsNullOrWhiteSpace(dataFolder))
        {
            throw new InvalidOperationException("Папка данных плагина недоступна.");
        }

        return Path.Combine(dataFolder, "library.json");
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

    private static StringComparison Comparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;
}
