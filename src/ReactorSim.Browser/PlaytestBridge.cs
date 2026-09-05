using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using ReactorSim.Core;
using ReactorSim.Game;

namespace ReactorSim.Browser
{
    /// <summary>
    /// JSON boundary used by the optional browser-WASM host. The browser sees
    /// one command/response shape for both modes; Lab diagnostics are carried
    /// as an additional field so the Play presentation remains reusable.
    /// </summary>
    public static class PlaytestBridgeV1
    {
        private const string DefaultMode = "play";
        private const string DefaultDataPackId =
            PracticeGameSessionFactory.DiffusionDataPackVersion;
        private const string LabDataPackId = "lab-2x8-synthetic-v1";
        private const string TowardEndA = "toward-end-a";
        private const string TowardEndB = "toward-end-b";

        private static readonly object Sync = new object();
        private static BridgeRuntime? _runtimeInstance;

        // Do not construct the full browser session as a type initializer.
        // A failure there is surfaced by the WASM runtime only as the generic
        // TypeInitialization_Type error, which hides the actionable cause from
        // the browser console. Lazy construction keeps capability discovery
        // available and preserves the original exception at the JSON boundary.
        private static BridgeRuntime _runtime
        {
            get
            {
                if (_runtimeInstance != null)
                {
                    return _runtimeInstance;
                }

                lock (Sync)
                {
                    if (_runtimeInstance == null)
                    {
                        try
                        {
                            _runtimeInstance = CreateRuntime(DefaultMode, "{}");
                        }
                        catch (Exception exception)
                        {
                            throw new InvalidOperationException(
                                "The authoritative browser session could not initialize: " +
                                exception,
                                exception);
                        }
                    }

                    return _runtimeInstance;
                }
            }
            set { _runtimeInstance = value; }
        }

        /// <summary>
        /// Returns the stable capability descriptor without creating a game
        /// session. It is safe to call before WASM initialization completes.
        /// </summary>
        public static string GetCapabilities()
        {
            BridgeCapabilitiesDto capabilities = new BridgeCapabilitiesDto
            {
                Operations = new List<string>
                {
                    "GetCapabilities",
                    "Initialize",
                    "Dispatch"
                },
                Modes = new List<BridgeModeCapabilityDto>
                {
                    new BridgeModeCapabilityDto
                    {
                        Id = "play",
                        Label = "Play",
                        AuthoritativeModel = "ReactorSim.Game.GameSession",
                        FixtureId = PracticeGameSessionFactory.DiffusionDataPackVersion,
                        Commands = PlayCommands()
                    },
                    new BridgeModeCapabilityDto
                    {
                        Id = "lab",
                        Label = "Lab",
                        AuthoritativeModel = "ReactorSim.Core.SpatialEigenSolve",
                        FixtureId = LabPlaytestSession.FixtureId,
                        Commands = new List<string>
                        {
                            "solve",
                            "preview-refuel",
                            "commit-refuel",
                            "reset"
                        }
                    }
                }
            };

            return PlaytestProtocolV1.Serialize(capabilities);
        }

        /// <summary>
        /// Creates a deterministic session. The request accepts {"mode":
        /// "play"} or {"mode":"lab"}; unknown modes are rejected without
        /// replacing the current session.
        /// </summary>
        public static string Initialize(string requestJson)
        {
            lock (Sync)
            {
                if (!TryParseObject(requestJson, out JsonDocument? document, out BridgeDiagnosticDto? parseFailure))
                {
                    return SerializeError("initialize", parseFailure!, _runtime);
                }

                JsonDocument parsedDocument = document!;
                using (parsedDocument)
                {
                    string mode = DefaultMode;
                    if (PlaytestInput.TryGetProperty(
                            parsedDocument.RootElement,
                            out JsonElement modeValue,
                            "mode"))
                    {
                        if (modeValue.ValueKind != JsonValueKind.String ||
                            string.IsNullOrWhiteSpace(modeValue.GetString()))
                        {
                            return SerializeError(
                                "initialize",
                                PlaytestProtocolV1.Diagnostic(
                                    "Browser.Initialize.Mode.Invalid",
                                    "mode",
                                    "mode must be the string play or lab."),
                                _runtime);
                        }

                        mode = PlaytestInput.NormalizeMode(modeValue.GetString()!);
                    }

                    if (mode != "play" && mode != "lab")
                    {
                        return SerializeError(
                            "initialize",
                            PlaytestProtocolV1.Diagnostic(
                                "Browser.Initialize.Mode.Unsupported",
                                "mode",
                                "The browser bridge supports play and lab modes."),
                            _runtime);
                    }

                    BridgeRuntime candidate = CreateRuntime(mode, requestJson ?? "{}");
                    if (candidate.InitializationFailure != null)
                    {
                        return SerializeError(
                            "initialize",
                            candidate.InitializationFailure,
                            _runtime);
                    }

                    _runtime = candidate;
                    PlaytestSnapshotDto snapshot = CreateSnapshot(_runtime, 0.0);
                    string stateDigest = ComputeStateDigest(_runtime, snapshot);
                    return PlaytestProtocolV1.Serialize(
                        new PlaytestResponseDto
                        {
                            Operation = "initialize",
                            Ok = true,
                            Accepted = true,
                            Mode = _runtime.Mode,
                            Message = "Deterministic " + _runtime.Mode + " playtest session initialized.",
                            Sequence = _runtime.Sequence,
                            Snapshot = snapshot,
                            StateDigest = stateDigest,
                            ReplayDigest = ComputeReplayDigest(_runtime),
                            Lab = CreateLabSnapshot(_runtime),
                            Diagnostics = new List<PlaytestDiagnosticDto>(),
                            Command = null
                        });
                }
            }
        }

        /// <summary>
        /// Returns the current presentation snapshot without adding a command
        /// to the deterministic history.
        /// </summary>
        public static string GetSnapshotJson()
        {
            lock (Sync)
            {
                PlaytestSnapshotDto snapshot = CreateSnapshot(_runtime, 0.0);
                return PlaytestProtocolV1.Serialize(snapshot);
            }
        }

        /// <summary>
        /// Validates and dispatches one versioned command envelope. Invalid or
        /// nonconvergent commands return the unchanged live snapshot.
        /// </summary>
        public static string Dispatch(string commandJson)
        {
            lock (Sync)
            {
                if (!TryParseObject(commandJson, out JsonDocument? document, out BridgeDiagnosticDto? parseFailure))
                {
                    return SerializeError("dispatch", parseFailure!, _runtime);
                }

                JsonDocument parsedDocument = document!;
                using (parsedDocument)
                {
                    JsonElement root = parsedDocument.RootElement;
                    if (PlaytestInput.TryGetProperty(root, out JsonElement protocol, "protocol") &&
                        (protocol.ValueKind != JsonValueKind.String ||
                         !string.Equals(
                             protocol.GetString(),
                             PlaytestProtocolV1.ProtocolId,
                             StringComparison.Ordinal)))
                    {
                        return SerializeError(
                            "dispatch",
                            PlaytestProtocolV1.Diagnostic(
                                "Browser.Dispatch.Protocol.Unsupported",
                                "protocol",
                                "The command must use candu-playtest-v1."),
                            _runtime);
                    }

                    JsonElement payload = root;
                    if (PlaytestInput.TryGetProperty(root, out JsonElement envelopePayload, "payload"))
                    {
                        payload = envelopePayload;
                    }

                    if (payload.ValueKind != JsonValueKind.Object ||
                        !PlaytestInput.TryGetProperty(payload, out JsonElement typeValue, "type") ||
                        typeValue.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(typeValue.GetString()))
                    {
                        return SerializeError(
                            "dispatch",
                            PlaytestProtocolV1.Diagnostic(
                                "Browser.Dispatch.Command.Invalid",
                                "payload.type",
                                "A command payload requires a non-empty type string."),
                            _runtime);
                    }

                    string commandType = PlaytestInput.NormalizeType(typeValue.GetString()!);
                    string canonicalCommand = PlaytestProtocolV1.CanonicalizeJson(payload);
                    BridgeCommandExecution execution;
                    PlaytestPreviewDto? preview;
                    LabSnapshotDto? labPreview = null;
                    PlaytestSnapshotDto beforeSnapshot = CreateSnapshot(_runtime, 0.0);
                    if (commandType == "reset")
                    {
                        BridgeRuntime candidate = CreateRuntime(_runtime.Mode, _runtime.InitializationJson);
                        if (candidate.InitializationFailure != null)
                        {
                            execution = BridgeCommandExecution.Failure(candidate.InitializationFailure);
                            preview = null;
                        }
                        else
                        {
                            _runtime = candidate;
                            _runtime.LastEvent = new PlaytestEventDto
                            {
                                EventId = "wasm-event-reset",
                                TimeSeconds = 0.0,
                                Title = "Run reset",
                                Detail = "The deterministic browser session was restored.",
                                Tone = "info"
                            };
                            execution = BridgeCommandExecution.Success("Browser playtest run reset.");
                            preview = null;
                        }
                    }
                    else if (_runtime.Mode == "lab" &&
                        (commandType == "solve" ||
                         commandType == "preview-refuel" ||
                         commandType == "commit-refuel"))
                    {
                        execution = DispatchLab(_runtime, commandType, payload);
                        preview = null;
                        labPreview = execution.PreviewSnapshot as LabSnapshotDto;
                    }
                    else
                    {
                        execution = DispatchPlay(_runtime, commandType, payload, out preview);
                    }

                    _runtime.Sequence = checked(_runtime.Sequence + 1);
                    PlaytestSnapshotDto snapshot = CreateSnapshot(
                        _runtime,
                        commandType == "reset" && execution.Accepted
                            ? _runtime.PlaySession.Snapshot.ScoreTotal
                            : execution.Accepted
                                ? beforeSnapshot.ScoreTotal
                                : 0.0);
                    string stateDigest = ComputeStateDigest(_runtime, snapshot);
                    _runtime.CommandJson.Add(canonicalCommand);
                    _runtime.History.Add(
                        new BridgeHistoryEntry
                        {
                            Sequence = _runtime.Sequence,
                            Type = commandType,
                            CommandJson = canonicalCommand,
                            Accepted = execution.Accepted,
                            DiagnosticCode = execution.Diagnostics.Count == 0
                                ? null
                                : execution.Diagnostics[0].Code,
                            StateDigest = stateDigest
                        });

                    string message = execution.Message;
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        message = execution.Accepted
                            ? "Command accepted."
                            : "Command rejected; authoritative state was unchanged.";
                    }

                    return PlaytestProtocolV1.Serialize(
                        new PlaytestResponseDto
                        {
                            Operation = "dispatch",
                            Ok = execution.Accepted,
                            Accepted = execution.Accepted,
                            Mode = _runtime.Mode,
                            Message = message,
                            Sequence = _runtime.Sequence,
                            Command = payload.Clone(),
                            Snapshot = snapshot,
                            Preview = preview,
                            LabPreview = labPreview,
                            Lab = CreateLabSnapshot(_runtime),
                            StateDigest = stateDigest,
                            ReplayDigest = ComputeReplayDigest(_runtime),
                            Diagnostics = execution.Diagnostics
                                .Select(ToWireDiagnostic)
                                .ToList()
                        });
                }
            }
        }

        /// <summary>
        /// Alias used by the tiny browser host and by hand-written integration
        /// tests. Keeping the export name explicit avoids coupling the web app
        /// to a generated assembly namespace.
        /// </summary>
        public static string DispatchJson(string commandJson)
        {
            return Dispatch(commandJson);
        }

        private static List<string> PlayCommands()
        {
            return new List<string>
            {
                "advance",
                "step",
                "set-playback-mode",
                "pause",
                "resume",
                "queue-power-target",
                "queue-tilt-target",
                "preview-refuel",
                "commit-refuel",
                "reset"
            };
        }

        private static BridgeRuntime CreateRuntime(string mode, string initializationJson)
        {
            GameSession playSession = PracticeGameSessionFactory.CreateBrowserPlaytest();
            LabPlaytestSession? labSession = null;
            BridgeDiagnosticDto? failure = null;
            if (mode == "lab" &&
                !LabPlaytestSession.TryCreate(
                    new LabSolverOptions(),
                    out labSession,
                    out failure))
            {
                labSession = null;
            }

            return new BridgeRuntime(
                mode,
                initializationJson,
                playSession,
                labSession,
                failure);
        }

        private static BridgeCommandExecution DispatchLab(
            BridgeRuntime runtime,
            string commandType,
            JsonElement payload)
        {
            if (runtime.LabSession == null)
            {
                return BridgeCommandExecution.Failure(
                    PlaytestProtocolV1.Diagnostic(
                        "Lab.Session.Unavailable",
                        "mode",
                        "The Lab fixture could not initialize a usable spatial session."));
            }

            if (commandType == "commit-refuel")
            {
                return runtime.LabSession.Dispatch("refuel", payload);
            }

            return runtime.LabSession.Dispatch(commandType, payload);
        }

        private static BridgeCommandExecution DispatchPlay(
            BridgeRuntime runtime,
            string commandType,
            JsonElement payload,
            out PlaytestPreviewDto? preview)
        {
            preview = null;
            GameSession session = runtime.PlaySession;
            switch (commandType)
            {
                case "advance":
                    if (!TryGetUInt64(
                            payload,
                            out ulong wallMilliseconds,
                            "wallMilliseconds",
                            "wall_milliseconds"))
                    {
                        return InvalidCommand(
                            "Browser.Advance.WallMilliseconds.Invalid",
                            "wallMilliseconds",
                            "wallMilliseconds must be a finite nonnegative integer.");
                    }

                    return ToExecution(session.AdvanceWallMilliseconds(wallMilliseconds));

                case "step":
                    if (!TryGetFiniteDouble(
                            payload,
                            out double simulationSeconds,
                            "simulationSeconds",
                            "simulation_seconds") ||
                        simulationSeconds <= 0.0)
                    {
                        return InvalidCommand(
                            "Browser.Step.SimulationSeconds.Invalid",
                            "simulationSeconds",
                            "simulationSeconds must be a finite positive value.");
                    }

                    bool wasPaused = session.Snapshot.IsPaused;
                    if (wasPaused)
                    {
                        session.Resume();
                    }

                    double acceleration = Math.Max(session.Snapshot.AccelerationFactor, 1.0);
                    double wallMillisecondsDouble = simulationSeconds * 1000.0 / acceleration;
                    if (!double.IsFinite(wallMillisecondsDouble) ||
                        wallMillisecondsDouble > ulong.MaxValue)
                    {
                        return InvalidCommand(
                            "Browser.Step.WallMilliseconds.Overflow",
                            "simulationSeconds",
                            "The requested step is outside the browser wall-time range.");
                    }

                    GameSessionCommandResult stepResult = session.AdvanceWallMilliseconds(
                        checked((ulong)Math.Ceiling(wallMillisecondsDouble)));
                    if (wasPaused)
                    {
                        session.Pause();
                    }

                    return ToExecution(stepResult);

                case "set-playback-mode":
                    if (!TryGetString(payload, out string playbackMode, "modeId", "mode_id"))
                    {
                        return InvalidCommand(
                            "Browser.PlaybackMode.Missing",
                            "modeId",
                            "A playback mode is required.");
                    }

                    switch (PlaytestInput.NormalizeMode(playbackMode))
                    {
                        case "pause":
                            return ToExecution(session.Pause());
                        case "1x":
                            return ToExecution(
                                session.SetPlaybackMode(PracticeGameSessionFactory.RealTimePlaybackModeId));
                        case "10x":
                            return ToExecution(
                                session.SetPlaybackMode(PracticeGameSessionFactory.PlayPlaybackModeId));
                        case "60x":
                            return ToExecution(
                                session.SetPlaybackMode(PracticeGameSessionFactory.DebugPlaybackModeId));
                        default:
                            return InvalidCommand(
                                "Browser.PlaybackMode.Unsupported",
                                "modeId",
                                "Use pause, 1x, 10x, or 60x.");
                    }

                case "pause":
                    return ToExecution(session.Pause());

                case "resume":
                    return ToExecution(session.Resume());

                case "queue-power-target":
                    return QueuePowerTarget(session, payload);

                case "queue-tilt-target":
                    return QueueTiltTarget(session, payload);

                case "preview-refuel":
                    return Refuel(session, payload, true, out preview);

                case "commit-refuel":
                    return Refuel(session, payload, false, out preview);

                case "reset":
                    runtime.PlaySession = PracticeGameSessionFactory.CreateBrowserPlaytest();
                    runtime.LastEvent = new PlaytestEventDto
                    {
                        EventId = "wasm-event-reset",
                        TimeSeconds = 0.0,
                        Title = "Run reset",
                        Detail = "The deterministic practice session was restored.",
                        Tone = "info"
                    };
                    return BridgeCommandExecution.Success("Practice run reset.");

                default:
                    return InvalidCommand(
                        "Browser.Command.Unsupported",
                        "type",
                        "The selected command is not supported by the Play session.");
            }
        }

        private static BridgeCommandExecution QueuePowerTarget(
            GameSession session,
            JsonElement payload)
        {
            if (!TryGetFiniteDouble(payload, out double target, "targetFraction", "target_fraction"))
            {
                return InvalidCommand(
                    "Browser.PowerTarget.Invalid",
                    "targetFraction",
                    "targetFraction must be finite.");
            }

            return ToExecution(session.QueuePowerTarget(target));
        }

        private static BridgeCommandExecution QueueTiltTarget(
            GameSession session,
            JsonElement payload)
        {
            if (!TryGetFiniteDouble(payload, out double target, "targetFraction", "target_fraction"))
            {
                return InvalidCommand(
                    "Browser.TiltTarget.Invalid",
                    "targetFraction",
                    "targetFraction must be finite.");
            }

            return ToExecution(session.QueueTiltTarget(target));
        }

        private static BridgeCommandExecution Refuel(
            GameSession session,
            JsonElement payload,
            bool isPreview,
            out PlaytestPreviewDto? preview)
        {
            preview = null;
            JsonElement request = payload;
            if (PlaytestInput.TryGetProperty(payload, out JsonElement nested, "request"))
            {
                request = nested;
            }

            if (!TryGetUInt32(request, out uint channelIndex, "channelIndex", "channel_index") ||
                !TryGetString(request, out string direction, "directionId", "direction_id", "direction") ||
                !TryGetUInt16(request, out ushort shiftCount, "shiftCount", "shift_count") ||
                !TryGetString(request, out string fuelType, "fuelTypeId", "fuel_type_id"))
            {
                return InvalidCommand(
                    "Browser.Refuel.Request.Invalid",
                    "request",
                    "channelIndex, directionId, shiftCount, and fuelTypeId are required.");
            }

            GameSessionSnapshot before = session.Snapshot;
            GameSessionCommandResult result = isPreview
                ? session.PreviewRefuelChannel(channelIndex, direction, shiftCount, fuelType)
                : session.RefuelChannel(channelIndex, direction, shiftCount, fuelType);
            if (isPreview && result.Accepted && result.PreviewCore != null)
            {
                preview = CreatePreview(
                    before,
                    result.PreviewCore,
                    channelIndex,
                    direction,
                    shiftCount,
                    fuelType);
            }

            return ToExecution(result);
        }

        private static PlaytestPreviewDto CreatePreview(
            GameSessionSnapshot before,
            GameCorePresentationSnapshot candidate,
            uint channelIndex,
            string direction,
            ushort shiftCount,
            string fuelType)
        {
            GameChannelPresentationSnapshot source = before.Core.GetChannel(channelIndex);
            GameChannelPresentationSnapshot target = candidate.GetChannel(channelIndex);
            HashSet<string> sourceIds = source.Bundles
                .Select(bundle => bundle.BundleId)
                .ToHashSet(StringComparer.Ordinal);
            HashSet<string> targetIds = target.Bundles
                .Select(bundle => bundle.BundleId)
                .ToHashSet(StringComparer.Ordinal);
            int dischargeStart = string.Equals(
                    direction,
                    TowardEndB,
                    StringComparison.OrdinalIgnoreCase)
                ? checked((int)GameCorePresentationConstants.BundlePositionCount - shiftCount)
                : 0;
            double dischargeBurnup = source.Bundles
                .Where(bundle =>
                    bundle.Position >= dischargeStart &&
                    bundle.Position < dischargeStart + shiftCount)
                .Select(bundle => bundle.CurrentBurnupMwDayPerKg)
                .DefaultIfEmpty()
                .Average();
            double localPowerDelta = target.LocalPowerFraction - source.LocalPowerFraction;
            double localTiltDelta = target.LocalTiltFraction - source.LocalTiltFraction;
            return new PlaytestPreviewDto
            {
                Request = new PlaytestRefuelRequestDto
                {
                    ChannelIndex = channelIndex,
                    DirectionId = PlaytestInput.NormalizeType(direction),
                    ShiftCount = shiftCount,
                    FuelTypeId = fuelType.Trim()
                },
                DischargeBurnupMwdPerKg = dischargeBurnup,
                LocalPowerDeltaFraction = localPowerDelta,
                LocalTiltDeltaFraction = localTiltDelta,
                PredictedReactivityDelta = candidate.Physics.Reactivity - before.Physics.Reactivity,
                ProjectedPowerFraction = before.Physics.ReferencePowerWatts <= 0.0
                    ? before.NormalizedPowerFraction
                    : candidate.Physics.TotalPowerWatts / candidate.Physics.ReferencePowerWatts,
                ProjectedTiltFraction = before.AbsoluteTiltFraction,
                ProjectedScoreDelta = 6.0 + dischargeBurnup - shiftCount * 0.75,
                InsertedBundleIds = target.Bundles
                    .Where(bundle => !sourceIds.Contains(bundle.BundleId))
                    .Select(bundle => bundle.BundleId)
                    .ToList(),
                DischargedBundleIds = source.Bundles
                    .Where(bundle => !targetIds.Contains(bundle.BundleId))
                    .Select(bundle => bundle.BundleId)
                    .ToList()
            };
        }

        private static BridgeCommandExecution ToExecution(GameSessionCommandResult result)
        {
            if (result.Accepted)
            {
                return BridgeCommandExecution.Success(result.Message);
            }

            return BridgeCommandExecution.Failure(
                PlaytestProtocolV1.Diagnostic(
                    result.DiagnosticCode,
                    "command",
                    result.DiagnosticMessage));
        }

        private static BridgeCommandExecution InvalidCommand(
            string code,
            string path,
            string message)
        {
            return BridgeCommandExecution.Failure(
                PlaytestProtocolV1.Diagnostic(code, path, message));
        }

        private static PlaytestSnapshotDto CreateSnapshot(
            BridgeRuntime runtime,
            double previousScore)
        {
            GameSessionSnapshot game = runtime.PlaySession.Snapshot;
            double scoreDelta = game.ScoreTotal - previousScore;
            if (previousScore == 0.0 && runtime.Sequence == 0)
            {
                scoreDelta = 0.0;
            }

            string playback = game.IsPaused
                ? "pause"
                : game.PlaybackModeId == PracticeGameSessionFactory.RealTimePlaybackModeId
                    ? "1x"
                    : game.PlaybackModeId == PracticeGameSessionFactory.DebugPlaybackModeId
                        ? "60x"
                        : "10x";
            LabSnapshotDto? labSnapshot = runtime.LabSession?.CreateSnapshot();
            LabSpatialSolveSnapshotDto? solve = labSnapshot?.SpatialSolve;
            double relativePowerError = game.Physics.TargetPowerWatts <= 0.0
                ? 0.0
                : Math.Abs(game.Physics.TotalPowerWatts - game.Physics.TargetPowerWatts) /
                  game.Physics.TargetPowerWatts;
            PlaytestConvergenceDto convergence = solve == null
                ? new PlaytestConvergenceDto
                {
                    State = game.Physics.SolveState,
                    Iterations = game.Physics.SolverIterationCount,
                    Residual = game.Physics.SolverResidualRelativeInfinity,
                    RelativePowerError = relativePowerError,
                    LastSolveMilliseconds = 0.0,
                    SolverLabel = game.Physics.SolverIdentity
                }
                : new PlaytestConvergenceDto
                {
                    State = solve.HasUsableState ? "converged" : "pending",
                    Iterations = solve.Diagnostics.IterationCount,
                    Residual = solve.Diagnostics.ResidualRelativeInfinity ?? 0.0,
                    RelativePowerError = solve.FinalState == null
                        ? relativePowerError
                        : Math.Abs(solve.FinalState.TotalPowerW - 0.4) / 0.4,
                    LastSolveMilliseconds = 0.0,
                    SolverLabel = "Core SpatialEigenSolve / " + LabPlaytestSession.FixtureId
                };

            return new PlaytestSnapshotDto
            {
                Protocol = PlaytestProtocolV1.ProtocolId,
                Source = "wasm",
                Sequence = runtime.Sequence,
                ScenarioId = game.ScenarioId,
                DataPackId = runtime.Mode == "lab" ? LabDataPackId : DefaultDataPackId,
                SimulationTimeSeconds = game.SimulationTimeSeconds,
                WallElapsedSeconds = game.WallElapsedSeconds,
                NormalizedPowerFraction = game.NormalizedPowerFraction,
                TargetPowerFraction = 1.0,
                AbsoluteTiltFraction = game.AbsoluteTiltFraction,
                TargetTiltFraction = 0.0,
                ControlMarginFraction = game.ControlMarginFraction,
                DeviceAvailableFraction = game.DeviceAvailableFraction,
                PendingActionCount = game.PendingActionCount,
                ScoreTotal = game.ScoreTotal,
                ScoreDelta = scoreDelta,
                IsPaused = game.IsPaused,
                PlaybackModeId = playback,
                FreshBundlesAvailable = game.FreshBundlesAvailable,
                RefuellingOperationCount = game.RefuellingOperationCount,
                LastRefuelledChannel = game.RefuellingOperationCount == 0
                    ? -1
                    : game.LastRefuelledChannel,
                LastRefuellingDirectionId = game.RefuellingOperationCount == 0
                    ? null
                    : game.LastRefuellingDirectionId,
                LastRefuellingShiftCount = game.RefuellingOperationCount == 0
                    ? (ushort)0
                    : game.LastRefuellingShiftCount,
                Physics = new PlaytestPhysicsDto
                {
                    SourceId = game.Physics.SourceId,
                    SolveState = game.Physics.SolveState,
                    IsAuthoritative = game.Physics.IsAuthoritative,
                    BindingVersion = game.Physics.BindingVersion,
                    ReferencePowerWatts = game.Physics.ReferencePowerWatts,
                    PowerAmplitude = game.Physics.PowerAmplitude,
                    ActualPowerFraction = game.Physics.ActualPowerFraction,
                    TargetPowerWatts = game.Physics.TargetPowerWatts,
                    TotalPowerWatts = game.Physics.TotalPowerWatts,
                    MeanChannelPowerWatts = game.Physics.MeanChannelPowerWatts,
                    MeanBundlePowerWatts = game.Physics.MeanBundlePowerWatts,
                    EffectiveK = game.Physics.EffectiveK,
                    Reactivity = game.Physics.Reactivity,
                    PowerBalanceRelativeError = game.Physics.PowerBalanceRelativeError,
                    SolverIdentity = game.Physics.SolverIdentity,
                    SolverIterationCount = game.Physics.SolverIterationCount,
                    SolverResidualRelativeInfinity = game.Physics.SolverResidualRelativeInfinity
                },
                Core = CreateCoreSnapshot(game.Core, game.Physics.MeanBundlePowerWatts),
                Diagnostics = new PlaytestDiagnosticsDto
                {
                    Convergence = convergence,
                    Checks = new List<PlaytestCheckDto>
                    {
                        new PlaytestCheckDto
                        {
                            Label = "Topology",
                            Value = "380 × 12",
                            Status = "pass"
                        },
                        new PlaytestCheckDto
                        {
                            Label = "Control margin",
                            Value = FormatPercent(game.ControlMarginFraction),
                            Status = game.ControlMarginFraction > 0.65 ? "pass" : "watch"
                        },
                        new PlaytestCheckDto
                        {
                            Label = "Data provenance",
                            Value = runtime.Mode == "lab"
                                ? "synthetic Lab fixture"
                                : "synthetic-calibrated full-core pack",
                            Status = "info"
                        }
                    }
                },
                LastEvent = runtime.LastEvent ?? new PlaytestEventDto
                {
                    EventId = "wasm-event-ready",
                    TimeSeconds = game.SimulationTimeSeconds,
                    Title = "Practice session online",
                    Detail = "Select a channel to inspect the 12-position bundle stack.",
                    Tone = "info"
                },
                Lab = labSnapshot
            };
        }

        private static PlaytestCoreDto CreateCoreSnapshot(
            GameCorePresentationSnapshot core,
            double meanBundlePowerWatts)
        {
            return new PlaytestCoreDto
            {
                ChannelCount = GameCorePresentationConstants.ChannelCount,
                BundlePositionCount = GameCorePresentationConstants.BundlePositionCount,
                GridWidth = GameCorePresentationConstants.GridWidth,
                GridHeight = GameCorePresentationConstants.GridHeight,
                Channels = core.Channels
                    .Select(channel => new PlaytestChannelDto
                    {
                        ChannelIndex = channel.ChannelIndex,
                        GridColumn = channel.GridColumn,
                        GridRow = channel.GridRow,
                        FlowDirection = channel.FlowDirection == FlowDirection.EndAtoEndB
                            ? TowardEndB
                            : TowardEndA,
                        AverageBurnupMwdPerKg = channel.AverageBurnupMwDayPerKg,
                        PowerWatts = channel.PowerWatts,
                        LocalPowerFraction = channel.LocalPowerFraction,
                        LocalTiltFraction = channel.LocalTiltFraction,
                        Bundles = channel.Bundles
                            .Select(bundle => new PlaytestBundleDto
                            {
                                Position = bundle.Position,
                                BundleId = bundle.BundleId,
                                FuelTypeId = bundle.FuelTypeId,
                                CurrentBurnupMwdPerKg = bundle.CurrentBurnupMwDayPerKg,
                                PowerWatts = bundle.PowerWatts,
                                LocalPowerFraction = meanBundlePowerWatts <= 0.0
                                    ? 0.0
                                    : bundle.PowerWatts / meanBundlePowerWatts,
                                InsertedAtSeconds = bundle.InsertedAtSeconds,
                                StateVersion = bundle.StateVersion,
                                IsFresh = bundle.IsFresh
                            })
                            .ToList()
                    })
                    .ToList()
            };
        }

        private static LabSnapshotDto? CreateLabSnapshot(BridgeRuntime runtime)
        {
            return runtime.LabSession?.CreateSnapshot();
        }

        private static string ComputeStateDigest(
            BridgeRuntime runtime,
            PlaytestSnapshotDto snapshot)
        {
            string snapshotJson = PlaytestProtocolV1.Serialize(snapshot);
            using JsonDocument document = JsonDocument.Parse(snapshotJson);
            string canonical = PlaytestProtocolV1.CanonicalizeJson(document.RootElement);
            if (runtime.LabSession != null)
            {
                canonical += "|" + runtime.LabSession.ComputeStateDigest();
            }

            return PlaytestProtocolV1.ComputeDigest(canonical);
        }

        private static string ComputeReplayDigest(BridgeRuntime runtime)
        {
            string canonical = runtime.Mode + "|" + runtime.InitializationJson + "|" +
                string.Join("|", runtime.CommandJson);
            return PlaytestProtocolV1.ComputeDigest(canonical);
        }

        private static string SerializeError(
            string operation,
            BridgeDiagnosticDto diagnostic,
            BridgeRuntime runtime)
        {
            PlaytestSnapshotDto snapshot = CreateSnapshot(runtime, 0.0);
            return PlaytestProtocolV1.Serialize(
                new PlaytestResponseDto
                {
                    Operation = operation,
                    Ok = false,
                    Accepted = false,
                    Mode = runtime.Mode,
                    Message = diagnostic.Message,
                    Sequence = runtime.Sequence,
                    Snapshot = snapshot,
                    StateDigest = ComputeStateDigest(runtime, snapshot),
                    ReplayDigest = ComputeReplayDigest(runtime),
                    Diagnostics = new List<PlaytestDiagnosticDto>
                    {
                        ToWireDiagnostic(diagnostic)
                    },
                    Lab = CreateLabSnapshot(runtime),
                    Command = null
                });
        }

        private static PlaytestDiagnosticDto ToWireDiagnostic(BridgeDiagnosticDto diagnostic)
        {
            return new PlaytestDiagnosticDto
            {
                Level = "error",
                Code = diagnostic.Code,
                Message = diagnostic.Message
            };
        }

        private static bool TryParseObject(
            string? json,
            out JsonDocument? document,
            out BridgeDiagnosticDto? failure)
        {
            document = null;
            failure = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                failure = PlaytestProtocolV1.Diagnostic(
                    "Browser.Json.Empty",
                    "json",
                    "A non-empty JSON object is required.");
                return false;
            }

            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException exception)
            {
                failure = PlaytestProtocolV1.Diagnostic(
                    "Browser.Json.Invalid",
                    "json",
                    "The browser command was not valid JSON: " + exception.Message);
                return false;
            }

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                document = null;
                failure = PlaytestProtocolV1.Diagnostic(
                    "Browser.Json.ObjectRequired",
                    "json",
                    "The browser protocol requires a JSON object.");
                return false;
            }

            return true;
        }

        private static bool TryGetString(
            JsonElement value,
            out string result,
            params string[] names)
        {
            result = string.Empty;
            if (!PlaytestInput.TryGetProperty(value, out JsonElement property, names) ||
                property.ValueKind != JsonValueKind.String ||
                property.GetString() == null)
            {
                return false;
            }

            result = property.GetString()!;
            return !string.IsNullOrWhiteSpace(result);
        }

        private static bool TryGetFiniteDouble(
            JsonElement value,
            out double result,
            params string[] names)
        {
            result = 0.0;
            if (!PlaytestInput.TryGetProperty(value, out JsonElement property, names) ||
                !property.TryGetDouble(out result))
            {
                return false;
            }

            return double.IsFinite(result);
        }

        private static bool TryGetUInt16(
            JsonElement value,
            out ushort result,
            params string[] names)
        {
            result = 0;
            return PlaytestInput.TryGetProperty(value, out JsonElement property, names) &&
                property.TryGetUInt16(out result);
        }

        private static bool TryGetUInt32(
            JsonElement value,
            out uint result,
            params string[] names)
        {
            result = 0;
            return PlaytestInput.TryGetProperty(value, out JsonElement property, names) &&
                property.TryGetUInt32(out result);
        }

        private static bool TryGetUInt64(
            JsonElement value,
            out ulong result,
            params string[] names)
        {
            result = 0;
            return PlaytestInput.TryGetProperty(value, out JsonElement property, names) &&
                property.TryGetUInt64(out result);
        }

        private static string FormatPercent(double value)
        {
            return (value * 100.0).ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private sealed class BridgeRuntime
        {
            public BridgeRuntime(
                string mode,
                string initializationJson,
                GameSession playSession,
                LabPlaytestSession? labSession,
                BridgeDiagnosticDto? initializationFailure)
            {
                Mode = mode;
                InitializationJson = initializationJson;
                PlaySession = playSession;
                LabSession = labSession;
                InitializationFailure = initializationFailure;
            }

            public string Mode { get; }

            public string InitializationJson { get; }

            public GameSession PlaySession { get; set; }

            public LabPlaytestSession? LabSession { get; }

            public BridgeDiagnosticDto? InitializationFailure { get; }

            public ulong Sequence { get; set; }

            public double LastScore { get; set; }

            public List<string> CommandJson { get; } = new List<string>();

            public List<BridgeHistoryEntry> History { get; } = new List<BridgeHistoryEntry>();

            public PlaytestEventDto? LastEvent { get; set; }
        }
    }

    internal sealed class PlaytestResponseDto
    {
        public string Protocol { get; set; } = PlaytestProtocolV1.ProtocolId;

        public uint SchemaVersion { get; set; } = PlaytestProtocolV1.SchemaVersion;

        public string Operation { get; set; } = string.Empty;

        public bool Ok { get; set; }

        public bool Accepted { get; set; }

        public string Mode { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public ulong Sequence { get; set; }

        public JsonElement? Command { get; set; }

        public PlaytestSnapshotDto Snapshot { get; set; } = new PlaytestSnapshotDto();

        public PlaytestPreviewDto? Preview { get; set; }

        public LabSnapshotDto? LabPreview { get; set; }

        public LabSnapshotDto? Lab { get; set; }

        public string StateDigest { get; set; } = string.Empty;

        public string ReplayDigest { get; set; } = string.Empty;

        public List<PlaytestDiagnosticDto> Diagnostics { get; set; } =
            new List<PlaytestDiagnosticDto>();
    }

    internal sealed class PlaytestDiagnosticDto
    {
        public string Level { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }

    internal sealed class PlaytestSnapshotDto
    {
        public string Protocol { get; set; } = PlaytestProtocolV1.ProtocolId;

        public string Source { get; set; } = "wasm";

        public ulong Sequence { get; set; }

        public string ScenarioId { get; set; } = string.Empty;

        public string DataPackId { get; set; } = string.Empty;

        public double SimulationTimeSeconds { get; set; }

        public double WallElapsedSeconds { get; set; }

        public double NormalizedPowerFraction { get; set; }

        public double TargetPowerFraction { get; set; }

        public double AbsoluteTiltFraction { get; set; }

        public double TargetTiltFraction { get; set; }

        public double ControlMarginFraction { get; set; }

        public double DeviceAvailableFraction { get; set; }

        public uint PendingActionCount { get; set; }

        public double ScoreTotal { get; set; }

        public double ScoreDelta { get; set; }

        public bool IsPaused { get; set; }

        public string PlaybackModeId { get; set; } = "1x";

        public uint FreshBundlesAvailable { get; set; }

        public uint RefuellingOperationCount { get; set; }

        public int LastRefuelledChannel { get; set; } = -1;

        public string? LastRefuellingDirectionId { get; set; }

        public ushort LastRefuellingShiftCount { get; set; }

        public PlaytestCoreDto Core { get; set; } = new PlaytestCoreDto();

        public PlaytestPhysicsDto Physics { get; set; } = new PlaytestPhysicsDto();

        public PlaytestDiagnosticsDto Diagnostics { get; set; } = new PlaytestDiagnosticsDto();

        public PlaytestEventDto? LastEvent { get; set; }

        public LabSnapshotDto? Lab { get; set; }
    }

    internal sealed class PlaytestCoreDto
    {
        public uint ChannelCount { get; set; }

        public uint BundlePositionCount { get; set; }

        public int GridWidth { get; set; }

        public int GridHeight { get; set; }

        public List<PlaytestChannelDto> Channels { get; set; } = new List<PlaytestChannelDto>();
    }

    internal sealed class PlaytestChannelDto
    {
        public uint ChannelIndex { get; set; }

        public int GridColumn { get; set; }

        public int GridRow { get; set; }

        public string FlowDirection { get; set; } = string.Empty;

        public double AverageBurnupMwdPerKg { get; set; }

        public double PowerWatts { get; set; }

        public double LocalPowerFraction { get; set; }

        public double LocalTiltFraction { get; set; }

        public List<PlaytestBundleDto> Bundles { get; set; } = new List<PlaytestBundleDto>();
    }

    internal sealed class PlaytestBundleDto
    {
        public uint Position { get; set; }

        public string BundleId { get; set; } = string.Empty;

        public string FuelTypeId { get; set; } = string.Empty;

        public double CurrentBurnupMwdPerKg { get; set; }

        public double PowerWatts { get; set; }

        public double LocalPowerFraction { get; set; }

        public double InsertedAtSeconds { get; set; }

        public ulong StateVersion { get; set; }

        public bool IsFresh { get; set; }
    }

    internal sealed class PlaytestDiagnosticsDto
    {
        public PlaytestConvergenceDto Convergence { get; set; } = new PlaytestConvergenceDto();

        public List<PlaytestCheckDto> Checks { get; set; } = new List<PlaytestCheckDto>();
    }

    internal sealed class PlaytestConvergenceDto
    {
        public string State { get; set; } = "unavailable";

        public int Iterations { get; set; }

        public double Residual { get; set; }

        public double RelativePowerError { get; set; }

        public double LastSolveMilliseconds { get; set; }

        public string SolverLabel { get; set; } = string.Empty;
    }

    internal sealed class PlaytestPhysicsDto
    {
        public string SourceId { get; set; } = string.Empty;

        public string SolveState { get; set; } = string.Empty;

        public bool IsAuthoritative { get; set; }

        public ulong BindingVersion { get; set; }

        public double ReferencePowerWatts { get; set; }

        public double PowerAmplitude { get; set; }

        public double ActualPowerFraction { get; set; }

        public double TargetPowerWatts { get; set; }

        public double TotalPowerWatts { get; set; }

        public double MeanChannelPowerWatts { get; set; }

        public double MeanBundlePowerWatts { get; set; }

        public double EffectiveK { get; set; }

        public double Reactivity { get; set; }

        public double PowerBalanceRelativeError { get; set; }

        public string SolverIdentity { get; set; } = string.Empty;

        public int SolverIterationCount { get; set; }

        public double SolverResidualRelativeInfinity { get; set; }
    }

    internal sealed class PlaytestCheckDto
    {
        public string Label { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public string Status { get; set; } = "info";
    }

    internal sealed class PlaytestEventDto
    {
        public string EventId { get; set; } = string.Empty;

        public double TimeSeconds { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;

        public string Tone { get; set; } = "info";
    }

    internal sealed class PlaytestRefuelRequestDto
    {
        public uint ChannelIndex { get; set; }

        public string DirectionId { get; set; } = string.Empty;

        public ushort ShiftCount { get; set; }

        public string FuelTypeId { get; set; } = string.Empty;
    }

    internal sealed class PlaytestPreviewDto
    {
        public PlaytestRefuelRequestDto Request { get; set; } = new PlaytestRefuelRequestDto();

        public double DischargeBurnupMwdPerKg { get; set; }

        public double LocalPowerDeltaFraction { get; set; }

        public double LocalTiltDeltaFraction { get; set; }

        public double PredictedReactivityDelta { get; set; }

        public double ProjectedPowerFraction { get; set; }

        public double ProjectedTiltFraction { get; set; }

        public double ProjectedScoreDelta { get; set; }

        public List<string> InsertedBundleIds { get; set; } = new List<string>();

        public List<string> DischargedBundleIds { get; set; } = new List<string>();
    }
}
