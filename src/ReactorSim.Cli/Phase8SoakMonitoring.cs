using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ReactorSim.Core;

namespace ReactorSim.Cli
{
    internal static class Phase8SoakPlanV1
    {
        private static readonly string[] RequiredPolicyIds =
        {
            "boundary-observer-challenge",
            "centered-power-standard",
            "hold-equilibrium-practice",
            "regional-tilt-standard"
        };

        public const string PlanId = "p8-t06-long-run-soak-v1";
        public const int CyclesPerPolicy = 16;

        public static IReadOnlyList<string> PolicyIds
        {
            get { return new ReadOnlyCollection<string>(RequiredPolicyIds); }
        }
    }

    internal sealed class Phase8SoakInvariantFailure : InvalidOperationException
    {
        public Phase8SoakInvariantFailure(string path, string message)
            : base(message)
        {
            Path = path;
        }

        public string Path { get; }
    }

    internal sealed class Phase8BaselinePolicySoakCycleResultV1
    {
        public Phase8BaselinePolicySoakCycleResultV1(
            string policyId,
            int cycleIndex,
            ulong replaySeed,
            Phase8BaselinePolicyRunResultV1 runResult,
            int invariantSampleCount)
        {
            PolicyId = policyId;
            CycleIndex = cycleIndex;
            ReplaySeed = replaySeed;
            Outcome = runResult.Outcome;
            SimulationTimeSeconds = runResult.SimulationTimeSeconds;
            WallElapsedSeconds = runResult.WallElapsedSeconds;
            ScoreTotal = runResult.ScoreTotal;
            LossCount = runResult.LossCount;
            TurnSummaryCount = runResult.TurnSummaryCount;
            ReplayDigest = runResult.ReplayDigest;
            InvariantSampleCount = invariantSampleCount;
        }

        public string PolicyId { get; }

        public int CycleIndex { get; }

        public ulong ReplaySeed { get; }

        public Phase8ScenarioOutcomeV1 Outcome { get; }

        public double SimulationTimeSeconds { get; }

        public double WallElapsedSeconds { get; }

        public double ScoreTotal { get; }

        public uint LossCount { get; }

        public int TurnSummaryCount { get; }

        public string ReplayDigest { get; }

        public int InvariantSampleCount { get; }
    }

    internal sealed class Phase8BaselinePolicySoakResultV1
    {
        public Phase8BaselinePolicySoakResultV1(
            string planId,
            int cyclesPerPolicy,
            IReadOnlyList<Phase8BaselinePolicySoakCycleResultV1> cycles)
        {
            PlanId = planId;
            CyclesPerPolicy = cyclesPerPolicy;
            Cycles = new ReadOnlyCollection<Phase8BaselinePolicySoakCycleResultV1>(
                cycles.ToArray());
            InvariantSampleCount = Cycles.Sum(item => item.InvariantSampleCount);
            TotalSimulationTimeSeconds = Cycles.Sum(item => item.SimulationTimeSeconds);
            TotalWallElapsedSeconds = Cycles.Sum(item => item.WallElapsedSeconds);
            TotalLossCount = Cycles.Aggregate(0u, (total, item) => checked(total + item.LossCount));
            AggregateDigest = ComputeAggregateDigest();
        }

        public string PlanId { get; }

        public int CyclesPerPolicy { get; }

        public IReadOnlyList<Phase8BaselinePolicySoakCycleResultV1> Cycles { get; }

        public int InvariantSampleCount { get; }

        public double TotalSimulationTimeSeconds { get; }

        public double TotalWallElapsedSeconds { get; }

        public uint TotalLossCount { get; }

        public string AggregateDigest { get; }

        private string ComputeAggregateDigest()
        {
            var canonical = new StringBuilder();
            canonical.Append("plan=")
                .Append(PlanId)
                .Append("|cycles_per_policy=")
                .Append(CyclesPerPolicy.ToString(CultureInfo.InvariantCulture))
                .AppendLine();
            foreach (Phase8BaselinePolicySoakCycleResultV1 cycle in Cycles)
            {
                canonical.Append(cycle.PolicyId)
                    .Append('|')
                    .Append(cycle.CycleIndex.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(cycle.ReplaySeed.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(cycle.Outcome)
                    .Append('|')
                    .Append(cycle.SimulationTimeSeconds.ToString("R", CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(cycle.WallElapsedSeconds.ToString("R", CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(cycle.ScoreTotal.ToString("R", CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(cycle.LossCount.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(cycle.TurnSummaryCount.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(cycle.InvariantSampleCount.ToString(CultureInfo.InvariantCulture))
                    .Append('|')
                    .Append(cycle.ReplayDigest)
                    .AppendLine();
            }

            byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
            return Convert.ToHexString(digest).ToLowerInvariant();
        }
    }

    internal sealed class Phase8SoakInvariantMonitorV1
    {
        private readonly Phase8BaselinePolicyV1 _policy;
        private readonly Phase8ScoredScenarioRuntimeV1 _runtime;
        private readonly uint _maximumPendingCommands;
        private readonly Phase8OperatingEnvelopeV1 _envelope;
        private readonly string _contextPath;
        private double _previousSimulationTimeSeconds;
        private double _previousWallElapsedSeconds;
        private double _previousPowerFraction;
        private double _previousTiltFraction;
        private ulong _previousSimulationStepIndex;
        private uint _previousProcessedScriptedEventCount;
        private int _previousTurnSummaryCount;
        private ulong _previousActionId;
        private double _previousEventTimeSeconds;
        private double _previousLossTimeSeconds;
        private int _sampleCount;

        public Phase8SoakInvariantMonitorV1(
            Phase8BaselinePolicyV1 policy,
            Phase8ScoredScenarioRuntimeV1 runtime,
            uint maximumPendingCommands,
            Phase8OperatingEnvelopeV1 envelope,
            string contextPath)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _envelope = envelope ?? throw new ArgumentNullException(nameof(envelope));
            _contextPath = string.IsNullOrWhiteSpace(contextPath)
                ? throw new ArgumentException("A soak context path is required.", nameof(contextPath))
                : contextPath;
            _maximumPendingCommands = maximumPendingCommands;
            CheckRuntimeState("initial");
        }

        public int SampleCount
        {
            get { return _sampleCount; }
        }

        public void ObserveQueuedAction()
        {
            CheckRuntimeState("action.queue");
        }

        public void ObserveAdvance(
            Phase8ScoredAdvanceResultV1 result,
            ulong expectedWallMilliseconds)
        {
            if (result == null)
            {
                throw Failure("advance", "The soak advance result is missing.");
            }

            Phase8ScenarioAdvanceResultV1 advance = result.Advance;
            if (advance == null || result.TurnSummary == null || result.Score == null)
            {
                throw Failure("advance", "The scored advance result is incomplete.");
            }

            double simulationTimeBefore = _previousSimulationTimeSeconds;
            double wallElapsedBefore = _previousWallElapsedSeconds;
            double powerBefore = _previousPowerFraction;
            double tiltBefore = _previousTiltFraction;
            ulong simulationStepBefore = _previousSimulationStepIndex;
            int turnSummaryBefore = _previousTurnSummaryCount;
            CheckAdvanceEnvelope(
                advance,
                expectedWallMilliseconds,
                simulationTimeBefore,
                wallElapsedBefore,
                simulationStepBefore);
            CheckAdvanceSegments(advance.StateSegments, simulationTimeBefore);
            CheckActionTransitions(advance.ActionTransitions);
            CheckEvents(advance.EventRecords);
            CheckLosses(advance.LossRecords);
            CheckTurnSummary(
                advance,
                result.TurnSummary,
                result.Score,
                simulationTimeBefore,
                powerBefore,
                tiltBefore,
                turnSummaryBefore);
            CheckScore(result.Score);
            CheckRuntimeState("advance");
            _sampleCount++;
        }

        public void Complete(Phase8BaselinePolicyRunResultV1 runResult)
        {
            if (runResult == null)
            {
                throw Failure("result", "The soak policy result is missing.");
            }

            CheckRuntimeState("complete");
            if (_runtime.Outcome == Phase8ScenarioOutcomeV1.Running)
            {
                throw Failure("outcome", "A soak cycle must resolve before completion.");
            }

            RequireEqual(
                _policy.Expected.Outcome,
                runResult.Outcome.ToString(),
                "expected.outcome");
            RequireEqual(
                _policy.Expected.SimulationTimeSeconds,
                runResult.SimulationTimeSeconds,
                "expected.simulation_time_s");
            RequireEqual(
                _policy.Expected.WallElapsedSeconds,
                runResult.WallElapsedSeconds,
                "expected.wall_elapsed_s");
            RequireEqual(
                _policy.Expected.ScoreTotal,
                runResult.ScoreTotal,
                "expected.score_total");
            RequireEqual(
                _policy.Expected.LossCount,
                runResult.LossCount,
                "expected.loss_count");
            RequireEqual(
                _policy.Expected.TurnSummaryCount,
                runResult.TurnSummaryCount,
                "expected.turn_summary_count");
            RequireEqual(
                _policy.Expected.ReplayDigest,
                runResult.ReplayDigest,
                "expected.replay_digest");
        }

        private void CheckRuntimeState(string path)
        {
            CheckFinite(_runtime.SimulationTimeSeconds, path + ".simulation_time_s");
            CheckFinite(_runtime.WallElapsedSeconds, path + ".wall_elapsed_s");
            CheckFinite(_runtime.NormalizedPowerFraction, path + ".normalized_power_fraction");
            CheckFinite(_runtime.AbsoluteTiltFraction, path + ".absolute_tilt_fraction");
            CheckFinite(_runtime.ControlMarginFraction, path + ".control_margin_fraction");
            CheckFinite(_runtime.DeviceAvailableFraction, path + ".device_available_fraction");
            CheckFinite(_runtime.Score.TotalPoints, path + ".score_total");

            if (_runtime.SimulationTimeSeconds < 0 ||
                _runtime.SimulationTimeSeconds > _runtime.ScenarioHorizonSeconds ||
                _runtime.WallElapsedSeconds < 0 ||
                _runtime.WallElapsedSeconds > _policy.HorizonWallMilliseconds / 1000.0 ||
                _runtime.SimulationTimeSeconds < _previousSimulationTimeSeconds ||
                _runtime.WallElapsedSeconds < _previousWallElapsedSeconds ||
                _runtime.SimulationStepIndex < _previousSimulationStepIndex ||
                _runtime.ProcessedScriptedEventCount < _previousProcessedScriptedEventCount ||
                _runtime.DeviceAvailableFraction < 0 ||
                _runtime.DeviceAvailableFraction > 1 ||
                _runtime.PendingActionCount > _maximumPendingCommands ||
                !Enum.IsDefined(_runtime.Outcome))
            {
                throw Failure(path, "A runtime clock, state, capacity, or outcome invariant was violated.");
            }

            if (_runtime.Outcome == Phase8ScenarioOutcomeV1.RecordLoss)
            {
                CheckTerminalLossEnvelope(path);
            }
            else
            {
                CheckOperatingEnvelope(
                    _runtime.NormalizedPowerFraction,
                    _runtime.AbsoluteTiltFraction,
                    _runtime.ControlMarginFraction,
                    _runtime.DeviceAvailableFraction,
                    path + ".operating_envelope");
            }

            _previousSimulationTimeSeconds = _runtime.SimulationTimeSeconds;
            _previousWallElapsedSeconds = _runtime.WallElapsedSeconds;
            _previousPowerFraction = _runtime.NormalizedPowerFraction;
            _previousTiltFraction = _runtime.AbsoluteTiltFraction;
            _previousSimulationStepIndex = _runtime.SimulationStepIndex;
            _previousProcessedScriptedEventCount = _runtime.ProcessedScriptedEventCount;
            _previousTurnSummaryCount = _runtime.TurnSummaries.Count;
        }

        private void CheckAdvanceEnvelope(
            Phase8ScenarioAdvanceResultV1 advance,
            ulong expectedWallMilliseconds,
            double simulationTimeBefore,
            double wallElapsedBefore,
            ulong simulationStepBefore)
        {
            if (advance.WallMillisecondsRequested != expectedWallMilliseconds ||
                advance.WallMillisecondsRequested == 0 ||
                _runtime.Runtime.WallControlTickMilliseconds != 100 ||
                advance.SimulationTimeSeconds != _runtime.SimulationTimeSeconds ||
                advance.WallElapsedSeconds != _runtime.WallElapsedSeconds ||
                advance.Outcome != _runtime.Outcome ||
                advance.IsPaused != _runtime.IsPaused)
            {
                throw Failure("advance", "The returned advance envelope does not match the runtime state or scheduler request.");
            }

            if (advance.ControlTicksProcessed == 0)
            {
                throw Failure("advance.control_ticks", "A positive running soak interval must process a control tick.");
            }

            ulong actualWallMilliseconds = ToWallMilliseconds(
                _runtime.WallElapsedSeconds - wallElapsedBefore,
                "advance.wall_delta_ms");
            ulong expectedControlTicks =
                (actualWallMilliseconds + _runtime.Runtime.WallControlTickMilliseconds - 1) /
                _runtime.Runtime.WallControlTickMilliseconds;
            if (_runtime.SimulationStepIndex < simulationStepBefore ||
                advance.ControlTicksProcessed != expectedControlTicks ||
                _runtime.SimulationStepIndex - simulationStepBefore != advance.ControlTicksProcessed)
            {
                throw Failure(
                    "advance.control_ticks",
                    "The returned control-tick count does not match explicit wall-time decomposition and step progress.");
            }

            if (_runtime.SimulationTimeSeconds < simulationTimeBefore)
            {
                throw Failure("advance.simulation_time_s", "The returned simulation clock moved backward.");
            }
        }

        private void CheckAdvanceSegments(
            IReadOnlyList<Phase8ScenarioAdvanceSegmentV1> segments,
            double simulationTimeBefore)
        {
            if (segments == null)
            {
                throw Failure("advance.state_segments", "State segments are missing.");
            }

            if (segments.Count == 0 && _runtime.SimulationTimeSeconds != simulationTimeBefore)
            {
                throw Failure("advance.state_segments", "State segments do not cover a positive simulation interval.");
            }

            double previousEnd = simulationTimeBefore;
            foreach (Phase8ScenarioAdvanceSegmentV1 segment in segments)
            {
                CheckFinite(segment.SimulationTimeStartSeconds, "state_segments.simulation_time_start_s");
                CheckFinite(segment.SimulationTimeEndSeconds, "state_segments.simulation_time_end_s");
                CheckFinite(segment.NormalizedPowerFraction, "state_segments.normalized_power_fraction");
                CheckFinite(segment.AbsoluteTiltFraction, "state_segments.absolute_tilt_fraction");
                CheckFinite(segment.ControlMarginFraction, "state_segments.control_margin_fraction");
                CheckOperatingEnvelope(
                    segment.NormalizedPowerFraction,
                    segment.AbsoluteTiltFraction,
                    segment.ControlMarginFraction,
                    _runtime.DeviceAvailableFraction,
                    "state_segments.operating_envelope");
                if (segment.SimulationTimeStartSeconds != previousEnd ||
                    segment.SimulationTimeEndSeconds < segment.SimulationTimeStartSeconds ||
                    segment.SimulationTimeEndSeconds > _runtime.ScenarioHorizonSeconds ||
                    segment.AbsoluteTiltFraction < 0)
                {
                    throw Failure("advance.state_segments", "State segment ordering or domain is invalid.");
                }

                previousEnd = segment.SimulationTimeEndSeconds;
                _sampleCount++;
            }

            if (segments.Count > 0 && previousEnd != _runtime.SimulationTimeSeconds)
            {
                throw Failure("advance.state_segments", "State segments leave an uncovered simulation interval.");
            }
        }

        private void CheckOperatingEnvelope(
            double powerFraction,
            double tiltFraction,
            double controlMarginFraction,
            double deviceAvailableFraction,
            string path)
        {
            if (powerFraction < _envelope.NormalizedPowerMinimum ||
                powerFraction > _envelope.NormalizedPowerMaximum ||
                tiltFraction < 0 ||
                tiltFraction > _envelope.AbsoluteTiltMaximum ||
                controlMarginFraction < _envelope.ControlMarginMinimum ||
                controlMarginFraction > _envelope.ControlMarginMaximum ||
                deviceAvailableFraction <= 0 ||
                deviceAvailableFraction > 1)
            {
                throw Failure(path, "A nonterminal runtime state is outside the approved operating envelope.");
            }
        }

        private void CheckTerminalLossEnvelope(string path)
        {
            if (_runtime.Runtime.LossRecords.Count != 1)
            {
                throw Failure(path + ".losses", "A record-loss outcome must have exactly one loss record.");
            }

            Phase8LossRecordV1 loss = _runtime.Runtime.LossRecords[0];
            bool matches = loss.Metric switch
            {
                "normalized_power_fraction" =>
                    (loss.Value < _envelope.NormalizedPowerMinimum ||
                     loss.Value > _envelope.NormalizedPowerMaximum) &&
                    loss.Value == _runtime.NormalizedPowerFraction,
                "absolute_tilt_fraction" =>
                    loss.Value > _envelope.AbsoluteTiltMaximum &&
                    loss.Value == _runtime.AbsoluteTiltFraction,
                "control_margin_fraction" =>
                    (loss.Value < _envelope.ControlMarginMinimum ||
                     loss.Value > _envelope.ControlMarginMaximum) &&
                    loss.Value == _runtime.ControlMarginFraction,
                "device_available_fraction" =>
                    loss.Value <= 0 && loss.Value == _runtime.DeviceAvailableFraction,
                _ => false
            };
            if (!matches)
            {
                throw Failure(path + ".losses", "The terminal loss does not identify the observed envelope violation.");
            }
        }

        private ulong ToWallMilliseconds(double seconds, string path)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
            {
                throw Failure(path, "A wall-time observable must be finite and nonnegative.");
            }

            double milliseconds = Math.Round(seconds * 1000.0, MidpointRounding.ToEven);
            if (milliseconds < 0 || milliseconds > ulong.MaxValue)
            {
                throw Failure(path, "A wall-time observable is outside the UInt64 millisecond domain.");
            }

            return (ulong)milliseconds;
        }

        private void CheckActionTransitions(
            IReadOnlyList<Phase8ActionTransitionV1> transitions)
        {
            ulong currentWallMilliseconds = ToWallMilliseconds(
                _runtime.WallElapsedSeconds,
                "action_transitions.current_wall_ms");
            foreach (Phase8ActionTransitionV1 transition in transitions)
            {
                CheckFinite(transition.AcknowledgedWallTimeSeconds, "action_transitions.acknowledged_wall_s");
                CheckFinite(transition.CommittedWallTimeSeconds, "action_transitions.committed_wall_s");
                CheckFinite(transition.QueueDelayWallTimeSeconds, "action_transitions.queue_delay_wall_s");
                ulong acknowledgedWallMilliseconds = ToWallMilliseconds(
                    transition.AcknowledgedWallTimeSeconds,
                    "action_transitions.acknowledged_wall_ms");
                ulong committedWallMilliseconds = ToWallMilliseconds(
                    transition.CommittedWallTimeSeconds,
                    "action_transitions.committed_wall_ms");
                ulong queueDelayMilliseconds = ToWallMilliseconds(
                    transition.QueueDelayWallTimeSeconds,
                    "action_transitions.queue_delay_ms");
                if (transition.ActionId <= _previousActionId ||
                    acknowledgedWallMilliseconds > committedWallMilliseconds ||
                    committedWallMilliseconds > currentWallMilliseconds ||
                    queueDelayMilliseconds != committedWallMilliseconds - acknowledgedWallMilliseconds ||
                    queueDelayMilliseconds > _runtime.Runtime.WallControlTickMilliseconds)
                {
                    throw Failure(
                        "action_transitions",
                        "Action identity, timing, or queue-delay ordering is invalid: id=" +
                        transition.ActionId.ToString(CultureInfo.InvariantCulture) +
                        ", previous_id=" + _previousActionId.ToString(CultureInfo.InvariantCulture) +
                        ", ack=" + transition.AcknowledgedWallTimeSeconds.ToString("R", CultureInfo.InvariantCulture) +
                        ", commit=" + transition.CommittedWallTimeSeconds.ToString("R", CultureInfo.InvariantCulture) +
                        ", delay=" + transition.QueueDelayWallTimeSeconds.ToString("R", CultureInfo.InvariantCulture) +
                        ", maximum=" + (_runtime.Runtime.WallControlTickMilliseconds / 1000.0).ToString("R", CultureInfo.InvariantCulture));
                }

                _previousActionId = transition.ActionId;
                _sampleCount++;
            }
        }

        private void CheckEvents(IReadOnlyList<Phase8EventRecordV1> events)
        {
            foreach (Phase8EventRecordV1 record in events)
            {
                CheckFinite(record.SimulationTimeSeconds, "events.simulation_time_s");
                if (record.SimulationTimeSeconds < _previousEventTimeSeconds ||
                    record.SimulationTimeSeconds > _runtime.ScenarioHorizonSeconds ||
                    string.IsNullOrWhiteSpace(record.EventId))
                {
                    throw Failure("events", "Scripted event identity or time ordering is invalid.");
                }

                _previousEventTimeSeconds = record.SimulationTimeSeconds;
                _sampleCount++;
            }
        }

        private void CheckLosses(IReadOnlyList<Phase8LossRecordV1> losses)
        {
            foreach (Phase8LossRecordV1 record in losses)
            {
                CheckFinite(record.SimulationTimeSeconds, "losses.simulation_time_s");
                CheckFinite(record.Value, "losses.value");
                if (record.SimulationTimeSeconds < _previousLossTimeSeconds ||
                    record.SimulationTimeSeconds > _runtime.ScenarioHorizonSeconds ||
                    string.IsNullOrWhiteSpace(record.LossId) ||
                    string.IsNullOrWhiteSpace(record.Metric))
                {
                    throw Failure("losses", "Loss identity or time ordering is invalid.");
                }

                _previousLossTimeSeconds = record.SimulationTimeSeconds;
                _sampleCount++;
            }

            if (_runtime.Runtime.LossRecords.Count > 1)
            {
                throw Failure("losses", "A synthetic scenario may record at most one terminal loss.");
            }
        }

        private void CheckTurnSummary(
            Phase8ScenarioAdvanceResultV1 advance,
            Phase8TurnSummaryV1 summary,
            Phase8ScoreSnapshotV1 score,
            double simulationTimeBefore,
            double powerBefore,
            double tiltBefore,
            int turnSummaryBefore)
        {
            CheckFinite(summary.SimulationTimeStartSeconds, "turn_summary.simulation_time_start_s");
            CheckFinite(summary.SimulationTimeEndSeconds, "turn_summary.simulation_time_end_s");
            CheckFinite(summary.NormalizedPowerBeforeFraction, "turn_summary.normalized_power_before_fraction");
            CheckFinite(summary.NormalizedPowerAfterFraction, "turn_summary.normalized_power_after_fraction");
            CheckFinite(summary.AbsoluteTiltBeforeFraction, "turn_summary.absolute_tilt_before_fraction");
            CheckFinite(summary.AbsoluteTiltAfterFraction, "turn_summary.absolute_tilt_after_fraction");
            CheckFinite(summary.ScoreTotal, "turn_summary.score_total");
            if (summary.TurnId != (ulong)(turnSummaryBefore + 1) ||
                summary.WallMillisecondsRequested != advance.WallMillisecondsRequested ||
                summary.SimulationTimeStartSeconds != simulationTimeBefore ||
                summary.SimulationTimeEndSeconds != _runtime.SimulationTimeSeconds ||
                summary.CommittedActionCount != (uint)advance.ActionTransitions.Count ||
                summary.ScriptedEventCount != (uint)advance.EventRecords.Count ||
                summary.LossCount != (uint)advance.LossRecords.Count ||
                summary.NormalizedPowerBeforeFraction != powerBefore ||
                summary.NormalizedPowerAfterFraction != _runtime.NormalizedPowerFraction ||
                summary.AbsoluteTiltBeforeFraction != tiltBefore ||
                summary.AbsoluteTiltAfterFraction != _runtime.AbsoluteTiltFraction ||
                summary.Outcome != _runtime.Outcome ||
                summary.ScoreTotal != score.TotalPoints ||
                _runtime.TurnSummaries.Count != turnSummaryBefore + 1 ||
                string.IsNullOrWhiteSpace(summary.Cause) ||
                string.IsNullOrWhiteSpace(summary.Effect))
            {
                throw Failure("turn_summary", "Turn-summary identity, timing, outcome, or score is inconsistent.");
            }
        }

        private void CheckScore(Phase8ScoreSnapshotV1 score)
        {
            double[] pointValues =
            {
                score.TotalPoints,
                score.SurvivalPoints,
                score.EnergyPoints,
                score.StabilityPoints,
                score.FuellingEfficiencyPoints,
                score.ControlPenaltyPoints,
                score.LossPenaltyPoints,
                score.EnergyQuality,
                score.StabilityQuality,
                score.FuellingEfficiency
            };
            if (pointValues.Any(value => double.IsNaN(value) || double.IsInfinity(value)) ||
                score.TotalPoints < _runtime.Parameters.ScoreMinimum ||
                score.TotalPoints > _runtime.Parameters.ScoreMaximum ||
                score.EnergyQuality < 0 || score.EnergyQuality > 1 ||
                score.StabilityQuality < 0 || score.StabilityQuality > 1 ||
                score.FuellingEfficiency < 0 || score.FuellingEfficiency > 1 ||
                score.RecordedLossCount != (uint)_runtime.Runtime.LossRecords.Count)
            {
                throw Failure("score", "Score finiteness, bounds, quality, or loss-count invariant was violated.");
            }
        }

        private void CheckFinite(double value, string path)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw Failure(path, "A soak observable must remain finite.");
            }
        }

        private void RequireEqual<T>(T expected, T actual, string path)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw Failure(path, "The soak observable does not match the approved P8-T05 baseline.");
            }
        }

        private Phase8SoakInvariantFailure Failure(string path, string message)
        {
            return new Phase8SoakInvariantFailure(_contextPath + "." + path, message);
        }
    }
}
