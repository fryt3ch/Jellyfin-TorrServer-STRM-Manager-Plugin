using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TorrServerStrm.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>TorrServer URL used by the Jellyfin server for API calls and written to .strm files.</summary>
    public string TorrServerUrl { get; set; } = "http://localhost:8090";

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool ScanAfterAdd { get; set; } = true;
}
