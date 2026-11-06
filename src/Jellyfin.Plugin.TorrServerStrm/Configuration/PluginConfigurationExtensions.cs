namespace Jellyfin.Plugin.TorrServerStrm.Configuration;

public static class PluginConfigurationExtensions
{
    public static string ResolveBaseUrl(this PluginConfiguration configuration)
    {
        return (configuration.TorrServerUrl ?? string.Empty).TrimEnd('/');
    }
}
