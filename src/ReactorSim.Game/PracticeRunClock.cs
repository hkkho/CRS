using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ReactorSim.Core;

namespace ReactorSim.Game
{
    internal enum PracticeRunOutcome { Running, SurvivedScenarioHorizon, RecordLoss }
    internal sealed class PracticeRunSegment
    {
        internal PracticeRunSegment(double start, double end, double power)
        { SimulationTimeStartSeconds = start; SimulationTimeEndSeconds = end; NormalizedPowerFraction = power; }
        internal double SimulationTimeStartSeconds { get; }
        internal double SimulationTimeEndSeconds { get; }
        internal double NormalizedPowerFraction { get; }
    }
    internal sealed class PracticeRunAdvance
    {
        internal PracticeRunAdvance(PracticeRunClock owner, ulong generation, PracticeRunClock candidate, List<PracticeRunSegment> segments)
        { Owner = owner; Generation = generation; Candidate = candidate.CaptureState(); StateSegments = new ReadOnlyCollection<PracticeRunSegment>(segments); }
        internal PracticeRunClock Owner { get; }
        internal ulong Generation { get; }
        internal PracticeRunState Candidate { get; }
        internal IReadOnlyList<PracticeRunSegment> StateSegments { get; }
        internal PracticeRunAdvance Advance => this;
        internal double SimulationTimeSeconds => Candidate.SimulationTimeSeconds;
    }
    internal sealed class PracticeRunState
    {
        internal PracticeRunState(ulong elapsed, ulong accumulator, List<double> targets,
            double time, double power, PracticeRunOutcome outcome, uint turns)
        {
            Elapsed = elapsed; Accumulator = accumulator; Targets = Array.AsReadOnly(targets.ToArray());
            SimulationTimeSeconds = time; NormalizedPowerFraction = power; Outcome = outcome; TurnSummaryCount = turns;
        }
        internal ulong Elapsed { get; }
        internal ulong Accumulator { get; }
        internal IReadOnlyList<double> Targets { get; }
        internal double SimulationTimeSeconds { get; }
        internal double NormalizedPowerFraction { get; }
        internal PracticeRunOutcome Outcome { get; }
        internal uint TurnSummaryCount { get; }
    }
    /// <summary>Practice clock/queued target/outcome authority. No scenario event
    /// engine, legacy scoring windows or retained turn-history allocations.</summary>
    internal sealed class PracticeRunClock
    {
        private readonly Phase8TimeModelV1 timeModel;
        private Phase8PlaybackModeV1 playback;
        private List<double> targets = new List<double>();
        private ulong elapsed, accumulator, generation, nextActionId = 1;
        internal PracticeRunClock(string scenario, ulong seed, double horizon, Phase8TimeModelV1 model, Phase8PlaybackModeV1 mode)
        { ScenarioId = scenario; Seed = seed; ScenarioHorizonSeconds = horizon; timeModel = model; playback = mode; }
        internal string ScenarioId { get; }
        internal string DifficultyId { get; } = PracticeGameSessionFactory.DifficultyId;
        internal ulong Seed { get; }
        internal double ScenarioHorizonSeconds { get; }
        internal double SimulationTimeSeconds { get; private set; }
        internal double WallElapsedSeconds => elapsed / 1000.0;
        internal double NormalizedPowerFraction { get; private set; } = 1;
        internal double DeviceAvailableFraction { get; } = 1;
        internal uint RefuelRequestsRemaining { get; } = 6; // Preserved legacy metadata, never inventory.
        internal uint PendingActionCount => checked((uint)targets.Count);
        internal uint ProcessedScriptedEventCount { get; }
        internal uint TurnSummaryCount { get; private set; }
        internal bool IsPaused { get; private set; }
        internal PracticeRunOutcome Outcome { get; private set; }
        internal string PlaybackModeId => playback.ModeId;
        internal double AccelerationFactor => playback.AccelerationFactor;
        internal ContractValidationResult<bool> TryPause() { IsPaused = true; generation++; return ContractValidationResult<bool>.Valid(true); }
        internal ContractValidationResult<bool> TryResume() { IsPaused = false; generation++; return ContractValidationResult<bool>.Valid(true); }
        internal ContractValidationResult<bool> TrySetPlaybackMode(Phase8PlaybackModeV1 mode)
        { playback = mode; generation++; return ContractValidationResult<bool>.Valid(true); }
        internal ContractValidationResult<uint> TryClearPendingActions()
        { uint count = PendingActionCount; targets.Clear(); generation++; return ContractValidationResult<uint>.Valid(count); }
        internal ContractValidationResult<bool> TryQueuePowerTarget(double value)
        {
            if (IsPaused) return Invalid<bool>("Phase8Runtime.Action.Paused", "action", "Player actions are not accepted while the scenario is paused.");
            if (double.IsNaN(value) || double.IsInfinity(value)) return Invalid<bool>("Phase8Runtime.Action.Value.Invalid", "action.value", "An action target must be finite.");
            if (targets.Count >= 4) return Invalid<bool>("Phase8Runtime.Action.Queue.Full", "pending_actions", "The approved difficulty command capacity has been reached.");
            if (nextActionId == ulong.MaxValue) return Invalid<bool>("Phase8Runtime.Action.Id.Overflow", "action_id", "The action identity sequence cannot advance past UInt64.MaxValue.");
            nextActionId++; targets.Add(value); generation++;
            return ContractValidationResult<bool>.Valid(true);
        }
        internal ContractValidationResult<PracticeRunAdvance> TryPlanAdvanceWallMilliseconds(ulong milliseconds)
        {
            var candidate = (PracticeRunClock)MemberwiseClone(); candidate.targets = new List<double>(targets);
            var segments = new List<PracticeRunSegment>();
            ulong remaining = milliseconds;
            if (!IsPaused && Outcome == PracticeRunOutcome.Running) while (remaining > 0)
            {
                ulong chunk = Math.Min(remaining, timeModel.WallControlTickMilliseconds);
                if (candidate.elapsed > ulong.MaxValue - chunk || candidate.accumulator > ulong.MaxValue - chunk)
                    return Invalid<PracticeRunAdvance>("Phase8Runtime.WallTime.Overflow", "wall_milliseconds", "The explicit presentation wall time cannot advance past UInt64.MaxValue milliseconds.");
                candidate.elapsed += chunk; candidate.accumulator += chunk; remaining -= chunk;
                while (candidate.accumulator >= timeModel.WallControlTickMilliseconds && candidate.Outcome == PracticeRunOutcome.Running)
                {
                    candidate.accumulator -= timeModel.WallControlTickMilliseconds;
                    foreach (double target in candidate.targets)
                    {
                        candidate.NormalizedPowerFraction = target;
                        if (target < .8 || target > 1.2) { candidate.Outcome = PracticeRunOutcome.RecordLoss; break; }
                    }
                    candidate.targets.Clear();
                    if (candidate.Outcome != PracticeRunOutcome.Running) break;
                    double start = candidate.SimulationTimeSeconds;
                    double end = start + AccelerationFactor * timeModel.WallControlTickSeconds;
                    candidate.SimulationTimeSeconds = ScenarioHorizonSeconds > 0 ? Math.Min(ScenarioHorizonSeconds, end) : end;
                    if (candidate.SimulationTimeSeconds > start) segments.Add(new PracticeRunSegment(start, candidate.SimulationTimeSeconds, candidate.NormalizedPowerFraction));
                    if (ScenarioHorizonSeconds > 0 && candidate.SimulationTimeSeconds >= ScenarioHorizonSeconds) candidate.Outcome = PracticeRunOutcome.SurvivedScenarioHorizon;
                }
                if (candidate.Outcome != PracticeRunOutcome.Running) break;
            }
            candidate.TurnSummaryCount = checked(TurnSummaryCount + 1);
            return ContractValidationResult<PracticeRunAdvance>.Valid(new PracticeRunAdvance(this, generation, candidate, segments));
        }
        internal ContractValidationResult<bool> TryCommitAdvance(PracticeRunAdvance plan)
        {
            if (!ReferenceEquals(plan.Owner, this) || plan.Generation != generation)
                return Invalid<bool>("GameSession.Clock.StalePlan", "advance", "The clock candidate no longer matches the committed run.");
            var candidate = plan.Candidate;
            elapsed = candidate.Elapsed; accumulator = candidate.Accumulator; targets = new List<double>(candidate.Targets);
            SimulationTimeSeconds = candidate.SimulationTimeSeconds; NormalizedPowerFraction = candidate.NormalizedPowerFraction;
            Outcome = candidate.Outcome; TurnSummaryCount = candidate.TurnSummaryCount; generation++;
            return ContractValidationResult<bool>.Valid(true);
        }
        internal PracticeRunState CaptureState() => new PracticeRunState(elapsed, accumulator, targets,
            SimulationTimeSeconds, NormalizedPowerFraction, Outcome, TurnSummaryCount);

        internal ulong Generation => generation;

        internal PracticeRunClock Fork()
        {
            var copy = (PracticeRunClock)MemberwiseClone();
            copy.targets = new List<double>(targets);
            return copy;
        }

        internal ContractValidationResult<PracticeRunAdvance> TryPlanSimulationStep(double seconds)
        {
            if (TurnSummaryCount == uint.MaxValue)
                return Invalid<PracticeRunAdvance>("GameSession.Day.StepCount.Overflow", "steps", "The simulation step count cannot advance.");
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds <= 0 || targets.Count > 0)
                return Invalid<PracticeRunAdvance>("GameSession.Day.Step.Invalid", "seconds", "A daily step requires positive finite simulation time and no queued targets.");
            var candidate = Fork();
            double end = SimulationTimeSeconds + seconds;
            if (double.IsInfinity(end) || end <= SimulationTimeSeconds)
                return Invalid<PracticeRunAdvance>("GameSession.Day.Time.Overflow", "seconds", "Simulation time cannot advance.");
            candidate.SimulationTimeSeconds = ScenarioHorizonSeconds > 0 ? Math.Min(ScenarioHorizonSeconds, end) : end;
            if (ScenarioHorizonSeconds > 0 && candidate.SimulationTimeSeconds >= ScenarioHorizonSeconds)
                candidate.Outcome = PracticeRunOutcome.SurvivedScenarioHorizon;
            candidate.TurnSummaryCount = checked(TurnSummaryCount + 1);
            return ContractValidationResult<PracticeRunAdvance>.Valid(new PracticeRunAdvance(this, generation, candidate,
                new List<PracticeRunSegment> { new PracticeRunSegment(SimulationTimeSeconds, candidate.SimulationTimeSeconds, NormalizedPowerFraction) }));
        }
        private static ContractValidationResult<T> Invalid<T>(string code, string path, string message) => ContractValidationResult<T>.Invalid(code, path, message);
    }
}
