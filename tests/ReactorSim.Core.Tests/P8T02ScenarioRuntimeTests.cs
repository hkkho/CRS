using System;
using System.Collections.Generic;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P8T02ScenarioRuntimeTests
{
    [Fact]
    public void TenTimesPlaybackAdvancesOneSimulationSecondPerHundredMilliseconds()
    {
        Phase8ScenarioRuntimeV1 runtime = CreateRuntime();

        ContractValidationResult<Phase8ScenarioAdvanceResultV1> halfTick =
            runtime.TryAdvanceWallMilliseconds(50);
        Assert.True(halfTick.IsValid);
        Assert.Equal(0.0, runtime.SimulationTimeSeconds);
        Assert.Equal(0u, halfTick.Value.ControlTicksProcessed);

        ContractValidationResult<Phase8ScenarioAdvanceResultV1> fullTick =
            runtime.TryAdvanceWallMilliseconds(50);
        Assert.True(fullTick.IsValid);
        Assert.Equal(1.0, runtime.SimulationTimeSeconds);
        Assert.Equal(1u, fullTick.Value.ControlTicksProcessed);
        Assert.Equal(0.1, runtime.WallElapsedSeconds, 12);
    }

    [Fact]
    public void PauseDiscardsNewWallElapsedButPreservesThePartialTickWindow()
    {
        Phase8ScenarioRuntimeV1 runtime = CreateRuntime();

        Assert.True(runtime.TryAdvanceWallMilliseconds(50).IsValid);
        Assert.Equal(0.05, runtime.WallElapsedSeconds, 12);

        Assert.True(runtime.TryPause().IsValid);
        ContractValidationResult<Phase8ScenarioAdvanceResultV1> paused =
            runtime.TryAdvanceWallMilliseconds(1000);
        Assert.True(paused.IsValid);
        Assert.Equal(0.0, runtime.SimulationTimeSeconds);
        Assert.Equal(0.05, runtime.WallElapsedSeconds, 12);
        Assert.Equal(0u, paused.Value.ControlTicksProcessed);

        Assert.True(runtime.TryResume().IsValid);
        Assert.True(runtime.TryAdvanceWallMilliseconds(50).IsValid);
        Assert.Equal(1.0, runtime.SimulationTimeSeconds);
        Assert.Equal(0.1, runtime.WallElapsedSeconds, 12);
    }

    [Fact]
    public void PlayerActionCommitsAtTheNextControlBoundary()
    {
        Phase8ScenarioRuntimeV1 runtime = CreateRuntime();
        ContractValidationResult<Phase8ActionQueueResultV1> queued =
            runtime.TryQueuePowerTarget(0.95);
        Assert.True(queued.IsValid);
        Assert.Equal(1u, queued.Value.PendingActionCount);

        Assert.True(runtime.TryAdvanceWallMilliseconds(50).IsValid);
        Assert.Equal(1.0, runtime.NormalizedPowerFraction);

        ContractValidationResult<Phase8ScenarioAdvanceResultV1> committed =
            runtime.TryAdvanceWallMilliseconds(50);
        Assert.True(committed.IsValid);
        Assert.Equal(0.95, runtime.NormalizedPowerFraction);
        Phase8ActionTransitionV1 transition = Assert.Single(committed.Value.ActionTransitions);
        Assert.Equal(1ul, transition.ActionId);
        Assert.Equal(0.0, transition.AcknowledgedWallTimeSeconds, 12);
        Assert.Equal(0.1, transition.CommittedWallTimeSeconds, 12);
        Assert.Equal(0.1, transition.QueueDelayWallTimeSeconds, 12);
        Assert.True(transition.QueueDelayWallTimeSeconds <= 0.1 + 1e-12);
    }

    [Fact]
    public void SameBoundaryActionCommitsBeforeTheScriptedEventAtThatSimulationTime()
    {
        Phase8ScenarioRuntimeV1 runtime = CreateRuntime(
            horizonSeconds: 20.0,
            scriptedEvents: new[]
            {
                Event(0.1, Phase8ScenarioEventKindV1.SetPowerTarget, powerTarget: 1.0)
            });
        Assert.True(runtime.TryQueuePowerTarget(0.95).IsValid);

        ContractValidationResult<Phase8ScenarioAdvanceResultV1> result =
            runtime.TryAdvanceWallMilliseconds(100);

        Assert.True(result.IsValid);
        Phase8ActionTransitionV1 transition = Assert.Single(result.Value.ActionTransitions);
        Assert.Equal(0.0, transition.AcknowledgedWallTimeSeconds, 12);
        Assert.Equal(0.1, transition.CommittedWallTimeSeconds, 12);
        Phase8EventRecordV1 scriptedEvent = Assert.Single(result.Value.EventRecords);
        Assert.Equal(0.1, scriptedEvent.SimulationTimeSeconds, 12);
        Assert.Equal(1.0, runtime.NormalizedPowerFraction);
    }

    [Fact]
    public void ApprovedPendingCommandCapacityRejectsTheNextAction()
    {
        Phase8ScenarioRuntimeV1 runtime = CreateRuntime(maximumPendingCommands: 1);

        Assert.True(runtime.TryQueuePowerTarget(0.95).IsValid);
        ContractValidationResult<Phase8ActionQueueResultV1> rejected =
            runtime.TryQueueTiltTarget(0.05);

        Assert.False(rejected.IsValid);
        Assert.Equal("Phase8Runtime.Action.Queue.Full", rejected.FirstDiagnostic.Code);
    }

    [Fact]
    public void InitialStateFactoryRejectsNonfiniteAndOutOfDomainValues()
    {
        Assert.False(
            Phase8ScenarioInitialStateV1.TryCreate(double.NaN, 0.0, 0.0, 1.0, 6).IsValid);
        Assert.False(
            Phase8ScenarioInitialStateV1.TryCreate(double.PositiveInfinity, 0.0, 0.0, 1.0, 6).IsValid);
        Assert.False(
            Phase8ScenarioInitialStateV1.TryCreate(1.0, -0.01, 0.0, 1.0, 6).IsValid);
        Assert.False(
            Phase8ScenarioInitialStateV1.TryCreate(1.0, 0.0, 0.0, 1.01, 6).IsValid);
    }

    [Theory]
    [InlineData(0.89, 0.0, 0.0, 1.0, "power_below_minimum")]
    [InlineData(1.11, 0.0, 0.0, 1.0, "power_above_maximum")]
    [InlineData(1.0, 0.11, 0.0, 1.0, "tilt_above_maximum")]
    [InlineData(1.0, 0.0, 0.11, 1.0, "control_margin_outside_envelope")]
    [InlineData(1.0, 0.0, 0.0, 0.0, "device_exhaustion")]
    public void EachInitialEnvelopeViolationRecordsItsApprovedLoss(
        double normalizedPowerFraction,
        double absoluteTiltFraction,
        double controlMarginFraction,
        double deviceAvailableFraction,
        string expectedLossId)
    {
        Phase8ScenarioRuntimeV1 runtime = CreateRuntime(
            normalizedPowerFraction: normalizedPowerFraction,
            absoluteTiltFraction: absoluteTiltFraction,
            controlMarginFraction: controlMarginFraction,
            deviceAvailableFraction: deviceAvailableFraction);

        Assert.Equal(Phase8ScenarioOutcomeV1.RecordLoss, runtime.Outcome);
        Phase8LossRecordV1 loss = Assert.Single(runtime.LossRecords);
        Assert.Equal(expectedLossId, loss.LossId);
    }

    [Fact]
    public void BoundaryScenarioRecordsOnlyTheApprovedLossAtTheExactEventTime()
    {
        Phase8ScenarioRuntimeV1 runtime = CreateRuntime(
            horizonSeconds: 20.0,
            scriptedEvents: new[]
            {
                Event(0.0, Phase8ScenarioEventKindV1.Inspect),
                Event(10.0, Phase8ScenarioEventKindV1.SetPowerTarget, powerTarget: 1.11)
            });

        ContractValidationResult<Phase8ScenarioAdvanceResultV1> result =
            runtime.TryAdvanceWallMilliseconds(1000);
        Assert.True(result.IsValid);
        Assert.Equal(Phase8ScenarioOutcomeV1.RecordLoss, runtime.Outcome);
        Phase8LossRecordV1 loss = Assert.Single(result.Value.LossRecords);
        Assert.Equal("power_above_maximum", loss.LossId);
        Assert.Equal(10.0, loss.SimulationTimeSeconds);
        Assert.Equal(10.0, runtime.SimulationTimeSeconds);
    }

    [Fact]
    public void IdenticalChunkingProducesIdenticalRuntimeOutcome()
    {
        Phase8ScenarioRuntimeV1 first = CreateRuntime();
        Phase8ScenarioRuntimeV1 second = CreateRuntime();

        Assert.True(first.TryAdvanceWallMilliseconds(1000).IsValid);
        Assert.True(second.TryAdvanceWallMilliseconds(50).IsValid);
        Assert.True(second.TryAdvanceWallMilliseconds(50).IsValid);
        Assert.True(second.TryAdvanceWallMilliseconds(900).IsValid);

        Assert.Equal(first.ReplaySeed, second.ReplaySeed);
        Assert.Equal(first.SimulationTimeSeconds, second.SimulationTimeSeconds);
        Assert.Equal(first.SimulationStepIndex, second.SimulationStepIndex);
        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(first.ProcessedScriptedEventCount, second.ProcessedScriptedEventCount);
        Assert.Equal(first.NormalizedPowerFraction, second.NormalizedPowerFraction);
        Assert.Equal(first.RefuelRequestsRemaining, second.RefuelRequestsRemaining);
    }

    private static Phase8ScenarioRuntimeV1 CreateRuntime(
        double horizonSeconds = 600.0,
        IEnumerable<Phase8ScenarioEventV1>? scriptedEvents = null,
        double normalizedPowerFraction = 1.0,
        double absoluteTiltFraction = 0.0,
        double controlMarginFraction = 0.0,
        double deviceAvailableFraction = 1.0,
        uint maximumPendingCommands = 4)
    {
        Phase8TimeModelV1 timeModel = Require(
            Phase8TimeModelV1.TryCreate(100, 1.0, 10.0, 10.0, true));
        Phase8PlaybackModeV1 playbackMode = Require(
            Phase8PlaybackModeV1.TryCreate("play-accelerated-10x", 10.0, timeModel));
        Phase8OperatingEnvelopeV1 envelope = Require(
            Phase8OperatingEnvelopeV1.TryCreate(
                0.90,
                1.10,
                0.0,
                0.10,
                -0.10,
                0.10,
                0.0,
                1.0));
        Phase8DifficultyProfileV1 profile = Require(
            Phase8DifficultyProfileV1.TryCreate(
                "practice",
                horizonSeconds,
                20.0,
                maximumPendingCommands,
                6,
                envelope));
        Phase8ScenarioInitialStateV1 initialState = Require(
            Phase8ScenarioInitialStateV1.TryCreate(
                normalizedPowerFraction,
                absoluteTiltFraction,
                controlMarginFraction,
                deviceAvailableFraction,
                6));
        Phase8ScenarioDefinitionV1 scenario = Require(
            Phase8ScenarioDefinitionV1.TryCreate(
                "runtime-test",
                "practice",
                1001,
                initialState,
                scriptedEvents ?? new[] { Event(0.0, Phase8ScenarioEventKindV1.Inspect) }));
        return Require(
            Phase8ScenarioRuntimeV1.TryCreate(scenario, profile, timeModel, playbackMode));
    }

    private static Phase8ScenarioEventV1 Event(
        double atSeconds,
        Phase8ScenarioEventKindV1 kind,
        double? powerTarget = null)
    {
        return Require(
            Phase8ScenarioEventV1.TryCreate(
                atSeconds,
                kind,
                powerTarget,
                null,
                null,
                null));
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }
}
