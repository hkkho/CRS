using System.Text.Json;
using System.Text.Json.Nodes;
using ReactorSim.Core;

internal static class LiquidZoneTubeCalibration
{
    internal static void Run(string sourcePath, string directory)
    {
        Directory.CreateDirectory(directory);
        var source = JsonNode.Parse(File.ReadAllText(sourcePath))!;
        if (source["data_pack_version"]!.GetValue<string>() != "candu6-two-group-diffusion-v1-xenon-reference-v6")
            throw new ArgumentException("Supply the archived xenon-reference-v6 pack.", nameof(sourcePath));
        var proposal = source.DeepClone();
        proposal["data_pack_version"] = "candu6-two-group-diffusion-v1-lzc-tubes-v7";
        var core = SyntheticGameCoreStateV1.CreateAgedPractice(1001);
        double rod = source["adjusters"]!["inner_absorption_group2_per_m"]!.GetValue<double>();
        double boundary = source["geometry"]!["vacuum_boundary_conductance_m2"]!["group1_m2"]!.GetValue<double>();
        double water = .1;
        PracticeXenonStateV1? poison = null;
        var trials = new List<object>();
        FullCoreDiffusionModelV1 Model(bool tight = false, bool noRods = false)
        {
            var candidate = proposal.DeepClone();
            candidate["adjusters"]!["inner_absorption_group1_per_m"] = noRods ? 0 : rod * .1;
            candidate["adjusters"]!["inner_absorption_group2_per_m"] = noRods ? 0 : rod;
            candidate["geometry"]!["vacuum_boundary_conductance_m2"]!["group1_m2"] = boundary;
            candidate["geometry"]!["vacuum_boundary_conductance_m2"]!["group2_m2"] = boundary / 2;
            if (tight)
            {
                candidate["solver"]!["maximum_iterations"] = 5000;
                candidate["solver"]!["maximum_inner_iterations"] = 256;
                candidate["solver"]!["k_absolute_tolerance"] = 2e-9;
                candidate["solver"]!["k_relative_tolerance"] = 2e-9;
                candidate["solver"]!["residual_tolerance"] = 2e-7;
                candidate["solver"]!["source_shape_tolerance"] = 1e-7;
            }
            return Require(FullCoreDiffusionModelV1.TryCreateCandu6(Require(
                FullCoreDiffusionDataPackV1.TryLoadJson(candidate.ToJsonString()))));
        }
        StaticAbsorptionOverlayV1 Overlay(double fill)
        {
            var mapping = Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6(water * .1, water));
            var zones = Require(mapping.TryBuildOverlay(Enumerable.Repeat(fill, 14).ToArray()));
            var xe = poison?.BindBurnupReference(core).Overlay;
            return Require(StaticAbsorptionOverlayV1.TryCreate("tube-fit-frozen-xe",
                zones.Entries.Select(e => new StaticAbsorptionOverlayEntryV1(e.Node, e.DeltaAbsorptionGroup1PerM,
                    e.DeltaAbsorptionGroup2PerM + (xe?.GetDeltaAbsorptionGroup2PerM(e.Node) ?? 0)))));
        }
        FullCoreDiffusionSolveResultV1 Solve(double fill, bool tight = false, bool noRods = false) =>
            Require(Model(tight, noRods).TrySolve(core.EnumerateBundles(), Overlay(fill), 2_064_000_000));
        EquilibriumCoreProjectionV1 Projection(bool tight = false)
        {
            var solver = Require(EquilibriumCoreSolverV1.TryCreate(Model(tight), core.EnumerateBundles(), 2_064_000_000));
            return Require(solver.TrySolveCandidate(core.EnumerateBundles(), Overlay(.5)));
        }
        static double Worth(double kin, double kout) => 1000 * (1 / kin - 1 / kout);
        poison = PracticeXenonStateV1.CreateEquilibrium(core, Projection(), 0);
        for (int pass = 0; pass < 24; pass++)
        {
            // Coordinate secants: only absorber strength and boundary leakage change.
            // Re-equilibrate actual Xe between passes; freeze it for all worth probes.
            double rodWorth = Worth(Solve(.5).EffectiveK, Solve(.5, noRods: true).EffectiveK);
            rod *= 17 / rodWorth;
            double zoneWorth = Worth(Solve(1).EffectiveK, Solve(0).EffectiveK);
            water *= 7 / zoneWorth;
            for (int i = 0; i < 4; i++)
            {
                double k = Solve(.5).EffectiveK, original = boundary;
                if (Math.Abs(k - 1) < 2e-7) break;
                double delta = Math.Max(1e-7, original * .01);
                boundary += delta;
                double slope = (Solve(.5).EffectiveK - k) / delta;
                boundary = Math.Clamp(original + (1 - k) / slope, 1e-8, .01);
            }
            var half = Projection();
            var next = PracticeXenonStateV1.CreateEquilibrium(core, half, 0);
            double error = next.Xenon.Select((x, n) => Math.Abs(x - poison.Xenon[n]) / Math.Max(1, x)).Max();
            poison = next;
            rodWorth = Worth(Solve(.5).EffectiveK, Solve(.5, noRods: true).EffectiveK);
            zoneWorth = Worth(Solve(1).EffectiveK, Solve(0).EffectiveK);
            double critical = Solve(.5).EffectiveK;
            trials.Add(new { pass, rod, water, boundary, k = critical, rodWorth, zoneWorth, poisonError = error });
            Console.WriteLine($"pass={pass} k={critical:F9} rods={rodWorth:F6}mk zones={zoneWorth:F6}mk water={water:R} poison={error:G5}");
            if (Math.Abs(critical - 1) < 3e-6 && Math.Abs(rodWorth - 17) < .002 && Math.Abs(zoneWorth - 7) < .002 && error < 1e-7) break;
        }
        var tightHalf = Projection(true);
        for (int pass = 0; pass < 12; pass++)
        {
            var next = PracticeXenonStateV1.CreateEquilibrium(core, tightHalf, 0);
            double error = next.Xenon.Select((x, n) => Math.Abs(x - poison.Xenon[n]) / Math.Max(1, x)).Max();
            poison = next;
            tightHalf = Projection(true);
            if (error < 1e-8) break;
        }
        var full = Solve(1, true); var empty = Solve(0, true); var outRods = Solve(.5, true, true);
        double finalRodWorth = Worth(tightHalf.EffectiveK, outRods.EffectiveK);
        double finalZoneWorth = Worth(full.EffectiveK, empty.EffectiveK);
        if (Math.Abs(tightHalf.EffectiveK - 1) > 5e-5 || Math.Abs(finalRodWorth - 17) > .03 || Math.Abs(finalZoneWorth - 7) > .03)
            throw new InvalidOperationException("Independent tight tube calibration failed.");
        proposal["adjusters"]!["inner_absorption_group1_per_m"] = rod * .1;
        proposal["adjusters"]!["inner_absorption_group2_per_m"] = rod;
        proposal["geometry"]!["vacuum_boundary_conductance_m2"]!["group1_m2"] = boundary;
        proposal["geometry"]!["vacuum_boundary_conductance_m2"]!["group2_m2"] = boundary / 2;
        proposal["source_provenance"] = source["source_provenance"]!.GetValue<string>() +
            "; localized LZC tubes v7: St-Aubin/Marleau 2018 Figs1-2 layout; one-pitch by one-bundle homogenization; authored vertical endpoints rounded to lattice boundaries; bottom-up water cell overlap; authored fast/thermal absorption ratio 0.1; 7mk total water and 17mk rods re-fit at frozen equilibrium Xe with seed1001 half-fill critical; fuel knots, interior coupling and Xe reference preserved";
        proposal["source_toolchain"] = source["source_toolchain"]!.GetValue<string>() + "; tools/AgedCoreBenchmark --fit-lzc-tubes";
        var pretty = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(Path.Combine(directory, "proposal.json"), proposal.ToJsonString(pretty));
        File.WriteAllText(Path.Combine(directory, "fit.json"), JsonSerializer.Serialize(new
        {
            seed = 1001, sourceVersion = source["data_pack_version"]!.GetValue<string>(),
            layout = PracticeLiquidZoneTubesV1.LayoutId, halfFillK = tightHalf.EffectiveK,
            measuredAdjusterWorthMk = finalRodWorth, measuredZoneWorthMk = finalZoneWorth,
            group1ZoneStrength = water * .1, group2ZoneStrength = water, rod, boundary,
            peakChannelKw = tightHalf.ShapeChannelPowerWatts.Max() / 1000,
            peakBundleKw = tightHalf.ShapeNodePowerWatts.Max() / 1000,
            compartments = PracticeLiquidZoneTubesV1.Compartments, trials
        }, pretty));
        Console.WriteLine($"Verified tubes: k={tightHalf.EffectiveK:F9}, rods={finalRodWorth:F6}mk, zones={finalZoneWorth:F6}mk");
    }
    private static T Require<T>(ContractValidationResult<T> result) => result.IsValid ? result.Value :
        throw new InvalidOperationException(result.FirstDiagnostic.ToString());
}
