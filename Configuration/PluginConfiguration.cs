using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.TorrServerStrm.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>Адрес TorrServer для API (с точки зрения сервера Jellyfin).</summary>
    public string TorrServerUrl { get; set; } = "http://localhost:8090";

    /// <summary>Адрес TorrServer, который попадёт в .strm (если клиенты видят его по другому адресу). Пусто = как TorrServerUrl.</summary>
    public string StreamBaseUrl { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string MoviesPath { get; set; } = "/media/movies";

    public string SeriesPath { get; set; } = "/media/series";

    public bool ScanAfterAdd { get; set; } = true;
}
