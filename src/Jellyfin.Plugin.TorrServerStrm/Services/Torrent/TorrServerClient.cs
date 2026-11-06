using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.TorrServerStrm.Domain;
using Jellyfin.Plugin.TorrServerStrm.Infrastructure;

namespace Jellyfin.Plugin.TorrServerStrm.Services.Torrent;

public sealed class TorrServerClient : ITorrServerClient
{
    private const int MetadataAttempts = 30;
    private static readonly TimeSpan MetadataDelay = TimeSpan.FromSeconds(2);
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpFactory;
    private readonly IPluginConfigurationProvider _configurationProvider;

    public TorrServerClient(
        IHttpClientFactory httpFactory,
        IPluginConfigurationProvider configurationProvider)
    {
        _httpFactory = httpFactory;
        _configurationProvider = configurationProvider;
    }

    public async Task<TorrentInfo> AddMagnetAsync(
        string magnet,
        string? title,
        CancellationToken cancellationToken)
    {
        var info = await SendAsync<TorrentInfo>(
            new { action = "add", link = magnet, title = title ?? string.Empty, poster = "", data = "", save_to_db = true },
            cancellationToken).ConfigureAwait(false);

        EnsureHash(info.Hash, "TorrServer did not return the torrent hash.");

        for (var attempt = 0; attempt < MetadataAttempts && !HasFiles(info); attempt++)
        {
            await Task.Delay(MetadataDelay, cancellationToken).ConfigureAwait(false);
            info = await SendAsync<TorrentInfo>(
                new { action = "get", hash = info.Hash },
                cancellationToken).ConfigureAwait(false);
        }

        if (!HasFiles(info))
        {
            throw new InvalidOperationException("TorrServer did not return the file list (metadata was not received).");
        }

        return info;
    }

    public async Task AddByHashAsync(string hash, string? title, CancellationToken cancellationToken)
    {
        EnsureHash(hash, "Torrent hash is not specified.");
        var info = await SendAsync<TorrentInfo>(
            new { action = "add", link = hash, title = title ?? string.Empty, poster = "", data = "", save_to_db = true },
            cancellationToken).ConfigureAwait(false);

        EnsureHash(info.Hash, "TorrServer did not return the torrent hash.");
    }

    public async Task RemoveAsync(string hash, CancellationToken cancellationToken)
    {
        EnsureHash(hash, "Torrent hash is not specified.");
        await SendRawAsync(new { action = "rem", hash }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<HashSet<string>> GetHashesAsync(CancellationToken cancellationToken)
    {
        var text = await SendRawAsync(new { action = "list" }, cancellationToken).ConfigureAwait(false);
        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
        {
            return hashes;
        }

        using var document = JsonDocument.Parse(text);
        ThrowIfError(document.RootElement);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return hashes;
        }

        var items = JsonSerializer.Deserialize<List<TorrentInfo>>(text, SerializerOptions) ?? new List<TorrentInfo>();
        foreach (var item in items)
        {
            if (!string.IsNullOrWhiteSpace(item.Hash))
            {
                hashes.Add(item.Hash);
            }
        }

        return hashes;
    }

    private async Task<T> SendAsync<T>(object body, CancellationToken cancellationToken)
    {
        var text = await SendRawAsync(body, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Empty response from TorrServer.");
        }

        using var document = JsonDocument.Parse(text);
        ThrowIfError(document.RootElement);

        return JsonSerializer.Deserialize<T>(text, SerializerOptions)
            ?? throw new InvalidOperationException("Empty response from TorrServer.");
    }

    private async Task<string> SendRawAsync(object body, CancellationToken cancellationToken)
    {
        var config = _configurationProvider.RequireConfiguration();
        var baseUrl = config.TorrServerUrl.TrimEnd('/');
        using var http = _httpFactory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/torrents")
        {
            Content = JsonContent.Create(body)
        };

        if (!string.IsNullOrWhiteSpace(config.Username))
        {
            var raw = Encoding.UTF8.GetBytes($"{config.Username}:{config.Password}");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(raw));
        }

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"TorrServer: {(int)response.StatusCode} {text}".Trim());
        }

        return text;
    }

    private static void ThrowIfError(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("error", out var error)
            && error.ValueKind == JsonValueKind.String)
        {
            throw new InvalidOperationException(error.GetString());
        }
    }

    private static bool HasFiles(TorrentInfo info) => info.FileStats is { Count: > 0 };

    private static void EnsureHash(string? hash, string message)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new InvalidOperationException(message);
        }
    }
}
