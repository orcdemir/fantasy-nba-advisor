using System.Text.Json;
using FantasyAdvisor.Core.Models;
using FantasyAdvisor.Core.Sources;

namespace FantasyAdvisor.Sources;

/// <summary>
/// Unofficial, undocumented ESPN JSON endpoints. They may change without notice, so parsing is isolated
/// in <see cref="Parse"/> (fixture-tested) and any schema surprise fails loudly instead of yielding bad stats.
/// </summary>
public sealed class EspnStatsSource(HttpClient http) : IStatsSource
{
    private const string StatsUrl =
        "https://site.web.api.espn.com/apis/common/v3/sports/basketball/nba/statistics/byathlete" +
        "?region=us&lang=en&contentorigin=espn&limit=500&sort=offensive.avgPoints%3Adesc";

    private const string InjuriesUrl = "https://site.api.espn.com/apis/site/v2/sports/basketball/nba/injuries";

    /// <summary>Labels where ESPN puts the per-game value after the season total (observed: general.REB).</summary>
    private static readonly HashSet<string> PerGameIsLast = ["REB"];

    public string Name => "espn";

    public async Task<IReadOnlyList<Player>> GetPlayersAsync(CancellationToken ct)
    {
        var stats = await http.GetStringAsync(StatsUrl, ct);
        var injuries = await http.GetStringAsync(InjuriesUrl, ct);
        return Parse(stats, injuries);
    }

    public static IReadOnlyList<Player> Parse(string statsJson, string injuriesJson)
    {
        using var stats = JsonDocument.Parse(statsJson);
        using var inj = JsonDocument.Parse(injuriesJson);

        var statuses = ParseInjuries(inj.RootElement);
        var labels = ReadLabels(stats.RootElement);
        var players = new List<Player>();

        foreach (var entry in stats.RootElement.GetProperty("athletes").EnumerateArray())
        {
            var athlete = entry.GetProperty("athlete");
            var name = athlete.GetProperty("displayName").GetString()!;

            // Labels repeat: one occurrence is the per-game average, the other the season total.
            var occurrences = new Dictionary<string, List<double>>();
            foreach (var cat in entry.GetProperty("categories").EnumerateArray())
            {
                var catName = cat.GetProperty("name").GetString()!;
                if (!labels.TryGetValue(catName, out var catLabels)) continue;
                var vals = cat.GetProperty("values").EnumerateArray().Select(v => v.GetDouble()).ToList();
                for (var i = 0; i < Math.Min(vals.Count, catLabels.Count); i++)
                {
                    if (!occurrences.TryGetValue(catLabels[i], out var list))
                        occurrences[catLabels[i]] = list = [];
                    list.Add(vals[i]);
                }
            }

            double Games() => occurrences.TryGetValue("GP", out var gp) ? gp[0] : 0;

            double V(string label)
            {
                if (!occurrences.TryGetValue(label, out var list))
                    throw new FormatException($"ESPN yanıtında '{label}' alanı yok ({name}); şema değişmiş olabilir.");

                // ESPN lists per-game first for every stat except REB, where it comes last.
                var perGame = PerGameIsLast.Contains(label) ? list[^1] : list[0];

                // Guard against ESPN reordering: per-game x games must reproduce the season total.
                if (list.Count == 2 && Games() > 0)
                {
                    var total = PerGameIsLast.Contains(label) ? list[0] : list[1];
                    if (Math.Abs(perGame * Games() - total) > 0.5 + 0.02 * Math.Abs(total))
                        throw new FormatException(
                            $"'{label}' maç başı değeri sezon toplamıyla tutarsız ({name}: {perGame} x {Games()} != {total}); şema değişmiş olabilir.");
                }

                return perGame;
            }

            var positions = athlete.TryGetProperty("position", out var pos) && pos.TryGetProperty("abbreviation", out var abbr)
                ? new[] { abbr.GetString()! }
                : [];
            var team = athlete.TryGetProperty("teamShortName", out var ts) ? ts.GetString() ?? "" : "";

            var line = new StatLine(
                (int)V("GP"), V("MIN"), V("PTS"), V("REB"), V("AST"), V("STL"), V("BLK"), V("3PM"), V("TO"),
                V("FGM"), V("FGA"), V("FTM"), V("FTA"));

            statuses.TryGetValue(Normalize(name), out var status);
            players.Add(new Player(athlete.GetProperty("id").GetString()!, name, team, positions, status, line, null, null, null));
        }

        if (players.Count == 0) throw new FormatException("ESPN oyuncu listesi boş döndü.");
        return players;
    }

    private static Dictionary<string, List<string>> ReadLabels(JsonElement root)
    {
        var result = new Dictionary<string, List<string>>();
        foreach (var cat in root.GetProperty("categories").EnumerateArray())
            result[cat.GetProperty("name").GetString()!] =
                cat.GetProperty("labels").EnumerateArray().Select(l => l.GetString()!).ToList();
        return result;
    }

    private static Dictionary<string, InjuryStatus> ParseInjuries(JsonElement root)
    {
        var map = new Dictionary<string, InjuryStatus>();
        foreach (var team in root.GetProperty("injuries").EnumerateArray())
        foreach (var item in team.GetProperty("injuries").EnumerateArray())
        {
            var name = item.GetProperty("athlete").GetProperty("displayName").GetString();
            if (name is null) continue;
            map[Normalize(name)] = item.GetProperty("status").GetString() switch
            {
                "Out" => InjuryStatus.Out,
                "Day-To-Day" => InjuryStatus.Questionable,
                "Doubtful" => InjuryStatus.Doubtful,
                _ => InjuryStatus.Questionable
            };
        }

        return map;
    }

    private static string Normalize(string name) => NameNormalizer.Normalize(name);
}
