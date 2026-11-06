using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.TorrServerStrm.Infrastructure;

public static class EpisodeNaming
{
    private static readonly Regex SxE = new(
        @"[Ss](\d{1,2})[\s._-]*[Ee](\d{1,3})",
        RegexOptions.Compiled);

    private static readonly Regex NxM = new(
        @"(?<!\d)(\d{1,2})x(\d{2,3})(?!\d)",
        RegexOptions.Compiled);

    private static readonly Regex SeasonDirectory = new(
        @"(?:[Ss]eason|[Сс]езон|\b[Ss])[\s._-]*(\d{1,2})\b",
        RegexOptions.Compiled);

    public static (int Season, int Episode) Resolve(
        string filePath,
        IDictionary<int, int> fallbackCounters)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var match = SxE.Match(fileName);
        if (!match.Success)
        {
            match = NxM.Match(fileName);
        }

        if (match.Success)
        {
            return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
        }

        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        var seasonMatch = SeasonDirectory.Match(directory);
        var season = seasonMatch.Success ? int.Parse(seasonMatch.Groups[1].Value) : 1;
        var episode = (fallbackCounters.TryGetValue(season, out var current) ? current : 0) + 1;
        fallbackCounters[season] = episode;
        return (season, episode);
    }

    public static string MakeUniqueName(
        string show,
        int season,
        int episode,
        ISet<string> usedNames)
    {
        var baseName = $"{show} S{season:00}E{episode:00}";
        var unique = baseName;
        for (var suffix = 2; !usedNames.Add(unique); suffix++)
        {
            unique = $"{baseName} - {suffix}";
        }

        return unique;
    }
}
