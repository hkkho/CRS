using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class UnlimitedFreshFuelTests
{
    [Fact]
    public void UnlimitedInventoryPreservesFuelIdentityAndAcceptsMovesBeyondFiniteBudget()
    {
        var bounded = SyntheticGameCoreStateV1.CreateAgedPractice(1001);
        var state = bounded.WithUnlimitedFreshFuel();
        Assert.Equal(bounded.GetBundle(0, 0).BundleId, state.GetBundle(0, 0).BundleId);
        Assert.Equal(128u, bounded.FreshBundlesAvailable);
        for (uint move = 0; move < 40; move++)
        {
            var result = state.TryRefuel(move, GameRefuellingDirectionV1.TowardEndB, 8, "NAT-U-SYNTHETIC", move * 86400);
            Assert.True(result.IsValid);
            state = result.Value.ResultingState;
            Assert.True(state.UnlimitedFreshFuel);
            Assert.Equal(0u, state.FreshBundlesAvailable);
            Assert.Equal(move + 1, state.RefuellingOperationCount);
        }
        Assert.False(state.TryRefuel(999, GameRefuellingDirectionV1.TowardEndB, 8, "NAT-U-SYNTHETIC", 0).IsValid);
        Assert.True(state.WithFreshBundles(8).UnlimitedFreshFuel);
    }
}
