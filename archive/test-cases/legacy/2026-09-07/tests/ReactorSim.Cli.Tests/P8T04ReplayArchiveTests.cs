using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ReactorSim.Cli;
using Xunit;

namespace ReactorSim.Cli.Tests;

public sealed class P8T04ReplayArchiveTests
{
    [Fact]
    public void SaveArchiveIsCanonicalAndReplayVerifiesTheSameDigest()
    {
        string path = CreateArchivePath();
        try
        {
            (int exitCode, string output, string error) first = Run(
                "new run tutorial-equilibrium play-accelerated-10x\n" +
                "set power target 0.95\n" +
                "advance wall 50\n" +
                "set tilt target 0.1\n" +
                "advance wall 50\n" +
                "pause\n" +
                "resume\n" +
                "save " + path + "\n" +
                "quit\n");

            Assert.Equal(0, first.exitCode);
            Assert.Equal(string.Empty, first.error);
            Assert.Contains("save: written", first.output);
            Assert.Contains("command_count=6", first.output);
            string firstDigest = ExtractLineValue(first.output, "final_state_digest=");
            string firstDocument = File.ReadAllText(path);

            (int exitCode, string output, string error) second = Run(
                "new run tutorial-equilibrium play-accelerated-10x\n" +
                "set power target 0.95\n" +
                "advance wall 50\n" +
                "set tilt target 0.1\n" +
                "advance wall 50\n" +
                "pause\n" +
                "resume\n" +
                "save " + path + "\n" +
                "quit\n");
            string secondDocument = File.ReadAllText(path);

            Assert.Equal(0, second.exitCode);
            Assert.Equal(string.Empty, second.error);
            Assert.Equal(firstDocument, secondDocument);
            Assert.NotEqual('\uFEFF', firstDocument[0]);
            Assert.True(firstDocument.IndexOf("\"format\"", StringComparison.Ordinal) <
                        firstDocument.IndexOf("\"schema_version\"", StringComparison.Ordinal));
            Assert.True(firstDocument.IndexOf("\"commands\"", StringComparison.Ordinal) <
                        firstDocument.IndexOf("\"expected_final_state_digest\"", StringComparison.Ordinal));

            (int exitCode, string output, string error) replay = Run(
                "replay " + path + "\nquit\n");

            Assert.Equal(0, replay.exitCode);
            Assert.Equal(string.Empty, replay.error);
            Assert.Contains("replay: verified", replay.output);
            Assert.Contains("command_count=6", replay.output);
            Assert.Equal(firstDigest, ExtractLineValue(replay.output, "final_state_digest="));
        }
        finally
        {
            DeleteArchive(path);
        }
    }

    [Fact]
    public void LoadRestoresStateAndDoesNotNeedAnExistingSession()
    {
        string path = CreateArchivePath();
        try
        {
            (int exitCode, string output, string error) saved = Run(
                "new run tutorial-equilibrium\n" +
                "set power target 0.95\n" +
                "advance wall 100\n" +
                "save " + path + "\n" +
                "quit\n");

            Assert.Equal(0, saved.exitCode);
            Assert.Equal(string.Empty, saved.error);
            string digest = ExtractLineValue(saved.output, "final_state_digest=");

            (int exitCode, string output, string error) loaded = Run(
                "load " + path + "\n" +
                "inspect core\n" +
                "inspect score\n" +
                "quit\n");

            Assert.Equal(0, loaded.exitCode);
            Assert.Equal(string.Empty, loaded.error);
            Assert.Contains("load: restored", loaded.output);
            Assert.Contains("final_state_digest=" + digest, loaded.output);
            Assert.Contains("simulation_time_s=1", loaded.output);
            Assert.Contains("normalized_power_fraction=0.95", loaded.output);
            Assert.Contains("turn_summary_count=1", loaded.output);
            Assert.Contains("score:", loaded.output);
        }
        finally
        {
            DeleteArchive(path);
        }
    }

    [Fact]
    public void FailedLoadIsAtomicAndLeavesTheCurrentSessionUnchanged()
    {
        string path = CreateArchivePath();
        try
        {
            (int exitCode, string output, string error) saved = Run(
                "new run tutorial-equilibrium\n" +
                "advance wall 50\n" +
                "save " + path + "\n" +
                "quit\n");
            Assert.Equal(0, saved.exitCode);
            Assert.Equal(string.Empty, saved.error);

            string document = File.ReadAllText(path);
            string digest = ExtractLineValue(saved.output, "final_state_digest=");
            File.WriteAllText(
                path,
                document.Replace(
                    digest,
                    new string('0', digest.Length),
                    StringComparison.Ordinal),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            (int exitCode, string output, string error) result = Run(
                "new run tutorial-equilibrium\n" +
                "advance wall 100\n" +
                "load " + path + "\n" +
                "inspect core\n" +
                "quit\n");

            Assert.Equal(2, result.exitCode);
            Assert.Contains("CLI.Load.Invalid", result.error);
            Assert.DoesNotContain("load: restored", result.output);
            Assert.Contains("simulation_time_s=1", result.output);
            Assert.Contains("wall_elapsed_s=0.1", result.output);
            Assert.Contains("turn_summary_count=1", result.output);
        }
        finally
        {
            DeleteArchive(path);
        }
    }

    [Fact]
    public void MalformedArchivesFailClosedBeforeReplay()
    {
        string path = CreateArchivePath();
        try
        {
            (int exitCode, string output, string error) saved = Run(
                "new run tutorial-equilibrium\n" +
                "set power target 0.95\n" +
                "advance wall 50\n" +
                "save " + path + "\n" +
                "quit\n");
            Assert.Equal(0, saved.exitCode);
            Assert.Equal(string.Empty, saved.error);
            string document = File.ReadAllText(path);

            var malformedDocuments = new List<string>
            {
                document.Replace(
                    "{\"format\":\"reactorsim.p8-replay-archive/v1\",\"schema_version\":1",
                    "{\"schema_version\":1,\"format\":\"reactorsim.p8-replay-archive/v1\"",
                    StringComparison.Ordinal),
                document.Replace(
                    "\"format\":\"reactorsim.p8-replay-archive/v1\"",
                    "\"format\":\"reactorsim.p8-replay-archive/v1\",\"format\":\"reactorsim.p8-replay-archive/v1\"",
                    StringComparison.Ordinal),
                document.Replace("\"commands\":", "\"unknown\":", StringComparison.Ordinal),
                document[..^1] + ",}",
                "/* rejected comment */" + document,
                document.Replace("\"set_power_target\"", "\"unsupported\"", StringComparison.Ordinal),
                document.Replace("\"target_fraction\":0.95", "\"target_fraction\":1e400", StringComparison.Ordinal),
                document.Replace(
                    "{\"kind\":\"set_power_target\",\"target_fraction\":0.95}",
                    "{\"target_fraction\":0.95,\"kind\":\"set_power_target\"}",
                    StringComparison.Ordinal),
                document.Replace(
                    ExtractScenarioDigest(document),
                    new string('0', 64),
                    StringComparison.Ordinal)
            };

            foreach (string malformedDocument in malformedDocuments)
            {
                File.WriteAllText(
                    path,
                    malformedDocument,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                (int exitCode, string output, string error) result = Run(
                    "replay " + path + "\nquit\n");

                Assert.Equal(2, result.exitCode);
                Assert.Contains("CLI.Replay.Invalid", result.error);
                Assert.DoesNotContain("replay: verified", result.output);
            }
        }
        finally
        {
            DeleteArchive(path);
        }
    }

    [Fact]
    public void TamperedInitialPlaybackModeFailsEvenWhenACommandResetsTheFinalMode()
    {
        string path = CreateArchivePath();
        try
        {
            (int exitCode, string output, string error) saved = Run(
                "new run tutorial-equilibrium play-accelerated-10x\n" +
                "set playback audit-real-time-1x\n" +
                "advance wall 50\n" +
                "save " + path + "\n" +
                "quit\n");
            Assert.Equal(0, saved.exitCode);
            Assert.Equal(string.Empty, saved.error);

            string document = File.ReadAllText(path).Replace(
                "\"initial_playback_mode_id\":\"play-accelerated-10x\"",
                "\"initial_playback_mode_id\":\"audit-real-time-1x\"",
                StringComparison.Ordinal);
            File.WriteAllText(
                path,
                document,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            (int exitCode, string output, string error) replay = Run(
                "replay " + path + "\nquit\n");

            Assert.Equal(2, replay.exitCode);
            Assert.Contains("CLI.Replay.Invalid", replay.error);
            Assert.Contains("final-state digest", replay.error);
            Assert.DoesNotContain("replay: verified", replay.output);
        }
        finally
        {
            DeleteArchive(path);
        }
    }

    [Fact]
    public void PauseZeroAdvanceAndResumeRemainDeterministicAtCommandBoundaries()
    {
        string path = CreateArchivePath();
        try
        {
            (int exitCode, string output, string error) result = Run(
                "new run tutorial-equilibrium\n" +
                "pause\n" +
                "advance wall 0\n" +
                "advance wall 50\n" +
                "resume\n" +
                "advance wall 50\n" +
                "save " + path + "\n" +
                "replay " + path + "\n" +
                "quit\n");

            Assert.Equal(0, result.exitCode);
            Assert.Equal(string.Empty, result.error);
            Assert.Contains("command_count=5", result.output);
            Assert.Equal(
                2,
                result.output.Split("final_state_digest=", StringSplitOptions.None).Length - 1);
            Assert.Contains("replay: verified", result.output);
        }
        finally
        {
            DeleteArchive(path);
        }
    }

    [Fact]
    public void ReplayCommandCapacityFailsClosedBeforeMutatingTheRuntime()
    {
        string path = CreateArchivePath();
        try
        {
            var commands = new StringBuilder("new run tutorial-equilibrium\n");
            for (int index = 0; index < 4097; index++)
            {
                commands.Append("pause").Append('\n');
            }

            commands.Append("save ").Append(path).Append('\n');
            commands.Append("inspect core\nquit\n");
            (int exitCode, string output, string error) result = Run(commands.ToString());

            Assert.Equal(2, result.exitCode);
            Assert.Contains("CLI.Replay.Capacity", result.error);
            Assert.Contains("save: written", result.output);
            Assert.Contains("command_count=4096", result.output);
            Assert.Contains("paused=true", result.output);
            Assert.True(File.Exists(path));
        }
        finally
        {
            DeleteArchive(path);
        }
    }

    private static (int exitCode, string output, string error) Run(string commands)
    {
        using var input = new StringReader(commands);
        using var output = new StringWriter(new StringBuilder());
        using var error = new StringWriter(new StringBuilder());
        int exitCode = CliApplication.Run(input, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }

    private static string CreateArchivePath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "reactorsim-p8-t04-" + Guid.NewGuid().ToString("N") + ".json");
    }

    private static string ExtractLineValue(string output, string prefix)
    {
        string? line = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal));
        Assert.NotNull(line);
        return line![prefix.Length..].TrimEnd('\r');
    }

    private static string ExtractScenarioDigest(string document)
    {
        const string prefix = "\"scenario_parameter_sha256\":\"";
        int start = document.IndexOf(prefix, StringComparison.Ordinal);
        Assert.True(start >= 0);
        start += prefix.Length;
        return document.Substring(start, 64);
    }

    private static void DeleteArchive(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
