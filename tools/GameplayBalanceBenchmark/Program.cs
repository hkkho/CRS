using System.Text.Json;
using ReactorSim.Core;
using ReactorSim.Game;

if (args.Length == 3 && args[0] == "--compare")
{
    ComparePhysicalResults(args[1], args[2]);
    return;
}

// Offline gameplay comparisons consume the authoritative session and Core shift
// contract. No alternate reactor, scoring formula or wall-time ranking is used.
ulong[] seeds = { 1001, 1002, 1013 };
string[] policies = { "no-refuels", "highest-burnup-default-direction", "waste-reversal", "considered-discharge" };
var results = new List<object>();
foreach (ulong seed in seeds)
{
    foreach (string policy in policies)
    {
        GameSession session = PracticeGameSessionFactory.CreateBrowserPlaytest(seed, challenge: true);
        var moves = new List<object>();
        for (int interval = 0; interval < 8 && !session.Snapshot.IsGameOver; interval++)
        {
            if (interval == 0 && policy == "waste-reversal")
            {
                var choice = BestDischarge(session);
                for (int move = 0; move < 4 && !session.Snapshot.IsGameOver; move++)
                {
                    string direction = move % 2 == 0 ? choice.Direction : Opposite(choice.Direction);
                    Commit(session, choice.Channel, direction, moves);
                }
            }
            else if ((interval == 0 || interval == 4) && policy != "no-refuels" && policy != "waste-reversal")
            {
                var choice = policy == "considered-discharge" ? BestDischarge(session) : HighestBurnup(session);
                Commit(session, choice.Channel, choice.Direction, moves);
            }
            if (!session.Snapshot.IsGameOver)
            {
                Require(session.Resume());
                // Browser base rate: 6,000 wall ms gives exactly three simulated hours.
                Require(session.AdvanceWallMilliseconds(6000));
                if (!session.Snapshot.IsGameOver) Require(session.Pause());
            }
        }
        GameSessionSnapshot snapshot = session.Snapshot;
        ShiftProgress shift = snapshot.Shift;
        results.Add(new
        {
            seed,
            policy,
            scorePolicyId = snapshot.ScorePolicyId,
            survivalSeconds = snapshot.SimulationTimeSeconds,
            snapshot.RunStatus,
            reason = snapshot.GameOverReason,
            shift.Outcome,
            shift.RewardEarned,
            shift.ElectricalEnergyMwhEstimate,
            shift.ThermalEnergyMwh,
            shift.FuelConsumed,
            shift.UsefulBundlesDischarged,
            shift.OperatingPoints,
            shift.DischargeReward,
            shift.FreshFuelCost,
            snapshot.ScoreTotal,
            snapshot.RrsReserveFraction,
            moves
        });
        Console.WriteLine($"Seed {seed}, {policy}: {snapshot.ScoreTotal:F3} points, {shift.FuelConsumed} bundles, {shift.Outcome}");
    }
}
string output = args.FirstOrDefault() ?? "tmp/gameplay-balance.json";
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Report: {Path.GetFullPath(output)}");
if (args.Length > 1) ComparePhysicalResults(args[1], output);

static void ComparePhysicalResults(string baselinePath, string tunedPath)
{
    using var baseline = JsonDocument.Parse(File.ReadAllText(baselinePath));
    using var tuned = JsonDocument.Parse(File.ReadAllText(tunedPath));
    JsonElement baselineRows = baseline.RootElement.ValueKind == JsonValueKind.Array
        ? baseline.RootElement : baseline.RootElement.GetProperty("baseline");
    JsonElement tunedRows = tuned.RootElement.ValueKind == JsonValueKind.Array
        ? tuned.RootElement : tuned.RootElement.GetProperty("tuned");
    string[] unchanged = { "survivalSeconds", "RunStatus", "reason", "Outcome", "RewardEarned",
        "ElectricalEnergyMwhEstimate", "ThermalEnergyMwh", "FuelConsumed", "UsefulBundlesDischarged",
        "DischargeReward", "FreshFuelCost", "RrsReserveFraction" };
    foreach (JsonElement row in tunedRows.EnumerateArray())
    {
        JsonElement before = baselineRows.EnumerateArray().Single(candidate =>
            candidate.GetProperty("seed").GetUInt64() == row.GetProperty("seed").GetUInt64() &&
            candidate.GetProperty("policy").GetString() == row.GetProperty("policy").GetString());
        foreach (string field in unchanged)
            if (before.GetProperty(field).GetRawText() != row.GetProperty(field).GetRawText())
                throw new InvalidOperationException($"Unexpected physical/outcome change: {field}, seed {row.GetProperty("seed")}, {row.GetProperty("policy")}");
        string? oldPolicy = before.GetProperty("scorePolicyId").GetString();
        double conversion = oldPolicy == "practice-discharge-per-bundle-v1" ? 1800.0 :
            oldPolicy == row.GetProperty("scorePolicyId").GetString() ? 1.0 :
            throw new InvalidOperationException("Unsupported baseline scoring policy.");
        double expectedOperating = before.GetProperty("OperatingPoints").GetDouble() / conversion;
        if (Math.Abs(expectedOperating - row.GetProperty("OperatingPoints").GetDouble()) > 1e-9)
            throw new InvalidOperationException("Operating-score conversion differs from the authored rate.");
    }
    foreach (var group in tunedRows.EnumerateArray().GroupBy(row => row.GetProperty("seed").GetUInt64()))
    {
        double Score(string policy) => group.Single(row => row.GetProperty("policy").GetString() == policy).GetProperty("ScoreTotal").GetDouble();
        if (!(Score("considered-discharge") > Score("highest-burnup-default-direction") &&
            Score("highest-burnup-default-direction") > Score("no-refuels") &&
            Score("no-refuels") > Score("waste-reversal")))
            throw new InvalidOperationException($"Unexpected strategy incentive ranking for seed {group.Key}.");
    }
    Console.WriteLine("Comparison passed: physical metrics/outcomes unchanged; useful discharge wins on every seed.");
}

static (uint Channel, string Direction) HighestBurnup(GameSession session)
{
    GameChannelPresentationSnapshot channel = session.Snapshot.Core.Channels
        .OrderByDescending(channel => channel.AverageBurnupMwDayPerKg).ThenBy(channel => channel.ChannelIndex).First();
    return (channel.ChannelIndex, channel.FlowDirection == FlowDirection.EndBtoEndA ? "toward-end-a" : "toward-end-b");
}

static (uint Channel, string Direction) BestDischarge(GameSession session)
{
    double bestPoints = double.NegativeInfinity;
    (uint Channel, string Direction) best = default;
    double time = session.Snapshot.SimulationTimeSeconds;
    foreach (uint channel in Enumerable.Range(0, (int)SyntheticGameCoreStateV1.ChannelCount).Select(index => (uint)index))
    {
        foreach (GameRefuellingDirectionV1 direction in new[] { GameRefuellingDirectionV1.TowardEndA, GameRefuellingDirectionV1.TowardEndB })
        {
            var candidate = session.CoreState.TryRefuel(channel, direction, 8, "NAT-U-SYNTHETIC", time);
            if (!candidate.IsValid) continue;
            double points = PracticeScoring.DescribeDischarge(candidate.Value.DischargedBundles.Select(bundle =>
                bundle.CurrentBurnupJPerKgHm / 86_400_000_000.0)).NetPoints;
            if (points <= bestPoints) continue;
            bestPoints = points;
            best = (channel, direction == GameRefuellingDirectionV1.TowardEndA ? "toward-end-a" : "toward-end-b");
        }
    }
    return best;
}

static void Commit(GameSession session, uint channel, string direction, List<object> moves)
{
    var result = session.RefuelChannel(channel, direction, 8, "NAT-U-SYNTHETIC");
    Require(result);
    moves.Add(new
    {
        channel,
        direction,
        simulationTimeSeconds = result.Snapshot.SimulationTimeSeconds,
        result.Snapshot.LastRefuellingScore
    });
}

static string Opposite(string direction) => direction == "toward-end-a" ? "toward-end-b" : "toward-end-a";
static void Require(GameSessionCommandResult result)
{
    if (!result.Accepted) throw new InvalidOperationException(result.DiagnosticCode + ": " + result.DiagnosticMessage);
}
