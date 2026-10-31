namespace Jellyfin.Plugin.TorrServerStrm.Domain;

public sealed record StoredFile
{
    public string Path { get; init; } = string.Empty;

    public string Hash { get; init; } = string.Empty;
}

public sealed record StoredItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public MediaKind Kind { get; init; } = MediaKind.Movie;

    public string Title { get; init; } = string.Empty;

    public int? Year { get; init; }

    public string RootPath { get; init; } = string.Empty;

    public string LibraryRoot { get; init; } = string.Empty;

    public DateTimeOffset AddedAt { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<StoredFile> Files { get; init; } = Array.Empty<StoredFile>();
}

public sealed record IndexSnapshot
{
    public DateTimeOffset LoadedAt { get; init; }

    public IReadOnlyList<StoredItem> Items { get; init; } = Array.Empty<StoredItem>();

    public static IndexSnapshot Empty { get; } = new();
}

public readonly record struct IndexMergeResult(StoredItem Item, IReadOnlyList<string> ObsoleteHashes);
