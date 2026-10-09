using System;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class PracticeLiquidZoneTubeTests
{
    [Fact]
    public void LocalizedFootprintsConserveEachCompartmentWaterVolumeAtEveryFill()
    {
        var mapping = PracticeLiquidZoneRrsMappingV1.TryCreateCandu6().Value;
        Assert.Equal(14, PracticeLiquidZoneTubesV1.Compartments.Count);
        Assert.Equal(6, PracticeLiquidZoneTubesV1.Compartments.Select(t => (t.HorizontalCentreM, t.AxialCentreM)).Distinct().Count());
        Assert.Equal(424, mapping.Nodes.Count(n => n.WaterColumn != null));
        var zeroStrength = PracticeLiquidZoneRrsMappingV1.TryCreateCandu6(BitConverter.Int64BitsToDouble(long.MinValue), 0);
        Assert.True(zeroStrength.IsValid);
        Assert.All(zeroStrength.Value.Nodes, n => Assert.Equal(0, BitConverter.DoubleToInt64Bits(n.Group1AbsorptionPerMPerFillFraction)));
        Assert.Contains(mapping.Nodes, n => n.WaterColumn != null && n.AbsorberZoneId != n.LogicalZoneId);
        foreach (double fill in new[] { 0.0, .1, .25, .5, .9, 1.0 })
        {
            var overlay = mapping.TryBuildOverlay(Enumerable.Repeat(fill, 14).ToArray()).Value;
            foreach (var tube in PracticeLiquidZoneTubesV1.Compartments)
            {
                var cells = mapping.Nodes.Where(n => n.WaterColumn != null && n.AbsorberZoneId == tube.ZoneId).ToArray();
                Assert.Equal(new uint[] { tube.ZoneId < 7 ? 2U : 8U, tube.ZoneId < 7 ? 3U : 9U },
                    cells.Select(n => n.Node.Position.Value).Distinct().OrderBy(p => p));
                double expectedCellVolumes = fill * (tube.VerticalMaximumM - tube.VerticalMinimumM) / PracticeAdjustersV1.LatticePitchM;
                Assert.Equal(expectedCellVolumes, cells.Sum(n => overlay.GetDeltaAbsorptionGroup2PerM(n.Node)) /
                    PracticeLiquidZoneRrsIdentityV1.Group2AbsorptionPerMPerFillFraction, 10);
            }
            Assert.All(mapping.Nodes.Where(n => n.WaterColumn == null), n =>
                Assert.Equal(0, BitConverter.DoubleToInt64Bits(overlay.GetDeltaAbsorptionGroup2PerM(n.Node))));
        }
    }

    [Fact]
    public void WaterRisesFromBottomAndOnlyItsOwningCompartmentChanges()
    {
        var mapping = PracticeLiquidZoneRrsMappingV1.TryCreateCandu6().Value;
        var fills = Enumerable.Repeat(.25, 14).ToArray();
        var low = mapping.TryBuildOverlay(fills).Value;
        fills[3] = .75;
        var high = mapping.TryBuildOverlay(fills).Value;
        var footprint = mapping.Nodes.Where(n => n.WaterColumn != null && n.AbsorberZoneId == 3).ToArray();
        var bottom = footprint.OrderBy(n => n.WaterColumn!.CellBottomM).First();
        var top = footprint.OrderByDescending(n => n.WaterColumn!.CellTopM).First();
        Assert.Equal(bottom.Group2AbsorptionPerMPerFillFraction, low.GetDeltaAbsorptionGroup2PerM(bottom.Node), 12);
        Assert.Equal(0, low.GetDeltaAbsorptionGroup2PerM(top.Node));
        Assert.Equal(0, high.GetDeltaAbsorptionGroup2PerM(top.Node));
        Assert.Contains(footprint, n => high.GetDeltaAbsorptionGroup2PerM(n.Node) > low.GetDeltaAbsorptionGroup2PerM(n.Node));
        Assert.All(mapping.Nodes.Where(n => n.AbsorberZoneId != 3), n =>
            Assert.Equal(low.GetDeltaAbsorptionGroup2PerM(n.Node), high.GetDeltaAbsorptionGroup2PerM(n.Node)));
        var staticMasks = PracticeLiquidZoneRrsMappingV1.TryCreate(mapping.Nodes.Select(n =>
            new PracticeLiquidZoneRrsNodeBindingV1(n.Node, n.LogicalZoneId,
                n.Group1AbsorptionPerMPerFillFraction, n.Group2AbsorptionPerMPerFillFraction, n.AbsorberZoneId))).Value;
        Assert.NotEqual(mapping.MappingDigest, staticMasks.MappingDigest);
    }
}
