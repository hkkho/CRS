using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReactorSim.Core;
using ReactorSim.Game;

[assembly: InternalsVisibleTo("ReactorSim.Browser.Tests")]

namespace ReactorSim.Browser
{
    public sealed partial class PlaytestRuntime
    {
        private const string DefaultMode = "play";
        private const string DefaultDataPackId =
            PracticeGameSessionFactory.DiffusionDataPackVersion;
        private const string TowardEndA = "toward-end-a";
        private const string TowardEndB = "toward-end-b";

        private readonly object Sync = new object();
        private BridgeRuntime? _runtimeInstance;
#if RESEARCH_EXPERIMENTS
        private GpuCoupledSpatialFixtureV1? _coupledExperiment;
        private BridgeRuntime? _coupledExperimentRuntime;
        private string _coupledExperimentId = "";
#endif
        private int _coreSnapshotMaterializationCount;

        internal int CoreSnapshotMaterializationCount
        {
            get { return Volatile.Read(ref _coreSnapshotMaterializationCount); }
        }

        internal void ResetCoreSnapshotMaterializationCount()
        {
            Interlocked.Exchange(ref _coreSnapshotMaterializationCount, 0);
        }

        // Do not construct the full browser session as a type initializer.
        // A failure there is surfaced by the WASM runtime only as the generic
        // TypeInitialization_Type error, which hides the actionable cause from
        // the browser console. Lazy construction keeps capability discovery
        // available and preserves the original exception at the JSON boundary.
        private BridgeRuntime _runtime
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
                    }
                }
            };

            return PlaytestProtocolV2.Serialize(capabilities);
        }

        /// <summary>
        /// Creates a deterministic full-core session. The bridge exposes only
        /// the Play mode; unknown modes are rejected without replacing the
        /// current session.
        /// </summary>
        public string Initialize(string requestJson)
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
                    JsonElement root = parsedDocument.RootElement;
                    if (!PlaytestInput.TryGetProperty(root, out JsonElement protocol, "protocol") ||
                        protocol.ValueKind != JsonValueKind.String ||
                        !string.Equals(
                            protocol.GetString(),
                            PlaytestProtocolV2.ProtocolId,
                            StringComparison.Ordinal))
                    {
                        return SerializeError(
                            "initialize",
                            PlaytestProtocolV2.Diagnostic(
                                "Browser.Initialize.Protocol.Unsupported",
                                "protocol",
                                "Initialization must use candu-playtest-v2."),
                            _runtime);
                    }

                    string mode = DefaultMode;
                    if (PlaytestInput.TryGetProperty(
                            root,
                            out JsonElement modeValue,
                            "mode"))
                    {
                        if (modeValue.ValueKind != JsonValueKind.String ||
                            string.IsNullOrWhiteSpace(modeValue.GetString()))
                        {
                            return SerializeError(
                                "initialize",
                                PlaytestProtocolV2.Diagnostic(
                                    "Browser.Initialize.Mode.Invalid",
                                    "mode",
                                    "mode must be the string play."),
                                _runtime);
                        }

                        mode = PlaytestInput.NormalizeMode(modeValue.GetString()!);
                    }

                    if (mode != DefaultMode)
                    {
                        return SerializeError(
                            "initialize",
                            PlaytestProtocolV2.Diagnostic(
                                "Browser.Initialize.Mode.Unsupported",
                                "mode",
                                "The browser bridge supports only play mode."),
                            _runtime);
                    }

                    if (!TryReadSeed(root, PlaytestProtocolV2.PracticeSeed, out ulong seed))
                    {
                        return SerializeError("initialize", PlaytestProtocolV2.Diagnostic(
                            "Browser.Seed.Invalid", "seed", "seed must be an integer from 0 to 4294967295."), _runtime);
                    }
                    if (!TryReadShiftId(root, ShiftProgress.PracticeId, out string shiftId))
                        return SerializeError("initialize", PlaytestProtocolV2.Diagnostic(
                            "Browser.Shift.Invalid", "shiftId", "Unknown shift objective."), _runtime);
                    BridgeRuntime candidate = CreateRuntime(
                        mode,
                        requestJson ?? "{}", seed, shiftId);
                    _runtime = candidate;
                    GameSessionSnapshot game = _runtime.PlaySession.Snapshot;
                    PlaytestSnapshotDto snapshot = CreateSnapshot(_runtime, game, 0.0);
                    CacheGameSnapshot(_runtime, game);
                    string stateDigest = ComputeStateDigest(_runtime, snapshot);
                    return PlaytestProtocolV2.Serialize(
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
        public string GetSnapshotJson()
        {
            lock (Sync)
            {
                GameSessionSnapshot game = GetCurrentGameSnapshot(_runtime);
                PlaytestSnapshotDto snapshot = CreateSnapshot(_runtime, game, 0.0);
                return PlaytestProtocolV2.Serialize(snapshot);
            }
        }

        /// <summary>
        /// Validates and dispatches one versioned command envelope. Invalid or
        /// nonconvergent commands return the unchanged live snapshot.
        /// </summary>
        public string Dispatch(string commandJson)
        {
#if RUNTIME_PROFILE
            using var profileScope = ReactorSim.Core.RuntimeProfile.Measure("bridge-dispatch");
#endif
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
                    if (!PlaytestInput.TryGetProperty(root, out JsonElement protocol, "protocol") ||
                        protocol.ValueKind != JsonValueKind.String ||
                        !string.Equals(
                            protocol.GetString(),
                            PlaytestProtocolV2.ProtocolId,
                            StringComparison.Ordinal))
                    {
                        return SerializeError(
                            "dispatch",
                            PlaytestProtocolV2.Diagnostic(
                                "Browser.Dispatch.Protocol.Unsupported",
                                "protocol",
                                "The command must use candu-playtest-v2."),
                            _runtime);
                    }

                    JsonElement payload = root;
                    if (PlaytestInput.TryGetProperty(root, out JsonElement envelopePayload, "payload"))
                    {
                        payload = envelopePayload;
                    }

                    bool hasBaseSequence = PlaytestInput.TryGetProperty(
                        root,
                        out JsonElement baseSequenceValue,
                        "baseSequence",
                        "base_sequence");
                    ulong? requestedBaseSequence = null;
                    if (hasBaseSequence)
                    {
                        if (baseSequenceValue.ValueKind != JsonValueKind.Number ||
                            !baseSequenceValue.TryGetUInt64(out ulong parsedBaseSequence))
                        {
                            return SerializeError(
                                "dispatch",
                                PlaytestProtocolV2.Diagnostic(
                                    "Browser.Dispatch.BaseSequence.Invalid",
                                    "baseSequence",
                                    "baseSequence must be a nonnegative integer."),
                                _runtime);
                        }

                        requestedBaseSequence = parsedBaseSequence;
                    }

                    if (payload.ValueKind != JsonValueKind.Object ||
                        !PlaytestInput.TryGetProperty(payload, out JsonElement typeValue, "type") ||
                        typeValue.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(typeValue.GetString()))
                    {
                        return SerializeError(
                            "dispatch",
                            PlaytestProtocolV2.Diagnostic(
                                "Browser.Dispatch.Command.Invalid",
                                "payload.type",
                                "A command payload requires a non-empty type string."),
                            _runtime);
                    }

                    string commandType = PlaytestInput.NormalizeType(typeValue.GetString()!);
                    string canonicalCommand = PlaytestProtocolV2.CanonicalizeJson(payload);
                    // Engineering commands update the live full-core session.
                    // Keep those responses materialized even when the caller
                    // asks for compact play responses; ordinary game commands
                    // retain the existing compact transport path.
                    bool engineeringCommand = IsEngineeringCommand(commandType);
                    bool compactRequested = IsCompactResponseRequested(root) &&
                        _runtime.Mode == DefaultMode &&
                        !engineeringCommand;

                    if (compactRequested &&
                        commandType != "reset" &&
                        requestedBaseSequence.HasValue &&
                        requestedBaseSequence.Value != _runtime.Sequence)
                    {
                        return SerializeCompactResync(
                            payload,
                            requestedBaseSequence.Value,
                            _runtime);
                    }

                    BridgeCommandExecution execution;
                    double previousScore = _runtime.LastScore;
                    var previousGame = GetCurrentGameSnapshot(_runtime);
                    double previousAmplitude = previousGame.Physics.PowerAmplitude;
                    object? previousDetailedProjection = _runtime.LastDetailedProjection;
                    if (commandType == "reset")
                    {
                        if (!TryReadSeed(payload, _runtime.PlaySession.Snapshot.Seed, out ulong resetSeed))
                            return SerializeError("dispatch", PlaytestProtocolV2.Diagnostic(
                                "Browser.Seed.Invalid", "seed", "seed must be an integer from 0 to 4294967295."), _runtime);
                        if (!TryReadShiftId(payload, _runtime.PlaySession.Snapshot.Shift.Id, out string shiftId))
                            return SerializeError("dispatch", PlaytestProtocolV2.Diagnostic(
                                "Browser.Shift.Invalid", "shiftId", "Unknown shift objective."), _runtime);
                        string resetInitialization = "{\"protocol\":\"candu-playtest-v2\",\"mode\":\"play\",\"seed\":" +
                            resetSeed.ToString(CultureInfo.InvariantCulture) + ",\"shiftId\":\"" + shiftId + "\"}";
                        BridgeRuntime candidate = CreateRuntime(
                            _runtime.Mode,
                            resetInitialization, resetSeed, shiftId);
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
                    }
                    else if (IsEngineeringCommand(commandType))
                    {
                        execution = DispatchEngineering(_runtime, commandType, payload);
                    }
                    else
                    {
                        execution = DispatchPlay(_runtime, commandType, payload);
                    }

                    _runtime.Sequence = checked(_runtime.Sequence + 1);
                    GameSessionSnapshot game = execution.Snapshot ?? GetCurrentGameSnapshot(_runtime);
                    bool detailedProjectionChanged =
                        previousDetailedProjection != null &&
                        !ReferenceEquals(
                            previousDetailedProjection,
                            _runtime.PlaySession.CurrentSpatialCandidate);
                    double previousScoreForResponse = commandType == "reset" && execution.Accepted
                        ? game.ScoreTotal
                        : execution.Accepted
                            ? previousScore
                            : 0.0;
                    bool returnCompact = compactRequested &&
                        commandType != "reset" &&
                        _runtime.Mode == DefaultMode;
                    PlaytestSnapshotDto? snapshot = null;
                    PlaytestSnapshotPatchDto? snapshotPatch = null;
                    PlaytestCoreDto? coreReplacement = null;
                    PlaytestCoreMeasurementsDto? coreMeasurements = null;
                    string stateDigest;
                    if (returnCompact)
                    {
                        snapshotPatch = CreateSnapshotPatch(
                            _runtime,
                            game,
                            previousScoreForResponse);
                        if (execution.Accepted &&
                            (commandType == "commit-refuel" || detailedProjectionChanged || previousAmplitude != game.Physics.PowerAmplitude))
                        {
                            coreReplacement = CreateCoreSnapshot(
                                _runtime,
                                game.Core,
                                game.Physics.MeanBundlePowerWatts);
                        }

                        if (execution.Accepted && coreReplacement == null && game.SimulationTimeSeconds != previousGame.SimulationTimeSeconds)
                            coreMeasurements = new PlaytestCoreMeasurementsDto
                            {
                                BundleBurnupMwdPerKg = game.Core.Channels.SelectMany(c => c.Bundles).Select(b => b.CurrentBurnupMwDayPerKg).ToArray(),
                                BundleStateVersions = game.Core.Channels.SelectMany(c => c.Bundles).Select(b => b.StateVersion).ToArray(),
                                BundleIsFresh = game.Core.Channels.SelectMany(c => c.Bundles).Select(b => b.IsFresh).ToArray(),
                                ChannelAverageBurnupMwdPerKg = game.Core.Channels.Select(c => c.AverageBurnupMwDayPerKg).ToArray()
                            };

                        stateDigest = ComputeCompactStateDigest(
                            _runtime,
                            game,
                            snapshotPatch);
                    }
                    else
                    {
                        snapshot = CreateSnapshot(
                            _runtime,
                            game,
                            previousScoreForResponse);
                        stateDigest = ComputeStateDigest(_runtime, snapshot);
                    }
                    CacheGameSnapshot(_runtime, game);
                    _runtime.Replay.Append(canonicalCommand);

                    string message = execution.Message;
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        message = execution.Accepted
                            ? "Command accepted."
                            : "Command rejected; authoritative state was unchanged.";
                    }

                    if (!returnCompact)
                    {
                        return PlaytestProtocolV2.Serialize(
                            new PlaytestResponseDto
                            {
                                Operation = "dispatch",
                                Ok = execution.Accepted,
                                Accepted = execution.Accepted,
                                Mode = _runtime.Mode,
                                Message = message,
                                Sequence = _runtime.Sequence,
                                Command = payload.Clone(),
                                Snapshot = snapshot!,
                                StateDigest = stateDigest,
                                ReplayDigest = ComputeReplayDigest(_runtime),
                                Diagnostics = execution.Diagnostics
                                    .Select(ToWireDiagnostic)
                                    .ToList()
                            });
                    }

                    return PlaytestProtocolV2.Serialize(
                        new PlaytestResponseDto
                        {
                            Operation = "dispatch",
                            Ok = execution.Accepted,
                            Accepted = execution.Accepted,
                            Mode = _runtime.Mode,
                            Message = message,
                            Sequence = _runtime.Sequence,
                            ResponseKind = "compact",
                            BaseSequence = requestedBaseSequence ?? _runtime.Sequence - 1UL,
                            RequiresResync = false,
                            Command = payload.Clone(),
                            SnapshotPatch = snapshotPatch!,
                            CoreReplacement = coreReplacement,
                            CoreMeasurements = coreMeasurements,
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
        public string DispatchJson(string commandJson)
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
                "commit-refuel",
                "configure-cell",
                "configure-zone-layout",
                "solve",
                "reset"
            };
        }

        /// <summary>Developer-only timing envelope; the command response and its
        /// deterministic digests are identical to Dispatch. Not a game command.</summary>
        private static BridgeRuntime CreateRuntime(
            string mode,
            string initializationJson,
            ulong seed = PlaytestProtocolV2.PracticeSeed,
            string shiftId = ShiftProgress.PracticeId)
        {
            return new BridgeRuntime(
                mode,
                initializationJson,
                PracticeGameSessionFactory.CreateBrowserPlaytest(seed, shiftId == ShiftProgress.ChallengeId));
        }

        private static bool TryReadShiftId(JsonElement payload, string fallback, out string shiftId)
        {
            shiftId = fallback;
            if (!PlaytestInput.TryGetProperty(payload, out JsonElement value, "shiftId")) return true;
            if (value.ValueKind != JsonValueKind.String) return false;
            shiftId = value.GetString()!;
            return shiftId == ShiftProgress.PracticeId || shiftId == ShiftProgress.ChallengeId;
        }

        private static bool TryReadSeed(JsonElement payload, ulong fallback, out ulong seed)
        {
            seed = fallback;
            if (!PlaytestInput.TryGetProperty(payload, out JsonElement value, "seed")) return true;
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetUInt32(out uint supplied)) return false;
            seed = supplied;
            return true;
        }

        private static BridgeCommandExecution DispatchEngineering(
            BridgeRuntime runtime,
            string commandType,
            JsonElement payload)
        {
            if (commandType == "configure-zone-layout")
            {
                if (!PlaytestInput.TryGetProperty(payload, out JsonElement nodes, "nodes") ||
                    nodes.ValueKind != JsonValueKind.Array || nodes.GetArrayLength() != 4560)
                    return InvalidCommand("Browser.ZoneLayout.Nodes.Invalid", "nodes", "Supply exactly 4560 zone node bindings.");
                var bindings = new List<PracticeLiquidZoneRrsNodeBindingV1>();
                foreach (JsonElement node in nodes.EnumerateArray())
                {
                    if (!TryGetUInt32(node, out uint channel, "channelIndex") || channel >= 380 ||
                        !TryGetUInt32(node, out uint position, "position") || position >= 12 ||
                        !TryGetUInt32(node, out uint zone, "logicalZoneId") || zone >= 14 ||
                        !TryGetUInt32(node, out uint absorberZone, "absorberZoneId") || absorberZone >= 14 ||
                        !PlaytestInput.TryGetProperty(node, out JsonElement fast, "group1AbsorptionPerMPerFillFraction") ||
                        fast.ValueKind != JsonValueKind.Number || !fast.TryGetDouble(out double a1) || double.IsNaN(a1) || double.IsInfinity(a1) || a1 < 0 || a1 > 1 ||
                        !PlaytestInput.TryGetProperty(node, out JsonElement thermal, "group2AbsorptionPerMPerFillFraction") ||
                        thermal.ValueKind != JsonValueKind.Number || !thermal.TryGetDouble(out double a2) || double.IsNaN(a2) || double.IsInfinity(a2) || a2 < 0 || a2 > 1)
                        return InvalidCommand("Browser.ZoneLayout.Binding.Invalid", "nodes", "Invalid node, zone or absorption slope (allowed range 0–1 m^-1 per unit fill).");
                    bindings.Add(new PracticeLiquidZoneRrsNodeBindingV1(
                        new NodeKey(new ChannelId(channel), new BundlePosition(position)), zone,
                        a1 == 0 ? 0 : a1, a2 == 0 ? 0 : a2, absorberZone));
                }
                return ToExecution(runtime.PlaySession.ConfigureZoneLayout(bindings));
            }
            if (commandType == "configure-cell")
            {
                if (!TryReadConfiguredCell(
                        payload,
                        out uint channelIndex,
                        out uint position,
                        out bool hasFuel,
                        out IReadOnlyCollection<TopologyFace> reflectiveFaces,
                        out BridgeDiagnosticDto? diagnostic))
                {
                    return BridgeCommandExecution.Failure(diagnostic!);
                }

                return ToExecution(
                    runtime.PlaySession.ConfigureCell(
                        channelIndex,
                        position,
                        hasFuel,
                        reflectiveFaces));
            }

            if (commandType == "solve")
            {
                return ToExecution(runtime.PlaySession.SolveConfiguredCore());
            }

            return InvalidCommand(
                "Browser.Command.Unsupported",
                "type",
                "The selected engineering command is not supported by the live Play session.");
        }

        private static bool TryReadConfiguredCell(
            JsonElement payload,
            out uint channelIndex,
            out uint position,
            out bool hasFuel,
            out IReadOnlyCollection<TopologyFace> reflectiveFaces,
            out BridgeDiagnosticDto? diagnostic)
        {
            channelIndex = 0;
            position = 0;
            hasFuel = false;
            reflectiveFaces = Array.Empty<TopologyFace>();
            diagnostic = null;

            if (!TryGetUInt32(payload, out channelIndex, "channelIndex", "channel_index"))
            {
                diagnostic = PlaytestProtocolV2.Diagnostic(
                    "Browser.ConfigureCell.ChannelIndex.Invalid",
                    "channelIndex",
                    "channelIndex must be a nonnegative integer.");
                return false;
            }

            if (!TryGetUInt32(payload, out position, "position"))
            {
                diagnostic = PlaytestProtocolV2.Diagnostic(
                    "Browser.ConfigureCell.Position.Invalid",
                    "position",
                    "position must be a nonnegative integer.");
                return false;
            }

            if (!PlaytestInput.TryGetProperty(payload, out JsonElement fuel, "hasFuel", "has_fuel") ||
                (fuel.ValueKind != JsonValueKind.True && fuel.ValueKind != JsonValueKind.False))
            {
                diagnostic = PlaytestProtocolV2.Diagnostic(
                    "Browser.ConfigureCell.HasFuel.Invalid",
                    "hasFuel",
                    "hasFuel must be a JSON boolean.");
                return false;
            }

            hasFuel = fuel.GetBoolean();
            if (!PlaytestInput.TryGetProperty(
                    payload,
                    out JsonElement faces,
                    "reflectiveFaces",
                    "reflective_faces") ||
                faces.ValueKind != JsonValueKind.Array)
            {
                diagnostic = PlaytestProtocolV2.Diagnostic(
                    "Browser.ConfigureCell.ReflectiveFaces.Invalid",
                    "reflectiveFaces",
                    "reflectiveFaces must be an array of face strings.");
                return false;
            }

            var parsedFaces = new List<TopologyFace>();
            foreach (JsonElement faceValue in faces.EnumerateArray())
            {
                if (faceValue.ValueKind != JsonValueKind.String ||
                    !TryParseTopologyFace(faceValue.GetString(), out TopologyFace face))
                {
                    diagnostic = PlaytestProtocolV2.Diagnostic(
                        "Browser.ConfigureCell.ReflectiveFaces.Invalid",
                        "reflectiveFaces",
                        "Each reflective face must be one of north, east, south, west, end-a, or end-b.");
                    return false;
                }

                if (parsedFaces.Contains(face))
                {
                    diagnostic = PlaytestProtocolV2.Diagnostic(
                        "Browser.ConfigureCell.ReflectiveFaces.Duplicate",
                        "reflectiveFaces",
                        "A reflective face may be listed only once.");
                    return false;
                }

                parsedFaces.Add(face);
            }

            reflectiveFaces = parsedFaces;
            return true;
        }

        private static bool TryParseTopologyFace(
            string? value,
            out TopologyFace face)
        {
            face = default(TopologyFace);
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = PlaytestInput.NormalizeType(value);
            switch (normalized)
            {
                case "north":
                    face = TopologyFace.North;
                    return true;
                case "east":
                    face = TopologyFace.East;
                    return true;
                case "south":
                    face = TopologyFace.South;
                    return true;
                case "west":
                    face = TopologyFace.West;
                    return true;
                case "end-a":
                case "enda":
                    face = TopologyFace.EndA;
                    return true;
                case "end-b":
                case "endb":
                    face = TopologyFace.EndB;
                    return true;
                default:
                    return false;
            }
        }

        private static string TopologyFaceId(TopologyFace face)
        {
            switch (face)
            {
                case TopologyFace.North:
                    return "north";
                case TopologyFace.East:
                    return "east";
                case TopologyFace.South:
                    return "south";
                case TopologyFace.West:
                    return "west";
                case TopologyFace.EndA:
                    return "end-a";
                case TopologyFace.EndB:
                    return "end-b";
                default:
                    throw new ArgumentOutOfRangeException(nameof(face));
            }
        }

        private static bool IsEngineeringCommand(string commandType)
        {
            return commandType == "configure-cell" ||
                commandType == "configure-zone-layout" ||
                commandType == "solve";
        }

        private static BridgeCommandExecution DispatchPlay(
            BridgeRuntime runtime,
            string commandType,
            JsonElement payload)
        {
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

                    GameSessionSnapshot beforeStep = GetCurrentGameSnapshot(runtime);
                    bool wasPaused = beforeStep.IsPaused;
                    if (wasPaused)
                    {
                        session.Resume();
                    }

                    double acceleration = Math.Max(beforeStep.AccelerationFactor, 1.0);
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
                        GameSessionCommandResult pauseResult = session.Pause();
                        return WithSnapshot(ToExecution(stepResult), pauseResult.Snapshot);
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

                case "commit-refuel":
                    return Refuel(runtime, payload);

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

        private static BridgeCommandExecution Refuel(
            BridgeRuntime runtime,
            JsonElement payload)
        {
            GameSession session = runtime.PlaySession;
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

            return ToExecution(
                session.RefuelChannel(channelIndex, direction, shiftCount, fuelType));
        }

        private static BridgeCommandExecution ToExecution(GameSessionCommandResult result)
        {
            if (result.Accepted)
            {
                return BridgeCommandExecution.Success(
                    result.Message,
                    snapshot: result.Snapshot);
            }

            return BridgeCommandExecution.Failure(
                PlaytestProtocolV2.Diagnostic(
                    result.DiagnosticCode,
                    "command",
                    result.DiagnosticMessage),
                result.Snapshot);
        }

        private static BridgeCommandExecution WithSnapshot(
            BridgeCommandExecution execution,
            GameSessionSnapshot snapshot)
        {
            return new BridgeCommandExecution
            {
                Accepted = execution.Accepted,
                Message = execution.Message,
                Diagnostics = execution.Diagnostics,
                SpatialSolve = execution.SpatialSolve,
                Snapshot = snapshot
            };
        }

        private static BridgeCommandExecution InvalidCommand(
            string code,
            string path,
            string message)
        {
            return BridgeCommandExecution.Failure(
                PlaytestProtocolV2.Diagnostic(code, path, message));
        }

        private static GameSessionSnapshot GetCurrentGameSnapshot(BridgeRuntime runtime)
        {
            if (runtime.LastGameSnapshot != null)
            {
                return runtime.LastGameSnapshot;
            }

            GameSessionSnapshot snapshot = runtime.PlaySession.Snapshot;
            CacheGameSnapshot(runtime, snapshot);
            return snapshot;
        }

        private static void CacheGameSnapshot(
            BridgeRuntime runtime,
            GameSessionSnapshot snapshot)
        {
            runtime.LastGameSnapshot = snapshot;
            runtime.LastScore = snapshot.ScoreTotal;
            runtime.LastDetailedProjection = runtime.PlaySession.CurrentSpatialCandidate;
        }

        private static bool IsCompactResponseRequested(JsonElement root)
        {
            if (TryGetResponseMode(root, out string responseMode) &&
                string.Equals(
                    PlaytestInput.NormalizeType(responseMode),
                    "compact",
                    StringComparison.Ordinal))
            {
                return true;
            }

            if (PlaytestInput.TryGetProperty(root, out JsonElement compact, "compact") &&
                compact.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (PlaytestInput.TryGetProperty(root, out JsonElement options, "options") &&
                options.ValueKind == JsonValueKind.Object &&
                TryGetResponseMode(options, out responseMode) &&
                string.Equals(
                    PlaytestInput.NormalizeType(responseMode),
                    "compact",
                    StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        private static bool TryGetResponseMode(JsonElement value, out string responseMode)
        {
            responseMode = string.Empty;
            if (!PlaytestInput.TryGetProperty(
                    value,
                    out JsonElement mode,
                    "responseMode",
                    "response_mode",
                    "response") ||
                mode.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(mode.GetString()))
            {
                return false;
            }

            responseMode = mode.GetString()!;
            return true;
        }

        private string SerializeCompactResync(
            JsonElement payload,
            ulong requestedBaseSequence,
            BridgeRuntime runtime)
        {
            PlaytestSnapshotDto snapshot = CreateSnapshot(runtime, 0.0);
            BridgeDiagnosticDto diagnostic = PlaytestProtocolV2.Diagnostic(
                "Browser.Dispatch.BaseSequence.Mismatch",
                "baseSequence",
                "The compact command baseSequence does not match the authoritative sequence; request a full snapshot before retrying.");
            return PlaytestProtocolV2.Serialize(
                new PlaytestResponseDto
                {
                    Operation = "dispatch",
                    Ok = false,
                    Accepted = false,
                    Mode = runtime.Mode,
                    Message = diagnostic.Message,
                    Sequence = runtime.Sequence,
                    ResponseKind = "compact",
                    BaseSequence = requestedBaseSequence,
                    RequiresResync = true,
                    Command = payload.Clone(),
                    StateDigest = ComputeStateDigest(runtime, snapshot),
                    ReplayDigest = ComputeReplayDigest(runtime),
                    Diagnostics = new List<PlaytestDiagnosticDto>
                    {
                        ToWireDiagnostic(diagnostic)
                    }
                });
        }

        private string SerializeError(
            string operation,
            BridgeDiagnosticDto diagnostic,
            BridgeRuntime runtime)
        {
            PlaytestSnapshotDto snapshot = CreateSnapshot(runtime, 0.0);
            return PlaytestProtocolV2.Serialize(
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

        private sealed class BridgeRuntime
        {
            public BridgeRuntime(
                string mode,
                string initializationJson,
                GameSession playSession)
            {
                Mode = mode;
                InitializationJson = initializationJson;
                Replay = new ReplayDigestChain(mode, initializationJson);
                PlaySession = playSession;
                LastDetailedProjection = playSession.CurrentSpatialCandidate;
            }

            public string Mode { get; }

            public string InitializationJson { get; }

            public GameSession PlaySession { get; set; }

            public ulong Sequence { get; set; }

            public double LastScore { get; set; }

            public GameSessionSnapshot? LastGameSnapshot { get; set; }

            public EquilibriumCoreProjectionV1? LastDetailedProjection { get; set; }

            public ReplayDigestChain Replay { get; }

            public PlaytestEventDto? LastEvent { get; set; }
        }
    }
}
