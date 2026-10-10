using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class DailyTurnTests
{
    [Fact]
    public void NewBrowserRunsFreezeAndOnlyDailyCommandsAdvance()
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        Assert.Equal("daily-turn", session.Snapshot.PacingMode);
        Assert.True(session.Snapshot.IsPaused);
        Assert.False(session.Resume().Accepted);
        Assert.False(session.AdvanceWallMilliseconds(100).Accepted);
        Assert.False(session.SetPlaybackMode(PracticeGameSessionFactory.DebugPlaybackModeId).Accepted);
        Assert.False(session.QueuePowerTarget(1).Accepted);
        Assert.False(session.RefuelChannel(210, "toward-end-b", 8, "NAT-U-SYNTHETIC").Accepted);
        var day = session.CommitDay(0, Array.Empty<uint>());
        Assert.True(day.Accepted, day.DiagnosticMessage);
        Assert.Equal(86400, day.Snapshot.SimulationTimeSeconds);
        Assert.Equal(0, day.Snapshot.WallElapsedSeconds);
        Assert.Equal(1u, day.Snapshot.CompletedDays);
        Assert.True(day.Snapshot.IsPaused);
        Assert.Empty(day.Snapshot.LastDayResult!.Movements);
        Assert.InRange(day.Snapshot.LastDayResult.ScoreDelta, 0, 24);
        Assert.Equal(2064 * 24, day.Snapshot.LastDayResult.ThermalEnergyMwh, 5);
        Assert.False(session.CommitDay(0, Array.Empty<uint>()).Accepted);
        Assert.Equal(86400, session.Snapshot.SimulationTimeSeconds);
    }

    [Fact]
    public void InvalidPlansPreserveStateAndReport()
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest(challenge: true);
        var before = session.CoreState;
        var projection = session.CurrentSpatialCandidate;
        foreach (var channels in new[] { new uint[] { 210, 210 }, new uint[] { 380 }, Enumerable.Range(0, 20).Select(i => (uint)i).ToArray() })
        {
            Assert.False(session.CommitDay(0, channels).Accepted);
            Assert.Same(before, session.CoreState);
            Assert.Same(projection, session.CurrentSpatialCandidate);
            Assert.Equal(0, session.Snapshot.SimulationTimeSeconds);
            Assert.Equal(0u, session.Snapshot.Shift.FuelConsumed);
            Assert.Null(session.Snapshot.LastDayResult);
        }
        var realtime = PracticeGameSessionFactory.CreateBrowserPlaytest(dailyTurns: false);
        Assert.False(realtime.CommitDay(0, Array.Empty<uint>()).Accepted);
    }

    [Fact]
    public void ADayUsesOneFrozenFluxExposureAndOneFinalSolve()
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        var before = session.Snapshot;
        var projection = session.CurrentSpatialCandidate;
        var expectedCore = session.CoreState.TryAddFissionEnergy(projection.ShapeNodePowerWatts.Select(w => w * 86400).ToArray()).Value;
        var expectedPoison = session.CurrentXenonState.Advance(projection, 1, 86400).BindBurnupReference(expectedCore);
        double expectedScore = PracticeScoring.OperatingPoints(86400, before.Ripple.RmsDeviationFraction);
        var day = session.CommitDay(0, Array.Empty<uint>());
        Assert.True(day.Accepted, day.DiagnosticMessage);
        Assert.Equal(1u, day.Snapshot.TurnSummaryCount);
        Assert.Equal(before.Physics.BindingVersion + 2, day.Snapshot.Physics.BindingVersion);
        Assert.Equal(GameSession.DailyIntegrationId, day.Snapshot.Physics.CadenceIdentity);
        Assert.Equal(expectedPoison.Iodine, session.CurrentXenonState.Iodine);
        Assert.Equal(expectedPoison.Xenon, session.CurrentXenonState.Xenon);
        Assert.Equal(expectedCore.EnumerateBundles().Select(b => b.CurrentBurnupJPerKgHm), session.CoreState.EnumerateBundles().Select(b => b.CurrentBurnupJPerKgHm));
        Assert.Equal(expectedCore.EnumerateBundles().Select(b => b.StateVersion), session.CoreState.EnumerateBundles().Select(b => b.StateVersion));
        Assert.Equal(expectedScore, day.Snapshot.ScoreTotal);
        Assert.Equal(86400, day.Snapshot.Xenon.SimulationTimeSeconds);
        Assert.Equal(86400, session.CurrentLiquidZoneRrs.SimulationTimeSeconds);
    }

    [Fact]
    public void PlansExecuteCanonicallyAndReplayDeterministically()
    {
        var first = PracticeGameSessionFactory.CreateBrowserPlaytest();
        var second = PracticeGameSessionFactory.CreateBrowserPlaytest();
        var a = first.CommitDay(0, new uint[] { 211, 210 });
        var b = second.CommitDay(0, new uint[] { 210, 211 });
        Assert.True(a.Accepted, a.DiagnosticMessage); Assert.True(b.Accepted, b.DiagnosticMessage);
        Assert.Equal(a.Snapshot.SimulationTimeSeconds, b.Snapshot.SimulationTimeSeconds);
        Assert.Equal(a.Snapshot.ScoreTotal, b.Snapshot.ScoreTotal);
        Assert.Equal(first.CurrentSpatialCandidate.ShapeChannelPowerWatts, second.CurrentSpatialCandidate.ShapeChannelPowerWatts);
        Assert.Equal(first.CurrentXenonState.Xenon, second.CurrentXenonState.Xenon);
        Assert.Equal(new uint[] { 210, 211 }.Take(a.Snapshot.LastDayResult!.Movements.Count), a.Snapshot.LastDayResult.ExecutedChannels);
    }

    [Fact]
    public void ChallengeHorizonAndTerminalFuelMovesStopTheTurn()
    {
        var challenge = PracticeGameSessionFactory.CreateBrowserPlaytest(challenge: true);
        var day = challenge.CommitDay(0, Array.Empty<uint>());
        Assert.True(day.Accepted);
        Assert.Equal(86400, day.Snapshot.SimulationTimeSeconds);
        Assert.True(day.Snapshot.IsGameOver);
        Assert.Equal("missed", day.Snapshot.Shift.Outcome);
        Assert.False(day.Snapshot.Shift.RewardEarned);
        Assert.False(challenge.CommitDay(1, Array.Empty<uint>()).Accepted);

        var overfuelled = PracticeGameSessionFactory.CreateBrowserPlaytest();
        var terminal = overfuelled.CommitDay(0, Enumerable.Range(0, 380).Select(i => (uint)i).ToArray());
        Assert.True(terminal.Accepted, terminal.DiagnosticMessage);
        Assert.True(terminal.Snapshot.IsGameOver);
        Assert.Equal(0, terminal.Snapshot.SimulationTimeSeconds);
        Assert.Equal(0u, terminal.Snapshot.CompletedDays);
        Assert.NotEmpty(terminal.Snapshot.LastDayResult!.UnexecutedChannels);
        Assert.Equal(terminal.Snapshot.LastDayResult.ExecutedChannels.Count * 8u, terminal.Snapshot.Shift.FuelConsumed);
    }

    [Fact]
    public void NumericalFailureAfterFuelMovesRollsBackTheWholeDay()
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        typeof(GameSession).GetField("_powerProjectionVersion", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(session, ulong.MaxValue - 1);
        var before = session.CoreState;
        var projection = session.CurrentSpatialCandidate;
        var poison = session.CurrentXenonState;
        // Refuelling can use the final version; the first time step cannot.
        var failed = session.CommitDay(0, new uint[] { 210 });
        Assert.False(failed.Accepted);
        Assert.Same(before, session.CoreState); Assert.Same(projection, session.CurrentSpatialCandidate);
        Assert.Same(poison, session.CurrentXenonState);
        Assert.Equal(0u, session.Snapshot.Shift.FuelConsumed);
        Assert.Equal(0, session.Snapshot.SimulationTimeSeconds);
        Assert.Null(session.Snapshot.LastDayResult);
    }

    [Fact]
    public void DailyCandidateRemainsDetachedAndRejectsAChangedOwner()
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        var before = session.CoreState;
        var operation = session.BeginDay(0, new uint[] { 210 });
        Assert.Null(operation.Result);
        Assert.Equal(0, operation.SimulationSecondsAdvanced);
        Assert.Same(before, session.CoreState); Assert.Equal(0, session.Snapshot.SimulationTimeSeconds);
        session.Pause(); // Changes the owner clock generation.
        operation.CalculateDay();
        Assert.False(operation.Result!.Accepted);
        Assert.Same(before, session.CoreState);
    }
}
