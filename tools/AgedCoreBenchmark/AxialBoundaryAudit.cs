using System.Text.Json;
using System.Text.Json.Nodes;
using ReactorSim.Core;
using ReactorSim.Game;

internal static class AxialBoundaryAudit
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };
    public static void Run(string outputPath)
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest(1001);
        var initial = session.CurrentSpatialCandidate.SpatialSolve;
        var inventory = session.CoreState.EnumerateBundles().ToArray();
        var zones = Require(session.CurrentLiquidZoneRrs.TryBuildOverlay());
        var xe = session.CurrentXenonState.Overlay;
        using var stream = typeof(FullCoreDiffusionDataPackV1).Assembly.GetManifestResourceStream(FullCoreDiffusionDataPackV1.EmbeddedResourceName)!;
        using var reader = new StreamReader(stream);
        var source = JsonNode.Parse(reader.ReadToEnd())!.AsObject();
        var solver = source["solver"]!;
        solver["absolute_residual_tolerance"] = 1e-12;
        solver["relative_residual_tolerance"] = 1e-9;
        solver["maximum_inner_iterations"] = 256;
        solver["k_absolute_tolerance"] = 2e-9;
        solver["k_relative_tolerance"] = 2e-9;
        solver["residual_tolerance"] = 2e-7;
        solver["source_shape_tolerance"] = 1e-7;
        solver["maximum_iterations"] = 5000;
        var activePack = Require(FullCoreDiffusionDataPackV1.TryLoadJson(source.ToJsonString()));
        // Use the legacy end-face diagonal as a mathematical baseline so every
        // explored end loss is a positive addition, even when the active pack
        // already uses Marshak. Radial boundaries are identical in both packs.
        source["geometry"]!.AsObject().Remove("axial_boundary");
        var insertedPack = Require(FullCoreDiffusionDataPackV1.TryLoadJson(source.ToJsonString()));
        var removed = source.DeepClone().AsObject(); removed.Remove("adjusters");
        var withdrawnPack = Require(FullCoreDiffusionDataPackV1.TryLoadJson(removed.ToJsonString()));
        double h = PracticeAdjustersV1.BundleLengthM, area = insertedPack.NodeVolumeM3 / h;
        double d1 = insertedPack.AxialConductance.Group1M2 * h / area;
        double d2 = insertedPack.AxialConductance.Group2M2 * h / area;
        double old1 = insertedPack.VacuumBoundaryConductance.Group1M2;
        double old2 = insertedPack.VacuumBoundaryConductance.Group2M2;
        var variants = new[] {
            (Name: "current", D1: d1 * area / activePack.AxialVacuumBoundaryConductance.Group1M2 - h / 2,
                D2: d2 * area / activePack.AxialVacuumBoundaryConductance.Group2M2 - h / 2),
            (Name: "zero-incoming-current", D1: 2 * d1, D2: 2 * d2),
            (Name: "extrapolation-0.25m", D1: .25, D2: .25),
            (Name: "extrapolation-0.50m", D1: .5, D2: .5)
        };
        var results = new List<object>();
        foreach (var variant in variants)
        {
            double c1 = d1 * area / (h / 2 + variant.D1), c2 = d2 * area / (h / 2 + variant.D2);
            var overlay = Require(StaticAbsorptionOverlayV1.TryCreate("offline-axial-leakage-equivalent-diagonal-sink",
                zones.Entries.Select(e =>
                {
                    bool end = e.Node.Position.Value is 0 or 11;
                    return new StaticAbsorptionOverlayEntryV1(e.Node,
                        e.DeltaAbsorptionGroup1PerM + xe.GetDeltaAbsorptionGroup1PerM(e.Node) + (end ? (c1 - old1) / insertedPack.NodeVolumeM3 : 0),
                        e.DeltaAbsorptionGroup2PerM + xe.GetDeltaAbsorptionGroup2PerM(e.Node) + (end ? (c2 - old2) / insertedPack.NodeVolumeM3 : 0));
                })));
            foreach (var mode in new[] { (Name: "in", Pack: insertedPack), (Name: "out", Pack: withdrawnPack) })
            {
                var solved = Require(Require(FullCoreDiffusionModelV1.TryCreateCandu6(mode.Pack)).TrySolve(inventory, overlay,
                    PracticeGameSessionFactory.PracticeReferenceThermalPowerWatts, initial.EffectiveK, initial.Group1Flux, initial.Group2Flux));
                if (Math.Abs(solved.TotalPowerWatts - 2_064_000_000) > .01) throw new InvalidOperationException("Normalization failed.");
                var power = solved.NodePowerWatts.Skip(200 * 12).Take(12).Select(p => p / 1000).ToArray();
                if (power.Any(p => !double.IsFinite(p) || p <= 0)) throw new InvalidOperationException("Invalid power.");
                results.Add(new
                {
                    variant = variant.Name,
                    adjusters = mode.Name,
                    extrapolationFastM = variant.D1,
                    extrapolationThermalM = variant.D2,
                    boundaryFastM2 = c1,
                    boundaryThermalM2 = c2,
                    k = solved.EffectiveK,
                    residual = solved.ResidualRelativeInfinity,
                    iterations = solved.IterationCount,
                    totalCoreWatts = solved.TotalPowerWatts,
                    channelKw = power.Sum(),
                    bundleKw = power,
                    endAToPeak = power[0] / power.Max(),
                    endBToPeak = power[11] / power.Max()
                });
                Console.WriteLine($"{variant.Name}, rods {mode.Name}: k={solved.EffectiveK:F7}; M11 ends/peak={power[0] / power.Max():P1}, {power[11] / power.Max():P1}");
            }
        }
        var report = new
        {
            seed = 1001,
            channel = "M11",
            channelIndex = 200,
            pack = insertedPack.Descriptor.DataPackVersion,
            method = "Offline axial-only leakage sensitivity. C_g=D_g*A/(h/2+delta_g), D_g derived from authored interior conductance. Additional end-face loss enters as an equivalent positive diagonal sink DeltaC_g/V on positions 1 and 12 only. Radial boundaries, all fuel, actual Xe, LZC fills and total 2064 MW thermal remain fixed. All adjusters in/out. No runtime pack change or criticality/device-worth retune.",
            axialCellLengthM = h,
            coreLengthM = 12 * h,
            nodeVolumeM3 = insertedPack.NodeVolumeM3,
            authoredEffectiveFastD_M = d1,
            authoredEffectiveThermalD_M = d2,
            originalBoundaryFastM2 = old1,
            originalBoundaryThermalM2 = old2,
            zoneFills = session.CurrentLiquidZoneRrs.ZoneFills,
            results
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, Pretty));
    }
    private static T Require<T>(ContractValidationResult<T> r) => r.IsValid ? r.Value : throw new InvalidOperationException(r.FirstDiagnostic.ToString());
}
