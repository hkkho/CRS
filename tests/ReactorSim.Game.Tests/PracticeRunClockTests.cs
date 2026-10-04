using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class PracticeRunClockTests
{
    [Fact]
    public void EndlessClockContinuesPastOneHundredDaysAndStillHonorsOperatingLoss()
    {
        var model = Phase8TimeModelV1.TryCreate(100, 518400, 864000, 5184000, true).Value;
        var mode = Phase8PlaybackModeV1.TryCreate("test", 864000, model).Value;
        var clock = new PracticeRunClock("test", 1001, 0, model, mode);
        Assert.True(clock.TryCommitAdvance(clock.TryPlanAdvanceWallMilliseconds(10200).Value).IsValid);
        Assert.Equal(102 * 86400, clock.SimulationTimeSeconds);
        Assert.Equal(PracticeRunOutcome.Running, clock.Outcome);
        clock.TryQueuePowerTarget(.7);
        Assert.True(clock.TryCommitAdvance(clock.TryPlanAdvanceWallMilliseconds(100).Value).IsValid);
        Assert.Equal(PracticeRunOutcome.RecordLoss, clock.Outcome);
        Assert.Equal(102 * 86400, clock.SimulationTimeSeconds);
    }

    private static PracticeRunClock Create(double horizon = 600)
    {
        var model = Phase8TimeModelV1.TryCreate(100, 6, 10, 60, true).Value;
        var mode = Phase8PlaybackModeV1.TryCreate("test", 10, model).Value;
        return new PracticeRunClock("test", 1001, horizon, model, mode);
    }

    [Fact]
    public void PartialTicksPreserveTargetsAndCommitOnlyOnce()
    {
        var clock = Create();
        Assert.True(clock.TryQueuePowerTarget(.95).IsValid);
        var partial = clock.TryPlanAdvanceWallMilliseconds(40).Value;
        Assert.Equal(0, clock.WallElapsedSeconds);
        Assert.Equal(1u, clock.PendingActionCount);
        Assert.Empty(partial.StateSegments);
        Assert.True(clock.TryCommitAdvance(partial).IsValid);
        Assert.False(clock.TryCommitAdvance(partial).IsValid);
        var complete = clock.TryPlanAdvanceWallMilliseconds(60).Value;
        Assert.Single(complete.StateSegments);
        Assert.Equal(.95, complete.StateSegments[0].NormalizedPowerFraction);
        Assert.True(clock.TryCommitAdvance(complete).IsValid);
        Assert.Equal(1, clock.SimulationTimeSeconds);
        Assert.Equal(.1, clock.WallElapsedSeconds);
        Assert.Equal(0u, clock.PendingActionCount);
        Assert.Equal(2u, clock.TurnSummaryCount);
    }

    [Fact]
    public void PauseInvalidatesCandidateAndQueueCapacityPreservesAcceptedTargets()
    {
        var clock = Create();
        for (int i = 0; i < 4; i++) Assert.True(clock.TryQueuePowerTarget(1).IsValid);
        Assert.False(clock.TryQueuePowerTarget(.95).IsValid);
        Assert.Equal(4u, clock.PendingActionCount);
        var plan = clock.TryPlanAdvanceWallMilliseconds(100).Value;
        clock.TryPause();
        Assert.False(clock.TryCommitAdvance(plan).IsValid);
        Assert.False(clock.TryQueuePowerTarget(1).IsValid);
        var paused = clock.TryPlanAdvanceWallMilliseconds(1000).Value;
        Assert.Empty(paused.StateSegments);
        Assert.True(clock.TryCommitAdvance(paused).IsValid);
        Assert.Equal(0, clock.WallElapsedSeconds);
        clock.TryResume();
        Assert.True(clock.TryCommitAdvance(clock.TryPlanAdvanceWallMilliseconds(100).Value).IsValid);
        Assert.Equal(1, clock.SimulationTimeSeconds);
    }

    [Fact]
    public void HorizonAndLossStopAtTheSameControlTick()
    {
        var clock = Create(2.5);
        var plan = clock.TryPlanAdvanceWallMilliseconds(1000).Value;
        Assert.Equal(3, plan.StateSegments.Count);
        Assert.True(clock.TryCommitAdvance(plan).IsValid);
        Assert.Equal(2.5, clock.SimulationTimeSeconds);
        Assert.Equal(.3, clock.WallElapsedSeconds);
        Assert.Equal(PracticeRunOutcome.SurvivedScenarioHorizon, clock.Outcome);
        var loss = Create();
        loss.TryQueuePowerTarget(.7);
        var failed = loss.TryPlanAdvanceWallMilliseconds(1000).Value;
        Assert.Empty(failed.StateSegments);
        Assert.True(loss.TryCommitAdvance(failed).IsValid);
        Assert.Equal(0, loss.SimulationTimeSeconds);
        Assert.Equal(.1, loss.WallElapsedSeconds);
        Assert.Equal(PracticeRunOutcome.RecordLoss, loss.Outcome);
        Assert.False(clock.TryCommitAdvance(failed).IsValid);
    }
}
