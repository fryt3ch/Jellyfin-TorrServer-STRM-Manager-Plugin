using System.Net;
using System.Text;
using Jellyfin.Plugin.TorrServerStrm.Configuration;
using Jellyfin.Plugin.TorrServerStrm.Services.Torrent;

namespace Jellyfin.Plugin.TorrServerStrm.Tests;

public sealed class TorrServerClientTests
{
    [Fact]
    public async Task GetHashes_UsesConfigurationAndBasicAuthPerRequest()
    {
        var handler = new RecordingHandler(
            """[{"hash":"H1"},{"hash":"h2"}]""");
        var client = new TorrServerClient(
            new StubHttpClientFactory(handler),
            new FakeConfigurationProvider(new PluginConfiguration
            {
                TorrServerUrl = "http://torr:8090/",
                Username = "user",
                Password = "pass"
            }));

        var hashes = await client.GetHashesAsync(CancellationToken.None);

        Assert.Equal(2, hashes.Count);
        Assert.Contains("h1", hashes);
        Assert.Contains("H2", hashes);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("http://torr:8090/torrents", request.Uri);
        Assert.Equal("Basic dXNlcjpwYXNz", request.Authorization);
        Assert.Contains("\"action\":\"list\"", request.Body);
    }

    [Fact]
    public async Task GetHashes_ThrowsTorrServerErrorMessage()
    {
        var handler = new RecordingHandler("""{"error":"boom"}""");
        var client = new TorrServerClient(
            new StubHttpClientFactory(handler),
            new FakeConfigurationProvider(new PluginConfiguration
            {
                TorrServerUrl = "http://torr:8090"
            }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GetHashesAsync(CancellationToken.None));

        Assert.Equal("boom", exception.Message);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubHttpClientFactory(HttpMessageHandler handler)
        {
            _handler = handler;
        }

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public RecordingHandler(string responseBody)
        {
            _responseBody = responseBody;
        }

        public List<RecordedRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            Requests.Add(new RecordedRequest(
                request.RequestUri?.ToString() ?? string.Empty,
                request.Headers.Authorization?.ToString(),
                body));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed record RecordedRequest(string Uri, string? Authorization, string Body);
}
