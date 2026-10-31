using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FantasyAdvisor.Core.Models;

namespace FantasyAdvisor.Core.Sources;

/// <summary>League-specific data: my roster, who is rostered, who is available. Phase 2 swaps in the Yahoo API.</summary>
public interface ILeagueDataSource
{
    string Name { get; }
    Task<LeagueSnapshot> CollectAsync(CancellationToken ct);
}

/// <summary>League-agnostic NBA player stats and injury status.</summary>
public interface IStatsSource
{
    string Name { get; }
    Task<IReadOnlyList<Player>> GetPlayersAsync(CancellationToken ct);
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
}

public static class NameNormalizer
{
    private static readonly string[] Suffixes = ["jr", "sr", "ii", "iii", "iv", "v"];

    /// <summary>Lowercase, strip accents/punctuation and generational suffixes: "Nikola Jokić Jr." -> "nikola jokic".</summary>
    public static string Normalize(string name)
    {
        var decomposed = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in decomposed)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
        }

        var parts = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (parts.Count > 1 && Suffixes.Contains(parts[^1])) parts.RemoveAt(parts.Count - 1);
        return string.Join(' ', parts);
    }
}

/// <summary>Hand-maintained league files. <c>Teams</c> maps team name to player names; <c>MyTeam</c> picks mine.</summary>
public sealed record LeagueRosterFile(string MyTeam, Dictionary<string, List<string>> Teams);

/// <summary>
/// Builds a snapshot from stats + roster files. Free agents = every NBA player nobody rosters
/// and nobody listed as unavailable. Waiver status is unknown without Yahoo, so Waivers stays empty.
/// </summary>
public static class SnapshotBuilder
{
    public static LeagueSnapshot Build(
        IReadOnlyList<Player> allPlayers,
        LeagueRosterFile rosters,
        IEnumerable<string> unavailable,
        DateTimeOffset collectedAt,
        string source)
    {
        var league = new LeagueInfo(Math.Max(rosters.Teams.Count, 1), "H2H-9CAT");

        if (rosters.Teams.Count == 0 || !rosters.Teams.TryGetValue(rosters.MyTeam, out var mine) || mine.Count == 0)
            return Error(collectedAt, source, league, "league-rosters.json boş veya MyTeam kadrosu bulunamadı.");

        var byName = new Dictionary<string, Player>();
        foreach (var p in allPlayers) byName.TryAdd(NameNormalizer.Normalize(p.Name), p);

        var warnings = new List<string>();
        var rostered = new HashSet<string>();
        var roster = new List<Player>();

        foreach (var (team, names) in rosters.Teams)
        foreach (var name in names)
        {
            var key = NameNormalizer.Normalize(name);
            rostered.Add(key);
            if (team != rosters.MyTeam) continue;

            if (byName.TryGetValue(key, out var player)) roster.Add(player);
            else warnings.Add($"Kadro oyuncusu ESPN verisinde bulunamadı: {name}");
        }

        foreach (var name in rosters.Teams.Where(t => t.Key != rosters.MyTeam).SelectMany(t => t.Value))
            if (!byName.ContainsKey(NameNormalizer.Normalize(name)))
                warnings.Add($"Rakip kadro oyuncusu ESPN verisinde bulunamadı: {name}");

        if (roster.Count == 0)
            return Error(collectedAt, source, league, "Kadrodaki hiçbir oyuncu istatistik kaynağında eşleşmedi.");

        var blocked = unavailable.Select(NameNormalizer.Normalize).ToHashSet();
        var freeAgents = allPlayers
            .Where(p =>
            {
                var key = NameNormalizer.Normalize(p.Name);
                return !rostered.Contains(key) && !blocked.Contains(key);
            })
            .ToList();

        return new LeagueSnapshot(collectedAt, source, "ok", league, roster, freeAgents, [], warnings);
    }

    private static LeagueSnapshot Error(DateTimeOffset at, string source, LeagueInfo league, string message) =>
        new(at, source, "error", league, [], [], [], [message]);
}
