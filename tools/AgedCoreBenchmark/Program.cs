using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ReactorSim.Core;
using ReactorSim.Game;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
if (args.Length > 0 && args[0] == "--fit-core")
{
    CoreFuelCalibration.Run(args.Length > 1 ? args[1] : "artifacts/core-fuel-calibration", args.Length > 2 ? args[2] : null);
    return;
}
if (args.Length > 0 && args[0] == "--refuel-worth")
{
    AuditRefuelWorth(args.Length > 1 ? args[1] : "artifacts/refuel-worth");
    return;
}
if (args.Length > 0 && args[0] == "--refuel-response")
{
    BenchmarkRefuelResponse(args.Length > 1 ? args[1] : "artifacts/refuel-zone-response", args.Length > 2 ? ulong.Parse(args[2], CultureInfo.InvariantCulture) : 1001);
    return;
}
if (args.Length > 0 && args[0] == "--calibrate-zones")
{
    CalibrateZones(args.Length > 1 ? args[1] : "artifacts/zone-calibration", args.Length > 2 ? args[2] : null);
    return;
}
if (args.Length > 0 && args[0] == "--zone-geometry")
{
    AuditZoneGeometry(args.Length > 1 ? args[1] : "artifacts/zone-geometry-audit");
    return;
}
if (args.Length > 0 && args[0] == "--zone-worth")
{
    AuditZones(args.Length > 1 ? args[1] : "artifacts/zone-worth-audit");
    return;
}
string outputDirectory = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/aged-core-benchmark");
ulong[] seeds = args.Length > 1
    ? args.Skip(1).Select(ulong.Parse).ToArray()
    : new ulong[] { 0, 1001, 1002, 1003, 1004, uint.MaxValue };
Directory.CreateDirectory(outputDirectory);
var summaries = new List<SeedSummary>();
var samples = new List<Sample>();
var totalTimer = Stopwatch.StartNew();
foreach (ulong seed in seeds)
{
    var timer = Stopwatch.StartNew();
    var session = PracticeGameSessionFactory.CreateBrowserPlaytest(seed);
    BundleState[] initialBundles = session.CoreState.EnumerateBundles().ToArray();
    var rows = new List<Sample>();
    rows.Add(Measure(session));
    Console.WriteLine($"Seed {seed}: initialized, rho={rows[0].UnregulatedRho:R}, fills={rows[0].ZoneMeanPercent:F2}%");
    while (session.Snapshot.SimulationTimeSeconds < 3 * 86400 && !session.Snapshot.IsGameOver)
    {
        double before = session.Snapshot.SimulationTimeSeconds;
        var advance = session.AdvanceWallMilliseconds(1000);
        if (!advance.Accepted) throw new InvalidOperationException($"Seed {seed}: {advance.DiagnosticCode}: {advance.DiagnosticMessage}");
        if (advance.Snapshot.SimulationTimeSeconds <= before) throw new InvalidOperationException("The running session did not advance.");
        rows.Add(Measure(session));
        if (rows[^1].Seconds % 86400 == 0 || rows[^1].Terminal)
            Console.WriteLine($"Seed {seed}: day {rows[^1].Seconds / 86400:F3}, peak channel={rows[^1].MaxChannelMw:F4} MW, rho={rows[^1].UnregulatedRho:R}, regulated rho={rows[^1].RegulatedRho:R}, elapsed={timer.Elapsed.TotalSeconds:F1}s");
    }
    Sample initial = rows[0], final = rows[^1];
    Sample channelPeak = rows.MaxBy(row => row.MaxChannelMw)!;
    Sample bundlePeak = rows.MaxBy(row => row.MaxBundleKw)!;
    double days = final.Seconds / 86400;
    double lossMk = (initial.UnregulatedRho - final.UnregulatedRho) * 1000;
    Sample? dayOne = rows.FirstOrDefault(row => row.Seconds == 86400);
    Console.WriteLine($"Seed {seed}: checking burnup-only decay with tighter offline solves...");
    var initialProbe = Probe(initialBundles);
    var finalProbe = Probe(session.CoreState.EnumerateBundles().ToArray());
    double probeLossMk = (initialProbe.Rho - finalProbe.Rho) * 1000;
    var summary = new SeedSummary(seed, days, rows.Count, final.Terminal, final.TerminalReason,
        channelPeak.MaxChannelMw, channelPeak.MaxChannelIndex, channelPeak.Seconds / 3600,
        bundlePeak.MaxBundleKw, bundlePeak.MaxBundleChannelIndex, bundlePeak.MaxBundlePositionIndex, bundlePeak.Seconds / 3600,
        final.ZoneMeanPercent, final.ZoneMinPercent, final.ZoneMaxPercent,
        rows.Average(row => row.ZoneMeanPercent), rows.Min(row => row.ZoneMinPercent), rows.Max(row => row.ZoneMaxPercent),
        initial.UnregulatedRho * 1000, final.UnregulatedRho * 1000, final.RegulatedRho * 1000,
        lossMk, lossMk / days,
        dayOne == null ? null : (initial.UnregulatedRho - dayOne.UnregulatedRho) * 1000 * 3,
        rows.Where(row => row.Seconds % 86400 == 0 || row.Terminal).ToArray(), timer.Elapsed.TotalSeconds,
        initialProbe, finalProbe, probeLossMk, probeLossMk / days);
    Console.WriteLine($"Seed {seed}: tighter-solve loss={probeLossMk:F6} milli-k ({probeLossMk / days:F6}/day), iterations={initialProbe.Iterations}/{finalProbe.Iterations}");
    summaries.Add(summary);
    samples.AddRange(rows);
    // Preserve completed runs even if a later run fails.
    Save();
}
Console.WriteLine($"Completed {summaries.Count} seeds in {totalTimer.Elapsed.TotalSeconds:F1}s. Results: {outputDirectory}");

void Save()
{
    var report = new
    {
        format = "aged-core-three-day-benchmark-v1",
        createdUtc = DateTime.UtcNow,
        dataPack = PracticeGameSessionFactory.DiffusionDataPackVersion,
        controllerModel = PracticeLiquidZoneRrsIdentityV1.ControllerIdentity,
        criticalityToleranceMk = 1000 * PracticeLiquidZoneRrsIdentityV1.CriticalityTolerance,
        zoneModel = new
        {
            mappingIdentity = PracticeLiquidZoneRrsIdentityV1.MappingIdentity,
            referenceFill = PracticeLiquidZoneRrsIdentityV1.AbsorptionReferenceFillFraction,
            initialFill = PracticeLiquidZoneRrsIdentityV1.InitialFillFraction,
            calibratedTotalWorthMk = PracticeLiquidZoneRrsIdentityV1.CalibratedTotalZoneWorthMk,
            fastAbsorptionPerMPerUnitFill = PracticeLiquidZoneRrsIdentityV1.Group1AbsorptionPerMPerFillFraction,
            thermalAbsorptionPerMPerUnitFill = PracticeLiquidZoneRrsIdentityV1.Group2AbsorptionPerMPerFillFraction,
            configuration = "Factory default: independent region and absorber IDs, same IDs at every node; effective homogenized absorption covers all 4560 nodes. Positive-only absorption fitted to 6.5 mk and reference fuel production rebalanced for seed 1001 at 50% fill."
        },
        referencePowerWatts = PracticeGameSessionFactory.PracticeReferencePowerWatts,
        referenceElectricalPowerWatts = PracticeGameSessionFactory.PracticeReferenceElectricalPowerWatts,
        requestedDays = 3,
        samplingIntervalSeconds = PracticeGameSessionFactory.FullCoreDiffusionRecomputeIntervalSeconds,
        method = "Authoritative native GameSession browser factory; normal 1x pacing and RRS; no user refuelling or power commands. Stock scripted initial targets remain (95% at 120s, 100% at 360s). No xenon dynamics. Stop on native terminal state.",
        reactivityDefinition = "rho=(k-1)/k; unregulated RRS CoreReactivity comes from the no-overlay empty-zone base solve using the live burned inventory. Decay is rho(start)-rho(end), positive means lost reactivity. milli-k=1000*rho. Day is 86400 simulation seconds.",
        peakDefinition = "Maximum across all 380 channels / 4560 bundles and all half-hour samples including startup; channel and bundle IDs are zero-based. Zone mean is unweighted across 14 fills.",
        offlineProbeDefinition = "Same live start/end inventories and unchanged coefficient tables, no zone overlay; independent cold-start Core solves. Only convergence tolerances tightened: k abs 1e-8, k rel 1e-7, residual 2e-6, source shape 1e-6, inner abs 1e-12, inner rel 1e-9, 256 inner / 4000 outer iterations. Probes never mutate the live session.",
        summaries
    };
    File.WriteAllText(Path.Combine(outputDirectory, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    var csv = new StringBuilder("seed,hours,total_power_mw,max_channel_mw,max_channel_index,max_bundle_kw,max_bundle_channel_index,max_bundle_position_index,zone_mean_percent,zone_min_percent,zone_max_percent,unregulated_rho,regulated_rho,rrs_reserve_percent,average_burnup_mwd_per_kg,terminal\n");
    foreach (var row in samples)
        csv.AppendLine(FormattableString.Invariant($"{row.Seed},{row.Seconds / 3600:R},{row.TotalPowerMw:R},{row.MaxChannelMw:R},{row.MaxChannelIndex},{row.MaxBundleKw:R},{row.MaxBundleChannelIndex},{row.MaxBundlePositionIndex},{row.ZoneMeanPercent:R},{row.ZoneMinPercent:R},{row.ZoneMaxPercent:R},{row.UnregulatedRho:R},{row.RegulatedRho:R},{row.RrsReservePercent:R},{row.AverageBurnupMwDayPerKg:R},{row.Terminal}"));
    File.WriteAllText(Path.Combine(outputDirectory, "samples.csv"), csv.ToString());
}

static ReactivityProbe Probe(BundleState[] bundles)
{
    var solution = EquilibriumCoreSolverV1.TryCreate(TightModel(), bundles, PracticeGameSessionFactory.PracticeReferencePowerWatts);
    if (!solution.IsValid) throw new InvalidOperationException(solution.FirstDiagnostic.ToString());
    var spatial = solution.Value.CurrentProjection.SpatialSolve;
    return new ReactivityProbe(solution.Value.CurrentProjection.RelativeReactivity, spatial.IterationCount, spatial.ResidualRelativeInfinity);
}

static FullCoreDiffusionModelV1 TightModel(string? sourcePackPath = null)
{
    using var stream = typeof(FullCoreDiffusionDataPackV1).Assembly.GetManifestResourceStream(
        "ReactorSim.Core.Data.candu6-two-group-diffusion-pack-v1.json")!;
    using var reader = new StreamReader(stream);
    var json = JsonNode.Parse(sourcePackPath is null ? reader.ReadToEnd() : File.ReadAllText(sourcePackPath))!;
    var solver = json["solver"]!;
    solver["absolute_residual_tolerance"] = 1e-12;
    solver["relative_residual_tolerance"] = 1e-9;
    solver["maximum_inner_iterations"] = 256;
    solver["k_absolute_tolerance"] = 1e-8;
    solver["k_relative_tolerance"] = 1e-7;
    solver["residual_tolerance"] = 2e-6;
    solver["source_shape_tolerance"] = 1e-6;
    solver["maximum_iterations"] = 4000;
    var pack = FullCoreDiffusionDataPackV1.TryLoadJson(json.ToJsonString());
    if (!pack.IsValid) throw new InvalidOperationException(pack.FirstDiagnostic.ToString());
    var model = FullCoreDiffusionModelV1.TryCreateCandu6(pack.Value);
    if (!model.IsValid) throw new InvalidOperationException(model.FirstDiagnostic.ToString());
    return model.Value;
}

static void AuditZones(string outputPath)
{
    Directory.CreateDirectory(outputPath);
    var results = new List<object>();
    var completedSeeds = new HashSet<ulong>();
    string savedPath = Path.Combine(outputPath, "zone-worth.json");
    if (File.Exists(savedPath))
    {
        using var saved = JsonDocument.Parse(File.ReadAllText(savedPath));
        if (saved.RootElement.GetProperty("dataPack").GetString() != PracticeGameSessionFactory.DiffusionDataPackVersion)
            throw new InvalidOperationException("Use a new audit directory for a different physics pack.");
        foreach (var row in saved.RootElement.GetProperty("results").EnumerateArray())
        {
            results.Add(row.Clone());
            completedSeeds.Add(row.GetProperty("Seed").GetUInt64());
        }
    }
    var mapping = Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());
    foreach (ulong seed in new ulong[] { 0, 1001, 1002, 1003, 1004, uint.MaxValue })
    {
        if (completedSeeds.Contains(seed)) continue;
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest(seed);
        BundleState[] initialBundles = session.CoreState.EnumerateBundles().ToArray();
        double[] initialFills = session.CurrentLiquidZoneRrs.ZoneFills.ToArray();
        double initialGameplayRho = session.CurrentLiquidZoneRrs.CompensatedNetReactivity;
        while (session.Snapshot.SimulationTimeSeconds < 3 * 86400 && !session.Snapshot.IsGameOver)
        {
            var result = session.AdvanceWallMilliseconds(1000);
            if (!result.Accepted) throw new InvalidOperationException(result.DiagnosticMessage);
        }
        if (session.Snapshot.SimulationTimeSeconds != 3 * 86400) throw new InvalidOperationException("Audit requires a completed 72-hour trajectory.");
        BundleState[] finalBundles = session.CoreState.EnumerateBundles().ToArray();
        double[] finalFills = session.CurrentLiquidZoneRrs.ZoneFills.ToArray();
        var initialSolver = Require(EquilibriumCoreSolverV1.TryCreate(TightModel(), initialBundles, 1e9));
        var finalSolver = Require(EquilibriumCoreSolverV1.TryCreate(TightModel(), finalBundles, 1e9));
        EquilibriumCoreProjectionV1 Solve(EquilibriumCoreSolverV1 solver, BundleState[] inventory, double[] fills) =>
            Require(solver.TrySolveCandidate(inventory, Require(mapping.TryBuildOverlay(fills))));
        var initialControlled = Solve(initialSolver, initialBundles, initialFills);
        Require(initialSolver.TryCommitCandidate(initialControlled));
        var finalAtInitialFills = Solve(finalSolver, finalBundles, initialFills);
        var finalControlled = Solve(finalSolver, finalBundles, finalFills);
        Require(finalSolver.TryCommitCandidate(finalControlled));
        double UniformCoefficient(EquilibriumCoreSolverV1 solver, BundleState[] inventory, double[] fills)
        {
            double plus = Math.Min(0.01, 1 - fills.Max()), minus = Math.Min(0.01, fills.Min());
            if (plus + minus <= 0) throw new InvalidOperationException("No feasible uniform perturbation at opposing fill limits.");
            return (Solve(solver, inventory, fills.Select(f => f + plus).ToArray()).RelativeReactivity -
                Solve(solver, inventory, fills.Select(f => f - minus).ToArray()).RelativeReactivity) * 10 / (plus + minus);
        }
        double initialCoefficient = UniformCoefficient(initialSolver, initialBundles, initialFills);
        double finalCoefficient = UniformCoefficient(finalSolver, finalBundles, finalFills);
        var zoneCoefficients = new double[14];
        for (int zone = 0; zone < 14; zone++)
        {
            double[] plus = (double[])finalFills.Clone(), minus = (double[])finalFills.Clone();
            plus[zone] = Math.Min(1, plus[zone] + 0.01); minus[zone] = Math.Max(0, minus[zone] - 0.01);
            zoneCoefficients[zone] = (Solve(finalSolver, finalBundles, plus).RelativeReactivity -
                Solve(finalSolver, finalBundles, minus).RelativeReactivity) * 10 / (plus[zone] - minus[zone]);
        }
        double burnLossMk = (initialControlled.RelativeReactivity - finalAtInitialFills.RelativeReactivity) * 1000;
        double zoneGainMk = (finalControlled.RelativeReactivity - finalAtInitialFills.RelativeReactivity) * 1000;
        double linearZoneGainMk = Enumerable.Range(0, 14).Sum(z => zoneCoefficients[z] * (finalFills[z] - initialFills[z]) * 100);
        var row = new
        {
            Seed = seed,
            InitialFillsPercent = initialFills.Select(f => f * 100).ToArray(),
            FinalFillsPercent = finalFills.Select(f => f * 100).ToArray(),
            InitialMeanFillPercent = initialFills.Average() * 100,
            FinalMeanFillPercent = finalFills.Average() * 100,
            ActualMeanDrainPercentagePoints = (initialFills.Average() - finalFills.Average()) * 100,
            InitialUniformCoefficientMkPerPercentagePoint = initialCoefficient,
            FinalUniformCoefficientMkPerPercentagePoint = finalCoefficient,
            FinalIndividualZoneCoefficientsMkPerPercentagePoint = zoneCoefficients,
            FixedInitialZonesBurnupLossMk = burnLossMk,
            ExpectedUniformDrainPercentagePoints = burnLossMk / -finalCoefficient,
            ExactReactivityGainedFromZoneMovementMk = zoneGainMk,
            EquivalentImportanceWeightedDrainPercentagePoints = zoneGainMk / -finalCoefficient,
            LinearizedZoneMovementGainMk = linearZoneGainMk,
            InitialTightControlledRhoMk = initialControlled.RelativeReactivity * 1000,
            FinalTightControlledRhoMk = finalControlled.RelativeReactivity * 1000,
            InitialGameplayControlledRhoMk = initialGameplayRho * 1000,
            FinalGameplayControlledRhoMk = session.CurrentLiquidZoneRrs.CompensatedNetReactivity * 1000,
            FinalTightSolveResidual = finalControlled.SpatialSolve.ResidualRelativeInfinity
        };
        results.Add(row);
        File.WriteAllText(Path.Combine(outputPath, "zone-worth.json"), JsonSerializer.Serialize(new
        {
            format = "liquid-zone-worth-audit-v1",
            dataPack = PracticeGameSessionFactory.DiffusionDataPackVersion,
            absorptionPerMPerUnitFill = new
            {
                Fast = PracticeLiquidZoneRrsIdentityV1.Group1AbsorptionPerMPerFillFraction,
                Thermal = PracticeLiquidZoneRrsIdentityV1.Group2AbsorptionPerMPerFillFraction
            },
            referenceFill = PracticeLiquidZoneRrsIdentityV1.AbsorptionReferenceFillFraction,
            method = "Native 72-hour trajectories; tighter endpoint solves with actual initial/final zone patterns; +/-1 percentage-point central differences, clipped to physical bounds with actual perturbation denominator (one-sided at a fill limit). Uniform coefficient changes all 14 fills together. Individual coefficients change one zone. Positive drain means fill falls. Exact frozen-inventory decomposition separates burnup loss from zone movement.",
            results
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Seed {seed}: coefficient={finalCoefficient:F4} mk/pp; burn loss={burnLossMk:F4} mk; expected drain={burnLossMk / -finalCoefficient:F3} pp; actual mean drain={row.ActualMeanDrainPercentagePoints:F3} pp; zone gain={zoneGainMk:F4} mk; residual start/end={row.InitialTightControlledRhoMk:F3}/{row.FinalTightControlledRhoMk:F3} mk");
    }
}

static void AuditZoneGeometry(string outputPath)
{
    Directory.CreateDirectory(outputPath);
    var mapping = Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());
    var results = new List<object>();
    foreach (ulong seed in new ulong[] { 0, 1001, 1002, 1003, 1004, uint.MaxValue })
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest(seed);
        var bundles = session.CoreState.EnumerateBundles().ToArray();
        var solver = Require(EquilibriumCoreSolverV1.TryCreate(TightModel(), bundles, 1e9));
        ContractValidationResult<EquilibriumCoreProjectionV1> Solve(double fill, double scale, bool emptyReference = false)
        {
            var scaled = Require(PracticeLiquidZoneRrsMappingV1.TryCreate(mapping.Nodes.Select(n =>
                new PracticeLiquidZoneRrsNodeBindingV1(n.Node, n.LogicalZoneId,
                    n.Group1AbsorptionPerMPerFillFraction * scale, n.Group2AbsorptionPerMPerFillFraction * scale))));
            var overlay = emptyReference
                ? Require(StaticAbsorptionOverlayV1.TryCreate("exploratory-empty-reference-zone-overlay",
                    scaled.Nodes.Select(n => new StaticAbsorptionOverlayEntryV1(n.Node,
                        n.Group1AbsorptionPerMPerFillFraction * fill, n.Group2AbsorptionPerMPerFillFraction * fill))))
                : Require(scaled.TryBuildOverlay(Enumerable.Repeat(fill, 14).ToArray()));
            return solver.TrySolveCandidate(bundles, overlay);
        }
        var empty = Solve(0, 1);
        double full = Require(Solve(1, 1)).RelativeReactivity * 1000;
        double reference = Require(Solve(0.5, 1)).RelativeReactivity * 1000;
        double extrapolatedWorth = 2 * (reference - full);
        double scale = 6.5 / extrapolatedWorth;
        double scaledEmpty = Require(Solve(0, scale, true)).RelativeReactivity * 1000;
        double scaledFull = Require(Solve(1, scale, true)).RelativeReactivity * 1000;
        results.Add(new
        {
            seed,
            emptyRhoMk = empty.IsValid ? (double?)(empty.Value.RelativeReactivity * 1000) : null,
            emptyFailure = empty.IsValid ? null : empty.FirstDiagnostic.ToString(),
            fullRhoMk = full,
            referenceRhoMk = reference,
            measuredHalfToFullWorthMk = reference - full,
            measuredEmptyToFullWorthMk = empty.IsValid ? (double?)(empty.Value.RelativeReactivity * 1000 - full) : null,
            extrapolatedFullRangeWorthMk = extrapolatedWorth,
            exploratoryUniformScale = scale,
            exploratoryScaledWorthMk = scaledEmpty - scaledFull,
            exploratoryScaledFullRhoMk = scaledFull
        });
        Console.WriteLine($"Seed {seed}: empty valid={empty.IsValid}, half-to-full worth={reference - full:F5} mk (full-range extrapolation {extrapolatedWorth:F5}); exploratory positive-only scale={scale:F6}, worth={scaledEmpty - scaledFull:F5} mk, full rho={scaledFull:F4} mk");
    }
    var nodes = mapping.Nodes.Select(n =>
    {
        var p = Candu6CoreTopologyFactoryV1.GetPosition(n.Node.ChannelId.Value);
        return new
        {
            channelIndex = n.Node.ChannelId.Value,
            position = n.Node.Position.Value,
            gridColumn = p.Column,
            gridRow = p.DisplayRow,
            logicalZoneId = n.LogicalZoneId,
            group1AbsorptionPerMPerFillFraction = n.Group1AbsorptionPerMPerFillFraction,
            group2AbsorptionPerMPerFillFraction = n.Group2AbsorptionPerMPerFillFraction
        };
    }).ToArray();
    File.WriteAllText(Path.Combine(outputPath, "zone-geometry.json"), JsonSerializer.Serialize(new
    {
        format = "zone-geometry-audit-v1",
        dataPack = PracticeGameSessionFactory.DiffusionDataPackVersion,
        method = "Tighter frozen starting-inventory solves at 0%, 50%, 100% fill. Empty failures recorded; direct empty-to-full worth when valid. Full-range extrapolation also recorded as twice 50%-100% worth. Exploratory scaling retained as a diagnostic, without changing runtime or pack. Synthetic homogenized absorption, not physical tube geometry.",
        totalNodes = nodes.Length,
        absorbingNodes = mapping.Nodes.Count(n => n.Group1AbsorptionPerMPerFillFraction > 0 || n.Group2AbsorptionPerMPerFillFraction > 0),
        regionNodeCounts = mapping.NodeIndicesByZone.Select(n => n.Count).ToArray(),
        results,
        nodes
    }, new JsonSerializerOptions { WriteIndented = true }));
}

static void CalibrateZones(string outputPath, string? sourcePackPath)
{
    Directory.CreateDirectory(outputPath);
    // Offline only: positive absorption, independent of the runtime's old signed reference.
    var bundles = SyntheticGameCoreStateV1.CreateAgedPractice(1001).EnumerateBundles().ToArray();
    var solver = Require(EquilibriumCoreSolverV1.TryCreate(TightModel(sourcePackPath), bundles, 1e9));
    var mapping = Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());
    double SolveK(double fill, double scale)
    {
        var overlay = Require(StaticAbsorptionOverlayV1.TryCreate("offline-positive-zone-calibration",
            mapping.Nodes.Select(n => new StaticAbsorptionOverlayEntryV1(n.Node,
                0.02 * scale * fill,
                0.008 * scale * fill))));
        return Require(solver.TrySolveCandidate(bundles, overlay)).SpatialSolve.EffectiveK;
    }
    double scale = 0.081;
    double emptyK = SolveK(0, scale), halfK = 0, fullK = 0, worth = 0;
    for (int pass = 0; pass < 6; pass++)
    {
        halfK = SolveK(0.5, scale); fullK = SolveK(1, scale);
        // A uniform multiplication of both nu-fission groups scales k exactly,
        // without changing fission heating, fuel history or the flux shape at fixed absorption.
        worth = 1000 * halfK * (1 / fullK - 1 / emptyK);
        Console.WriteLine($"Offline pass {pass}: zone scale={scale:R}, calibrated full-range worth={worth:F6} mk");
        if (Math.Abs(worth - 6.5) <= 0.001) break;
        scale *= 6.5 / worth;
    }
    double productionFactor = 1 / halfK;
    File.WriteAllText(Path.Combine(outputPath, "calibration.json"), JsonSerializer.Serialize(new
    {
        format = "positive-zone-calibration-v1",
        referenceSeed = 1001,
        targetTotalWorthMk = 6.5,
        sourcePack = sourcePackPath ?? PracticeGameSessionFactory.DiffusionDataPackVersion,
        absorberReferenceFill = 0.0,
        initialFill = 0.5,
        zoneSlopeScale = scale,
        fastAbsorptionPerMPerUnitFill = 0.02 * scale,
        thermalAbsorptionPerMPerUnitFill = 0.008 * scale,
        neutronProductionFactor = productionFactor,
        predictedEmptyRhoMk = (1 - 1 / (emptyK * productionFactor)) * 1000,
        predictedHalfRhoMk = 0.0,
        predictedFullRhoMk = (1 - 1 / (fullK * productionFactor)) * 1000,
        predictedTotalWorthMk = worth,
        method = "Tighter seed-1001 frozen-inventory diffusion solves with positive-only absorption. Slopes fitted to 6.5 mk empty-to-full. A common multiplier of both nu-fission groups makes the same inventory critical at uniform 50% fill. Synthetic game calibration; regional homogenization retained."
    }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Apply slopes {0.02 * scale:R}/{0.008 * scale:R}, production factor {productionFactor:R}.");
}

static T Require<T>(ContractValidationResult<T> result)
{
    if (!result.IsValid) throw new InvalidOperationException(result.FirstDiagnostic.ToString());
    return result.Value;
}

static void AuditRefuelWorth(string outputPath)
{
    Directory.CreateDirectory(outputPath);
    var session = PracticeGameSessionFactory.CreateBrowserPlaytest(1001);
    while (session.Snapshot.SimulationTimeSeconds < 14 * 3600)
    {
        var advance = session.AdvanceWallMilliseconds(1000);
        if (!advance.Accepted) throw new InvalidOperationException(advance.DiagnosticMessage);
    }
    var initialFills = session.CurrentLiquidZoneRrs.ZoneFills.ToArray();
    var choices = session.Snapshot.Core.Channels.Select(c => new
    {
        channel = c.ChannelIndex,
        average = c.AverageBurnupMwDayPerKg,
        direction = c.Bundles.Skip(4).Average(b => b.CurrentBurnupMwDayPerKg) >= c.Bundles.Take(8).Average(b => b.CurrentBurnupMwDayPerKg)
            ? "toward-end-b" : "toward-end-a"
    }).GroupBy(c => c.direction).OrderBy(g => g.Key)
        .Select(g => g.OrderByDescending(c => c.average).ThenBy(c => c.channel).First()).ToArray();
    var frozenOverlay = Require(session.CurrentLiquidZoneRrs.Mapping.TryBuildOverlay(initialFills));
    var observations = new List<object>();
    double? initialBare = null, initialFrozen = null, previousBare = null, previousFrozen = null;
    void Capture(uint? channel, string phase)
    {
        var bundles = session.CoreState.EnumerateBundles().ToArray();
        var solver = Require(EquilibriumCoreSolverV1.TryCreate(TightModel(), bundles, PracticeGameSessionFactory.PracticeReferenceThermalPowerWatts));
        double bare = solver.CurrentProjection.RelativeReactivity * 1000;
        double frozen = Require(solver.TrySolveCandidate(bundles, frozenOverlay)).RelativeReactivity * 1000;
        double live = Require(solver.TrySolveCandidate(bundles, Require(session.CurrentLiquidZoneRrs.TryBuildOverlay()))).RelativeReactivity * 1000;
        initialBare ??= bare; initialFrozen ??= frozen;
        var row = new
        {
            phase,
            channelIndex = channel,
            bareRhoMk = bare,
            frozenInitialZonesRhoMk = frozen,
            liveZonesRhoMk = live,
            cumulativeBareFuelGainMk = bare - initialBare.Value,
            cumulativeFrozenZonesFuelGainMk = frozen - initialFrozen.Value,
            incrementalBareFuelGainMk = bare - (previousBare ?? bare),
            incrementalFrozenZonesFuelGainMk = frozen - (previousFrozen ?? frozen),
            currentGameplayNetRhoMk = session.CurrentLiquidZoneRrs.CompensatedNetReactivity * 1000,
            channelInventory = session.Snapshot.Core.Channels.Where(c => choices.Any(choice => choice.channel == c.ChannelIndex)).Select(c => new
            {
                c.ChannelIndex,
                c.AverageBurnupMwDayPerKg,
                c.PowerWatts,
                bundles = c.Bundles.Select(b => new { b.Position, b.CurrentBurnupMwDayPerKg, b.PowerWatts })
            }).ToArray()
        };
        observations.Add(row);
        Console.WriteLine($"{phase}: bare gain={row.cumulativeBareFuelGainMk:F6} mk, frozen-zone gain={row.cumulativeFrozenZonesFuelGainMk:F6} mk; net rho={live:F6} mk");
        previousBare = bare; previousFrozen = frozen;
    }
    Capture(null, "before-refuelling");
    foreach (var choice in choices)
    {
        var result = session.RefuelChannel(choice.channel, choice.direction, 8, "NAT-U-SYNTHETIC");
        if (!result.Accepted) throw new InvalidOperationException(result.DiagnosticMessage);
        Capture(choice.channel, $"after-channel-{choice.channel}");
    }
    File.WriteAllText(Path.Combine(outputPath, "refuel-worth.json"), JsonSerializer.Serialize(new
    {
        seed = 1001,
        hour = 14,
        dataPack = PracticeGameSessionFactory.DiffusionDataPackVersion,
        method = "Replay authoritative hour-14 benchmark operations: oldest channel in each discharge direction, eight fresh bundles each. Tight independent same-inventory solves before/after. Frozen initial nonuniform zone fills isolate fuel worth; empty-zone solves also reported. No burn time elapses during refuelling. Live-zone probes separate controller response.",
        initialZoneFills = initialFills,
        observations
    }, new JsonSerializerOptions { WriteIndented = true }));
}

static void BenchmarkRefuelResponse(string outputPath, ulong seed)
{
    Directory.CreateDirectory(outputPath);
    var rows = new List<ZoneResponseSample>();
    var events = new List<object>();
    var mapping = Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());
    var timer = Stopwatch.StartNew();
    foreach (string scenario in new[] { "no-refuelling", "two-channels-at-14h" })
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest(seed);
        uint operations = 0;
        void Capture(string phase)
        {
            var sample = Measure(session, operations, 128 - 8 * operations);
            var fills = session.CurrentLiquidZoneRrs.ZoneFills.Select(f => 100 * f).ToArray();
            if (fills.Length != 14 || fills.Any(f => !double.IsFinite(f) || f < 0 || f > 100))
                throw new InvalidOperationException("Invalid zone fills.");
            rows.Add(new ZoneResponseSample(scenario, phase, sample, fills));
        }
        Capture("equilibrium");
        while (session.Snapshot.SimulationTimeSeconds < 72 * 3600 && !session.Snapshot.IsGameOver)
        {
            double before = session.Snapshot.SimulationTimeSeconds;
            var advance = session.AdvanceWallMilliseconds(1000);
            if (!advance.Accepted || advance.Snapshot.SimulationTimeSeconds <= before)
                throw new InvalidOperationException(advance.DiagnosticMessage);
            bool fuelNow = scenario == "two-channels-at-14h" && session.Snapshot.SimulationTimeSeconds == 14 * 3600;
            Capture(fuelNow ? "before-refuel" : "equilibrium");
            if (fuelNow)
            {
                // Select one old channel for each discharge direction from the
                // actual inventory, without prescribing a new reactor rule.
                var choices = session.Snapshot.Core.Channels.Select(c => new
                {
                    channel = c,
                    direction = c.Bundles.Skip(4).Average(b => b.CurrentBurnupMwDayPerKg) >= c.Bundles.Take(8).Average(b => b.CurrentBurnupMwDayPerKg)
                        ? "toward-end-b" : "toward-end-a"
                }).GroupBy(c => c.direction).OrderBy(g => g.Key)
                    .Select(g => g.OrderByDescending(c => c.channel.AverageBurnupMwDayPerKg).ThenBy(c => c.channel.ChannelIndex).First()).ToArray();
                if (choices.Length != 2) throw new InvalidOperationException("Expected two opposite discharge directions.");
                foreach (var choice in choices)
                {
                    var pre = rows[^1];
                    var result = session.RefuelChannel(choice.channel.ChannelIndex, choice.direction, 8, "NAT-U-SYNTHETIC");
                    if (!result.Accepted) throw new InvalidOperationException(result.DiagnosticMessage);
                    operations++;
                    if (session.Snapshot.SimulationTimeSeconds != 14 * 3600)
                        throw new InvalidOperationException("Refuelling must not advance time.");
                    Capture($"after-refuel-{operations}");
                    events.Add(new
                    {
                        hour = 14,
                        channelIndex = choice.channel.ChannelIndex,
                        direction = choice.direction,
                        shiftCount = 8,
                        transverseZonePair = mapping.Nodes.First(n => n.Node.ChannelId.Value == choice.channel.ChannelIndex).LogicalZoneId % 7 + 1,
                        preChannelAverageBurnupMwDayPerKg = choice.channel.AverageBurnupMwDayPerKg,
                        before = pre,
                        after = rows[^1],
                        message = result.Message
                    });
                    Console.WriteLine($"Seed {seed}: hour 14, channel {choice.channel.ChannelIndex}, {choice.direction}, 8 bundles; mean fill {rows[^1].Summary.ZoneMeanPercent:F3}%");
                }
            }
            if (session.Snapshot.SimulationTimeSeconds % 86400 == 0)
                Console.WriteLine($"{scenario}: hour {session.Snapshot.SimulationTimeSeconds / 3600:g}, elapsed {timer.Elapsed.TotalSeconds:F1}s");
        }
        if (session.Snapshot.SimulationTimeSeconds != 72 * 3600 || session.Snapshot.IsGameOver)
            throw new InvalidOperationException($"{scenario} terminated early: {session.Snapshot.GameOverReason}");
    }
    var report = new
    {
        format = "two-channel-refuel-zone-response-v1",
        seed,
        dataPack = PracticeGameSessionFactory.DiffusionDataPackVersion,
        controllerModel = PracticeLiquidZoneRrsIdentityV1.ControllerIdentity,
        criticalityToleranceMk = 1000 * PracticeLiquidZoneRrsIdentityV1.CriticalityTolerance,
        referencePowerWatts = PracticeGameSessionFactory.PracticeReferencePowerWatts,
        referenceElectricalPowerWatts = PracticeGameSessionFactory.PracticeReferenceElectricalPowerWatts,
        method = "Two authoritative browser-factory GameSession runs to 72h, identical seed and normal RRS. One no-refuel control; one refuels the oldest channel for each discharge direction at exactly 14h, eight fresh bundles each. Directions select the older eight-bundle outlet from live inventory. Two sequential accepted transactions at the same clock time; sampled before and after each. Half-hour equilibrium sampling, no extra user power commands, stock startup events retained. No xenon dynamics.",
        zonePairDefinition = "Z1/Z8, Z2/Z9, ..., Z7/Z14; End A then End B in the same transverse region.",
        refuellingEvents = events,
        samples = rows,
        wallSeconds = timer.Elapsed.TotalSeconds
    };
    File.WriteAllText(Path.Combine(outputPath, "report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    var csv = new StringBuilder("scenario,phase,hours,unregulated_rho,regulated_rho,max_channel_mw,max_bundle_kw," + string.Join(",", Enumerable.Range(1, 14).Select(z => $"z{z}_percent")) + "\n");
    foreach (var row in rows)
        csv.AppendLine(FormattableString.Invariant($"{row.Scenario},{row.Phase},{row.Summary.Seconds / 3600:R},{row.Summary.UnregulatedRho:R},{row.Summary.RegulatedRho:R},{row.Summary.MaxChannelMw:R},{row.Summary.MaxBundleKw:R},") + string.Join(",", row.ZoneFillsPercent.Select(f => f.ToString("R", CultureInfo.InvariantCulture))));
    File.WriteAllText(Path.Combine(outputPath, "zones.csv"), csv.ToString());
    Console.WriteLine($"Refuel response completed: {outputPath}");
}

static Sample Measure(GameSession session, uint expectedOperations = 0, uint expectedFreshBundles = 128)
{
    var snapshot = session.Snapshot;
    var channels = snapshot.Core.Channels;
    var maxChannel = channels.MaxBy(channel => channel.PowerWatts)!;
    var maxBundle = channels.SelectMany(channel => channel.Bundles.Select(bundle => (channel.ChannelIndex, Bundle: bundle)))
        .MaxBy(item => item.Bundle.PowerWatts);
    double[] fills = session.CurrentLiquidZoneRrs.ZoneFills.ToArray();
    double channelTotal = channels.Sum(channel => channel.PowerWatts);
    double bundleTotal = channels.Sum(channel => channel.Bundles.Sum(bundle => bundle.PowerWatts));
    if (!double.IsFinite(channelTotal) || Math.Abs(channelTotal - snapshot.Physics.TotalPowerWatts) > 1e-5 * channelTotal ||
        Math.Abs(bundleTotal - channelTotal) > 1e-5 * channelTotal || snapshot.RefuellingOperationCount != expectedOperations || snapshot.FreshBundlesAvailable != expectedFreshBundles)
        throw new InvalidOperationException("Power/inventory conservation failed in benchmark capture.");
    return new Sample(snapshot.Seed, snapshot.SimulationTimeSeconds, snapshot.Physics.TotalPowerWatts / 1e6,
        maxChannel.PowerWatts / 1e6, maxChannel.ChannelIndex,
        maxBundle.Bundle.PowerWatts / 1e3, maxBundle.ChannelIndex, maxBundle.Bundle.Position,
        fills.Average() * 100, fills.Min() * 100, fills.Max() * 100,
        session.CurrentLiquidZoneRrs.CoreReactivity, session.CurrentLiquidZoneRrs.CompensatedNetReactivity,
        snapshot.RrsReserveFraction * 100, channels.Average(channel => channel.AverageBurnupMwDayPerKg),
        snapshot.IsGameOver, snapshot.GameOverReason);
}

internal sealed record Sample(ulong Seed, double Seconds, double TotalPowerMw,
    double MaxChannelMw, uint MaxChannelIndex, double MaxBundleKw, uint MaxBundleChannelIndex, uint MaxBundlePositionIndex,
    double ZoneMeanPercent, double ZoneMinPercent, double ZoneMaxPercent, double UnregulatedRho, double RegulatedRho,
    double RrsReservePercent, double AverageBurnupMwDayPerKg, bool Terminal, string TerminalReason);

internal sealed record SeedSummary(ulong Seed, double CompletedDays, int SampleCount, bool Terminal, string TerminalReason,
    double PeakChannelMw, uint PeakChannelIndex, double PeakChannelHour,
    double PeakBundleKw, uint PeakBundleChannelIndex, uint PeakBundlePositionIndex, double PeakBundleHour,
    double FinalZoneMeanPercent, double FinalZoneMinPercent, double FinalZoneMaxPercent,
    double TrajectoryZoneTimeMeanPercent, double TrajectoryZoneMinPercent, double TrajectoryZoneMaxPercent,
    double InitialUnregulatedReactivityMk, double FinalUnregulatedReactivityMk, double FinalRegulatedReactivityMk,
    double ReactivityLossMk, double MeanReactivityLossMkPerDay, double? DayOneLinearPredictionOfThreeDayLossMk,
    Sample[] DailyCheckpoints, double WallSeconds,
    ReactivityProbe InitialOfflineProbe, ReactivityProbe FinalOfflineProbe, double OfflineProbeReactivityLossMk, double OfflineProbeMeanLossMkPerDay);

internal sealed record ReactivityProbe(double Rho, int Iterations, double ResidualRelativeInfinity);

internal sealed record ZoneResponseSample(string Scenario, string Phase, Sample Summary, double[] ZoneFillsPercent);
