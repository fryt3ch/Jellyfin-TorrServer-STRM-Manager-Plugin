using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.TorrServerStrm.Configuration;

namespace Jellyfin.Plugin.TorrServerStrm.Services;

public class TorrentFile
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;
    [JsonPropertyName("length")] public long Length { get; set; }
}

public class TorrentInfo
{
    [JsonPropertyName("hash")] public string Hash { get; set; } = string.Empty;
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("file_stats")] public List<TorrentFile>? FileStats { get; set; }
}

public class TorrServerClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public TorrServerClient(HttpClient http, PluginConfiguration cfg)
    {
        _http = http;
        _baseUrl = cfg.TorrServerUrl.TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(cfg.Username))
        {
            var raw = Encoding.UTF8.GetBytes($"{cfg.Username}:{cfg.Password}");
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(raw));
        }
    }

    public async Task<TorrentInfo> AddAsync(string magnet, string? title, CancellationToken ct)
    {
        var info = await PostAsync(new { action = "add", link = magnet, title = title ?? string.Empty, poster = "", data = "", save_to_db = true }, ct);
        if (string.IsNullOrWhiteSpace(info.Hash))
        {
            throw new InvalidOperationException("TorrServer не вернул hash торрента.");
        }

        // метаданные могут подтянуться не сразу — подождём до ~60 сек
        for (var i = 0; i < 30 && (info.FileStats == null || info.FileStats.Count == 0); i++)
        {
            await Task.Delay(2000, ct);
            info = await PostAsync(new { action = "get", hash = info.Hash }, ct);
        }

        if (info.FileStats == null || info.FileStats.Count == 0)
        {
            throw new InvalidOperationException("TorrServer не вернул список файлов (метаданные не получены).");
        }

        return info;
    }

    public async Task RemoveAsync(string hash, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new ArgumentException("Не указан hash торрента.", nameof(hash));
        }

        using var resp = await _http.PostAsJsonAsync($"{_baseUrl}/torrents", new { action = "rem", hash }, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"TorrServer: {(int)resp.StatusCode} {text}".Trim());
        }
    }

    public async Task<HashSet<string>> GetExistingHashesAsync(CancellationToken ct)
    {
        using var resp = await _http.PostAsJsonAsync($"{_baseUrl}/torrents", new { action = "list" }, ct).ConfigureAwait(false);
        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"TorrServer: {(int)resp.StatusCode} {text}".Trim());
        }

        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
        {
            return hashes;
        }

        using var doc = JsonDocument.Parse(text);
        if (doc.RootElement.ValueKind == JsonValueKind.Object
            && doc.RootElement.TryGetProperty("error", out var error)
            && error.ValueKind == JsonValueKind.String)
        {
            throw new InvalidOperationException(error.GetString());
        }

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return hashes;
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var list = JsonSerializer.Deserialize<List<TorrentInfo>>(text, options) ?? new List<TorrentInfo>();
        foreach (var item in list)
        {
            if (!string.IsNullOrWhiteSpace(item.Hash))
            {
                hashes.Add(item.Hash);
            }
        }

        return hashes;
    }

    public async Task AddByHashAsync(string hash, string? title, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(hash))
        {
            throw new ArgumentException("Не указан hash торрента.", nameof(hash));
        }

        var info = await PostAsync(
            new { action = "add", link = hash, title = title ?? string.Empty, poster = "", data = "", save_to_db = true },
            ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(info.Hash))
        {
            throw new InvalidOperationException("TorrServer не вернул hash торрента.");
        }
    }

    private async Task<TorrentInfo> PostAsync(object body, CancellationToken ct)
    {
        using var resp = await _http.PostAsJsonAsync($"{_baseUrl}/torrents", body, ct);
        var text = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"TorrServer: {(int)resp.StatusCode} {text}".Trim());
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Пустой ответ от TorrServer.");
        }

        using var doc = JsonDocument.Parse(text);
        if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
        {
            throw new InvalidOperationException(error.GetString());
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var info = JsonSerializer.Deserialize<TorrentInfo>(text, options);
        return info ?? throw new InvalidOperationException("Пустой ответ от TorrServer.");
    }
}
