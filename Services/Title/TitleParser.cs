using Jellyfin.Plugin.TorrServerStrm.Domain;
using Jellyfin.Plugin.TorrServerStrm.Infrastructure;

namespace Jellyfin.Plugin.TorrServerStrm.Services.Title;

public sealed class TitleParser : ITitleParser
{
    public ParsedTitle ParseMagnet(string? magnet)
    {
        var displayName = MagnetLink.GetDisplayName(magnet);
        return string.IsNullOrWhiteSpace(displayName)
            ? new ParsedTitle()
            : ParseName(displayName, null);
    }

    public ParsedTitle DetectFromTorrent(TorrentInfo torrent, MediaKind kind)
    {
        var videos = (torrent.FileStats ?? new List<TorrentFile>())
            .Where(file => MediaFileTypes.IsVideoFile(file.Path))
            .ToList();
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(torrent.Title))
        {
            candidates.Add(torrent.Title);
        }

        if (kind == MediaKind.Series)
        {
            var show = GuessShowName(videos);
            if (!string.IsNullOrWhiteSpace(show))
            {
                candidates.Add(show);
            }

            candidates.AddRange(videos.Select(video => Path.GetFileNameWithoutExtension(video.Path)));
        }
        else
        {
            var main = videos.OrderByDescending(video => video.Length).FirstOrDefault();
            if (main is not null)
            {
                candidates.Add(Path.GetFileNameWithoutExtension(main.Path));
            }
        }

        foreach (var candidate in candidates.Where(candidate => !string.IsNullOrWhiteSpace(candidate)))
        {
            var parsed = ParseName(candidate, kind);
            if (!string.IsNullOrWhiteSpace(parsed.Title))
            {
                return parsed;
            }
        }

        return new ParsedTitle { Kind = kind };
    }

    public MediaKind DetectKind(TorrentInfo torrent)
    {
        if (TitlePatterns.SeriesMarker.IsMatch(torrent.Title ?? string.Empty))
        {
            return MediaKind.Series;
        }

        var videos = (torrent.FileStats ?? new List<TorrentFile>())
            .Where(file => MediaFileTypes.IsVideoFile(file.Path));

        return videos.Any(video => TitlePatterns.SeriesMarker.IsMatch(video.Path))
            ? MediaKind.Series
            : MediaKind.Movie;
    }

    public ParsedTitle ParseName(string? raw, MediaKind? forcedKind)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new ParsedTitle { Kind = forcedKind ?? MediaKind.Movie };
        }

        var kind = forcedKind ?? DetectKind(raw);
        var year = ExtractYear(raw);
        return new ParsedTitle
        {
            Title = CleanTitle(raw, kind, year),
            Year = year,
            Kind = kind
        };
    }

    public int? ExtractYear(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var maxYear = DateTime.UtcNow.Year + 2;
        foreach (Match match in TitlePatterns.BracketYearCapture.Matches(text))
        {
            var value = int.Parse(match.Groups[1].Value);
            if (value >= 1900 && value <= maxYear)
            {
                return value;
            }
        }

        var loose = TitlePatterns.LooseYear.Matches(text)
            .Cast<Match>()
            .Select(match => int.Parse(match.Groups[1].Value))
            .Where(year => year >= 1900 && year <= maxYear)
            .ToList();

        return loose.Count > 0 ? loose[^1] : null;
    }

    private static MediaKind DetectKind(string raw)
    {
        return TitlePatterns.SeriesMarker.IsMatch(raw) || TitlePatterns.SeriesWord.IsMatch(raw)
            ? MediaKind.Series
            : MediaKind.Movie;
    }

    private static string CleanTitle(string raw, MediaKind kind, int? year)
    {
        var text = TitlePatterns.Domain.Replace(raw.Trim(), " ");
        text = TitlePatterns.Brackets.Replace(text, " ");
        text = TitlePatterns.BracketYear.Replace(text, " ");

        var series = TitlePatterns.SeriesMarker.Match(text);
        if (kind == MediaKind.Series && series.Success)
        {
            text = text[..series.Index];
        }
        else
        {
            text = TitlePatterns.SeriesMarker.Replace(text, " ");
        }

        var tag = TitlePatterns.StrongTag.Match(text);
        if (tag.Success)
        {
            text = text[..tag.Index];
        }

        text = PickTitleSegment(text);
        text = TitlePatterns.ReleaseTag.Replace(text, " ");
        text = text.Replace('_', ' ').Replace('.', ' ');
        if (year.HasValue)
        {
            var withoutYear = TitlePatterns.LooseYear.Replace(
                text,
                match => int.Parse(match.Groups[1].Value) == year.Value ? " " : match.Value);
            if (!string.IsNullOrWhiteSpace(withoutYear))
            {
                text = withoutYear;
            }
        }

        text = TitlePatterns.ReleaseGroup.Replace(text, string.Empty);
        text = TitlePatterns.Spaces.Replace(text, " ").Trim();
        text = TitlePatterns.Junk.Replace(text, string.Empty);
        return text.Trim();
    }

    private static string PickTitleSegment(string text)
    {
        var parts = text
            .Split(new[] { " / " }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim().Trim('/').Trim())
            .Where(part => part.Length > 0)
            .ToList();

        if (parts.Count == 0)
        {
            return text;
        }

        if (parts.Count == 1 || !parts.Any(part => TitlePatterns.Cyrillic.IsMatch(part)))
        {
            return text;
        }

        var latin = parts.FirstOrDefault(
            part => TitlePatterns.Latin.IsMatch(part) && !TitlePatterns.Cyrillic.IsMatch(part));
        return latin ?? parts[0];
    }

    private static string? GuessShowName(IReadOnlyList<TorrentFile> videos)
    {
        var names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var video in videos)
        {
            var directory = Path.GetDirectoryName(video.Path);
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            var leaf = Path.GetFileName(directory);
            if (TitlePatterns.SeasonFolder.IsMatch(leaf))
            {
                var parent = Path.GetDirectoryName(directory);
                if (!string.IsNullOrWhiteSpace(parent))
                {
                    leaf = Path.GetFileName(parent);
                }
            }

            if (!string.IsNullOrWhiteSpace(leaf))
            {
                names[leaf] = names.GetValueOrDefault(leaf) + 1;
            }
        }

        return names
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Key)
            .FirstOrDefault();
    }
}
