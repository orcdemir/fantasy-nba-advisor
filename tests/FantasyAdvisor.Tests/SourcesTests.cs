using FantasyAdvisor.Core.Models;
using FantasyAdvisor.Core.Sources;
using FantasyAdvisor.Sources;

namespace FantasyAdvisor.Tests;

public class SourcesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 5, 35, 0, TimeSpan.FromHours(3));

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static IReadOnlyList<Player> EspnPlayers() =>
        EspnStatsSource.Parse(Fixture("espn-stats.json"), Fixture("espn-injuries.json"));

    [Theory]
    [InlineData("Luka Dončić", "luka doncic")]
    [InlineData("Jaren Jackson Jr.", "jaren jackson")]
    [InlineData("Gary Trent  Jr", "gary trent")]
    [InlineData("De'Aaron Fox", "de aaron fox")]
    public void NameNormalizer_StripsAccentsPunctuationAndSuffix(string input, string expected) =>
        Assert.Equal(expected, NameNormalizer.Normalize(input));

    [Fact]
    public void EspnParse_ReadsPerGameStats()
    {
        var luka = EspnPlayers().Single(p => p.Name == "Luka Doncic");

        // ESPN displays rounded values (22.8) but the JSON carries full precision (22.75), hence the tolerance.
        Assert.Equal(4, luka.Season.Games);
        Assert.Equal(35.5, luka.Season.Minutes, 0.06);
        Assert.Equal(36.0, luka.Season.Pts, 0.06);
        Assert.Equal(11.0, luka.Season.Fgm, 0.06);
        Assert.Equal(22.75, luka.Season.Fga, 0.06);
        Assert.Equal(5.3, luka.Season.Tpm, 0.06);
        Assert.Equal(8.8, luka.Season.Ftm, 0.06);
        Assert.Equal(10.0, luka.Season.Fta, 0.06);
        Assert.Equal(8.3, luka.Season.Ast, 0.06);
        Assert.Equal(5.8, luka.Season.To, 0.06);
        Assert.Equal(1.3, luka.Season.Stl, 0.06);
        Assert.Equal(0.5, luka.Season.Blk, 0.06);
        Assert.Equal(["G"], luka.Positions);
    }

    [Fact]
    public void EspnParse_AppliesInjuryStatusByNormalizedName()
    {
        var players = EspnPlayers();
        Assert.Equal(InjuryStatus.Out, players.Single(p => p.Name == "Luka Doncic").Status);
        Assert.Equal(InjuryStatus.Questionable, players.Single(p => p.Name == "Anthony Edwards").Status);
    }

    [Fact]
    public void EspnParse_FailsLoudlyOnSchemaChange()
    {
        // Drop the STL label everywhere, as if ESPN renamed or removed the field.
        var broken = Fixture("espn-stats.json").Replace("\"STL\"", "\"STX\"");
        Assert.Throws<FormatException>(() => EspnStatsSource.Parse(broken, Fixture("espn-injuries.json")));
    }

    [Fact]
    public void Build_SplitsRosterAndFreeAgents_AndHonorsUnavailable()
    {
        var players = EspnPlayers();
        var rosters = new LeagueRosterFile("Ben", new()
        {
            ["Ben"] = ["Luka Dončić"],
            ["Rakip"] = ["Anthony Edwards"]
        });

        var snap = SnapshotBuilder.Build(players, rosters, [], Now, "espn");
        Assert.Equal("ok", snap.Status);
        Assert.Equal(["Luka Doncic"], snap.Roster.Select(p => p.Name));
        Assert.DoesNotContain(snap.FreeAgents, p => p.Name is "Luka Doncic" or "Anthony Edwards");
        Assert.Equal(players.Count - 2, snap.FreeAgents.Count);

        var other = players.First(p => p.Name is not ("Luka Doncic" or "Anthony Edwards"));
        var blocked = SnapshotBuilder.Build(players, rosters, [other.Name], Now, "espn");
        Assert.DoesNotContain(blocked.FreeAgents, p => p.PlayerId == other.PlayerId);
    }

    [Fact]
    public void Build_WarnsOnUnmatchedNames()
    {
        var rosters = new LeagueRosterFile("Ben", new()
        {
            ["Ben"] = ["Luka Doncic", "Yok Oyuncu"],
            ["Rakip"] = ["Baska Yok"]
        });
        var snap = SnapshotBuilder.Build(EspnPlayers(), rosters, [], Now, "espn");

        Assert.Equal("ok", snap.Status);
        Assert.Contains(snap.Warnings!, w => w.Contains("Yok Oyuncu"));
        Assert.Contains(snap.Warnings!, w => w.Contains("Baska Yok"));
    }

    [Fact]
    public void Build_ReturnsErrorWhenRosterEmpty()
    {
        var empty = new LeagueRosterFile("Ben", new() { ["Ben"] = [], ["Rakip"] = [] });
        var snap = SnapshotBuilder.Build(EspnPlayers(), empty, [], Now, "espn");
        Assert.Equal("error", snap.Status);
        Assert.Empty(snap.FreeAgents);
    }
}
