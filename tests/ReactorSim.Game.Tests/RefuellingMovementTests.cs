using System.Linq;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class RefuellingMovementTests
{
    [Fact]
    public void FourBundleOrdersAreRejectedAtomicallyAndOnlyEightBundlePlansArePublished()
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        var before = session.CoreState;
        var rejected = session.RefuelChannel(210, "toward-end-b", 4, "NAT-U-SYNTHETIC");
        Assert.False(rejected.Accepted);
        Assert.Equal("GameRefuelling.ShiftCount.Unsupported", rejected.DiagnosticCode);
        Assert.Same(before, session.CoreState);
        Assert.Equal(2, rejected.Snapshot.RefuellingPlans.Count);
        Assert.All(rejected.Snapshot.RefuellingPlans, p => Assert.Equal(8, p.ShiftCount));
    }

    [Fact]
    public void GeometryEligibilityMatchesCommandRejectionAndRestoration()
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        Assert.True(session.Snapshot.Core.Channels[0].CanRefuel);
        var changed = session.ConfigureCell(0, 0, false, System.Array.Empty<ReactorSim.Core.TopologyFace>());
        Assert.True(changed.Accepted, changed.DiagnosticMessage);
        var channel = changed.Snapshot.Core.Channels[0];
        Assert.False(channel.CanRefuel);
        Assert.Contains("nonfuel", channel.RefuellingIneligibilityReason);
        var rejected = session.RefuelChannel(0, "toward-end-a", 8, "NAT-U-SYNTHETIC");
        Assert.False(rejected.Accepted);
        Assert.Equal(channel.RefuellingIneligibilityReason, rejected.DiagnosticMessage);
        Assert.True(session.ConfigureCell(0, 0, true, System.Array.Empty<ReactorSim.Core.TopologyFace>()).Snapshot.Core.Channels[0].CanRefuel);
    }

    [Theory]
    [InlineData(210u, "toward-end-a", 8)]
    [InlineData(211u, "toward-end-b", 8)]
    public void PublishedPlanMatchesConfirmedIdentitiesAndRejectedCommandsRetainSummary(uint channelIndex, string direction, ushort count)
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        var before = session.Snapshot.Core.Channels[(int)channelIndex];
        var plan = session.Snapshot.RefuellingPlans.Single(p => p.DirectionId == (before.FlowDirection == ReactorSim.Core.FlowDirection.EndAtoEndB ? "toward-end-b" : "toward-end-a") && p.ShiftCount == count);
        var result = session.RefuelChannel(channelIndex, direction, count, "NAT-U-SYNTHETIC");
        Assert.True(result.Accepted, result.DiagnosticMessage);
        var move = result.Snapshot.LastFuelMovement!;
        Assert.Equal(plan.DirectionId, move.Plan.DirectionId);
        Assert.Equal(1u, move.OperationId);
        Assert.Equal(plan.DischargedPositions, move.Bundles.Where(b => b.AfterPosition == null).Select(b => b.BeforePosition!.Value));
        Assert.Equal(plan.InsertedPositions, move.Bundles.Where(b => b.BeforePosition == null).Select(b => b.AfterPosition!.Value));
        foreach (var bundle in move.Bundles.Where(b => b.BeforePosition != null))
            Assert.Equal(before.Bundles[(int)bundle.BeforePosition!.Value].BundleId, bundle.BundleId);
        foreach (var bundle in move.Bundles.Where(b => b.AfterPosition != null))
            Assert.Equal(result.Snapshot.Core.Channels[(int)channelIndex].Bundles[(int)bundle.AfterPosition!.Value].BundleId, bundle.BundleId);
        Assert.Same(result.Snapshot.LastRefuellingScore, move.Score);
        Assert.Same(move, session.RefuelChannel(999, direction, count, "NAT-U-SYNTHETIC").Snapshot.LastFuelMovement);
    }
}
