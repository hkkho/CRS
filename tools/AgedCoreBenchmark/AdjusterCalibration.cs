using System.Text.Json;
using System.Text.Json.Nodes;
using ReactorSim.Core;
using ReactorSim.Game;

// Offline two-endpoint fit on identical inventory, frozen xenon and fixed zones.
// Retains fuel coefficients and interior coupling; restores startup by leakage only.
internal static class AdjusterCalibration
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };
    internal static void Run(string sourcePath, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var source = JsonNode.Parse(File.ReadAllText(sourcePath))!;
        if (source["data_pack_version"]!.GetValue<string>() != "candu6-two-group-diffusion-v1-literature-geometry-v4")
            throw new ArgumentException("Supply the archived v4 source pack.", nameof(sourcePath));
        // Frozen v4 zone slopes make this fit reproducible after v5 is staged.
        const double sourceZoneSlope1 = .0017635947814886662;
        const double sourceZoneSlope2 = .0007054379125954665;

        var proposal = source.DeepClone();
        proposal["data_pack_version"] = "candu6-two-group-diffusion-v1-adjusters-v5";
        proposal["adjusters"] = new JsonObject
        {
            ["layout_id"] = PracticeAdjustersV1.LayoutId,
            ["inner_absorption_group1_per_m"] = 0.0,
            ["inner_absorption_group2_per_m"] = 0.0
        };
        var inventory = SyntheticGameCoreStateV1.CreateAgedPractice(1001);
        var mapping = Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());
        double boundary = source["geometry"]!["vacuum_boundary_conductance_m2"]!["group1_m2"]!.GetValue<double>();
        double strength = .04, zoneScale = 1;
        var trials = new List<object>();
        FullCoreDiffusionSolveResultV1 Solve(double rodStrength, double fill, bool tight = false)
        {
            var packJson = proposal.DeepClone();
            packJson["adjusters"]!["inner_absorption_group1_per_m"] = rodStrength * .1;
            packJson["adjusters"]!["inner_absorption_group2_per_m"] = rodStrength;
            packJson["geometry"]!["vacuum_boundary_conductance_m2"]!["group1_m2"] = boundary;
            packJson["geometry"]!["vacuum_boundary_conductance_m2"]!["group2_m2"] = boundary / 2;
            if (tight)
            {
                packJson["solver"]!["maximum_iterations"] = 5000;
                packJson["solver"]!["maximum_inner_iterations"] = 256;
                packJson["solver"]!["k_absolute_tolerance"] = 2e-9;
                packJson["solver"]!["k_relative_tolerance"] = 2e-9;
                packJson["solver"]!["residual_tolerance"] = 2e-7;
                packJson["solver"]!["source_shape_tolerance"] = 1e-7;
            }
            var pack = Require(FullCoreDiffusionDataPackV1.TryLoadJson(packJson.ToJsonString()));
            var model = Require(FullCoreDiffusionModelV1.TryCreateCandu6(pack));
            var overlay = Require(StaticAbsorptionOverlayV1.TryCreate("adjuster-fit-fixed-zones",
                mapping.Nodes.Select(n => new StaticAbsorptionOverlayEntryV1(n.Node,
                    sourceZoneSlope1 * fill * zoneScale,
                    sourceZoneSlope2 * fill * zoneScale))));
            return Require(model.TrySolve(inventory.EnumerateBundles(), overlay, 2_064_000_000));
        }
        double Worth(double kin, double kout) => 1000 * (1 / kin - 1 / kout);
        for (int pass = 0; pass < 8; pass++)
        {
            double outK = Solve(0, .5).EffectiveK;
            double lo = 0, hi = .3;
            if (Worth(Solve(hi, .5).EffectiveK, outK) < 17) throw new InvalidOperationException("Adjuster fit not bracketed.");
            for (int i = 0; i < 17; i++)
            {
                strength = (lo + hi) / 2;
                double worth = Worth(Solve(strength, .5).EffectiveK, outK);
                if (worth < 17) lo = strength; else hi = strength;
            }
            lo = 1e-8; hi = .01;
            boundary = lo;
            if (Solve(strength, .5).EffectiveK < 1) throw new InvalidOperationException("Cannot recover startup through leakage with fuel curve fixed.");
            boundary = hi;
            if (Solve(strength, .5).EffectiveK > 1) throw new InvalidOperationException("Leakage fit not bracketed.");
            for (int i = 0; i < 19; i++)
            {
                boundary = (lo + hi) / 2;
                double k = Solve(strength, .5).EffectiveK;
                if (k > 1) lo = boundary; else hi = boundary;
            }
            var half = Solve(strength, .5);
            outK = Solve(0, .5).EffectiveK;
            double rodWorth = Worth(half.EffectiveK, outK);
            double zoneWorth = Worth(Solve(strength, 1).EffectiveK, Solve(strength, 0).EffectiveK);
            trials.Add(new { pass, strength, boundary, zoneScale, k = half.EffectiveK, rodWorth, zoneWorth });
            Console.WriteLine($"pass={pass} strength={strength:R} boundary={boundary:R} k={half.EffectiveK:F8} adjusters={rodWorth:F6}mk zones={zoneWorth:F6}mk");
            if (Math.Abs(rodWorth - 17) < .005 && Math.Abs(zoneWorth - 7) < .005) break;
            zoneScale *= 7 / zoneWorth;
        }
        var tightHalf = Solve(strength, .5, true);
        var tightOut = Solve(0, .5, true);
        double measuredWorth = Worth(tightHalf.EffectiveK, tightOut.EffectiveK);
        double measuredZoneWorth = Worth(Solve(strength, 1, true).EffectiveK, Solve(strength, 0, true).EffectiveK);
        if (Math.Abs(tightHalf.EffectiveK - 1) > 5e-5 || Math.Abs(measuredWorth - 17) > .03 || Math.Abs(measuredZoneWorth - 7) > .03)
            throw new InvalidOperationException("Independent tight fit checks failed.");
        proposal["adjusters"]!["inner_absorption_group1_per_m"] = strength * .1;
        proposal["adjusters"]!["inner_absorption_group2_per_m"] = strength;
        proposal["geometry"]!["vacuum_boundary_conductance_m2"]!["group1_m2"] = boundary;
        proposal["geometry"]!["vacuum_boundary_conductance_m2"]!["group2_m2"] = boundary / 2;
        proposal["source_provenance"] = source["source_provenance"]!.GetValue<string>() +
            "; fixed 21 fully inserted adjusters: IAEA-TECDOC-1994 p20 geometry; one-pitch by one-bundle homogenized columns, overlap-weighted; two steel-area segments; authored absorption fast/thermal ratio 0.1; user-requested 17 mk worth fit, not source-validated device strength; seed1001 half-fill leakage refit retaining lattice curve and interior coupling";
        proposal["source_toolchain"] = source["source_toolchain"]!.GetValue<string>() + "; tools/AgedCoreBenchmark --fit-adjusters";
        File.WriteAllText(Path.Combine(outputDirectory, "proposal.json"), proposal.ToJsonString(Pretty)
            .Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(outputDirectory, "fit.json"), JsonSerializer.Serialize(new
        {
            seed = 1001,
            targetWorthMk = 17,
            measuredWorthMk = measuredWorth,
            measuredZoneWorthMk = measuredZoneWorth,
            halfFillK = tightHalf.EffectiveK,
            allOutK = tightOut.EffectiveK,
            strength,
            boundary,
            zoneScale,
            group1ZoneSlope = sourceZoneSlope1 * zoneScale,
            group2ZoneSlope = sourceZoneSlope2 * zoneScale,
            peakChannelKw = tightHalf.NodePowerWatts.Chunk(12).Max(c => c.Sum()) / 1000,
            peakBundleKw = tightHalf.NodePowerWatts.Max() / 1000,
            rods = PracticeAdjustersV1.Rods,
            cells = PracticeAdjustersV1.Cells.Select(c => new
            {
                c.RodId,
                channel = c.Node.ChannelId.Value,
                axial = c.Node.Position.Value,
                c.VolumeFraction,
                c.AbsorptionWeight
            }),
            trials
        }, Pretty));
        Console.WriteLine($"Tight: {measuredWorth:F6}mk adjusters, {measuredZoneWorth:F6}mk zones, k={tightHalf.EffectiveK:F9}");
    }
    private static T Require<T>(ContractValidationResult<T> result) => result.IsValid ? result.Value :
        throw new InvalidOperationException(result.FirstDiagnostic.ToString());
}
