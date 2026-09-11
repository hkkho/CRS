using System;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class PracticeLiquidZoneRrsTests
{
    [Fact]
    public void PracticeMapCoversEveryNodeOnceAndSplitsEachChannelAxially()
    {
        PracticeLiquidZoneRrsMappingV1 mapping = Require(
            PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());

        Assert.Equal(380 * 12, mapping.Nodes.Count);
        Assert.Equal(380 * 12, mapping.Nodes.Select(node => node.Node).Distinct().Count());
        Assert.Equal(14, mapping.NodeIndicesByZone.Count);
        Assert.All(mapping.NodeIndicesByZone, zone => Assert.NotEmpty(zone));

        for (uint channel = 0; channel < 380; channel++)
        {
            uint endA = mapping.GetLogicalZoneId(
                new NodeKey(new ChannelId(channel), new BundlePosition(0)));
            uint endB = mapping.GetLogicalZoneId(
                new NodeKey(new ChannelId(channel), new BundlePosition(6)));

            Assert.Equal(endA + 7U, endB);
            for (uint bundle = 0; bundle < 6; bundle++)
            {
                Assert.Equal(endA, mapping.GetLogicalZoneId(
                    new NodeKey(new ChannelId(channel), new BundlePosition(bundle))));
                Assert.Equal(endB, mapping.GetLogicalZoneId(
                    new NodeKey(new ChannelId(channel), new BundlePosition(bundle + 6U))));
            }
        }
    }

    [Fact]
    public void IncreasingFillAddsAbsorptionForOnlyTheMappedZone()
    {
        PracticeLiquidZoneRrsMappingV1 mapping = Require(
            PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());
        double[] low = Enumerable.Repeat(0.5, 14).ToArray();
        double[] high = Enumerable.Repeat(0.5, 14).ToArray();
        high[3] = 0.75;

        StaticAbsorptionOverlayV1 lowOverlay = Require(mapping.TryBuildOverlay(low));
        StaticAbsorptionOverlayV1 highOverlay = Require(mapping.TryBuildOverlay(high));
        PracticeLiquidZoneRrsNodeBindingV1 controlled = mapping.Nodes
            .First(node => node.LogicalZoneId == 3U);
        PracticeLiquidZoneRrsNodeBindingV1 untouched = mapping.Nodes
            .First(node => node.LogicalZoneId != 3U);

        Assert.True(
            highOverlay.GetDeltaAbsorptionGroup1PerM(controlled.Node) >
            lowOverlay.GetDeltaAbsorptionGroup1PerM(controlled.Node));
        Assert.True(
            highOverlay.GetDeltaAbsorptionGroup2PerM(controlled.Node) >
            lowOverlay.GetDeltaAbsorptionGroup2PerM(controlled.Node));
        Assert.Equal(
            lowOverlay.GetDeltaAbsorptionGroup1PerM(untouched.Node),
            highOverlay.GetDeltaAbsorptionGroup1PerM(untouched.Node));
    }

    [Fact]
    public void InitialStateIsDeterministicCenteredAndUsesTheInitialShapeAsReference()
    {
        SyntheticGameCoreStateV1 inventory = SyntheticGameCoreStateV1.CreatePractice();
        FullCoreDiffusionDataPackV1 pack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        FullCoreDiffusionModelV1 model = Require(
            FullCoreDiffusionModelV1.TryCreateCandu6(pack));
        EquilibriumCoreSolverV1 solver = Require(
            EquilibriumCoreSolverV1.TryCreate(model, inventory.EnumerateBundles(), 1.0e9));
        PracticeLiquidZoneRrsMappingV1 mapping = Require(
            PracticeLiquidZoneRrsMappingV1.TryCreateCandu6());

        PracticeLiquidZoneRrsV1 first = Require(
            PracticeLiquidZoneRrsV1.TryCreate(mapping, solver.CurrentProjection));
        PracticeLiquidZoneRrsV1 second = Require(
            PracticeLiquidZoneRrsV1.TryCreate(mapping, solver.CurrentProjection));

        Assert.Equal(14, first.ZoneFills.Count);
        Assert.All(first.ZoneFills, fill => Assert.Equal(0.5, fill));
        Assert.Equal(first.ReferenceZonalPowerFractions, first.TargetZonalPowerFractions);
        Assert.Equal(first.TargetZonalPowerFractions, first.MeasuredZonalPowerFractions);
        Assert.Equal(first.StateDigest, second.StateDigest);
        Assert.False(first.LowExhaustion);
        Assert.False(first.HighExhaustion);
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        if (!result.IsValid)
        {
            Assert.Fail(result.FirstDiagnostic.Message);
        }

        return result.Value;
    }
}
