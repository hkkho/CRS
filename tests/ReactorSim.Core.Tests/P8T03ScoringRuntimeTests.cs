using System;
using System.Collections.Generic;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P8T03ScoringRuntimeTests
{
    [Fact]
    public void SurvivingTheHorizonProducesTheApprovedComponentBreakdown()
    {
        Phase8ScoredScenarioRuntimeV1 runtime = CreateRuntime(2.0);

        ContractValidationResult<Phase8ScoredAdvanceResultV1> result =
            runtime.TryAdvanceWallMilliseconds(200);

        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        Assert.Equal(Phase8ScenarioOutcomeV1.SurvivedScenarioHorizon, runtime.Outcome);
        Assert.Equal(2u, result.Value.Advance.ControlTicksProcessed);
        Assert.Equal(500.0, result.Value.Score.SurvivalPoints);
        Assert.Equal(1.0, result.Value.Score.EnergyQuality);
        Assert.Equal(250.0, result.Value.Score.EnergyPoints);
        Assert.Equal(1.0, result.Value.Score.StabilityQuality);
        Assert.Equal(150.0, result.Value.Score.StabilityPoints);
        Assert.Equal(1.0, result.Value.Score.FuellingEfficiency);
        Assert.Equal(50.0, result.Value.Score.FuellingEfficiencyPoints);
        Assert.Equal(950.0, result.Value.Score.TotalPoints);
        Assert.Equal("elapsed_play", result.Value.TurnSummary.Cause);
        Assert.Contains("scenario horizon survived", result.Value.TurnSummary.Effect, StringComparison.Ordinal);
        Assert.DoesNotContain("neutron", result.Value.TurnSummary.Effect, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActionAndScriptedEventAppearInTheSummaryAndControlUseIsPenalized()
    {
        Phase8ScoredScenarioRuntimeV1 runtime = CreateRuntime(
            2.0,
            new[] { Event(1.0, Phase8ScenarioEventKindV1.SetPowerTarget, 0.95) });
        Assert.True(runtime.TryQueuePowerTarget(0.95).IsValid);

        ContractValidationResult<Phase8ScoredAdvanceResultV1> first =
            runtime.TryAdvanceWallMilliseconds(100);
        Assert.True(first.IsValid, first.IsValid ? string.Empty : first.FirstDiagnostic.ToString());
        Assert.Equal(1u, first.Value.TurnSummary.CommittedActionCount);
        Assert.Equal(1u, first.Value.TurnSummary.ScriptedEventCount);
        Assert.Equal("scripted:SetPowerTarget", first.Value.TurnSummary.Cause);
        Assert.Equal(0.95, runtime.NormalizedPowerFraction);

        ContractValidationResult<Phase8ScoredAdvanceResultV1> second =
            runtime.TryAdvanceWallMilliseconds(100);
        Assert.True(second.IsValid, second.IsValid ? string.Empty : second.FirstDiagnostic.ToString());
        Assert.Equal(Phase8ScenarioOutcomeV1.SurvivedScenarioHorizon, runtime.Outcome);
        Assert.Equal(1u, second.Value.Score.CommittedActionCount);
        Assert.Equal(10.0, second.Value.Score.ControlPenaltyPoints);
        Assert.Equal(920.0, second.Value.Score.TotalPoints);
        Assert.Equal(2, runtime.TurnSummaries.Count);
    }

    [Fact]
    public void ScoreIsIndependentOfWallCommandChunking()
    {
        Phase8ScoredScenarioRuntimeV1 oneWindow = CreateRuntime(
            2.0,
            new[] { Event(1.0, Phase8ScenarioEventKindV1.SetPowerTarget, 0.95) });
        Phase8ScoredScenarioRuntimeV1 twoWindows = CreateRuntime(
            2.0,
            new[] { Event(1.0, Phase8ScenarioEventKindV1.SetPowerTarget, 0.95) });

        Assert.True(oneWindow.TryAdvanceWallMilliseconds(200).IsValid);
        Assert.True(twoWindows.TryAdvanceWallMilliseconds(100).IsValid);
        Assert.True(twoWindows.TryAdvanceWallMilliseconds(100).IsValid);

        Assert.Equal(oneWindow.Score.TotalPoints, twoWindows.Score.TotalPoints);
        Assert.Equal(oneWindow.Score.EnergyQuality, twoWindows.Score.EnergyQuality);
        Assert.Equal(oneWindow.Score.StabilityQuality, twoWindows.Score.StabilityQuality);
        Assert.Equal(oneWindow.Outcome, twoWindows.Outcome);
        Assert.Equal(oneWindow.Runtime.SimulationStepIndex, twoWindows.Runtime.SimulationStepIndex);
    }

    [Fact]
    public void StateSegmentsCaptureMidTickTransitionsAndRemainChunkInvariant()
    {
        Phase8ScoredScenarioRuntimeV1 oneWindow = CreateRuntime(
            1.0,
            new[] { Event(0.23, Phase8ScenarioEventKindV1.SetPowerTarget, 0.949) });
        Phase8ScoredScenarioRuntimeV1 splitWindow = CreateRuntime(
            1.0,
            new[] { Event(0.23, Phase8ScenarioEventKindV1.SetPowerTarget, 0.949) });

        ContractValidationResult<Phase8ScoredAdvanceResultV1> oneResult =
            oneWindow.TryAdvanceWallMilliseconds(100);
        Assert.True(oneResult.IsValid, oneResult.IsValid ? string.Empty : oneResult.FirstDiagnostic.ToString());
        Assert.Equal(2, oneResult.Value.Advance.StateSegments.Count);
        Assert.Equal(0.23, oneResult.Value.Advance.StateSegments[0].SimulationTimeEndSeconds);
        Assert.Equal(0.949, oneResult.Value.Advance.StateSegments[1].NormalizedPowerFraction);

        Assert.True(splitWindow.TryAdvanceWallMilliseconds(50).IsValid);
        ContractValidationResult<Phase8ScoredAdvanceResultV1> splitResult =
            splitWindow.TryAdvanceWallMilliseconds(50);
        Assert.True(splitResult.IsValid, splitResult.IsValid ? string.Empty : splitResult.FirstDiagnostic.ToString());

        Assert.Equal(0.961, oneResult.Value.Score.EnergyQuality);
        Assert.Equal(0.961, oneResult.Value.Score.StabilityQuality);
        Assert.Equal(240.183, oneResult.Value.Score.EnergyPoints);
        Assert.Equal(144.11, oneResult.Value.Score.StabilityPoints);
        Assert.Equal(934.292, oneResult.Value.Score.TotalPoints);
        Assert.Equal(oneResult.Value.Score.TotalPoints, splitResult.Value.Score.TotalPoints);
        Assert.Equal(oneResult.Value.Score.EnergyQuality, splitResult.Value.Score.EnergyQuality);
        Assert.Equal(oneResult.Value.Score.StabilityQuality, splitResult.Value.Score.StabilityQuality);
    }

    [Fact]
    public void SummaryEventCapacityFailsClosed()
    {
        Phase8ScoredScenarioRuntimeV1 runtime = CreateRuntime(
            2.0,
            new[] { Event(1.0, Phase8ScenarioEventKindV1.SetPowerTarget, 0.95) },
            1);
        Assert.True(runtime.TryQueuePowerTarget(0.95).IsValid);
        double scoreBefore = runtime.Score.TotalPoints;

        ContractValidationResult<Phase8ScoredAdvanceResultV1> result =
            runtime.TryAdvanceWallMilliseconds(100);

        Assert.False(result.IsValid);
        Assert.Equal("Phase8ScoredRuntime.SummaryCapacity.Exceeded", result.FirstDiagnostic.Code);
        Assert.Equal(0.0, runtime.SimulationTimeSeconds);
        Assert.Equal(0ul, runtime.SimulationStepIndex);
        Assert.Equal(0u, runtime.ProcessedScriptedEventCount);
        Assert.Equal(1u, runtime.PendingActionCount);
        Assert.Equal(Phase8ScenarioOutcomeV1.Running, runtime.Outcome);
        Assert.Empty(runtime.TurnSummaries);
        Assert.Equal(scoreBefore, runtime.Score.TotalPoints);
    }

    [Fact]
    public void ScoringParameterFactoryRejectsNonfiniteAndInvalidBounds()
    {
        Assert.False(Phase8ScoringParametersV1.TryCreate(
            double.NaN, 1000, 500, 250, 150, 50, 10, 250, 1, 3, 16).IsValid);
        Assert.False(Phase8ScoringParametersV1.TryCreate(
            0, 0, 500, 250, 150, 50, 10, 250, 1, 3, 16).IsValid);
        Assert.False(Phase8ScoringParametersV1.TryCreate(
            0, 1000, 500, 250, 150, 50, 10, 250, 0, 3, 16).IsValid);
        Assert.False(Phase8ScoringParametersV1.TryCreate(
            0, 1000, 500, 250, 150, 50, 10, 250, 1, 3, 0).IsValid);
    }

    private static Phase8ScoredScenarioRuntimeV1 CreateRuntime(
        double horizonSeconds,
        IEnumerable<Phase8ScenarioEventV1>? scriptedEvents = null,
        uint maximumSummaryEventsPerTurn = 16)
    {
        Phase8TimeModelV1 timeModel = Require(
            Phase8TimeModelV1.TryCreate(100, 1.0, 10.0, 10.0, true));
        Phase8PlaybackModeV1 playbackMode = Require(
            Phase8PlaybackModeV1.TryCreate("play-accelerated-10x", 10.0, timeModel));
        Phase8OperatingEnvelopeV1 envelope = Require(
            Phase8OperatingEnvelopeV1.TryCreate(
                0.80, 1.20, 0.0, 0.20, -0.20, 0.20, 0.0, 1.0));
        Phase8DifficultyProfileV1 profile = Require(
            Phase8DifficultyProfileV1.TryCreate(
                "practice", horizonSeconds, Math.Min(1.0, horizonSeconds), 4, 6, envelope));
        Phase8ScenarioInitialStateV1 initialState = Require(
            Phase8ScenarioInitialStateV1.TryCreate(1.0, 0.0, 0.0, 1.0, 6));
        Phase8ScenarioDefinitionV1 scenario = Require(
            Phase8ScenarioDefinitionV1.TryCreate(
                "score-test",
                "practice",
                7001,
                initialState,
                scriptedEvents ?? Array.Empty<Phase8ScenarioEventV1>()));
        Phase8ScenarioRuntimeV1 baseRuntime = Require(
            Phase8ScenarioRuntimeV1.TryCreate(scenario, profile, timeModel, playbackMode));
        Phase8ScoringParametersV1 parameters = Require(
            Phase8ScoringParametersV1.TryCreate(
                0,
                1000,
                500,
                250,
                150,
                50,
                10,
                250,
                1,
                3,
                maximumSummaryEventsPerTurn));
        return Require(Phase8ScoredScenarioRuntimeV1.TryCreate(baseRuntime, parameters));
    }

    private static Phase8ScenarioEventV1 Event(
        double atSeconds,
        Phase8ScenarioEventKindV1 kind,
        double? powerTarget = null)
    {
        return Require(Phase8ScenarioEventV1.TryCreate(
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
