using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class PracticeRunLifecycleTests
{
    [Fact]
    public void HorizonCompletionFreezesTheRunAndRejectsFurtherMoves()
    {
        // The standard non-browser practice fixture has a short 600-second horizon.
        var session = PracticeGameSessionFactory.Create();
        var completed = session.AdvanceWallMilliseconds(60_000);
        Assert.True(completed.Accepted, completed.DiagnosticMessage);
        Assert.True(completed.Snapshot.IsGameOver);
        Assert.False(completed.Snapshot.Rrs.IsGameOver);
        Assert.Equal(600, completed.Snapshot.SimulationTimeSeconds);
        Assert.Contains("horizon", completed.Snapshot.GameOverReason);
        Assert.False(session.AdvanceWallMilliseconds(100).Accepted);
        Assert.False(session.RefuelChannel(189, "toward-end-b", 4, "NAT-U-SYNTHETIC").Accepted);
        Assert.False(session.QueuePowerTarget(0.95).Accepted);
        Assert.False(session.Resume().Accepted);
        Assert.False(session.DebugGrantFreshBundles(4).Accepted);
        Assert.False(session.DebugResetSyntheticResponse().Accepted);
        Assert.False(session.ConfigureCell(189, 0, false, System.Array.Empty<ReactorSim.Core.TopologyFace>()).Accepted);
        Assert.Equal(completed.Snapshot.ScoreTotal, session.Snapshot.ScoreTotal);
        Assert.Equal(completed.Snapshot.FreshBundlesAvailable, session.Snapshot.FreshBundlesAvailable);
    }

    [Fact]
    public void FreePracticeDoesNotOverrideThePlayersTargetOrInventFuelMoves()
    {
        var session = PracticeGameSessionFactory.Create();
        Assert.True(session.QueuePowerTarget(0.9).Accepted);
        var advanced = session.AdvanceWallMilliseconds(40_000);
        Assert.True(advanced.Accepted, advanced.DiagnosticMessage);
        Assert.Equal(400, advanced.Snapshot.SimulationTimeSeconds);
        Assert.Equal(0.9, advanced.Snapshot.NormalizedPowerFraction, 10);
        Assert.Equal(0u, advanced.Snapshot.ProcessedScriptedEventCount);
        Assert.Equal(128u, advanced.Snapshot.FreshBundlesAvailable);
        Assert.Equal(0u, advanced.Snapshot.RefuellingOperationCount);
    }
}
