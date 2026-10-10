using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class WebExposureTests
{
    [Fact]
    public void ExposureUsesSiEnergyAndPreservesCanonicalBundleIdentity()
    {
        var source = SyntheticGameCoreStateV1.CreateAgedPractice(1001);
        var before = source.EnumerateBundles().ToArray();
        var energy = before.Select((_, index) => (index + 1) * 1e9).ToArray();
        var result = source.TryAddFissionEnergy(energy);
        Assert.True(result.IsValid);
        var after = result.Value.EnumerateBundles().ToArray();
        for (int index = 0; index < before.Length; index++)
        {
            Assert.Equal(before[index].BundleId, after[index].BundleId);
            Assert.Equal(before[index].ChannelId, after[index].ChannelId);
            Assert.Equal(before[index].Position, after[index].Position);
            Assert.Equal(before[index].CumulativeFissionEnergyJ + energy[index], after[index].CumulativeFissionEnergyJ);
            Assert.Equal(before[index].CurrentBurnupJPerKgHm + energy[index] / before[index].HeavyMetalMassKg,
                after[index].CurrentBurnupJPerKgHm, 3);
        }
        Assert.Equal(before, source.EnumerateBundles().ToArray());
        Assert.Equal(source.FreshBundlesAvailable, result.Value.FreshBundlesAvailable);
        Assert.Equal(source.RefuellingOperationCount, result.Value.RefuellingOperationCount);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1.0)]
    public void InvalidLateExposurePreservesTheWholeInventory(double invalidEnergy)
    {
        var source = SyntheticGameCoreStateV1.CreateAgedPractice(1001);
        var before = source.EnumerateBundles().ToArray();
        var energy = Enumerable.Repeat(1e9, before.Length).ToArray();
        energy[^1] = invalidEnergy;
        Assert.False(source.TryAddFissionEnergy(energy).IsValid);
        Assert.Equal(before, source.EnumerateBundles().ToArray());
        Assert.False(source.TryAddFissionEnergy(energy[..^1]).IsValid);
        Assert.Equal(before, source.EnumerateBundles().ToArray());
    }
}
