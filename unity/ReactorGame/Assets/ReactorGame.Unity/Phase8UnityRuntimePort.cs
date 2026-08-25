using System;

namespace ReactorGame.Unity
{
    /// <summary>
    /// The presentation-facing command vocabulary for the approved Phase 8
    /// runtime. The Unity adapter forwards these commands; it does not apply
    /// them or own simulation state.
    /// </summary>
    public enum Phase8UnityCommandKindV1 : byte
    {
        AdvanceWallMilliseconds = 0,
        QueuePowerTarget = 1,
        QueueTiltTarget = 2,
        SetPlaybackMode = 3,
        Pause = 4,
        Resume = 5
    }

    /// <summary>
    /// Immutable, sequence-numbered input sent from a Unity presentation to a
    /// runtime port. Wall time is deliberately an explicit integer duration.
    /// </summary>
    public sealed class Phase8UnityInputCommandV1
    {
        private Phase8UnityInputCommandV1(
            ulong sequence,
            Phase8UnityCommandKindV1 kind,
            ulong wallMilliseconds,
            double targetFraction,
            string playbackModeId)
        {
            if (sequence == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence), "A command sequence must be positive.");
            }

            Sequence = sequence;
            Kind = kind;
            WallMilliseconds = wallMilliseconds;
            TargetFraction = targetFraction;
            PlaybackModeId = playbackModeId;
        }

        public ulong Sequence { get; }

        public Phase8UnityCommandKindV1 Kind { get; }

        public ulong WallMilliseconds { get; }

        public double TargetFraction { get; }

        public string PlaybackModeId { get; }

        public static Phase8UnityInputCommandV1 AdvanceWallMilliseconds(
            ulong sequence,
            ulong wallMilliseconds)
        {
            return new Phase8UnityInputCommandV1(
                sequence,
                Phase8UnityCommandKindV1.AdvanceWallMilliseconds,
                wallMilliseconds,
                0.0,
                null);
        }

        public static Phase8UnityInputCommandV1 QueuePowerTarget(
            ulong sequence,
            double targetFraction)
        {
            RequireFinite(targetFraction, nameof(targetFraction));
            return new Phase8UnityInputCommandV1(
                sequence,
                Phase8UnityCommandKindV1.QueuePowerTarget,
                0,
                targetFraction,
                null);
        }

        public static Phase8UnityInputCommandV1 QueueTiltTarget(
            ulong sequence,
            double targetFraction)
        {
            RequireFinite(targetFraction, nameof(targetFraction));
            return new Phase8UnityInputCommandV1(
                sequence,
                Phase8UnityCommandKindV1.QueueTiltTarget,
                0,
                targetFraction,
                null);
        }

        public static Phase8UnityInputCommandV1 SetPlaybackMode(
            ulong sequence,
            string playbackModeId)
        {
            if (string.IsNullOrWhiteSpace(playbackModeId))
            {
                throw new ArgumentException("A playback mode identifier is required.", nameof(playbackModeId));
            }

            return new Phase8UnityInputCommandV1(
                sequence,
                Phase8UnityCommandKindV1.SetPlaybackMode,
                0,
                0.0,
                playbackModeId);
        }

        public static Phase8UnityInputCommandV1 Pause(ulong sequence)
        {
            return new Phase8UnityInputCommandV1(
                sequence,
                Phase8UnityCommandKindV1.Pause,
                0,
                0.0,
                null);
        }

        public static Phase8UnityInputCommandV1 Resume(ulong sequence)
        {
            return new Phase8UnityInputCommandV1(
                sequence,
                Phase8UnityCommandKindV1.Resume,
                0,
                0.0,
                null);
        }

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "The command value must be finite.");
            }
        }
    }

    /// <summary>
    /// Immutable presentation projection of the engine-neutral Phase 8
    /// runtime. Values are already validated by the runtime port that owns
    /// the simulation state.
    /// </summary>
    public sealed class Phase8UnityPresentationSnapshotV1
    {
        public Phase8UnityPresentationSnapshotV1(
            string scenarioId,
            string difficultyId,
            string playbackModeId,
            double accelerationFactor,
            uint wallControlTickMilliseconds,
            double scenarioHorizonSeconds,
            double simulationTimeSeconds,
            double wallElapsedSeconds,
            double normalizedPowerFraction,
            double absoluteTiltFraction,
            double controlMarginFraction,
            double deviceAvailableFraction,
            uint refuelRequestsRemaining,
            uint pendingActionCount,
            uint processedScriptedEventCount,
            double scoreTotal,
            uint turnSummaryCount,
            string outcomeId,
            bool isPaused)
        {
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                throw new ArgumentException("A snapshot requires a scenario identifier.", nameof(scenarioId));
            }

            if (string.IsNullOrWhiteSpace(difficultyId))
            {
                throw new ArgumentException("A snapshot requires a difficulty identifier.", nameof(difficultyId));
            }

            if (string.IsNullOrWhiteSpace(playbackModeId))
            {
                throw new ArgumentException("A snapshot requires a playback mode identifier.", nameof(playbackModeId));
            }

            if (string.IsNullOrWhiteSpace(outcomeId))
            {
                throw new ArgumentException("A snapshot requires an outcome identifier.", nameof(outcomeId));
            }

            if (wallControlTickMilliseconds == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(wallControlTickMilliseconds),
                    "The approved wall control tick must be positive milliseconds.");
            }

            RequireFinite(accelerationFactor, nameof(accelerationFactor));
            RequireFinite(scenarioHorizonSeconds, nameof(scenarioHorizonSeconds));
            RequireFinite(simulationTimeSeconds, nameof(simulationTimeSeconds));
            RequireFinite(wallElapsedSeconds, nameof(wallElapsedSeconds));
            RequireFinite(normalizedPowerFraction, nameof(normalizedPowerFraction));
            RequireFinite(absoluteTiltFraction, nameof(absoluteTiltFraction));
            RequireFinite(controlMarginFraction, nameof(controlMarginFraction));
            RequireFinite(deviceAvailableFraction, nameof(deviceAvailableFraction));
            RequireFinite(scoreTotal, nameof(scoreTotal));

            ScenarioId = scenarioId;
            DifficultyId = difficultyId;
            PlaybackModeId = playbackModeId;
            AccelerationFactor = accelerationFactor;
            WallControlTickMilliseconds = wallControlTickMilliseconds;
            ScenarioHorizonSeconds = scenarioHorizonSeconds;
            SimulationTimeSeconds = simulationTimeSeconds;
            WallElapsedSeconds = wallElapsedSeconds;
            NormalizedPowerFraction = normalizedPowerFraction;
            AbsoluteTiltFraction = absoluteTiltFraction;
            ControlMarginFraction = controlMarginFraction;
            DeviceAvailableFraction = deviceAvailableFraction;
            RefuelRequestsRemaining = refuelRequestsRemaining;
            PendingActionCount = pendingActionCount;
            ProcessedScriptedEventCount = processedScriptedEventCount;
            ScoreTotal = scoreTotal;
            TurnSummaryCount = turnSummaryCount;
            OutcomeId = outcomeId;
            IsPaused = isPaused;
        }

        public string ScenarioId { get; }

        public string DifficultyId { get; }

        public string PlaybackModeId { get; }

        public double AccelerationFactor { get; }

        public uint WallControlTickMilliseconds { get; }

        public double ScenarioHorizonSeconds { get; }

        public double SimulationTimeSeconds { get; }

        public double WallElapsedSeconds { get; }

        public double NormalizedPowerFraction { get; }

        public double AbsoluteTiltFraction { get; }

        public double ControlMarginFraction { get; }

        public double DeviceAvailableFraction { get; }

        public uint RefuelRequestsRemaining { get; }

        public uint PendingActionCount { get; }

        public uint ProcessedScriptedEventCount { get; }

        public double ScoreTotal { get; }

        public uint TurnSummaryCount { get; }

        public string OutcomeId { get; }

        public bool IsPaused { get; }

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "The snapshot value must be finite.");
            }
        }
    }

    /// <summary>
    /// Result returned by the runtime port after one explicit input command.
    /// Rejected commands remain observable to the UI without throwing through
    /// the presentation loop.
    /// </summary>
    public sealed class Phase8UnityCommandResultV1
    {
        private Phase8UnityCommandResultV1(
            ulong sequence,
            Phase8UnityCommandKindV1 kind,
            bool accepted,
            string diagnosticCode,
            string diagnosticMessage,
            Phase8UnityPresentationSnapshotV1 snapshot)
        {
            Sequence = sequence;
            Kind = kind;
            Accepted = accepted;
            DiagnosticCode = diagnosticCode;
            DiagnosticMessage = diagnosticMessage;
            Snapshot = snapshot;
        }

        public ulong Sequence { get; }

        public Phase8UnityCommandKindV1 Kind { get; }

        public bool Accepted { get; }

        public string DiagnosticCode { get; }

        public string DiagnosticMessage { get; }

        public Phase8UnityPresentationSnapshotV1 Snapshot { get; }

        public static Phase8UnityCommandResultV1 AcceptedResult(
            Phase8UnityInputCommandV1 command,
            Phase8UnityPresentationSnapshotV1 snapshot)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new Phase8UnityCommandResultV1(
                command.Sequence,
                command.Kind,
                true,
                null,
                null,
                snapshot);
        }

        public static Phase8UnityCommandResultV1 RejectedResult(
            Phase8UnityInputCommandV1 command,
            string diagnosticCode,
            string diagnosticMessage,
            Phase8UnityPresentationSnapshotV1 snapshot = null)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            return RejectedResult(
                command.Sequence,
                command.Kind,
                diagnosticCode,
                diagnosticMessage,
                snapshot);
        }

        public static Phase8UnityCommandResultV1 RejectedResult(
            ulong sequence,
            Phase8UnityCommandKindV1 kind,
            string diagnosticCode,
            string diagnosticMessage,
            Phase8UnityPresentationSnapshotV1 snapshot = null)
        {
            if (string.IsNullOrWhiteSpace(diagnosticCode))
            {
                throw new ArgumentException("A rejection requires a diagnostic code.", nameof(diagnosticCode));
            }

            return new Phase8UnityCommandResultV1(
                sequence,
                kind,
                false,
                diagnosticCode,
                diagnosticMessage ?? string.Empty,
                snapshot);
        }
    }

    /// <summary>
    /// Unity-to-runtime seam. An implementation may wrap the CLI/Core
    /// consumer, but it remains the sole owner of simulation transitions.
    /// </summary>
    public interface IPhase8RuntimePort
    {
        Phase8UnityPresentationSnapshotV1 Snapshot { get; }

        Phase8UnityCommandResultV1 Execute(Phase8UnityInputCommandV1 command);
    }
}
