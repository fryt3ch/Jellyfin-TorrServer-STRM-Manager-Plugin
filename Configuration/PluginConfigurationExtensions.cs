namespace Jellyfin.Plugin.TorrServerStrm.Configuration;

public static class PluginConfigurationExtensions
{
    public static string ResolveBaseUrl(this PluginConfiguration configuration)
    {
        var url = string.IsNullOrWhiteSpace(configuration.StreamBaseUrl)
            ? configuration.TorrServerUrl
            : configuration.StreamBaseUrl;

        return (url ?? string.Empty).TrimEnd('/');
    }
}
