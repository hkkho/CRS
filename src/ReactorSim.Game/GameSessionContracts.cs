using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ReactorSim.Core;

namespace ReactorSim.Game
{
    public sealed class GameSessionSnapshot
    {
        internal GameSessionSnapshot(
            string scenarioId,
            string difficultyId,
            ulong seed,
            string playbackModeId,
            double accelerationFactor,
            uint wallControlTickMilliseconds,
            double scenarioHorizonSeconds,
            double simulationTimeSeconds,
            double wallElapsedSeconds,
            double normalizedPowerFraction,
            double axialTiltFraction,
            double rrsReserveFraction,
            double deviceAvailableFraction,
            uint refuelRequestsRemaining,
            uint pendingActionCount,
            uint processedScriptedEventCount,
            double scoreTotal,
            uint turnSummaryCount,
            string outcomeId,
            bool isGameOver,
            string gameOverReason,
            bool isPaused,
            uint freshBundlesAvailable,
            uint refuellingOperationCount,
            int lastRefuelledChannel,
            string lastRefuellingDirectionId,
            ushort lastRefuellingShiftCount,
            double? lastDischargedMaximumBurnupMwDayPerKg,
            double? maximumDischargedBurnupMwDayPerKg,
            GameCorePresentationSnapshot core,
            RefuellingScoreBreakdown? lastRefuellingScore,
            ShiftProgress shift,
            RefuellingMovement? lastFuelMovement, RunProvenance provenance)
        {
            ScenarioId = scenarioId;
            DifficultyId = difficultyId;
            Seed = seed;
            PlaybackModeId = playbackModeId;
            AccelerationFactor = accelerationFactor;
            WallControlTickMilliseconds = wallControlTickMilliseconds;
            ScenarioHorizonSeconds = scenarioHorizonSeconds;
            SimulationTimeSeconds = simulationTimeSeconds;
            WallElapsedSeconds = wallElapsedSeconds;
            NormalizedPowerFraction = normalizedPowerFraction;
            AxialTiltFraction = axialTiltFraction;
            RrsReserveFraction = rrsReserveFraction;
            DeviceAvailableFraction = deviceAvailableFraction;
            RefuelRequestsRemaining = refuelRequestsRemaining;
            PendingActionCount = pendingActionCount;
            ProcessedScriptedEventCount = processedScriptedEventCount;
            ScoreTotal = scoreTotal;
            LastRefuellingScore = lastRefuellingScore;
            Shift = shift;
            LastFuelMovement = lastFuelMovement;
            Provenance = provenance;
            TurnSummaryCount = turnSummaryCount;
            OutcomeId = outcomeId;
            IsGameOver = isGameOver;
            GameOverReason = gameOverReason;
            IsPaused = isPaused;
            FreshBundlesAvailable = freshBundlesAvailable;
            RefuellingOperationCount = refuellingOperationCount;
            LastRefuelledChannel = lastRefuelledChannel;
            LastRefuellingDirectionId = lastRefuellingDirectionId;
            LastRefuellingShiftCount = lastRefuellingShiftCount;
            LastDischargedMaximumBurnupMwDayPerKg = lastDischargedMaximumBurnupMwDayPerKg;
            MaximumDischargedBurnupMwDayPerKg = maximumDischargedBurnupMwDayPerKg;
            Core = core ?? throw new ArgumentNullException(nameof(core));
            Physics = Core.Physics;
            Xenon = Core.Xenon;
            Rrs = Core.Rrs;
        }

        public RunProvenance Provenance { get; }
        public ShiftProgress Shift { get; }
        public RefuellingMovement? LastFuelMovement { get; }
        public IReadOnlyList<GameRefuellingPlanV1> RefuellingPlans { get; } = System.Array.AsReadOnly(new[] {
            new GameRefuellingPlanV1(GameRefuellingDirectionV1.TowardEndA, 4),
            new GameRefuellingPlanV1(GameRefuellingDirectionV1.TowardEndB, 4),
            new GameRefuellingPlanV1(GameRefuellingDirectionV1.TowardEndA, 8),
            new GameRefuellingPlanV1(GameRefuellingDirectionV1.TowardEndB, 8) });

        public string ScenarioId { get; }

        public ulong Seed { get; }

        public string DifficultyId { get; }

        public string PlaybackModeId { get; }

        public double AccelerationFactor { get; }

        public uint WallControlTickMilliseconds { get; }

        public double ScenarioHorizonSeconds { get; }

        public double SimulationTimeSeconds { get; }

        public double WallElapsedSeconds { get; }

        public double NormalizedPowerFraction { get; }

        public double AxialTiltFraction { get; }

        public double RrsReserveFraction { get; }

        public double DeviceAvailableFraction { get; }

        public uint RefuelRequestsRemaining { get; }

        public uint PendingActionCount { get; }

        public uint ProcessedScriptedEventCount { get; }

        public double ScoreTotal { get; }

        public string ScorePolicyId { get; } = PracticeScoring.PolicyId;

        public RefuellingScoreBreakdown? LastRefuellingScore { get; }

        public uint TurnSummaryCount { get; }

        public string OutcomeId { get; }

        public string RunStatus => IsGameOver
            ? OutcomeId == nameof(PracticeRunOutcome.SurvivedScenarioHorizon) && !Rrs.IsGameOver
                ? "completed" : "ended"
            : IsPaused ? "paused" : "running";

        public bool IsGameOver { get; }

        public string GameOverReason { get; }

        public bool IsPaused { get; }

        public uint FreshBundlesAvailable { get; }

        public uint RefuellingOperationCount { get; }

        public int LastRefuelledChannel { get; }

        public string LastRefuellingDirectionId { get; }

        public ushort LastRefuellingShiftCount { get; }

        public double? LastDischargedMaximumBurnupMwDayPerKg { get; }

        public double? MaximumDischargedBurnupMwDayPerKg { get; }

        public GameCorePresentationSnapshot Core { get; }

        public GamePhysicsPresentationSnapshot Physics { get; }

        public GameXenonPresentationSnapshot Xenon { get; }

        public GameRrsPresentationSnapshot Rrs { get; }
    }

    public sealed class GameSessionCommandResult
    {
        internal GameSessionCommandResult(
            bool accepted,
            string diagnosticCode,
            string diagnosticMessage,
            string message,
            GameSessionSnapshot snapshot)
        {
            Accepted = accepted;
            DiagnosticCode = diagnosticCode;
            DiagnosticMessage = diagnosticMessage;
            Message = message;
            Snapshot = snapshot;
        }

        public bool Accepted { get; }

        public string DiagnosticCode { get; }

        public string DiagnosticMessage { get; }

        public string Message { get; }

        public GameSessionSnapshot Snapshot { get; }
    }

}
