using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Browser.Tests;

public sealed partial class PlaytestBridgeTests
{
    private static string CustomPhysicsRequest(string overrides) =>
        """{"protocol":"candu-playtest-v2","mode":"play","seed":1001,"pacingMode":"demo","customPhysics":""" + overrides + "}";

    [Fact]
    public void CustomPhysicsChangesTheAuthoritativePowerAndReplaysWithTheSameModel()
    {
        const string overrides = """{"practice.thermal_power_multiplier":0.98,"practice.iodine_half_life_multiplier":1.05}""";
        var runtime = new PlaytestRuntime();
        var initialized = Parse(runtime.Initialize(CustomPhysicsRequest(overrides)));
        AssertAccepted(initialized);
        var snapshot = initialized.GetProperty("snapshot");
        string id = snapshot.GetProperty("dataPackId").GetString()!;
        Assert.StartsWith(FullCoreDiffusionDataPackV1.CustomPracticeVersionPrefix, id);
        Assert.True(snapshot.GetProperty("provenance").GetProperty("isModified").GetBoolean());
        Assert.False(snapshot.GetProperty("provenance").GetProperty("eligibleForStandardChallenge").GetBoolean());
        double expectedPower = 2_064_000_000 * 0.98;
        Assert.Equal(expectedPower, snapshot.GetProperty("physics").GetProperty("referencePowerWatts").GetDouble());
        Assert.Equal(expectedPower, snapshot.GetProperty("ripple").GetProperty("referenceChannelPowerWatts")
            .EnumerateArray().Sum(value => value.GetDouble()), 3);

        string day = CompactCommand(0, "commit-day", "\"expectedCompletedDays\":0,\"channelIndices\":[]");
        var advanced = Parse(runtime.Dispatch(day));
        AssertAccepted(advanced);
        Assert.Equal(id, advanced.GetProperty("snapshotPatch").GetProperty("dataPackId").GetString());
        var reset = Parse(runtime.Dispatch("""{"protocol":"candu-playtest-v2","type":"reset","seed":1001}"""));
        AssertAccepted(reset);
        Assert.Equal(id, reset.GetProperty("snapshot").GetProperty("dataPackId").GetString());
        Assert.Equal(snapshot.GetProperty("core").GetRawText(), reset.GetProperty("snapshot").GetProperty("core").GetRawText());

        var replay = new PlaytestRuntime();
        AssertAccepted(Parse(replay.Initialize(CustomPhysicsRequest(overrides))));
        Assert.Equal(advanced.GetProperty("stateDigest").GetString(),
            Parse(replay.Dispatch(day)).GetProperty("stateDigest").GetString());
    }

    [Theory]
    [InlineData("""{"practice.iodine_half_life_multiplier":0}""", "PracticeRange")]
    [InlineData("""{"coefficient_tables[0].rows[1].burnup_j_per_kg_hm":0}""", "StrictlyIncreasing")]
    [InlineData("""{"channel_count":381}""", "Parameter.Invalid")]
    [InlineData("""{"solver.maximum_iterations":1601}""", "Iterations.Invalid")]
    [InlineData("""{"solver.maximum_iterations":1}""", "could not initialize")]
    public void InvalidPhysicsKeepsTheExistingRun(string overrides, string error)
    {
        var runtime = new PlaytestRuntime();
        string before = runtime.GetSnapshotJson();
        var rejected = Parse(runtime.Initialize(CustomPhysicsRequest(overrides)));
        Assert.False(rejected.GetProperty("accepted").GetBoolean());
        Assert.Contains(error, rejected.GetProperty("message").GetString());
        Assert.Equal(before, runtime.GetSnapshotJson());
    }
}
