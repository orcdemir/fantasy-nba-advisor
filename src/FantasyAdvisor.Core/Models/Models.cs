namespace FantasyAdvisor.Core.Models;

public enum Category { Pts, Reb, Ast, Stl, Blk, Tpm, Fg, Ft, To }

public enum InjuryStatus { Healthy, Questionable, Doubtful, Out }

/// <summary>Per-game averages plus shot volumes, so FG%/FT% can be weighted correctly.</summary>
public sealed record StatLine(
    int Games,
    double Minutes,
    double Pts,
    double Reb,
    double Ast,
    double Stl,
    double Blk,
    double Tpm,
    double To,
    double Fgm,
    double Fga,
    double Ftm,
    double Fta);

public sealed record Player(
    string PlayerId,
    string Name,
    string Team,
    IReadOnlyList<string> Positions,
    InjuryStatus Status,
    StatLine Season,
    StatLine? Last14,
    StatLine? Last7,
    int? GamesRemainingThisWeek);

public sealed record LeagueInfo(int Teams, string Scoring);

public sealed record LeagueSnapshot(
    DateTimeOffset CollectedAt,
    string Source,
    string Status,
    LeagueInfo League,
    IReadOnlyList<Player> Roster,
    IReadOnlyList<Player> FreeAgents,
    IReadOnlyList<Player> Waivers,
    IReadOnlyList<string>? Warnings = null);

public sealed record CategoryImpact(Category Category, double Delta);

public sealed record SwapRecommendation(
    Player Add,
    Player Drop,
    double TotalDelta,
    IReadOnlyList<CategoryImpact> Impacts,
    IReadOnlyList<string> Risks);

public sealed record AnalysisResult(
    DateTimeOffset AnalyzedAt,
    DateTimeOffset DataCollectedAt,
    string Status,
    string? Message,
    IReadOnlyList<SwapRecommendation> Swaps);
