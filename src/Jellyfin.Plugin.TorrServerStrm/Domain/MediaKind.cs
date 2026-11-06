using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.TorrServerStrm.Domain;

[JsonConverter(typeof(MediaKindJsonConverter))]
public enum MediaKind
{
    Movie,
    Series
}

public static class MediaKindExtensions
{
    public static string ToApiValue(this MediaKind kind)
        => kind == MediaKind.Series ? "series" : "movie";

    public static string ToPrimaryLibraryType(this MediaKind kind)
        => kind == MediaKind.Series ? "tvshows" : "movies";

    public static string ToDisplayName(this MediaKind kind)
        => kind == MediaKind.Series ? "Series" : "Movies";

    public static bool TryParseApiValue(string? value, out MediaKind kind)
    {
        if (string.Equals(value, "series", StringComparison.OrdinalIgnoreCase))
        {
            kind = MediaKind.Series;
            return true;
        }

        if (string.Equals(value, "movie", StringComparison.OrdinalIgnoreCase))
        {
            kind = MediaKind.Movie;
            return true;
        }

        kind = MediaKind.Movie;
        return false;
    }
}

public sealed class MediaKindJsonConverter : JsonConverter<MediaKind>
{
    public override MediaKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (MediaKindExtensions.TryParseApiValue(value, out var kind))
        {
            return kind;
        }

        throw new JsonException("Type must be movie or series.");
    }

    public override void Write(Utf8JsonWriter writer, MediaKind value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToApiValue());
    }
}
