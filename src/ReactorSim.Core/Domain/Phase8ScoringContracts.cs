using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace ReactorSim.Core
{
    /// <summary>
    /// Synthetic gameplay scoring authority consumed after the approved P8-T02
    /// scenario runtime. These weights are not physical quantities.
    /// </summary>
    public sealed class Phase8ScoringParametersV1
    {
        private Phase8ScoringParametersV1(
            double scoreMinimum,
            double scoreMaximum,
            double survivalPoints,
            double energyQualityPoints,
            double stabilityQualityPoints,
            double fuellingEfficiencyPoints,
            double controlActionPenaltyPoints,
            double recordLossPenaltyPoints,
            double nominalPowerFraction,
            uint roundingDecimalPlaces,
            uint maximumSummaryEventsPerTurn)
        {
            ScoreMinimum = scoreMinimum;
            ScoreMaximum = scoreMaximum;
            SurvivalPoints = survivalPoints;
            EnergyQualityPoints = energyQualityPoints;
            StabilityQualityPoints = stabilityQualityPoints;
            FuellingEfficiencyPoints = fuellingEfficiencyPoints;
            ControlActionPenaltyPoints = controlActionPenaltyPoints;
            RecordLossPenaltyPoints = recordLossPenaltyPoints;
            NominalPowerFraction = nominalPowerFraction;
            RoundingDecimalPlaces = roundingDecimalPlaces;
            MaximumSummaryEventsPerTurn = maximumSummaryEventsPerTurn;
        }

        public double ScoreMinimum { get; }

        public double ScoreMaximum { get; }

        public double SurvivalPoints { get; }

        public double EnergyQualityPoints { get; }

        public double StabilityQualityPoints { get; }

        public double FuellingEfficiencyPoints { get; }

        public double ControlActionPenaltyPoints { get; }

        public double RecordLossPenaltyPoints { get; }

        public double NominalPowerFraction { get; }

        public uint RoundingDecimalPlaces { get; }

        public uint MaximumSummaryEventsPerTurn { get; }

        public static ContractValidationResult<Phase8ScoringParametersV1> TryCreate(
            double scoreMinimum,
            double scoreMaximum,
            double survivalPoints,
            double energyQualityPoints,
            double stabilityQualityPoints,
            double fuellingEfficiencyPoints,
            double controlActionPenaltyPoints,
            double recordLossPenaltyPoints,
            double nominalPowerFraction,
            uint roundingDecimalPlaces,
            uint maximumSummaryEventsPerTurn)
        {
            double[] values =
            {
                scoreMinimum,
                scoreMaximum,
                survivalPoints,
                energyQualityPoints,
                stabilityQualityPoints,
                fuellingEfficiencyPoints,
                controlActionPenaltyPoints,
                recordLossPenaltyPoints,
                nominalPowerFraction
            };
            if (values.Any(value => !ContractValidation.IsFinite(value)))
            {
                return ContractValidationResult<Phase8ScoringParametersV1>.Invalid(
                    "Phase8ScoringParameters.Finite.Invalid",
                    "score_model",
                    "Every score parameter must be finite.");
            }

            if (scoreMinimum < 0 || scoreMaximum <= scoreMinimum ||
                survivalPoints < 0 || energyQualityPoints < 0 ||
                stabilityQualityPoints < 0 || fuellingEfficiencyPoints < 0 ||
                controlActionPenaltyPoints < 0 || recordLossPenaltyPoints < 0 ||
                nominalPowerFraction <= 0 || roundingDecimalPlaces > 9 ||
                maximumSummaryEventsPerTurn == 0)
            {
                return ContractValidationResult<Phase8ScoringParametersV1>.Invalid(
                    "Phase8ScoringParameters.Domain.Invalid",
                    "score_model",
                    "Score bounds, weights, nominal power, rounding, and summary capacity must be valid.");
            }

            return ContractValidationResult<Phase8ScoringParametersV1>.Valid(
                new Phase8ScoringParametersV1(
                    scoreMinimum,
                    scoreMaximum,
                    survivalPoints,
                    energyQualityPoints,
                    stabilityQualityPoints,
                    fuellingEfficiencyPoints,
                    controlActionPenaltyPoints,
                    recordLossPenaltyPoints,
                    nominalPowerFraction,
                    roundingDecimalPlaces,
                    maximumSummaryEventsPerTurn));
        }
    }

    public sealed class Phase8ScoreSnapshotV1
    {
        internal Phase8ScoreSnapshotV1(
            double totalPoints,
            double survivalPoints,
            double energyQuality,
            double energyPoints,
            double stabilityQuality,
            double stabilityPoints,
            double fuellingEfficiency,
            double fuellingEfficiencyPoints,
            double controlPenaltyPoints,
            double lossPenaltyPoints,
            uint committedActionCount,
            uint recordedLossCount)
        {
            TotalPoints = totalPoints;
            SurvivalPoints = survivalPoints;
            EnergyQuality = energyQuality;
            EnergyPoints = energyPoints;
            StabilityQuality = stabilityQuality;
            StabilityPoints = stabilityPoints;
            FuellingEfficiency = fuellingEfficiency;
            FuellingEfficiencyPoints = fuellingEfficiencyPoints;
            ControlPenaltyPoints = controlPenaltyPoints;
            LossPenaltyPoints = lossPenaltyPoints;
            CommittedActionCount = committedActionCount;
            RecordedLossCount = recordedLossCount;
        }

        public double TotalPoints { get; }

        public double SurvivalPoints { get; }

        public double EnergyQuality { get; }

        public double EnergyPoints { get; }

        public double StabilityQuality { get; }

        public double StabilityPoints { get; }

        public double FuellingEfficiency { get; }

        public double FuellingEfficiencyPoints { get; }

        public double ControlPenaltyPoints { get; }

        public double LossPenaltyPoints { get; }

        public uint CommittedActionCount { get; }

        public uint RecordedLossCount { get; }
    }

    public sealed class Phase8TurnSummaryV1
    {
        internal Phase8TurnSummaryV1(
            ulong turnId,
            ulong wallMillisecondsRequested,
            double simulationTimeStartSeconds,
            double simulationTimeEndSeconds,
            uint committedActionCount,
            uint scriptedEventCount,
            uint lossCount,
            double normalizedPowerBeforeFraction,
            double normalizedPowerAfterFraction,
            double absoluteTiltBeforeFraction,
            double absoluteTiltAfterFraction,
            Phase8ScenarioOutcomeV1 outcome,
            string cause,
            string effect,
            double scoreTotal)
        {
            TurnId = turnId;
            WallMillisecondsRequested = wallMillisecondsRequested;
            SimulationTimeStartSeconds = simulationTimeStartSeconds;
            SimulationTimeEndSeconds = simulationTimeEndSeconds;
            CommittedActionCount = committedActionCount;
            ScriptedEventCount = scriptedEventCount;
            LossCount = lossCount;
            NormalizedPowerBeforeFraction = normalizedPowerBeforeFraction;
            NormalizedPowerAfterFraction = normalizedPowerAfterFraction;
            AbsoluteTiltBeforeFraction = absoluteTiltBeforeFraction;
            AbsoluteTiltAfterFraction = absoluteTiltAfterFraction;
            Outcome = outcome;
            Cause = cause;
            Effect = effect;
            ScoreTotal = scoreTotal;
        }

        public ulong TurnId { get; }

        public ulong WallMillisecondsRequested { get; }

        public double SimulationTimeStartSeconds { get; }

        public double SimulationTimeEndSeconds { get; }

        public uint CommittedActionCount { get; }

        public uint ScriptedEventCount { get; }

        public uint LossCount { get; }

        public double NormalizedPowerBeforeFraction { get; }

        public double NormalizedPowerAfterFraction { get; }

        public double AbsoluteTiltBeforeFraction { get; }

        public double AbsoluteTiltAfterFraction { get; }

        public Phase8ScenarioOutcomeV1 Outcome { get; }

        public string Cause { get; }

        public string Effect { get; }

        public double ScoreTotal { get; }
    }

    public sealed class Phase8ScoredAdvanceResultV1
    {
        internal Phase8ScoredAdvanceResultV1(
            Phase8ScenarioAdvanceResultV1 advance,
            Phase8TurnSummaryV1 turnSummary,
            Phase8ScoreSnapshotV1 score)
        {
            Advance = advance;
            TurnSummary = turnSummary;
            Score = score;
        }

        public Phase8ScenarioAdvanceResultV1 Advance { get; }

        public Phase8TurnSummaryV1 TurnSummary { get; }

        public Phase8ScoreSnapshotV1 Score { get; }
    }

    /// <summary>
    /// Deterministic synthetic scoring and cause/effect summary wrapper over
    /// the approved P8-T02 scenario runtime. It consumes only explicit runtime
    /// observations and never changes simulation state transitions.
    /// </summary>
    public sealed class Phase8ScoredScenarioRuntimeV1
    {
        private sealed class ScoredRuntimeSnapshot
        {
            public ScoredRuntimeSnapshot(
                Phase8ScenarioRuntimeSnapshotV1 runtime,
                double integratedEnergyProxy,
                double integratedStabilityProxy,
                uint committedActionCount,
                uint recordedLossCount,
                ulong nextTurnId,
                int turnSummaryCount)
            {
                Runtime = runtime;
                IntegratedEnergyProxy = integratedEnergyProxy;
                IntegratedStabilityProxy = integratedStabilityProxy;
                CommittedActionCount = committedActionCount;
                RecordedLossCount = recordedLossCount;
                NextTurnId = nextTurnId;
                TurnSummaryCount = turnSummaryCount;
            }

            public Phase8ScenarioRuntimeSnapshotV1 Runtime { get; }

            public double IntegratedEnergyProxy { get; }

            public double IntegratedStabilityProxy { get; }

            public uint CommittedActionCount { get; }

            public uint RecordedLossCount { get; }

            public ulong NextTurnId { get; }

            public int TurnSummaryCount { get; }
        }

        private readonly Phase8ScenarioRuntimeV1 _runtime;
        private readonly Phase8ScoringParametersV1 _parameters;
        private readonly double _initialRefuelRequests;
        private double _integratedEnergyProxy;
        private double _integratedStabilityProxy;
        private uint _committedActionCount;
        private uint _recordedLossCount;
        private ulong _nextTurnId = 1;

        private Phase8ScoredScenarioRuntimeV1(
            Phase8ScenarioRuntimeV1 runtime,
            Phase8ScoringParametersV1 parameters)
        {
            _runtime = runtime;
            _parameters = parameters;
            _initialRefuelRequests = runtime.RefuelRequestsRemaining;
            _recordedLossCount = (uint)runtime.LossRecords.Count;
        }

        public Phase8ScenarioRuntimeV1 Runtime
        {
            get { return _runtime; }
        }

        public Phase8ScoringParametersV1 Parameters
        {
            get { return _parameters; }
        }

        public Phase8ScoreSnapshotV1 Score
        {
            get { return CreateScoreSnapshot(); }
        }

        public IReadOnlyList<Phase8TurnSummaryV1> TurnSummaries
        {
            get { return new ReadOnlyCollection<Phase8TurnSummaryV1>(_turnSummaries.ToArray()); }
        }

        public string ScenarioId
        {
            get { return _runtime.ScenarioId; }
        }

        public string DifficultyId
        {
            get { return _runtime.DifficultyId; }
        }

        public ulong Seed
        {
            get { return _runtime.Seed; }
        }

        public ulong ReplaySeed
        {
            get { return _runtime.ReplaySeed; }
        }

        public string PlaybackModeId
        {
            get { return _runtime.PlaybackModeId; }
        }

        public double AccelerationFactor
        {
            get { return _runtime.AccelerationFactor; }
        }

        public double ScenarioHorizonSeconds
        {
            get { return _runtime.ScenarioHorizonSeconds; }
        }

        public double SimulationTimeSeconds
        {
            get { return _runtime.SimulationTimeSeconds; }
        }

        public ulong SimulationStepIndex
        {
            get { return _runtime.SimulationStepIndex; }
        }

        public double WallElapsedSeconds
        {
            get { return _runtime.WallElapsedSeconds; }
        }

        public double NormalizedPowerFraction
        {
            get { return _runtime.NormalizedPowerFraction; }
        }

        public double AbsoluteTiltFraction
        {
            get { return _runtime.AbsoluteTiltFraction; }
        }

        public double ControlMarginFraction
        {
            get { return _runtime.ControlMarginFraction; }
        }

        public double DeviceAvailableFraction
        {
            get { return _runtime.DeviceAvailableFraction; }
        }

        public uint RefuelRequestsRemaining
        {
            get { return _runtime.RefuelRequestsRemaining; }
        }

        public uint PendingActionCount
        {
            get { return _runtime.PendingActionCount; }
        }

        public uint ProcessedScriptedEventCount
        {
            get { return _runtime.ProcessedScriptedEventCount; }
        }

        public bool IsPaused
        {
            get { return _runtime.IsPaused; }
        }

        public Phase8ScenarioOutcomeV1 Outcome
        {
            get { return _runtime.Outcome; }
        }

        private readonly List<Phase8TurnSummaryV1> _turnSummaries =
            new List<Phase8TurnSummaryV1>();

        public static ContractValidationResult<Phase8ScoredScenarioRuntimeV1> TryCreate(
            Phase8ScenarioRuntimeV1 runtime,
            Phase8ScoringParametersV1 parameters)
        {
            if (runtime == null || parameters == null)
            {
                return ContractValidationResult<Phase8ScoredScenarioRuntimeV1>.Invalid(
                    "Phase8ScoredRuntime.Input.Missing",
                    "runtime",
                    "A scored runtime requires the approved P8-T02 runtime and P8-T03 parameters.");
            }

            return ContractValidationResult<Phase8ScoredScenarioRuntimeV1>.Valid(
                new Phase8ScoredScenarioRuntimeV1(runtime, parameters));
        }

        public ContractValidationResult<Phase8ActionQueueResultV1> TryQueuePowerTarget(
            double targetNormalizedPowerFraction)
        {
            return _runtime.TryQueuePowerTarget(targetNormalizedPowerFraction);
        }

        public ContractValidationResult<Phase8ActionQueueResultV1> TryQueueTiltTarget(
            double targetAbsoluteTiltFraction)
        {
            return _runtime.TryQueueTiltTarget(targetAbsoluteTiltFraction);
        }

        public ContractValidationResult<bool> TrySetPlaybackMode(Phase8PlaybackModeV1 playbackMode)
        {
            return _runtime.TrySetPlaybackMode(playbackMode);
        }

        public ContractValidationResult<bool> TryPause()
        {
            return _runtime.TryPause();
        }

        public ContractValidationResult<bool> TryResume()
        {
            return _runtime.TryResume();
        }

        public ContractValidationResult<Phase8ScoredAdvanceResultV1> TryAdvanceWallMilliseconds(
            ulong wallMilliseconds)
        {
            ScoredRuntimeSnapshot snapshot = CaptureSnapshot();
            double simulationTimeStart = _runtime.SimulationTimeSeconds;
            double powerStart = _runtime.NormalizedPowerFraction;
            double tiltStart = _runtime.AbsoluteTiltFraction;

            var actionTransitions = new List<Phase8ActionTransitionV1>();
            var eventRecords = new List<Phase8EventRecordV1>();
            var lossRecords = new List<Phase8LossRecordV1>();
            var stateSegments = new List<Phase8ScenarioAdvanceSegmentV1>();
            uint controlTicksProcessed = 0;
            if (wallMilliseconds == 0 || _runtime.IsPaused ||
                _runtime.Outcome != Phase8ScenarioOutcomeV1.Running)
            {
                ContractValidationResult<Phase8ScenarioAdvanceResultV1> singleAdvance =
                    _runtime.TryAdvanceWallMilliseconds(wallMilliseconds);
                if (!singleAdvance.IsValid)
                {
                    return InvalidAdvance(singleAdvance, snapshot);
                }

                AppendAdvance(
                    singleAdvance.Value,
                    actionTransitions,
                    eventRecords,
                    lossRecords,
                    stateSegments,
                    ref controlTicksProcessed);
                ContractValidationResult<bool> accumulation = AccumulateScoreWindow(singleAdvance.Value);
                if (!accumulation.IsValid)
                {
                    return RollbackInvalid(
                        snapshot,
                        accumulation.FirstDiagnostic.Code,
                        accumulation.FirstDiagnostic.Path,
                        accumulation.FirstDiagnostic.Message);
                }
            }
            else
            {
                ulong remainingMilliseconds = wallMilliseconds;
                while (remainingMilliseconds > 0)
                {
                    ulong chunkMilliseconds = Math.Min(
                        remainingMilliseconds,
                        _runtime.WallControlTickMilliseconds);
                    ContractValidationResult<Phase8ScenarioAdvanceResultV1> chunkAdvance =
                        _runtime.TryAdvanceWallMilliseconds(chunkMilliseconds);
                    if (!chunkAdvance.IsValid)
                    {
                        return InvalidAdvance(chunkAdvance, snapshot);
                    }

                    AppendAdvance(
                        chunkAdvance.Value,
                        actionTransitions,
                        eventRecords,
                        lossRecords,
                        stateSegments,
                        ref controlTicksProcessed);
                    ContractValidationResult<bool> accumulation = AccumulateScoreWindow(chunkAdvance.Value);
                    if (!accumulation.IsValid)
                    {
                        return RollbackInvalid(
                            snapshot,
                            accumulation.FirstDiagnostic.Code,
                            accumulation.FirstDiagnostic.Path,
                            accumulation.FirstDiagnostic.Message);
                    }
                    remainingMilliseconds -= chunkMilliseconds;
                    if (_runtime.Outcome != Phase8ScenarioOutcomeV1.Running)
                    {
                        break;
                    }
                }
            }

            Phase8ScenarioAdvanceResultV1 aggregateAdvance = new Phase8ScenarioAdvanceResultV1(
                wallMilliseconds,
                controlTicksProcessed,
                _runtime.SimulationTimeSeconds,
                _runtime.WallElapsedSeconds,
                _runtime.Outcome,
                _runtime.IsPaused,
                actionTransitions,
                eventRecords,
                lossRecords,
                stateSegments);
            ulong summaryEventCount = (ulong)actionTransitions.Count +
                                      (ulong)eventRecords.Count +
                                      (ulong)lossRecords.Count;
            if (summaryEventCount > _parameters.MaximumSummaryEventsPerTurn)
            {
                return RollbackInvalid(
                    snapshot,
                    "Phase8ScoredRuntime.SummaryCapacity.Exceeded",
                    "summary.events",
                    "The approved maximum summary event capacity was exceeded.");
            }

            Phase8ScoreSnapshotV1 score = CreateScoreSnapshot();
            Phase8TurnSummaryV1 summary = CreateTurnSummary(
                wallMilliseconds,
                simulationTimeStart,
                _runtime.SimulationTimeSeconds,
                powerStart,
                _runtime.NormalizedPowerFraction,
                tiltStart,
                _runtime.AbsoluteTiltFraction,
                aggregateAdvance,
                score);
            if (_nextTurnId == ulong.MaxValue)
            {
                return RollbackInvalid(
                    snapshot,
                    "Phase8ScoredRuntime.TurnId.Overflow",
                    "turn_id",
                    "The turn summary identity sequence cannot advance past UInt64.MaxValue.");
            }

            _nextTurnId++;
            _turnSummaries.Add(summary);

            return ContractValidationResult<Phase8ScoredAdvanceResultV1>.Valid(
                new Phase8ScoredAdvanceResultV1(aggregateAdvance, summary, score));
        }

        private ScoredRuntimeSnapshot CaptureSnapshot()
        {
            return new ScoredRuntimeSnapshot(
                _runtime.CaptureSnapshot(),
                _integratedEnergyProxy,
                _integratedStabilityProxy,
                _committedActionCount,
                _recordedLossCount,
                _nextTurnId,
                _turnSummaries.Count);
        }

        private ContractValidationResult<Phase8ScoredAdvanceResultV1> InvalidAdvance(
            ContractValidationResult<Phase8ScenarioAdvanceResultV1> advance,
            ScoredRuntimeSnapshot snapshot)
        {
            return RollbackInvalid(
                snapshot,
                advance.FirstDiagnostic.Code,
                advance.FirstDiagnostic.Path,
                advance.FirstDiagnostic.Message);
        }

        private ContractValidationResult<Phase8ScoredAdvanceResultV1> RollbackInvalid(
            ScoredRuntimeSnapshot snapshot,
            string code,
            string path,
            string message)
        {
            _runtime.RestoreSnapshot(snapshot.Runtime);
            _integratedEnergyProxy = snapshot.IntegratedEnergyProxy;
            _integratedStabilityProxy = snapshot.IntegratedStabilityProxy;
            _committedActionCount = snapshot.CommittedActionCount;
            _recordedLossCount = snapshot.RecordedLossCount;
            _nextTurnId = snapshot.NextTurnId;
            if (_turnSummaries.Count > snapshot.TurnSummaryCount)
            {
                _turnSummaries.RemoveRange(
                    snapshot.TurnSummaryCount,
                    _turnSummaries.Count - snapshot.TurnSummaryCount);
            }

            return ContractValidationResult<Phase8ScoredAdvanceResultV1>.Invalid(
                code,
                path,
                message);
        }

        private static void AppendAdvance(
            Phase8ScenarioAdvanceResultV1 advance,
            List<Phase8ActionTransitionV1> actionTransitions,
            List<Phase8EventRecordV1> eventRecords,
            List<Phase8LossRecordV1> lossRecords,
            List<Phase8ScenarioAdvanceSegmentV1> stateSegments,
            ref uint controlTicksProcessed)
        {
            controlTicksProcessed = checked(controlTicksProcessed + advance.ControlTicksProcessed);
            foreach (Phase8ActionTransitionV1 action in advance.ActionTransitions)
            {
                actionTransitions.Add(action);
            }

            foreach (Phase8EventRecordV1 record in advance.EventRecords)
            {
                eventRecords.Add(record);
            }

            foreach (Phase8LossRecordV1 loss in advance.LossRecords)
            {
                lossRecords.Add(loss);
            }

            foreach (Phase8ScenarioAdvanceSegmentV1 segment in advance.StateSegments)
            {
                stateSegments.Add(segment);
            }
        }

        private ContractValidationResult<bool> AccumulateScoreWindow(
            Phase8ScenarioAdvanceResultV1 advance)
        {
            foreach (Phase8ScenarioAdvanceSegmentV1 segment in advance.StateSegments)
            {
                double elapsed = segment.SimulationTimeEndSeconds - segment.SimulationTimeStartSeconds;
                if (!ContractValidation.IsFinite(elapsed) || elapsed < 0 ||
                    !ContractValidation.IsFinite(segment.NormalizedPowerFraction) ||
                    !ContractValidation.IsFinite(segment.AbsoluteTiltFraction) ||
                    !ContractValidation.IsFinite(segment.ControlMarginFraction))
                {
                    return ContractValidationResult<bool>.Invalid(
                        "Phase8ScoredRuntime.StateSegment.Invalid",
                        "state_segments",
                        "The scored runtime observed a nonfinite or nonmonotone state segment.");
                }

                _integratedEnergyProxy += segment.NormalizedPowerFraction * elapsed;
                _integratedStabilityProxy += StabilityQuality(
                    segment.NormalizedPowerFraction,
                    segment.AbsoluteTiltFraction,
                    segment.ControlMarginFraction) * elapsed;
            }

            _committedActionCount += checked((uint)advance.ActionTransitions.Count);
            _recordedLossCount += checked((uint)advance.LossRecords.Count);
            return ContractValidationResult<bool>.Valid(true);
        }

        private Phase8ScoreSnapshotV1 CreateScoreSnapshot()
        {
            double horizon = _runtime.ScenarioHorizonSeconds;
            double energyQuality = Clamp(
                _integratedEnergyProxy / horizon / _parameters.NominalPowerFraction,
                0.0,
                1.0);
            double stabilityQuality = Clamp(_integratedStabilityProxy / horizon, 0.0, 1.0);
            double fuellingEfficiency = _initialRefuelRequests <= 0
                ? 1.0
                : Clamp(_runtime.RefuelRequestsRemaining / _initialRefuelRequests, 0.0, 1.0);
            double survivalPoints = _runtime.Outcome == Phase8ScenarioOutcomeV1.SurvivedScenarioHorizon
                ? _parameters.SurvivalPoints
                : 0.0;
            double energyPoints = energyQuality * _parameters.EnergyQualityPoints;
            double stabilityPoints = stabilityQuality * _parameters.StabilityQualityPoints;
            double fuellingPoints = fuellingEfficiency * _parameters.FuellingEfficiencyPoints;
            double controlPenalty = _committedActionCount * _parameters.ControlActionPenaltyPoints;
            double lossPenalty = _recordedLossCount * _parameters.RecordLossPenaltyPoints;
            double total = Clamp(
                survivalPoints + energyPoints + stabilityPoints + fuellingPoints -
                controlPenalty - lossPenalty,
                _parameters.ScoreMinimum,
                _parameters.ScoreMaximum);

            return new Phase8ScoreSnapshotV1(
                Round(total),
                Round(survivalPoints),
                Round(energyQuality),
                Round(energyPoints),
                Round(stabilityQuality),
                Round(stabilityPoints),
                Round(fuellingEfficiency),
                Round(fuellingPoints),
                Round(controlPenalty),
                Round(lossPenalty),
                _committedActionCount,
                _recordedLossCount);
        }

        private Phase8TurnSummaryV1 CreateTurnSummary(
            ulong wallMilliseconds,
            double simulationTimeStart,
            double simulationTimeEnd,
            double powerStart,
            double powerEnd,
            double tiltStart,
            double tiltEnd,
            Phase8ScenarioAdvanceResultV1 advance,
            Phase8ScoreSnapshotV1 score)
        {
            string cause;
            if (advance.LossRecords.Count > 0)
            {
                cause = "loss:" + advance.LossRecords[0].LossId;
            }
            else if (advance.EventRecords.Count > 0)
            {
                cause = "scripted:" + advance.EventRecords[0].Kind;
            }
            else if (advance.ActionTransitions.Count > 0)
            {
                cause = "player:" + advance.ActionTransitions[0].Kind;
            }
            else
            {
                cause = "elapsed_play";
            }

            string effect;
            if (advance.LossRecords.Count > 0)
            {
                effect = "scenario ended with " + advance.LossRecords[0].LossId + "; no safety response";
            }
            else if (advance.EventRecords.Count > 0 || advance.ActionTransitions.Count > 0)
            {
                effect = "proxy power " + FormatDelta(powerStart, powerEnd) +
                         ", tilt " + FormatDelta(tiltStart, tiltEnd) +
                         "; score=" + score.TotalPoints.ToString("R", CultureInfo.InvariantCulture);
            }
            else if (advance.Outcome == Phase8ScenarioOutcomeV1.SurvivedScenarioHorizon)
            {
                effect = "scenario horizon survived; score=" +
                         score.TotalPoints.ToString("R", CultureInfo.InvariantCulture);
            }
            else
            {
                effect = "simulation advanced without a recorded proxy transition";
            }

            return new Phase8TurnSummaryV1(
                _nextTurnId,
                wallMilliseconds,
                simulationTimeStart,
                simulationTimeEnd,
                checked((uint)advance.ActionTransitions.Count),
                checked((uint)advance.EventRecords.Count),
                checked((uint)advance.LossRecords.Count),
                powerStart,
                powerEnd,
                tiltStart,
                tiltEnd,
                advance.Outcome,
                cause,
                effect,
                score.TotalPoints);
        }

        private double StabilityQuality(double averagePower, double averageTilt, double averageMargin)
        {
            return Clamp(
                1.0 - Math.Abs(averagePower - _parameters.NominalPowerFraction) -
                averageTilt - Math.Abs(averageMargin),
                0.0,
                1.0);
        }

        private double Round(double value)
        {
            return Math.Round(
                value,
                (int)_parameters.RoundingDecimalPlaces,
                MidpointRounding.AwayFromZero);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static string FormatDelta(double before, double after)
        {
            return (after - before).ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
