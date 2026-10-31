using Jellyfin.Plugin.TorrServerStrm.Configuration;
using Jellyfin.Plugin.TorrServerStrm.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.TorrServerStrm;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly StrmUrlRewriter _urlRewriter;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<Plugin> _logger;

    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        TorrentCleanupService cleanupService,
        StrmUrlRewriter urlRewriter,
        IHttpContextAccessor httpContextAccessor,
        ILogger<Plugin> logger)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        _urlRewriter = urlRewriter;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _ = cleanupService;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "TorrServer STRM Manager";

    public override Guid Id => Guid.Parse("7c1f3a52-9d4e-4b8a-a6f1-2e5b8c9d0f13");

    public override string Description => "Add magnet links via TorrServer as .strm files";

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
                _logger.LogWarning(ex, "TorrServerStrm: failed to update addresses in existing .strm files");
            }
        });
    }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        var ns = GetType().Namespace;
        var settingsPage = new PluginPageInfo
        {
            Name = "TorrServerStrm",
            DisplayName = "TorrServer STRM Manager Settings",
            EmbeddedResourcePath = $"{ns}.Configuration.configPage.html"
        };
        var addPage = new PluginPageInfo
        {
            Name = "TorrServerStrmAdd",
            DisplayName = "Import .magnet link",
            EmbeddedResourcePath = $"{ns}.Configuration.addPage.html",
            EnableInMainMenu = true,
            MenuSection = "server",
            MenuIcon = "cloud_download"
        };

        // Jellyfin 10.10 selects the plugin settings page as the first page
        // with EnableInMainMenu = true, and all such pages appear in the side menu.
        // To keep settings out of the menu but still open them from the plugin card,
        // return different page sets depending on the request.
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return new[] { settingsPage };
        }

        var path = (context.Request.Path.Value ?? string.Empty).TrimEnd('/');
        if (path.EndsWith("/web/ConfigurationPage", StringComparison.OrdinalIgnoreCase))
        {
            return new[] { settingsPage, addPage };
        }

        if (context.Request.Query.TryGetValue("enableInMainMenu", out var value)
            && bool.TryParse(value.ToString(), out var inMainMenu)
            && inMainMenu)
        {
            return new[] { addPage };
        }

        return new[] { settingsPage };
    }
}
