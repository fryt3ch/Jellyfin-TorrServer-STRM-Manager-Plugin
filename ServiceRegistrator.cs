using Jellyfin.Plugin.TorrServerStrm.Infrastructure;
using Jellyfin.Plugin.TorrServerStrm.Services.Import;
using Jellyfin.Plugin.TorrServerStrm.Services.Index;
using Jellyfin.Plugin.TorrServerStrm.Services.Library;
using Jellyfin.Plugin.TorrServerStrm.Services.Strm;
using Jellyfin.Plugin.TorrServerStrm.Services.Title;
using Jellyfin.Plugin.TorrServerStrm.Services.Torrent;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.TorrServerStrm;

public sealed class ServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IPluginConfigurationProvider, PluginConfigurationProvider>();
        serviceCollection.AddSingleton<IBackgroundTaskRunner, BackgroundTaskRunner>();

        serviceCollection.AddSingleton<LibraryCatalog>();
        serviceCollection.AddSingleton<ITitleParser, TitleParser>();
        serviceCollection.AddSingleton<IStrmScanner, StrmScanner>();
        serviceCollection.AddSingleton<IStrmIndex, StrmIndex>();
        serviceCollection.AddSingleton<IStrmWriter, StrmWriter>();
        serviceCollection.AddSingleton<IStrmUrlRewriter, StrmUrlRewriter>();

        serviceCollection.AddSingleton<ITorrServerClient, TorrServerClient>();
        serviceCollection.AddSingleton<ITorrentRemovalService, TorrentRemovalService>();
        serviceCollection.AddSingleton<ITorrentRepairService, TorrentRepairService>();
        serviceCollection.AddSingleton<StrmMaintenanceService>();
        serviceCollection.AddSingleton<IMediaImportService, MediaImportService>();

        serviceCollection.AddHostedService<TorrentCleanupService>();
        serviceCollection.AddHostedService<StrmIndexStartupService>();
        serviceCollection.AddSingleton<ILibraryPostScanTask, StrmLibraryPostScanTask>();
    }
}
