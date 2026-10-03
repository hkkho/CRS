using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ReactorSim.Core;
using ReactorSim.Game;

internal static class CampaignComparison
{
    private const double Horizon = 72 * 3600;
    private const double SampleInterval = 1800;
    private static readonly ulong[] Seeds = { 0, 1001, 1002, 1003, 1004, uint.MaxValue };
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    internal static void RunFifteenMinuteCampaign(string directory, string referenceDirectory)
    {
        if (RuntimeProfile.ScopesEnabled) throw new InvalidOperationException("Timing comparisons require profiling scopes compiled out.");
        directory = Path.GetFullPath(directory);
        referenceDirectory = Path.GetFullPath(referenceDirectory);
        if (string.Equals(directory, referenceDirectory, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Use a separate directory to preserve the reference campaign.");
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(referenceDirectory, "comparison.json")))!;
        using var stream = typeof(FullCoreDiffusionDataPackV1).Assembly.GetManifestResourceStream(
            "ReactorSim.Core.Data.candu6-two-group-diffusion-pack-v1.json")!;
        string packSha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (document["criticalityTolerance"]!.GetValue<double>() != PracticeLiquidZoneRrsIdentityV1.CriticalityTolerance ||
            document["shapeTolerance"]!.GetValue<double>() != PracticeLiquidZoneRrsIdentityV1.ControllerTolerance ||
            document["packSha256"]!.GetValue<string>() != packSha256)
            throw new InvalidOperationException("Reference campaign must use the same controller tolerances and physics pack.");
        Directory.CreateDirectory(directory);
        document["referenceDirectory"] = referenceDirectory;
        document["fifteenMinuteCreatedUtc"] = DateTime.UtcNow;
        document["method"] = document["method"]!.GetValue<string>() +
            " Additional two-check-core-900 cases freshly measured at 15-minute spatial intervals; earlier reference/3-minute/30-minute traces retained from referenceDirectory, with their original timings. Browser gameplay cadence unchanged.";
        var cases = document["runs"]!.AsArray();
        var referenceFuel = cases.Select(node => JsonSerializer.Deserialize<CampaignRun>(node!, JsonOptions)!)
            .Single(run => run.Policy == "reference-game-1800" && run.Seed == 1001 && run.Refuel);
        if (referenceFuel.RefuelActions.Count != 2) throw new InvalidOperationException("Expected two reference fuel operations.");
        var recoveryPath = Path.Combine(referenceDirectory, "initialization-recovery.json");
        var recoveryRuns = File.Exists(recoveryPath)
            ? JsonSerializer.Deserialize<List<CampaignRun>>(System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(recoveryPath))!["runs"]!, JsonOptions)!
            : new List<CampaignRun>();
        var model = Require(FullCoreDiffusionModelV1.TryCreateCandu6(Require(FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6())));
        _ = PracticeGameSessionFactory.CreateBrowserPlaytest(1001);
        void Save()
        {
            File.WriteAllText(Path.Combine(directory, "comparison.json"), document.ToJsonString(JsonOptions));
            File.WriteAllText(Path.Combine(directory, "initialization-recovery.json"), JsonSerializer.Serialize(new
            {
                method = "Original bounded startup recovery traces retained; fresh 15-minute recovery uses 32 bootstrap passes with unchanged criteria. Default-budget failures remain in comparison.json.",
                runs = recoveryRuns
            }, JsonOptions));
        }
        void Execute(ulong seed, IReadOnlyList<RefuelAction> actions)
        {
            var run = RunCore(model, seed, "two-check-core-900", actions);
            cases.Add(JsonSerializer.SerializeToNode(run, JsonOptions));
            Save();
            if (run.Error.StartsWith("PracticeSingleSolve.Initialization.NotSettled", StringComparison.Ordinal))
            {
                recoveryRuns.Add(RunCore(model, seed, "two-check-core-900", actions, 32));
                Save();
            }
        }
        foreach (ulong seed in Seeds) Execute(seed, Array.Empty<RefuelAction>());
        Execute(1001, referenceFuel.RefuelActions);
        Console.WriteLine($"Completed 15-minute campaigns: {directory}");
    }

    internal static void RerunCoreCase(string directory, string policy, ulong seed)
    {
        if (policy != "reference-core-1800" && policy != "two-check-core-1800" && policy != "two-check-core-900" && policy != "two-check-core-180")
            throw new ArgumentException("Expected a Core campaign policy.", nameof(policy));
        string path = Path.Combine(directory, "comparison.json");
        var document = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!;
        if (document["criticalityTolerance"]!.GetValue<double>() != PracticeLiquidZoneRrsIdentityV1.CriticalityTolerance)
            throw new InvalidOperationException("Campaign tolerance differs from the current controller.");
        var model = Require(FullCoreDiffusionModelV1.TryCreateCandu6(Require(FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6())));
        _ = PracticeGameSessionFactory.CreateBrowserPlaytest(1001);
        var cases = document["runs"]!.AsArray();
        for (int index = 0; index < cases.Count; index++)
        {
            if (cases[index]!["Policy"]!.GetValue<string>() != policy || cases[index]!["Seed"]!.GetValue<ulong>() != seed)
                continue;
            var previous = JsonSerializer.Deserialize<CampaignRun>(cases[index]!, JsonOptions)!;
            var rerun = RunCore(model, seed, policy, previous.RefuelActions);
            // Preserve the original trace before replacing this explicitly selected case.
            File.WriteAllText(Path.Combine(directory, $"before-rerun-{policy}-{seed}-{previous.Refuel}.json"),
                JsonSerializer.Serialize(previous, JsonOptions));
            cases[index] = JsonSerializer.SerializeToNode(rerun, JsonOptions);
        }
        File.WriteAllText(path, document.ToJsonString(JsonOptions));
    }

    internal static void RunRecovery(string directory)
    {
        var model = Require(FullCoreDiffusionModelV1.TryCreateCandu6(Require(FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6())));
        var runs = new List<CampaignRun>();
        foreach (string policy in new[] { "two-check-core-1800", "two-check-core-180" })
        {
            runs.Add(RunCore(model, uint.MaxValue, policy, Array.Empty<RefuelAction>(), 32));
            File.WriteAllText(Path.Combine(directory, "initialization-recovery.json"), JsonSerializer.Serialize(new
            {
                method = "Same two-check solver and controller tolerances; only bootstrap pass budget increased from 8 to 32 for seed 4294967295. Default-budget failures remain in comparison.json.",
                runs
            }, JsonOptions));
        }
    }

    internal static void ProbeInitialization(ulong seed, int maximumPasses)
    {
        var core = SyntheticGameCoreStateV1.CreateAgedPractice(seed);
        var model = Require(FullCoreDiffusionModelV1.TryCreateCandu6(Require(FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6())));
        var solver = Require(EquilibriumCoreSolverV1.TryCreate(model, core.EnumerateBundles(), PracticeGameSessionFactory.PracticeReferencePowerWatts));
        var initial = Require(PracticeLiquidZoneRrsV1.TryCreate(Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6()), solver.CurrentProjection));
        var timer = Stopwatch.StartNew();
        var initialized = PracticeLiquidZoneRrsV1.TryInitializeSingleSolve(solver, core.EnumerateBundles(), initial, maximumPasses: maximumPasses);
        timer.Stop();
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            seed,
            maximumPasses,
            initialized = initialized.IsValid,
            elapsedMs = timer.Elapsed.TotalMilliseconds,
            candidateSolves = initialized.IsValid ? (int?)initialized.Value.CandidateSolveCount : null,
            reactivity = initialized.IsValid ? (double?)initialized.Value.Projection.RelativeReactivity : null,
            maxShapeError = initialized.IsValid ? (double?)initialized.Value.ZonalShapeErrors.Max(Math.Abs) : null,
            error = initialized.IsValid ? "" : initialized.FirstDiagnostic.ToString()
        }, JsonOptions));
    }

    internal static void Run(string directory, bool resume)
    {
        if (RuntimeProfile.ScopesEnabled) throw new InvalidOperationException("Timing comparisons require profiling scopes compiled out.");
        directory = Path.GetFullPath(directory);
        Directory.CreateDirectory(directory);
        var runs = new List<CampaignRun>();
        string outputFile = Path.Combine(directory, "comparison.json");
        var pack = Require(FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        var model = Require(FullCoreDiffusionModelV1.TryCreateCandu6(pack));
        using var stream = typeof(FullCoreDiffusionDataPackV1).Assembly.GetManifestResourceStream(
            "ReactorSim.Core.Data.candu6-two-group-diffusion-pack-v1.json")!;
        string packSha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (resume && File.Exists(outputFile))
        {
            using var saved = JsonDocument.Parse(File.ReadAllText(outputFile));
            var metadata = saved.RootElement;
            if (metadata.GetProperty("criticalityTolerance").GetDouble() != PracticeLiquidZoneRrsIdentityV1.CriticalityTolerance ||
                metadata.GetProperty("shapeTolerance").GetDouble() != PracticeLiquidZoneRrsIdentityV1.ControllerTolerance ||
                metadata.GetProperty("packSha256").GetString() != packSha256)
                throw new InvalidOperationException("Cannot resume campaigns with different controller tolerances or physics packs. Use a new output directory.");
            runs.AddRange(JsonSerializer.Deserialize<List<CampaignRun>>(metadata.GetProperty("runs"), JsonOptions)!
                .Where(run => run.Error.Length == 0 && run.CompletedHours == 72 && !run.Terminal));
        }
        void Save() => File.WriteAllText(Path.Combine(directory, "comparison.json"), JsonSerializer.Serialize(new
        {
            format = "two-check-72h-comparison-v1",
            createdUtc = DateTime.UtcNow,
            runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            processorCount = Environment.ProcessorCount,
            packSha256,
            xenonModel = PracticeXenonDataV1.Identity,
            referenceThermalPowerWatts = PracticeGameSessionFactory.PracticeReferencePowerWatts,
            criticalityTolerance = PracticeLiquidZoneRrsIdentityV1.CriticalityTolerance,
            controllerIdentity = PracticeLiquidZoneRrsIdentityV1.ControllerIdentity,
            shapeTolerance = PracticeLiquidZoneRrsIdentityV1.ControllerTolerance,
            method = "Six default seeds, 72h. Startup targets 100%, 95% at 120s, 100% at 360s. Analytic xenon enabled everywhere. Current authoritative GameSession reference, matched Core reference at 1800s, and two-check Core paths at 1800s/900s/180s. Core integrations split at the same 180s control boundaries and startup events. Capture every 1800s plus before/after each hour-14 refuel. Hour-14 actions selected by the current GameSession reference and replayed unchanged into each Core variant. Native timings; Core variants exclude Game presentation/command overhead. Candidate counts exclude bootstrap, include instantaneous inventory events. No tighter endpoint probes included in wall timing.",
            runs
        }, JsonOptions));
        CampaignRun Execute(string policy, ulong seed, bool refuel, Func<CampaignRun> operation)
        {
            var existing = runs.FirstOrDefault(run => run.Policy == policy && run.Seed == seed && run.Refuel == refuel);
            if (existing != null) return existing;
            var run = operation();
            runs.Add(run);
            Save();
            return run;
        }

        // JIT warmup outside measured runs. All variants subsequently run serially.
        _ = PracticeGameSessionFactory.CreateBrowserPlaytest(1001);
        foreach (ulong seed in Seeds)
        {
            Execute("reference-game-1800", seed, false, () => RunGame(seed, false));
        }
        CampaignRun gameFuel = Execute("reference-game-1800", 1001, true, () => RunGame(1001, true));
        foreach (string policy in new[] { "reference-core-1800", "two-check-core-1800", "two-check-core-900", "two-check-core-180" })
        {
            foreach (ulong seed in Seeds)
            {
                Execute(policy, seed, false, () => RunCore(model, seed, policy, Array.Empty<RefuelAction>()));
            }
            if (gameFuel.RefuelActions.Count == 2)
                Execute(policy, 1001, true, () => RunCore(model, 1001, policy, gameFuel.RefuelActions));
        }
        Console.WriteLine($"Completed {runs.Count} runs: {directory}");
    }

    private static CampaignRun RunGame(ulong seed, bool refuel)
    {
        var result = new CampaignRun("reference-game-1800", seed, refuel);
        var timer = Stopwatch.StartNew();
        try
        {
            var session = PracticeGameSessionFactory.CreateBrowserPlaytest(seed);
            result.InitializationMs = timer.Elapsed.TotalMilliseconds;
            double generatedEnergy = 0;
            void Capture(string phase)
            {
                var rrs = session.CurrentLiquidZoneRrs;
                result.Samples.Add(Measure(session.CoreState, session.CurrentEquilibriumProjection, session.CurrentXenonState,
                    rrs.ZoneFills, rrs.ZonalShapeErrors, session.Snapshot.SimulationTimeSeconds, phase,
                    generatedEnergy));
            }
            Capture("equilibrium");
            while (session.Snapshot.SimulationTimeSeconds < Horizon && !session.Snapshot.IsGameOver)
            {
                double before = session.Snapshot.SimulationTimeSeconds;
                double energyBefore = session.CoreState.EnumerateBundles().Sum(b => b.CumulativeFissionEnergyJ);
                var advance = session.AdvanceWallMilliseconds(1000);
                if (!advance.Accepted || advance.Snapshot.SimulationTimeSeconds <= before)
                    throw new InvalidOperationException(advance.DiagnosticCode + ": " + advance.DiagnosticMessage);
                generatedEnergy += session.CoreState.EnumerateBundles().Sum(b => b.CumulativeFissionEnergyJ) - energyBefore;
                result.CandidateSolves += session.CurrentLiquidZoneRrs.TotalCandidateSolveCount;
                result.ObserveResidual(session.CurrentLiquidZoneRrs.CompensatedNetReactivity, session.CurrentLiquidZoneRrs.ZonalShapeErrors);
                Capture(refuel && advance.Snapshot.SimulationTimeSeconds == 14 * 3600 ? "before-refuel" : "equilibrium");
                if (refuel && advance.Snapshot.SimulationTimeSeconds == 14 * 3600)
                {
                    var channels = session.Snapshot.Core.Channels;
                    var choices = channels.Select(c => new
                    {
                        channel = c,
                        direction = c.Bundles.Skip(4).Average(b => b.CurrentBurnupMwDayPerKg) >= c.Bundles.Take(8).Average(b => b.CurrentBurnupMwDayPerKg)
                            ? "toward-end-b" : "toward-end-a"
                    }).GroupBy(c => c.direction).OrderBy(g => g.Key)
                        .Select(g => g.OrderByDescending(c => c.channel.AverageBurnupMwDayPerKg).ThenBy(c => c.channel.ChannelIndex).First()).ToArray();
                    if (choices.Length != 2) throw new InvalidOperationException("Expected both discharge directions.");
                    foreach (var choice in choices)
                    {
                        var watch = Stopwatch.StartNew();
                        var operation = session.RefuelChannel(choice.channel.ChannelIndex, choice.direction, 8, "NAT-U-SYNTHETIC");
                        watch.Stop();
                        if (!operation.Accepted) throw new InvalidOperationException(operation.DiagnosticMessage);
                        if (session.Snapshot.SimulationTimeSeconds != 14 * 3600) throw new InvalidOperationException("Refuel advanced the clock.");
                        result.RefuelActions.Add(new RefuelAction(choice.channel.ChannelIndex, choice.direction, watch.Elapsed.TotalMilliseconds));
                        result.CandidateSolves += session.CurrentLiquidZoneRrs.TotalCandidateSolveCount;
                        result.ObserveResidual(session.CurrentLiquidZoneRrs.CompensatedNetReactivity, session.CurrentLiquidZoneRrs.ZonalShapeErrors);
                        Capture("after-refuel-" + result.RefuelActions.Count);
                    }
                }
                if (advance.Snapshot.SimulationTimeSeconds % 86400 == 0)
                    Console.WriteLine($"{result.Policy} seed {seed} refuel={refuel}: {advance.Snapshot.SimulationTimeSeconds / 3600}h");
            }
            result.CompletedHours = session.Snapshot.SimulationTimeSeconds / 3600;
            result.Terminal = session.Snapshot.IsGameOver;
            result.TerminalReason = session.Snapshot.GameOverReason;
            result.EndNodePowerWatts = session.CurrentEquilibriumProjection.ShapeNodePowerWatts.ToArray();
            result.EndBurnup = session.CoreState.EnumerateBundles().Select(b => b.CurrentBurnupJPerKgHm / 8.64e10).ToArray();
        }
        catch (InvalidOperationException exception) { result.Error = exception.Message; }
        result.WallMs = timer.Elapsed.TotalMilliseconds;
        Print(result);
        return result;
    }

    private static CampaignRun RunCore(FullCoreDiffusionModelV1 model, ulong seed, string policy, IReadOnlyList<RefuelAction> actions, int bootstrapPasses = 8)
    {
        bool twoChecks = policy.StartsWith("two-check", StringComparison.Ordinal);
        double cadence = policy switch
        {
            "two-check-core-180" => 180,
            "two-check-core-900" => 900,
            "two-check-core-1800" or "reference-core-1800" => 1800,
            _ => throw new ArgumentException("Unknown Core campaign policy.", nameof(policy))
        };
        var result = new CampaignRun(policy, seed, actions.Count > 0);
        result.BootstrapPassBudget = twoChecks ? bootstrapPasses : null;
        var timer = Stopwatch.StartNew();
        try
        {
            var core = SyntheticGameCoreStateV1.CreateAgedPractice(seed);
            var solver = Require(EquilibriumCoreSolverV1.TryCreate(model, core.EnumerateBundles(), PracticeGameSessionFactory.PracticeReferencePowerWatts));
            var initial = Require(PracticeLiquidZoneRrsV1.TryCreate(Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6()), solver.CurrentProjection));
            PracticeSingleSolveStateV1? single = null;
            PracticeLiquidZoneRrsV1 rrs = initial;
            EquilibriumCoreProjectionV1 projection;
            if (twoChecks)
            {
                single = Require(PracticeLiquidZoneRrsV1.TryInitializeSingleSolve(solver, core.EnumerateBundles(), initial, maximumPasses: bootstrapPasses));
                projection = single.Projection;
            }
            else
            {
                var settled = Require(PracticeLiquidZoneRrsV1.TryRunEquilibrium(solver, core.EnumerateBundles(), initial, 0));
                Require(solver.TryCommitCandidate(settled.Projection));
                for (int pass = 0; pass < 7 && Math.Abs(settled.State.CompensatedNetReactivity) > PracticeLiquidZoneRrsIdentityV1.CriticalityTolerance; pass++)
                {
                    settled = Require(PracticeLiquidZoneRrsV1.TryRunEquilibrium(solver, core.EnumerateBundles(), settled.State, 0));
                    Require(solver.TryCommitCandidate(settled.Projection));
                }
                rrs = settled.State;
                projection = settled.Projection;
            }
            Require(solver.TryCommitCandidate(projection));
            var poison = PracticeXenonStateV1.CreateEquilibrium(core, projection, 0);
            result.InitializationMs = timer.Elapsed.TotalMilliseconds;
            double time = 0, energy = 0;
            IReadOnlyList<double> Fills() => twoChecks ? single!.ZoneFills : rrs.ZoneFills;
            IReadOnlyList<double> Errors() => twoChecks ? single!.ZonalShapeErrors : rrs.ZonalShapeErrors;
            void Capture(string phase) => result.Samples.Add(Measure(core, projection, poison, Fills(), Errors(), time, phase, energy));
            void Solve(bool inventoryEvent)
            {
                var watch = Stopwatch.StartNew();
                if (twoChecks)
                {
                    single = Require(inventoryEvent
                        ? PracticeLiquidZoneRrsV1.TryRunTwoSolveInventoryEvent(solver, core.EnumerateBundles(), single!, time, poison.Overlay)
                        : PracticeLiquidZoneRrsV1.TryRunTwoSolve(solver, core.EnumerateBundles(), single!, time, poison.Overlay));
                    projection = single.Projection;
                    result.CandidateSolves += single.CandidateSolveCount;
                    result.SpatialIterations += single.SpatialIterationCount;
                    if (single.FineTuningAccepted) result.AcceptedCorrections++;
                }
                else
                {
                    rrs = Require(rrs.TryWithSimulationTime(time));
                    var regulated = Require(PracticeLiquidZoneRrsV1.TryRunEquilibrium(solver, core.EnumerateBundles(), rrs, time, projection.SpatialSolve, poison.Overlay));
                    rrs = regulated.State;
                    projection = regulated.Projection;
                    result.CandidateSolves += rrs.TotalCandidateSolveCount;
                    // The reference API exposes accepted-candidate iterations only.
                }
                Require(solver.TryCommitCandidate(projection));
                watch.Stop();
                result.SolveWallMs += watch.Elapsed.TotalMilliseconds;
                result.ObserveResidual(projection.RelativeReactivity, Errors());
            }
            Capture("equilibrium");
            while (time < Horizon)
            {
                double end = Math.Min(Horizon, time + cadence);
                // Match Game's 180-second control boundaries and startup events,
                // independently of how often the spatial shape is refreshed.
                while (time < end)
                {
                    double next = Math.Min(end, (Math.Floor(time / 180) + 1) * 180);
                    foreach (double boundary in new double[] { 120, 240, 360 })
                        if (boundary > time) next = Math.Min(next, boundary);
                    double amplitude = time >= 120 && time < 360 ? 0.95 : 1;
                    // Match Game's multiplication order as well as integration boundaries.
                    double integratedPowerScale = amplitude * (next - time);
                    double[] increments = projection.ShapeNodePowerWatts.Select(p => p * integratedPowerScale).ToArray();
                    core = Require(core.TryAddFissionEnergy(increments));
                    poison = poison.Advance(projection, amplitude, next - time);
                    energy += increments.Sum();
                    time = next;
                }
                Solve(false);
                bool fuelNow = actions.Count > 0 && time == 14 * 3600;
                if (time % SampleInterval == 0) Capture(fuelNow ? "before-refuel" : "equilibrium");
                if (fuelNow)
                {
                    foreach (var action in actions)
                    {
                        var watch = Stopwatch.StartNew();
                        core = Require(core.TryRefuel(action.Channel, action.Direction == "toward-end-a"
                            ? GameRefuellingDirectionV1.TowardEndA : GameRefuellingDirectionV1.TowardEndB,
                            8, "NAT-U-SYNTHETIC", time)).ResultingState;
                        poison = poison.Rebind(core);
                        Solve(true);
                        watch.Stop();
                        result.RefuelActions.Add(action with { WallMs = watch.Elapsed.TotalMilliseconds });
                        Capture("after-refuel-" + result.RefuelActions.Count);
                    }
                }
                if (time % 86400 == 0) Console.WriteLine($"{policy} seed {seed} refuel={actions.Count > 0}: {time / 3600}h");
                if (Fills().Average() <= 0 || Fills().Average() >= 1)
                {
                    result.Terminal = true;
                    result.TerminalReason = Fills().Average() <= 0 ? "liquid-zone-average-empty" : "liquid-zone-average-full";
                    break;
                }
            }
            result.CompletedHours = time / 3600;
            result.EndNodePowerWatts = projection.ShapeNodePowerWatts.ToArray();
            result.EndBurnup = core.EnumerateBundles().Select(b => b.CurrentBurnupJPerKgHm / 8.64e10).ToArray();
        }
        catch (InvalidOperationException exception) { result.Error = exception.Message; }
        result.WallMs = timer.Elapsed.TotalMilliseconds;
        Print(result);
        return result;
    }

    private static CampaignSample Measure(SyntheticGameCoreStateV1 core, EquilibriumCoreProjectionV1 projection,
        PracticeXenonStateV1 poison, IReadOnlyList<double> fills, IReadOnlyList<double> errors,
        double time, string phase, double energy)
    {
        double total = projection.ShapeNodePowerWatts.Sum();
        if (!double.IsFinite(total) || Math.Abs(total / PracticeGameSessionFactory.PracticeReferencePowerWatts - 1) > 1e-6 ||
            fills.Any(f => !double.IsFinite(f) || f < 0 || f > 1) || poison.SimulationTimeSeconds != time)
            throw new InvalidOperationException("Power, fills or poison clock failed validation.");
        double peakChannel = Enumerable.Range(0, 380).Max(channel => projection.ShapeNodePowerWatts.Skip(channel * 12).Take(12).Sum());
        return new CampaignSample(time / 3600, phase, projection.RelativeReactivity,
            errors.Max(Math.Abs), peakChannel / 1e6, projection.ShapeNodePowerWatts.Max() / 1e3,
            fills.Select(f => f * 100).ToArray(), core.EnumerateBundles().Average(b => b.CurrentBurnupJPerKgHm / 8.64e10),
            poison.Iodine.Sum(), poison.Xenon.Sum(), energy);
    }

    private static void Print(CampaignRun run) => Console.WriteLine($"{run.Policy} seed {run.Seed} refuel={run.Refuel}: {run.CompletedHours}h, {run.WallMs / 1000:F2}s, {run.CandidateSolves} candidates, error={run.Error}");
    private static T Require<T>(ContractValidationResult<T> result) => result.IsValid ? result.Value : throw new InvalidOperationException(result.FirstDiagnostic.ToString());
}

internal sealed class CampaignRun(string policy, ulong seed, bool refuel)
{
    public string Policy { get; } = policy;
    public ulong Seed { get; } = seed;
    public bool Refuel { get; } = refuel;
    public double CompletedHours { get; set; }
    public bool Terminal { get; set; }
    public string TerminalReason { get; set; } = "";
    public string Error { get; set; } = "";
    public double InitializationMs { get; set; }
    public int? BootstrapPassBudget { get; set; }
    public double WallMs { get; set; }
    public double SolveWallMs { get; set; }
    public int CandidateSolves { get; set; }
    public int? SpatialIterations { get; set; } = policy.StartsWith("two-check", StringComparison.Ordinal) ? 0 : null;
    public int AcceptedCorrections { get; set; }
    public double MaxAbsoluteReactivity { get; set; }
    public double MaxShapeError { get; set; }
    public int ResidualChecks { get; set; }
    public int NonconvergedChecks { get; set; }
    public List<CampaignSample> Samples { get; set; } = new();
    public List<RefuelAction> RefuelActions { get; set; } = new();
    public double[] EndNodePowerWatts { get; set; } = Array.Empty<double>();
    public double[] EndBurnup { get; set; } = Array.Empty<double>();
    public void ObserveResidual(double rho, IReadOnlyList<double> errors)
    {
        MaxAbsoluteReactivity = Math.Max(MaxAbsoluteReactivity, Math.Abs(rho));
        MaxShapeError = Math.Max(MaxShapeError, errors.Max(Math.Abs));
        ResidualChecks++;
        if (Math.Abs(rho) > PracticeLiquidZoneRrsIdentityV1.CriticalityTolerance || errors.Max(Math.Abs) > PracticeLiquidZoneRrsIdentityV1.ControllerTolerance)
            NonconvergedChecks++;
    }
}

internal sealed record CampaignSample(double Hours, string Phase, double Reactivity, double MaxShapeError,
    double MaxChannelMw, double MaxBundleKw, double[] ZoneFillsPercent, double AverageBurnup,
    double Iodine, double Xenon, double GeneratedEnergyJ);
internal sealed record RefuelAction(uint Channel, string Direction, double WallMs);
