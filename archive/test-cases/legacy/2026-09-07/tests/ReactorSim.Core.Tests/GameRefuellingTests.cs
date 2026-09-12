using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class GameRefuellingTests
{
    [Fact]
    public void PracticeCoreMovesBundlesInBothDirectionsAndConsumesFreshInventory()
    {
        SyntheticGameCoreStateV1 initial = SyntheticGameCoreStateV1.CreatePractice();
        BundleState[] towardEndBBefore = initial.GetChannel(7).ToArray();

        ContractValidationResult<GameRefuellingResultV1> towardEndB = initial.TryRefuel(
            7,
            GameRefuellingDirectionV1.TowardEndB,
            4,
            "NAT-U-SYNTHETIC",
            120.0);

        Assert.True(towardEndB.IsValid, towardEndB.IsValid ? string.Empty : towardEndB.FirstDiagnostic.ToString());
        SyntheticGameCoreStateV1 afterEndB = towardEndB.Value.ResultingState;
        Assert.Equal(124u, afterEndB.FreshBundlesAvailable);
        Assert.Equal(1u, afterEndB.RefuellingOperationCount);
        Assert.Equal(towardEndBBefore.Take(8).Select(bundle => bundle.BundleId),
            afterEndB.GetChannel(7).Skip(4).Select(bundle => bundle.BundleId));
        Assert.Equal(towardEndBBefore.Skip(8).Select(bundle => bundle.BundleId),
            towardEndB.Value.DischargedBundles.Select(bundle => bundle.BundleId));
        Assert.All(afterEndB.GetChannel(7).Take(4), bundle => Assert.Equal(0.0, bundle.CurrentBurnupJPerKgHm));
        Assert.Equal(towardEndBBefore[0].BundleId, initial.GetBundle(7, 0).BundleId);

        BundleState[] towardEndABefore = afterEndB.GetChannel(8).ToArray();
        ContractValidationResult<GameRefuellingResultV1> towardEndA = afterEndB.TryRefuel(
            8,
            GameRefuellingDirectionV1.TowardEndA,
            8,
            "NAT-U-SYNTHETIC",
            180.0);

        Assert.True(towardEndA.IsValid, towardEndA.IsValid ? string.Empty : towardEndA.FirstDiagnostic.ToString());
        SyntheticGameCoreStateV1 final = towardEndA.Value.ResultingState;
        Assert.Equal(towardEndABefore.Skip(8).Select(bundle => bundle.BundleId),
            final.GetChannel(8).Take(4).Select(bundle => bundle.BundleId));
        Assert.Equal(towardEndABefore.Take(8).Select(bundle => bundle.BundleId),
            towardEndA.Value.DischargedBundles.Select(bundle => bundle.BundleId));
        Assert.All(final.GetChannel(8).Skip(4), bundle => Assert.Equal(0.0, bundle.CurrentBurnupJPerKgHm));
        Assert.Equal(116u, final.FreshBundlesAvailable);
        Assert.Equal(2u, final.RefuellingOperationCount);
    }
}
