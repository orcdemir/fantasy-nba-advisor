using FantasyAdvisor.Core.Models;

namespace FantasyAdvisor.Core.Analysis;

public sealed record SwapOptions(
    double MinTotalDelta = 1.5,
    int MinGames = 5,
    int TopN = 5,
    TimeSpan MaxDataAge = default,
    double DefaultWeekGames = 3.5)
{
    public TimeSpan EffectiveMaxAge => MaxDataAge == default ? TimeSpan.FromHours(20) : MaxDataAge;
}

/// <summary>Scores every (add, drop) pair by net 9-CAT z-score change and keeps only meaningful ones.</summary>
public sealed class SwapEvaluator
{
    private readonly SwapOptions _options;

    public SwapEvaluator(SwapOptions? options = null) => _options = options ?? new SwapOptions();

    public AnalysisResult Evaluate(LeagueSnapshot snapshot, DateTimeOffset now)
    {
        if (snapshot.Status != "ok")
            return Refuse(snapshot, now, $"Veri toplama başarısız (status={snapshot.Status}); öneri üretilmedi.");

        var age = now - snapshot.CollectedAt;
        if (age > _options.EffectiveMaxAge)
            return Refuse(snapshot, now, $"Veri eski ({age.TotalHours:F0} saat); öneri üretilmedi.");

        if (snapshot.Roster.Count == 0)
            return Refuse(snapshot, now, "Kadro boş; öneri üretilmedi.");

        var pool = snapshot.Roster.Concat(snapshot.FreeAgents).Concat(snapshot.Waivers).Select(p => p.Season).ToList();
        var calc = new NineCatCalculator(pool);

        var candidates = snapshot.FreeAgents
            .Where(p => p.Status != InjuryStatus.Out && p.Season.Games >= _options.MinGames)
            .ToList();

        var swaps = new List<SwapRecommendation>();
        foreach (var add in candidates)
        foreach (var drop in snapshot.Roster)
        {
            if (!SharesPosition(add, drop)) continue;

            var impacts = NineCatCalculator.Categories
                .Select(c => new CategoryImpact(c, Value(calc, add, c) - Value(calc, drop, c)))
                .ToList();
            var total = impacts.Sum(i => i.Delta);
            if (total < _options.MinTotalDelta) continue;

            swaps.Add(new SwapRecommendation(add, drop, total, impacts, Risks(add)));
        }

        var top = swaps.OrderByDescending(s => s.TotalDelta).Take(_options.TopN).ToList();
        var message = top.Count == 0 ? "Bugün anlamlı bir transfer fırsatı yok." : null;
        return new AnalysisResult(now, snapshot.CollectedAt, "ok", message, top);
    }

    /// <summary>Blend of season and recent form, scaled by games left this week.</summary>
    private double Value(NineCatCalculator calc, Player p, Category c)
    {
        var season = calc.Z(p.Season, c);
        var recent = p.Last14 is { Games: >= 3 } l14 ? calc.Z(l14, c) : season;
        var blended = 0.6 * season + 0.4 * recent;
        var games = p.GamesRemainingThisWeek ?? _options.DefaultWeekGames;
        return blended * (games / _options.DefaultWeekGames);
    }

    private static bool SharesPosition(Player a, Player b) =>
        a.Positions.Count == 0 || b.Positions.Count == 0 || a.Positions.Intersect(b.Positions).Any();

    private static IReadOnlyList<string> Risks(Player add)
    {
        var risks = new List<string>();
        if (add.Status is InjuryStatus.Questionable or InjuryStatus.Doubtful)
            risks.Add($"{add.Name} sakatlık raporunda ({add.Status}).");
        if (add.Season.Games < 10)
            risks.Add($"Küçük örneklem ({add.Season.Games} maç).");
        if (add.Season.Minutes < 18)
            risks.Add("Düşük oynama süresi.");
        return risks;
    }

    private static AnalysisResult Refuse(LeagueSnapshot s, DateTimeOffset now, string message) =>
        new(now, s.CollectedAt, "stale-or-error", message, []);
}
