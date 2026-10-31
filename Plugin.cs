using Jellyfin.Plugin.TorrServerStrm.Configuration;
using Jellyfin.Plugin.TorrServerStrm.Infrastructure;
using Jellyfin.Plugin.TorrServerStrm.Services.Strm;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.TorrServerStrm;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly IStrmUrlRewriter _urlRewriter;
    private readonly IBackgroundTaskRunner _backgroundTasks;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public Plugin(
        IApplicationPaths applicationPaths,
        IXmlSerializer xmlSerializer,
        IStrmUrlRewriter urlRewriter,
        IBackgroundTaskRunner backgroundTasks,
        IHttpContextAccessor httpContextAccessor)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
        _urlRewriter = urlRewriter;
        _backgroundTasks = backgroundTasks;
        _httpContextAccessor = httpContextAccessor;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "TorrServer STRM Manager";

    public override Guid Id => Guid.Parse("7c1f3a52-9d4e-4b8a-a6f1-2e5b8c9d0f13");

    public override string Description => "Add magnet links via TorrServer as .strm files";

    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        var previousBaseUrl = Configuration.ResolveBaseUrl();
        base.UpdateConfiguration(configuration);

        var newBaseUrl = Configuration.ResolveBaseUrl();
        if (string.IsNullOrWhiteSpace(newBaseUrl)
            || string.Equals(previousBaseUrl, newBaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _backgroundTasks.Run(
            token => _urlRewriter.RewriteAsync(newBaseUrl, token),
            "update addresses in existing .strm files");
    }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        var settingsPage = BuildSettingsPage();
        var addPage = BuildAddPage();

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

    private PluginPageInfo BuildSettingsPage()
    {
        var ns = GetType().Namespace;
        return new PluginPageInfo
        {
            Name = "TorrServerStrm",
            DisplayName = "TorrServer STRM Manager Settings",
            EmbeddedResourcePath = $"{ns}.Configuration.configPage.html"
        };
    }

    private PluginPageInfo BuildAddPage()
    {
        var ns = GetType().Namespace;
        return new PluginPageInfo
        {
            Name = "TorrServerStrmAdd",
            DisplayName = "Import .magnet link",
            EmbeddedResourcePath = $"{ns}.Configuration.addPage.html",
            EnableInMainMenu = true,
            MenuSection = "server",
            MenuIcon = "cloud_download"
        };
    }
}
