using System;
using System.Collections.Generic;
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
            double absoluteTiltFraction,
            double controlMarginFraction,
            double deviceAvailableFraction,
            uint refuelRequestsRemaining,
            uint pendingActionCount,
            uint processedScriptedEventCount,
            double scoreTotal,
            uint turnSummaryCount,
            string outcomeId,
            bool isPaused,
            uint freshBundlesAvailable,
            uint refuellingOperationCount,
            int lastRefuelledChannel,
            string lastRefuellingDirectionId,
            ushort lastRefuellingShiftCount,
            GameCorePresentationSnapshot core)
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
            FreshBundlesAvailable = freshBundlesAvailable;
            RefuellingOperationCount = refuellingOperationCount;
            LastRefuelledChannel = lastRefuelledChannel;
            LastRefuellingDirectionId = lastRefuellingDirectionId;
            LastRefuellingShiftCount = lastRefuellingShiftCount;
            Core = core ?? throw new ArgumentNullException(nameof(core));
            Physics = Core.Physics;
            Xenon = Core.Xenon;
            Rrs = Core.Rrs;
        }

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

        public uint FreshBundlesAvailable { get; }

        public uint RefuellingOperationCount { get; }

        public int LastRefuelledChannel { get; }

        public string LastRefuellingDirectionId { get; }

        public ushort LastRefuellingShiftCount { get; }

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

    public sealed class GameSession
    {
        private sealed class PracticeTransaction
        {
            internal PracticeTransaction(
                SyntheticGameCoreStateV1 coreState,
                EquilibriumCoreProjectionV1 spatialCandidate,
                PracticeLiquidZoneRrsV1 rrs,
                double lastFullCoreSolveSimulationTime,
                double syntheticScore,
                ulong powerProjectionVersion)
            {
                CoreState = coreState;
                SpatialCandidate = spatialCandidate;
                Rrs = rrs;
                LastFullCoreSolveSimulationTime = lastFullCoreSolveSimulationTime;
                SyntheticScore = syntheticScore;
                PowerProjectionVersion = powerProjectionVersion;
            }

            internal SyntheticGameCoreStateV1 CoreState;

            internal EquilibriumCoreProjectionV1 SpatialCandidate;

            internal PracticeLiquidZoneRrsV1 Rrs;

            internal double LastFullCoreSolveSimulationTime;

            internal double SyntheticScore;

            internal ulong PowerProjectionVersion;
        }

        private readonly Phase8ScoredScenarioRuntimeV1 _runtime;
        private readonly IReadOnlyDictionary<string, Phase8PlaybackModeV1> _playbackModes;
        private readonly uint _wallControlTickMilliseconds;
        private readonly EquilibriumCoreSolverV1 _equilibriumSolver;
        private PracticeLiquidZoneRrsV1 _practiceRrs;
        private SyntheticGameCoreStateV1 _coreState;
        private double _lastFullCoreSolveSimulationTime;
        private double _syntheticScore;
        private double _scoreResetBaseline;
        private ulong _powerProjectionVersion;

        internal GameSession(
            Phase8ScoredScenarioRuntimeV1 runtime,
            IReadOnlyDictionary<string, Phase8PlaybackModeV1> playbackModes,
            uint wallControlTickMilliseconds,
            SyntheticGameCoreStateV1 coreState,
            EquilibriumCoreSolverV1 equilibriumSolver,
            PracticeLiquidZoneRrsV1 practiceRrs)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _playbackModes = playbackModes ?? throw new ArgumentNullException(nameof(playbackModes));
            _wallControlTickMilliseconds = wallControlTickMilliseconds;
            _coreState = coreState ?? throw new ArgumentNullException(nameof(coreState));
            _equilibriumSolver = equilibriumSolver ?? throw new ArgumentNullException(nameof(equilibriumSolver));
            _practiceRrs = practiceRrs ?? throw new ArgumentNullException(nameof(practiceRrs));
            _lastFullCoreSolveSimulationTime = _runtime.SimulationTimeSeconds;
        }

        public GameSessionSnapshot Snapshot
        {
            get { return CreateSnapshot(); }
        }

        public SyntheticGameCoreStateV1 CoreState
        {
            get { return _coreState; }
        }

        public IqsSpatialCandidateV1 CurrentSpatialCandidate
        {
            get { return _equilibriumSolver.CurrentProjection.LegacyPresentationProjection; }
        }

        public EquilibriumCoreProjectionV1 CurrentEquilibriumProjection
        {
            get { return _equilibriumSolver.CurrentProjection; }
        }

        public PracticeLiquidZoneRrsV1 CurrentLiquidZoneRrs
        {
            get { return _practiceRrs; }
        }

        public GameSessionCommandResult AdvanceWallMilliseconds(ulong wallMilliseconds)
        {
            ContractValidationResult<Phase8ScoredAdvanceResultV1> planned =
                _runtime.TryPlanAdvanceWallMilliseconds(wallMilliseconds);
            if (!planned.IsValid)
            {
                return Rejected(
                    planned.FirstDiagnostic.Code,
                    planned.FirstDiagnostic.Message);
            }

            ContractValidationResult<PracticeTransaction> transaction =
                TryBuildPracticeAdvance(planned.Value.Advance);
            if (!transaction.IsValid)
            {
                return Rejected(
                    transaction.FirstDiagnostic.Code,
                    transaction.FirstDiagnostic.Message);
            }

            EquilibriumCoreProjectionV1 previousProjection =
                _equilibriumSolver.CurrentProjection;
            bool projectionChanged =
                !ReferenceEquals(previousProjection, transaction.Value.SpatialCandidate);
            if (projectionChanged)
            {
                ContractValidationResult<bool> projected =
                    _equilibriumSolver.TryCommitCandidate(
                        transaction.Value.SpatialCandidate);
                if (!projected.IsValid)
                {
                    return Rejected(
                        projected.FirstDiagnostic.Code,
                        projected.FirstDiagnostic.Message);
                }
            }

            ContractValidationResult<Phase8ScoredAdvanceResultV1> result =
                _runtime.TryAdvanceWallMilliseconds(wallMilliseconds);
            if (!result.IsValid)
            {
                if (projectionChanged)
                {
                    _equilibriumSolver.TryCommitCandidate(previousProjection);
                }

                return Rejected(
                    result.FirstDiagnostic.Code,
                    result.FirstDiagnostic.Message);
            }

            ApplyPracticeTransaction(transaction.Value);
            return Complete(result);
        }

        public GameSessionCommandResult QueuePowerTarget(double targetFraction)
        {
            return Complete(_runtime.TryQueuePowerTarget(targetFraction));
        }

        public GameSessionCommandResult QueueTiltTarget(double targetFraction)
        {
            return Complete(_runtime.TryQueueTiltTarget(targetFraction));
        }

        public GameSessionCommandResult SetPlaybackMode(string playbackModeId)
        {
            if (string.IsNullOrWhiteSpace(playbackModeId) ||
                !_playbackModes.TryGetValue(playbackModeId, out Phase8PlaybackModeV1 playbackMode))
            {
                return Rejected(
                    "GameSession.PlaybackMode.NotFound",
                    "The requested playback mode is not available in this game session.");
            }

            ContractValidationResult<bool> result = _runtime.TrySetPlaybackMode(playbackMode);
            if (!result.IsValid || !_runtime.IsPaused)
            {
                return Complete(result);
            }

            // Selecting a live speed is also the explicit resume action after
            // a day jump. This keeps the desktop, browser, and fixture
            // controls consistent while the pause button remains a separate
            // hard hold.
            return Complete(_runtime.TryResume());
        }

        public GameSessionCommandResult Pause()
        {
            return Complete(_runtime.TryPause());
        }

        public GameSessionCommandResult Resume()
        {
            return Complete(_runtime.TryResume());
        }

        public GameSessionCommandResult DebugGrantFreshBundles(uint additionalBundles)
        {
            if (additionalBundles == 0)
            {
                return Rejected(
                    "GameSession.Debug.Inventory.Invalid",
                    "The debug inventory grant must be greater than zero.");
            }

            _coreState = _coreState.WithFreshBundles(additionalBundles);
            return AcceptedMessage(
                "Debug: granted " + additionalBundles.ToString(CultureInfo.InvariantCulture) +
                " fresh bundles; debug state is not scored.");
        }

        public GameSessionCommandResult DebugClearPendingActions()
        {
            return CompleteWithMessage(
                _runtime.TryClearPendingActions(),
                clearedCount =>
                    "Debug: cleared " + clearedCount.ToString(CultureInfo.InvariantCulture) +
                    " pending actions; debug state is not scored.");
        }

        public GameSessionCommandResult DebugResetSyntheticResponse()
        {
            _syntheticScore = 0.0;
            _scoreResetBaseline = _runtime.Score.TotalPoints;
            return AcceptedMessage("Debug: practice score adjustment reset.");
        }

        public GameSessionCommandResult RefuelChannel(
            uint channelIndex,
            string directionId,
            ushort shiftCount,
            string fuelTypeId)
        {
            if (!TryParseDirection(directionId, out GameRefuellingDirectionV1 direction))
            {
                return Rejected(
                    "GameSession.Refuelling.Direction.Invalid",
                    "Choose either toward-end-a or toward-end-b.");
            }

            ContractValidationResult<GameRefuellingResultV1> result = TryRefuel(
                channelIndex,
                direction,
                shiftCount,
                fuelTypeId);
            if (!result.IsValid)
            {
                return Rejected(result.FirstDiagnostic.Code, result.FirstDiagnostic.Message);
            }

            ContractValidationResult<PracticeTransaction> transaction =
                TryBuildRefuellingTransaction(result.Value);
            if (!transaction.IsValid)
            {
                return Rejected(
                    transaction.FirstDiagnostic.Code,
                    transaction.FirstDiagnostic.Message);
            }

            ContractValidationResult<bool> committed =
                _equilibriumSolver.TryCommitCandidate(
                    transaction.Value.SpatialCandidate);
            if (!committed.IsValid)
            {
                return Rejected(
                    committed.FirstDiagnostic.Code,
                    committed.FirstDiagnostic.Message);
            }

            ApplyPracticeTransaction(transaction.Value);
            string message = FormatRefuellingMessage(result.Value);
            return new GameSessionCommandResult(
                true,
                string.Empty,
                string.Empty,
                message,
                CreateSnapshot());
        }

        private GameSessionCommandResult Complete<T>(ContractValidationResult<T> result)
        {
            if (!result.IsValid)
            {
                return Rejected(
                    result.FirstDiagnostic.Code,
                    result.FirstDiagnostic.Message);
            }

            return new GameSessionCommandResult(
                true,
                string.Empty,
                string.Empty,
                string.Empty,
                CreateSnapshot());
        }

        private GameSessionCommandResult CompleteWithMessage<T>(
            ContractValidationResult<T> result,
            Func<T, string> messageFactory)
        {
            if (!result.IsValid)
            {
                return Rejected(
                    result.FirstDiagnostic.Code,
                    result.FirstDiagnostic.Message);
            }

            return AcceptedMessage(messageFactory(result.Value));
        }

        private GameSessionCommandResult AcceptedMessage(string message)
        {
            return new GameSessionCommandResult(
                true,
                string.Empty,
                string.Empty,
                message,
                CreateSnapshot());
        }

        private GameSessionCommandResult Rejected(string code, string message)
        {
            return new GameSessionCommandResult(
                false,
                code,
                message,
                message,
                CreateSnapshot());
        }

        private GameSessionSnapshot CreateSnapshot()
        {
            GameCorePresentationSnapshot core = CreateCorePresentationSnapshot(_coreState);
            return new GameSessionSnapshot(
                _runtime.ScenarioId,
                _runtime.DifficultyId,
                _runtime.Seed,
                _runtime.PlaybackModeId,
                _runtime.AccelerationFactor,
                _wallControlTickMilliseconds,
                _runtime.ScenarioHorizonSeconds,
                _runtime.SimulationTimeSeconds,
                _runtime.WallElapsedSeconds,
                Clamp(_runtime.NormalizedPowerFraction, 0.0, 1.50),
                CurrentTiltFraction(),
                _runtime.ControlMarginFraction,
                _runtime.DeviceAvailableFraction,
                _runtime.RefuelRequestsRemaining,
                _runtime.PendingActionCount,
                _runtime.ProcessedScriptedEventCount,
                _runtime.Score.TotalPoints - _scoreResetBaseline + _syntheticScore,
                checked((uint)_runtime.TurnSummaries.Count),
                _runtime.Outcome.ToString(),
                _runtime.IsPaused,
                _coreState.FreshBundlesAvailable,
                _coreState.RefuellingOperationCount,
                _coreState.LastRefuelledChannel,
                DirectionId(_coreState.LastDirection),
                _coreState.LastShiftCount,
                core);
        }

        private ContractValidationResult<GameRefuellingResultV1> TryRefuel(
            uint channelIndex,
            GameRefuellingDirectionV1 direction,
            ushort shiftCount,
            string fuelTypeId)
        {
            return _coreState.TryRefuel(
                channelIndex,
                direction,
                shiftCount,
                fuelTypeId,
                _runtime.SimulationTimeSeconds);
        }

        private ContractValidationResult<PracticeTransaction> TryBuildRefuellingTransaction(
            GameRefuellingResultV1 result)
        {
            if (result == null)
            {
                return InvalidTransaction(
                    "GameSession.Refuelling.Result.Missing",
                    "refuelling",
                    "A refuelling transaction requires a validated inventory candidate.");
            }

            double simulationTimeSeconds = _runtime.SimulationTimeSeconds;
            ContractValidationResult<bool> timeBinding =
                ValidateCommittedTime(simulationTimeSeconds);
            if (!timeBinding.IsValid)
            {
                return InvalidTransaction(
                    timeBinding.FirstDiagnostic.Code,
                    timeBinding.FirstDiagnostic.Path,
                    timeBinding.FirstDiagnostic.Message);
            }

            ContractValidationResult<PracticeLiquidZoneRrsEquilibriumResultV1> regulated =
                TryBuildRrsEquilibrium(
                    result.ResultingState,
                    _practiceRrs,
                    simulationTimeSeconds);
            if (!regulated.IsValid)
            {
                return InvalidTransaction(
                    regulated.FirstDiagnostic.Code,
                    regulated.FirstDiagnostic.Path,
                    regulated.FirstDiagnostic.Message);
            }

            ContractValidationResult<ulong> nextProjectionVersion =
                TryNextPowerProjectionVersion(_powerProjectionVersion);
            if (!nextProjectionVersion.IsValid)
            {
                return InvalidTransaction(
                    nextProjectionVersion.FirstDiagnostic.Code,
                    nextProjectionVersion.FirstDiagnostic.Path,
                    nextProjectionVersion.FirstDiagnostic.Message);
            }

            double nextScore = _syntheticScore + PracticeRefuellingScore(result);
            if (!IsFinite(nextScore))
            {
                return InvalidTransaction(
                    "GameSession.Refuelling.Score.NonFinite",
                    "score",
                    "The refuelling transaction score must remain finite.");
            }

            return ContractValidationResult<PracticeTransaction>.Valid(
                new PracticeTransaction(
                    result.ResultingState,
                    regulated.Value.Projection,
                    regulated.Value.State,
                    simulationTimeSeconds,
                    nextScore,
                    nextProjectionVersion.Value));
        }

        private ContractValidationResult<EquilibriumCoreProjectionV1> TryBuildEquilibriumCandidate(
            SyntheticGameCoreStateV1 coreState,
            FullCoreDiffusionSolveResultV1? initialSpatialSolve = null)
        {
            if (coreState == null)
            {
                return ContractValidationResult<EquilibriumCoreProjectionV1>.Invalid(
                    "GameSession.SpatialCandidate.Input.Missing",
                    "candidate",
                    "An equilibrium spatial candidate requires a validated inventory candidate.");
            }

            return initialSpatialSolve == null
                ? _equilibriumSolver.TrySolveCandidate(
                    coreState.EnumerateBundles())
                : _equilibriumSolver.TrySolveCandidate(
                coreState.EnumerateBundles(),
                initialSpatialSolve);
        }

        private ContractValidationResult<PracticeLiquidZoneRrsEquilibriumResultV1>
            TryBuildRrsEquilibrium(
                SyntheticGameCoreStateV1 coreState,
                PracticeLiquidZoneRrsV1 previousRrs,
                double simulationTimeSeconds)
        {
            if (coreState == null)
            {
                return ContractValidationResult<PracticeLiquidZoneRrsEquilibriumResultV1>.Invalid(
                    "GameSession.Rrs.Input.Missing",
                    "candidate",
                    "An RRS event requires a validated inventory candidate.");
            }

            if (previousRrs == null)
            {
                return ContractValidationResult<PracticeLiquidZoneRrsEquilibriumResultV1>.Invalid(
                    "GameSession.Rrs.State.Missing",
                    "rrs",
                    "An RRS event requires the last accepted RRS state.");
            }

            return PracticeLiquidZoneRrsV1.TryRunEquilibrium(
                _equilibriumSolver,
                coreState.EnumerateBundles(),
                previousRrs,
                simulationTimeSeconds);
        }

        private static double PracticeRefuellingScore(GameRefuellingResultV1 result)
        {
            double averageDischargedBurnup = result.DischargedBundles.Count == 0
                ? 0.0
                : result.DischargedBundles.Average(
                    bundle => bundle.CurrentBurnupJPerKgHm /
                              GameCorePresentationConstants.JoulesPerMegaWattDayPerKilogram);
            double utilizationQuality = Clamp((averageDischargedBurnup - 4.0) / 6.0, 0.0, 1.0);
            double shiftFactor = result.ShiftCount / 4.0;
            // Scoring observes the operation. It does not modify power,
            // tilt, or reactivity; those are recomputed from the resulting
            // bundle state by the full-core diffusion solve.
            return
                6.0 + 10.0 * utilizationQuality - 0.75 * shiftFactor;
        }

        private ContractValidationResult<PracticeTransaction> TryBuildPracticeAdvance(
            Phase8ScenarioAdvanceResultV1 advance)
        {
            if (advance == null)
            {
                return InvalidTransaction(
                    "GameSession.Advance.Result.Missing",
                    "advance",
                    "A practice advance transaction requires a validated runtime advance.");
            }

            ContractValidationResult<bool> timeBinding =
                ValidateCommittedTime(_runtime.SimulationTimeSeconds);
            if (!timeBinding.IsValid)
            {
                return InvalidTransaction(
                    timeBinding.FirstDiagnostic.Code,
                    timeBinding.FirstDiagnostic.Path,
                    timeBinding.FirstDiagnostic.Message);
            }

            var transaction = new PracticeTransaction(
                _coreState,
                _equilibriumSolver.CurrentProjection,
                _practiceRrs,
                _lastFullCoreSolveSimulationTime,
                _syntheticScore,
                _powerProjectionVersion);

            foreach (Phase8ScenarioAdvanceSegmentV1 segment in advance.StateSegments)
            {
                double segmentStartSeconds = segment.SimulationTimeStartSeconds;
                double segmentEndSeconds = segment.SimulationTimeEndSeconds;
                if (!IsFinite(segmentStartSeconds) ||
                    !IsFinite(segmentEndSeconds) ||
                    segmentEndSeconds < segmentStartSeconds)
                {
                    return InvalidTransaction(
                        "GameSession.Advance.Segment.Time.Invalid",
                        "state_segments",
                        "A practice transaction requires finite monotone runtime state segments.");
                }

                double requestedAmplitude = Clamp(segment.NormalizedPowerFraction, 0.0, 1.5);
                double simulationCursor = segmentStartSeconds;
                while (simulationCursor < segmentEndSeconds - 1.0e-9)
                {
                    if (!AreSameSimulationTime(
                            transaction.Rrs.SimulationTimeSeconds,
                            simulationCursor))
                    {
                        return InvalidTransaction(
                            "GameSession.Advance.State.TimeMismatch",
                            "state_segments",
                            "The candidate inventory, equilibrium projection, and regulator must share the segment start time.");
                    }

                    double elapsedSinceShapeSolve =
                        simulationCursor - transaction.LastFullCoreSolveSimulationTime;
                    if (elapsedSinceShapeSolve >=
                        PracticeGameSessionFactory.FullCoreDiffusionRecomputeIntervalSeconds - 1e-9)
                    {
                        ContractValidationResult<bool> scheduled =
                            TryBuildScheduledShape(transaction, simulationCursor);
                        if (!scheduled.IsValid)
                        {
                            return InvalidTransaction(
                                scheduled.FirstDiagnostic.Code,
                                scheduled.FirstDiagnostic.Path,
                                scheduled.FirstDiagnostic.Message);
                        }

                        if (scheduled.Value)
                        {
                            continue;
                        }
                    }

                    double untilShapeSolve =
                        PracticeGameSessionFactory.FullCoreDiffusionRecomputeIntervalSeconds -
                        (simulationCursor - transaction.LastFullCoreSolveSimulationTime);

                    double stepSeconds = Math.Min(
                        segmentEndSeconds - simulationCursor,
                        untilShapeSolve);
                    if (!IsFinite(stepSeconds) || stepSeconds <= 0.0)
                    {
                        return InvalidTransaction(
                            "GameSession.Advance.Step.Invalid",
                            "state_segments",
                            "Every practice integration step must be finite and strictly positive.");
                    }

                    double stepEnd = simulationCursor + stepSeconds;
                    // Short ticks reuse the last accepted static equilibrium
                    // projection. The operator target is the equilibrium
                    // power scale; no exponential response or kinetics
                    // substep is implied by this burnup integration.
                    double integratedPowerScale = requestedAmplitude * stepSeconds;

                    var deltaEnergy = new double[
                        transaction.SpatialCandidate.ShapeNodePowerWatts.Count];
                    for (int index = 0; index < deltaEnergy.Length; index++)
                    {
                        deltaEnergy[index] =
                            transaction.SpatialCandidate.ShapeNodePowerWatts[index] *
                            integratedPowerScale;
                    }

                    ContractValidationResult<SyntheticGameCoreStateV1> integrated =
                        transaction.CoreState.TryAddFissionEnergy(deltaEnergy);
                    if (!integrated.IsValid)
                    {
                        return InvalidTransaction(
                            integrated.FirstDiagnostic.Code,
                            integrated.FirstDiagnostic.Path,
                            integrated.FirstDiagnostic.Message);
                    }

                    transaction.CoreState = integrated.Value;
                    ContractValidationResult<PracticeLiquidZoneRrsV1> advancedRrs =
                        transaction.Rrs.TryWithSimulationTime(stepEnd);
                    if (!advancedRrs.IsValid)
                    {
                        return InvalidTransaction(
                            advancedRrs.FirstDiagnostic.Code,
                            advancedRrs.FirstDiagnostic.Path,
                            advancedRrs.FirstDiagnostic.Message);
                    }

                    transaction.Rrs = advancedRrs.Value;
                    ContractValidationResult<ulong> nextProjectionVersion =
                        TryNextPowerProjectionVersion(
                            transaction.PowerProjectionVersion);
                    if (!nextProjectionVersion.IsValid)
                    {
                        return InvalidTransaction(
                            nextProjectionVersion.FirstDiagnostic.Code,
                            nextProjectionVersion.FirstDiagnostic.Path,
                            nextProjectionVersion.FirstDiagnostic.Message);
                    }

                    transaction.PowerProjectionVersion = nextProjectionVersion.Value;
                    double averagePowerScale = integratedPowerScale / stepSeconds;
                    double actualPowerFraction =
                        transaction.SpatialCandidate.ShapePowerWatts * averagePowerScale /
                        PracticeGameSessionFactory.PracticeReferencePowerWatts;
                    double powerQuality = 1.0 -
                        Clamp(Math.Abs(actualPowerFraction - 1.0) / 0.02, 0.0, 1.0);
                    double tiltQuality = 1.0 -
                        Clamp(Math.Abs(segment.AbsoluteTiltFraction) / 0.05, 0.0, 1.0);
                    transaction.SyntheticScore += stepSeconds *
                        (0.35 * powerQuality + 0.15 * tiltQuality);
                    simulationCursor = stepEnd;
                    if (simulationCursor - transaction.LastFullCoreSolveSimulationTime >=
                        PracticeGameSessionFactory.FullCoreDiffusionRecomputeIntervalSeconds - 1e-9)
                    {
                        ContractValidationResult<bool> scheduled =
                            TryBuildScheduledShape(transaction, simulationCursor);
                        if (!scheduled.IsValid)
                        {
                            return InvalidTransaction(
                                scheduled.FirstDiagnostic.Code,
                                scheduled.FirstDiagnostic.Path,
                                scheduled.FirstDiagnostic.Message);
                        }
                    }
                }
            }

            if (!AreSameSimulationTime(
                    transaction.Rrs.SimulationTimeSeconds,
                    advance.SimulationTimeSeconds))
            {
                return InvalidTransaction(
                    "GameSession.Advance.Result.TimeMismatch",
                    "simulation_time_s",
                    "The candidate state must finish at the exact planned authoritative simulation time.");
            }

            return ContractValidationResult<PracticeTransaction>.Valid(transaction);
        }

        private ContractValidationResult<bool> TryBuildScheduledShape(
            PracticeTransaction transaction,
            double simulationTimeSeconds)
        {
            if (!AreSameSimulationTime(
                    transaction.Rrs.SimulationTimeSeconds,
                    simulationTimeSeconds))
            {
                return InvalidTransactionBoolean(
                    "GameSession.Shape.TimeMismatch",
                    "simulation_time_s",
                    "A scheduled equilibrium shape must bind the exact candidate simulation time.");
            }

            ContractValidationResult<PracticeLiquidZoneRrsEquilibriumResultV1> regulated =
                TryBuildRrsEquilibrium(
                    transaction.CoreState,
                    transaction.Rrs,
                    simulationTimeSeconds);
            if (!regulated.IsValid)
            {
                return InvalidTransactionBoolean(
                    regulated.FirstDiagnostic.Code,
                    regulated.FirstDiagnostic.Path,
                    regulated.FirstDiagnostic.Message);
            }

            ContractValidationResult<ulong> nextProjectionVersion =
                TryNextPowerProjectionVersion(transaction.PowerProjectionVersion);
            if (!nextProjectionVersion.IsValid)
            {
                return InvalidTransactionBoolean(
                    nextProjectionVersion.FirstDiagnostic.Code,
                    nextProjectionVersion.FirstDiagnostic.Path,
                    nextProjectionVersion.FirstDiagnostic.Message);
            }

            transaction.SpatialCandidate = regulated.Value.Projection;
            transaction.Rrs = regulated.Value.State;
            transaction.LastFullCoreSolveSimulationTime = simulationTimeSeconds;
            transaction.PowerProjectionVersion = nextProjectionVersion.Value;
            return ContractValidationResult<bool>.Valid(true);
        }

        private void ApplyPracticeTransaction(PracticeTransaction transaction)
        {
            _coreState = transaction.CoreState;
            _practiceRrs = transaction.Rrs;
            _lastFullCoreSolveSimulationTime =
                transaction.LastFullCoreSolveSimulationTime;
            _syntheticScore = transaction.SyntheticScore;
            _powerProjectionVersion = transaction.PowerProjectionVersion;
        }

        private GameCorePresentationSnapshot CreateCorePresentationSnapshot(
            SyntheticGameCoreStateV1 state)
        {
            return CreateCorePresentationSnapshot(
                state,
                CurrentPowerFraction(),
                _equilibriumSolver.CurrentProjection,
                _practiceRrs);
        }

        private GameCorePresentationSnapshot CreateCorePresentationSnapshot(
            SyntheticGameCoreStateV1 state,
            double powerAmplitude,
            EquilibriumCoreProjectionV1 projection,
            PracticeLiquidZoneRrsV1 rrs)
        {
            GameXenonPresentationSnapshot xenon =
                CreateXenonPresentationSnapshot(
                    state.RefuellingOperationCount == 0
                        ? -1
                        : state.LastRefuelledChannel,
                    _runtime.SimulationTimeSeconds);
            var channelStates = new IReadOnlyList<BundleState>[
                (int)GameCorePresentationConstants.ChannelCount];
            for (uint channelIndex = 0;
                 channelIndex < GameCorePresentationConstants.ChannelCount;
                 channelIndex++)
            {
                IReadOnlyList<BundleState> bundles = state.GetChannel(channelIndex);
                channelStates[(int)channelIndex] = bundles;
            }

            double amplitude = Clamp(powerAmplitude, 0.0, 1.5);
            double physicalShapeScale = amplitude;
            double totalPowerWatts = projection.ShapePowerWatts * physicalShapeScale;
            double actualPowerFraction = totalPowerWatts /
                                         PracticeGameSessionFactory.PracticeReferencePowerWatts;
            double meanChannelPowerWatts = totalPowerWatts /
                                           GameCorePresentationConstants.ChannelCount;
            var channelPowerWatts = new double[
                (int)GameCorePresentationConstants.ChannelCount];
            for (int index = 0; index < projection.ShapeNodePowerWatts.Count; index++)
            {
                uint channelIndex = (uint)(index /
                    (int)GameCorePresentationConstants.BundlePositionCount);
                channelPowerWatts[(int)channelIndex] +=
                    projection.ShapeNodePowerWatts[index] * physicalShapeScale;
            }

            var channels = new List<GameChannelPresentationSnapshot>(
                (int)GameCorePresentationConstants.ChannelCount);
            int nodeIndex = 0;
            for (uint channelIndex = 0;
                 channelIndex < GameCorePresentationConstants.ChannelCount;
                 channelIndex++)
            {
                PracticeCoreGridPosition grid = PracticeCoreLayout.GetPosition(channelIndex);
                IReadOnlyList<BundleState> bundles = channelStates[(int)channelIndex];
                var bundleSnapshots = new List<GameBundlePresentationSnapshot>(
                    (int)GameCorePresentationConstants.BundlePositionCount);
                double burnupTotal = 0.0;
                double axialPowerMoment = 0.0;
                for (int bundleIndex = 0; bundleIndex < bundles.Count; bundleIndex++)
                {
                    BundleState bundle = bundles[bundleIndex];
                    double burnup = bundle.CurrentBurnupJPerKgHm /
                                    GameCorePresentationConstants.JoulesPerMegaWattDayPerKilogram;
                    double bundlePower =
                        projection.ShapeNodePowerWatts[nodeIndex++] * physicalShapeScale;
                    burnupTotal += burnup;
                    axialPowerMoment += bundlePower *
                        (2.0 * bundle.Position.Value /
                         (GameCorePresentationConstants.BundlePositionCount - 1) - 1.0);
                    bundleSnapshots.Add(
                        new GameBundlePresentationSnapshot(
                            bundle.Position.Value,
                            bundle.BundleId.ToString(),
                            bundle.MaterialVariantId.Value,
                            burnup,
                            bundlePower,
                            bundle.InsertedAtSeconds,
                            bundle.StateVersion));
                }

                double channelPower = channelPowerWatts[(int)channelIndex];
                double localPower = meanChannelPowerWatts <= 0.0
                    ? 1.0
                    : channelPower / meanChannelPowerWatts;
                double localTilt = channelPower <= 0.0
                    ? 0.0
                    : Math.Abs(axialPowerMoment / channelPower);
                channels.Add(
                    new GameChannelPresentationSnapshot(
                        channelIndex,
                        grid.Column,
                        grid.Row,
                        burnupTotal / GameCorePresentationConstants.BundlePositionCount,
                        channelPower,
                        localPower,
                        localTilt,
                        PracticeCoreLayout.GetFlowDirection(grid),
                        bundleSnapshots,
                        xenon.GetChannel(channelIndex)));
            }

            FullCoreDiffusionSolveResultV1 spatial = projection.SpatialSolve;
            double targetPowerAmplitude = Clamp(
                _runtime.NormalizedPowerFraction,
                0.0,
                1.5);
            var physics = new GamePhysicsPresentationSnapshot(
                EquilibriumCoreSolverIdentityV1.ModelId,
                EquilibriumCoreSolverIdentityV1.FormulationId,
                EquilibriumCoreSolverIdentityV1.ShapeMethodId,
                EquilibriumCoreSolverIdentityV1.AmplitudeMethodId,
                EquilibriumCoreSolverIdentityV1.ReactivityMethodId,
                "converged",
                true,
                _powerProjectionVersion,
                PracticeGameSessionFactory.PracticeReferencePowerWatts,
                amplitude,
                actualPowerFraction,
                PracticeGameSessionFactory.PracticeReferencePowerWatts * targetPowerAmplitude,
                totalPowerWatts,
                meanChannelPowerWatts,
                totalPowerWatts /
                    (GameCorePresentationConstants.ChannelCount *
                     GameCorePresentationConstants.BundlePositionCount),
                spatial.EffectiveK,
                spatial.Reactivity,
                projection.WeightedPerturbationReactivity,
                projection.ReactivityNumerator,
                projection.ReactivityDenominator,
                projection.ReactivityIdentity,
                projection.ReactivityBindingDigestHex,
                spatial.PowerBalanceRelativeError,
                projection.SolverIdentity,
                spatial.IterationCount,
                spatial.ResidualRelativeInfinity,
                rrs.CoreReactivity,
                rrs.CompensatedNetReactivity,
                rrs.AverageFillFraction,
                rrs.AverageFillFraction,
                0.0,
                1.0,
                rrs.LowExhaustion || rrs.HighExhaustion,
                0.0,
                rrs.CadenceIdentity,
                "equilibrium-static-only-v1",
                projection.ReactivityBindingDigestHex,
                0,
                0.0);
            return new GameCorePresentationSnapshot(
                channels,
                physics,
                xenon,
                new GameRrsPresentationSnapshot(rrs));
        }

        private static GameXenonPresentationSnapshot CreateXenonPresentationSnapshot(
            int selectedChannelIndex,
            double simulationTimeSeconds)
        {
            var channelDiagnostics = new List<GameXenonChannelPresentationSnapshot>(
                (int)GameCorePresentationConstants.ChannelCount);

            for (uint channelIndex = 0;
                 channelIndex < GameCorePresentationConstants.ChannelCount;
                 channelIndex++)
            {
                channelDiagnostics.Add(
                    new GameXenonChannelPresentationSnapshot(
                        channelIndex,
                        0.0,
                        0.0,
                        0.0,
                        0.0,
                        0.0,
                        0.0,
                        0.0,
                        0.0));
            }

            return new GameXenonPresentationSnapshot(
                "xenon-unavailable-static-compatibility-v1",
                "sha256:" + new string('0', 64),
                0UL,
                simulationTimeSeconds,
                checked((int)(GameCorePresentationConstants.ChannelCount *
                    GameCorePresentationConstants.BundlePositionCount)),
                "xenon-unavailable-static-compatibility-v1",
                false,
                string.Empty,
                string.Empty,
                string.Empty,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                channelDiagnostics,
                selectedChannelIndex);
        }

        private static string DigestHex(Digest32 digest)
        {
            var builder = new StringBuilder(digest.Bytes.Count * 2 + 7);
            builder.Append("sha256:");
            foreach (byte value in digest.Bytes)
            {
                builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private string FormatRefuellingMessage(
            GameRefuellingResultV1 result)
        {
            string endName = result.Direction == GameRefuellingDirectionV1.TowardEndA
                ? "End A"
                : "End B";
            double averageDischargedBurnup = result.DischargedBundles.Count == 0
                ? 0.0
                : result.DischargedBundles.Average(
                    bundle => bundle.CurrentBurnupJPerKgHm /
                              GameCorePresentationConstants.JoulesPerMegaWattDayPerKilogram);
            string inventory = "; " + _coreState.FreshBundlesAvailable.ToString(CultureInfo.InvariantCulture) +
                               " fresh bundles remain";
            return "Channel " + result.ChannelIndex.ToString(CultureInfo.InvariantCulture) +
                   " refuelled toward " + endName + " with " +
                   result.ShiftCount.ToString(CultureInfo.InvariantCulture) + " " +
                   result.FuelTypeId + " bundles; discharged burnup " +
                   averageDischargedBurnup.ToString("0.00", CultureInfo.InvariantCulture) +
                   " MWd/kg HM" + inventory + ".";
        }

        private double CurrentPowerFraction()
        {
            return Clamp(_runtime.NormalizedPowerFraction, 0.0, 1.5);
        }

        private double CurrentTiltFraction()
        {
            return Clamp(Math.Abs(_runtime.AbsoluteTiltFraction), 0.0, 1.0);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private ContractValidationResult<bool> ValidateCommittedTime(
            double simulationTimeSeconds)
        {
            if (!AreSameSimulationTime(
                    _practiceRrs.SimulationTimeSeconds,
                    simulationTimeSeconds))
            {
                return ContractValidationResult<bool>.Invalid(
                    "GameSession.State.TimeMismatch",
                    "simulation_time_s",
                    "The committed equilibrium projection, RRS state, and scenario runtime must share one authoritative time.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        private static ContractValidationResult<ulong> TryNextPowerProjectionVersion(
            ulong currentVersion)
        {
            if (currentVersion == ulong.MaxValue)
            {
                return ContractValidationResult<ulong>.Invalid(
                    "GameSession.PowerProjectionVersion.Overflow",
                    "power_projection_version",
                    "The practice power projection version cannot increment beyond UInt64.MaxValue.");
            }

            return ContractValidationResult<ulong>.Valid(currentVersion + 1UL);
        }

        private static bool AreSameSimulationTime(double first, double second)
        {
            return Math.Abs(first - second) <= 1.0e-8;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static ContractValidationResult<PracticeTransaction> InvalidTransaction(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<PracticeTransaction>.Invalid(
                code,
                path,
                message);
        }

        private static ContractValidationResult<bool> InvalidTransactionBoolean(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<bool>.Invalid(code, path, message);
        }

        private static bool TryParseDirection(
            string directionId,
            out GameRefuellingDirectionV1 direction)
        {
            if (string.Equals(directionId, "toward-end-a", StringComparison.OrdinalIgnoreCase))
            {
                direction = GameRefuellingDirectionV1.TowardEndA;
                return true;
            }

            if (string.Equals(directionId, "toward-end-b", StringComparison.OrdinalIgnoreCase))
            {
                direction = GameRefuellingDirectionV1.TowardEndB;
                return true;
            }

            direction = default(GameRefuellingDirectionV1);
            return false;
        }

        private static string DirectionId(GameRefuellingDirectionV1 direction)
        {
            return direction == GameRefuellingDirectionV1.TowardEndA
                ? "toward-end-a"
                : "toward-end-b";
        }
    }
}
