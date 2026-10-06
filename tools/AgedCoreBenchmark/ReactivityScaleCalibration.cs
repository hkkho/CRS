using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using ReactorSim.Core;
using ReactorSim.Game;

internal static class ReactivityScaleCalibration
{
    // Archived powerlimits-v2 zone slopes make this fit reproducible after
    // the calibrated runtime constants have been updated.
    private const double SourceGroup1Slope = 0.0015964146304331297;
    private const double SourceGroup2Slope = 0.0006385658521732518;
    internal static void Run(string sourcePath, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        var source = JsonNode.Parse(File.ReadAllText(sourcePath))!;
        if (source["data_pack_version"]!.GetValue<string>() != "candu6-two-group-diffusion-v1-cycle190-650mwe-powerlimits-v2")
            throw new InvalidOperationException("Use the archived powerlimits-v2 source pack for this fit.");
        File.WriteAllText(Path.Combine(outputDirectory, "source-pack.json"), source.ToJsonString());
        var initial = SyntheticGameCoreStateV1.CreateAgedPractice(1001);
        var mapping = Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());
        double contrast = 1, zoneScale = 1;
        var trials = new List<object>();
        for (int pass = 0; pass < 8; pass++)
        {
            var proposal = source.DeepClone();
            var original = source["coefficient_tables"]![0]!["rows"]!.AsArray();
            var rows = proposal["coefficient_tables"]![0]!["rows"]!.AsArray();
            for (int i = 0; i < rows.Count; i++)
                foreach (int group in new[] { 1, 2 })
                {
                    string fission = $"fission_group{group}_per_m", production = $"nu_fission_group{group}_per_m";
                    double fresh = original[0]![fission]!.GetValue<double>();
                    double old = original[i]![fission]!.GetValue<double>();
                    // Positive at every retained burnup knot, including the tail.
                    double factor = Math.Pow(old / fresh, contrast - 1);
                    rows[i]![fission] = old * factor;
                    rows[i]![production] = original[i]![production]!.GetValue<double>() * factor;
                }
            var solver = Require(EquilibriumCoreSolverV1.TryCreate(Model(proposal), initial.EnumerateBundles(),
                PracticeGameSessionFactory.PracticeReferenceThermalPowerWatts));
            StaticAbsorptionOverlayV1 Overlay(double fill) => Require(StaticAbsorptionOverlayV1.TryCreate(
                "offline-reactivity-scale-fit", mapping.Nodes.Select(n => new StaticAbsorptionOverlayEntryV1(n.Node,
                    SourceGroup1Slope * fill * zoneScale,
                    SourceGroup2Slope * fill * zoneScale))));
            var half = Require(solver.TrySolveCandidate(initial.EnumerateBundles(), Overlay(.5)));
            var empty = Require(solver.TrySolveCandidate(initial.EnumerateBundles(), Overlay(0)));
            var full = Require(solver.TrySolveCandidate(initial.EnumerateBundles(), Overlay(1)));
            var burned = Require(initial.TryAddFissionEnergy(half.ShapeNodePowerWatts.Select(p => p * 86400).ToArray()));
            var after = Require(solver.TrySolveCandidate(burned.EnumerateBundles(), Overlay(.5)));
            double halfK = half.SpatialSolve.EffectiveK;
            double worth = 1000 * halfK * (1 / full.SpatialSolve.EffectiveK - 1 / empty.SpatialSolve.EffectiveK);
            double loss = 1000 * (halfK / after.SpatialSolve.EffectiveK - 1);
            trials.Add(new { pass, contrast, zoneScale, normalizedZoneWorthMk = worth, normalizedBurnupLossMkPerFpd = loss });
            Console.WriteLine($"Pass {pass}: burnup={loss:F6} mk/FPD, zones={worth:F6} mk, contrast={contrast:R}, zone scale={zoneScale:R}");
            if (Math.Abs(loss - .5) < .002 && Math.Abs(worth - 7) < .002)
            {
                foreach (var row in rows)
                    foreach (int group in new[] { 1, 2 })
                    {
                        string field = $"nu_fission_group{group}_per_m";
                        row![field] = row[field]!.GetValue<double>() / halfK;
                    }
                proposal["data_pack_version"] = "candu6-two-group-diffusion-v1-cycle190-650mwe-reactivity-v3";
                proposal["source_provenance"] = source["source_provenance"]!.GetValue<string>() +
                    $"; reactivity-scale v3: positive burnup contrast exponent {contrast:R}, seed-1001 half-fill production normalization {1 / halfK:R}; frozen-shape first-day burnup loss fitted to 0.5 mk per FPD and homogenized total zone worth to 7 mk; project-authored calibration";
                foreach (var table in proposal["coefficient_tables"]!.AsArray())
                {
                    table!.AsObject().Remove("checksum");
                    table["checksum"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(CanonicalJson(table)))).ToLowerInvariant();
                }
                var pretty = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(Path.Combine(outputDirectory, "proposal.json"), proposal.ToJsonString(pretty));
                File.WriteAllText(Path.Combine(outputDirectory, "fit.json"), JsonSerializer.Serialize(new
                {
                    sourcePack = source["data_pack_version"]!.GetValue<string>(),
                    targetBurnupLossMkPerFpd = .5,
                    targetTotalZoneWorthMk = 7,
                    group1AbsorptionPerMPerFillFraction = SourceGroup1Slope * zoneScale,
                    group2AbsorptionPerMPerFillFraction = SourceGroup2Slope * zoneScale,
                    method = "Seed-1001 aged inventory; static tight solves, no evolving poison or refuelling. Fixed half-fill initial shape deposits one full-power day of thermal fission energy. Uniform production normalization makes half fill critical. Geometry, burnup knots, masses and group ordering preserved.",
                    trials,
                }, pretty));
                return;
            }
            contrast *= .5 / loss;
            zoneScale *= 7 / worth;
            if (!double.IsFinite(contrast) || contrast <= 0 || contrast > 5 || zoneScale <= 0 || zoneScale > 2)
                throw new InvalidOperationException("Reactivity fit left the bounded calibration domain.");
        }
        throw new InvalidOperationException("Reactivity fit did not converge; no proposal was accepted.");
    }

    private static string CanonicalJson(JsonNode? node) => node switch
    {
        JsonObject value => "{" + string.Join(",", value.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => JsonSerializer.Serialize(pair.Key) + ":" + CanonicalJson(pair.Value))) + "}",
        JsonArray value => "[" + string.Join(",", value.Select(CanonicalJson)) + "]",
        null => "null",
        _ => node.GetValueKind() == JsonValueKind.Number ? node.ToJsonString().Replace('E', 'e') : node.ToJsonString(),
    };

    private static FullCoreDiffusionModelV1 Model(JsonNode proposal)
    {
        var json = proposal.DeepClone(); var options = json["solver"]!;
        options["k_absolute_tolerance"] = 1e-8; options["k_relative_tolerance"] = 1e-7;
        options["residual_tolerance"] = 2e-6; options["source_shape_tolerance"] = 1e-6;
        options["absolute_residual_tolerance"] = 1e-12; options["relative_residual_tolerance"] = 1e-9;
        options["maximum_inner_iterations"] = 256; options["maximum_iterations"] = 4000;
        return Require(FullCoreDiffusionModelV1.TryCreateCandu6(Require(FullCoreDiffusionDataPackV1.TryLoadJson(json.ToJsonString()))));
    }
    private static T Require<T>(ContractValidationResult<T> result) => result.IsValid
        ? result.Value : throw new InvalidOperationException(result.FirstDiagnostic.ToString());
}
