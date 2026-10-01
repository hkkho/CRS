using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ReactorSim.Core;
using ReactorSim.Game;

internal static class CoreFuelCalibration
{
    private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true };
    internal static void Run(string outputPath, string? sourcePackPath = null)
    {
        Directory.CreateDirectory(outputPath);
        using var stream = typeof(FullCoreDiffusionDataPackV1).Assembly.GetManifestResourceStream(
            "ReactorSim.Core.Data.candu6-two-group-diffusion-pack-v1.json")!;
        using var reader = new StreamReader(stream);
        sourcePackPath ??= Path.Combine(outputPath, "source-pack.json");
        string sourceText = File.Exists(sourcePackPath) ? File.ReadAllText(sourcePackPath) : reader.ReadToEnd();
        var source = JsonNode.Parse(sourceText)!;
        if (source["data_pack_version"]!.GetValue<string>() != "candu6-two-group-diffusion-v1-burnup30-zones65")
            throw new InvalidOperationException("Supply the archived zones65 source pack to reproduce this calibration.");
        File.WriteAllText(Path.Combine(outputPath, "source-pack.json"), sourceText);
        const double channelCycleDays = 190;
        double exitBurnup = PracticeGameSessionFactory.PracticeReferenceThermalPowerWatts / 1e6 * channelCycleDays /
            (380 * 8 * SyntheticGameCoreStateV1.DefaultHeavyMetalMassKg);
        double coordinateScale = exitBurnup / 16.0; // Archived zones65 source discharge target.
        var trials = new List<object>();
        double contrast = 0.4;
        JsonNode? best = null;
        bool accepted = false;
        double bestZoneScale = 1, bestProductionScale = 1;
        for (int pass = 0; pass < 7; pass++)
        {
            var json = source.DeepClone();
            var material = json["coefficient_tables"]![0]!;
            var oldRows = source["coefficient_tables"]![0]!["rows"]!.AsArray();
            var rows = material["rows"]!.AsArray();
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i]!; var old = oldRows[i]!;
                row["burnup_j_per_kg_hm"] = old["burnup_j_per_kg_hm"]!.GetValue<double>() * coordinateScale;
                foreach (int group in new[] { 1, 2 })
                {
                    string f = $"fission_group{group}_per_m", nu = $"nu_fission_group{group}_per_m";
                    double original = old[f]!.GetValue<double>();
                    double fresh = oldRows[0]![f]!.GetValue<double>();
                    double adjusted = fresh + contrast * (original - fresh);
                    row[f] = adjusted;
                    row[nu] = old[nu]!.GetValue<double>() * adjusted / original;
                }
            }
            // Retain at least 30 MWd/kg domain with positive bounded tail knots.
            var last = rows[^1]!;
            double lastB = last["burnup_j_per_kg_hm"]!.GetValue<double>() / 8.64e10;
            double lastF = last["fission_group2_per_m"]!.GetValue<double>();
            double yield = last["nu_fission_group2_per_m"]!.GetValue<double>() / lastF;
            foreach (double b in new[] { 15.0, 20.0, 25.0, 30.0 })
            {
                if (b <= lastB) continue;
                var tail = last.DeepClone();
                double f = Math.Max(0.02, lastF - 0.002 * contrast / coordinateScale * (b - lastB));
                tail["burnup_j_per_kg_hm"] = b * 8.64e10;
                tail["fission_group2_per_m"] = f;
                tail["nu_fission_group2_per_m"] = yield * f;
                rows.Add(tail);
            }
            var initial = SyntheticGameCoreStateV1.CreateAgedPractice(1001, exitBurnup);
            var model = Model(json);
            var solver = Require(EquilibriumCoreSolverV1.TryCreate(model, initial.EnumerateBundles(), ThermalPower));
            double zoneScale = 1, productionScale = 1, worth = 0;
            for (int zonePass = 0; zonePass < 3; zonePass++)
            {
                double K(double fill) => Require(solver.TrySolveCandidate(initial.EnumerateBundles(), Overlay(fill, zoneScale))).SpatialSolve.EffectiveK;
                double empty = K(0), half = K(0.5), full = K(1);
                productionScale = 1 / half;
                worth = 1000 * half * (1 / full - 1 / empty);
                if (Math.Abs(worth - 6.5) < 0.001) break;
                zoneScale *= 6.5 / worth;
            }
            foreach (var row in rows)
                foreach (int group in new[] { 1, 2 })
                {
                    string field = $"nu_fission_group{group}_per_m";
                    row![field] = row[field]!.GetValue<double>() * productionScale;
                }
            model = Model(json);
            var overlay = Overlay(0.5, zoneScale);
            solver = Require(EquilibriumCoreSolverV1.TryCreate(model, initial.EnumerateBundles(), ThermalPower));
            var state = initial;
            var projection = Require(solver.TrySolveCandidate(state.EnumerateBundles(), overlay));
            Require(solver.TryCommitCandidate(projection));
            double initialRho = projection.RelativeReactivity;
            SyntheticGameCoreStateV1? at14 = null;
            for (int step = 1; step <= 48; step++)
            {
                state = Require(state.TryAddFissionEnergy(projection.ShapeNodePowerWatts.Select(p => p * 1800).ToArray()));
                projection = Require(solver.TrySolveCandidate(state.EnumerateBundles(), overlay));
                Require(solver.TryCommitCandidate(projection));
                if (step == 28) at14 = state;
            }
            double dailyDecay = 1000 * (initialRho - projection.RelativeReactivity);
            var fuelSolver = Require(EquilibriumCoreSolverV1.TryCreate(model, at14!.EnumerateBundles(), ThermalPower));
            double before = Require(fuelSolver.TrySolveCandidate(at14.EnumerateBundles(), overlay)).RelativeReactivity;
            var choices = Enumerable.Range(0, 380).Select(c => new
            {
                channel = (uint)c,
                average = at14.GetChannel((uint)c).Average(b => b.CurrentBurnupJPerKgHm),
                direction = at14.GetChannel((uint)c).Skip(4).Average(b => b.CurrentBurnupJPerKgHm) >= at14.GetChannel((uint)c).Take(8).Average(b => b.CurrentBurnupJPerKgHm)
                    ? GameRefuellingDirectionV1.TowardEndB : GameRefuellingDirectionV1.TowardEndA
            }).GroupBy(c => c.direction).OrderBy(g => g.Key)
                .Select(g => g.OrderByDescending(c => c.average).ThenBy(c => c.channel).First()).ToArray();
            var refuelled = at14;
            foreach (var choice in choices)
                refuelled = Require(refuelled.TryRefuel(choice.channel, choice.direction, 8, "NAT-U-SYNTHETIC", 14 * 3600)).ResultingState;
            double after = Require(fuelSolver.TrySolveCandidate(refuelled.EnumerateBundles(), overlay)).RelativeReactivity;
            double fuelWorth = 1000 * (after - before);
            var trial = new
            {
                pass,
                contrast,
                exitBurnupMwDayPerKg = exitBurnup,
                productionScale,
                zoneScale,
                totalZoneWorthMk = worth,
                dailyFixedZonesDecayMk = dailyDecay,
                sixteenBundleWorthMk = fuelWorth,
                channels = choices.Select(c => c.channel).ToArray()
            };
            trials.Add(trial);
            Console.WriteLine($"Fit {pass}: contrast={contrast:F6}, 16-bundle worth={fuelWorth:F6} mk, daily decay={dailyDecay:F6} mk, zones={worth:F6} mk");
            best = json; bestZoneScale = zoneScale; bestProductionScale = productionScale;
            if (fuelWorth >= 0.4 && fuelWorth <= 0.7 && dailyDecay >= 0.4 && dailyDecay <= 0.7)
            {
                accepted = true;
                break;
            }
            contrast *= 0.55 / fuelWorth;
            if (contrast < 0.001 || contrast > 1.5) throw new InvalidOperationException("Fit left bounded contrast domain.");
        }
        if (!accepted) throw new InvalidOperationException("No candidate met both the 16-bundle worth and one-day decay ranges.");
        File.WriteAllText(Path.Combine(outputPath, "proposal-pack.json"), best!.ToJsonString(PrettyJson));
        File.WriteAllText(Path.Combine(outputPath, "fit.json"), JsonSerializer.Serialize(new
        {
            sourcePack = source["data_pack_version"]!.GetValue<string>(),
            channelCycleDays,
            averageBundleResidenceDays = 1.5 * channelCycleDays,
            targetExitBurnupMwDayPerKg = exitBurnup,
            thermalPowerWatts = ThermalPower,
            electricalPowerWatts = PracticeGameSessionFactory.PracticeReferenceElectricalPowerWatts,
            fastAbsorptionPerMPerFill = PracticeLiquidZoneRrsIdentityV1.Group1AbsorptionPerMPerFillFraction * bestZoneScale,
            thermalAbsorptionPerMPerFill = PracticeLiquidZoneRrsIdentityV1.Group2AbsorptionPerMPerFillFraction * bestZoneScale,
            productionMultiplierRelativeToSource = bestProductionScale,
            trials,
            method = "Offline synthetic joint fit: 190-FPD channel cycle at 2064 MW thermal; energy-balanced discharge target; remapped burnup knots; reduced fission/production contrast preserving group yields before common criticality multiplier. Seed-1001 half-fill critical reference, 6.5 mk empty-to-full zones, 14h old-channel pair refuelling worth and frozen half-fill one-day decay both 0.4-0.7 mk. Does not modify runtime files."
        }, PrettyJson));
    }

    private static double ThermalPower => PracticeGameSessionFactory.PracticeReferenceThermalPowerWatts;

    private static StaticAbsorptionOverlayV1 Overlay(double fill, double scale)
    {
        var mapping = Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());
        return Require(StaticAbsorptionOverlayV1.TryCreate("offline-joint-core-fit", mapping.Nodes.Select(n =>
            new StaticAbsorptionOverlayEntryV1(n.Node, n.Group1AbsorptionPerMPerFillFraction * fill * scale,
                n.Group2AbsorptionPerMPerFillFraction * fill * scale))));
    }

    private static FullCoreDiffusionModelV1 Model(JsonNode input)
    {
        var json = input.DeepClone(); var solver = json["solver"]!;
        solver["absolute_residual_tolerance"] = 1e-12; solver["relative_residual_tolerance"] = 1e-9;
        solver["maximum_inner_iterations"] = 256; solver["k_absolute_tolerance"] = 1e-8;
        solver["k_relative_tolerance"] = 1e-7; solver["residual_tolerance"] = 2e-6;
        solver["source_shape_tolerance"] = 1e-6; solver["maximum_iterations"] = 4000;
        return Require(FullCoreDiffusionModelV1.TryCreateCandu6(Require(FullCoreDiffusionDataPackV1.TryLoadJson(json.ToJsonString()))));
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        if (!result.IsValid) throw new InvalidOperationException(result.FirstDiagnostic.ToString());
        return result.Value;
    }
}
