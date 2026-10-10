using System.Text.Json;
using System.Text.Json.Nodes;
using ReactorSim.Core;
using ReactorSim.Game;

internal static class BundlePowerAdjusterAudit
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    public static void Run(string outputPath, ulong seed, uint channel)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(channel, Candu6CoreTopologyFactoryV1.ChannelCount);
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest(seed);
        var original = session.CurrentSpatialCandidate.SpatialSolve;
        var inventory = session.CoreState.EnumerateBundles().ToArray();
        var zones = Require(session.CurrentLiquidZoneRrs.TryBuildOverlay());
        var xenon = session.CurrentXenonState.Overlay;
        var background = Require(StaticAbsorptionOverlayV1.TryCreate("fixed-xenon-lzc-adjuster-power-audit",
            zones.Entries.Select(e => new StaticAbsorptionOverlayEntryV1(e.Node,
                e.DeltaAbsorptionGroup1PerM + xenon.GetDeltaAbsorptionGroup1PerM(e.Node),
                e.DeltaAbsorptionGroup2PerM + xenon.GetDeltaAbsorptionGroup2PerM(e.Node)))));
        using var stream = typeof(FullCoreDiffusionDataPackV1).Assembly.GetManifestResourceStream(FullCoreDiffusionDataPackV1.EmbeddedResourceName)!;
        using var reader = new StreamReader(stream);
        var source = JsonNode.Parse(reader.ReadToEnd())!.AsObject();
        var settings = source["solver"]!;
        settings["absolute_residual_tolerance"] = 1e-12;
        settings["relative_residual_tolerance"] = 1e-9;
        settings["maximum_inner_iterations"] = 256;
        settings["k_absolute_tolerance"] = 2e-9;
        settings["k_relative_tolerance"] = 2e-9;
        settings["residual_tolerance"] = 2e-7;
        settings["source_shape_tolerance"] = 1e-7;
        settings["maximum_iterations"] = 5000;
        var inPack = Require(FullCoreDiffusionDataPackV1.TryLoadJson(source.ToJsonString()));
        var outSource = source.DeepClone().AsObject();
        if (!outSource.Remove("adjusters")) throw new InvalidOperationException("The pack has no adjuster block to remove.");
        var outPack = Require(FullCoreDiffusionDataPackV1.TryLoadJson(outSource.ToJsonString()));
        if (inPack.Adjusters == null || outPack.Adjusters != null) throw new InvalidOperationException("The comparison did not switch adjusters correctly.");
        double power = PracticeGameSessionFactory.PracticeReferenceThermalPowerWatts;
        FullCoreDiffusionSolveResultV1 Solve(FullCoreDiffusionDataPackV1 pack) =>
            Require(Require(FullCoreDiffusionModelV1.TryCreateCandu6(pack)).TrySolve(inventory, background, power,
                original.EffectiveK, original.Group1Flux, original.Group2Flux));
        Console.WriteLine("Solving fixed-inventory, fixed-Xe, fixed-LZC comparison with all 21 adjusters in...");
        var inserted = Solve(inPack);
        Console.WriteLine("Solving the identical state with all 21 adjusters out...");
        var withdrawn = Solve(outPack);
        foreach (var solve in new[] { inserted, withdrawn })
        {
            if (Math.Abs(solve.TotalPowerWatts - power) > .01 || solve.NodePowerWatts.Any(p => !double.IsFinite(p) || p < 0))
                throw new InvalidOperationException("Power normalization or finite-value validation failed.");
        }
        for (int n = 0; n < inserted.Coefficients.Nodes.Count; n++)
        {
            var a = inserted.Coefficients.Nodes[n];
            var b = withdrawn.Coefficients.Nodes[n];
            double error = Math.Max(Math.Abs(a.AbsorptionGroup1PerM - b.AbsorptionGroup1PerM - inPack.Adjusters.GetDeltaAbsorptionGroup1PerM(a.Node)),
                Math.Abs(a.AbsorptionGroup2PerM - b.AbsorptionGroup2PerM - inPack.Adjusters.GetDeltaAbsorptionGroup2PerM(a.Node)));
            if (a.Node != b.Node || error > 1e-12 || a.FissionGroup1PerM != b.FissionGroup1PerM || a.FissionGroup2PerM != b.FissionGroup2PerM)
                throw new InvalidOperationException("A coefficient changed beyond the intended adjuster absorption.");
        }
        var rows = Enumerable.Range(0, 12).Select(p =>
        {
            int n = checked((int)(channel * 12) + p);
            var node = inventory[n].Node;
            double inKw = inserted.NodePowerWatts[n] / 1000, outKw = withdrawn.NodePowerWatts[n] / 1000;
            return new
            {
                bundlePosition = p + 1,
                bundleId = inventory[n].BundleId.ToString(),
                burnupMwdPerKg = inventory[n].CurrentBurnupJPerKgHm / 86_400_000_000,
                adjustersInKw = inKw,
                adjustersOutKw = outKw,
                powerDifferenceKw = outKw - inKw,
                insertedDepressionPercent = 100 * (1 - inKw / outKw),
                directlyOverlapsAdjuster = inPack.Adjusters.GetDeltaAbsorptionGroup2PerM(node) > 0,
                directlyOverlapsLzcWater = zones.GetDeltaAbsorptionGroup2PerM(node) > 0
            };
        }).ToArray();
        var rodIds = PracticeAdjustersV1.Cells.Where(c => c.Node.ChannelId.Value == channel).Select(c => c.RodId).Distinct().ToHashSet();
        var rods = PracticeAdjustersV1.Rods.Where(r => rodIds.Contains(r.Id)).Select(r => new
        {
            id = r.Id,
            axialBundleCoordinate = r.AxialCentreM / PracticeAdjustersV1.BundleLengthM + .5
        }).ToArray();
        var mappedZones = session.CurrentLiquidZoneRrs.Mapping.Nodes.Where(b => b.Node.ChannelId.Value == channel && b.WaterColumn != null).Select(b => b.AbsorberZoneId).ToHashSet();
        var tubes = PracticeLiquidZoneTubesV1.Compartments.Where(t => mappedZones.Contains(t.ZoneId)).Select(t => new
        {
            zone = t.ZoneId + 1,
            fill = session.CurrentLiquidZoneRrs.ZoneFills[(int)t.ZoneId],
            axialBundleCoordinate = t.AxialCentreM / PracticeAdjustersV1.BundleLengthM + .5
        }).ToArray();
        var report = new
        {
            dataPack = inPack.Descriptor.DataPackVersion,
            seed,
            channelIndex = channel,
            simulationTimeSeconds = session.Snapshot.SimulationTimeSeconds,
            method = "All 21 adjusters inserted versus all 21 removed. Identical seed-aged fuel, frozen actual xenon and included reference, all 14 frozen LZC fills, leakage, fission coefficients and solver tolerances. Independent tighter static solves, each normalized to 2064 MW thermal; no controller compensation, fuel movement or time advancement. Only the adjuster absorption block changes.",
            groupOrdering = inPack.EnergyGroupOrder,
            zoneFills = session.CurrentLiquidZoneRrs.ZoneFills,
            backgroundOverlayDigest = background.OverlayDigestHex,
            insertedCorePowerWatts = inserted.TotalPowerWatts,
            withdrawnCorePowerWatts = withdrawn.TotalPowerWatts,
            insertedChannelKw = rows.Sum(r => r.adjustersInKw),
            withdrawnChannelKw = rows.Sum(r => r.adjustersOutKw),
            directlyOverlappingBundleDepressionPercent = 100 * (1 - rows.Where(r => r.directlyOverlapsAdjuster).Sum(r => r.adjustersInKw) / rows.Where(r => r.directlyOverlapsAdjuster).Sum(r => r.adjustersOutKw)),
            insertedK = inserted.EffectiveK,
            withdrawnK = withdrawn.EffectiveK,
            adjusterWorthMk = 1000 * (1 / inserted.EffectiveK - 1 / withdrawn.EffectiveK),
            insertedIterations = inserted.IterationCount,
            withdrawnIterations = withdrawn.IterationCount,
            insertedResidual = inserted.ResidualRelativeInfinity,
            withdrawnResidual = withdrawn.ResidualRelativeInfinity,
            insertedCoefficientDigest = Convert.ToHexString(inserted.CoefficientBindingDigest.ToArray()).ToLowerInvariant(),
            withdrawnCoefficientDigest = Convert.ToHexString(withdrawn.CoefficientBindingDigest.ToArray()).ToLowerInvariant(),
            adjusters = rods,
            lzcCompartments = tubes,
            bundles = rows
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, Pretty));
        Console.WriteLine($"Channel {channel}: in={report.insertedChannelKw:F3} kW, out={report.withdrawnChannelKw:F3} kW; overlapping-cell depression={report.directlyOverlappingBundleDepressionPercent:F3}%; {outputPath}");
    }

    private static T Require<T>(ContractValidationResult<T> result) => result.IsValid ? result.Value :
        throw new InvalidOperationException(result.FirstDiagnostic.ToString());
}
