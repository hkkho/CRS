using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ReactorSim.Browser;

/// <summary>Native campaign through the same versioned daily bridge as the browser.
/// Only the player's channel choice is automated; simulation rules remain shared.</summary>
internal static class DailyTurnsCampaign
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    internal static void Run(string[] args)
    {
        string Option(string key, string fallback) => args.FirstOrDefault(a => a.StartsWith("--" + key + "=", StringComparison.Ordinal))?.Split('=', 2)[1] ?? fallback;
        int days = int.Parse(Option("days", "100"), CultureInfo.InvariantCulture);
        int count = int.Parse(Option("channels-per-day", "2"), CultureInfo.InvariantCulture);
        uint seed = uint.Parse(Option("seed", "1001"), CultureInfo.InvariantCulture);
        string policy = Option("policy", "oldest");
        string output = Path.GetFullPath(Option("output", "tmp/daily-turns-100-days"));
        if (days < 1 || days > 10000 || count < 0 || count > 380 || (policy != "oldest" && policy != "oldest-lzc") ||
            Option("power", "1") != "1" || Option("pair", "false") != "false" || Option("fuel", "128") != "128")
            throw new ArgumentException("Daily campaigns use normal endless browser defaults, oldest or oldest-lzc selection, and no physics/stock/power overrides.");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        if (File.Exists(output + ".jsonl") || File.Exists(output + ".json"))
            throw new InvalidOperationException("Campaign output already exists; choose a new output prefix to retain earlier results.");
        using var telemetry = new StreamWriter(output + ".jsonl");
        using var commands = new StreamWriter(output + "-commands.jsonl");
        var timer = Stopwatch.StartNew();
        var startedUtc = DateTimeOffset.UtcNow;
        var assemblyHashes = new[] { typeof(PlaytestRuntime).Assembly, typeof(ReactorSim.Game.GameSession).Assembly, typeof(ReactorSim.Core.SyntheticGameCoreStateV1).Assembly }
            .ToDictionary(a => a.GetName().Name!, a => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(a.Location))).ToLowerInvariant());
        var runtime = new PlaytestRuntime();
        var rows = new List<object>();
        string initialization = JsonSerializer.Serialize(new { protocol = "candu-playtest-v2", mode = "play", seed, pacingMode = "daily-turn" });
        var initialized = Parse(runtime.Initialize(initialization));
        Require(initialized.GetProperty("accepted").GetBoolean(), "Daily initialization rejected.");
        var snapshot = initialized.GetProperty("snapshot").Clone();
        string stateDigest = initialized.GetProperty("stateDigest").GetString()!;
        string replayDigest = initialized.GetProperty("replayDigest").GetString()!;
        WriteCommand("initialize", initialization, true, stateDigest, replayDigest);
        Validate(snapshot);
        var initial = Row(snapshot, Array.Empty<uint>(), 0);
        Record(initial);
        string status = "running", reason = "";
        int attemptedDay = 0;
        Save();
        Console.WriteLine($"START daily-turn seed={seed} requestedDays={days} policy={policy} channelsPerDay={count} pack={snapshot.GetProperty("dataPackId").GetString()} output={output}");
        try
        {
            for (int day = 1; day <= days && !Terminal(snapshot); day++)
            {
                attemptedDay = day;
                double start = Number(snapshot, "simulationTimeSeconds"), score = Number(snapshot, "scoreTotal");
                // Automated player decision only: use the published boundary
                // reading to add one channel below 45% LZC, without changing physics.
                int todayCount = policy == "oldest-lzc" && Number(snapshot.GetProperty("rrs"), "averageFillFraction") < .45
                    ? Math.Min(380, count + 1) : count;
                uint[] selected = snapshot.GetProperty("core").GetProperty("channels").EnumerateArray()
                    .Where(c => c.GetProperty("canRefuel").GetBoolean())
                    .OrderByDescending(c => Number(c, "averageBurnupMwdPerKg"))
                    .ThenBy(c => c.GetProperty("channelIndex").GetUInt32()).Take(todayCount)
                    .Select(c => c.GetProperty("channelIndex").GetUInt32()).OrderBy(c => c).ToArray();
                Require(selected.Length == todayCount, "Not enough eligible channels for the chosen policy.");
                string command = JsonSerializer.Serialize(new
                {
                    protocol = "candu-playtest-v2",
                    responseMode = "compact",
                    baseSequence = snapshot.GetProperty("sequence").GetUInt64(),
                    type = "commit-day",
                    expectedCompletedDays = snapshot.GetProperty("completedDays").GetUInt32(),
                    channelIndices = selected
                });
                var dayTimer = Stopwatch.StartNew();
                var progress = Parse(runtime.BeginDailyDispatchJson(command));
                double lastProgress = -1;
                int lastBucket = -1;
                while (!progress.GetProperty("completed").GetBoolean())
                {
                    double advanced = Number(progress, "simulationSecondsAdvanced");
                    Require(advanced > lastProgress && advanced <= 86400, "Daily calculation stalled or exceeded one day.");
                    lastProgress = advanced;
                    int bucket = (int)(advanced / 14400);
                    if (bucket != lastBucket)
                    {
                        Console.WriteLine($"CALCULATING day={day}/{days} singleStepSeconds=86400 elapsed={timer.Elapsed.TotalSeconds:F1}s");
                        lastBucket = bucket;
                    }
                    progress = Parse(runtime.ContinueDailyDispatchJson());
                }
                var response = Parse(progress.GetProperty("responseJson").GetString()!);
                bool accepted = response.GetProperty("accepted").GetBoolean();
                stateDigest = response.GetProperty("stateDigest").GetString()!;
                replayDigest = response.GetProperty("replayDigest").GetString()!;
                WriteCommand("dispatch", command, accepted, stateDigest, replayDigest);
                snapshot = Parse(runtime.GetSnapshotJson());
                Validate(snapshot);
                if (!accepted)
                {
                    status = "command-rejected";
                    reason = response.GetProperty("message").GetString()!;
                    Require(Number(snapshot, "simulationTimeSeconds") == start && Number(snapshot, "scoreTotal") == score,
                        "Rejected day changed committed time or score.");
                    Record(Row(snapshot, selected, dayTimer.Elapsed.TotalSeconds)); Save(); break;
                }
                double elapsed = Number(snapshot, "simulationTimeSeconds") - start;
                Require(elapsed >= 0 && elapsed <= 86400, "Invalid accepted day duration.");
                Require(Terminal(snapshot) || elapsed == 86400, "A live day must advance exactly 86400 seconds.");
                Require(Number(snapshot, "scoreTotal") - score <= 24 + 1e-8, "Daily operating score exceeded 24 points.");
                var result = snapshot.GetProperty("lastDayResult");
                Require(result.GetProperty("requestedChannels").EnumerateArray().Select(c => c.GetUInt32()).SequenceEqual(selected), "Day report changed the chosen channels.");
                Require(result.GetProperty("fuelUsed").GetInt32() == result.GetProperty("movements").GetArrayLength() * 8, "Daily fuel accounting differs from movements.");
                Record(Row(snapshot, selected, dayTimer.Elapsed.TotalSeconds));
                if (Terminal(snapshot)) { status = "ended-early"; reason = snapshot.GetProperty("runEndReason").GetString()!; }
                Save();
                Console.WriteLine($"DAY {day}/{days} elapsedDays={Number(snapshot, "simulationTimeSeconds") / 86400:F3} LZC={Number(snapshot.GetProperty("rrs"), "averageFillFraction"):P2} score={Number(snapshot, "scoreTotal"):F3} fuel={snapshot.GetProperty("shift").GetProperty("fuelConsumed")} status={snapshot.GetProperty("runStatus").GetString()} dayWallSeconds={dayTimer.Elapsed.TotalSeconds:F1}");
            }
            if (Number(snapshot, "simulationTimeSeconds") == days * 86400.0 && !Terminal(snapshot)) status = "completed";
            else if (status == "running") { status = "ended-early"; reason = snapshot.GetProperty("runEndReason").GetString()!; }
        }
        catch (Exception error)
        {
            status = "harness-or-runtime-error"; reason = error.ToString();
        }
        Save();
        Console.WriteLine($"END status={status} completedDays={Number(snapshot, "simulationTimeSeconds") / 86400:F6} reason={reason} report={output}.json");
        Environment.ExitCode = status == "completed" ? 0 : 1;

        void Record(object row) { rows.Add(row); telemetry.WriteLine(JsonSerializer.Serialize(row)); telemetry.Flush(); }
        void WriteCommand(string operation, string requestJson, bool accepted, string digest, string replay)
        {
            commands.WriteLine(JsonSerializer.Serialize(new { operation, requestJson, accepted, stateDigest = digest, replayDigest = replay })); commands.Flush();
        }
        object Row(JsonElement s, uint[] selected, double wallSeconds)
        {
            var channels = s.GetProperty("core").GetProperty("channels").EnumerateArray().ToArray();
            var shift = s.GetProperty("shift");
            return new
            {
                simulationDays = Number(s, "simulationTimeSeconds") / 86400,
                completedDays = s.GetProperty("completedDays").GetUInt32(),
                runStatus = s.GetProperty("runStatus").GetString(),
                endReason = s.GetProperty("runEndReason").GetString(),
                selected,
                score = Number(s, "scoreTotal"),
                fuelConsumed = shift.GetProperty("fuelConsumed").GetUInt32(),
                operations = s.GetProperty("refuellingOperationCount").GetUInt32(),
                thermalEnergyMwh = Number(shift, "thermalEnergyMwh"),
                electricalEnergyMwh = Number(shift, "electricalEnergyMwhEstimate"),
                averageLzcFraction = Number(s.GetProperty("rrs"), "averageFillFraction"),
                axialTiltFraction = Number(s, "axialTiltFraction"),
                maximumChannelKw = channels.Max(c => Number(c, "powerWatts")) / 1000,
                maximumBundleKw = channels.SelectMany(c => c.GetProperty("bundles").EnumerateArray()).Max(b => Number(b, "powerWatts")) / 1000,
                zoneFills = s.GetProperty("rrs").GetProperty("zones").EnumerateArray().Select(z => Number(z, "fillFraction")).ToArray(),
                dailyResult = s.TryGetProperty("lastDayResult", out var daily) ? (JsonElement?)daily.Clone() : null,
                stateDigest,
                replayDigest,
                dayWallSeconds = wallSeconds,
                totalWallSeconds = timer.Elapsed.TotalSeconds
            };
        }
        void Save()
        {
            string report = JsonSerializer.Serialize(new
            {
                format = "daily-turns-campaign-v1",
                execution = "native .NET versioned Browser bridge; shared Core/Game physics; not a 100-day browser UI/performance test",
                startedUtc,
                assemblyHashes,
                seed,
                requestedDays = days,
                channelsPerDay = count,
                policy,
                adaptiveThirdChannelBelowLzcFraction = policy == "oldest-lzc" ? (double?).45 : null,
                pacingMode = "daily-turn",
                dailyIntegrationId = snapshot.GetProperty("physics").GetProperty("cadenceIdentity").GetString(),
                status,
                reason,
                attemptedDay,
                completed = status == "completed",
                completedDays = snapshot.GetProperty("completedDays").GetUInt32(),
                elapsedSimulationDays = Number(snapshot, "simulationTimeSeconds") / 86400,
                pack = snapshot.GetProperty("dataPackId").GetString(),
                scorePolicy = snapshot.GetProperty("scorePolicyId").GetString(),
                physicsOverridden = false,
                scoringOverridden = false,
                inventoryOverridden = false,
                initial,
                final = Row(snapshot, Array.Empty<uint>(), 0),
                dailyObservations = rows,
                wallSeconds = timer.Elapsed.TotalSeconds
            }, Pretty);
            File.WriteAllText(output + ".json.tmp", report); File.Move(output + ".json.tmp", output + ".json", true);
        }
    }

    private static JsonElement Parse(string json) { using var document = JsonDocument.Parse(json); return document.RootElement.Clone(); }
    private static double Number(JsonElement root, string field) => root.GetProperty(field).GetDouble();
    private static bool Terminal(JsonElement root) => root.GetProperty("runStatus").GetString() is "ended" or "completed";
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    private static void Validate(JsonElement s)
    {
        Require(s.GetProperty("pacingMode").GetString() == "daily-turn" && s.GetProperty("isPaused").GetBoolean(), "Daily pacing did not remain frozen.");
        var shift = s.GetProperty("shift");
        Require(shift.GetProperty("isEndless").GetBoolean() && shift.GetProperty("unlimitedFreshFuel").GetBoolean() && !s.GetProperty("provenance").GetProperty("isModified").GetBoolean(), "Normal browser configuration changed.");
        Require(shift.GetProperty("fuelConsumed").GetUInt32() == s.GetProperty("refuellingOperationCount").GetUInt32() * 8, "Cumulative fuel accounting failed.");
        Require(Number(s, "wallElapsedSeconds") == 0 && Number(s, "targetPowerFraction") == 1, "A real-time advance or power override occurred.");
        double seconds = Number(s, "simulationTimeSeconds");
        Require(Math.Abs(Number(shift, "thermalEnergyMwh") - 2064 * seconds / 3600) <= Math.Max(1e-5, 2064 * seconds / 3600 * 1e-9), "Thermal energy accounting failed.");
        Require(Math.Abs(Number(shift, "electricalEnergyMwhEstimate") - 650 * seconds / 3600) <= Math.Max(1e-5, 650 * seconds / 3600 * 1e-9), "Electrical energy accounting failed.");
        foreach (var c in s.GetProperty("core").GetProperty("channels").EnumerateArray())
        {
            Require(double.IsFinite(Number(c, "powerWatts")) && double.IsFinite(Number(c, "averageBurnupMwdPerKg")), "Nonfinite channel measurement.");
            foreach (var b in c.GetProperty("bundles").EnumerateArray()) Require(double.IsFinite(Number(b, "powerWatts")) && double.IsFinite(Number(b, "currentBurnupMwdPerKg")), "Nonfinite bundle measurement.");
        }
    }
}
