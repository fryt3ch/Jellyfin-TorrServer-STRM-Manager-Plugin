using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.TorrServerStrm.Services;

public sealed class ParsedTitle
{
    public string Title { get; init; } = string.Empty;

    public int? Year { get; init; }

    public string Type { get; init; } = "movie";
}

public static class TitleParser
{
    private static readonly HashSet<string> VideoExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".avi", ".mov", ".m4v", ".ts", ".m2ts", ".wmv", ".webm", ".mpg", ".mpeg"
    };

    private static readonly Regex SeriesMarker = new(
        @"(?:^|[\s._\-])(?:s\d{1,2}(?:[\s._:\-–—]*e\d{1,3})?|\d{1,2}x\d{2,3}|(?:season|сезон|сери[яи]|эпизод|episode)[\s._:\-–—]*\d{1,2})(?=$|[\s._\-])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SeriesWord = new(
        @"(?<![a-zа-я])(?:series|сериал)(?![a-zа-я])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StrongTag = new(
        @"(?:^|[\s._\-])(?:2160p|1080p|720p|480p|4k|uhd|web[\s._-]?dl|web[\s._-]?rip|blu[\s._-]?ray|bdrip|brrip|bdremux|hdtv|dvdrip|remux|x264|x265|h264|h265|hevc|avc|av1|10bit|8bit)(?=$|[\s._\-])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ReleaseTag = new(
        @"(?<![a-zа-я0-9])(?:hdr10\+?|hdr|dolby[\s._-]?vision|dv|atmos|truehd|dts(?:[\s._-]?hd)?|aac|ac3|eac3|ddp?|flac|opus|proper|repack|extended|unrated|directors[\s._-]?cut|imax|yts(?:\.mx)?|rarbg|torrenting|rutor|nnmclub|mvo|dvo|avo|lostfilm|hdrezka)(?![a-zа-я0-9])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ReleaseGroup = new(
        @"-[A-Z][A-Z0-9]{2,}$",
        RegexOptions.Compiled);

    private static readonly Regex BracketYear = new(
        @"[\(\[]\s*(?:19|20)\d{2}\s*[\)\]]",
        RegexOptions.Compiled);

    private static readonly Regex LooseYear = new(
        @"(?<!\d)((?:19|20)\d{2})(?!\d)",
        RegexOptions.Compiled);

    private static readonly Regex Brackets = new(
        @"[\[\{][^\]\}]*[\]\}]",
        RegexOptions.Compiled);

    private static readonly Regex Domain = new(
        @"\b(?:https?://)?(?:www\.)?[a-z0-9-]+(?:\.[a-z0-9-]+)*\.(?:com|net|org|info|me|tv|ru|io|to|cc|mx|in|pro|site|club|online)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SeasonFolder = new(
        @"^(?:season|сезон|s)[\s._-]*\d+$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    private static readonly Regex Junk = new(@"(^[\s\-–—:|+/]+|[\s\-–—:|+/]+$)", RegexOptions.Compiled);

    private static readonly Regex Latin = new(@"[A-Za-z]", RegexOptions.Compiled);

    private static readonly Regex Cyrillic = new(@"[А-Яа-яЁё]", RegexOptions.Compiled);

    public static ParsedTitle ParseMagnet(string? magnet)
    {
        var displayName = GetMagnetDisplayName(magnet);
        return string.IsNullOrWhiteSpace(displayName)
            ? new ParsedTitle()
            : ParseName(displayName, null);
    }

    public static ParsedTitle DetectFromTorrent(TorrentInfo torrent, string type)
    {
        var videos = (torrent.FileStats ?? new List<TorrentFile>())
            .Where(f => VideoExt.Contains(Path.GetExtension(f.Path)))
            .ToList();
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(torrent.Title))
        {
            candidates.Add(torrent.Title);
        }

        if (type == "series")
        {
            var show = GuessShowName(videos);
            if (!string.IsNullOrWhiteSpace(show))
            {
                candidates.Add(show);
            }

            candidates.AddRange(videos.Select(v => Path.GetFileNameWithoutExtension(v.Path)));
        }
        else
        {
            var main = videos.OrderByDescending(v => v.Length).FirstOrDefault();
            if (main is not null)
            {
                candidates.Add(Path.GetFileNameWithoutExtension(main.Path));
            }
        }

        foreach (var candidate in candidates.Where(c => !string.IsNullOrWhiteSpace(c)))
        {
            var parsed = ParseName(candidate, type);
            if (!string.IsNullOrWhiteSpace(parsed.Title))
            {
                return parsed;
            }
        }

        return new ParsedTitle { Type = type };
    }

    public static string DetectType(TorrentInfo torrent)
    {
        if (SeriesMarker.IsMatch(torrent.Title ?? string.Empty))
        {
            return "series";
        }

        var videos = (torrent.FileStats ?? new List<TorrentFile>())
            .Where(f => VideoExt.Contains(Path.GetExtension(f.Path)))
            .ToList();

        if (videos.Any(v => SeriesMarker.IsMatch(v.Path)))
        {
            return "series";
        }

        return "movie";
    }

    public static ParsedTitle ParseName(string? raw, string? forcedType)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new ParsedTitle { Type = forcedType ?? "movie" };
        }

        var type = forcedType ?? DetectType(raw);
        var year = ExtractYear(raw);
        return new ParsedTitle
        {
            Title = CleanTitle(raw, type, year),
            Year = year,
            Type = type
        };
    }

    public static int? ExtractYear(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var maxYear = DateTime.UtcNow.Year + 2;
        var bracket = Regex.Matches(text, @"[\(\[]\s*((?:19|20)\d{2})\s*[\)\]]");
        foreach (Match match in bracket)
        {
            var value = int.Parse(match.Groups[1].Value);
            if (value >= 1900 && value <= maxYear)
            {
                return value;
            }
        }

        var loose = LooseYear.Matches(text)
            .Cast<Match>()
            .Select(m => int.Parse(m.Groups[1].Value))
            .Where(y => y >= 1900 && y <= maxYear)
            .ToList();

        return loose.Count > 0 ? loose[^1] : null;
    }

    private static string DetectType(string raw)
    {
        return SeriesMarker.IsMatch(raw) || SeriesWord.IsMatch(raw) ? "series" : "movie";
    }

    private static string CleanTitle(string raw, string type, int? year)
    {
        var text = Domain.Replace(raw.Trim(), " ");
        text = Brackets.Replace(text, " ");
        text = BracketYear.Replace(text, " ");

        var series = SeriesMarker.Match(text);
        if (type == "series" && series.Success)
        {
            text = text[..series.Index];
        }
        else
        {
            text = SeriesMarker.Replace(text, " ");
        }

        var tag = StrongTag.Match(text);
        if (tag.Success)
        {
            text = text[..tag.Index];
        }

        text = PickTitleSegment(text);
        text = ReleaseTag.Replace(text, " ");
        text = text.Replace('_', ' ').Replace('.', ' ');
        if (year.HasValue)
        {
            var withoutYear = Regex.Replace(text, $@"(?<!\d){year}(?!\d)", " ");
            if (!string.IsNullOrWhiteSpace(withoutYear))
            {
                text = withoutYear;
            }
        }

        text = ReleaseGroup.Replace(text, string.Empty);
        text = Spaces.Replace(text, " ").Trim();
        text = Junk.Replace(text, string.Empty);
        return text.Trim();
    }

    private static string PickTitleSegment(string text)
    {
        var parts = text
            .Split(new[] { " / " }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim().Trim('/').Trim())
            .Where(p => p.Length > 0)
            .ToList();

        if (parts.Count == 0)
        {
            return text;
        }

        if (parts.Count == 1 || !parts.Any(p => Cyrillic.IsMatch(p)))
        {
            return text;
        }

        var latin = parts.FirstOrDefault(p => Latin.IsMatch(p) && !Cyrillic.IsMatch(p));
        return latin ?? parts[0];
    }

    private static string? GuessShowName(IReadOnlyList<TorrentFile> videos)
    {
        var names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var video in videos)
        {
            var dir = Path.GetDirectoryName(video.Path);
            if (string.IsNullOrWhiteSpace(dir))
            {
                continue;
            }

            var leaf = Path.GetFileName(dir);
            if (SeasonFolder.IsMatch(leaf))
            {
                var parent = Path.GetDirectoryName(dir);
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
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kv => kv.Key)
            .FirstOrDefault();
    }

    private static string? GetMagnetDisplayName(string? magnet)
    {
        if (string.IsNullOrWhiteSpace(magnet))
        {
            return null;
        }

        var queryStart = magnet.IndexOf('?');
        if (queryStart < 0)
        {
            return null;
        }

        foreach (var part in magnet[(queryStart + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = part.IndexOf('=');
            if (equals <= 0 || !part[..equals].Equals("dn", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = part[(equals + 1)..].Replace("+", " ");
            try
            {
                return Uri.UnescapeDataString(value).Trim();
            }
            catch
            {
                return value.Trim();
            }
        }

        return null;
    }
}
