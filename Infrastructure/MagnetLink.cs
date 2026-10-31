namespace Jellyfin.Plugin.TorrServerStrm.Infrastructure;

public static class MagnetLink
{
    public static bool IsValid(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.TrimStart().StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase);
    }

    public static string? GetDisplayName(string? magnet)
    {
        if (string.IsNullOrWhiteSpace(magnet))
        {
            return null;
        }

        var queryStart = magnet.IndexOf('?');
        if (queryStart < 0)
        {
            return null;
        }

        foreach (var part in magnet[(queryStart + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = part.IndexOf('=');
            if (equals <= 0 || !part[..equals].Equals("dn", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = part[(equals + 1)..].Replace("+", " ");
            try
            {
                return Uri.UnescapeDataString(value).Trim();
            }
            catch
            {
                return value.Trim();
            }
        }

        return null;
    }
}
