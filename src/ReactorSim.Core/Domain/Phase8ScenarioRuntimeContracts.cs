using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ReactorSim.Core
{
    public enum Phase8ScenarioEventKindV1 : byte
    {
        Inspect = 0,
        SetPowerTarget = 1,
        SetTiltTarget = 2,
        RefuelRequest = 3
    }

    public enum Phase8ScenarioOutcomeV1 : byte
    {
        Running = 0,
        SurvivedScenarioHorizon = 1,
        RecordLoss = 2
    }

    public enum Phase8ActionKindV1 : byte
    {
        SetPowerTarget = 0,
        SetTiltTarget = 1
    }

    /// <summary>
    /// The approved Phase 8 presentation pacing authority. It supplies wall
    /// time only as an explicit client request; it never becomes a physics
    /// timestep or a replacement for the existing stable integration policy.
    /// </summary>
    public sealed class Phase8TimeModelV1
    {
        private Phase8TimeModelV1(
            uint wallControlTickMilliseconds,
            double maximumPresentationAdvancePerWallTickSeconds,
            double defaultAccelerationFactor,
            double maximumAccelerationFactor)
        {
            WallControlTickMilliseconds = wallControlTickMilliseconds;
            MaximumPresentationAdvancePerWallTickSeconds = maximumPresentationAdvancePerWallTickSeconds;
            DefaultAccelerationFactor = defaultAccelerationFactor;
            MaximumAccelerationFactor = maximumAccelerationFactor;
        }

        public uint WallControlTickMilliseconds { get; }

        public double WallControlTickSeconds
        {
            get { return WallControlTickMilliseconds / 1000.0; }
        }

        public double MaximumPresentationAdvancePerWallTickSeconds { get; }

        public double DefaultAccelerationFactor { get; }

        public double MaximumAccelerationFactor { get; }

        public static ContractValidationResult<Phase8TimeModelV1> TryCreate(
            uint wallControlTickMilliseconds,
            double maximumPresentationAdvancePerWallTickSeconds,
            double defaultAccelerationFactor,
            double maximumAccelerationFactor,
            bool wallClockMustNotDrivePhysics)
        {
            if (wallControlTickMilliseconds == 0)
            {
                return ContractValidationResult<Phase8TimeModelV1>.Invalid(
                    "Phase8TimeModel.WallTick.Invalid",
                    "wall_control_tick_ms",
                    "The wall control tick must be a positive number of milliseconds.");
            }

            if (!ContractValidation.IsFinite(maximumPresentationAdvancePerWallTickSeconds) ||
                maximumPresentationAdvancePerWallTickSeconds <= 0)
            {
                return ContractValidationResult<Phase8TimeModelV1>.Invalid(
                    "Phase8TimeModel.PresentationCap.Invalid",
                    "maximum_presentation_advance_per_wall_tick_s",
                    "The presentation advance cap must be finite and strictly positive SI seconds.");
            }

            if (!ContractValidation.IsFinite(defaultAccelerationFactor) || defaultAccelerationFactor <= 0)
            {
                return ContractValidationResult<Phase8TimeModelV1>.Invalid(
                    "Phase8TimeModel.DefaultFactor.Invalid",
                    "default_acceleration_factor_simulation_seconds_per_wall_second",
                    "The default acceleration factor must be finite and strictly positive.");
            }

            if (!ContractValidation.IsFinite(maximumAccelerationFactor) ||
                maximumAccelerationFactor < defaultAccelerationFactor)
            {
                return ContractValidationResult<Phase8TimeModelV1>.Invalid(
                    "Phase8TimeModel.MaximumFactor.Invalid",
                    "maximum_acceleration_factor_simulation_seconds_per_wall_second",
                    "The maximum acceleration factor must be finite and at least the default factor.");
            }

            if (!wallClockMustNotDrivePhysics)
            {
                return ContractValidationResult<Phase8TimeModelV1>.Invalid(
                    "Phase8TimeModel.WallClockBoundary.Invalid",
                    "wall_clock_must_not_drive_physics",
                    "Wall-clock pacing must not become an implicit physics input.");
            }

            double tickSeconds = wallControlTickMilliseconds / 1000.0;
            if (defaultAccelerationFactor * tickSeconds >
                maximumPresentationAdvancePerWallTickSeconds + 1e-12 ||
                maximumAccelerationFactor * tickSeconds >
                maximumPresentationAdvancePerWallTickSeconds + 1e-12)
            {
                return ContractValidationResult<Phase8TimeModelV1>.Invalid(
                    "Phase8TimeModel.PresentationCap.Exceeded",
                    "maximum_presentation_advance_per_wall_tick_s",
                    "Every approved acceleration factor must fit within the per-tick presentation cap.");
            }

            return ContractValidationResult<Phase8TimeModelV1>.Valid(
                new Phase8TimeModelV1(
                    wallControlTickMilliseconds,
                    maximumPresentationAdvancePerWallTickSeconds,
                    defaultAccelerationFactor,
                    maximumAccelerationFactor));
        }
    }

    public sealed class Phase8PlaybackModeV1
    {
        private Phase8PlaybackModeV1(string modeId, double accelerationFactor)
        {
            ModeId = modeId;
            AccelerationFactor = accelerationFactor;
        }

        public string ModeId { get; }

        public double AccelerationFactor { get; }

        public static ContractValidationResult<Phase8PlaybackModeV1> TryCreate(
            string modeId,
            double accelerationFactor,
            Phase8TimeModelV1 timeModel)
        {
            if (string.IsNullOrWhiteSpace(modeId))
            {
                return ContractValidationResult<Phase8PlaybackModeV1>.Invalid(
                    "Phase8PlaybackMode.Id.Missing",
                    "mode_id",
                    "A playback mode requires a stable nonempty identifier.");
            }

            if (timeModel == null)
            {
                return ContractValidationResult<Phase8PlaybackModeV1>.Invalid(
                    "Phase8PlaybackMode.TimeModel.Missing",
                    "time_model",
                    "A playback mode requires the approved time model.");
            }

            if (!ContractValidation.IsFinite(accelerationFactor) ||
                accelerationFactor <= 0 ||
                accelerationFactor > timeModel.MaximumAccelerationFactor)
            {
                return ContractValidationResult<Phase8PlaybackModeV1>.Invalid(
                    "Phase8PlaybackMode.Factor.Invalid",
                    "acceleration_factor_simulation_seconds_per_wall_second",
                    "The playback factor must be finite, positive, and no greater than the approved maximum.");
            }

            if (accelerationFactor * timeModel.WallControlTickSeconds >
                timeModel.MaximumPresentationAdvancePerWallTickSeconds + 1e-12)
            {
                return ContractValidationResult<Phase8PlaybackModeV1>.Invalid(
                    "Phase8PlaybackMode.PresentationCap.Exceeded",
                    "acceleration_factor_simulation_seconds_per_wall_second",
                    "The playback factor exceeds the approved per-tick presentation cap.");
            }

            return ContractValidationResult<Phase8PlaybackModeV1>.Valid(
                new Phase8PlaybackModeV1(modeId, accelerationFactor));
        }
    }

    public sealed class Phase8OperatingEnvelopeV1
    {
        private Phase8OperatingEnvelopeV1(
            double normalizedPowerMinimum,
            double normalizedPowerMaximum,
            double absoluteTiltMaximum,
            double controlMarginMinimum,
            double controlMarginMaximum)
        {
            NormalizedPowerMinimum = normalizedPowerMinimum;
            NormalizedPowerMaximum = normalizedPowerMaximum;
            AbsoluteTiltMaximum = absoluteTiltMaximum;
            ControlMarginMinimum = controlMarginMinimum;
            ControlMarginMaximum = controlMarginMaximum;
        }

        public double NormalizedPowerMinimum { get; }

        public double NormalizedPowerMaximum { get; }

        public double AbsoluteTiltMaximum { get; }

        public double ControlMarginMinimum { get; }

        public double ControlMarginMaximum { get; }

        public static ContractValidationResult<Phase8OperatingEnvelopeV1> TryCreate(
            double normalizedPowerMinimum,
            double normalizedPowerMaximum,
            double absoluteTiltMinimum,
            double absoluteTiltMaximum,
            double controlMarginMinimum,
            double controlMarginMaximum,
            double deviceAvailableMinimum,
            double deviceAvailableMaximum)
        {
            if (!IsFiniteOrdered(normalizedPowerMinimum, normalizedPowerMaximum) ||
                !IsFiniteOrdered(absoluteTiltMinimum, absoluteTiltMaximum) ||
                !IsFiniteOrdered(controlMarginMinimum, controlMarginMaximum) ||
                !IsFiniteOrdered(deviceAvailableMinimum, deviceAvailableMaximum))
            {
                return ContractValidationResult<Phase8OperatingEnvelopeV1>.Invalid(
                    "Phase8Envelope.Bounds.Invalid",
                    "operating_envelope",
                    "Every operating-envelope pair must be finite and ordered.");
            }

            if (absoluteTiltMinimum < 0 || deviceAvailableMinimum < 0 || deviceAvailableMaximum > 1)
            {
                return ContractValidationResult<Phase8OperatingEnvelopeV1>.Invalid(
                    "Phase8Envelope.Domain.Invalid",
                    "operating_envelope",
                    "Absolute tilt must be nonnegative and device availability must remain in [0,1].");
            }

            return ContractValidationResult<Phase8OperatingEnvelopeV1>.Valid(
                new Phase8OperatingEnvelopeV1(
                    normalizedPowerMinimum,
                    normalizedPowerMaximum,
                    absoluteTiltMaximum,
                    controlMarginMinimum,
                    controlMarginMaximum));
        }

        private static bool IsFiniteOrdered(double minimum, double maximum)
        {
            return ContractValidation.IsFinite(minimum) &&
                   ContractValidation.IsFinite(maximum) &&
                   minimum <= maximum;
        }
    }

    public sealed class Phase8DifficultyProfileV1
    {
        private Phase8DifficultyProfileV1(
            string difficultyId,
            double scenarioHorizonSeconds,
            double decisionIntervalSeconds,
            uint maximumPendingCommands,
            uint maximumRefuelRequests,
            Phase8OperatingEnvelopeV1 operatingEnvelope)
        {
            DifficultyId = difficultyId;
            ScenarioHorizonSeconds = scenarioHorizonSeconds;
            DecisionIntervalSeconds = decisionIntervalSeconds;
            MaximumPendingCommands = maximumPendingCommands;
            MaximumRefuelRequests = maximumRefuelRequests;
            OperatingEnvelope = operatingEnvelope;
        }

        public string DifficultyId { get; }

        public double ScenarioHorizonSeconds { get; }

        public double DecisionIntervalSeconds { get; }

        public uint MaximumPendingCommands { get; }

        public uint MaximumRefuelRequests { get; }

        public Phase8OperatingEnvelopeV1 OperatingEnvelope { get; }

        public static ContractValidationResult<Phase8DifficultyProfileV1> TryCreate(
            string difficultyId,
            double scenarioHorizonSeconds,
            double decisionIntervalSeconds,
            uint maximumPendingCommands,
            uint maximumRefuelRequests,
            Phase8OperatingEnvelopeV1 operatingEnvelope)
        {
            if (string.IsNullOrWhiteSpace(difficultyId))
            {
                return ContractValidationResult<Phase8DifficultyProfileV1>.Invalid(
                    "Phase8Difficulty.Id.Missing",
                    "difficulty_id",
                    "A difficulty profile requires a stable nonempty identifier.");
            }

            if (!ContractValidation.IsFinite(scenarioHorizonSeconds) || scenarioHorizonSeconds <= 0 ||
                !ContractValidation.IsFinite(decisionIntervalSeconds) || decisionIntervalSeconds <= 0 ||
                decisionIntervalSeconds > scenarioHorizonSeconds)
            {
                return ContractValidationResult<Phase8DifficultyProfileV1>.Invalid(
                    "Phase8Difficulty.Time.Invalid",
                    "scenario_horizon_s",
                    "The scenario horizon and decision interval must be finite positive gameplay seconds, with the interval within the horizon.");
            }

            if (maximumPendingCommands == 0 || operatingEnvelope == null)
            {
                return ContractValidationResult<Phase8DifficultyProfileV1>.Invalid(
                    "Phase8Difficulty.Configuration.Invalid",
                    "difficulty_profile",
                    "A difficulty profile requires a positive command capacity and an operating envelope.");
            }

            return ContractValidationResult<Phase8DifficultyProfileV1>.Valid(
                new Phase8DifficultyProfileV1(
                    difficultyId,
                    scenarioHorizonSeconds,
                    decisionIntervalSeconds,
                    maximumPendingCommands,
                    maximumRefuelRequests,
                    operatingEnvelope));
        }
    }

    public sealed class Phase8ScenarioInitialStateV1
    {
        private Phase8ScenarioInitialStateV1(
            double normalizedPowerFraction,
            double absoluteTiltFraction,
            double controlMarginFraction,
            double deviceAvailableFraction,
            uint refuelRequestsRemaining)
        {
            NormalizedPowerFraction = normalizedPowerFraction;
            AbsoluteTiltFraction = absoluteTiltFraction;
            ControlMarginFraction = controlMarginFraction;
            DeviceAvailableFraction = deviceAvailableFraction;
            RefuelRequestsRemaining = refuelRequestsRemaining;
        }

        public double NormalizedPowerFraction { get; }

        public double AbsoluteTiltFraction { get; }

        public double ControlMarginFraction { get; }

        public double DeviceAvailableFraction { get; }

        public uint RefuelRequestsRemaining { get; }

        public static ContractValidationResult<Phase8ScenarioInitialStateV1> TryCreate(
            double normalizedPowerFraction,
            double absoluteTiltFraction,
            double controlMarginFraction,
            double deviceAvailableFraction,
            uint refuelRequestsRemaining)
        {
            if (!ContractValidation.IsFinite(normalizedPowerFraction) ||
                !ContractValidation.IsFinite(absoluteTiltFraction) ||
                !ContractValidation.IsFinite(controlMarginFraction) ||
                !ContractValidation.IsFinite(deviceAvailableFraction) ||
                absoluteTiltFraction < 0 || deviceAvailableFraction < 0 || deviceAvailableFraction > 1)
            {
                return ContractValidationResult<Phase8ScenarioInitialStateV1>.Invalid(
                    "Phase8Scenario.InitialState.Invalid",
                    "initial_state",
                    "The initial gameplay proxy state contains an invalid finite or domain value.");
            }

            return ContractValidationResult<Phase8ScenarioInitialStateV1>.Valid(
                new Phase8ScenarioInitialStateV1(
                    normalizedPowerFraction,
                    absoluteTiltFraction,
                    controlMarginFraction,
                    deviceAvailableFraction,
                    refuelRequestsRemaining));
        }
    }

    public sealed class Phase8ScenarioEventV1
    {
        private Phase8ScenarioEventV1(
            double atSeconds,
            Phase8ScenarioEventKindV1 kind,
            double? targetNormalizedPowerFraction,
            double? targetAbsoluteTiltFraction,
            uint? channelId,
            uint? bundlePosition)
        {
            AtSeconds = atSeconds;
            Kind = kind;
            TargetNormalizedPowerFraction = targetNormalizedPowerFraction;
            TargetAbsoluteTiltFraction = targetAbsoluteTiltFraction;
            ChannelId = channelId;
            BundlePosition = bundlePosition;
        }

        public double AtSeconds { get; }

        public Phase8ScenarioEventKindV1 Kind { get; }

        public double? TargetNormalizedPowerFraction { get; }

        public double? TargetAbsoluteTiltFraction { get; }

        public uint? ChannelId { get; }

        public uint? BundlePosition { get; }

        public static ContractValidationResult<Phase8ScenarioEventV1> TryCreate(
            double atSeconds,
            Phase8ScenarioEventKindV1 kind,
            double? targetNormalizedPowerFraction,
            double? targetAbsoluteTiltFraction,
            uint? channelId,
            uint? bundlePosition)
        {
            if (!ContractValidation.IsFinite(atSeconds) || atSeconds < 0 ||
                !Enum.IsDefined(typeof(Phase8ScenarioEventKindV1), kind))
            {
                return ContractValidationResult<Phase8ScenarioEventV1>.Invalid(
                    "Phase8Scenario.Event.Invalid",
                    "scripted_event",
                    "A scripted event requires a finite nonnegative time and a closed event kind.");
            }

            if (targetNormalizedPowerFraction.HasValue &&
                !ContractValidation.IsFinite(targetNormalizedPowerFraction.Value))
            {
                return ContractValidationResult<Phase8ScenarioEventV1>.Invalid(
                    "Phase8Scenario.Event.PowerTarget.Invalid",
                    "target_normalized_power_fraction",
                    "A power target must be finite.");
            }

            if (targetAbsoluteTiltFraction.HasValue &&
                (!ContractValidation.IsFinite(targetAbsoluteTiltFraction.Value) ||
                 targetAbsoluteTiltFraction.Value < 0))
            {
                return ContractValidationResult<Phase8ScenarioEventV1>.Invalid(
                    "Phase8Scenario.Event.TiltTarget.Invalid",
                    "target_absolute_tilt_fraction",
                    "An absolute tilt target must be finite and nonnegative.");
            }

            bool hasPowerTarget = targetNormalizedPowerFraction.HasValue;
            bool hasTiltTarget = targetAbsoluteTiltFraction.HasValue;
            bool hasRefuelLocation = channelId.HasValue && bundlePosition.HasValue;
            bool shapeValid = kind switch
            {
                Phase8ScenarioEventKindV1.Inspect => !hasPowerTarget && !hasTiltTarget && !hasRefuelLocation,
                Phase8ScenarioEventKindV1.SetPowerTarget => hasPowerTarget && !hasTiltTarget && !hasRefuelLocation,
                Phase8ScenarioEventKindV1.SetTiltTarget => !hasPowerTarget && hasTiltTarget && !hasRefuelLocation,
                Phase8ScenarioEventKindV1.RefuelRequest => !hasPowerTarget && !hasTiltTarget && hasRefuelLocation,
                _ => false
            };
            if (!shapeValid)
            {
                return ContractValidationResult<Phase8ScenarioEventV1>.Invalid(
                    "Phase8Scenario.Event.Shape.Invalid",
                    "scripted_event",
                    "The scripted event payload does not match its closed event kind.");
            }

            return ContractValidationResult<Phase8ScenarioEventV1>.Valid(
                new Phase8ScenarioEventV1(
                    atSeconds,
                    kind,
                    targetNormalizedPowerFraction,
                    targetAbsoluteTiltFraction,
                    channelId,
                    bundlePosition));
        }
    }

    public sealed class Phase8ScenarioDefinitionV1
    {
        private readonly ReadOnlyCollection<Phase8ScenarioEventV1> _scriptedEvents;

        private Phase8ScenarioDefinitionV1(
            string scenarioId,
            string difficultyId,
            ulong seed,
            Phase8ScenarioInitialStateV1 initialState,
            IReadOnlyList<Phase8ScenarioEventV1> scriptedEvents)
        {
            ScenarioId = scenarioId;
            DifficultyId = difficultyId;
            Seed = seed;
            InitialState = initialState;
            _scriptedEvents = new ReadOnlyCollection<Phase8ScenarioEventV1>(scriptedEvents.ToArray());
        }

        public string ScenarioId { get; }

        public string DifficultyId { get; }

        public ulong Seed { get; }

        public Phase8ScenarioInitialStateV1 InitialState { get; }

        public IReadOnlyList<Phase8ScenarioEventV1> ScriptedEvents
        {
            get { return _scriptedEvents; }
        }

        public static ContractValidationResult<Phase8ScenarioDefinitionV1> TryCreate(
            string scenarioId,
            string difficultyId,
            ulong seed,
            Phase8ScenarioInitialStateV1 initialState,
            IEnumerable<Phase8ScenarioEventV1> scriptedEvents)
        {
            if (string.IsNullOrWhiteSpace(scenarioId) || string.IsNullOrWhiteSpace(difficultyId))
            {
                return ContractValidationResult<Phase8ScenarioDefinitionV1>.Invalid(
                    "Phase8Scenario.Identity.Missing",
                    "scenario_id",
                    "A scenario requires stable scenario and difficulty identifiers.");
            }

            if (initialState == null || scriptedEvents == null)
            {
                return ContractValidationResult<Phase8ScenarioDefinitionV1>.Invalid(
                    "Phase8Scenario.Payload.Missing",
                    "scenario",
                    "A scenario requires an initial state and scripted event collection.");
            }

            Phase8ScenarioEventV1[] events = scriptedEvents.ToArray();
            if (events.Any(item => item == null))
            {
                return ContractValidationResult<Phase8ScenarioDefinitionV1>.Invalid(
                    "Phase8Scenario.Event.Null",
                    "scripted_events",
                    "A scenario may not contain a null scripted event.");
            }

            for (int i = 1; i < events.Length; i++)
            {
                if (events[i - 1].AtSeconds > events[i].AtSeconds)
                {
                    return ContractValidationResult<Phase8ScenarioDefinitionV1>.Invalid(
                        "Phase8Scenario.Event.Order.Invalid",
                        "scripted_events[" + i.ToString(CultureInfo.InvariantCulture) + "]",
                        "Scripted events must be in nondecreasing simulation-time order.");
                }
            }

            return ContractValidationResult<Phase8ScenarioDefinitionV1>.Valid(
                new Phase8ScenarioDefinitionV1(
                    scenarioId,
                    difficultyId,
                    seed,
                    initialState,
                    events));
        }
    }

    public static class Phase8ScenarioIdentityV1
    {
        public static ulong DeriveReplaySeed(string scenarioId, string difficultyId, ulong seed)
        {
            if (string.IsNullOrWhiteSpace(scenarioId))
            {
                throw new ArgumentException("A scenario identifier is required.", nameof(scenarioId));
            }

            if (string.IsNullOrWhiteSpace(difficultyId))
            {
                throw new ArgumentException("A difficulty identifier is required.", nameof(difficultyId));
            }

            string canonical = scenarioId + "|" + difficultyId + "|" +
                               seed.ToString(CultureInfo.InvariantCulture);
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            }
            ulong value = 0;
            for (int i = 0; i < sizeof(ulong); i++)
            {
                value |= (ulong)digest[i] << (8 * i);
            }

            return value;
        }
    }

    public sealed class Phase8ActionTransitionV1
    {
        internal Phase8ActionTransitionV1(
            ulong actionId,
            Phase8ActionKindV1 kind,
            double acknowledgedWallTimeSeconds,
            double committedWallTimeSeconds,
            double queueDelayWallTimeSeconds)
        {
            ActionId = actionId;
            Kind = kind;
            AcknowledgedWallTimeSeconds = acknowledgedWallTimeSeconds;
            CommittedWallTimeSeconds = committedWallTimeSeconds;
            QueueDelayWallTimeSeconds = queueDelayWallTimeSeconds;
        }

        public ulong ActionId { get; }

        public Phase8ActionKindV1 Kind { get; }

        public double AcknowledgedWallTimeSeconds { get; }

        public double CommittedWallTimeSeconds { get; }

        public double QueueDelayWallTimeSeconds { get; }
    }

    public sealed class Phase8ActionQueueResultV1
    {
        internal Phase8ActionQueueResultV1(ulong actionId, uint pendingActionCount)
        {
            ActionId = actionId;
            PendingActionCount = pendingActionCount;
        }

        public ulong ActionId { get; }

        public uint PendingActionCount { get; }
    }

    public sealed class Phase8EventRecordV1
    {
        internal Phase8EventRecordV1(
            double simulationTimeSeconds,
            string eventId,
            Phase8ScenarioEventKindV1 kind)
        {
            SimulationTimeSeconds = simulationTimeSeconds;
            EventId = eventId;
            Kind = kind;
        }

        public double SimulationTimeSeconds { get; }

        public string EventId { get; }

        public Phase8ScenarioEventKindV1 Kind { get; }
    }

    public sealed class Phase8LossRecordV1
    {
        internal Phase8LossRecordV1(
            double simulationTimeSeconds,
            string lossId,
            string metric,
            double value)
        {
            SimulationTimeSeconds = simulationTimeSeconds;
            LossId = lossId;
            Metric = metric;
            Value = value;
        }

        public double SimulationTimeSeconds { get; }

        public string LossId { get; }

        public string Metric { get; }

        public double Value { get; }
    }

    public sealed class Phase8ScenarioAdvanceResultV1
    {
        internal Phase8ScenarioAdvanceResultV1(
            ulong wallMillisecondsRequested,
            uint controlTicksProcessed,
            double simulationTimeSeconds,
            double wallElapsedSeconds,
            Phase8ScenarioOutcomeV1 outcome,
            bool paused,
            IReadOnlyList<Phase8ActionTransitionV1> actionTransitions,
            IReadOnlyList<Phase8EventRecordV1> eventRecords,
            IReadOnlyList<Phase8LossRecordV1> lossRecords)
        {
            WallMillisecondsRequested = wallMillisecondsRequested;
            ControlTicksProcessed = controlTicksProcessed;
            SimulationTimeSeconds = simulationTimeSeconds;
            WallElapsedSeconds = wallElapsedSeconds;
            Outcome = outcome;
            IsPaused = paused;
            ActionTransitions = new ReadOnlyCollection<Phase8ActionTransitionV1>(actionTransitions.ToArray());
            EventRecords = new ReadOnlyCollection<Phase8EventRecordV1>(eventRecords.ToArray());
            LossRecords = new ReadOnlyCollection<Phase8LossRecordV1>(lossRecords.ToArray());
        }

        public ulong WallMillisecondsRequested { get; }

        public uint ControlTicksProcessed { get; }

        public double SimulationTimeSeconds { get; }

        public double WallElapsedSeconds { get; }

        public Phase8ScenarioOutcomeV1 Outcome { get; }

        public bool IsPaused { get; }

        public IReadOnlyList<Phase8ActionTransitionV1> ActionTransitions { get; }

        public IReadOnlyList<Phase8EventRecordV1> EventRecords { get; }

        public IReadOnlyList<Phase8LossRecordV1> LossRecords { get; }
    }

    /// <summary>
    /// Deterministic, engine-neutral Phase 8 scenario consumer. All state is
    /// advanced by explicit caller-supplied wall durations. The wall duration
    /// is converted into bounded presentation ticks; it is never sampled from
    /// a process clock and never passed to a physics solver as its timestep.
    /// </summary>
    public sealed class Phase8ScenarioRuntimeV1
    {
        private sealed class PendingAction
        {
            public PendingAction(
                ulong actionId,
                Phase8ActionKindV1 kind,
                double value,
                double enqueueWallTimeSeconds)
            {
                ActionId = actionId;
                Kind = kind;
                Value = value;
                EnqueueWallTimeSeconds = enqueueWallTimeSeconds;
            }

            public ulong ActionId { get; }

            public Phase8ActionKindV1 Kind { get; }

            public double Value { get; }

            public double EnqueueWallTimeSeconds { get; }
        }

        private readonly Phase8ScenarioDefinitionV1 _scenario;
        private readonly Phase8DifficultyProfileV1 _profile;
        private readonly Phase8TimeModelV1 _timeModel;
        private readonly List<PendingAction> _pendingActions = new List<PendingAction>();
        private readonly List<Phase8ActionTransitionV1> _actionTransitions = new List<Phase8ActionTransitionV1>();
        private readonly List<Phase8EventRecordV1> _eventRecords = new List<Phase8EventRecordV1>();
        private readonly List<Phase8LossRecordV1> _lossRecords = new List<Phase8LossRecordV1>();
        private SimulationClockV1 _clock;
        private Phase8PlaybackModeV1 _playbackMode;
        private ulong _wallElapsedMilliseconds;
        private ulong _wallAccumulatorMilliseconds;
        private double _normalizedPowerFraction;
        private double _absoluteTiltFraction;
        private double _controlMarginFraction;
        private double _deviceAvailableFraction;
        private uint _refuelRequestsRemaining;
        private uint _nextScriptedEventIndex;
        private ulong _nextActionId = 1;
        private Phase8ScenarioOutcomeV1 _outcome = Phase8ScenarioOutcomeV1.Running;
        private bool _isPaused;

        private Phase8ScenarioRuntimeV1(
            Phase8ScenarioDefinitionV1 scenario,
            Phase8DifficultyProfileV1 profile,
            Phase8TimeModelV1 timeModel,
            Phase8PlaybackModeV1 playbackMode,
            SimulationClockV1 clock)
        {
            _scenario = scenario;
            _profile = profile;
            _timeModel = timeModel;
            _playbackMode = playbackMode;
            _clock = clock;
            _normalizedPowerFraction = scenario.InitialState.NormalizedPowerFraction;
            _absoluteTiltFraction = scenario.InitialState.AbsoluteTiltFraction;
            _controlMarginFraction = scenario.InitialState.ControlMarginFraction;
            _deviceAvailableFraction = scenario.InitialState.DeviceAvailableFraction;
            _refuelRequestsRemaining = scenario.InitialState.RefuelRequestsRemaining;
        }

        public string ScenarioId
        {
            get { return _scenario.ScenarioId; }
        }

        public string DifficultyId
        {
            get { return _scenario.DifficultyId; }
        }

        public ulong Seed
        {
            get { return _scenario.Seed; }
        }

        public ulong ReplaySeed
        {
            get { return Phase8ScenarioIdentityV1.DeriveReplaySeed(ScenarioId, DifficultyId, Seed); }
        }

        public string PlaybackModeId
        {
            get { return _playbackMode.ModeId; }
        }

        public double AccelerationFactor
        {
            get { return _playbackMode.AccelerationFactor; }
        }

        public double SimulationTimeSeconds
        {
            get { return _clock.CurrentSimulationTimeSeconds; }
        }

        public ulong SimulationStepIndex
        {
            get { return _clock.StepIndex; }
        }

        public double WallElapsedSeconds
        {
            get { return _wallElapsedMilliseconds / 1000.0; }
        }

        public double NormalizedPowerFraction
        {
            get { return _normalizedPowerFraction; }
        }

        public double AbsoluteTiltFraction
        {
            get { return _absoluteTiltFraction; }
        }

        public double ControlMarginFraction
        {
            get { return _controlMarginFraction; }
        }

        public double DeviceAvailableFraction
        {
            get { return _deviceAvailableFraction; }
        }

        public uint RefuelRequestsRemaining
        {
            get { return _refuelRequestsRemaining; }
        }

        public uint PendingActionCount
        {
            get { return (uint)_pendingActions.Count; }
        }

        public uint ProcessedScriptedEventCount
        {
            get { return _nextScriptedEventIndex; }
        }

        public IReadOnlyList<Phase8LossRecordV1> LossRecords
        {
            get { return new ReadOnlyCollection<Phase8LossRecordV1>(_lossRecords.ToArray()); }
        }

        public Phase8ScenarioOutcomeV1 Outcome
        {
            get { return _outcome; }
        }

        public bool IsPaused
        {
            get { return _isPaused; }
        }

        public static ContractValidationResult<Phase8ScenarioRuntimeV1> TryCreate(
            Phase8ScenarioDefinitionV1 scenario,
            Phase8DifficultyProfileV1 profile,
            Phase8TimeModelV1 timeModel,
            Phase8PlaybackModeV1 playbackMode)
        {
            if (scenario == null || profile == null || timeModel == null || playbackMode == null)
            {
                return ContractValidationResult<Phase8ScenarioRuntimeV1>.Invalid(
                    "Phase8Runtime.Input.Missing",
                    "runtime",
                    "A scenario runtime requires a scenario, profile, time model, and playback mode.");
            }

            ContractValidationResult<bool> playbackValidation =
                ValidatePlaybackMode(playbackMode, timeModel);
            if (!playbackValidation.IsValid)
            {
                return ContractValidationResult<Phase8ScenarioRuntimeV1>.Invalid(
                    playbackValidation.FirstDiagnostic.Code,
                    playbackValidation.FirstDiagnostic.Path,
                    playbackValidation.FirstDiagnostic.Message);
            }

            if (!string.Equals(scenario.DifficultyId, profile.DifficultyId, StringComparison.Ordinal))
            {
                return ContractValidationResult<Phase8ScenarioRuntimeV1>.Invalid(
                    "Phase8Runtime.Difficulty.Mismatch",
                    "difficulty_id",
                    "The scenario and selected difficulty profile must use the same stable identifier.");
            }

            if (scenario.ScriptedEvents.Any(item => item.AtSeconds > profile.ScenarioHorizonSeconds))
            {
                return ContractValidationResult<Phase8ScenarioRuntimeV1>.Invalid(
                    "Phase8Runtime.Event.AfterHorizon",
                    "scripted_events",
                    "A scripted event may not occur after the selected scenario horizon.");
            }

            ContractValidationResult<SimulationClockV1> clockResult = SimulationClockV1.TryCreate(
                SimulationClockV1.CurrentSchemaVersion,
                0.0,
                0);
            if (!clockResult.IsValid)
            {
                return ContractValidationResult<Phase8ScenarioRuntimeV1>.Invalid(
                    clockResult.FirstDiagnostic.Code,
                    "clock",
                    clockResult.FirstDiagnostic.Message);
            }

            var runtime = new Phase8ScenarioRuntimeV1(
                scenario,
                profile,
                timeModel,
                playbackMode,
                clockResult.Value);
            ContractValidationResult<bool> initialEvents = runtime.ProcessUntil(0.0);
            if (!initialEvents.IsValid)
            {
                return ContractValidationResult<Phase8ScenarioRuntimeV1>.Invalid(
                    initialEvents.FirstDiagnostic.Code,
                    initialEvents.FirstDiagnostic.Path,
                    initialEvents.FirstDiagnostic.Message);
            }

            return ContractValidationResult<Phase8ScenarioRuntimeV1>.Valid(runtime);
        }

        public ContractValidationResult<Phase8ActionQueueResultV1> TryQueuePowerTarget(
            double targetNormalizedPowerFraction)
        {
            return TryQueueAction(
                Phase8ActionKindV1.SetPowerTarget,
                targetNormalizedPowerFraction);
        }

        public ContractValidationResult<Phase8ActionQueueResultV1> TryQueueTiltTarget(
            double targetAbsoluteTiltFraction)
        {
            if (!ContractValidation.IsFinite(targetAbsoluteTiltFraction) || targetAbsoluteTiltFraction < 0)
            {
                return ContractValidationResult<Phase8ActionQueueResultV1>.Invalid(
                    "Phase8Runtime.Action.TiltTarget.Invalid",
                    "target_absolute_tilt_fraction",
                    "An absolute tilt target must be finite and nonnegative.");
            }

            return TryQueueAction(Phase8ActionKindV1.SetTiltTarget, targetAbsoluteTiltFraction);
        }

        public ContractValidationResult<bool> TrySetPlaybackMode(Phase8PlaybackModeV1 playbackMode)
        {
            if (playbackMode == null)
            {
                return ContractValidationResult<bool>.Invalid(
                    "Phase8Runtime.PlaybackMode.Missing",
                    "playback_mode",
                    "A playback mode is required.");
            }

            ContractValidationResult<bool> playbackValidation =
                ValidatePlaybackMode(playbackMode, _timeModel);
            if (!playbackValidation.IsValid)
            {
                return playbackValidation;
            }

            if (_outcome != Phase8ScenarioOutcomeV1.Running)
            {
                return ContractValidationResult<bool>.Invalid(
                    "Phase8Runtime.PlaybackMode.Completed",
                    "playback_mode",
                    "Playback mode cannot change after the scenario has resolved.");
            }

            _playbackMode = playbackMode;
            return ContractValidationResult<bool>.Valid(true);
        }

        private static ContractValidationResult<bool> ValidatePlaybackMode(
            Phase8PlaybackModeV1 playbackMode,
            Phase8TimeModelV1 timeModel)
        {
            if (!ContractValidation.IsFinite(playbackMode.AccelerationFactor) ||
                playbackMode.AccelerationFactor <= 0 ||
                playbackMode.AccelerationFactor > timeModel.MaximumAccelerationFactor ||
                playbackMode.AccelerationFactor * timeModel.WallControlTickSeconds >
                timeModel.MaximumPresentationAdvancePerWallTickSeconds + 1e-12)
            {
                return ContractValidationResult<bool>.Invalid(
                    "Phase8Runtime.PlaybackMode.Incompatible",
                    "playback_mode",
                    "The playback mode is incompatible with the approved runtime time model.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        public ContractValidationResult<bool> TryPause()
        {
            _isPaused = true;
            return ContractValidationResult<bool>.Valid(true);
        }

        public ContractValidationResult<bool> TryResume()
        {
            _isPaused = false;
            return ContractValidationResult<bool>.Valid(true);
        }

        public ContractValidationResult<Phase8ScenarioAdvanceResultV1> TryAdvanceWallMilliseconds(
            ulong wallMilliseconds)
        {
            int actionStart = _actionTransitions.Count;
            int eventStart = _eventRecords.Count;
            int lossStart = _lossRecords.Count;
            uint ticksProcessed = 0;

            if (_isPaused || _outcome != Phase8ScenarioOutcomeV1.Running)
            {
                return ContractValidationResult<Phase8ScenarioAdvanceResultV1>.Valid(
                    CreateAdvanceResult(
                        wallMilliseconds,
                        ticksProcessed,
                        actionStart,
                        eventStart,
                        lossStart));
            }

            if (_wallElapsedMilliseconds > ulong.MaxValue - wallMilliseconds ||
                _wallAccumulatorMilliseconds > ulong.MaxValue - wallMilliseconds)
            {
                return ContractValidationResult<Phase8ScenarioAdvanceResultV1>.Invalid(
                    "Phase8Runtime.WallTime.Overflow",
                    "wall_milliseconds",
                    "The explicit presentation wall time cannot advance past UInt64.MaxValue milliseconds.");
            }

            _wallElapsedMilliseconds += wallMilliseconds;
            _wallAccumulatorMilliseconds += wallMilliseconds;
            if (!ContractValidation.IsFinite(WallElapsedSeconds))
            {
                return ContractValidationResult<Phase8ScenarioAdvanceResultV1>.Invalid(
                    "Phase8Runtime.WallTime.Overflow",
                    "wall_elapsed_s",
                    "The explicit presentation wall time became non-finite.");
            }

            while (_wallAccumulatorMilliseconds >= _timeModel.WallControlTickMilliseconds &&
                   _outcome == Phase8ScenarioOutcomeV1.Running)
            {
                _wallAccumulatorMilliseconds -= _timeModel.WallControlTickMilliseconds;
                if (ticksProcessed == uint.MaxValue)
                {
                    return ContractValidationResult<Phase8ScenarioAdvanceResultV1>.Invalid(
                        "Phase8Runtime.ControlTick.Overflow",
                        "control_ticks",
                        "The control-tick count cannot advance past UInt32.MaxValue.");
                }

                ticksProcessed++;
                ContractValidationResult<bool> actionResult = CommitPendingActions();
                if (!actionResult.IsValid)
                {
                    return ContractValidationResult<Phase8ScenarioAdvanceResultV1>.Invalid(
                        actionResult.FirstDiagnostic.Code,
                        actionResult.FirstDiagnostic.Path,
                        actionResult.FirstDiagnostic.Message);
                }

                if (_outcome == Phase8ScenarioOutcomeV1.Running)
                {
                    double requestedSimulationAdvance = _playbackMode.AccelerationFactor *
                                                        _timeModel.WallControlTickSeconds;
                    double targetTime = Math.Min(
                        _profile.ScenarioHorizonSeconds,
                        _clock.CurrentSimulationTimeSeconds + requestedSimulationAdvance);
                    ContractValidationResult<bool> processResult = ProcessUntil(targetTime);
                    if (!processResult.IsValid)
                    {
                        return ContractValidationResult<Phase8ScenarioAdvanceResultV1>.Invalid(
                            processResult.FirstDiagnostic.Code,
                            processResult.FirstDiagnostic.Path,
                            processResult.FirstDiagnostic.Message);
                    }

                    if (_outcome == Phase8ScenarioOutcomeV1.Running &&
                        _clock.CurrentSimulationTimeSeconds >= _profile.ScenarioHorizonSeconds)
                    {
                        _outcome = Phase8ScenarioOutcomeV1.SurvivedScenarioHorizon;
                    }
                }
            }

            return ContractValidationResult<Phase8ScenarioAdvanceResultV1>.Valid(
                CreateAdvanceResult(
                    wallMilliseconds,
                    ticksProcessed,
                    actionStart,
                    eventStart,
                    lossStart));
        }

        private ContractValidationResult<Phase8ActionQueueResultV1> TryQueueAction(
            Phase8ActionKindV1 kind,
            double value)
        {
            if (_isPaused)
            {
                return ContractValidationResult<Phase8ActionQueueResultV1>.Invalid(
                    "Phase8Runtime.Action.Paused",
                    "action",
                    "Player actions are not accepted while the scenario is paused.");
            }

            if (_outcome != Phase8ScenarioOutcomeV1.Running)
            {
                return ContractValidationResult<Phase8ActionQueueResultV1>.Invalid(
                    "Phase8Runtime.Action.Completed",
                    "action",
                    "Player actions are not accepted after the scenario has resolved.");
            }

            if (!ContractValidation.IsFinite(value))
            {
                return ContractValidationResult<Phase8ActionQueueResultV1>.Invalid(
                    "Phase8Runtime.Action.Value.Invalid",
                    "action.value",
                    "An action target must be finite.");
            }

            if (_pendingActions.Count >= _profile.MaximumPendingCommands)
            {
                return ContractValidationResult<Phase8ActionQueueResultV1>.Invalid(
                    "Phase8Runtime.Action.Queue.Full",
                    "pending_actions",
                    "The approved difficulty command capacity has been reached.");
            }

            if (_nextActionId == ulong.MaxValue)
            {
                return ContractValidationResult<Phase8ActionQueueResultV1>.Invalid(
                    "Phase8Runtime.Action.Id.Overflow",
                    "action_id",
                    "The action identity sequence cannot advance past UInt64.MaxValue.");
            }

            ulong actionId = _nextActionId++;
            _pendingActions.Add(new PendingAction(actionId, kind, value, WallElapsedSeconds));
            return ContractValidationResult<Phase8ActionQueueResultV1>.Valid(
                new Phase8ActionQueueResultV1(actionId, PendingActionCount));
        }

        private ContractValidationResult<bool> CommitPendingActions()
        {
            if (_pendingActions.Count == 0)
            {
                return ContractValidationResult<bool>.Valid(true);
            }

            double boundaryWallTime =
                (_wallElapsedMilliseconds - _wallAccumulatorMilliseconds) / 1000.0;
            PendingAction[] actions = _pendingActions.ToArray();
            _pendingActions.Clear();
            foreach (PendingAction action in actions)
            {
                if (action.Kind == Phase8ActionKindV1.SetPowerTarget)
                {
                    _normalizedPowerFraction = action.Value;
                }
                else
                {
                    _absoluteTiltFraction = action.Value;
                }

                double queueDelay = boundaryWallTime - action.EnqueueWallTimeSeconds;
                if (queueDelay < 0)
                {
                    return ContractValidationResult<bool>.Invalid(
                        "Phase8Runtime.Action.QueueDelay.Backward",
                        "action.queue_delay_wall_s",
                        "An action commit boundary may not precede its enqueue boundary.");
                }

                _actionTransitions.Add(
                    new Phase8ActionTransitionV1(
                        action.ActionId,
                        action.Kind,
                        action.EnqueueWallTimeSeconds,
                        boundaryWallTime,
                        queueDelay));

                if (!EvaluateLoss())
                {
                    continue;
                }

                break;
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private ContractValidationResult<bool> ProcessUntil(double targetTime)
        {
            if (!ContractValidation.IsFinite(targetTime) ||
                targetTime < _clock.CurrentSimulationTimeSeconds ||
                targetTime > _profile.ScenarioHorizonSeconds)
            {
                return ContractValidationResult<bool>.Invalid(
                    "Phase8Runtime.TargetTime.Invalid",
                    "target_simulation_time_s",
                    "The scenario runtime target must be finite, monotone, and within the approved horizon.");
            }

            while (_nextScriptedEventIndex < _scenario.ScriptedEvents.Count)
            {
                Phase8ScenarioEventV1 scriptedEvent =
                    _scenario.ScriptedEvents[(int)_nextScriptedEventIndex];
                if (scriptedEvent.AtSeconds > targetTime)
                {
                    break;
                }

                ContractValidationResult<bool> clockResult = TryAdvanceClockTo(scriptedEvent.AtSeconds);
                if (!clockResult.IsValid)
                {
                    return clockResult;
                }

                ApplyScriptedEvent(scriptedEvent, _nextScriptedEventIndex);
                _nextScriptedEventIndex++;
                if (EvaluateLoss())
                {
                    break;
                }
            }

            if (_outcome == Phase8ScenarioOutcomeV1.Running)
            {
                ContractValidationResult<bool> clockResult = TryAdvanceClockTo(targetTime);
                if (!clockResult.IsValid)
                {
                    return clockResult;
                }

                EvaluateLoss();
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private ContractValidationResult<bool> TryAdvanceClockTo(double targetTime)
        {
            ContractValidationResult<SimulationClockV1> result = _clock.TryAdvanceTo(targetTime);
            if (!result.IsValid)
            {
                return ContractValidationResult<bool>.Invalid(
                    result.FirstDiagnostic.Code,
                    result.FirstDiagnostic.Path,
                    result.FirstDiagnostic.Message);
            }

            _clock = result.Value;
            return ContractValidationResult<bool>.Valid(true);
        }

        private void ApplyScriptedEvent(Phase8ScenarioEventV1 scriptedEvent, uint eventIndex)
        {
            switch (scriptedEvent.Kind)
            {
                case Phase8ScenarioEventKindV1.SetPowerTarget:
                    _normalizedPowerFraction = scriptedEvent.TargetNormalizedPowerFraction!.Value;
                    break;
                case Phase8ScenarioEventKindV1.SetTiltTarget:
                    _absoluteTiltFraction = scriptedEvent.TargetAbsoluteTiltFraction!.Value;
                    break;
                case Phase8ScenarioEventKindV1.RefuelRequest:
                    if (_refuelRequestsRemaining > 0)
                    {
                        _refuelRequestsRemaining--;
                    }

                    break;
                case Phase8ScenarioEventKindV1.Inspect:
                    break;
                default:
                    throw new InvalidOperationException("The scripted event kind is not closed.");
            }

            _eventRecords.Add(
                new Phase8EventRecordV1(
                    _clock.CurrentSimulationTimeSeconds,
                    "scripted-" + eventIndex.ToString(CultureInfo.InvariantCulture),
                    scriptedEvent.Kind));
        }

        private bool EvaluateLoss()
        {
            if (_outcome != Phase8ScenarioOutcomeV1.Running)
            {
                return true;
            }

            if (_normalizedPowerFraction < _profile.OperatingEnvelope.NormalizedPowerMinimum)
            {
                return RecordLoss("power_below_minimum", "normalized_power_fraction", _normalizedPowerFraction);
            }

            if (_normalizedPowerFraction > _profile.OperatingEnvelope.NormalizedPowerMaximum)
            {
                return RecordLoss("power_above_maximum", "normalized_power_fraction", _normalizedPowerFraction);
            }

            if (_absoluteTiltFraction > _profile.OperatingEnvelope.AbsoluteTiltMaximum)
            {
                return RecordLoss("tilt_above_maximum", "absolute_tilt_fraction", _absoluteTiltFraction);
            }

            if (_controlMarginFraction < _profile.OperatingEnvelope.ControlMarginMinimum ||
                _controlMarginFraction > _profile.OperatingEnvelope.ControlMarginMaximum)
            {
                return RecordLoss("control_margin_outside_envelope", "control_margin_fraction", _controlMarginFraction);
            }

            if (_deviceAvailableFraction <= 0)
            {
                return RecordLoss("device_exhaustion", "device_available_fraction", _deviceAvailableFraction);
            }

            return false;
        }

        private bool RecordLoss(string lossId, string metric, double value)
        {
            _lossRecords.Add(
                new Phase8LossRecordV1(
                    _clock.CurrentSimulationTimeSeconds,
                    lossId,
                    metric,
                    value));
            _outcome = Phase8ScenarioOutcomeV1.RecordLoss;
            return true;
        }

        private Phase8ScenarioAdvanceResultV1 CreateAdvanceResult(
            ulong wallMillisecondsRequested,
            uint controlTicksProcessed,
            int actionStart,
            int eventStart,
            int lossStart)
        {
            return new Phase8ScenarioAdvanceResultV1(
                wallMillisecondsRequested,
                controlTicksProcessed,
                SimulationTimeSeconds,
                WallElapsedSeconds,
                Outcome,
                IsPaused,
                _actionTransitions.Skip(actionStart).ToArray(),
                _eventRecords.Skip(eventStart).ToArray(),
                _lossRecords.Skip(lossStart).ToArray());
        }
    }
}
