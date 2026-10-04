using System.Text.Json;
using System.Text.Json.Nodes;
using ReactorSim.Core;
using ReactorSim.Game;

internal static class PowerBalanceCalibration
{
    private static readonly JsonSerializerOptions PrettyJson = new() { WriteIndented = true };
    internal static void Run(string directory, string sourcePackPath)
    {
        Directory.CreateDirectory(directory);
        var source = JsonNode.Parse(File.ReadAllText(sourcePackPath))!;
        if (source["data_pack_version"]!.GetValue<string>() != "candu6-two-group-diffusion-v1-cycle190-650mwe-innerrel1e7")
            throw new InvalidOperationException("Supply the archived pre-limit source pack to reproduce the calibration.");
        var results = new List<object>();
        foreach (var (coupling, boundary) in new[] { (4.0, .1), (8.0, .1), (16.0, .1), (8.0, .02) })
        {
            var pack = source.DeepClone();
            foreach (string edge in new[] { "axial_edge_conductance_m2", "transverse_edge_conductance_m2", "vacuum_boundary_conductance_m2" })
                foreach (string group in new[] { "group1_m2", "group2_m2" })
                    pack["geometry"]![edge]![group] = source["geometry"]![edge]![group]!.GetValue<double>() * (edge.StartsWith("vacuum", StringComparison.Ordinal) ? boundary : coupling);
            var model = Model(pack);
            var initial = SyntheticGameCoreStateV1.CreateAgedPractice(1001);
            var solver = Require(EquilibriumCoreSolverV1.TryCreate(model, initial.EnumerateBundles(), Power));
            var half = Require(solver.TrySolveCandidate(initial.EnumerateBundles(), Overlay(.5)));
            double multiplier = 1 / half.SpatialSolve.EffectiveK;
            foreach (var row in pack["coefficient_tables"]![0]!["rows"]!.AsArray())
                foreach (int group in new[] { 1, 2 })
                {
                    string field = $"nu_fission_group{group}_per_m";
                    row![field] = row[field]!.GetValue<double>() * multiplier;
                }
            var peaks = new List<object>();
            model = Model(pack);
            foreach (ulong seed in new ulong[] { 1001, 1002, 1013, 1042, 1100, 2026 })
            {
                initial = SyntheticGameCoreStateV1.CreateAgedPractice(seed);
                solver = Require(EquilibriumCoreSolverV1.TryCreate(model, initial.EnumerateBundles(), Power));
                var projection = Require(solver.TrySolveCandidate(initial.EnumerateBundles(), Overlay(.5)));
                peaks.Add(new { seed, channelKw = projection.ShapeChannelPowerWatts.Max() / 1000, bundleKw = projection.ShapeNodePowerWatts.Max() / 1000, rhoMk = projection.RelativeReactivity * 1000 });
            }
            var reference = PracticeChannelPowerReference.Create(Require(FullCoreDiffusionDataPackV1.TryLoadJson(pack.ToJsonString())), Power);
            var result = new { coupling, boundary, multiplier, referencePeakKw = reference.ChannelPowerWatts.Max() / 1000, peaks };
            results.Add(result);
            Console.WriteLine(JsonSerializer.Serialize(result));
            File.WriteAllText(Path.Combine(directory, $"pack-{coupling}-{boundary}.json"), pack.ToJsonString(PrettyJson));
        }
        File.WriteAllText(Path.Combine(directory, "trials.json"), JsonSerializer.Serialize(results, PrettyJson));
    }
    private static double Power => PracticeGameSessionFactory.PracticeReferencePowerWatts;
    private static FullCoreDiffusionModelV1 Model(JsonNode pack) => Require(FullCoreDiffusionModelV1.TryCreateCandu6(Require(FullCoreDiffusionDataPackV1.TryLoadJson(pack.ToJsonString()))));
    private static StaticAbsorptionOverlayV1 Overlay(double fill) => Require(StaticAbsorptionOverlayV1.TryCreate("power-balance-calibration", Require(PracticeLiquidZoneRrsMappingV1.TryCreateCandu6()).Nodes.Select(n => new StaticAbsorptionOverlayEntryV1(n.Node, n.Group1AbsorptionPerMPerFillFraction * fill, n.Group2AbsorptionPerMPerFillFraction * fill))));
    private static T Require<T>(ContractValidationResult<T> result) => result.IsValid ? result.Value : throw new InvalidOperationException(result.FirstDiagnostic.ToString());
}
