using System;
using System.Linq;
using System.IO;
using ReactorSim.Core;
using ReactorSim.TestInfrastructure;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class AgedCoreSnapshotTests
{
    [Fact]
    public void ExtendedPackMatchesRepositoryAndPreservesPositiveTailYield()
    {
        var pack = FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6().Value;
        using var stream = typeof(FullCoreDiffusionDataPackV1).Assembly.GetManifestResourceStream(
            "ReactorSim.Core.Data.candu6-two-group-diffusion-pack-v1.json")!;
        using var reader = new StreamReader(stream);
        Assert.Equal(File.ReadAllText(TestDataLocator.RequireRepositoryFile(
            "data/packs/candu6-two-group-diffusion-pack-v1.json")), reader.ReadToEnd());
        var table = Assert.Single(pack.CoefficientTables);
        foreach (double burnup in new[] { 20.0, 22.5, 25.0, 27.5, 30.0 })
        {
            var coefficients = table.TryLookup(burnup * 8.64e10).Value.Coefficients;
            Assert.Equal(0.11411937984496123 - 0.0020440310077519384 * (burnup - 20), coefficients.FissionGroup2PerM, 12);
            Assert.Equal(2.286808433764792, coefficients.NuFissionGroup2PerM / coefficients.FissionGroup2PerM, 12);
            Assert.True(coefficients.FissionGroup2PerM > 0);
            Assert.True(coefficients.AbsorptionGroup2PerM >= coefficients.FissionGroup2PerM);
        }
        Assert.False(table.TryLookup(30.00001 * 8.64e10).IsValid);
    }

    [Fact]
    public void ChannelCycleMatchesThermalEnergyAndEightBundleThroughput()
    {
        double bundlesPerDay = SyntheticGameCoreStateV1.ChannelCount * 8 /
            AgedCoreSnapshotGeneratorV1.ChannelRefuellingIntervalFullPowerDays;
        Assert.Equal(16, bundlesPerDay);
        Assert.Equal(285, AgedCoreSnapshotGeneratorV1.MeanBundleResidenceFullPowerDays);
        double dischargedEnergyMwDayPerDay = bundlesPerDay * SyntheticGameCoreStateV1.DefaultHeavyMetalMassKg *
            AgedCoreSnapshotGeneratorV1.TargetDischargeBurnupMwDayPerKg;
        Assert.Equal(2064, dischargedEnergyMwDayPerDay, 8);
    }

    [Fact]
    public void PatternedAgesAreReproducibleAndBalancedInEveryRegion()
    {
        double[] first = AgedCoreSnapshotGeneratorV1.CreateChannelAges(1001);
        Assert.Equal(first, AgedCoreSnapshotGeneratorV1.CreateChannelAges(1001));
        Assert.NotEqual(first, AgedCoreSnapshotGeneratorV1.CreateChannelAges(1002));
        var mapping = PracticeLiquidZoneRrsMappingV1.TryCreateCandu6().Value;
        for (uint region = 0; region < 7; region++)
        {
            var channels = mapping.Nodes.Where(n => n.Node.Position.Value == 0 && n.LogicalZoneId == region)
                .Select(n => n.Node.ChannelId.Value).ToArray();
            double[] ages = channels.Select(c => first[c]).OrderBy(a => a).ToArray();
            Assert.NotEmpty(ages);
            Assert.Equal(0.5, ages.Average(), 12);
            for (int k = 0; k < ages.Length; k++) Assert.Equal((k + 0.5) / ages.Length, ages[k], 12);
        }
    }

    [Fact]
    public void BeginningAndEndOfCycleRespectEightBundleMovementAndDischargeTarget()
    {
        for (int k = 0; k < 8; k++) Assert.Equal(0, Burnup(k, 0));
        for (int k = 8; k < 12; k++) Assert.Equal(Burnup(k - 8, 1), Burnup(k, 0), 12);
        Assert.Equal(AgedCoreSnapshotGeneratorV1.TargetDischargeBurnupMwDayPerKg,
            Enumerable.Range(4, 8).Average(k => Burnup(k, 1)), 12);
        Assert.Throws<ArgumentOutOfRangeException>(() => Burnup(12, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Burnup(0, double.NaN));
    }

    [Theory]
    [InlineData(0UL)]
    [InlineData(1001UL)]
    [InlineData(1002UL)]
    [InlineData(4294967295UL)]
    public void InventoryUsesAlternatingDirectionsAndFitsExistingBurnupDomain(ulong seed)
    {
        var core = SyntheticGameCoreStateV1.CreateAgedPractice(seed);
        var ages = AgedCoreSnapshotGeneratorV1.CreateChannelAges(seed);
        var table = Assert.Single(FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6().Value.CoefficientTables);
        Assert.Equal(4560, core.EnumerateBundles().Select(b => b.BundleId).Distinct().Count());
        Assert.Equal(128U, core.FreshBundlesAvailable);
        Assert.Equal(0U, core.RefuellingOperationCount);
        foreach (var bundle in core.EnumerateBundles())
        {
            var grid = Candu6CoreTopologyFactoryV1.GetPosition(bundle.ChannelId.Value);
            int k = (int)bundle.Position.Value;
            if (((grid.Column + grid.CartesianY) & 1) != 0) k = 11 - k;
            Assert.Equal(Burnup(k, ages[bundle.ChannelId.Value]) * 8.64e10, bundle.CurrentBurnupJPerKgHm, 5);
            Assert.True(table.TryLookup(bundle.CurrentBurnupJPerKgHm).IsValid);
            // Generous 2 MW/bundle exposure for the complete 30-day browser run.
            double upperExposure = bundle.CurrentBurnupJPerKgHm + 2e6 * 30 * 86400 / bundle.HeavyMetalMassKg;
            Assert.True(table.TryLookup(upperExposure).IsValid);
        }
    }

    private static double Burnup(int k, double age) => AgedCoreSnapshotGeneratorV1.BundleBurnupMwDayPerKg(k, age);
}
