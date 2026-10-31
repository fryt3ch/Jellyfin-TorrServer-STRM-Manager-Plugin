using Jellyfin.Plugin.TorrServerStrm.Domain;
using Jellyfin.Plugin.TorrServerStrm.Services.Library;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Moq;

namespace Jellyfin.Plugin.TorrServerStrm.Tests;

public sealed class LibraryCatalogTests
{
    [Fact]
    public void GetLocations_ReportsCompatibilityPerMediaKind()
    {
        var catalog = CreateCatalog(
            ("Movies", CollectionTypeOptions.movies, "/libs/movies"),
            ("Shows", CollectionTypeOptions.tvshows, "/libs/shows"),
            ("Music", CollectionTypeOptions.music, "/libs/music"));

        var locations = catalog.GetLocations();

        var movies = locations.Single(library => library.Type == "movies");
        var shows = locations.Single(library => library.Type == "tvshows");
        var music = locations.Single(library => library.Type == "music");

        Assert.True(movies.IsMovieCompatible);
        Assert.False(movies.IsSeriesCompatible);
        Assert.False(shows.IsMovieCompatible);
        Assert.True(shows.IsSeriesCompatible);
        Assert.True(music.IsMovieCompatible);
        Assert.True(music.IsSeriesCompatible);
    }

    [Fact]
    public void Resolve_PrefersMatchingPrimaryLibraryType()
    {
        var catalog = CreateCatalog(
            ("Music", CollectionTypeOptions.music, "/libs/music"),
            ("Shows", CollectionTypeOptions.tvshows, "/libs/shows"),
            ("Movies", CollectionTypeOptions.movies, "/libs/movies"));

        var movie = catalog.Resolve(null, MediaKind.Movie);
        var series = catalog.Resolve(null, MediaKind.Series);

        Assert.Equal("movies", movie.Type);
        Assert.Equal("tvshows", series.Type);
    }

    [Fact]
    public void Resolve_HonorsRequestedCompatiblePath()
    {
        var catalog = CreateCatalog(
            ("Movies", CollectionTypeOptions.movies, "/libs/movies"),
            ("Shows", CollectionTypeOptions.tvshows, "/libs/shows"));

        var resolved = catalog.Resolve("/libs/shows", MediaKind.Series);

        Assert.Equal("/libs/shows", resolved.Path);
    }

    [Fact]
    public void Resolve_ThrowsWhenNoLibrariesExist()
    {
        var catalog = CreateCatalog();

        Assert.Throws<InvalidOperationException>(() => catalog.Resolve(null, MediaKind.Movie));
    }

    private static LibraryCatalog CreateCatalog(
        params (string Name, CollectionTypeOptions Type, string Path)[] folders)
    {
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager
            .Setup(manager => manager.GetVirtualFolders())
            .Returns(folders
                .Select(folder => new VirtualFolderInfo
                {
                    Name = folder.Name,
                    CollectionType = folder.Type,
                    Locations = new[] { folder.Path }
                })
                .ToList());

        return new LibraryCatalog(libraryManager.Object);
    }
}
