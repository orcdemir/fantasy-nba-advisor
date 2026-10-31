using FantasyAdvisor.Core.Analysis;
using FantasyAdvisor.Core.Models;

namespace FantasyAdvisor.Tests;

public class AnalysisTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 6, 0, 0, TimeSpan.FromHours(3));

    private static StatLine Line(int games = 20, double pts = 10, double reb = 5, double ast = 3, double stl = 1,
        double blk = 0.5, double tpm = 1, double to = 2, double fgm = 4, double fga = 9, double ftm = 2, double fta = 2.5,
        double min = 28) => new(games, min, pts, reb, ast, stl, blk, tpm, to, fgm, fga, ftm, fta);

    private static Player P(string id, StatLine s, InjuryStatus st = InjuryStatus.Healthy, string pos = "PG") =>
        new(id, id, "TST", [pos], st, s, null, null, null);

    private static LeagueSnapshot Snap(IEnumerable<Player> roster, IEnumerable<Player> fa,
        DateTimeOffset? collected = null, string status = "ok") =>
        new(collected ?? Now.AddMinutes(-30), "test", status, new LeagueInfo(8, "H2H-9CAT"),
            roster.ToList(), fa.ToList(), []);

    private static List<Player> Filler() =>
        Enumerable.Range(0, 6).Select(i => P($"f{i}", Line(pts: 9 + i, reb: 4 + i % 3, ast: 2 + i % 2))).ToList();

    [Fact]
    public void TeamPct_IsVolumeWeighted_NotAverageOfPercentages()
    {
        var lines = new[] { Line(fgm: 9, fga: 10), Line(fgm: 1, fga: 10) };
        Assert.Equal(0.5, NineCatCalculator.TeamPct(lines, Category.Fg), 6);

        var skewed = new[] { Line(fgm: 10, fga: 10), Line(fgm: 0, fga: 30) };
        Assert.Equal(0.25, NineCatCalculator.TeamPct(skewed, Category.Fg), 6);
    }

    [Fact]
    public void Turnovers_AreInverted()
    {
        var calc = new NineCatCalculator([Line(to: 1), Line(to: 3), Line(to: 5)]);
        Assert.True(calc.Z(Line(to: 1), Category.To) > 0);
        Assert.True(calc.Z(Line(to: 5), Category.To) < 0);
    }

    [Fact]
    public void FgImpact_RewardsVolumeAtGoodEfficiency()
    {
        var pool = new[] { Line(fgm: 4, fga: 10), Line(fgm: 5, fga: 10), Line(fgm: 3, fga: 10) };
        var calc = new NineCatCalculator(pool);
        var efficientHighVolume = Line(fgm: 12, fga: 20);
        var efficientLowVolume = Line(fgm: 3, fga: 5);
        Assert.True(calc.Z(efficientHighVolume, Category.Fg) > calc.Z(efficientLowVolume, Category.Fg));
    }

    [Fact]
    public void Evaluate_RecommendsClearUpgrade()
    {
        var weak = P("weak", Line(pts: 5, reb: 2, ast: 1, stl: 0.3, blk: 0.1, tpm: 0.3));
        var star = P("star", Line(pts: 22, reb: 9, ast: 6, stl: 1.8, blk: 1.2, tpm: 2.5));
        var result = new SwapEvaluator().Evaluate(Snap(Filler().Append(weak), [star]), Now);

        Assert.Equal("ok", result.Status);
        var top = Assert.Single(result.Swaps.Take(1));
        Assert.Equal("star", top.Add.PlayerId);
        Assert.Equal("weak", top.Drop.PlayerId);
        Assert.True(top.TotalDelta >= 1.5);
    }

    [Fact]
    public void Evaluate_NoSwap_WhenNothingBetter()
    {
        var result = new SwapEvaluator().Evaluate(Snap(Filler(), [P("meh", Line(pts: 3, reb: 1, ast: 1))]), Now);
        Assert.Empty(result.Swaps);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public void Evaluate_SkipsInjuredOutAndSmallSample()
    {
        var weak = P("weak", Line(pts: 5, reb: 2, ast: 1));
        var out_ = P("out", Line(pts: 25, reb: 10, ast: 7), InjuryStatus.Out);
        var rookie = P("rookie", Line(games: 2, pts: 25, reb: 10, ast: 7));
        var result = new SwapEvaluator().Evaluate(Snap(Filler().Append(weak), [out_, rookie]), Now);
        Assert.Empty(result.Swaps);
    }

    [Fact]
    public void Evaluate_RespectsPositionEligibility()
    {
        var weakC = P("weakC", Line(pts: 5, reb: 2, ast: 1), pos: "C");
        var starPg = P("starPg", Line(pts: 22, reb: 9, ast: 6, stl: 1.8, blk: 1.2, tpm: 2.5), pos: "PG");
        var roster = Filler().Select(p => p with { Positions = ["C"] }).Append(weakC);
        var result = new SwapEvaluator().Evaluate(Snap(roster, [starPg]), Now);
        Assert.Empty(result.Swaps);
    }

    [Fact]
    public void Evaluate_RefusesStaleData()
    {
        var weak = P("weak", Line(pts: 5, reb: 2, ast: 1));
        var star = P("star", Line(pts: 22, reb: 9, ast: 6, stl: 1.8, blk: 1.2, tpm: 2.5));
        var snap = Snap(Filler().Append(weak), [star], collected: Now.AddHours(-30));
        var result = new SwapEvaluator().Evaluate(snap, Now);
        Assert.Equal("stale-or-error", result.Status);
        Assert.Empty(result.Swaps);
    }

    [Fact]
    public void Evaluate_RefusesErrorSnapshot()
    {
        var result = new SwapEvaluator().Evaluate(Snap(Filler(), [], status: "error"), Now);
        Assert.Equal("stale-or-error", result.Status);
        Assert.Empty(result.Swaps);
    }
}
