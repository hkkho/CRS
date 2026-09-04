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
        private readonly FullCoreDiffusionModelV1 _fullCoreModel;
        private SyntheticGameCoreStateV1 _coreState;
        private FullCoreDiffusionSolveResultV1 _fullCoreSolve;
        private readonly double _referenceEffectiveK;
        private double _lastFullCoreSolveSimulationTime;
        private double _syntheticScore;
        private double _scoreResetBaseline;
        private ulong _powerProjectionVersion;

        internal GameSession(
            Phase8ScoredScenarioRuntimeV1 runtime,
            IReadOnlyDictionary<string, Phase8PlaybackModeV1> playbackModes,
            uint wallControlTickMilliseconds,
            SyntheticGameCoreStateV1 coreState,
            FullCoreDiffusionModelV1 fullCoreModel)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _playbackModes = playbackModes ?? throw new ArgumentNullException(nameof(playbackModes));
            _wallControlTickMilliseconds = wallControlTickMilliseconds;
            _coreState = coreState ?? throw new ArgumentNullException(nameof(coreState));
            _fullCoreModel = fullCoreModel ?? throw new ArgumentNullException(nameof(fullCoreModel));
            _fullCoreSolve = RequireFullCoreSolve(
                SolveFullCore(_coreState, null));
            _referenceEffectiveK = _fullCoreSolve.EffectiveK;
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

            ContractValidationResult<FullCoreDiffusionSolveResultV1> projected =
                SolveFullCore(result.Value.ResultingState, _fullCoreSolve);
            if (!projected.IsValid)
            {
                return Rejected(
                    projected.FirstDiagnostic.Code,
                    projected.FirstDiagnostic.Message);
            }

            _coreState = result.Value.ResultingState;
            _fullCoreSolve = projected.Value;
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

            ContractValidationResult<FullCoreDiffusionSolveResultV1> projected =
                SolveFullCore(result.Value.ResultingState, _fullCoreSolve);
            if (!projected.IsValid)
            {
                return Rejected(
                    projected.FirstDiagnostic.Code,
                    projected.FirstDiagnostic.Message);
            }

            return new GameSessionCommandResult(
                true,
                string.Empty,
                string.Empty,
                FormatRefuellingMessage(result.Value, true),
                CreateSnapshot(),
                CreateCorePresentationSnapshot(
                    result.Value.ResultingState,
                    CurrentPowerFraction(),
                    projected.Value));
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
                CurrentPowerFraction(),
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
            bool stateChanged = false;
            foreach (Phase8ScenarioAdvanceSegmentV1 segment in advance.StateSegments)
            {
                double remainingSeconds = segment.SimulationTimeEndSeconds -
                                           segment.SimulationTimeStartSeconds;
                if (remainingSeconds <= 0.0)
                {
                    continue;
                }

                double requestedAmplitude = Clamp(segment.NormalizedPowerFraction, 0.0, 1.5);
                double actualAmplitude = CurrentPhysicsPowerFraction(
                    requestedAmplitude,
                    _fullCoreSolve);
                while (remainingSeconds > 0.0)
                {
                    double stepSeconds = Math.Min(600.0, remainingSeconds);
                    var deltaEnergy = new double[
                        checked((int)(GameCorePresentationConstants.ChannelCount *
                                     GameCorePresentationConstants.BundlePositionCount))];
                    for (int index = 0; index < deltaEnergy.Length; index++)
                    {
                        deltaEnergy[index] = _fullCoreSolve.NodePowerWatts[index] *
                                             actualAmplitude *
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
                    stateChanged = true;
                    _powerProjectionVersion = checked(_powerProjectionVersion + 1);
                    double powerQuality = 1.0 -
                        Clamp(Math.Abs(actualAmplitude - 1.0) / 0.02, 0.0, 1.0);
                    double tiltQuality = 1.0 -
                        Clamp(Math.Abs(segment.AbsoluteTiltFraction) / 0.05, 0.0, 1.0);
                    _syntheticScore += stepSeconds *
                        (0.35 * powerQuality + 0.15 * tiltQuality);
                    remainingSeconds -= stepSeconds;
                }
            }

            if (stateChanged &&
                _runtime.SimulationTimeSeconds - _lastFullCoreSolveSimulationTime >=
                PracticeGameSessionFactory.FullCoreDiffusionRecomputeIntervalSeconds)
            {
                _fullCoreSolve = RequireFullCoreSolve(
                    SolveFullCore(_coreState, _fullCoreSolve));
                _lastFullCoreSolveSimulationTime = _runtime.SimulationTimeSeconds;
            }
        }

        private GameCorePresentationSnapshot CreateCorePresentationSnapshot(
            SyntheticGameCoreStateV1 state)
        {
            return CreateCorePresentationSnapshot(state, CurrentPowerFraction());
        }

        private GameCorePresentationSnapshot CreateCorePresentationSnapshot(
            SyntheticGameCoreStateV1 state,
            double powerAmplitude)
        {
            return CreateCorePresentationSnapshot(state, powerAmplitude, _fullCoreSolve);
        }

        private GameCorePresentationSnapshot CreateCorePresentationSnapshot(
            SyntheticGameCoreStateV1 state,
            double powerAmplitude,
            FullCoreDiffusionSolveResultV1 projection)
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
            double actualPowerFraction = CurrentPhysicsPowerFraction(amplitude, projection);
            double meanChannelPowerWatts = projection.TotalPowerWatts * actualPowerFraction /
                                           GameCorePresentationConstants.ChannelCount;
            var channelPowerWatts = new double[
                (int)GameCorePresentationConstants.ChannelCount];
            for (int index = 0; index < projection.NodePowerWatts.Count; index++)
            {
                uint channelIndex = (uint)(index /
                    (int)GameCorePresentationConstants.BundlePositionCount);
                channelPowerWatts[(int)channelIndex] +=
                    projection.NodePowerWatts[index] * actualPowerFraction;
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
                    double bundlePower = projection.NodePowerWatts[nodeIndex++] * actualPowerFraction;
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

            var physics = new GamePhysicsPresentationSnapshot(
                projection.DataPack.ModelId,
                "converged",
                true,
                _powerProjectionVersion,
                PracticeGameSessionFactory.PracticeReferencePowerWatts,
                amplitude,
                actualPowerFraction,
                PracticeGameSessionFactory.PracticeReferencePowerWatts * amplitude,
                projection.TotalPowerWatts * actualPowerFraction,
                meanChannelPowerWatts,
                projection.TotalPowerWatts * actualPowerFraction /
                    (GameCorePresentationConstants.ChannelCount *
                     GameCorePresentationConstants.BundlePositionCount),
                projection.EffectiveK,
                projection.Reactivity,
                projection.PowerBalanceRelativeError,
                projection.SolverIdentity,
                projection.IterationCount,
                projection.ResidualRelativeInfinity);
            return new GameCorePresentationSnapshot(channels, physics);
        }

        private double CurrentPhysicsPowerFraction(
            double requestedPowerFraction,
            FullCoreDiffusionSolveResultV1 projection)
        {
            ContractValidationResult<double> response =
                FullCorePowerResponseV1.TryComputeNormalizedPowerFraction(
                    requestedPowerFraction,
                    projection.EffectiveK,
                    _referenceEffectiveK);
            if (!response.IsValid)
            {
                throw new InvalidOperationException(
                    "The full-core criticality power response failed: " +
                    response.FirstDiagnostic);
            }

            return response.Value;
        }

        private ContractValidationResult<FullCoreDiffusionSolveResultV1> SolveFullCore(
            SyntheticGameCoreStateV1 state,
            FullCoreDiffusionSolveResultV1? previous)
        {
            return _fullCoreModel.TrySolve(
                state.EnumerateBundles(),
                PracticeGameSessionFactory.PracticeReferencePowerWatts,
                previous == null ? 1.0 : previous.EffectiveK,
                previous?.Group1Flux,
                previous?.Group2Flux);
        }

        private static FullCoreDiffusionSolveResultV1 RequireFullCoreSolve(
            ContractValidationResult<FullCoreDiffusionSolveResultV1> result)
        {
            if (!result.IsValid)
            {
                throw new InvalidOperationException(
                    "The CANDU-6 full-core diffusion solve failed: " +
                    result.FirstDiagnostic);
            }

            return result.Value;
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
            return Clamp(_runtime.NormalizedPowerFraction, 0.0, 1.50);
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
