using System;
using System.IO;
using System.Text;
using ReactorSim.Cli;
using Xunit;

namespace ReactorSim.Cli.Tests;

public sealed class CliApplicationTests
{
    [Fact]
    public void NewRunInspectionAndPauseProduceStableText()
    {
        const string commands = "help\nnew run\ninspect core\ninspect channel 1\ninspect bundle 0 2\npause\nquit\n";

        (int exitCode, string output, string error) first = Run(commands);
        (int exitCode, string output, string error) second = Run(commands);

        Assert.Equal(0, first.exitCode);
        Assert.Equal(string.Empty, first.error);
        Assert.Equal(first.output, second.output);
        Assert.Equal(first.error, second.error);
        Assert.DoesNotContain("\r", first.output);
        Assert.DoesNotContain("\r", first.error);
        Assert.Contains("run: created", first.output);
        Assert.Contains("run_kind=synthetic-scenario", first.output);
        Assert.Contains("scenario_id=tutorial-equilibrium", first.output);
        Assert.Contains("acceleration_factor=10", first.output);
        Assert.Contains("approved_scoring_parameter_sha256=", first.output);
        Assert.Contains("topology_channel_count=2", first.output);
        Assert.Contains("flow_direction=EndBtoEndA", first.output);
        Assert.Contains("material_variant_id=synthetic-fuel", first.output);
        Assert.Contains("paused=true", first.output);
        Assert.Contains("bye", first.output);
    }

    [Fact]
    public void InspectionBeforeRunAndMalformedArgumentsFailClosed()
    {
        (int exitCode, string output, string error) result = Run(
            "inspect core\nnew run\ninspect core\ninspect channel -1\ninspect channel 4294967296\ninspect bundle 0 4294967296\ninspect bundle 8 0\ninspect core\nunknown\nquit\n");

        Assert.Equal(2, result.exitCode);
        Assert.Contains("CLI.Run.Missing", result.error);
        Assert.Contains("CLI.Command.Argument.Invalid", result.error);
        Assert.Contains("CLI.Command.Argument.OutOfRange", result.error);
        Assert.Contains("CLI.Command.Unknown", result.error);
        Assert.Equal(2, CountOccurrences(result.output, "core:\n"));
        Assert.Contains("bye", result.output);
    }

    [Fact]
    public void InteractiveModeUsesPromptAndKeepsErrorsOffStdout()
    {
        using var input = new StringReader("new run\ninspect channel -1\nquit\n");
        using var output = new StringWriter(new StringBuilder());
        using var error = new StringWriter(new StringBuilder());

        int exitCode = CliApplication.RunInteractive(input, output, error);

        Assert.Equal(2, exitCode);
        Assert.Contains("reactor> run: created\n", output.ToString());
        Assert.DoesNotContain("error[", output.ToString());
        Assert.Contains("error[CLI.Command.Argument.Invalid]", error.ToString());
        Assert.DoesNotContain("\r", output.ToString());
        Assert.DoesNotContain("\r", error.ToString());
    }

    [Fact]
    public void NullStreamsAreRejectedBeforeCommandProcessing()
    {
        using var output = new StringWriter(new StringBuilder());
        using var error = new StringWriter(new StringBuilder());

        Assert.Throws<ArgumentNullException>(() => CliApplication.Run(null!, output, error));
        Assert.Throws<ArgumentNullException>(() => CliApplication.Run(new StringReader("quit\n"), null!, error));
        Assert.Throws<ArgumentNullException>(() => CliApplication.Run(new StringReader("quit\n"), output, null!));
    }

    [Fact]
    public void QuitStopsProcessingFollowingCommands()
    {
        (int exitCode, string output, string error) result = Run("quit\nnew run\n");

        Assert.Equal(0, result.exitCode);
        Assert.Equal(string.Empty, result.error);
        Assert.Contains("bye", result.output);
        Assert.DoesNotContain("run: created", result.output);
    }

    [Fact]
    public void ApprovedScenarioAdvancesAtTenTimesAndCommitsActionsAtTheNextTick()
    {
        (int exitCode, string output, string error) result = Run(
            "new run tutorial-equilibrium\nadvance wall 50\nset power target 0.95\nadvance wall 50\ninspect core\nquit\n");

        Assert.Equal(0, result.exitCode);
        Assert.Equal(string.Empty, result.error);
        Assert.Contains("control_ticks_processed=0", result.output);
        Assert.Contains("control_ticks_processed=1", result.output);
        Assert.Contains("simulation_time_s=1", result.output);
        Assert.Contains("action_queue_delay_wall_s=", result.output);
        Assert.Contains("normalized_power_fraction=0.95", result.output);
    }

    [Fact]
    public void BoundaryScenarioRecordsLossWithoutSafetySystemBehavior()
    {
        (int exitCode, string output, string error) result = Run(
            "new run operating-envelope-boundary\nadvance wall 1000\nquit\n");

        Assert.Equal(0, result.exitCode);
        Assert.Equal(string.Empty, result.error);
        Assert.Contains("outcome=RecordLoss", result.output);
        Assert.Contains("loss_id=power_above_maximum", result.output);
        Assert.Contains("loss_time_s=10", result.output);
        Assert.DoesNotContain("scram", result.output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("shutdown", result.output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AdvanceAndScoreInspectionExposeCauseEffectAndComponentScores()
    {
        (int exitCode, string output, string error) result = Run(
            "new run tutorial-equilibrium\nadvance wall 100\ninspect score\nquit\n");

        Assert.Equal(0, result.exitCode);
        Assert.Equal(string.Empty, result.error);
        Assert.Contains("turn_id=1", result.output);
        Assert.Contains("turn_cause=elapsed_play", result.output);
        Assert.Contains("score_total=", result.output);
        Assert.Contains("energy_quality=", result.output);
        Assert.Contains("stability_quality=", result.output);
        Assert.Contains("fuelling_efficiency=", result.output);
        Assert.Contains("score:", result.output);
        Assert.Contains("approved_scoring_parameter_sha256=", result.output);
        Assert.DoesNotContain("neutron", result.output, StringComparison.OrdinalIgnoreCase);
    }

    private static (int exitCode, string output, string error) Run(string commands)
    {
        using var input = new StringReader(commands);
        using var output = new StringWriter(new StringBuilder());
        using var error = new StringWriter(new StringBuilder());
        int exitCode = CliApplication.Run(input, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }

    private static int CountOccurrences(string value, string token)
    {
        int count = 0;
        int start = 0;
        while ((start = value.IndexOf(token, start, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            start += token.Length;
        }

        return count;
    }
}
