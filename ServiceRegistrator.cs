using Jellyfin.Plugin.TorrServerStrm.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.TorrServerStrm;

public sealed class ServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<LibraryCatalog>();
        serviceCollection.AddSingleton<StrmIndex>();
        serviceCollection.AddSingleton<TorrentRepairService>();
        serviceCollection.AddSingleton<StrmMaintenanceService>();
        serviceCollection.AddSingleton<TorrentCleanupService>();
        serviceCollection.AddSingleton<StrmUrlRewriter>();
        serviceCollection.AddHostedService<StrmIndexStartupService>();
    }
}
