namespace Jellyfin.Plugin.TorrServerStrm.Domain;

public sealed record WrittenFile
{
    public string Path { get; init; } = string.Empty;

    public bool Existed { get; init; }

    public string? PreviousContent { get; init; }
}

public sealed record WriteResult
{
    public string RootPath { get; init; } = string.Empty;

    public string LibraryRoot { get; init; } = string.Empty;

    public IReadOnlyList<WrittenFile> Files { get; init; } = new List<WrittenFile>();
}
