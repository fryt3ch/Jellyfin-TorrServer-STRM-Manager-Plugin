using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TorrServerStrm.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Адрес TorrServer API, доступный с сервера Jellyfin (например имя контейнера).</summary>
    public string TorrServerUrl { get; set; } = "http://localhost:8090";

    /// <summary>Адрес TorrServer, доступный с клиентов и записываемый в .strm. Пусто = как TorrServerUrl.</summary>
    public string StreamBaseUrl { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool ScanAfterAdd { get; set; } = true;
}
