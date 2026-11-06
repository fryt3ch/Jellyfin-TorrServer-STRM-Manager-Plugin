using Jellyfin.Plugin.TorrServerStrm.Domain;

namespace Jellyfin.Plugin.TorrServerStrm.Services.Import;

public interface IMediaImportService
{
    Task<ImportResult> ImportAsync(ImportRequest request, CancellationToken cancellationToken);
}

public sealed class MediaImportValidationException : Exception
{
    public MediaImportValidationException(string message)
        : base(message)
    {
    }
}
