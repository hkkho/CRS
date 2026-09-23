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
            double absoluteTiltFraction,
            double controlMarginFraction,
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
            IsGameOver = isGameOver;
            GameOverReason = gameOverReason;
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

        public bool IsGameOver { get; }

        public string GameOverReason { get; }

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

        private sealed class ConfiguredCoreDesign
        {
            internal ConfiguredCoreDesign(
                IEnumerable<NodeKey> nonfuelNodes,
                IEnumerable<KeyValuePair<NodeKey, IEnumerable<TopologyFace>>> reflectiveFaceOverrides)
            {
                NodeKey[] orderedNonfuelNodes = (nonfuelNodes ?? throw new ArgumentNullException(nameof(nonfuelNodes)))
                    .Distinct()
                    .OrderBy(node => node)
                    .ToArray();
                _nonfuelNodes = new ReadOnlyCollection<NodeKey>(orderedNonfuelNodes);

                var map = new Dictionary<NodeKey, IReadOnlyList<TopologyFace>>();
                if (reflectiveFaceOverrides == null)
                {
                    throw new ArgumentNullException(nameof(reflectiveFaceOverrides));
                }

                foreach (KeyValuePair<NodeKey, IEnumerable<TopologyFace>> entry in
                    reflectiveFaceOverrides.OrderBy(item => item.Key))
                {
                    TopologyFace[] faces = (entry.Value ?? throw new ArgumentNullException(nameof(reflectiveFaceOverrides)))
                        .Distinct()
                        .OrderBy(GameSession.FaceRank)
                        .ToArray();
                    if (faces.Length > 0)
                    {
                        map[entry.Key] = new ReadOnlyCollection<TopologyFace>(faces);
                    }
                }

                _reflectiveFaceOverrides =
                    new ReadOnlyDictionary<NodeKey, IReadOnlyList<TopologyFace>>(map);
            }

            private readonly IReadOnlyList<NodeKey> _nonfuelNodes;
            private readonly IReadOnlyDictionary<NodeKey, IReadOnlyList<TopologyFace>>
                _reflectiveFaceOverrides;

            internal IReadOnlyList<NodeKey> NonfuelNodes
            {
                get { return _nonfuelNodes; }
            }

            internal IReadOnlyDictionary<NodeKey, IReadOnlyList<TopologyFace>>
                ReflectiveFaceOverrides
            {
                get { return _reflectiveFaceOverrides; }
            }
        }

        private sealed class ConfiguredCoreTransaction
        {
            internal ConfiguredCoreTransaction(
                EquilibriumCoreSolverV1 solver,
                PracticeLiquidZoneRrsV1 rrs,
                ConfiguredCoreDesign design,
                ulong powerProjectionVersion)
            {
                Solver = solver;
                Rrs = rrs;
                Design = design;
                PowerProjectionVersion = powerProjectionVersion;
            }

            internal EquilibriumCoreSolverV1 Solver;

            internal PracticeLiquidZoneRrsV1 Rrs;

            internal ConfiguredCoreDesign Design;

            internal ulong PowerProjectionVersion;
        }

        private readonly Phase8ScoredScenarioRuntimeV1 _runtime;
        private readonly IReadOnlyDictionary<string, Phase8PlaybackModeV1> _playbackModes;
        private readonly uint _wallControlTickMilliseconds;
        private EquilibriumCoreSolverV1 _equilibriumSolver;
        private PracticeLiquidZoneRrsV1 _practiceRrs;
        private SyntheticGameCoreStateV1 _coreState;
        private double _lastFullCoreSolveSimulationTime;
        private double _syntheticScore;
        private double _scoreResetBaseline;
        private ulong _powerProjectionVersion;
        private IReadOnlyList<NodeKey> _nonfuelNodes;
        private IReadOnlyDictionary<NodeKey, IReadOnlyList<TopologyFace>>
            _reflectiveFaceOverrides;

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
            if (_practiceRrs.IsGameOver)
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

            ApplyConfiguredCoreTransaction(transaction.Value);
            return AcceptedMessage(
                "Core cell " + channelIndex.ToString(CultureInfo.InvariantCulture) +
                ":" + position.ToString(CultureInfo.InvariantCulture) +
                " configuration committed.");
        }

        public GameSessionCommandResult SolveConfiguredCore()
        {
            if (_practiceRrs.IsGameOver)
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

        public GameSessionCommandResult AdvanceWallMilliseconds(ulong wallMilliseconds)
        {
            if (_practiceRrs.IsGameOver)
            {
                return RejectGameOver();
            }

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

            ulong acceptedWallMilliseconds = wallMilliseconds;
            if (transaction.Value.Rrs.IsGameOver)
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

            ContractValidationResult<Phase8ScoredAdvanceResultV1> result =
                _runtime.TryAdvanceWallMilliseconds(acceptedWallMilliseconds);
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
            if (_practiceRrs.IsGameOver)
            {
                return RejectGameOver();
            }

            return Complete(_runtime.TryQueuePowerTarget(targetFraction));
        }

        public GameSessionCommandResult QueueTiltTarget(double targetFraction)
        {
            if (_practiceRrs.IsGameOver)
            {
                return RejectGameOver();
            }

            return Complete(_runtime.TryQueueTiltTarget(targetFraction));
        }

        public GameSessionCommandResult SetPlaybackMode(string playbackModeId)
        {
            if (_practiceRrs.IsGameOver)
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
            if (_practiceRrs.IsGameOver)
            {
                return RejectGameOver();
            }

            return Complete(_runtime.TryPause());
        }

        public GameSessionCommandResult Resume()
        {
            if (_practiceRrs.IsGameOver)
            {
                return RejectGameOver();
            }

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
            if (_practiceRrs.IsGameOver)
            {
                return RejectGameOver();
            }

            if (!TryParseDirection(directionId, out GameRefuellingDirectionV1 direction))
            {
                return Rejected(
                    "GameSession.Refuelling.Direction.Invalid",
                    "Choose either toward-end-a or toward-end-b.");
            }

            if (_nonfuelNodes.Any(node => node.ChannelId.Value == channelIndex))
            {
                return Rejected(
                    "GameSession.Refuelling.Channel.Nonfuel",
                    "A channel containing a configured nonfuel cell cannot be refuelled until every cell in that channel is fuel.");
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
                _practiceRrs.IsGameOver,
                _practiceRrs.GameOverReason,
                _runtime.IsPaused,
                _coreState.FreshBundlesAvailable,
                _coreState.RefuellingOperationCount,
                _coreState.LastRefuelledChannel,
                DirectionId(_coreState.LastDirection),
                _coreState.LastShiftCount,
                core);
        }

        private GameSessionCommandResult RejectGameOver()
        {
            return Rejected(
                "GameSession.Run.GameOver",
                "The practice run is terminal: " + _practiceRrs.GameOverReason + ".");
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

        private ContractValidationResult<ConfiguredCoreDesign>
            TryBuildConfiguredCoreDesign(
                uint channelIndex,
                uint position,
                bool hasFuel,
                IReadOnlyCollection<TopologyFace> reflectiveFaces)
        {
            if (channelIndex >= GameCorePresentationConstants.ChannelCount)
            {
                return InvalidConfiguredDesign(
                    "GameSession.ConfigureCell.Channel.OutOfRange",
                    "channelIndex",
                    "The configured channel index must identify one of the 380 full-core channels.");
            }

            if (position >= GameCorePresentationConstants.BundlePositionCount)
            {
                return InvalidConfiguredDesign(
                    "GameSession.ConfigureCell.Position.OutOfRange",
                    "position",
                    "The configured position must identify one of the 12 bundle positions.");
            }

            if (reflectiveFaces == null)
            {
                return InvalidConfiguredDesign(
                    "GameSession.ConfigureCell.ReflectiveFaces.Missing",
                    "reflectiveFaces",
                    "A cell configuration requires an explicit reflective face collection.");
            }

            var requestedFaces = new HashSet<TopologyFace>();
            foreach (TopologyFace face in reflectiveFaces)
            {
                if (!Enum.IsDefined(typeof(TopologyFace), face))
                {
                    return InvalidConfiguredDesign(
                        "GameSession.ConfigureCell.ReflectiveFaces.Invalid",
                        "reflectiveFaces",
                        "Every reflective face must be a known topology face.");
                }

                if (!requestedFaces.Add(face))
                {
                    return InvalidConfiguredDesign(
                        "GameSession.ConfigureCell.ReflectiveFaces.Duplicate",
                        "reflectiveFaces",
                        "A reflective face may be listed only once.");
                }
            }

            NodeKey node = new NodeKey(
                new ChannelId(channelIndex),
                new BundlePosition(position));
            var nonfuelNodes = new HashSet<NodeKey>(_nonfuelNodes);
            if (hasFuel)
            {
                nonfuelNodes.Remove(node);
            }
            else
            {
                nonfuelNodes.Add(node);
            }

            var faceMap = new Dictionary<NodeKey, List<TopologyFace>>();
            foreach (KeyValuePair<NodeKey, IReadOnlyList<TopologyFace>> entry in
                _reflectiveFaceOverrides)
            {
                faceMap[entry.Key] = entry.Value.ToList();
            }

            if (faceMap.TryGetValue(node, out List<TopologyFace>? currentFaces))
            {
                foreach (TopologyFace currentFace in currentFaces.ToArray())
                {
                    RemoveFace(faceMap, node, currentFace);
                    if (TryGetInteriorNeighbor(
                            node,
                            currentFace,
                            out NodeKey neighbor))
                    {
                        RemoveFace(faceMap, neighbor, InverseFace(currentFace));
                    }
                }
            }

            foreach (TopologyFace face in requestedFaces)
            {
                AddFace(faceMap, node, face);
                if (TryGetInteriorNeighbor(node, face, out NodeKey neighbor))
                {
                    AddFace(faceMap, neighbor, InverseFace(face));
                }
            }

            return ContractValidationResult<ConfiguredCoreDesign>.Valid(
                new ConfiguredCoreDesign(
                    nonfuelNodes,
                    faceMap.Select(entry =>
                        new KeyValuePair<NodeKey, IEnumerable<TopologyFace>>(
                            entry.Key,
                            entry.Value))));
        }

        private ContractValidationResult<ConfiguredCoreTransaction>
            TryBuildConfiguredCoreTransaction(ConfiguredCoreDesign design)
        {
            if (design == null)
            {
                return InvalidConfiguredTransaction(
                    "GameSession.ConfigureCell.Design.Missing",
                    "design",
                    "A configured full-core solve requires an explicit immutable design.");
            }

            ContractValidationResult<bool> timeBinding =
                ValidateCommittedTime(_runtime.SimulationTimeSeconds);
            if (!timeBinding.IsValid)
            {
                return InvalidConfiguredTransaction(
                    timeBinding.FirstDiagnostic.Code,
                    timeBinding.FirstDiagnostic.Path,
                    timeBinding.FirstDiagnostic.Message);
            }

            var overrides = design.ReflectiveFaceOverrides
                .OrderBy(entry => entry.Key)
                .SelectMany(entry => entry.Value.Select(face =>
                    new ReflectiveFaceOverrideV1(entry.Key, face)))
                .ToArray();
            ContractValidationResult<CoreTopology> topology =
                Candu6CoreTopologyFactoryV1.TryCreate(overrides);
            if (!topology.IsValid)
            {
                return InvalidConfiguredTransaction(
                    topology.FirstDiagnostic.Code,
                    topology.FirstDiagnostic.Path,
                    topology.FirstDiagnostic.Message);
            }

            ContractValidationResult<SpatialStencil> stencil =
                SpatialStencil.TryCreate(topology.Value);
            if (!stencil.IsValid)
            {
                return InvalidConfiguredTransaction(
                    stencil.FirstDiagnostic.Code,
                    stencil.FirstDiagnostic.Path,
                    stencil.FirstDiagnostic.Message);
            }

            ContractValidationResult<FullCoreDiffusionModelV1> model =
                FullCoreDiffusionModelV1.TryCreate(
                    _equilibriumSolver.DataPack,
                    topology.Value,
                    stencil.Value,
                    design.NonfuelNodes);
            if (!model.IsValid)
            {
                return InvalidConfiguredTransaction(
                    model.FirstDiagnostic.Code,
                    model.FirstDiagnostic.Path,
                    model.FirstDiagnostic.Message);
            }

            ContractValidationResult<EquilibriumCoreSolverV1> solver =
                EquilibriumCoreSolverV1.TryCreate(
                    model.Value,
                    _coreState.EnumerateBundles(),
                    _equilibriumSolver.TargetPowerWatts);
            if (!solver.IsValid)
            {
                return InvalidConfiguredTransaction(
                    solver.FirstDiagnostic.Code,
                    solver.FirstDiagnostic.Path,
                    solver.FirstDiagnostic.Message);
            }

            ContractValidationResult<PracticeLiquidZoneRrsEquilibriumResultV1> regulated =
                PracticeLiquidZoneRrsV1.TryRunEquilibrium(
                    solver.Value,
                    _coreState.EnumerateBundles(),
                    _practiceRrs,
                    _runtime.SimulationTimeSeconds);
            if (!regulated.IsValid)
            {
                return InvalidConfiguredTransaction(
                    regulated.FirstDiagnostic.Code,
                    regulated.FirstDiagnostic.Path,
                    regulated.FirstDiagnostic.Message);
            }

            ContractValidationResult<bool> committed =
                solver.Value.TryCommitCandidate(regulated.Value.Projection);
            if (!committed.IsValid)
            {
                return InvalidConfiguredTransaction(
                    committed.FirstDiagnostic.Code,
                    committed.FirstDiagnostic.Path,
                    committed.FirstDiagnostic.Message);
            }

            ContractValidationResult<ulong> nextProjectionVersion =
                TryNextPowerProjectionVersion(_powerProjectionVersion);
            if (!nextProjectionVersion.IsValid)
            {
                return InvalidConfiguredTransaction(
                    nextProjectionVersion.FirstDiagnostic.Code,
                    nextProjectionVersion.FirstDiagnostic.Path,
                    nextProjectionVersion.FirstDiagnostic.Message);
            }

            return ContractValidationResult<ConfiguredCoreTransaction>.Valid(
                new ConfiguredCoreTransaction(
                    solver.Value,
                    regulated.Value.State,
                    design,
                    nextProjectionVersion.Value));
        }

        private void ApplyConfiguredCoreTransaction(
            ConfiguredCoreTransaction transaction)
        {
            _equilibriumSolver = transaction.Solver;
            _practiceRrs = transaction.Rrs;
            _nonfuelNodes = transaction.Design.NonfuelNodes;
            _reflectiveFaceOverrides = transaction.Design.ReflectiveFaceOverrides;
            _lastFullCoreSolveSimulationTime = _runtime.SimulationTimeSeconds;
            _powerProjectionVersion = transaction.PowerProjectionVersion;
        }

        private static ContractValidationResult<ConfiguredCoreDesign>
            InvalidConfiguredDesign(string code, string path, string message)
        {
            return ContractValidationResult<ConfiguredCoreDesign>.Invalid(
                code,
                path,
                message);
        }

        private static ContractValidationResult<ConfiguredCoreTransaction>
            InvalidConfiguredTransaction(string code, string path, string message)
        {
            return ContractValidationResult<ConfiguredCoreTransaction>.Invalid(
                code,
                path,
                message);
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

        private static void AddFace(
            Dictionary<NodeKey, List<TopologyFace>> faceMap,
            NodeKey node,
            TopologyFace face)
        {
            if (!faceMap.TryGetValue(node, out List<TopologyFace>? faces))
            {
                faces = new List<TopologyFace>();
                faceMap[node] = faces;
            }

            if (!faces.Contains(face))
            {
                faces.Add(face);
                faces.Sort((left, right) => FaceRank(left).CompareTo(FaceRank(right)));
            }
        }

        private static void RemoveFace(
            Dictionary<NodeKey, List<TopologyFace>> faceMap,
            NodeKey node,
            TopologyFace face)
        {
            if (!faceMap.TryGetValue(node, out List<TopologyFace>? faces))
            {
                return;
            }

            faces.Remove(face);
            if (faces.Count == 0)
            {
                faceMap.Remove(node);
            }
        }

        private static bool TryGetInteriorNeighbor(
            NodeKey node,
            TopologyFace face,
            out NodeKey neighbor)
        {
            neighbor = default(NodeKey);
            int column;
            int displayRow;
            switch (face)
            {
                case TopologyFace.North:
                    column = Candu6CoreTopologyFactoryV1.GetPosition(node.ChannelId.Value).Column;
                    displayRow = Candu6CoreTopologyFactoryV1.GetPosition(node.ChannelId.Value).DisplayRow - 1;
                    break;
                case TopologyFace.East:
                    column = Candu6CoreTopologyFactoryV1.GetPosition(node.ChannelId.Value).Column + 1;
                    displayRow = Candu6CoreTopologyFactoryV1.GetPosition(node.ChannelId.Value).DisplayRow;
                    break;
                case TopologyFace.South:
                    column = Candu6CoreTopologyFactoryV1.GetPosition(node.ChannelId.Value).Column;
                    displayRow = Candu6CoreTopologyFactoryV1.GetPosition(node.ChannelId.Value).DisplayRow + 1;
                    break;
                case TopologyFace.West:
                    column = Candu6CoreTopologyFactoryV1.GetPosition(node.ChannelId.Value).Column - 1;
                    displayRow = Candu6CoreTopologyFactoryV1.GetPosition(node.ChannelId.Value).DisplayRow;
                    break;
                case TopologyFace.EndA:
                    if (node.Position.Value == 0)
                    {
                        return false;
                    }

                    neighbor = new NodeKey(
                        node.ChannelId,
                        new BundlePosition(node.Position.Value - 1));
                    return true;
                case TopologyFace.EndB:
                    if (node.Position.Value + 1 >=
                        GameCorePresentationConstants.BundlePositionCount)
                    {
                        return false;
                    }

                    neighbor = new NodeKey(
                        node.ChannelId,
                        new BundlePosition(node.Position.Value + 1));
                    return true;
                default:
                    return false;
            }

            if (!Candu6CoreTopologyFactoryV1.TryGetChannelIndex(
                    column,
                    displayRow,
                    out uint neighborChannel))
            {
                return false;
            }

            neighbor = new NodeKey(
                new ChannelId(neighborChannel),
                node.Position);
            return true;
        }

        private static TopologyFace InverseFace(TopologyFace face)
        {
            switch (face)
            {
                case TopologyFace.North:
                    return TopologyFace.South;
                case TopologyFace.East:
                    return TopologyFace.West;
                case TopologyFace.South:
                    return TopologyFace.North;
                case TopologyFace.West:
                    return TopologyFace.East;
                case TopologyFace.EndA:
                    return TopologyFace.EndB;
                case TopologyFace.EndB:
                    return TopologyFace.EndA;
                default:
                    throw new ArgumentOutOfRangeException(nameof(face));
            }
        }

        private static int FaceRank(TopologyFace face)
        {
            return (byte)face;
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

                        if (transaction.Rrs.IsGameOver)
                        {
                            return ContractValidationResult<PracticeTransaction>.Valid(
                                transaction);
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

                        if (transaction.Rrs.IsGameOver)
                        {
                            return ContractValidationResult<PracticeTransaction>.Valid(
                                transaction);
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

        private ContractValidationResult<ulong> TryFindTerminalWallMilliseconds(
            ulong maximumWallMilliseconds)
        {
            ulong lowerBound = 0;
            ulong upperBound = maximumWallMilliseconds;
            while (lowerBound < upperBound)
            {
                ulong midpoint = lowerBound + (upperBound - lowerBound) / 2UL;
                ContractValidationResult<Phase8ScoredAdvanceResultV1> planned =
                    _runtime.TryPlanAdvanceWallMilliseconds(midpoint);
                if (!planned.IsValid)
                {
                    return ContractValidationResult<ulong>.Invalid(
                        planned.FirstDiagnostic.Code,
                        planned.FirstDiagnostic.Path,
                        planned.FirstDiagnostic.Message);
                }

                ContractValidationResult<PracticeTransaction> transaction =
                    TryBuildPracticeAdvance(planned.Value.Advance);
                if (!transaction.IsValid)
                {
                    return ContractValidationResult<ulong>.Invalid(
                        transaction.FirstDiagnostic.Code,
                        transaction.FirstDiagnostic.Path,
                        transaction.FirstDiagnostic.Message);
                }

                if (transaction.Value.Rrs.IsGameOver)
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
