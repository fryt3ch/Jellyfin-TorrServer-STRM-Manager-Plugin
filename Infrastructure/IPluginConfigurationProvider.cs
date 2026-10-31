using Jellyfin.Plugin.TorrServerStrm.Configuration;

namespace Jellyfin.Plugin.TorrServerStrm.Infrastructure;

public interface IPluginConfigurationProvider
{
    PluginConfiguration? Current { get; }
}

public sealed class PluginConfigurationProvider : IPluginConfigurationProvider
{
    public PluginConfiguration? Current => Plugin.Instance?.Configuration;
}

public static class PluginConfigurationProviderExtensions
{
    public static PluginConfiguration RequireConfiguration(this IPluginConfigurationProvider provider)
    {
        return provider.Current
            ?? throw new InvalidOperationException("Plugin configuration is unavailable.");
    }
}
