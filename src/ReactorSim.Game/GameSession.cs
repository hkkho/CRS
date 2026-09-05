using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
    }

    public sealed class GameSessionCommandResult
    {
        internal GameSessionCommandResult(
            bool accepted,
            string diagnosticCode,
            string diagnosticMessage,
            string message,
            GameSessionSnapshot snapshot,
            GameCorePresentationSnapshot? previewCore)
        {
            Accepted = accepted;
            DiagnosticCode = diagnosticCode;
            DiagnosticMessage = diagnosticMessage;
            Message = message;
            Snapshot = snapshot;
            PreviewCore = previewCore;
        }

        public bool Accepted { get; }

        public string DiagnosticCode { get; }

        public string DiagnosticMessage { get; }

        public string Message { get; }

        public GameSessionSnapshot Snapshot { get; }

        public GameCorePresentationSnapshot? PreviewCore { get; }
    }

    public sealed class GameSession
    {
        private readonly Phase8ScoredScenarioRuntimeV1 _runtime;
        private readonly IReadOnlyDictionary<string, Phase8PlaybackModeV1> _playbackModes;
        private readonly uint _wallControlTickMilliseconds;
        private readonly IqsFullCoreSolver _adiabaticSolver;
        private SyntheticPracticeRegulatorV1 _practiceRegulator;
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
            IqsFullCoreSolver adiabaticSolver,
            SyntheticPracticeRegulatorV1 practiceRegulator)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _playbackModes = playbackModes ?? throw new ArgumentNullException(nameof(playbackModes));
            _wallControlTickMilliseconds = wallControlTickMilliseconds;
            _coreState = coreState ?? throw new ArgumentNullException(nameof(coreState));
            _adiabaticSolver = adiabaticSolver ?? throw new ArgumentNullException(nameof(adiabaticSolver));
            _practiceRegulator = practiceRegulator ?? throw new ArgumentNullException(nameof(practiceRegulator));
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

        public GameSessionCommandResult AdvanceWallMilliseconds(ulong wallMilliseconds)
        {
            ContractValidationResult<Phase8ScoredAdvanceResultV1> result =
                _runtime.TryAdvanceWallMilliseconds(wallMilliseconds);
            if (result.IsValid)
            {
                ApplyPracticeAdvance(result.Value.Advance);
            }

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

            ContractValidationResult<IqsSpatialCandidateV1> projected =
                _adiabaticSolver.TrySolveCandidate(result.Value.ResultingState.EnumerateBundles());
            if (!projected.IsValid)
            {
                return Rejected(
                    projected.FirstDiagnostic.Code,
                    projected.FirstDiagnostic.Message);
            }

            ContractValidationResult<SyntheticPracticeRegulatorV1> reboundRegulator =
                _practiceRegulator.TryBindCoreReactivity(
                    projected.Value.SpatialSolve.Reactivity,
                    _runtime.SimulationTimeSeconds);
            if (!reboundRegulator.IsValid)
            {
                return Rejected(
                    reboundRegulator.FirstDiagnostic.Code,
                    reboundRegulator.FirstDiagnostic.Message);
            }

            ContractValidationResult<bool> committed =
                _adiabaticSolver.TryCommitCandidate(projected.Value);
            if (!committed.IsValid)
            {
                return Rejected(
                    committed.FirstDiagnostic.Code,
                    committed.FirstDiagnostic.Message);
            }

            _coreState = result.Value.ResultingState;
            _practiceRegulator = reboundRegulator.Value;
            _lastFullCoreSolveSimulationTime = _runtime.SimulationTimeSeconds;
            ApplyPracticeRefuellingScore(result.Value);
            _powerProjectionVersion = checked(_powerProjectionVersion + 1);
            string message = FormatRefuellingMessage(result.Value, false);
            return new GameSessionCommandResult(
                true,
                string.Empty,
                string.Empty,
                message,
                CreateSnapshot(),
                null);
        }

        public GameSessionCommandResult PreviewRefuelChannel(
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

            ContractValidationResult<IqsSpatialCandidateV1> projected =
                _adiabaticSolver.TrySolveCandidate(result.Value.ResultingState.EnumerateBundles());
            if (!projected.IsValid)
            {
                return Rejected(
                    projected.FirstDiagnostic.Code,
                    projected.FirstDiagnostic.Message);
            }

            ContractValidationResult<SyntheticPracticeRegulatorV1> previewRegulator =
                _practiceRegulator.TryBindCoreReactivity(
                    projected.Value.SpatialSolve.Reactivity,
                    _runtime.SimulationTimeSeconds);
            if (!previewRegulator.IsValid)
            {
                return Rejected(
                    previewRegulator.FirstDiagnostic.Code,
                    previewRegulator.FirstDiagnostic.Message);
            }

            return new GameSessionCommandResult(
                true,
                string.Empty,
                string.Empty,
                FormatRefuellingMessage(result.Value, true),
                CreateSnapshot(),
                CreateCorePresentationSnapshot(
                    result.Value.ResultingState,
                    CurrentPowerFraction(previewRegulator.Value),
                    projected.Value,
                    previewRegulator.Value));
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
                CreateSnapshot(),
                null);
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
                CreateSnapshot(),
                null);
        }

        private GameSessionCommandResult Rejected(string code, string message)
        {
            return new GameSessionCommandResult(
                false,
                code,
                message,
                message,
                CreateSnapshot(),
                null);
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

        private void ApplyPracticeRefuellingScore(GameRefuellingResultV1 result)
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
            _syntheticScore +=
                6.0 + 10.0 * utilizationQuality - 0.75 * shiftFactor;
        }

        private void ApplyPracticeAdvance(Phase8ScenarioAdvanceResultV1 advance)
        {
            foreach (Phase8ScenarioAdvanceSegmentV1 segment in advance.StateSegments)
            {
                double remainingSeconds = segment.SimulationTimeEndSeconds -
                                           segment.SimulationTimeStartSeconds;
                if (remainingSeconds <= 0.0)
                {
                    continue;
                }

                double requestedAmplitude = Clamp(segment.NormalizedPowerFraction, 0.0, 1.5);
                double simulationCursor = segment.SimulationTimeStartSeconds;
                while (remainingSeconds > 0.0)
                {
                    double untilShapeSolve =
                        _adiabaticSolver.DataPack.ShapeRecomputeIntervalSeconds -
                        (simulationCursor - _lastFullCoreSolveSimulationTime);
                    if (untilShapeSolve <= 1e-9)
                    {
                        CommitScheduledShape(simulationCursor);
                        continue;
                    }

                    double stepSeconds = Math.Min(
                        PracticeGameSessionFactory.SteadyStateLongStepSeconds,
                        Math.Min(remainingSeconds, untilShapeSolve));
                    double stepEnd = simulationCursor + stepSeconds;
                    ContractValidationResult<SyntheticPracticeRegulatorV1> regulation =
                        _practiceRegulator.TryAdvance(
                            _adiabaticSolver.CurrentSpatialSolve.Reactivity,
                            stepEnd);
                    if (!regulation.IsValid)
                    {
                        throw new InvalidOperationException(
                            "The synthetic steady-state regulation advance failed: " +
                            regulation.FirstDiagnostic);
                    }

                    _practiceRegulator = regulation.Value;
                    double actualAmplitude = PowerAmplitudeFor(
                        requestedAmplitude,
                        _practiceRegulator);

                    double physicalShapeScale = actualAmplitude * _adiabaticSolver.Amplitude;
                    var deltaEnergy = new double[
                        checked((int)(GameCorePresentationConstants.ChannelCount *
                                     GameCorePresentationConstants.BundlePositionCount))];
                    for (int index = 0; index < deltaEnergy.Length; index++)
                    {
                        deltaEnergy[index] =
                            _adiabaticSolver.CurrentProjection.ShapeNodePowerWatts[index] *
                            physicalShapeScale *
                            stepSeconds;
                    }

                    ContractValidationResult<SyntheticGameCoreStateV1> integrated =
                        _coreState.TryAddFissionEnergy(deltaEnergy);
                    if (!integrated.IsValid)
                    {
                        throw new InvalidOperationException(
                            "The full-core practice burnup integration failed: " +
                            integrated.FirstDiagnostic);
                    }

                    _coreState = integrated.Value;
                    _powerProjectionVersion = checked(_powerProjectionVersion + 1);
                    double actualPowerFraction =
                        _adiabaticSolver.CurrentProjection.ShapePowerWatts * physicalShapeScale /
                        PracticeGameSessionFactory.PracticeReferencePowerWatts;
                    double powerQuality = 1.0 -
                        Clamp(Math.Abs(actualPowerFraction - 1.0) / 0.02, 0.0, 1.0);
                    double tiltQuality = 1.0 -
                        Clamp(Math.Abs(segment.AbsoluteTiltFraction) / 0.05, 0.0, 1.0);
                    _syntheticScore += stepSeconds *
                        (0.35 * powerQuality + 0.15 * tiltQuality);
                    remainingSeconds -= stepSeconds;
                    simulationCursor = stepEnd;
                    if (simulationCursor - _lastFullCoreSolveSimulationTime >=
                        _adiabaticSolver.DataPack.ShapeRecomputeIntervalSeconds - 1e-9)
                    {
                        CommitScheduledShape(simulationCursor);
                    }
                }
            }
        }

        private void CommitScheduledShape(double simulationTimeSeconds)
        {
            ContractValidationResult<IqsSpatialCandidateV1> candidate =
                _adiabaticSolver.TrySolveCandidate(_coreState.EnumerateBundles());
            if (!candidate.IsValid)
            {
                throw new InvalidOperationException(
                    "The scheduled static-eigenmode full-core shape solve failed: " +
                    candidate.FirstDiagnostic);
            }

            ContractValidationResult<SyntheticPracticeRegulatorV1> reboundRegulator =
                _practiceRegulator.TryBindCoreReactivity(
                    candidate.Value.SpatialSolve.Reactivity,
                    simulationTimeSeconds);
            if (!reboundRegulator.IsValid)
            {
                throw new InvalidOperationException(
                    "The scheduled synthetic regulation binding failed: " +
                    reboundRegulator.FirstDiagnostic);
            }

            ContractValidationResult<bool> committed =
                _adiabaticSolver.TryCommitCandidate(candidate.Value);
            if (!committed.IsValid)
            {
                throw new InvalidOperationException(
                    "The scheduled static-eigenmode shape commit failed: " +
                    committed.FirstDiagnostic);
            }

            _practiceRegulator = reboundRegulator.Value;
            _lastFullCoreSolveSimulationTime = simulationTimeSeconds;
            _powerProjectionVersion = checked(_powerProjectionVersion + 1);
        }

        private GameCorePresentationSnapshot CreateCorePresentationSnapshot(
            SyntheticGameCoreStateV1 state)
        {
            return CreateCorePresentationSnapshot(
                state,
                CurrentPowerFraction(),
                _adiabaticSolver.CurrentProjection,
                _practiceRegulator);
        }

        private GameCorePresentationSnapshot CreateCorePresentationSnapshot(
            SyntheticGameCoreStateV1 state,
            double powerAmplitude,
            IqsSpatialCandidateV1 projection,
            SyntheticPracticeRegulatorV1 regulator)
        {
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
            double physicalShapeScale = amplitude * _adiabaticSolver.Amplitude;
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
                        bundleSnapshots));
            }

            FullCoreDiffusionSolveResultV1 spatial = projection.SpatialSolve;
            double targetPowerAmplitude = Clamp(
                _runtime.NormalizedPowerFraction,
                0.0,
                1.5);
            var physics = new GamePhysicsPresentationSnapshot(
                _adiabaticSolver.DataPack.ModelId,
                _adiabaticSolver.FormulationId,
                _adiabaticSolver.ShapeMethodId,
                _adiabaticSolver.AmplitudeMethodId,
                _adiabaticSolver.ReactivityMethodId,
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
                spatial.PowerBalanceRelativeError,
                _adiabaticSolver.DataPack.SolverId + "/" +
                    _adiabaticSolver.DataPack.DataPackVersion + "+" +
                    spatial.SolverIdentity,
                spatial.IterationCount,
                spatial.ResidualRelativeInfinity,
                regulator.CoreReactivity,
                regulator.CompensatedNetReactivity,
                regulator.CompensationState,
                regulator.CompensationCommand,
                regulator.LowerBound,
                regulator.UpperBound,
                regulator.CompensationSaturated,
                regulator.ResponseTimeSeconds,
                regulator.CadenceIdentity);
            return new GameCorePresentationSnapshot(channels, physics);
        }

        private string FormatRefuellingMessage(
            GameRefuellingResultV1 result,
            bool preview)
        {
            string endName = result.Direction == GameRefuellingDirectionV1.TowardEndA
                ? "End A"
                : "End B";
            double averageDischargedBurnup = result.DischargedBundles.Count == 0
                ? 0.0
                : result.DischargedBundles.Average(
                    bundle => bundle.CurrentBurnupJPerKgHm /
                              GameCorePresentationConstants.JoulesPerMegaWattDayPerKilogram);
            string prefix = preview ? "Preview: " : string.Empty;
            string inventory = preview
                ? string.Empty
                : "; " + _coreState.FreshBundlesAvailable.ToString(CultureInfo.InvariantCulture) +
                  " fresh bundles remain";
            return prefix + "Channel " + result.ChannelIndex.ToString(CultureInfo.InvariantCulture) +
                   " refuelled toward " + endName + " with " +
                   result.ShiftCount.ToString(CultureInfo.InvariantCulture) + " " +
                   result.FuelTypeId + " bundles; predicted discharge burnup " +
                   averageDischargedBurnup.ToString("0.00", CultureInfo.InvariantCulture) +
                   " MWd/kg HM" + inventory + ".";
        }

        private double CurrentPowerFraction()
        {
            return CurrentPowerFraction(_practiceRegulator);
        }

        private double CurrentPowerFraction(
            SyntheticPracticeRegulatorV1 regulator)
        {
            return PowerAmplitudeFor(
                _runtime.NormalizedPowerFraction,
                regulator);
        }

        private static double PowerAmplitudeFor(
            double requestedAmplitude,
            SyntheticPracticeRegulatorV1 regulator)
        {
            double target = Clamp(requestedAmplitude, 0.0, 1.5);
            if (target <= 0.0)
            {
                return 0.0;
            }

            double netReactivity = regulator.CompensatedNetReactivity;
            double responseMultiplier = netReactivity <= -1.0
                ? 0.0
                : 1.0 + netReactivity;
            if (double.IsNaN(responseMultiplier) ||
                double.IsInfinity(responseMultiplier))
            {
                responseMultiplier = netReactivity > 0.0 ? 1.5 : 0.0;
            }

            return Clamp(target * responseMultiplier, 0.0, 1.5);
        }

        private double CurrentTiltFraction()
        {
            return Clamp(Math.Abs(_runtime.AbsoluteTiltFraction), 0.0, 1.0);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
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
