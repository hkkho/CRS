using System;
using System.Globalization;
using System.IO;
using ReactorSim.Core;

namespace ReactorSim.Cli
{
    /// <summary>
    /// The first bounded text-client surface for the existing engine-neutral
    /// contracts. This slice exposes inspection and session-control commands;
    /// it does not execute physics or mutate Core state.
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
                    if (tokens.Length != 2 || !string.Equals(tokens[1], "run", StringComparison.OrdinalIgnoreCase))
                    {
                        return Fail(error, "CLI.Command.Usage", "usage: new run");
                    }

                    session = new CliSession(SyntheticFixtures.CreateTwoChannelThreePosition());
                    WriteLine(output, "run: created");
                    WriteLine(output, "run_kind=synthetic-inspection");
                    WriteLine(output, "data_pack_version=synthetic-p3-t01");
                    return CommandResult.Success;

                case "inspect":
                    return ExecuteInspect(tokens, session, output, error);

                case "pause":
                    if (tokens.Length != 1)
                    {
                        return Fail(error, "CLI.Command.Usage", "usage: pause");
                    }

                    if (session == null)
                    {
                        return MissingRun(error);
                    }

                    session.Pause();
                    WriteLine(output, "paused=true");
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
                    "usage: inspect core | inspect channel <channel_id> | inspect bundle <channel_id> <position>");
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

                case "channel":
                    return WriteChannelInspection(tokens, session, output, error);

                case "bundle":
                    return WriteBundleInspection(tokens, session, output, error);

                default:
                    return Fail(
                        error,
                        "CLI.Command.Usage",
                        "usage: inspect core | inspect channel <channel_id> | inspect bundle <channel_id> <position>");
            }
        }

        private static void WriteHelp(TextWriter output)
        {
            WriteLine(output, "commands:");
            WriteLine(output, "  help");
            WriteLine(output, "  new run");
            WriteLine(output, "  inspect core");
            WriteLine(output, "  inspect channel <channel_id>");
            WriteLine(output, "  inspect bundle <channel_id> <position>");
            WriteLine(output, "  pause");
            WriteLine(output, "  quit");
        }

        private static void WriteCoreInspection(CliSession session, TextWriter output)
        {
            SyntheticCoreFixture fixture = session.Fixture;
            WriteLine(output, "core:");
            WriteLine(output, "  run_kind=synthetic-inspection");
            WriteLine(output, "  topology_channel_count=" + fixture.Topology.ChannelCount.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  topology_bundle_position_count=" + fixture.Topology.BundlePositionCount.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  inventory_slot_count=" + fixture.Inventory.SlotCount.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  inventory_occupied_count=" + fixture.Inventory.OccupiedCount.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  data_pack_version=" + fixture.DataPack.DataPackVersion);
            WriteLine(output, "  simulation_time_s=" + FormatDouble(fixture.Configuration.InitialSimulationTimeSeconds));
            WriteLine(output, "  core_state_version=" + fixture.Configuration.InitialCoreStateVersion.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  spatial_state_version=" + fixture.Configuration.InitialSpatialStateVersion.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  power_snapshot_version=" + fixture.Configuration.InitialPowerSnapshotVersion.ToString(CultureInfo.InvariantCulture));
            WriteLine(output, "  paused=" + session.IsPaused.ToString().ToLowerInvariant());
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
            return Fail(error, "CLI.Run.Missing", "start a run with 'new run' before inspecting or pausing.");
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

        private sealed class CliSession
        {
            public CliSession(SyntheticCoreFixture fixture)
            {
                Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
            }

            public SyntheticCoreFixture Fixture { get; }

            public bool IsPaused { get; private set; }

            public void Pause()
            {
                IsPaused = true;
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
