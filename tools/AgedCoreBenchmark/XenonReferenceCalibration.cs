using System.Text.Json;
using System.Text.Json.Nodes;
using ReactorSim.Core;
using ReactorSim.Game;

// Retains the reference-derived fuel curve. The explicit Xe reference is an
// authored decomposition of that surrogate, not a measured isotope dataset.
internal static class XenonReferenceCalibration
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };
    private const double SourceZone1 = .0017642885157619739;
    private const double SourceZone2 = .0007057154063047896;
    internal static void Run(string sourcePath, string directory)
    {
        Directory.CreateDirectory(directory);
        var source = JsonNode.Parse(File.ReadAllText(sourcePath))!;
        if (source["data_pack_version"]!.GetValue<string>() != "candu6-two-group-diffusion-v1-adjusters-v5")
            throw new ArgumentException("Supply the archived adjusters-v5 pack.", nameof(sourcePath));
        var proposal = source.DeepClone();
        proposal["data_pack_version"] = "candu6-two-group-diffusion-v1-xenon-reference-v6";
        proposal["xenon_basis"] = PracticeXenonReferenceV1.BasisId;
        proposal["xenon_reference"] = BuildReference(source);
        proposal["source_provenance"] = source["source_provenance"]!.GetValue<string>() +
            "; explicit included Xe135 surrogate reference history at 31.9713 kW/kg HM; reference starts at zero, analytic model densities assigned over unchanged fuel knots; dynamic Xe replaces reference at current bundle burnup, no fixed startup rebase; not a measured poison-separated nuclear dataset; fuel curve unchanged";
        proposal["source_toolchain"] = source["source_toolchain"]!.GetValue<string>() + "; tools/AgedCoreBenchmark --fit-xenon-reference";
        var core = SyntheticGameCoreStateV1.CreateAgedPractice(1001);
        var mapping = Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());
        double strength = source["adjusters"]!["inner_absorption_group2_per_m"]!.GetValue<double>();
        double boundary = source["geometry"]!["vacuum_boundary_conductance_m2"]!["group1_m2"]!.GetValue<double>();
        double zoneScale = 1;
        PracticeXenonStateV1? poison = null;
        var trials = new List<object>();
        FullCoreDiffusionModelV1 Model(bool tight)
        {
            var candidate = proposal.DeepClone();
            candidate["adjusters"]!["inner_absorption_group1_per_m"] = strength * .1;
            candidate["adjusters"]!["inner_absorption_group2_per_m"] = strength;
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
        StaticAbsorptionOverlayV1 Overlay(SyntheticGameCoreStateV1 inventory, double fill)
        {
            var xe = poison?.BindBurnupReference(inventory).Overlay;
            return Require(StaticAbsorptionOverlayV1.TryCreate("xenon-reference-fit-fixed-zones",
                mapping.Nodes.Select(n => new StaticAbsorptionOverlayEntryV1(n.Node,
                    SourceZone1 * zoneScale * fill,
                    SourceZone2 * zoneScale * fill + (xe?.GetDeltaAbsorptionGroup2PerM(n.Node) ?? 0)))));
        }
        FullCoreDiffusionSolveResultV1 Solve(double fill, bool tight = false, SyntheticGameCoreStateV1? inventory = null)
        {
            inventory ??= core;
            return Require(Model(tight).TrySolve(inventory.EnumerateBundles(), Overlay(inventory, fill), 2_064_000_000));
        }
        EquilibriumCoreProjectionV1 Projection(bool tight = false)
        {
            var solver = Require(EquilibriumCoreSolverV1.TryCreate(Model(tight), core.EnumerateBundles(), 2_064_000_000));
            return Require(solver.TrySolveCandidate(core.EnumerateBundles(), Overlay(core, .5)));
        }
        static double Worth(double kin, double kout) => 1000 * (1 / kin - 1 / kout);
        poison = PracticeXenonStateV1.CreateEquilibrium(core, Projection(), 0);
        for (int pass = 0; pass < 10; pass++)
        {
            double retained = strength;
            strength = 0; double outK = Solve(.5).EffectiveK;
            double lo = 0, hi = .3;
            for (int i = 0; i < 17; i++)
            {
                strength = (lo + hi) / 2;
                if (Worth(Solve(.5).EffectiveK, outK) < 17) lo = strength; else hi = strength;
            }
            lo = 1e-8; hi = .01;
            for (int i = 0; i < 19; i++)
            {
                boundary = (lo + hi) / 2;
                if (Solve(.5).EffectiveK > 1) lo = boundary; else hi = boundary;
            }
            var half = Projection();
            var next = PracticeXenonStateV1.CreateEquilibrium(core, half, 0);
            double poisonError = next.Xenon.Select((x, n) => Math.Abs(x - poison.Xenon[n]) / Math.Max(1, x)).Max();
            poison = next;
            double inK = Solve(.5).EffectiveK;
            retained = strength; strength = 0; outK = Solve(.5).EffectiveK; strength = retained;
            double rodWorth = Worth(inK, outK);
            double zoneWorth = Worth(Solve(1).EffectiveK, Solve(0).EffectiveK);
            trials.Add(new { pass, strength, boundary, zoneScale, inK, rodWorth, zoneWorth, poisonError });
            Console.WriteLine($"pass={pass} k={inK:F9} rods={rodWorth:F6}mk zones={zoneWorth:F6}mk poison-error={poisonError:G5}");
            if (Math.Abs(inK - 1) < 3e-6 && Math.Abs(rodWorth - 17) < .005 && Math.Abs(zoneWorth - 7) < .005 && poisonError < 1e-6) break;
            zoneScale *= 7 / zoneWorth;
        }
        var finalHalf = Projection(true);
        // Settle equilibrium at fixed half zones before freezing actual Xe for all probes.
        for (int pass = 0; pass < 8; pass++)
        {
            var next = PracticeXenonStateV1.CreateEquilibrium(core, finalHalf, 0);
            double error = next.Xenon.Select((x, n) => Math.Abs(x - poison.Xenon[n]) / Math.Max(1, x)).Max();
            poison = next; finalHalf = Projection(true);
            if (error < 1e-8) break;
        }
        var empty = Solve(0, true); var full = Solve(1, true);
        double finalStrength = strength; strength = 0; var allOut = Solve(.5, true); strength = finalStrength;
        double measuredRodWorth = Worth(finalHalf.EffectiveK, allOut.EffectiveK);
        double measuredZoneWorth = Worth(full.EffectiveK, empty.EffectiveK);
        var burned = Require(core.TryAddFissionEnergy(finalHalf.ShapeNodePowerWatts.Select(p => p * 86400).ToArray()));
        var after = Solve(.5, true, burned);
        double fuelLoss = 1000 * (finalHalf.RelativeReactivity - (after.EffectiveK - 1) / after.EffectiveK);
        if (Math.Abs(finalHalf.EffectiveK - 1) > 5e-5 || Math.Abs(measuredRodWorth - 17) > .03 || Math.Abs(measuredZoneWorth - 7) > .03)
            throw new InvalidOperationException("Tight equilibrium/worth calibration failed.");
        proposal["adjusters"]!["inner_absorption_group1_per_m"] = strength * .1;
        proposal["adjusters"]!["inner_absorption_group2_per_m"] = strength;
        proposal["geometry"]!["vacuum_boundary_conductance_m2"]!["group1_m2"] = boundary;
        proposal["geometry"]!["vacuum_boundary_conductance_m2"]!["group2_m2"] = boundary / 2;
        proposal["source_provenance"] = proposal["source_provenance"]!.GetValue<string>() +
            "; seed1001 frozen-Xe 17mk adjuster/7mk zone/half-fill criticality calibration through device strength and leakage only; fuel decay measured, not fitted to an arbitrary rate";
        Write(Path.Combine(directory, "proposal.json"), proposal.ToJsonString(Pretty));
        Write(Path.Combine(directory, "fit.json"), JsonSerializer.Serialize(new
        {
            sourcePack = source["data_pack_version"]!.GetValue<string>(),
            seed = 1001,
            targetAdjusterWorthMk = 17,
            measuredAdjusterWorthMk = measuredRodWorth,
            measuredZoneWorthMk = measuredZoneWorth,
            halfFillK = finalHalf.EffectiveK,
            frozenXenonFuelLossMkPerFullPowerDay = fuelLoss,
            afterOneFullPowerDayK = after.EffectiveK,
            allOutK = allOut.EffectiveK,
            strength,
            boundary,
            zoneScale,
            group1ZoneSlope = SourceZone1 * zoneScale,
            group2ZoneSlope = SourceZone2 * zoneScale,
            peakChannelKw = finalHalf.ShapeChannelPowerWatts.Max() / 1000,
            peakBundleKw = finalHalf.ShapeNodePowerWatts.Max() / 1000,
            fuelCurveUnchanged = true,
            method = "Identical aged fuel, fixed half zones, self-consistent equilibrium Xe at startup. Actual Xe stays frozen in worth and one-day fuel-only probes; included reference follows current burnup. No arbitrary decay-rate fit.",
            reference = proposal["xenon_reference"],
            trials
        }, Pretty));
        Console.WriteLine($"Tight rods={measuredRodWorth:F6}mk zones={measuredZoneWorth:F6}mk fuel-only={fuelLoss:F6}mk/FPD");
    }

    private static JsonObject BuildReference(JsonNode source)
    {
        const double specificPower = 31_971.3;
        double volume = source["geometry"]!["node_volume_m3"]!.GetValue<double>();
        var tableRows = source["coefficient_tables"]![0]!["rows"]!.AsArray();
        var data = new JsonArray();
        (double Iodine, double Xenon) state = (0, 0);
        double previous = 0;
        double f = specificPower * SyntheticGameCoreStateV1.DefaultHeavyMetalMassKg /
            (volume * tableRows[0]!["energy_per_fission_j"]!.GetValue<double>());
        var sourcePack = Require(FullCoreDiffusionDataPackV1.TryLoadJson(source.ToJsonString()));
        var table = sourcePack.CoefficientTables[0];
        foreach (var row in tableRows)
        {
            double burnup = row!["burnup_j_per_kg_hm"]!.GetValue<double>();
            double duration = (burnup - previous) / specificPower;
            // Reference history alone is integrated at <= six-hour intervals.
            int steps = Math.Max(1, (int)Math.Ceiling(duration / 21_600));
            for (int n = 0; n < steps && duration > 0; n++)
            {
                double b = previous + (burnup - previous) * (n + .5) / steps;
                var c = Require(table.TryLookup(b)).Coefficients;
                double ratio = c.DownscatterGroup1To2PerM / c.AbsorptionGroup2PerM;
                double phi1 = f / (c.FissionGroup1PerM + ratio * c.FissionGroup2PerM);
                state = PracticeXenonDataV1.Advance(state.Iodine, state.Xenon, f, phi1, ratio * phi1, duration / steps);
            }
            data.Add(new JsonObject { ["burnup_j_per_kg_hm"] = burnup, ["xe135_number_density_m3"] = state.Xenon });
            previous = burnup;
        }
        return new JsonObject
        {
            ["model_id"] = PracticeXenonReferenceV1.Identity,
            ["poison_data_identity"] = PracticeXenonDataV1.Identity,
            ["reference_specific_power_w_per_kg_hm"] = specificPower,
            ["source_provenance"] = "Authored analytic Xe135 reference proxy at Naceur/Marleau reference specific power; current game mass, energy and softened cross section; not extracted source isotope concentrations",
            ["rows"] = data
        };
    }
    private static void Write(string path, string json) => File.WriteAllText(path,
        json.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal));
    private static T Require<T>(ContractValidationResult<T> result) => result.IsValid ? result.Value :
        throw new InvalidOperationException(result.FirstDiagnostic.ToString());
}
