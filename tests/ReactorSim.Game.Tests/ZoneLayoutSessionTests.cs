using System.Linq;
using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class ZoneLayoutSessionTests
{
    [Fact]
    public void LayoutEditsCommitTogetherAndInvalidLayoutsPreserveLiveState()
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest(1001);
        var before = session.Snapshot;
        var nodes = session.CurrentLiquidZoneRrs.Mapping.Nodes.ToArray();
        var original = nodes[0];
        nodes[0] = new PracticeLiquidZoneRrsNodeBindingV1(original.Node,
            (original.LogicalZoneId + 1) % 14, original.Group1AbsorptionPerMPerFillFraction,
            original.Group2AbsorptionPerMPerFillFraction, original.AbsorberZoneId);
        var edited = session.ConfigureZoneLayout(nodes);
        Assert.True(edited.Accepted, edited.DiagnosticMessage);
        Assert.Equal(nodes[0].LogicalZoneId, session.CurrentLiquidZoneRrs.Mapping.Nodes[0].LogicalZoneId);
        Assert.Equal(before.SimulationTimeSeconds, edited.Snapshot.SimulationTimeSeconds);
        Assert.Equal(before.FreshBundlesAvailable, edited.Snapshot.FreshBundlesAvailable);
        Assert.Equal(before.ScoreTotal, edited.Snapshot.ScoreTotal);
        var digest = session.CurrentLiquidZoneRrs.StateDigest;
        var invalid = session.ConfigureZoneLayout(nodes.Select(n => new PracticeLiquidZoneRrsNodeBindingV1(
            n.Node, 0, n.Group1AbsorptionPerMPerFillFraction, n.Group2AbsorptionPerMPerFillFraction)));
        Assert.False(invalid.Accepted);
        Assert.Equal(digest, session.CurrentLiquidZoneRrs.StateDigest);
        Assert.True(session.AdvanceWallMilliseconds(1000).Accepted);
    }
}
