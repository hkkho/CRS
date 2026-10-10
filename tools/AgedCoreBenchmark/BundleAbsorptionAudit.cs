using System.Text.Json;
using System.Globalization;
using ReactorSim.Core;
using ReactorSim.Game;

internal static class BundleAbsorptionAudit
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };
    public static void Run(string outputPath, ulong seed)
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest(seed);
        var projection = session.CurrentSpatialCandidate;
        var pack = projection.DataPack;
        var rods = pack.Adjusters ?? throw new InvalidOperationException("The active pack has no adjusters.");
        var waterResult = session.CurrentLiquidZoneRrs.TryBuildOverlay();
        if (!waterResult.IsValid) throw new InvalidOperationException(waterResult.FirstDiagnostic.ToString());
        var water = waterResult.Value;
        var bindings = session.CurrentLiquidZoneRrs.Mapping.Nodes.ToDictionary(n => n.Node);
        var inventory = session.CoreState.EnumerateBundles().ToDictionary(b => b.Node);
        var tables = pack.CoefficientTables.ToDictionary(t => t.MaterialVariantId);
        var poison = session.CurrentXenonState;
        var channels = projection.SpatialSolve.Coefficients.Nodes
            .Where(n => rods.GetDeltaAbsorptionGroup2PerM(n.Node) > 0)
            .Select(n => n.Node.ChannelId.Value).Distinct()
            .Where(c => projection.SpatialSolve.Coefficients.Nodes.Any(n => n.Node.ChannelId.Value == c && water.GetDeltaAbsorptionGroup2PerM(n.Node) > 0))
            .OrderBy(c =>
            {
                var grid = Candu6CoreTopologyFactoryV1.GetPosition(c);
                return Math.Pow(grid.Column - 10.5, 2) + Math.Pow(grid.DisplayRow - 10.5, 2);
            }).ThenBy(c => c).ToArray();
        if (channels.Length == 0) throw new InvalidOperationException("No channel overlaps both an adjuster and wetted LZC cells.");
        uint channel = channels[0];
        var position = Candu6CoreTopologyFactoryV1.GetPosition(channel);
        string[] labels = { "A", "B", "C", "D", "E", "F", "G", "H", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W" };
        var rows = projection.SpatialSolve.Coefficients.Nodes.Where(n => n.Node.ChannelId.Value == channel).Select(n =>
        {
            var bundle = inventory[n.Node];
            var lookup = tables[bundle.MaterialVariantId].TryLookup(bundle.CurrentBurnupJPerKgHm);
            if (!lookup.IsValid) throw new InvalidOperationException(lookup.FirstDiagnostic.ToString());
            var raw = lookup.Value.Coefficients;
            int index = checked((int)(channel * 12 + n.Node.Position.Value));
            double referenceXe = PracticeXenonDataV1.SigmaGroup2M2 * poison.IncludedReferenceXenon[index];
            double actualXe = PracticeXenonDataV1.SigmaGroup2M2 * poison.Xenon[index];
            double fuel2 = raw.AbsorptionGroup2PerM - referenceXe;
            double adjuster1 = rods.GetDeltaAbsorptionGroup1PerM(n.Node), adjuster2 = rods.GetDeltaAbsorptionGroup2PerM(n.Node);
            double lzc1 = water.GetDeltaAbsorptionGroup1PerM(n.Node), lzc2 = water.GetDeltaAbsorptionGroup2PerM(n.Node);
            double error = Math.Max(Math.Abs(n.AbsorptionGroup1PerM - raw.AbsorptionGroup1PerM - adjuster1 - lzc1),
                Math.Abs(n.AbsorptionGroup2PerM - fuel2 - actualXe - adjuster2 - lzc2));
            if (error > 1e-12 || fuel2 < 0) throw new InvalidOperationException("Absorption component sum does not match the authoritative solver coefficient.");
            return new
            {
                bundlePosition = n.Node.Position.Value + 1,
                bundleId = bundle.BundleId.ToString(),
                burnupMwdPerKg = bundle.CurrentBurnupJPerKgHm / 86_400_000_000,
                fuelBackgroundFastPerM = raw.AbsorptionGroup1PerM,
                fuelBackgroundThermalPerM = fuel2,
                xenonFastPerM = 0.0,
                xenonThermalPerM = actualXe,
                includedReferenceXenonThermalPerM = referenceXe,
                adjusterFastPerM = adjuster1,
                adjusterThermalPerM = adjuster2,
                lzcFastPerM = lzc1,
                lzcThermalPerM = lzc2,
                totalFastPerM = n.AbsorptionGroup1PerM,
                totalThermalPerM = n.AbsorptionGroup2PerM,
                absorberZone = bindings[n.Node].AbsorberZoneId + 1,
                zoneFill = session.CurrentLiquidZoneRrs.ZoneFills[(int)bindings[n.Node].AbsorberZoneId],
                componentSumErrorPerM = error
            };
        }).OrderBy(r => r.bundlePosition).ToArray();
        var rodIds = PracticeAdjustersV1.Cells.Where(c => c.Node.ChannelId.Value == channel).Select(c => c.RodId).Distinct().ToHashSet();
        var selectedRods = PracticeAdjustersV1.Rods.Where(r => rodIds.Contains(r.Id)).Select(r => new
        {
            id = r.Id,
            axialBundleCoordinate = r.AxialCentreM / PracticeAdjustersV1.BundleLengthM + .5
        }).ToArray();
        var selectedZones = bindings.Values.Where(b => b.Node.ChannelId.Value == channel && b.WaterColumn != null).Select(b => b.AbsorberZoneId).ToHashSet();
        var tubes = PracticeLiquidZoneTubesV1.Compartments.Where(t => selectedZones.Contains(t.ZoneId)).Select(t => new
        {
            zone = t.ZoneId + 1,
            fill = session.CurrentLiquidZoneRrs.ZoneFills[(int)t.ZoneId],
            axialBundleCoordinate = t.AxialCentreM / PracticeAdjustersV1.BundleLengthM + .5
        }).ToArray();
        var report = new
        {
            dataPack = pack.Descriptor.DataPackVersion,
            seed,
            simulationTimeSeconds = session.Snapshot.SimulationTimeSeconds,
            channelIndex = channel,
            channelLabel = labels[position.DisplayRow] + (position.Column + 1).ToString("00", CultureInfo.InvariantCulture),
            gridRow = position.DisplayRow,
            gridColumn = position.Column,
            units = "macroscopic absorption cross section, m^-1",
            method = "Initial aged browser core. Exact solved node coefficients; separate pack adjuster and current localized water overlays. Fuel background subtracts the included Xe reference before actual Xe is added. All component sums checked against solved coefficients to 1e-12 m^-1. Cell averages, not microscopic cross sections of individual materials.",
            adjusters = selectedRods,
            lzcCompartments = tubes,
            coefficientBindingDigest = Convert.ToHexString(projection.SpatialSolve.CoefficientBindingDigest.ToArray()).ToLowerInvariant(),
            bundles = rows
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(report, Pretty));
        Console.WriteLine($"Channel {report.channelLabel} (index {channel}): exported {rows.Length} bundles; zones {string.Join(",", tubes.Select(t => t.zone))}; max component error {rows.Max(r => r.componentSumErrorPerM):E2}; {outputPath}");
    }
}
