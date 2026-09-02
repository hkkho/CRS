using System;
using ReactorSim.Game;

namespace ReactorGame.Unity
{
    /// <summary>
    /// Production Unity port over the engine-neutral game session.
    /// </summary>
    public sealed class UnityRuntimePort : IPhase8RuntimePort
    {
        private readonly GameSession _session;

        public UnityRuntimePort()
            : this(PracticeGameSessionFactory.Create())
        {
        }

        public UnityRuntimePort(GameSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public Phase8UnityPresentationSnapshotV1 Snapshot
        {
            get { return Project(_session.Snapshot); }
        }

        public Phase8UnityCommandResultV1 Execute(Phase8UnityInputCommandV1 command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            GameSessionCommandResult result;
            switch (command.Kind)
            {
                case Phase8UnityCommandKindV1.AdvanceWallMilliseconds:
                    result = _session.AdvanceWallMilliseconds(command.WallMilliseconds);
                    break;
                case Phase8UnityCommandKindV1.QueuePowerTarget:
                    result = _session.QueuePowerTarget(command.TargetFraction);
                    break;
                case Phase8UnityCommandKindV1.QueueTiltTarget:
                    result = _session.QueueTiltTarget(command.TargetFraction);
                    break;
                case Phase8UnityCommandKindV1.SetPlaybackMode:
                    result = _session.SetPlaybackMode(command.PlaybackModeId);
                    break;
                case Phase8UnityCommandKindV1.Pause:
                    result = _session.Pause();
                    break;
                case Phase8UnityCommandKindV1.Resume:
                    result = _session.Resume();
                    break;
                case Phase8UnityCommandKindV1.RefuelChannel:
                    result = _session.RefuelChannel(
                        command.ChannelIndex,
                        command.RefuellingDirectionId,
                        command.ShiftCount,
                        command.FuelTypeId);
                    break;
                case Phase8UnityCommandKindV1.PreviewRefuelChannel:
                    result = _session.PreviewRefuelChannel(
                        command.ChannelIndex,
                        command.RefuellingDirectionId,
                        command.ShiftCount,
                        command.FuelTypeId);
                    break;
                case Phase8UnityCommandKindV1.Debug:
                    result = ExecuteDebug(command);
                    break;
                default:
                    return Phase8UnityCommandResultV1.RejectedResult(
                        command,
                        "UnityRuntimePort.Command.Unsupported",
                        "The game session does not support this command kind.",
                        Snapshot);
            }

            Phase8UnityPresentationSnapshotV1 snapshot = Project(result.Snapshot);
            return result.Accepted
                ? Phase8UnityCommandResultV1.AcceptedResult(
                    command,
                    snapshot,
                    result.Message,
                    result.PreviewCore)
                : Phase8UnityCommandResultV1.RejectedResult(
                    command,
                    result.DiagnosticCode,
                    result.DiagnosticMessage,
                    snapshot);
        }

        private GameSessionCommandResult ExecuteDebug(Phase8UnityInputCommandV1 command)
        {
            switch (command.DebugAction)
            {
                case Phase8UnityDebugActionKindV1.GrantFreshBundles:
                    return _session.DebugGrantFreshBundles(command.DebugValue);
                case Phase8UnityDebugActionKindV1.ClearPendingActions:
                    return _session.DebugClearPendingActions();
                case Phase8UnityDebugActionKindV1.ResetSyntheticResponse:
                    return _session.DebugResetSyntheticResponse();
                default:
                    throw new InvalidOperationException(
                        "The game session does not support this debug action.");
            }
        }

        private static Phase8UnityPresentationSnapshotV1 Project(GameSessionSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new Phase8UnityPresentationSnapshotV1(
                snapshot.ScenarioId,
                snapshot.DifficultyId,
                snapshot.PlaybackModeId,
                snapshot.AccelerationFactor,
                snapshot.WallControlTickMilliseconds,
                snapshot.ScenarioHorizonSeconds,
                snapshot.SimulationTimeSeconds,
                snapshot.WallElapsedSeconds,
                snapshot.NormalizedPowerFraction,
                snapshot.AbsoluteTiltFraction,
                snapshot.ControlMarginFraction,
                snapshot.DeviceAvailableFraction,
                snapshot.RefuelRequestsRemaining,
                snapshot.PendingActionCount,
                snapshot.ProcessedScriptedEventCount,
                snapshot.ScoreTotal,
                snapshot.TurnSummaryCount,
                snapshot.OutcomeId,
                snapshot.IsPaused,
                snapshot.FreshBundlesAvailable,
                snapshot.RefuellingOperationCount,
                snapshot.LastRefuelledChannel,
                snapshot.LastRefuellingDirectionId,
                snapshot.LastRefuellingShiftCount,
                snapshot.Core,
                snapshot.Seed);
        }
    }
}
