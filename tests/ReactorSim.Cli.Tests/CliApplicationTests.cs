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
        Assert.Contains("run: created", first.output);
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
            "inspect core\nnew run\ninspect channel nope\ninspect bundle 8 0\nunknown\nquit\n");

        Assert.Equal(2, result.exitCode);
        Assert.Contains("CLI.Run.Missing", result.error);
        Assert.Contains("CLI.Command.Argument.Invalid", result.error);
        Assert.Contains("CLI.Command.Argument.OutOfRange", result.error);
        Assert.Contains("CLI.Command.Unknown", result.error);
        Assert.Contains("bye", result.output);
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

    private static (int exitCode, string output, string error) Run(string commands)
    {
        using var input = new StringReader(commands);
        using var output = new StringWriter(new StringBuilder());
        using var error = new StringWriter(new StringBuilder());
        int exitCode = CliApplication.Run(input, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }
}
