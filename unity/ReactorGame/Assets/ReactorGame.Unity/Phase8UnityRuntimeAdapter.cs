using System;
using UnityEngine;

namespace ReactorGame.Unity
{
    /// <summary>
    /// Thin Unity presentation/input bridge for the engine-neutral Phase 8
    /// runtime. It never advances itself from Update or Unity time; callers
    /// must send explicit wall milliseconds to the bound runtime port.
    /// </summary>
    public sealed class Phase8UnityRuntimeAdapter : MonoBehaviour
    {
        private IPhase8RuntimePort _runtimePort;
        private Phase8UnityPresentationSnapshotV1 _snapshot;
        private ulong _nextCommandSequence = 1;
        private ulong _lastDispatchedSequence;

        public event Action<Phase8UnityPresentationSnapshotV1> SnapshotChanged;

        public event Action<Phase8UnityCommandResultV1> CommandCompleted;

        public bool IsBound
        {
            get { return _runtimePort != null; }
        }

        public Phase8UnityPresentationSnapshotV1 Snapshot
        {
            get { return _snapshot; }
        }

        public void Bind(IPhase8RuntimePort runtimePort)
        {
            if (runtimePort == null)
            {
                throw new ArgumentNullException(nameof(runtimePort));
            }

            _runtimePort = runtimePort;
            if (!TryRefreshSnapshot(out string diagnosticMessage))
            {
                _runtimePort = null;
                throw new InvalidOperationException(
                    "The Unity runtime adapter could not bind the runtime port: " + diagnosticMessage);
            }
        }

        public void Unbind()
        {
            _runtimePort = null;
            _snapshot = null;
        }

        public bool TryRefreshSnapshot(out string diagnosticMessage)
        {
            diagnosticMessage = string.Empty;
            if (_runtimePort == null)
            {
                diagnosticMessage = "The Unity runtime adapter has no bound runtime port.";
                return false;
            }

            try
            {
                Phase8UnityPresentationSnapshotV1 nextSnapshot = _runtimePort.Snapshot;
                if (nextSnapshot == null)
                {
                    diagnosticMessage = "The runtime port returned no presentation snapshot.";
                    return false;
                }

                PublishSnapshot(nextSnapshot);
                return true;
            }
            catch (Exception exception)
            {
                diagnosticMessage = exception.Message;
                return false;
            }
        }

        public Phase8UnityCommandResultV1 Dispatch(Phase8UnityInputCommandV1 command)
        {
            if (command == null)
            {
                return Phase8UnityCommandResultV1.RejectedResult(
                    0,
                    Phase8UnityCommandKindV1.AdvanceWallMilliseconds,
                    "UnityAdapter.Command.Missing",
                    "A Unity runtime command is required.");
            }

            if (_runtimePort == null)
            {
                return CompleteRejected(
                    command,
                    "UnityAdapter.Runtime.Unbound",
                    "The Unity runtime adapter has no bound runtime port.");
            }

            if (command.Sequence <= _lastDispatchedSequence)
            {
                return CompleteRejected(
                    command,
                    "UnityAdapter.Command.DuplicateSequence",
                    "A Unity command sequence may be dispatched only once.");
            }

            _lastDispatchedSequence = command.Sequence;
            if (command.Sequence >= _nextCommandSequence &&
                command.Sequence < ulong.MaxValue)
            {
                _nextCommandSequence = command.Sequence + 1;
            }

            Phase8UnityCommandResultV1 result;
            try
            {
                result = _runtimePort.Execute(command);
            }
            catch (Exception exception)
            {
                result = Phase8UnityCommandResultV1.RejectedResult(
                    command,
                    "UnityAdapter.Runtime.Exception",
                    exception.Message,
                    _snapshot);
            }

            if (result == null)
            {
                result = Phase8UnityCommandResultV1.RejectedResult(
                    command,
                    "UnityAdapter.Runtime.EmptyResult",
                    "The runtime port returned no command result.",
                    _snapshot);
            }
            else if (result.Sequence != command.Sequence || result.Kind != command.Kind)
            {
                result = Phase8UnityCommandResultV1.RejectedResult(
                    command,
                    "UnityAdapter.Runtime.ResultMismatch",
                    "The runtime port returned a result for a different command.",
                    _snapshot);
            }
            else if (result.Snapshot != null)
            {
                PublishSnapshot(result.Snapshot);
            }

            if (CommandCompleted != null)
            {
                CommandCompleted(result);
            }

            return result;
        }

        public Phase8UnityCommandResultV1 AdvanceWallMilliseconds(ulong wallMilliseconds)
        {
            return Dispatch(
                Phase8UnityInputCommandV1.AdvanceWallMilliseconds(
                    NextCommandSequence(),
                    wallMilliseconds));
        }

        public Phase8UnityCommandResultV1 QueuePowerTarget(double targetFraction)
        {
            return Dispatch(
                Phase8UnityInputCommandV1.QueuePowerTarget(
                    NextCommandSequence(),
                    targetFraction));
        }

        public Phase8UnityCommandResultV1 QueueTiltTarget(double targetFraction)
        {
            return Dispatch(
                Phase8UnityInputCommandV1.QueueTiltTarget(
                    NextCommandSequence(),
                    targetFraction));
        }

        public Phase8UnityCommandResultV1 SetPlaybackMode(string playbackModeId)
        {
            return Dispatch(
                Phase8UnityInputCommandV1.SetPlaybackMode(
                    NextCommandSequence(),
                    playbackModeId));
        }

        public Phase8UnityCommandResultV1 Pause()
        {
            return Dispatch(Phase8UnityInputCommandV1.Pause(NextCommandSequence()));
        }

        public Phase8UnityCommandResultV1 Resume()
        {
            return Dispatch(Phase8UnityInputCommandV1.Resume(NextCommandSequence()));
        }

        public Phase8UnityCommandResultV1 RefuelChannel(
            uint channelIndex,
            string refuellingDirectionId,
            ushort shiftCount,
            string fuelTypeId)
        {
            return Dispatch(
                Phase8UnityInputCommandV1.RefuelChannel(
                    NextCommandSequence(),
                    channelIndex,
                    refuellingDirectionId,
                    shiftCount,
                    fuelTypeId));
        }

        public Phase8UnityCommandResultV1 Debug(
            Phase8UnityDebugActionKindV1 debugAction,
            uint debugValue = 0)
        {
            return Dispatch(
                Phase8UnityInputCommandV1.Debug(
                    NextCommandSequence(),
                    debugAction,
                    debugValue));
        }

        private ulong NextCommandSequence()
        {
            if (_nextCommandSequence == ulong.MaxValue)
            {
                throw new InvalidOperationException("The Unity command sequence is exhausted.");
            }

            return _nextCommandSequence++;
        }

        private Phase8UnityCommandResultV1 CompleteRejected(
            Phase8UnityInputCommandV1 command,
            string diagnosticCode,
            string diagnosticMessage)
        {
            Phase8UnityCommandResultV1 result = Phase8UnityCommandResultV1.RejectedResult(
                command,
                diagnosticCode,
                diagnosticMessage,
                _snapshot);
            if (CommandCompleted != null)
            {
                CommandCompleted(result);
            }

            return result;
        }

        private void PublishSnapshot(Phase8UnityPresentationSnapshotV1 snapshot)
        {
            _snapshot = snapshot;
            if (SnapshotChanged != null)
            {
                SnapshotChanged(snapshot);
            }
        }
    }
}
