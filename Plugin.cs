using Jellyfin.Plugin.TorrServerStrm.Configuration;
using Jellyfin.Plugin.TorrServerStrm.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.TorrServerStrm;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        TorrentCleanupService cleanupService)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        _ = cleanupService;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "TorrServer STRM";

    public override Guid Id => Guid.Parse("7c1f3a52-9d4e-4b8a-a6f1-2e5b8c9d0f13");

    public override string Description => "Добавление magnet-ссылок через TorrServer как .strm файлов";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        var ns = GetType().Namespace;
        return new[]
        {
            new PluginPageInfo
            {
                Name = "TorrServerStrm",
                DisplayName = "TorrServer STRM",
                EmbeddedResourcePath = $"{ns}.Configuration.configPage.html",
                EnableInMainMenu = true,
                MenuSection = "server",
                MenuIcon = "cloud_download"
            }
        };
    }
}
