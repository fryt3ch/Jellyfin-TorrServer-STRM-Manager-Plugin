using Jellyfin.Plugin.TorrServerStrm.Configuration;
using Jellyfin.Plugin.TorrServerStrm.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrServerStrm;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly StrmUrlRewriter _urlRewriter;
    private readonly ILogger<Plugin> _logger;

    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        TorrentCleanupService cleanupService,
        StrmUrlRewriter urlRewriter,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        _urlRewriter = urlRewriter;
        _logger = logger;
        _ = cleanupService;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "TorrServer STRM";

    public override Guid Id => Guid.Parse("7c1f3a52-9d4e-4b8a-a6f1-2e5b8c9d0f13");

    public override string Description => "Добавление magnet-ссылок через TorrServer как .strm файлов";

    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        var previousBaseUrl = StrmWriter.ResolveBaseUrl(Configuration);
        base.UpdateConfiguration(configuration);

        var newBaseUrl = StrmWriter.ResolveBaseUrl(Configuration);
        if (string.IsNullOrWhiteSpace(newBaseUrl)
            || string.Equals(previousBaseUrl, newBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _urlRewriter.RewriteAsync(newBaseUrl, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TorrServerStrm: не удалось обновить адреса в существующих .strm");
            }
        });
    }

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
