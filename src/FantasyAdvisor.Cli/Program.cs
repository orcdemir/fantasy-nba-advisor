using System.Text.Json;
using FantasyAdvisor.Core.Analysis;
using FantasyAdvisor.Core.Models;
using FantasyAdvisor.Core.Sources;
using FantasyAdvisor.Sources;

var command = args.FirstOrDefault();
var dataDir = args.Length > 1 ? args[1] : "data";

switch (command)
{
    case "collect":
        return await CollectAsync(dataDir);
    case "analyze":
        return Analyze(dataDir);
    default:
        Console.Error.WriteLine("Kullanım: FantasyAdvisor.Cli <collect|analyze> [dataDir]");
        return 2;
}

async Task<int> CollectAsync(string dir)
{
    var snapshotPath = Path.Combine(dir, "snapshots", "latest.json");
    var now = DateTimeOffset.UtcNow;

    LeagueSnapshot snapshot;
    try
    {
        var rosters = ReadJson<LeagueRosterFile>(Path.Combine(dir, "league-rosters.json"))
                      ?? new LeagueRosterFile("", []);
        var unavailable = ReadJson<List<string>>(Path.Combine(dir, "unavailable.json")) ?? [];

        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (FantasyAdvisor personal use)");
        var stats = new EspnStatsSource(http);

        var players = await stats.GetPlayersAsync(CancellationToken.None);
        snapshot = SnapshotBuilder.Build(players, rosters, unavailable, now, stats.Name);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException or JsonException or KeyNotFoundException)
    {
        // Never keep serving an old snapshot as if it were fresh: record the failure instead.
        snapshot = new LeagueSnapshot(now, "espn", "error", new LeagueInfo(8, "H2H-9CAT"), [], [], [],
            [$"Veri toplama hatası: {ex.Message}"]);
    }

    Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
    File.WriteAllText(snapshotPath, JsonSerializer.Serialize(snapshot, Json.Options));

    Console.WriteLine($"{snapshot.Status}: kadro {snapshot.Roster.Count}, müsait {snapshot.FreeAgents.Count}");
    foreach (var w in snapshot.Warnings ?? []) Console.WriteLine($"UYARI: {w}");
    return snapshot.Status == "ok" ? 0 : 1;
}

int Analyze(string dir)
{
    var snapshotPath = Path.Combine(dir, "snapshots", "latest.json");
    if (!File.Exists(snapshotPath))
    {
        Console.Error.WriteLine($"Snapshot bulunamadı: {snapshotPath}");
        return 1;
    }

    var snapshot = JsonSerializer.Deserialize<LeagueSnapshot>(File.ReadAllText(snapshotPath), Json.Options)!;
    var result = new SwapEvaluator().Evaluate(snapshot, DateTimeOffset.UtcNow);

    var outPath = Path.Combine(dir, "analysis", "latest.json");
    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    File.WriteAllText(outPath, JsonSerializer.Serialize(result, Json.Options));
    Console.WriteLine($"{result.Status}: {result.Swaps.Count} öneri. {result.Message}");
    return 0;
}

static T? ReadJson<T>(string path) =>
    File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json.Options) : default;
