using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ReactorSim.Core;

namespace ReactorSim.Game
{
    public sealed class GameSession
    {
        private readonly PracticeRunClock _runtime;
        private readonly IReadOnlyDictionary<string, Phase8PlaybackModeV1> _playbackModes;
        private readonly uint _wallControlTickMilliseconds;
        private EquilibriumCoreSolverV1 _equilibriumSolver;
        private PracticeLiquidZoneRrsV1 _practiceRrs;
        private SyntheticGameCoreStateV1 _coreState;
        private double _lastFullCoreSolveSimulationTime;
        private double _syntheticScore;
        private readonly bool _challenge;
        private double _thermalEnergyJoules;
        private uint _fuelConsumed;
        private uint _usefulBundlesDischarged;
        private double _dischargeReward;
        private double _freshFuelCost;
        private RefuellingScoreBreakdown? _lastRefuellingScore;
        private RefuellingMovement? _lastFuelMovement;
        private readonly List<string> _modificationReasons = new List<string>();
        private double? _lastDischargedMaximumBurnupMwDayPerKg;
        private double? _maximumDischargedBurnupMwDayPerKg;
        private ulong _powerProjectionVersion;
        private PracticeXenonStateV1 _xenon;
        private PracticeXenonStateV1 _coupledXenon;
        private IReadOnlyList<NodeKey> _nonfuelNodes;
        private IReadOnlyDictionary<NodeKey, IReadOnlyList<TopologyFace>>
            _reflectiveFaceOverrides;

        internal GameSession(
            PracticeRunClock runtime,
            IReadOnlyDictionary<string, Phase8PlaybackModeV1> playbackModes,
            uint wallControlTickMilliseconds,
            SyntheticGameCoreStateV1 coreState,
            EquilibriumCoreSolverV1 equilibriumSolver,
            PracticeLiquidZoneRrsV1 practiceRrs,
            bool challenge = false)
        {
            _challenge = challenge;
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _playbackModes = playbackModes ?? throw new ArgumentNullException(nameof(playbackModes));
            _wallControlTickMilliseconds = wallControlTickMilliseconds;
            _coreState = coreState ?? throw new ArgumentNullException(nameof(coreState));
            _equilibriumSolver = equilibriumSolver ?? throw new ArgumentNullException(nameof(equilibriumSolver));
            _practiceRrs = practiceRrs ?? throw new ArgumentNullException(nameof(practiceRrs));
            _xenon = PracticeXenonStateV1.CreateEquilibrium(coreState, equilibriumSolver.CurrentProjection,
                _runtime.SimulationTimeSeconds);
            _coupledXenon = _xenon;
            _lastFullCoreSolveSimulationTime = _runtime.SimulationTimeSeconds;
            _nonfuelNodes = new ReadOnlyCollection<NodeKey>(Array.Empty<NodeKey>());
            _reflectiveFaceOverrides =
                new ReadOnlyDictionary<NodeKey, IReadOnlyList<TopologyFace>>(
                    new Dictionary<NodeKey, IReadOnlyList<TopologyFace>>());
        }

        public GameSessionSnapshot Snapshot
        {
            get { return CreateSnapshot(); }
        }

        public SyntheticGameCoreStateV1 CoreState
        {
            get { return _coreState; }
        }

        public EquilibriumCoreProjectionV1 CurrentSpatialCandidate
        {
            get { return _equilibriumSolver.CurrentProjection; }
        }

        public EquilibriumCoreProjectionV1 CurrentEquilibriumProjection
        {
            get { return _equilibriumSolver.CurrentProjection; }
        }

        public PracticeLiquidZoneRrsV1 CurrentLiquidZoneRrs
        {
            get { return _practiceRrs; }
        }

        public PracticeXenonStateV1 CurrentXenonState => _xenon;

        public bool IsFuelCell(uint channelIndex, uint position)
        {
            NodeKey node = GetNodeKey(channelIndex, position);
            return !_nonfuelNodes.Contains(node);
        }

        public IReadOnlyCollection<TopologyFace> GetReflectiveFaces(
            uint channelIndex,
            uint position)
        {
            NodeKey node = GetNodeKey(channelIndex, position);
            if (_reflectiveFaceOverrides.TryGetValue(node, out IReadOnlyList<TopologyFace>? faces))
            {
                return faces;
            }

            return Array.Empty<TopologyFace>();
        }

        public double GetCellGroup1Flux(uint channelIndex, uint position)
        {
            return GetCellFlux(_equilibriumSolver.CurrentProjection.ShapeGroup1, channelIndex, position);
        }

        public double GetCellGroup2Flux(uint channelIndex, uint position)
        {
            return GetCellFlux(_equilibriumSolver.CurrentProjection.ShapeGroup2, channelIndex, position);
        }

        public GameSessionCommandResult ConfigureCell(
            uint channelIndex,
            uint position,
            bool hasFuel,
            IReadOnlyCollection<TopologyFace> reflectiveFaces)
        {
            if (IsRunTerminal)
            {
                return RejectGameOver();
            }

            ContractValidationResult<ConfiguredCoreDesign> design =
                TryBuildConfiguredCoreDesign(
                    channelIndex,
                    position,
                    hasFuel,
                    reflectiveFaces);
            if (!design.IsValid)
            {
                return Rejected(
                    design.FirstDiagnostic.Code,
                    design.FirstDiagnostic.Message);
            }

            ContractValidationResult<ConfiguredCoreTransaction> transaction =
                TryBuildConfiguredCoreTransaction(design.Value);
            if (!transaction.IsValid)
            {
                return Rejected(
                    transaction.FirstDiagnostic.Code,
                    transaction.FirstDiagnostic.Message);
            }

            bool geometryChanged = !_nonfuelNodes.SequenceEqual(design.Value.NonfuelNodes) ||
                _reflectiveFaceOverrides.Count != design.Value.ReflectiveFaceOverrides.Count ||
                _reflectiveFaceOverrides.Any(entry => !design.Value.ReflectiveFaceOverrides.TryGetValue(entry.Key, out var faces) ||
                    !entry.Value.SequenceEqual(faces));
            ApplyConfiguredCoreTransaction(transaction.Value);
            if (geometryChanged) MarkModified("Fuel or reflective faces edited");
            return AcceptedMessage(
                "Core cell " + channelIndex.ToString(CultureInfo.InvariantCulture) +
                ":" + position.ToString(CultureInfo.InvariantCulture) +
                " configuration committed.");
        }

        public GameSessionCommandResult SolveConfiguredCore()
        {
            if (IsRunTerminal)
            {
                return RejectGameOver();
            }

            ConfiguredCoreDesign design =
                new ConfiguredCoreDesign(
                    _nonfuelNodes,
                    _reflectiveFaceOverrides.Select(
                        entry => new KeyValuePair<NodeKey, IEnumerable<TopologyFace>>(
                            entry.Key,
                            entry.Value)));
            ContractValidationResult<ConfiguredCoreTransaction> transaction =
                TryBuildConfiguredCoreTransaction(design);
            if (!transaction.IsValid)
            {
                return Rejected(
                    transaction.FirstDiagnostic.Code,
                    transaction.FirstDiagnostic.Message);
            }

            ApplyConfiguredCoreTransaction(transaction.Value);
            return AcceptedMessage("Configured full-core equilibrium solve committed.");
        }

        /// <summary>Replace regional membership and homogenized absorber footprints atomically.
        /// Retains fuel, clock, score and zone levels; rebuilds spatial references for the new layout.</summary>
        public GameSessionCommandResult ConfigureZoneLayout(
            IEnumerable<PracticeLiquidZoneRrsNodeBindingV1> nodes)
        {
            if (IsRunTerminal) return RejectGameOver();
            var mapping = PracticeLiquidZoneRrsMappingV1.TryCreate(nodes);
            if (!mapping.IsValid) return Rejected(mapping.FirstDiagnostic.Code, mapping.FirstDiagnostic.Message);
            var design = new ConfiguredCoreDesign(_nonfuelNodes,
                _reflectiveFaceOverrides.Select(entry =>
                    new KeyValuePair<NodeKey, IEnumerable<TopologyFace>>(entry.Key, entry.Value)));
            var transaction = TryBuildConfiguredCoreTransaction(design, mapping.Value);
            if (!transaction.IsValid) return Rejected(transaction.FirstDiagnostic.Code, transaction.FirstDiagnostic.Message);
            bool geometryChanged = !mapping.Value.MappingDigest.Equals(_practiceRrs.MappingDigest);
            ApplyConfiguredCoreTransaction(transaction.Value);
            if (geometryChanged) MarkModified("Zone geometry edited");
            return AcceptedMessage("Zone layout committed; spatial references rebuilt and existing fills retained as the regulation starting point.");
        }

        public GameSessionCommandResult AdvanceWallMilliseconds(ulong wallMilliseconds)
        {
            if (IsRunTerminal)
            {
                return RejectGameOver();
            }

            ContractValidationResult<PracticeRunAdvance> planned =
                _runtime.TryPlanAdvanceWallMilliseconds(wallMilliseconds);
            if (!planned.IsValid)
            {
                return Rejected(
                    planned.FirstDiagnostic.Code,
                    planned.FirstDiagnostic.Message);
            }

            ContractValidationResult<PracticeCandidate> transaction =
                TryBuildPracticeAdvance(planned.Value.Advance);
            if (!transaction.IsValid)
            {
                return Rejected(
                    transaction.FirstDiagnostic.Code,
                    transaction.FirstDiagnostic.Message);
            }

            ulong acceptedWallMilliseconds = wallMilliseconds;
            if (HasOperatingLoss(transaction.Value.Rrs, transaction.Value.SpatialCandidate,
                transaction.Value.OperatingPowerAmplitude))
            {
                ContractValidationResult<ulong> terminalWallMilliseconds =
                    TryFindTerminalWallMilliseconds(wallMilliseconds);
                if (!terminalWallMilliseconds.IsValid)
                {
                    return Rejected(
                        terminalWallMilliseconds.FirstDiagnostic.Code,
                        terminalWallMilliseconds.FirstDiagnostic.Message);
                }

                acceptedWallMilliseconds = terminalWallMilliseconds.Value;
                if (acceptedWallMilliseconds != wallMilliseconds)
                {
                    planned = _runtime.TryPlanAdvanceWallMilliseconds(
                        acceptedWallMilliseconds);
                    if (!planned.IsValid)
                    {
                        return Rejected(
                            planned.FirstDiagnostic.Code,
                            planned.FirstDiagnostic.Message);
                    }

                    transaction = TryBuildPracticeAdvance(planned.Value.Advance);
                    if (!transaction.IsValid)
                    {
                        return Rejected(
                            transaction.FirstDiagnostic.Code,
                            transaction.FirstDiagnostic.Message);
                    }
                }
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

            ContractValidationResult<bool> result =
                _runtime.TryCommitAdvance(transaction.Value.Clock!);
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

            ApplyPracticeCandidate(transaction.Value);
            return Complete(result);
        }

        public GameSessionCommandResult QueuePowerTarget(double targetFraction)
        {
            if (IsRunTerminal)
            {
                return RejectGameOver();
            }

            return Complete(_runtime.TryQueuePowerTarget(targetFraction));
        }

        public GameSessionCommandResult SetPlaybackMode(string playbackModeId)
        {
            if (IsRunTerminal)
            {
                return RejectGameOver();
            }

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
            if (IsRunTerminal)
            {
                return RejectGameOver();
            }

            return Complete(_runtime.TryPause());
        }

        public GameSessionCommandResult Resume()
        {
            if (IsRunTerminal)
            {
                return RejectGameOver();
            }

            return Complete(_runtime.TryResume());
        }

        public GameSessionCommandResult DebugGrantFreshBundles(uint additionalBundles)
        {
            if (IsRunTerminal) return RejectGameOver();
            if (additionalBundles == 0)
            {
                return Rejected(
                    "GameSession.Debug.Inventory.Invalid",
                    "The debug inventory grant must be greater than zero.");
            }

            _coreState = _coreState.WithFreshBundles(additionalBundles);
            MarkModified("Developer fuel grant");
            return AcceptedMessage(
                "Debug: granted " + additionalBundles.ToString(CultureInfo.InvariantCulture) +
                " fresh bundles; run marked modified sandbox.");
        }

        public GameSessionCommandResult DebugClearPendingActions()
        {
            if (IsRunTerminal) return RejectGameOver();
            var cleared = _runtime.TryClearPendingActions();
            if (cleared.IsValid && cleared.Value > 0) MarkModified("Developer actions cleared");
            return CompleteWithMessage(cleared, clearedCount =>
                "Debug: cleared " + clearedCount.ToString(CultureInfo.InvariantCulture) + " pending actions.");
        }

        public GameSessionCommandResult DebugResetSyntheticResponse()
        {
            if (IsRunTerminal) return RejectGameOver();
            if (_syntheticScore != 0.0 || _dischargeReward != 0.0 || _freshFuelCost != 0.0)
                MarkModified("Developer score reset");
            _syntheticScore = 0.0;
            _dischargeReward = 0.0;
            _freshFuelCost = 0.0;
            return AcceptedMessage("Debug: practice score adjustment reset.");
        }

        public GameSessionCommandResult RefuelChannel(
            uint channelIndex,
            string directionId,
            ushort shiftCount,
            string fuelTypeId)
        {
            if (IsRunTerminal)
            {
                return RejectGameOver();
            }

            if (!TryParseDirection(directionId, out GameRefuellingDirectionV1 direction))
            {
                return Rejected(
                    "GameSession.Refuelling.Direction.Invalid",
                    "Choose either toward-end-a or toward-end-b.");
            }

            if (RefuellingIneligibilityReason(channelIndex).Length > 0)
            {
                return Rejected(
                    "GameSession.Refuelling.Channel.Nonfuel",
                    RefuellingIneligibilityReason(channelIndex));
            }

            if (shiftCount != 8)
                return Rejected("GameRefuelling.ShiftCount.Unsupported", "Refuelling uses eight fresh bundles per move.");
            if (channelIndex >= SyntheticGameCoreStateV1.ChannelCount)
                return Rejected("GameRefuelling.Channel.OutOfRange", "Choose a channel from 0 through 379.");
            // Adjacent channels have opposite flow. Always fuel with the selected channel's flow.
            direction = Candu6CoreTopologyFactoryV1.GetFlowDirection(
                Candu6CoreTopologyFactoryV1.GetPosition(channelIndex)) == FlowDirection.EndAtoEndB
                ? GameRefuellingDirectionV1.TowardEndB : GameRefuellingDirectionV1.TowardEndA;

            ContractValidationResult<GameRefuellingResultV1> result = TryRefuel(
                channelIndex,
                direction,
                shiftCount,
                fuelTypeId);
            if (!result.IsValid)
            {
                return Rejected(result.FirstDiagnostic.Code, result.FirstDiagnostic.Message);
            }

            ContractValidationResult<PracticeCandidate> transaction =
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

            var previousCore = _coreState;
            ApplyPracticeCandidate(transaction.Value);
            _lastRefuellingScore = PracticeScoring.DescribeRefuelling(result.Value);
            _lastFuelMovement = new RefuellingMovement(previousCore, result.Value, _lastRefuellingScore);
            _fuelConsumed += checked((uint)result.Value.DischargedBundles.Count);
            _usefulBundlesDischarged += checked((uint)result.Value.DischargedBundles.Count(bundle =>
                bundle.CurrentBurnupJPerKgHm / GameCorePresentationConstants.JoulesPerMegaWattDayPerKilogram >=
                ShiftProgress.UsefulBurnupThresholdMwDayPerKg));
            _dischargeReward += _lastRefuellingScore.DischargeReward;
            _freshFuelCost += _lastRefuellingScore.FreshFuelCost;
            string message = FormatRefuellingMessage(result.Value);
            _lastDischargedMaximumBurnupMwDayPerKg = result.Value.DischargedBundles.Max(
                bundle => bundle.CurrentBurnupJPerKgHm /
                    GameCorePresentationConstants.JoulesPerMegaWattDayPerKilogram);
            _maximumDischargedBurnupMwDayPerKg = Math.Max(
                _maximumDischargedBurnupMwDayPerKg ?? 0.0,
                _lastDischargedMaximumBurnupMwDayPerKg.Value);
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
#if RUNTIME_PROFILE
            using var profileScope = ReactorSim.Core.RuntimeProfile.Measure("game-snapshot");
#endif
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
                core.AxialTiltFraction,
                ComputeRrsReserveFraction(core.Rrs),
                _runtime.DeviceAvailableFraction,
                _runtime.RefuelRequestsRemaining,
                _runtime.PendingActionCount,
                _runtime.ProcessedScriptedEventCount,
                _syntheticScore,
                _runtime.TurnSummaryCount,
                _runtime.Outcome.ToString(),
                IsRunTerminal,
                RunEndReason,
                _runtime.IsPaused,
                _coreState.FreshBundlesAvailable,
                _coreState.RefuellingOperationCount,
                _coreState.LastRefuelledChannel,
                DirectionId(_coreState.LastDirection),
                _coreState.LastShiftCount,
                _lastDischargedMaximumBurnupMwDayPerKg,
                _maximumDischargedBurnupMwDayPerKg,
                core,
                _lastRefuellingScore,
                new ShiftProgress(_challenge, _runtime.Seed, _runtime.ScenarioHorizonSeconds,
                    _runtime.SimulationTimeSeconds, IsRunTerminal,
                    _runtime.Outcome == PracticeRunOutcome.SurvivedScenarioHorizon && !HasOperatingLoss(_practiceRrs, CurrentEquilibriumProjection,
                        _runtime.NormalizedPowerFraction),
                    _fuelConsumed, _usefulBundlesDischarged, _thermalEnergyJoules,
                    _dischargeReward, _freshFuelCost, _syntheticScore, _modificationReasons.Count == 0,
                    _coreState.UnlimitedFreshFuel),
                _lastFuelMovement, new RunProvenance(_challenge, _modificationReasons),
                new ChannelRippleSnapshot(PracticeGameSessionFactory.ReferenceChannelPower,
                    CurrentEquilibriumProjection.ShapeChannelPowerWatts, core.Physics.PowerAmplitude));
        }

        private bool IsRunTerminal => HasOperatingLoss(_practiceRrs, CurrentEquilibriumProjection,
            _runtime.NormalizedPowerFraction) ||
            _runtime.Outcome != PracticeRunOutcome.Running;

        private static string OperatingEndReason(PracticeLiquidZoneRrsV1 rrs, EquilibriumCoreProjectionV1 projection,
            double powerAmplitude)
            => PracticeOperatingLimits.EndReason(rrs.AverageFillFraction,
                GamePresentationProjector.ComputeSignedAxialTiltFraction(projection.SpatialSolve.Group2Flux),
                projection.ShapeChannelPowerWatts.Max() * Clamp(powerAmplitude, 0, 1.5),
                projection.ShapeNodePowerWatts.Max() * Clamp(powerAmplitude, 0, 1.5));

        private static bool HasOperatingLoss(PracticeLiquidZoneRrsV1 rrs, EquilibriumCoreProjectionV1 projection,
            double powerAmplitude)
            => rrs.IsGameOver || OperatingEndReason(rrs, projection, powerAmplitude).Length > 0;

        private string RunEndReason => _practiceRrs.IsGameOver
            ? _practiceRrs.GameOverReason
            : OperatingEndReason(_practiceRrs, CurrentEquilibriumProjection, _runtime.NormalizedPowerFraction).Length > 0
                ? OperatingEndReason(_practiceRrs, CurrentEquilibriumProjection, _runtime.NormalizedPowerFraction)
            : _runtime.Outcome == PracticeRunOutcome.SurvivedScenarioHorizon
                ? _challenge ? "Challenge day completed" : "Practice horizon completed"
                : _runtime.Outcome != PracticeRunOutcome.Running
                    ? "Practice operating envelope ended the run"
                    : string.Empty;

        private GameSessionCommandResult RejectGameOver()
        {
            return Rejected(
                "GameSession.Run.GameOver",
                "The practice run is terminal: " + RunEndReason + ".");
        }

        private void MarkModified(string reason)
        {
            if (!_modificationReasons.Contains(reason)) _modificationReasons.Add(reason);
        }

        private string RefuellingIneligibilityReason(uint channelIndex) =>
            _nonfuelNodes.Any(node => node.ChannelId.Value == channelIndex)
                ? "This channel contains configured nonfuel cells and cannot be refuelled."
                : string.Empty;

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

        private ContractValidationResult<PracticeCandidate> TryBuildRefuellingTransaction(
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

            PracticeXenonStateV1 xenon = _xenon.Rebind(result.ResultingState);
            ContractValidationResult<PracticeLiquidZoneRrsEquilibriumResultV1> regulated =
                TryBuildRrsEquilibrium(
                    result.ResultingState,
                    _practiceRrs,
                    simulationTimeSeconds,
                    xenon: xenon);
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

            double nextScore = _syntheticScore + PracticeScoring.DescribeRefuelling(result).NetPoints;
            if (!IsFinite(nextScore))
            {
                return InvalidTransaction(
                    "GameSession.Refuelling.Score.NonFinite",
                    "score",
                    "The refuelling transaction score must remain finite.");
            }

            return ContractValidationResult<PracticeCandidate>.Valid(
                new PracticeCandidate(
                    result.ResultingState,
                    regulated.Value.Projection,
                    regulated.Value.State,
                    simulationTimeSeconds,
                    nextScore,
                    nextProjectionVersion.Value,
                    xenon, thermalEnergyJoules: _thermalEnergyJoules));
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

        private ContractValidationResult<ConfiguredCoreDesign> TryBuildConfiguredCoreDesign(uint channelIndex,
            uint position, bool hasFuel, IReadOnlyCollection<TopologyFace> reflectiveFaces)
            => GameGeometryTransactions.BuildDesign(channelIndex, position, hasFuel, reflectiveFaces,
                _nonfuelNodes, _reflectiveFaceOverrides);
        private ContractValidationResult<ConfiguredCoreTransaction> TryBuildConfiguredCoreTransaction(
            ConfiguredCoreDesign design, PracticeLiquidZoneRrsMappingV1? zoneMapping = null)
        {
            var time = ValidateCommittedTime(_runtime.SimulationTimeSeconds);
            if (!time.IsValid) return ContractValidationResult<ConfiguredCoreTransaction>.Invalid(
                time.FirstDiagnostic.Code, time.FirstDiagnostic.Path, time.FirstDiagnostic.Message);
            return GameGeometryTransactions.Build(design, zoneMapping, _equilibriumSolver, _coreState,
                _practiceRrs, _xenon, _runtime.SimulationTimeSeconds, _powerProjectionVersion);
        }

        private void ApplyConfiguredCoreTransaction(
            ConfiguredCoreTransaction transaction)
        {
            _equilibriumSolver = transaction.Solver;
            _coupledXenon = _xenon;
            _practiceRrs = transaction.Rrs;
            _nonfuelNodes = transaction.Design.NonfuelNodes;
            _reflectiveFaceOverrides = transaction.Design.ReflectiveFaceOverrides;
            _lastFullCoreSolveSimulationTime = _runtime.SimulationTimeSeconds;
            _powerProjectionVersion = transaction.PowerProjectionVersion;
        }

        private static NodeKey GetNodeKey(uint channelIndex, uint position)
        {
            if (channelIndex >= GameCorePresentationConstants.ChannelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(channelIndex));
            }

            if (position >= GameCorePresentationConstants.BundlePositionCount)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            return new NodeKey(
                new ChannelId(channelIndex),
                new BundlePosition(position));
        }

        private double GetCellFlux(
            IReadOnlyList<double> flux,
            uint channelIndex,
            uint position)
        {
            NodeKey node = GetNodeKey(channelIndex, position);
            return flux[_equilibriumSolver.SpatialModel.Topology.GetFlatIndex(node)];
        }

        private ContractValidationResult<PracticeLiquidZoneRrsEquilibriumResultV1>
            TryBuildRrsEquilibrium(
                SyntheticGameCoreStateV1 coreState,
                PracticeLiquidZoneRrsV1 previousRrs,
                double simulationTimeSeconds,
                FullCoreDiffusionSolveResultV1? warmStart = null,
                PracticeXenonStateV1? xenon = null)
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
                simulationTimeSeconds,
                warmStart,
                (xenon ?? _xenon).BuildOverlay(_nonfuelNodes));
        }

        private ContractValidationResult<PracticeCandidate> TryBuildPracticeAdvance(
            PracticeRunAdvance advance)
        {
#if RUNTIME_PROFILE
            using var profileScope = ReactorSim.Core.RuntimeProfile.Measure("practice-advance");
#endif
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

            var transaction = new PracticeAdvanceBuilder(
                _coreState,
                _equilibriumSolver.CurrentProjection,
                _practiceRrs,
                _lastFullCoreSolveSimulationTime,
                _syntheticScore,
                _powerProjectionVersion,
                _xenon,
                _coupledXenon, _thermalEnergyJoules);

            foreach (PracticeRunSegment segment in advance.StateSegments)
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

                        if (HasOperatingLoss(transaction.Rrs, transaction.SpatialCandidate, requestedAmplitude))
                        {
                            return ContractValidationResult<PracticeCandidate>.Valid(transaction.Freeze(advance, requestedAmplitude));
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
                    try
                    {
                        transaction.Xenon = transaction.Xenon.Advance(transaction.SpatialCandidate,
                            requestedAmplitude, stepSeconds);
                    }
                    catch (Exception exception) when (exception is ArgumentException ||
                        exception is InvalidOperationException || exception is OverflowException)
                    {
                        return InvalidTransaction("GameSession.Xenon.Advance.Invalid", "xenon", exception.Message);
                    }
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
                    transaction.ThermalEnergyJoules += deltaEnergy.Sum();
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
                    transaction.SyntheticScore += PracticeScoring.OperatingPoints(stepSeconds,
                        PracticeScoring.RmsRipple(transaction.SpatialCandidate.ShapeChannelPowerWatts,
                            PracticeGameSessionFactory.ReferenceChannelPower.ChannelPowerWatts, averagePowerScale));
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

                        if (HasOperatingLoss(transaction.Rrs, transaction.SpatialCandidate, requestedAmplitude))
                        {
                            return ContractValidationResult<PracticeCandidate>.Valid(transaction.Freeze(advance, requestedAmplitude));
                        }
                    }
                    if (HasOperatingLoss(transaction.Rrs, transaction.SpatialCandidate, requestedAmplitude))
                    {
                        return ContractValidationResult<PracticeCandidate>.Valid(transaction.Freeze(advance, requestedAmplitude));
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

            return ContractValidationResult<PracticeCandidate>.Valid(transaction.Freeze(advance));
        }

        private ContractValidationResult<ulong> TryFindTerminalWallMilliseconds(
            ulong maximumWallMilliseconds)
        {
            ulong lowerBound = 0;
            ulong upperBound = maximumWallMilliseconds;
            while (lowerBound < upperBound)
            {
                ulong midpoint = lowerBound + (upperBound - lowerBound) / 2UL;
                ContractValidationResult<PracticeRunAdvance> planned =
                    _runtime.TryPlanAdvanceWallMilliseconds(midpoint);
                if (!planned.IsValid)
                {
                    return ContractValidationResult<ulong>.Invalid(
                        planned.FirstDiagnostic.Code,
                        planned.FirstDiagnostic.Path,
                        planned.FirstDiagnostic.Message);
                }

                ContractValidationResult<PracticeCandidate> transaction =
                    TryBuildPracticeAdvance(planned.Value.Advance);
                if (!transaction.IsValid)
                {
                    return ContractValidationResult<ulong>.Invalid(
                        transaction.FirstDiagnostic.Code,
                        transaction.FirstDiagnostic.Path,
                        transaction.FirstDiagnostic.Message);
                }

                if (HasOperatingLoss(transaction.Value.Rrs, transaction.Value.SpatialCandidate,
                    transaction.Value.OperatingPowerAmplitude))
                {
                    upperBound = midpoint;
                }
                else
                {
                    lowerBound = midpoint + 1UL;
                }
            }

            return ContractValidationResult<ulong>.Valid(lowerBound);
        }

        private ContractValidationResult<bool> TryBuildScheduledShape(
            PracticeAdvanceBuilder transaction,
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
                    simulationTimeSeconds,
                    transaction.SpatialCandidate.SpatialSolve,
                    transaction.Xenon);
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
            transaction.CoupledXenon = transaction.Xenon;
            transaction.Rrs = regulated.Value.State;
            transaction.LastFullCoreSolveSimulationTime = simulationTimeSeconds;
            transaction.PowerProjectionVersion = nextProjectionVersion.Value;
            return ContractValidationResult<bool>.Valid(true);
        }

        private void ApplyPracticeCandidate(PracticeCandidate transaction)
        {
            _coreState = transaction.CoreState;
            _xenon = transaction.Xenon;
            _coupledXenon = transaction.CoupledXenon;
            _practiceRrs = transaction.Rrs;
            _lastFullCoreSolveSimulationTime =
                transaction.LastFullCoreSolveSimulationTime;
            _syntheticScore = transaction.SyntheticScore;
            _thermalEnergyJoules = transaction.ThermalEnergyJoules;
            _powerProjectionVersion = transaction.PowerProjectionVersion;
        }

        private GameCorePresentationSnapshot CreateCorePresentationSnapshot(SyntheticGameCoreStateV1 state,
            double powerAmplitude, EquilibriumCoreProjectionV1 projection, PracticeLiquidZoneRrsV1 rrs)
            => GamePresentationProjector.Build(state, powerAmplitude, projection, rrs,
                CreateXenonPresentationSnapshot(state.RefuellingOperationCount == 0 ? -1 : state.LastRefuelledChannel,
                    _runtime.SimulationTimeSeconds), _runtime.NormalizedPowerFraction,
                _powerProjectionVersion, _xenon, RefuellingIneligibilityReason);
        private GameXenonPresentationSnapshot CreateXenonPresentationSnapshot(int selectedChannelIndex, double simulationTimeSeconds)
            => GamePresentationProjector.Poison(selectedChannelIndex, simulationTimeSeconds, _xenon, _coupledXenon,
                _equilibriumSolver, _nonfuelNodes);

        private GameCorePresentationSnapshot CreateCorePresentationSnapshot(
            SyntheticGameCoreStateV1 state)
        {
            return CreateCorePresentationSnapshot(
                state,
                CurrentPowerFraction(),
                _equilibriumSolver.CurrentProjection,
                _practiceRrs);
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
            string inventory = _coreState.UnlimitedFreshFuel ? "; unlimited fresh fuel" :
                "; " + _coreState.FreshBundlesAvailable.ToString(CultureInfo.InvariantCulture) + " fresh bundles remain";
            return "Channel " + PracticeCoreLayout.GetChannelName(result.ChannelIndex) +
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

        private static double ComputeRrsReserveFraction(GameRrsPresentationSnapshot rrs)
        {
            return Clamp(2.0 * Math.Min(
                rrs.MinimumFillFraction,
                1.0 - rrs.MaximumFillFraction), 0.0, 1.0);
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
                    simulationTimeSeconds) || !AreSameSimulationTime(_xenon.SimulationTimeSeconds, simulationTimeSeconds))
            {
                return ContractValidationResult<bool>.Invalid(
                    "GameSession.State.TimeMismatch",
                    "simulation_time_s",
                    "The committed equilibrium projection, RRS state, and scenario runtime must share one authoritative time.");
            }

            return ContractValidationResult<bool>.Valid(true);
        }

        internal static ContractValidationResult<ulong> TryNextPowerProjectionVersion(
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

        private static ContractValidationResult<PracticeCandidate> InvalidTransaction(
            string code,
            string path,
            string message)
        {
            return ContractValidationResult<PracticeCandidate>.Invalid(
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
