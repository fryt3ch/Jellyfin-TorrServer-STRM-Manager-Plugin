using Jellyfin.Plugin.TorrServerStrm.Domain;
using Jellyfin.Plugin.TorrServerStrm.Infrastructure;

namespace Jellyfin.Plugin.TorrServerStrm.Services.Index;

public sealed class StrmIndex : IStrmIndex
{
    private readonly SemaphoreSlim _snapshotGate = new(1, 1);
    private readonly SemaphoreSlim _mutationGate = new(1, 1);
    private readonly IStrmScanner _scanner;
    private readonly ILogger<StrmIndex> _logger;
    private volatile IndexSnapshot _snapshot = IndexSnapshot.Empty;
    private volatile bool _loaded;

    public StrmIndex(
        IStrmScanner scanner,
        ILogger<StrmIndex> logger)
    {
        _scanner = scanner;
        _logger = logger;
    }

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        await _snapshotGate.WaitAsync(cancellationToken).ConfigureAwait(false);
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
            _snapshotGate.Release();
        }
    }

    public async Task ReloadAsync(CancellationToken cancellationToken)
    {
        await _snapshotGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await LoadCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _snapshotGate.Release();
        }
    }

    public IndexSnapshot GetSnapshot() => _snapshot;

    public async Task<IndexMergeResult> AddOrMergeAsync(
        StoredItem incoming,
        CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        return await MutateAsync<IndexMergeResult>(() =>
        {
            var current = _snapshot;
            var items = current.Items.ToList();
            var existingIndex = items.FindIndex(
                item => PathHelper.Equals(item.RootPath, incoming.RootPath));

            var before = existingIndex < 0
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : items[existingIndex].Files
                    .Select(file => file.Hash)
                    .Where(IsHash)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

            StoredItem merged;
            if (existingIndex < 0)
            {
                merged = incoming with { Files = incoming.Files.ToList() };
                items.Add(merged);
            }
            else
            {
                var existing = items[existingIndex];
                var files = existing.Files
                    .Where(file => !incoming.Files.Any(
                        candidate => PathHelper.Equals(file.Path, candidate.Path)))
                    .Concat(incoming.Files)
                    .ToList();

                merged = existing with
                {
                    Kind = incoming.Kind,
                    Title = incoming.Title,
                    Year = incoming.Year,
                    LibraryRoot = incoming.LibraryRoot,
                    Files = files
                };
                items[existingIndex] = merged;
            }

            var after = items
                .SelectMany(item => item.Files)
                .Select(file => file.Hash)
                .Where(IsHash)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var obsolete = before.Where(hash => !after.Contains(hash)).ToList();

            _snapshot = current with { Items = items };
            _logger.LogInformation(
                "TorrServerStrm: index updated for {Title} ({Files} file(s))",
                merged.Title,
                merged.Files.Count);

            return new IndexMergeResult(merged, obsolete);
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> RemoveMissingFilesAsync(
        string removedPath,
        CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        return await MutateAsync<IReadOnlyList<string>>(() =>
        {
            var current = _snapshot;
            var before = current.Items
                .SelectMany(item => item.Files)
                .Select(file => file.Hash)
                .Where(IsHash)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var affected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var changed = false;
            var items = new List<StoredItem>(current.Items.Count);

            foreach (var item in current.Items)
            {
                if (!string.IsNullOrWhiteSpace(item.LibraryRoot)
                    && !Directory.Exists(item.LibraryRoot))
                {
                    items.Add(item);
                    continue;
                }

                var files = new List<StoredFile>(item.Files.Count);
                var itemChanged = false;
                foreach (var file in item.Files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (PathHelper.IsUnder(file.Path, removedPath) && !File.Exists(file.Path))
                    {
                        itemChanged = true;
                        if (IsHash(file.Hash))
                        {
                            affected.Add(file.Hash);
                        }

                        continue;
                    }

                    files.Add(file);
                }

                if (!itemChanged)
                {
                    items.Add(item);
                    continue;
                }

                changed = true;
                if (files.Count > 0)
                {
                    items.Add(item with { Files = files });
                }
            }

            if (!changed)
            {
                return Array.Empty<string>();
            }

            _snapshot = current with { Items = items };
            var after = items
                .SelectMany(item => item.Files)
                .Select(file => file.Hash)
                .Where(IsHash)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return affected.Where(hash => !after.Contains(hash)).ToList();
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> MutateAsync<T>(Func<T> mutation, CancellationToken cancellationToken)
    {
        await _snapshotGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return mutation();
            }
            finally
            {
                _mutationGate.Release();
            }
        }
        finally
        {
            _snapshotGate.Release();
        }
    }

    private async Task LoadCoreAsync(CancellationToken cancellationToken)
    {
        var items = await _scanner.ScanAsync(cancellationToken).ConfigureAwait(false);
        await _mutationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _snapshot = new IndexSnapshot
            {
                LoadedAt = DateTimeOffset.UtcNow,
                Items = items
            };
            _loaded = true;
        }
        finally
        {
            _mutationGate.Release();
        }
    }

    private static bool IsHash(string hash) => !string.IsNullOrWhiteSpace(hash);
}
