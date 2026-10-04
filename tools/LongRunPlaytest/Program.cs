using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using ReactorSim.Core;
using ReactorSim.Game;

// Offline analysis of authoritative Game sessions. --endless uses the normal
// browser factory; bounded historical comparisons override only the horizon
// and, when explicitly requested, add debug stock. Physics stays Game-owned.
string Option(string key, string fallback) => args.FirstOrDefault(a => a.StartsWith("--" + key + "=", StringComparison.Ordinal))?.Split('=', 2)[1] ?? fallback;
double Number(string key, double fallback) => double.Parse(Option(key, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);
double days = Number("days", 101), power = Number("power", 1), threshold = Number("threshold", .3);
ulong seed = ulong.Parse(Option("seed", "1001"), CultureInfo.InvariantCulture);
string policy = Option("policy", "reserve"), output = Option("output", "tmp/long-run-playtest");
bool pair = Option("pair", "false") == "true", endless = Option("endless", "false") == "true";
uint fuel = uint.Parse(Option("fuel", "128"), CultureInfo.InvariantCulture);
if (fuel < 128) throw new ArgumentException("Fuel must be at least the shipped 128 bundles.");
if (!(double.IsFinite(days) && days > 0) || !(power >= .8 && power <= 1.2) || !(threshold > .1 && threshold < .9) ||
    !new[] { "none", "oldest", "reserve", "daily" }.Contains(policy)) throw new ArgumentException("Invalid playtest options.");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
using var telemetry = new StreamWriter(output + ".jsonl");
var timer = Stopwatch.StartNew();
GameSession Create(string speed)
{
    GameSession session;
    if (endless) session = PracticeGameSessionFactory.CreateBrowserPlaytest(seed);
    else if (days == 30) session = PracticeGameSessionFactory.CreateBoundedBrowserPlaytest(seed);
    else
    {
        var factory = typeof(PracticeGameSessionFactory).GetMethod("CreateSession", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Practice factory changed; audit harness before reuse.");
        double baseRate = PracticeGameSessionFactory.BrowserBaseSimulationSecondsPerWallSecond;
        double maximumTick = baseRate * 60 * PracticeGameSessionFactory.WallControlTickMilliseconds / 1000;
        session = (GameSession)factory.Invoke(null, new object[] { baseRate, baseRate * 10, baseRate * 60, maximumTick, days * 86400, PracticeGameSessionFactory.RealTimePlaybackModeId, seed, false, false })!;
    }
    Require(session.SetPlaybackMode(speed));
    if (!endless && fuel > 128) Require(session.DebugGrantFreshBundles(fuel - 128));
    if (power != 1) Require(session.QueuePowerTarget(power));
    return session;
}
var fast = Create(PracticeGameSessionFactory.PlayPlaybackModeId);
var slow = pair ? Create(PracticeGameSessionFactory.RealTimePlaybackModeId) : null;
var powerAnalysis = new CampaignPowerAnalysis(fast.Snapshot, power);
var moves = new List<object>();
var errors = new List<string>();
var drift = new Dictionary<string, double>();
var extrema = new Dictionary<string, double> { ["minLzc"] = 1, ["maxLzc"] = 0, ["maxAbsTilt"] = 0, ["maxChannelKw"] = 0, ["maxBundleKw"] = 0 };
var lastMoves = new Dictionary<uint, double>();
double nextDailyMove = 0;
object Row(GameSessionSnapshot s, string operation) => new
{
    operation,
    day = s.SimulationTimeSeconds / 86400,
    s.SimulationTimeSeconds,
    s.RunStatus,
    s.GameOverReason,
    lzc = s.Rrs.AverageFillFraction,
    minZone = s.Rrs.MinimumFillFraction,
    maxZone = s.Rrs.MaximumFillFraction,
    tilt = s.AxialTiltFraction,
    maxChannelKw = s.Core.Channels.Max(c => c.PowerWatts) / 1000,
    maxBundleKw = s.Core.Channels.SelectMany(c => c.Bundles).Max(b => b.PowerWatts) / 1000,
    s.FreshBundlesAvailable,
    s.RefuellingOperationCount,
    s.ScoreTotal,
    s.Ripple.RmsDeviationFraction,
    s.Shift.ThermalEnergyMwh,
    s.Shift.ElectricalEnergyMwhEstimate,
    s.Physics.EffectiveK,
    s.Xenon.MeanI135NumberDensityM3,
    s.Xenon.MeanXe135NumberDensityM3,
    zoneFills = s.Rrs.Zones.Select(z => z.FillFraction).ToArray(),
    elapsedSeconds = timer.Elapsed.TotalSeconds
};
void Record(GameSessionSnapshot s, string operation)
{
    powerAnalysis.Observe(s, operation);
    extrema["minLzc"] = Math.Min(extrema["minLzc"], s.Rrs.AverageFillFraction);
    extrema["maxLzc"] = Math.Max(extrema["maxLzc"], s.Rrs.AverageFillFraction);
    extrema["maxAbsTilt"] = Math.Max(extrema["maxAbsTilt"], Math.Abs(s.AxialTiltFraction));
    extrema["maxChannelKw"] = Math.Max(extrema["maxChannelKw"], s.Core.Channels.Max(c => c.PowerWatts) / 1000);
    extrema["maxBundleKw"] = Math.Max(extrema["maxBundleKw"], s.Core.Channels.SelectMany(c => c.Bundles).Max(b => b.PowerWatts) / 1000);
    telemetry.WriteLine(JsonSerializer.Serialize(Row(s, operation))); telemetry.Flush();
}
void Difference(string field, double a, double b) => drift[field] = Math.Max(drift.GetValueOrDefault(field), Math.Abs(a - b));
void Compare(GameSessionSnapshot a, GameSessionSnapshot b)
{
    Difference("simulationSeconds", a.SimulationTimeSeconds, b.SimulationTimeSeconds);
    Difference("score", a.ScoreTotal, b.ScoreTotal);
    Difference("thermalMwh", a.Shift.ThermalEnergyMwh, b.Shift.ThermalEnergyMwh);
    Difference("lzcFraction", a.Rrs.AverageFillFraction, b.Rrs.AverageFillFraction);
    Difference("tiltFraction", a.AxialTiltFraction, b.AxialTiltFraction);
    Difference("effectiveK", a.Physics.EffectiveK, b.Physics.EffectiveK);
    if (a.IsGameOver != b.IsGameOver || a.GameOverReason != b.GameOverReason || a.FreshBundlesAvailable != b.FreshBundlesAvailable)
        errors.Add($"Lifecycle/inventory difference at {a.SimulationTimeSeconds} seconds.");
    for (int z = 0; z < a.Rrs.Zones.Count; z++) Difference("zoneFillFraction", a.Rrs.Zones[z].FillFraction, b.Rrs.Zones[z].FillFraction);
    for (int c = 0; c < a.Core.Channels.Count; c++)
    {
        Difference("channelPowerWatts", a.Core.Channels[c].PowerWatts, b.Core.Channels[c].PowerWatts);
        Difference("channelXeRelative", a.Core.Channels[c].Xenon.MeanXe135NumberDensityM3 / Math.Max(1, b.Core.Channels[c].Xenon.MeanXe135NumberDensityM3), 1);
        for (int p = 0; p < 12; p++)
        {
            var x = a.Core.Channels[c].Bundles[p]; var y = b.Core.Channels[c].Bundles[p];
            Difference("bundlePowerWatts", x.PowerWatts, y.PowerWatts);
            Difference("burnupMwdPerKg", x.CurrentBurnupMwDayPerKg, y.CurrentBurnupMwDayPerKg);
            if (x.BundleId != y.BundleId) throw new InvalidOperationException("Fuel identity differs between speeds.");
        }
    }
}
Record(fast.Snapshot, "initial");
Console.WriteLine($"START seed={seed}, policy={policy}, horizon={days} days, power={power}, pair={pair}, stock={fast.Snapshot.FreshBundlesAvailable}, LZC={fast.Snapshot.Rrs.AverageFillFraction:P2}");
GameChannelPresentationSnapshot Choose(GameSessionSnapshot s)
{
    var eligible = s.Core.Channels.Where(c => c.CanRefuel && (!lastMoves.TryGetValue(c.ChannelIndex, out double t) || s.SimulationTimeSeconds - t >= 30 * 86400));
    if (policy == "oldest" || policy == "daily") return eligible.OrderByDescending(c => c.AverageBurnupMwDayPerKg).ThenBy(c => c.ChannelIndex).First();
    // Rank observed, depleted high-importance channels; favor below-reference
    // power and leave bundle headroom for the fresh-fuel/xenon response. This
    // is a player heuristic, never a substitute reactor or outcome preview.
    return eligible.OrderByDescending(c =>
    {
        double target = s.Ripple.ReferenceChannelPowerWatts[(int)c.ChannelIndex];
        double deficit = Math.Max(0, 1 - c.PowerWatts / (target * power));
        double peak = c.Bundles.Max(b => b.PowerWatts) / power;
        double headroom = Math.Clamp((935000 - peak) / 250000, .05, 1);
        return c.AverageBurnupMwDayPerKg * target * (1 + 3 * deficit) * headroom;
    }).ThenBy(c => c.ChannelIndex).First();
}
while (!fast.Snapshot.IsGameOver && fast.Snapshot.SimulationTimeSeconds < days * 86400)
{
    var before = fast.Snapshot;
    bool order = policy != "none" && (before.Shift.UnlimitedFreshFuel || before.FreshBundlesAvailable >= 8) &&
        (policy == "daily" ? before.SimulationTimeSeconds >= nextDailyMove : before.Rrs.AverageFillFraction < threshold);
    if (order)
    {
        var chosen = Choose(before);
        string direction = chosen.FlowDirection == FlowDirection.EndAtoEndB ? "toward-end-b" : "toward-end-a";
        var result = fast.RefuelChannel(chosen.ChannelIndex, direction, 8, "NAT-U-SYNTHETIC"); Require(result);
        if (slow != null) { Require(slow.RefuelChannel(chosen.ChannelIndex, direction, 8, "NAT-U-SYNTHETIC")); Compare(result.Snapshot, slow.Snapshot); }
        lastMoves[chosen.ChannelIndex] = before.SimulationTimeSeconds; nextDailyMove += 43200;
        moves.Add(new
        {
            day = before.SimulationTimeSeconds / 86400,
            channel = chosen.ChannelIndex,
            direction,
            chosen.AverageBurnupMwDayPerKg,
            beforePowerKw = chosen.PowerWatts / 1000,
            beforeLzc = before.Rrs.AverageFillFraction,
            afterLzc = result.Snapshot.Rrs.AverageFillFraction,
            afterPowerKw = result.Snapshot.Core.GetChannel(chosen.ChannelIndex).PowerWatts / 1000,
            peakBundleKw = result.Snapshot.Core.GetChannel(chosen.ChannelIndex).Bundles.Max(b => b.PowerWatts) / 1000
        });
        Record(result.Snapshot, "refuel");
        File.WriteAllText(output + "-moves.json", JsonSerializer.Serialize(moves, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"MOVE day={before.SimulationTimeSeconds / 86400:F3} ch={chosen.ChannelIndex} LZC={before.Rrs.AverageFillFraction:P2}->{result.Snapshot.Rrs.AverageFillFraction:P2} stock={result.Snapshot.FreshBundlesAvailable}");
        if (result.Snapshot.IsGameOver) break;
    }
    // Exactly one browser 10x control tick = 30 simulation minutes. At 1x,
    // ten normal ticks cover the same interval, preserving normal integration.
    var intervalStart = fast.Snapshot;
    var advanced = fast.AdvanceWallMilliseconds(100); Require(advanced);
    powerAnalysis.Integrate(intervalStart, advanced.Snapshot);
    if (slow != null) { Require(slow.AdvanceWallMilliseconds(1000)); Compare(advanced.Snapshot, slow.Snapshot); }
    Record(advanced.Snapshot, "advance");
    if (advanced.Snapshot.SimulationTimeSeconds % 86400 == 0 || advanced.Snapshot.IsGameOver)
        Console.WriteLine($"DAY {advanced.Snapshot.SimulationTimeSeconds / 86400:F3} LZC={advanced.Snapshot.Rrs.AverageFillFraction:P2} tilt={advanced.Snapshot.AxialTiltFraction:P2} channel={extrema["maxChannelKw"]:F1}kW bundle={extrema["maxBundleKw"]:F1}kW stock={advanced.Snapshot.FreshBundlesAvailable} status={advanced.Snapshot.RunStatus} elapsed={timer.Elapsed.TotalSeconds:F1}s");
    if (slow?.Snapshot.IsGameOver == true && !advanced.Snapshot.IsGameOver) break;
}
var final = fast.Snapshot;
File.WriteAllText(output + ".json", JsonSerializer.Serialize(new
{
    seed,
    policy,
    days,
    power,
    threshold,
    pair,
    endless,
    horizonOverridden = !endless && days != 30,
    inventoryOverridden = !endless && fuel != 128,
    physicsOverridden = false,
    initialFuel = endless ? (uint?)null : fuel,
    final.Shift.IsEndless,
    final.Shift.UnlimitedFreshFuel,
    final.Shift.FuelConsumed,
    final.Provenance,
    powerAnalysis = powerAnalysis.Report(),
    final = Row(final, "final"),
    slowFinal = slow == null ? null : Row(slow.Snapshot, "final"),
    extrema,
    maximumSpeedDifferences = drift,
    speedLifecycleDifferences = errors.Distinct().ToArray(),
    moves,
    wallSeconds = timer.Elapsed.TotalSeconds
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"END day={final.SimulationTimeSeconds / 86400:F6}: {final.GameOverReason}; report={output}.json");
if (pair && (errors.Count > 0 || drift["simulationSeconds"] != 0 || drift["channelPowerWatts"] > .01 ||
    drift["bundlePowerWatts"] > .01 || drift["lzcFraction"] > 1e-8 || drift["burnupMwdPerKg"] > 1e-8 ||
    drift["score"] > 1e-6 || drift["thermalMwh"] > 1e-6))
    throw new InvalidOperationException("Speed comparison diverged; inspect the saved report.");
static void Require(GameSessionCommandResult result)
{
    if (!result.Accepted) throw new InvalidOperationException(result.DiagnosticCode + ": " + result.DiagnosticMessage);
}
