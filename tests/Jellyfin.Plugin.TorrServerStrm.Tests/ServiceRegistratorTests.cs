using Jellyfin.Plugin.TorrServerStrm.Services.Import;
using Jellyfin.Plugin.TorrServerStrm.Services.Index;
using Jellyfin.Plugin.TorrServerStrm.Services.Library;
using Jellyfin.Plugin.TorrServerStrm.Services.Strm;
using Jellyfin.Plugin.TorrServerStrm.Services.Title;
using Jellyfin.Plugin.TorrServerStrm.Services.Torrent;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Jellyfin.Plugin.TorrServerStrm.Tests;

public sealed class ServiceRegistratorTests
{
    [Fact]
    public void RegisterServices_ResolvesCompleteServiceGraph()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<ILibraryManager>());
        services.AddSingleton(Mock.Of<IHttpClientFactory>());
        services.AddSingleton(Mock.Of<IHostApplicationLifetime>());

        new ServiceRegistrator().RegisterServices(
            services,
            Mock.Of<IServerApplicationHost>());

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<LibraryCatalog>());
        Assert.NotNull(provider.GetRequiredService<ITitleParser>());
        Assert.NotNull(provider.GetRequiredService<IStrmScanner>());
        Assert.NotNull(provider.GetRequiredService<IStrmIndex>());
        Assert.NotNull(provider.GetRequiredService<IStrmWriter>());
        Assert.NotNull(provider.GetRequiredService<IStrmUrlRewriter>());
        Assert.NotNull(provider.GetRequiredService<ITorrServerClient>());
        Assert.NotNull(provider.GetRequiredService<ITorrentRemovalService>());
        Assert.NotNull(provider.GetRequiredService<ITorrentRepairService>());
        Assert.NotNull(provider.GetRequiredService<StrmMaintenanceService>());
        Assert.NotNull(provider.GetRequiredService<IMediaImportService>());
        Assert.NotEmpty(provider.GetServices<IHostedService>());
        Assert.NotEmpty(provider.GetServices<ILibraryPostScanTask>());
    }
}
