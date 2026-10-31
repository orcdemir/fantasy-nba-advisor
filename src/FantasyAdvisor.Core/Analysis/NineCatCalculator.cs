using FantasyAdvisor.Core.Models;

namespace FantasyAdvisor.Core.Analysis;

/// <summary>
/// Turns per-game stat lines into per-category z-scores relative to a player pool.
/// FG%/FT% use volume-weighted impact: (made - poolPct * attempts), so high-volume shooters count more.
/// TO is sign-flipped so that higher z is always better.
/// </summary>
public sealed class NineCatCalculator
{
    public static readonly IReadOnlyList<Category> Categories = Enum.GetValues<Category>();

    private readonly double _poolFgPct;
    private readonly double _poolFtPct;
    private readonly Dictionary<Category, (double Mean, double Std)> _dist;

    public NineCatCalculator(IEnumerable<StatLine> pool)
    {
        var lines = pool.Where(s => s.Games > 0).ToList();
        if (lines.Count < 2)
            throw new ArgumentException("Need at least two players in the pool to compute z-scores.", nameof(pool));

        _poolFgPct = Ratio(lines.Sum(l => l.Fgm), lines.Sum(l => l.Fga));
        _poolFtPct = Ratio(lines.Sum(l => l.Ftm), lines.Sum(l => l.Fta));

        _dist = Categories.ToDictionary(c => c, c =>
        {
            var values = lines.Select(l => Raw(l, c)).ToList();
            var mean = values.Average();
            var variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
            return (mean, Std: Math.Max(Math.Sqrt(variance), 1e-9));
        });
    }

    public double Z(StatLine line, Category category)
    {
        var (mean, std) = _dist[category];
        var z = (Raw(line, category) - mean) / std;
        return category == Category.To ? -z : z;
    }

    public IReadOnlyDictionary<Category, double> ZScores(StatLine line) =>
        Categories.ToDictionary(c => c, c => Z(line, c));

    private double Raw(StatLine l, Category c) => c switch
    {
        Category.Pts => l.Pts,
        Category.Reb => l.Reb,
        Category.Ast => l.Ast,
        Category.Stl => l.Stl,
        Category.Blk => l.Blk,
        Category.Tpm => l.Tpm,
        Category.To => l.To,
        Category.Fg => l.Fgm - _poolFgPct * l.Fga,
        Category.Ft => l.Ftm - _poolFtPct * l.Fta,
        _ => throw new ArgumentOutOfRangeException(nameof(c))
    };

    /// <summary>Team FG% / FT% from summed makes and attempts (never an average of percentages).</summary>
    public static double TeamPct(IEnumerable<StatLine> lines, Category category)
    {
        var list = lines.ToList();
        return category switch
        {
            Category.Fg => Ratio(list.Sum(l => l.Fgm), list.Sum(l => l.Fga)),
            Category.Ft => Ratio(list.Sum(l => l.Ftm), list.Sum(l => l.Fta)),
            _ => throw new ArgumentException("Only Fg and Ft are percentage categories.", nameof(category))
        };
    }

    private static double Ratio(double made, double attempts) => attempts <= 0 ? 0 : made / attempts;
}
