using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ReactorSim.Core;

namespace ReactorSim.Cli
{
    /// <summary>
    /// The bounded text client over the engine-neutral synthetic Phase 8
    /// scenario runtime. Wall durations are explicit command inputs; the
    /// process clock and Unity frame timing are never sampled.
    /// </summary>
    public static class CliApplication
    {
        private const int SuccessExitCode = 0;
        private const int CommandErrorExitCode = 2;
        private const string CanonicalNewLine = "\n";
        private static readonly char[] CommandSeparators = { ' ', '\t' };

        public static int Run(TextReader input, TextWriter output, TextWriter error)
        {
            return RunCore(input, output, error, false);
        }

        public static int RunInteractive(TextReader input, TextWriter output, TextWriter error)
        {
            return RunCore(input, output, error, true);
        }

        private static int RunCore(
            TextReader input,
            TextWriter output,
            TextWriter error,
            bool showPrompt)
        {
            ArgumentNullException.ThrowIfNull(input);
            ArgumentNullException.ThrowIfNull(output);
            ArgumentNullException.ThrowIfNull(error);

            CliSession? session = null;
            bool hadCommandError = false;
            while (true)
            {
                if (showPrompt)
                {
                    output.Write("reactor> ");
                    output.Flush();
                }

                string? line = input.ReadLine();
                if (line == null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                CommandResult result = Execute(line, ref session, output, error);
                hadCommandError |= !result.Succeeded;
                if (result.ShouldQuit)
                {
                    break;
                }
            }

            return hadCommandError ? CommandErrorExitCode : SuccessExitCode;
        }

        private static CommandResult Execute(
            string line,
            ref CliSession? session,
            TextWriter output,
            TextWriter error)
        {
            string[] tokens = line.Split(
                CommandSeparators,
                StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                return CommandResult.Success;
            }

            string command = tokens[0].ToLowerInvariant();
            switch (command)
            {
                case "help":
                    if (tokens.Length != 1)
                    {
                        return Fail(error, "CLI.Command.Usage", "usage: help");
                    }

                    WriteHelp(output);
                    return CommandResult.Success;

                case "new":
                    return ExecuteNewRun(tokens, ref session, output, error);

                case "inspect":
                    return ExecuteInspect(tokens, session, output, error);

                case "advance":
                    return ExecuteAdvance(tokens, session, output, error);

                case "set":
                    return ExecuteSet(tokens, session, output, error);

                case "save":
                    return ExecuteSave(tokens, session, output, error);

                case "load":
                    return ExecuteLoad(tokens, ref session, output, error);

                case "replay":
                    return ExecuteReplay(tokens, output, error);

                case "pause":
                    if (tokens.Length != 1)
                    {
                        return Fail(error, "CLI.Command.Usage", "usage: pause");
                    }

                    if (session == null)
                    {
                        return MissingRun(error);
                    }

                    if (!session.CanAppendReplayCommand)
                    {
                        return Fail(error, "CLI.Replay.Capacity", "the approved replay command capacity has been reached.");
                    }

                    session.Pause();
                    session.AppendReplayCommand(Phase8ReplayCommandV1.Pause());
                    WriteLine(output, "paused=true");
                    return CommandResult.Success;

                case "resume":
                    if (tokens.Length != 1)
                    {
                        return Fail(error, "CLI.Command.Usage", "usage: resume");
                    }

                    if (session == null)
                    {
                        return MissingRun(error);
                    }

                    if (!session.CanAppendReplayCommand)
                    {
                        return Fail(error, "CLI.Replay.Capacity", "the approved replay command capacity has been reached.");
                    }

                    session.Resume();
                    session.AppendReplayCommand(Phase8ReplayCommandV1.Resume());
                    WriteLine(output, "paused=false");
                    return CommandResult.Success;

                case "quit":
                    if (tokens.Length != 1)
                    {
                        return Fail(error, "CLI.Command.Usage", "usage: quit");
                    }

                    WriteLine(output, "bye");
                    return new CommandResult(true, true);

                default:
                    return Fail(
                        error,
                        "CLI.Command.Unknown",
                        "unknown command '" + tokens[0] + "'. Type 'help' for available commands.");
            }
        }

        private static CommandResult ExecuteInspect(
            string[] tokens,
            CliSession? session,
            TextWriter output,
            TextWriter error)
        {
            if (session == null)
            {
                return MissingRun(error);
            }

            if (tokens.Length < 2)
            {
                return Fail(
                    error,
                    "CLI.Command.Usage",
                    "usage: inspect core | inspect scenario | inspect score | inspect channel <channel_id> | inspect bundle <channel_id> <position>");
            }

            switch (tokens[1].ToLowerInvariant())
            {
                case "core":
                    if (tokens.Length != 2)
                    {
                        return Fail(error, "CLI.Command.Usage", "usage: inspect core");
                    }

                    WriteCoreInspection(session, output);
                    return CommandResult.Success;

                case "scenario":
                    if (tokens.Length != 2)
                    {
                        return Fail(error, "CLI.Command.Usage", "usage: inspect scenario");
                    }

                    WriteScenarioInspection(session, output);
                    return CommandResult.Success;

                case "score":
                    if (tokens.Length != 2)
                    {
                        return Fail(error, "CLI.Command.Usage", "usage: inspect score");
                    }

                    WriteScoreInspection(session, output);
                    return CommandResult.Success;

                case "channel":
                    return WriteChannelInspection(tokens, session, output, error);

                case "bundle":
                    return WriteBundleInspection(tokens, session, output, error);

                default:
                    return Fail(
                        error,
                        "CLI.Command.Usage",
                        "usage: inspect core | inspect scenario | inspect score | inspect channel <channel_id> | inspect bundle <channel_id> <position>");
            }
        }

        private static CommandResult ExecuteNewRun(
            string[] tokens,
            ref CliSession? session,
            TextWriter output,
            TextWriter error)
        {
            if (tokens.Length < 2 || tokens.Length > 4 ||
                !string.Equals(tokens[1], "run", StringComparison.OrdinalIgnoreCase))
            {
                return Fail(error, "CLI.Command.Usage", "usage: new run [scenario_id] [playback_mode_id]");
            }

            string scenarioId = tokens.Length >= 3 ? tokens[2] : "tutorial-equilibrium";
            string modeId = tokens.Length == 4 ? tokens[3] : string.Empty;
            if (string.IsNullOrEmpty(modeId))
            {
                try
                {
                    Phase8ScenarioParameterPack pack = Phase8ScenarioParameterPack.LoadApproved(
                        Phase8ScenarioParameterPack.FindDefaultPath());
                    modeId = pack.DefaultPlaybackModeId;
                }
                catch (InvalidOperationException exception)
                {
                    return Fail(error, "CLI.ScenarioPack.Invalid", Phase8ScenarioParameterPack.FormatFailure(exception));
                }
            }

            if (!TryCreateSession(
                    scenarioId,
                    modeId,
                    out CliSession? createdSession,
                    out string failureCode,
                    out string failureMessage))
            {
                return Fail(error, failureCode, failureMessage);
            }

            session = createdSession;
            WriteRunCreated(output, createdSession!);
            return CommandResult.Success;
        }

        private static bool TryCreateSession(
            string scenarioId,
            string playbackModeId,
            out CliSession? session,
            out string failureCode,
            out string failureMessage)
        {
            session = null;
            failureCode = string.Empty;
            failureMessage = string.Empty;
            try
            {
                Phase8ScenarioParameterPack pack = Phase8ScenarioParameterPack.LoadApproved(
                    Phase8ScenarioParameterPack.FindDefaultPath());
                Phase8ScoringParameterPack scoringPack = Phase8ScoringParameterPack.LoadApproved(
                    Phase8ScoringParameterPack.FindDefaultPath());
                if (!pack.Scenarios.TryGetValue(scenarioId, out Phase8ScenarioDefinitionV1? scenario))
                {
                    failureCode = "CLI.Scenario.NotFound";
                    failureMessage = "the approved scenario identifier is not present in the parameter pack.";
                    return false;
                }

                if (!pack.DifficultyProfiles.TryGetValue(scenario.DifficultyId, out Phase8DifficultyProfileV1? profile))
                {
                    failureCode = "CLI.Difficulty.NotFound";
                    failureMessage = "the scenario difficulty profile is not present in the parameter pack.";
                    return false;
                }

                if (!pack.PlaybackModes.TryGetValue(playbackModeId, out Phase8PlaybackModeV1? playbackMode))
                {
                    failureCode = "CLI.PlaybackMode.NotFound";
                    failureMessage = "the approved playback mode identifier is not present in the parameter pack.";
                    return false;
                }

                ContractValidationResult<Phase8ScenarioRuntimeV1> runtimeResult =
                    Phase8ScenarioRuntimeV1.TryCreate(scenario, profile, pack.TimeModel, playbackMode);
                if (!runtimeResult.IsValid)
                {
                    failureCode = "CLI.ScenarioRuntime.Invalid";
                    failureMessage = runtimeResult.FirstDiagnostic.ToString();
                    return false;
                }

                ContractValidationResult<Phase8ScoredScenarioRuntimeV1> scoredRuntimeResult =
                    Phase8ScoredScenarioRuntimeV1.TryCreate(
                        runtimeResult.Value,
                        scoringPack.Parameters);
                if (!scoredRuntimeResult.IsValid)
                {
                    failureCode = "CLI.ScoringRuntime.Invalid";
                    failureMessage = scoredRuntimeResult.FirstDiagnostic.ToString();
                    return false;
                }

                session = new CliSession(
                    SyntheticFixtures.CreateTwoChannelThreePosition(),
                    pack,
                    scoringPack,
                    scoredRuntimeResult.Value);
                return true;
            }
            catch (InvalidOperationException exception)
            {
                failureCode = "CLI.ScenarioPack.Invalid";
                failureMessage = Phase8ScenarioParameterPack.FormatFailure(exception);
                return false;
            }
        }

        private static void WriteRunCreated(TextWriter output, CliSession session)
        {
            WriteLine(output, "run: created");
            WriteLine(output, "run_kind=synthetic-scenario");
            WriteLine(output, "scenario_id=" + session.Runtime.ScenarioId);
            WriteLine(output, "difficulty_id=" + session.Runtime.DifficultyId);
            WriteLine(output, "playback_mode_id=" + session.Runtime.PlaybackModeId);
            WriteLine(output, "acceleration_factor=" + FormatDouble(session.Runtime.AccelerationFactor));
            WriteLine(output, "replay_seed=" + session.Runtime.ReplaySeed.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "approved_scoring_parameter_sha256=" + session.ScoringPack.ArtifactSha256);
            WriteLine(output, "data_pack_version=" + session.Fixture.DataPack.DataPackVersion);
            WriteLine(output, "simulation_time_s=" + FormatDouble(session.Runtime.SimulationTimeSeconds));
            WriteLine(output, "paused=" + session.Runtime.IsPaused.ToString().ToLowerInvariant());
        }

        private static CommandResult ExecuteAdvance(
            string[] tokens,
            CliSession? session,
            TextWriter output,
            TextWriter error)
        {
            if (session == null)
            {
                return MissingRun(error);
            }

            if (tokens.Length != 3 || !string.Equals(tokens[1], "wall", StringComparison.OrdinalIgnoreCase) ||
                !ulong.TryParse(tokens[2], NumberStyles.None, CultureInfo.InvariantCulture, out ulong wallMilliseconds))
            {
                return Fail(error, "CLI.Command.Usage", "usage: advance wall <milliseconds>");
            }

            if (!session.CanAppendReplayCommand)
            {
                return Fail(error, "CLI.Replay.Capacity", "the approved replay command capacity has been reached.");
            }

            ContractValidationResult<Phase8ScoredAdvanceResultV1> result =
                session.Runtime.TryAdvanceWallMilliseconds(wallMilliseconds);
            if (!result.IsValid)
            {
                return Fail(error, "CLI.Advance.Invalid", result.FirstDiagnostic.ToString());
            }

            WriteLine(output, "advance:");
            WriteLine(output, "  wall_ms_requested=" + wallMilliseconds.ToString(CultureInfo.InvariantCulture));
            Phase8ScenarioAdvanceResultV1 advance = result.Value.Advance;
            Phase8TurnSummaryV1 summary = result.Value.TurnSummary;
            Phase8ScoreSnapshotV1 score = result.Value.Score;
            WriteLine(output, "  control_ticks_processed=" + advance.ControlTicksProcessed.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  simulation_time_s=" + FormatDouble(advance.SimulationTimeSeconds));
            WriteLine(output, "  wall_elapsed_s=" + FormatDouble(advance.WallElapsedSeconds));
            WriteLine(output, "  outcome=" + advance.Outcome);
            WriteLine(output, "  paused=" + advance.IsPaused.ToString().ToLowerInvariant());
            WriteLine(output, "  turn_id=" + summary.TurnId.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  turn_cause=" + summary.Cause);
            WriteLine(output, "  turn_effect=" + summary.Effect);
            WriteLine(output, "  score_total=" + FormatDouble(score.TotalPoints));
            WriteLine(output, "  energy_quality=" + FormatDouble(score.EnergyQuality));
            WriteLine(output, "  stability_quality=" + FormatDouble(score.StabilityQuality));
            WriteLine(output, "  fuelling_efficiency=" + FormatDouble(score.FuellingEfficiency));
            foreach (Phase8ActionTransitionV1 action in advance.ActionTransitions)
            {
                WriteLine(output, "  action_id=" + action.ActionId.ToString(CultureInfo.InvariantCulture));
                WriteLine(output, "  action_kind=" + action.Kind);
                WriteLine(output, "  action_queue_delay_wall_s=" + FormatDouble(action.QueueDelayWallTimeSeconds));
            }

            foreach (Phase8EventRecordV1 record in advance.EventRecords)
            {
                WriteLine(output, "  event_id=" + record.EventId);
                WriteLine(output, "  event_kind=" + record.Kind);
                WriteLine(output, "  event_time_s=" + FormatDouble(record.SimulationTimeSeconds));
            }

            foreach (Phase8LossRecordV1 loss in advance.LossRecords)
            {
                WriteLine(output, "  loss_id=" + loss.LossId);
                WriteLine(output, "  loss_metric=" + loss.Metric);
                WriteLine(output, "  loss_time_s=" + FormatDouble(loss.SimulationTimeSeconds));
            }

            session.AppendReplayCommand(Phase8ReplayCommandV1.Advance(wallMilliseconds));
            return CommandResult.Success;
        }

        private static CommandResult ExecuteSet(
            string[] tokens,
            CliSession? session,
            TextWriter output,
            TextWriter error)
        {
            if (session == null)
            {
                return MissingRun(error);
            }

            if (tokens.Length == 4 &&
                string.Equals(tokens[1], "power", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(tokens[2], "target", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseFiniteDouble(tokens[3], out double powerTarget))
                {
                    return Fail(error, "CLI.Command.Argument.Invalid", "power target must be a finite invariant-culture number.");
                }

                if (!session.CanAppendReplayCommand)
                {
                    return Fail(error, "CLI.Replay.Capacity", "the approved replay command capacity has been reached.");
                }

                ContractValidationResult<Phase8ActionQueueResultV1> result =
                    session.Runtime.TryQueuePowerTarget(powerTarget);
                CommandResult commandResult = WriteQueuedAction(result, output, error);
                if (commandResult.Succeeded)
                {
                    session.AppendReplayCommand(Phase8ReplayCommandV1.SetPowerTarget(powerTarget));
                }

                return commandResult;
            }

            if (tokens.Length == 4 &&
                string.Equals(tokens[1], "tilt", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(tokens[2], "target", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseFiniteDouble(tokens[3], out double tiltTarget))
                {
                    return Fail(error, "CLI.Command.Argument.Invalid", "tilt target must be a finite invariant-culture number.");
                }

                if (!session.CanAppendReplayCommand)
                {
                    return Fail(error, "CLI.Replay.Capacity", "the approved replay command capacity has been reached.");
                }

                ContractValidationResult<Phase8ActionQueueResultV1> result =
                    session.Runtime.TryQueueTiltTarget(tiltTarget);
                CommandResult commandResult = WriteQueuedAction(result, output, error);
                if (commandResult.Succeeded)
                {
                    session.AppendReplayCommand(Phase8ReplayCommandV1.SetTiltTarget(tiltTarget));
                }

                return commandResult;
            }

            if (tokens.Length == 3 && string.Equals(tokens[1], "playback", StringComparison.OrdinalIgnoreCase))
            {
                if (!session.Pack.PlaybackModes.TryGetValue(tokens[2], out Phase8PlaybackModeV1? playbackMode))
                {
                    return Fail(error, "CLI.PlaybackMode.NotFound", "the approved playback mode identifier is not present in the parameter pack.");
                }

                if (!session.CanAppendReplayCommand)
                {
                    return Fail(error, "CLI.Replay.Capacity", "the approved replay command capacity has been reached.");
                }

                ContractValidationResult<bool> result = session.Runtime.TrySetPlaybackMode(playbackMode);
                if (!result.IsValid)
                {
                    return Fail(error, "CLI.PlaybackMode.Invalid", result.FirstDiagnostic.ToString());
                }

                WriteLine(output, "playback_mode_id=" + playbackMode.ModeId);
                WriteLine(output, "acceleration_factor=" + FormatDouble(playbackMode.AccelerationFactor));
                session.AppendReplayCommand(Phase8ReplayCommandV1.SetPlaybackMode(playbackMode.ModeId));
                return CommandResult.Success;
            }

            return Fail(
                error,
                "CLI.Command.Usage",
                "usage: set power target <fraction> | set tilt target <fraction> | set playback <mode_id>");
        }

        private static CommandResult ExecuteSave(
            string[] tokens,
            CliSession? session,
            TextWriter output,
            TextWriter error)
        {
            if (session == null)
            {
                return MissingRun(error);
            }

            if (tokens.Length != 2)
            {
                return Fail(error, "CLI.Command.Usage", "usage: save <path>");
            }

            try
            {
                Phase8ReplayArchiveV1 archive = session.CreateReplayArchive();
                Phase8ReplayArchiveCodecV1.Save(tokens[1], archive);
                WriteLine(output, "save: written");
                WriteLine(output, "command_count=" + archive.Commands.Count.ToString(CultureInfo.InvariantCulture));
                WriteLine(output, "final_state_digest=" + archive.ExpectedFinalStateDigest);
                return CommandResult.Success;
            }
            catch (Phase8ReplayArchiveFailure exception)
            {
                return Fail(error, "CLI.Save.Invalid", Phase8ReplayArchiveCodecV1.FormatFailure(exception));
            }
            catch (InvalidOperationException exception)
            {
                return Fail(error, "CLI.Save.Invalid", exception.Message);
            }
        }

        private static CommandResult ExecuteLoad(
            string[] tokens,
            ref CliSession? session,
            TextWriter output,
            TextWriter error)
        {
            if (tokens.Length != 2)
            {
                return Fail(error, "CLI.Command.Usage", "usage: load <path>");
            }

            if (!TryLoadAndReplayArchive(tokens[1], out CliSession? candidate, out string failureMessage))
            {
                return Fail(error, "CLI.Load.Invalid", failureMessage);
            }

            session = candidate;
            WriteLine(output, "load: restored");
            WriteLine(output, "command_count=" + candidate!.ReplayCommandCount.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "final_state_digest=" + candidate.ComputeReplayDigest());
            return CommandResult.Success;
        }

        private static CommandResult ExecuteReplay(
            string[] tokens,
            TextWriter output,
            TextWriter error)
        {
            if (tokens.Length != 2)
            {
                return Fail(error, "CLI.Command.Usage", "usage: replay <path>");
            }

            if (!TryLoadAndReplayArchive(tokens[1], out CliSession? candidate, out string failureMessage))
            {
                return Fail(error, "CLI.Replay.Invalid", failureMessage);
            }

            WriteLine(output, "replay: verified");
            WriteLine(output, "command_count=" + candidate!.ReplayCommandCount.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "final_state_digest=" + candidate.ComputeReplayDigest());
            return CommandResult.Success;
        }

        private static bool TryLoadAndReplayArchive(
            string path,
            out CliSession? candidate,
            out string failureMessage)
        {
            candidate = null;
            failureMessage = string.Empty;
            Phase8ReplayArchiveV1 archive;
            try
            {
                archive = Phase8ReplayArchiveCodecV1.Load(path);
            }
            catch (Phase8ReplayArchiveFailure exception)
            {
                failureMessage = Phase8ReplayArchiveCodecV1.FormatFailure(exception);
                return false;
            }
            catch (InvalidOperationException exception)
            {
                failureMessage = exception.Message;
                return false;
            }

            if (!TryCreateSession(
                    archive.ScenarioId,
                    archive.InitialPlaybackModeId,
                    out CliSession? freshSession,
                    out string failureCode,
                    out string createFailureMessage))
            {
                failureMessage = failureCode + ": " + createFailureMessage;
                return false;
            }

            if (!string.Equals(freshSession!.Runtime.ScenarioId, archive.ScenarioId, StringComparison.Ordinal) ||
                !string.Equals(freshSession.Runtime.DifficultyId, archive.DifficultyId, StringComparison.Ordinal) ||
                freshSession.Runtime.Seed != archive.Seed)
            {
                failureMessage = "The replay archive identity does not match the approved scenario definition.";
                return false;
            }

            if (!string.Equals(
                    freshSession.Pack.ArtifactSha256,
                    archive.ScenarioParameterSha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    freshSession.ScoringPack.ArtifactSha256,
                    archive.ScoringParameterSha256,
                    StringComparison.Ordinal))
            {
                failureMessage = "The replay archive data-pack digests do not match the approved local artifacts.";
                return false;
            }

            for (int index = 0; index < archive.Commands.Count; index++)
            {
                Phase8ReplayCommandV1 command = archive.Commands[index];
                if (!TryApplyReplayCommand(freshSession, command, out string commandFailureMessage))
                {
                    failureMessage =
                        "commands[" + index.ToString(CultureInfo.InvariantCulture) + "]: " +
                        commandFailureMessage;
                    return false;
                }

                freshSession.AppendReplayCommand(command);
            }

            string actualDigest = freshSession.ComputeReplayDigest();
            if (!string.Equals(actualDigest, archive.ExpectedFinalStateDigest, StringComparison.Ordinal))
            {
                failureMessage =
                    "The replay final-state digest does not match the archive (expected " +
                    archive.ExpectedFinalStateDigest + ", actual " + actualDigest + ").";
                return false;
            }

            candidate = freshSession;
            return true;
        }

        private static bool TryApplyReplayCommand(
            CliSession session,
            Phase8ReplayCommandV1 command,
            out string failureMessage)
        {
            switch (command.Kind)
            {
                case Phase8ReplayCommandKindV1.AdvanceWall:
                    ContractValidationResult<Phase8ScoredAdvanceResultV1> advanceResult =
                        session.Runtime.TryAdvanceWallMilliseconds(command.WallMilliseconds);
                    if (!advanceResult.IsValid)
                    {
                        failureMessage = advanceResult.FirstDiagnostic.ToString();
                        return false;
                    }

                    failureMessage = string.Empty;
                    return true;

                case Phase8ReplayCommandKindV1.SetPowerTarget:
                    ContractValidationResult<Phase8ActionQueueResultV1> powerResult =
                        session.Runtime.TryQueuePowerTarget(command.TargetFraction);
                    if (!powerResult.IsValid)
                    {
                        failureMessage = powerResult.FirstDiagnostic.ToString();
                        return false;
                    }

                    failureMessage = string.Empty;
                    return true;

                case Phase8ReplayCommandKindV1.SetTiltTarget:
                    ContractValidationResult<Phase8ActionQueueResultV1> tiltResult =
                        session.Runtime.TryQueueTiltTarget(command.TargetFraction);
                    if (!tiltResult.IsValid)
                    {
                        failureMessage = tiltResult.FirstDiagnostic.ToString();
                        return false;
                    }

                    failureMessage = string.Empty;
                    return true;

                case Phase8ReplayCommandKindV1.SetPlaybackMode:
                    if (!session.Pack.PlaybackModes.TryGetValue(
                            command.PlaybackModeId,
                            out Phase8PlaybackModeV1? playbackMode))
                    {
                        failureMessage = "the approved playback mode identifier is not present in the parameter pack.";
                        return false;
                    }

                    ContractValidationResult<bool> playbackResult =
                        session.Runtime.TrySetPlaybackMode(playbackMode);
                    if (!playbackResult.IsValid)
                    {
                        failureMessage = playbackResult.FirstDiagnostic.ToString();
                        return false;
                    }

                    failureMessage = string.Empty;
                    return true;

                case Phase8ReplayCommandKindV1.Pause:
                    ContractValidationResult<bool> pauseResult = session.Runtime.TryPause();
                    if (!pauseResult.IsValid)
                    {
                        failureMessage = pauseResult.FirstDiagnostic.ToString();
                        return false;
                    }

                    failureMessage = string.Empty;
                    return true;

                case Phase8ReplayCommandKindV1.Resume:
                    ContractValidationResult<bool> resumeResult = session.Runtime.TryResume();
                    if (!resumeResult.IsValid)
                    {
                        failureMessage = resumeResult.FirstDiagnostic.ToString();
                        return false;
                    }

                    failureMessage = string.Empty;
                    return true;

                default:
                    failureMessage = "the replay command kind is not supported.";
                    return false;
            }
        }

        private static CommandResult WriteQueuedAction(
            ContractValidationResult<Phase8ActionQueueResultV1> result,
            TextWriter output,
            TextWriter error)
        {
            if (!result.IsValid)
            {
                return Fail(error, "CLI.Action.Invalid", result.FirstDiagnostic.ToString());
            }

            WriteLine(output, "action: queued");
            WriteLine(output, "action_id=" + result.Value.ActionId.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "pending_actions=" + result.Value.PendingActionCount.ToString(CultureInfo.InvariantCulture));
            return CommandResult.Success;
        }

        private static void WriteHelp(TextWriter output)
        {
            WriteLine(output, "commands:");
            WriteLine(output, "  help");
            WriteLine(output, "  new run [scenario_id] [playback_mode_id]");
            WriteLine(output, "  inspect core");
            WriteLine(output, "  inspect scenario");
            WriteLine(output, "  inspect score");
            WriteLine(output, "  inspect channel <channel_id>");
            WriteLine(output, "  inspect bundle <channel_id> <position>");
            WriteLine(output, "  advance wall <milliseconds>");
            WriteLine(output, "  set power target <fraction>");
            WriteLine(output, "  set tilt target <fraction>");
            WriteLine(output, "  set playback <mode_id>");
            WriteLine(output, "  pause");
            WriteLine(output, "  resume");
            WriteLine(output, "  save <path>");
            WriteLine(output, "  load <path>");
            WriteLine(output, "  replay <path>");
            WriteLine(output, "  quit");
        }

        private static void WriteCoreInspection(CliSession session, TextWriter output)
        {
            SyntheticCoreFixture fixture = session.Fixture;
            WriteLine(output, "core:");
            WriteLine(output, "  run_kind=synthetic-scenario");
            WriteLine(output, "  topology_channel_count=" + fixture.Topology.ChannelCount.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  topology_bundle_position_count=" + fixture.Topology.BundlePositionCount.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  inventory_slot_count=" + fixture.Inventory.SlotCount.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  inventory_occupied_count=" + fixture.Inventory.OccupiedCount.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  data_pack_version=" + fixture.DataPack.DataPackVersion);
            WriteLine(output, "  scenario_id=" + session.Runtime.ScenarioId);
            WriteLine(output, "  difficulty_id=" + session.Runtime.DifficultyId);
            WriteLine(output, "  playback_mode_id=" + session.Runtime.PlaybackModeId);
            WriteLine(output, "  acceleration_factor=" + FormatDouble(session.Runtime.AccelerationFactor));
            WriteLine(output, "  replay_seed=" + session.Runtime.ReplaySeed.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  approved_scoring_parameter_sha256=" + session.ScoringPack.ArtifactSha256);
            WriteLine(output, "  simulation_time_s=" + FormatDouble(session.Runtime.SimulationTimeSeconds));
            WriteLine(output, "  simulation_step_index=" + session.Runtime.SimulationStepIndex.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  wall_elapsed_s=" + FormatDouble(session.Runtime.WallElapsedSeconds));
            WriteLine(output, "  core_state_version=" + fixture.Configuration.InitialCoreStateVersion.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  spatial_state_version=" + fixture.Configuration.InitialSpatialStateVersion.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  power_snapshot_version=" + fixture.Configuration.InitialPowerSnapshotVersion.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  normalized_power_fraction=" + FormatDouble(session.Runtime.NormalizedPowerFraction));
            WriteLine(output, "  absolute_tilt_fraction=" + FormatDouble(session.Runtime.AbsoluteTiltFraction));
            WriteLine(output, "  control_margin_fraction=" + FormatDouble(session.Runtime.ControlMarginFraction));
            WriteLine(output, "  device_available_fraction=" + FormatDouble(session.Runtime.DeviceAvailableFraction));
            WriteLine(output, "  refuel_requests_remaining=" + session.Runtime.RefuelRequestsRemaining.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  pending_actions=" + session.Runtime.PendingActionCount.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  scripted_events_processed=" + session.Runtime.ProcessedScriptedEventCount.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  score_total=" + FormatDouble(session.Runtime.Score.TotalPoints));
            WriteLine(output, "  turn_summary_count=" + session.Runtime.TurnSummaries.Count.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  outcome=" + session.Runtime.Outcome);
            WriteLine(output, "  paused=" + session.Runtime.IsPaused.ToString().ToLowerInvariant());
        }

        private static void WriteScenarioInspection(CliSession session, TextWriter output)
        {
            Phase8DifficultyProfileV1 profile =
                session.Pack.DifficultyProfiles[session.Runtime.DifficultyId];
            WriteLine(output, "scenario:");
            WriteLine(output, "  scenario_id=" + session.Runtime.ScenarioId);
            WriteLine(output, "  difficulty_id=" + session.Runtime.DifficultyId);
            WriteLine(output, "  seed=" + session.Runtime.Seed.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  replay_seed=" + session.Runtime.ReplaySeed.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  playback_mode_id=" + session.Runtime.PlaybackModeId);
            WriteLine(output, "  acceleration_factor=" + FormatDouble(session.Runtime.AccelerationFactor));
            WriteLine(output, "  horizon_s=" + FormatDouble(profile.ScenarioHorizonSeconds));
            WriteLine(output, "  decision_interval_s=" + FormatDouble(profile.DecisionIntervalSeconds));
            WriteLine(output, "  approved_parameter_sha256=" + session.Pack.ArtifactSha256);
            WriteLine(output, "  approved_scoring_parameter_sha256=" + session.ScoringPack.ArtifactSha256);
        }

        private static void WriteScoreInspection(CliSession session, TextWriter output)
        {
            Phase8ScoreSnapshotV1 score = session.Runtime.Score;
            WriteLine(output, "score:");
            WriteLine(output, "  total_points=" + FormatDouble(score.TotalPoints));
            WriteLine(output, "  survival_points=" + FormatDouble(score.SurvivalPoints));
            WriteLine(output, "  energy_quality=" + FormatDouble(score.EnergyQuality));
            WriteLine(output, "  energy_points=" + FormatDouble(score.EnergyPoints));
            WriteLine(output, "  stability_quality=" + FormatDouble(score.StabilityQuality));
            WriteLine(output, "  stability_points=" + FormatDouble(score.StabilityPoints));
            WriteLine(output, "  fuelling_efficiency=" + FormatDouble(score.FuellingEfficiency));
            WriteLine(output, "  fuelling_efficiency_points=" + FormatDouble(score.FuellingEfficiencyPoints));
            WriteLine(output, "  control_penalty_points=" + FormatDouble(score.ControlPenaltyPoints));
            WriteLine(output, "  loss_penalty_points=" + FormatDouble(score.LossPenaltyPoints));
            WriteLine(output, "  committed_action_count=" + score.CommittedActionCount.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  recorded_loss_count=" + score.RecordedLossCount.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  turn_summary_count=" + session.Runtime.TurnSummaries.Count.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  approved_scoring_parameter_sha256=" + session.ScoringPack.ArtifactSha256);
        }

        private static CommandResult WriteChannelInspection(
            string[] tokens,
            CliSession session,
            TextWriter output,
            TextWriter error)
        {
            if (tokens.Length != 3)
            {
                return Fail(error, "CLI.Command.Usage", "usage: inspect channel <channel_id>");
            }

            if (!TryParseIndex(tokens[2], out uint channelId))
            {
                return Fail(error, "CLI.Command.Argument.Invalid", "channel_id must be a nonnegative decimal integer.");
            }

            if (channelId >= session.Fixture.Topology.ChannelCount)
            {
                return Fail(error, "CLI.Command.Argument.OutOfRange", "channel_id is outside the validated topology.");
            }

            ChannelTopology channel = session.Fixture.Topology.GetChannel(new ChannelId(channelId));
            WriteLine(output, "channel:");
            WriteLine(output, "  id=" + channel.ChannelId.Value.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  coordinate_x=" + channel.CoordinateX.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  coordinate_y=" + channel.CoordinateY.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  flow_direction=" + channel.FlowDirection);
            WriteLine(output, "  inlet_position=" + channel.InletPosition.Value.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  outlet_position=" + channel.OutletPosition.Value.ToString(CultureInfo.InvariantCulture));
            return CommandResult.Success;
        }

        private static CommandResult WriteBundleInspection(
            string[] tokens,
            CliSession session,
            TextWriter output,
            TextWriter error)
        {
            if (tokens.Length != 4)
            {
                return Fail(error, "CLI.Command.Usage", "usage: inspect bundle <channel_id> <position>");
            }

            if (!TryParseIndex(tokens[2], out uint channelId) ||
                !TryParseIndex(tokens[3], out uint position))
            {
                return Fail(error, "CLI.Command.Argument.Invalid", "channel_id and position must be nonnegative decimal integers.");
            }

            if (channelId >= session.Fixture.Topology.ChannelCount ||
                position >= session.Fixture.Topology.BundlePositionCount)
            {
                return Fail(error, "CLI.Command.Argument.OutOfRange", "the bundle location is outside the validated topology.");
            }

            BundleState? bundle = session.Fixture.Inventory.Get(
                new NodeKey(new ChannelId(channelId), new BundlePosition(position)));
            if (bundle == null)
            {
                return Fail(error, "CLI.Command.Bundle.Missing", "the validated location is empty.");
            }

            WriteLine(output, "bundle:");
            WriteLine(output, "  id=" + bundle.BundleId);
            WriteLine(output, "  channel_id=" + bundle.ChannelId.Value.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  position=" + bundle.Position.Value.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  material_variant_id=" + bundle.MaterialVariantId.Value);
            WriteLine(output, "  initial_burnup_j_per_kg_hm=" + FormatDouble(bundle.InitialBurnupJPerKgHm));
            WriteLine(output, "  cumulative_fission_energy_j=" + FormatDouble(bundle.CumulativeFissionEnergyJ));
            WriteLine(output, "  current_burnup_j_per_kg_hm=" + FormatDouble(bundle.CurrentBurnupJPerKgHm));
            WriteLine(output, "  heavy_metal_mass_kg=" + FormatDouble(bundle.HeavyMetalMassKg));
            WriteLine(output, "  inserted_at_s=" + FormatDouble(bundle.InsertedAtSeconds));
            return CommandResult.Success;
        }

        private static CommandResult MissingRun(TextWriter error)
        {
            return Fail(error, "CLI.Run.Missing", "start a run with 'new run' before inspecting, advancing, or issuing actions.");
        }

        private static CommandResult Fail(TextWriter error, string code, string message)
        {
            WriteLine(error, "error[" + code + "]: " + message);
            return CommandResult.Failure;
        }

        private static void WriteLine(TextWriter writer, string value)
        {
            writer.Write(value);
            writer.Write(CanonicalNewLine);
        }

        private static bool TryParseIndex(string value, out uint index)
        {
            return uint.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out index);
        }

        private static string FormatDouble(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool TryParseFiniteDouble(string value, out double parsed)
        {
            return double.TryParse(
                       value,
                       NumberStyles.Float,
                       CultureInfo.InvariantCulture,
                       out parsed) &&
                   !double.IsNaN(parsed) &&
                   !double.IsInfinity(parsed);
        }

        private sealed class CliSession
        {
            private readonly List<Phase8ReplayCommandV1> _replayCommands =
                new List<Phase8ReplayCommandV1>();

            public CliSession(
                SyntheticCoreFixture fixture,
                Phase8ScenarioParameterPack pack,
                Phase8ScoringParameterPack scoringPack,
                Phase8ScoredScenarioRuntimeV1 runtime)
            {
                Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
                Pack = pack ?? throw new ArgumentNullException(nameof(pack));
                ScoringPack = scoringPack ?? throw new ArgumentNullException(nameof(scoringPack));
                Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
                InitialPlaybackModeId = runtime.PlaybackModeId;
            }

            public SyntheticCoreFixture Fixture { get; }

            public Phase8ScenarioParameterPack Pack { get; }

            public Phase8ScoringParameterPack ScoringPack { get; }

            public Phase8ScoredScenarioRuntimeV1 Runtime { get; }

            public string InitialPlaybackModeId { get; }

            public int ReplayCommandCount
            {
                get { return _replayCommands.Count; }
            }

            public bool CanAppendReplayCommand
            {
                get { return _replayCommands.Count < Phase8ReplayArchiveV1.MaximumCommandCount; }
            }

            public string ComputeReplayDigest()
            {
                return Phase8ReplayStateDigest.Compute(
                    Runtime,
                    Pack.ArtifactSha256,
                    ScoringPack.ArtifactSha256,
                    InitialPlaybackModeId,
                    _replayCommands);
            }

            public void Pause()
            {
                Runtime.TryPause();
            }

            public void Resume()
            {
                Runtime.TryResume();
            }

            public void AppendReplayCommand(Phase8ReplayCommandV1 command)
            {
                ArgumentNullException.ThrowIfNull(command);

                if (_replayCommands.Count >= Phase8ReplayArchiveV1.MaximumCommandCount)
                {
                    throw new InvalidOperationException(
                        "The replay command collection exceeded the approved maximum.");
                }

                _replayCommands.Add(command);
            }

            public Phase8ReplayArchiveV1 CreateReplayArchive()
            {
                string digest = ComputeReplayDigest();
                return new Phase8ReplayArchiveV1(
                    Runtime.ScenarioId,
                    Runtime.DifficultyId,
                    Runtime.Seed,
                    InitialPlaybackModeId,
                    Pack.ArtifactSha256,
                    ScoringPack.ArtifactSha256,
                    _replayCommands,
                    digest);
            }
        }

        private readonly struct CommandResult
        {
            public static readonly CommandResult Success = new CommandResult(true, false);

            public static readonly CommandResult Failure = new CommandResult(false, false);

            public CommandResult(bool succeeded, bool shouldQuit)
            {
                Succeeded = succeeded;
                ShouldQuit = shouldQuit;
            }

            public bool Succeeded { get; }

            public bool ShouldQuit { get; }
        }
    }
}
