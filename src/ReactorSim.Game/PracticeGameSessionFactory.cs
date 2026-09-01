using System;
using System.Collections.Generic;
using ReactorSim.Core;

namespace ReactorSim.Game
{
    public static class PracticeGameSessionFactory
    {
        public const string ScenarioId = "tutorial-equilibrium";
        public const string DifficultyId = "practice";
        public const string RealTimePlaybackModeId = "audit-real-time-1x";
        public const string PlayPlaybackModeId = "play-accelerated-10x";
        public const uint WallControlTickMilliseconds = 100;

        public static GameSession Create()
        {
            Phase8TimeModelV1 timeModel = Require(
                Phase8TimeModelV1.TryCreate(
                    WallControlTickMilliseconds,
                    1.0,
                    10.0,
                    10.0,
                    true));
            Phase8PlaybackModeV1 realTime = Require(
                Phase8PlaybackModeV1.TryCreate(RealTimePlaybackModeId, 1.0, timeModel));
            Phase8PlaybackModeV1 play = Require(
                Phase8PlaybackModeV1.TryCreate(PlayPlaybackModeId, 10.0, timeModel));
            var playbackModes = new Dictionary<string, Phase8PlaybackModeV1>(StringComparer.Ordinal)
            {
                [realTime.ModeId] = realTime,
                [play.ModeId] = play
            };

            Phase8OperatingEnvelopeV1 envelope = Require(
                Phase8OperatingEnvelopeV1.TryCreate(
                    0.8,
                    1.2,
                    0.0,
                    0.2,
                    -0.2,
                    0.2,
                    0.0,
                    1.0));
            Phase8DifficultyProfileV1 profile = Require(
                Phase8DifficultyProfileV1.TryCreate(
                    DifficultyId,
                    600.0,
                    20.0,
                    4,
                    6,
                    envelope));
            Phase8ScenarioInitialStateV1 initialState = Require(
                Phase8ScenarioInitialStateV1.TryCreate(1.0, 0.0, 0.0, 1.0, 6));
            Phase8ScenarioEventV1[] events =
            {
                Require(Phase8ScenarioEventV1.TryCreate(
                    0.0,
                    Phase8ScenarioEventKindV1.Inspect,
                    null,
                    null,
                    null,
                    null)),
                Require(Phase8ScenarioEventV1.TryCreate(
                    120.0,
                    Phase8ScenarioEventKindV1.SetPowerTarget,
                    0.95,
                    null,
                    null,
                    null)),
                Require(Phase8ScenarioEventV1.TryCreate(
                    240.0,
                    Phase8ScenarioEventKindV1.RefuelRequest,
                    null,
                    null,
                    0,
                    2)),
                Require(Phase8ScenarioEventV1.TryCreate(
                    360.0,
                    Phase8ScenarioEventKindV1.SetPowerTarget,
                    1.0,
                    null,
                    null,
                    null))
            };
            Phase8ScenarioDefinitionV1 scenario = Require(
                Phase8ScenarioDefinitionV1.TryCreate(
                    ScenarioId,
                    DifficultyId,
                    1001,
                    initialState,
                    events));
            Phase8ScenarioRuntimeV1 runtime = Require(
                Phase8ScenarioRuntimeV1.TryCreate(scenario, profile, timeModel, play));
            Phase8ScoringParametersV1 scoring = Require(
                Phase8ScoringParametersV1.TryCreate(
                    0.0,
                    1000.0,
                    500.0,
                    250.0,
                    150.0,
                    50.0,
                    10.0,
                    250.0,
                    1.0,
                    3,
                    16));
            Phase8ScoredScenarioRuntimeV1 scoredRuntime = Require(
                Phase8ScoredScenarioRuntimeV1.TryCreate(runtime, scoring));
            return new GameSession(scoredRuntime, playbackModes, WallControlTickMilliseconds);
        }

        private static T Require<T>(ContractValidationResult<T> result)
        {
            if (!result.IsValid)
            {
                throw new InvalidOperationException(result.FirstDiagnostic.ToString());
            }

            return result.Value;
        }
    }
}
