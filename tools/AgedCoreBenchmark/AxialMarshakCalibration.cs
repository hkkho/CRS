using System.Text.Json;
using System.Text.Json.Nodes;
using ReactorSim.Core;

internal static class AxialMarshakCalibration
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };
    public static void Run(string sourcePath, string directory)
    {
        Directory.CreateDirectory(directory);
        var source = JsonNode.Parse(File.ReadAllText(sourcePath))!;
        if (source["data_pack_version"]!.GetValue<string>() != "candu6-two-group-diffusion-v1-lzc-tubes-v7")
            throw new ArgumentException("Supply the archived v7 pack.", nameof(sourcePath));
        var proposal = source.DeepClone();
        proposal["data_pack_version"] = "candu6-two-group-diffusion-v1-axial-marshak-v8";
        proposal["geometry"]!["axial_boundary"] = new JsonObject
        {
            ["condition_id"] = "zero-incoming-current-v1",
            ["cell_length_m"] = PracticeAdjustersV1.BundleLengthM
        };
        var core = SyntheticGameCoreStateV1.CreateAgedPractice(1001);
        double production = 1.04;
        double rod = source["adjusters"]!["inner_absorption_group2_per_m"]!.GetValue<double>() * .7;
        double water = PracticeLiquidZoneRrsIdentityV1.Group2AbsorptionPerMPerFillFraction;
        PracticeXenonStateV1? poison = null;
        var trials = new List<object>();
        JsonNode Candidate(bool tight, bool noRods = false)
        {
            var candidate = proposal.DeepClone();
            candidate["adjusters"]!["inner_absorption_group1_per_m"] = noRods ? 0 : rod * .1;
            candidate["adjusters"]!["inner_absorption_group2_per_m"] = noRods ? 0 : rod;
            for (int t = 0; t < candidate["coefficient_tables"]!.AsArray().Count; t++)
            {
                var rows = candidate["coefficient_tables"]![t]!["rows"]!.AsArray();
                var original = source["coefficient_tables"]![t]!["rows"]!.AsArray();
                for (int n = 0; n < rows.Count; n++)
                    foreach (string group in new[] { "nu_fission_group1_per_m", "nu_fission_group2_per_m" })
                        rows[n]![group] = original[n]![group]!.GetValue<double>() * production;
            }
            if (tight)
            {
                var settings = candidate["solver"]!;
                settings["absolute_residual_tolerance"] = 1e-12;
                settings["relative_residual_tolerance"] = 1e-9;
                settings["maximum_inner_iterations"] = 256;
                settings["maximum_iterations"] = 5000;
                settings["k_absolute_tolerance"] = 2e-9;
                settings["k_relative_tolerance"] = 2e-9;
                settings["residual_tolerance"] = 2e-7;
                settings["source_shape_tolerance"] = 1e-7;
            }
            return candidate;
        }
        FullCoreDiffusionModelV1 Model(bool tight, bool noRods = false) => Require(
            FullCoreDiffusionModelV1.TryCreateCandu6(Require(FullCoreDiffusionDataPackV1.TryLoadJson(Candidate(tight, noRods).ToJsonString()))));
        StaticAbsorptionOverlayV1 Overlay(SyntheticGameCoreStateV1 inventory, double fill)
        {
            var mapping = Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6(water * .1, water));
            var zones = Require(mapping.TryBuildOverlay(Enumerable.Repeat(fill, 14).ToArray()));
            var xe = poison?.BindBurnupReference(inventory).Overlay;
            return Require(StaticAbsorptionOverlayV1.TryCreate("axial-marshak-fit-frozen-xe",
                zones.Entries.Select(e => new StaticAbsorptionOverlayEntryV1(e.Node, e.DeltaAbsorptionGroup1PerM,
                    e.DeltaAbsorptionGroup2PerM + (xe?.GetDeltaAbsorptionGroup2PerM(e.Node) ?? 0)))));
        }
        FullCoreDiffusionSolveResultV1 Solve(double fill, bool tight, bool noRods = false, SyntheticGameCoreStateV1? inventory = null) =>
            Require(Model(tight, noRods).TrySolve((inventory ?? core).EnumerateBundles(), Overlay(inventory ?? core, fill), 2_064_000_000));
        EquilibriumCoreProjectionV1 Projection(bool tight)
        {
            var solver = Require(EquilibriumCoreSolverV1.TryCreate(Model(tight), core.EnumerateBundles(), 2_064_000_000));
            return Require(solver.TrySolveCandidate(core.EnumerateBundles(), Overlay(core, .5)));
        }
        static double Worth(double kin, double kout) => 1000 * (1 / kin - 1 / kout);
        poison = PracticeXenonStateV1.CreateEquilibrium(core, Projection(false), 0);
        for (int pass = 0; pass < 20; pass++)
        {
            var half = Projection(false);
            double rodWorth = Worth(half.SpatialSolve.EffectiveK, Solve(.5, false, true).EffectiveK);
            rod *= 17 / rodWorth;
            double zoneWorth = Worth(Solve(1, false).EffectiveK, Solve(0, false).EffectiveK);
            water *= 7 / zoneWorth;
            double k = Solve(.5, false).EffectiveK;
            production /= k;
            half = Projection(false);
            var next = PracticeXenonStateV1.CreateEquilibrium(core, half, 0);
            double error = next.Xenon.Select((x, n) => Math.Abs(x - poison.Xenon[n]) / Math.Max(1, x)).Max();
            poison = next;
            double critical = Solve(.5, false).EffectiveK;
            rodWorth = Worth(critical, Solve(.5, false, true).EffectiveK);
            zoneWorth = Worth(Solve(1, false).EffectiveK, Solve(0, false).EffectiveK);
            trials.Add(new { pass, production, rod, water, halfFillK = critical, rodWorthMk = rodWorth, zoneWorthMk = zoneWorth, poisonError = error });
            File.WriteAllText(Path.Combine(directory, "trials.json"), JsonSerializer.Serialize(trials, Pretty));
            Console.WriteLine($"pass={pass} k={critical:F9} rods={rodWorth:F5}mk zones={zoneWorth:F5}mk production={production:R} XeError={error:G3}");
            if (Math.Abs(critical - 1) < 3e-6 && Math.Abs(rodWorth - 17) < .002 && Math.Abs(zoneWorth - 7) < .002 && error < 1e-7) break;
        }
        var tightHalf = Projection(true);
        for (int pass = 0; pass < 12; pass++)
        {
            var next = PracticeXenonStateV1.CreateEquilibrium(core, tightHalf, 0);
            double error = next.Xenon.Select((x, n) => Math.Abs(x - poison.Xenon[n]) / Math.Max(1, x)).Max();
            poison = next; tightHalf = Projection(true);
            if (error < 1e-8) break;
        }
        double finalK = tightHalf.SpatialSolve.EffectiveK;
        double finalRodWorth = Worth(finalK, Solve(.5, true, true).EffectiveK);
        double finalZoneWorth = Worth(Solve(1, true).EffectiveK, Solve(0, true).EffectiveK);
        if (Math.Abs(finalK - 1) > 5e-5 || Math.Abs(finalRodWorth - 17) > .03 || Math.Abs(finalZoneWorth - 7) > .03)
            throw new InvalidOperationException("Independent tight Marshak calibration failed.");
        proposal = Candidate(false);
        string note = $"; axial Marshak v8: zero incoming partial current at End A/B, C=DA/(h/2+2D), h=0.4953 m; radial reflector and interior coupling retained; seed1001 half-fill criticality uses common nu-fission normalization {production:R}; Sigma_f and fission heating, fuel knots and relative production curve retained; rod/water strengths re-fit to 17mk/7mk at frozen equilibrium Xe; authored calibration, not measured group constants";
        proposal["source_provenance"] = source["source_provenance"]!.GetValue<string>() + note;
        proposal["source_toolchain"] = source["source_toolchain"]!.GetValue<string>() + "; tools/AgedCoreBenchmark --fit-axial-marshak";
        foreach (var table in proposal["coefficient_tables"]!.AsArray())
        {
            table!["data_version"] = table["data_version"]!.GetValue<string>() + "-axial-marshak-v8";
            table["source_provenance"] = table["source_provenance"]!.GetValue<string>() + note;
        }
        File.WriteAllText(Path.Combine(directory, "proposal.json"), proposal.ToJsonString(Pretty));
        var burned = Require(core.TryAddFissionEnergy(tightHalf.ShapeNodePowerWatts.Select(p => p * 86400).ToArray()));
        var after = Solve(.5, true, inventory: burned);
        var channelPower = tightHalf.ShapeNodePowerWatts.Skip(200 * 12).Take(12).ToArray();
        File.WriteAllText(Path.Combine(directory, "fit.json"), JsonSerializer.Serialize(new
        {
            sourceVersion = source["data_pack_version"]!.GetValue<string>(),
            seed = 1001,
            halfFillK = finalK,
            measuredAdjusterWorthMk = finalRodWorth,
            measuredZoneWorthMk = finalZoneWorth,
            productionMultiplier = production,
            rodThermalPerM = rod,
            waterFastPerM = water * .1,
            waterThermalPerM = water,
            axialFastBoundaryM2 = tightHalf.DataPack.AxialVacuumBoundaryConductance.Group1M2,
            axialThermalBoundaryM2 = tightHalf.DataPack.AxialVacuumBoundaryConductance.Group2M2,
            frozenXenonFuelLossMkPerFullPowerDay = 1000 * (tightHalf.RelativeReactivity - after.Reactivity),
            peakChannelKw = tightHalf.ShapeChannelPowerWatts.Max() / 1000,
            peakBundleKw = tightHalf.ShapeNodePowerWatts.Max() / 1000,
            m11BundleKw = channelPower.Select(p => p / 1000).ToArray(),
            m11EndAToPeak = channelPower[0] / channelPower.Max(),
            m11EndBToPeak = channelPower[11] / channelPower.Max(),
            trials
        }, Pretty));
        Console.WriteLine($"Verified: k={finalK:F9}, rods={finalRodWorth:F6}mk, zones={finalZoneWorth:F6}mk; {directory}");
    }
    private static T Require<T>(ContractValidationResult<T> result) => result.IsValid ? result.Value : throw new InvalidOperationException(result.FirstDiagnostic.ToString());
}
