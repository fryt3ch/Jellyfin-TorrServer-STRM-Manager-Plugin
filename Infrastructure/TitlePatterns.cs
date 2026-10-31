using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.TorrServerStrm.Infrastructure;

public static class TitlePatterns
{
    public static readonly Regex SeriesMarker = new(
        @"(?:^|[\s._\-])(?:s\d{1,2}(?:[\s._:\-–—]*e\d{1,3})?|\d{1,2}x\d{2,3}|(?:season|сезон|сери[яи]|эпизод|episode)[\s._:\-–—]*\d{1,2})(?=$|[\s._\-])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static readonly Regex SeriesWord = new(
        @"(?<![a-zа-я])(?:series|сериал)(?![a-zа-я])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static readonly Regex StrongTag = new(
        @"(?:^|[\s._\-])(?:2160p|1080p|720p|480p|4k|uhd|web[\s._-]?dl|web[\s._-]?rip|blu[\s._-]?ray|bdrip|brrip|bdremux|hdtv|dvdrip|remux|x264|x265|h264|h265|hevc|avc|av1|10bit|8bit)(?=$|[\s._\-])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static readonly Regex ReleaseTag = new(
        @"(?<![a-zа-я0-9])(?:hdr10\+?|hdr|dolby[\s._-]?vision|dv|atmos|truehd|dts(?:[\s._-]?hd)?|aac|ac3|eac3|ddp?|flac|opus|proper|repack|extended|unrated|directors[\s._-]?cut|imax|yts(?:\.mx)?|rarbg|torrenting|rutor|nnmclub|mvo|dvo|avo|lostfilm|hdrezka)(?![a-zа-я0-9])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static readonly Regex ReleaseGroup = new(
        @"-[A-Z][A-Z0-9]{2,}$",
        RegexOptions.Compiled);

    public static readonly Regex BracketYear = new(
        @"[\(\[]\s*(?:19|20)\d{2}\s*[\)\]]",
        RegexOptions.Compiled);

    public static readonly Regex BracketYearCapture = new(
        @"[\(\[]\s*((?:19|20)\d{2})\s*[\)\]]",
        RegexOptions.Compiled);

    public static readonly Regex LooseYear = new(
        @"(?<!\d)((?:19|20)\d{2})(?!\d)",
        RegexOptions.Compiled);

    public static readonly Regex Brackets = new(
        @"[\[\{][^\]\}]*[\]\}]",
        RegexOptions.Compiled);

    public static readonly Regex Domain = new(
        @"\b(?:https?://)?(?:www\.)?[a-z0-9-]+(?:\.[a-z0-9-]+)*\.(?:com|net|org|info|me|tv|ru|io|to|cc|mx|in|pro|site|club|online)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static readonly Regex SeasonFolder = new(
        @"^(?:season|сезон|s)[\s._-]*\d+$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    public static readonly Regex Junk = new(
        @"(^[\s\-–—:|+/]+|[\s\-–—:|+/]+$)",
        RegexOptions.Compiled);

    public static readonly Regex Latin = new(@"[A-Za-z]", RegexOptions.Compiled);

    public static readonly Regex Cyrillic = new(@"[А-Яа-яЁё]", RegexOptions.Compiled);
}
