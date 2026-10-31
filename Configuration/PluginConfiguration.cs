using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TorrServerStrm.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>TorrServer API URL reachable from the Jellyfin server (for example, the container name).</summary>
    public string TorrServerUrl { get; set; } = "http://localhost:8090";

    /// <summary>TorrServer URL reachable from clients and written to .strm files. Empty = same as TorrServerUrl.</summary>
    public string StreamBaseUrl { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool ScanAfterAdd { get; set; } = true;
}
