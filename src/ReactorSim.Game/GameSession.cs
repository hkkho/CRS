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
        private SyntheticGameCoreStateV1 _coreState;
        private readonly double[] _channelPowerResponses =
            new double[GameCorePresentationConstants.ChannelCount];
        private double _syntheticPowerDelta;
        private double _syntheticTiltX;
        private double _syntheticTiltY;
        private double _syntheticScore;
        private double _scoreResetBaseline;

        internal GameSession(
            Phase8ScoredScenarioRuntimeV1 runtime,
            IReadOnlyDictionary<string, Phase8PlaybackModeV1> playbackModes,
            uint wallControlTickMilliseconds,
            SyntheticGameCoreStateV1 coreState)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _playbackModes = playbackModes ?? throw new ArgumentNullException(nameof(playbackModes));
            _wallControlTickMilliseconds = wallControlTickMilliseconds;
            _coreState = coreState ?? throw new ArgumentNullException(nameof(coreState));
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
                ApplySyntheticAdvance(
                    result.Value.TurnSummary.SimulationTimeStartSeconds,
                    result.Value.TurnSummary.SimulationTimeEndSeconds);
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
            Array.Clear(_channelPowerResponses, 0, _channelPowerResponses.Length);
            _syntheticPowerDelta = 0.0;
            _syntheticTiltX = 0.0;
            _syntheticTiltY = 0.0;
            _syntheticScore = 0.0;
            _scoreResetBaseline = _runtime.Score.TotalPoints;
            return AcceptedMessage("Debug: synthetic response and score reset.");
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

            _coreState = result.Value.ResultingState;
            ApplySyntheticRefuellingResponse(result.Value);
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

            return new GameSessionCommandResult(
                true,
                string.Empty,
                string.Empty,
                FormatRefuellingMessage(result.Value, true),
                CreateSnapshot(),
                CreateCorePresentationSnapshot(result.Value.ResultingState));
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
                CreateCorePresentationSnapshot(_coreState));
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

        private void ApplySyntheticRefuellingResponse(GameRefuellingResultV1 result)
        {
            PracticeCoreGridPosition position = PracticeCoreLayout.GetPosition(result.ChannelIndex);
            double x = (position.Column - 10.5) / 10.5;
            double y = (10.5 - position.Row) / 10.5;
            double shiftFactor = result.ShiftCount / 4.0;
            double averageDischargedBurnup = result.DischargedBundles.Count == 0
                ? 0.0
                : result.DischargedBundles.Average(
                    bundle => bundle.CurrentBurnupJPerKgHm /
                              GameCorePresentationConstants.JoulesPerMegaWattDayPerKilogram);
            double utilizationQuality = Clamp((averageDischargedBurnup - 4.0) / 6.0, 0.0, 1.0);
            double responseMagnitude = 0.004 * shiftFactor + 0.002 * utilizationQuality;

            _channelPowerResponses[(int)result.ChannelIndex] += responseMagnitude;
            _syntheticPowerDelta += 0.0025 * shiftFactor + 0.0015 * utilizationQuality;
            _syntheticTiltX += x * responseMagnitude * 1.8;
            _syntheticTiltY += y * responseMagnitude * 1.8;
            _syntheticScore +=
                6.0 + 10.0 * utilizationQuality - 0.75 * shiftFactor;
        }

        private void ApplySyntheticAdvance(
            double simulationTimeStartSeconds,
            double simulationTimeEndSeconds)
        {
            double elapsedSeconds = simulationTimeEndSeconds - simulationTimeStartSeconds;
            if (elapsedSeconds <= 0.0)
            {
                return;
            }

            double decay = Math.Exp(-elapsedSeconds / 900.0);
            for (int index = 0; index < _channelPowerResponses.Length; index++)
            {
                _channelPowerResponses[index] *= decay;
            }

            _syntheticPowerDelta *= decay;
            _syntheticTiltX *= decay;
            _syntheticTiltY *= decay;

            double powerQuality = 1.0 - Clamp(Math.Abs(CurrentPowerFraction() - 1.0) / 0.02, 0.0, 1.0);
            double tiltQuality = 1.0 - Clamp(CurrentTiltFraction() / 0.05, 0.0, 1.0);
            _syntheticScore += elapsedSeconds * (0.35 * powerQuality + 0.15 * tiltQuality);
        }

        private GameCorePresentationSnapshot CreateCorePresentationSnapshot(
            SyntheticGameCoreStateV1 state)
        {
            var channels = new List<GameChannelPresentationSnapshot>(
                (int)GameCorePresentationConstants.ChannelCount);
            for (uint channelIndex = 0;
                 channelIndex < GameCorePresentationConstants.ChannelCount;
                 channelIndex++)
            {
                PracticeCoreGridPosition grid = PracticeCoreLayout.GetPosition(channelIndex);
                IReadOnlyList<BundleState> bundles = state.GetChannel(channelIndex);
                var bundleSnapshots = new List<GameBundlePresentationSnapshot>(
                    (int)GameCorePresentationConstants.BundlePositionCount);
                double burnupTotal = 0.0;
                foreach (BundleState bundle in bundles)
                {
                    double burnup = bundle.CurrentBurnupJPerKgHm /
                                    GameCorePresentationConstants.JoulesPerMegaWattDayPerKilogram;
                    burnupTotal += burnup;
                    bundleSnapshots.Add(
                        new GameBundlePresentationSnapshot(
                            bundle.Position.Value,
                            bundle.BundleId.ToString(),
                            bundle.MaterialVariantId.Value,
                            burnup,
                            bundle.InsertedAtSeconds,
                            bundle.StateVersion));
                }

                double averageBurnup = burnupTotal / GameCorePresentationConstants.BundlePositionCount;
                double centeredX = (grid.Column - 10.5) / 11.5;
                double centeredY = (grid.Row - 10.5) / 11.5;
                double radialDistance = Math.Sqrt(centeredX * centeredX + centeredY * centeredY);
                double radial = Clamp(1.0 - Math.Pow(radialDistance, 1.45), 0.0, 1.0);
                double azimuthalRipple =
                    (Math.Sin((grid.Column + 1) * 0.73) + Math.Cos((grid.Row + 1) * 0.61)) * 0.005;
                double localPower = Clamp(
                    0.74 + 0.34 * radial + azimuthalRipple +
                    _channelPowerResponses[(int)channelIndex],
                    0.64,
                    1.28);
                double localTilt = Math.Abs(_channelPowerResponses[(int)channelIndex]) +
                                   Math.Abs(centeredY * _syntheticTiltY) +
                                   Math.Abs(centeredX * _syntheticTiltX);

                channels.Add(
                    new GameChannelPresentationSnapshot(
                        channelIndex,
                        grid.Column,
                        grid.Row,
                        averageBurnup,
                        localPower,
                        localTilt,
                        PracticeCoreLayout.GetFlowDirection(grid),
                        bundleSnapshots));
            }

            return new GameCorePresentationSnapshot(channels);
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
            return Clamp(_runtime.NormalizedPowerFraction + _syntheticPowerDelta, 0.0, 1.50);
        }

        private double CurrentTiltFraction()
        {
            return Clamp(
                _runtime.AbsoluteTiltFraction +
                Math.Sqrt(_syntheticTiltX * _syntheticTiltX + _syntheticTiltY * _syntheticTiltY),
                0.0,
                1.0);
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
