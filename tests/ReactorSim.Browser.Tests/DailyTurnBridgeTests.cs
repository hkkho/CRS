using System.Text.Json;
using ReactorSim.Browser;
using Xunit;

namespace ReactorSim.Browser.Tests;

public sealed class DailyTurnBridgeTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
    [Fact]
    public void DefaultCapabilityAndCompactDayResultAreAuthoritative()
    {
        Assert.Contains("commit-day", PlaytestRuntime.GetCapabilities());
        var runtime = new PlaytestRuntime();
        var initial = Parse(runtime.Initialize("{\"protocol\":\"candu-playtest-v2\",\"seed\":1001}"));
        Assert.Equal("daily-turn", initial.GetProperty("snapshot").GetProperty("pacingMode").GetString());
        Assert.True(initial.GetProperty("snapshot").GetProperty("isPaused").GetBoolean());
        var progress = Parse(runtime.BeginDailyDispatchJson("{\"protocol\":\"candu-playtest-v2\",\"responseMode\":\"compact\",\"baseSequence\":0,\"type\":\"commit-day\",\"expectedCompletedDays\":0,\"channelIndices\":[]}"));
        Assert.False(progress.GetProperty("completed").GetBoolean());
        Assert.Equal(0, progress.GetProperty("simulationSecondsAdvanced").GetDouble());
        Assert.Equal(0, Parse(runtime.GetSnapshotJson()).GetProperty("simulationTimeSeconds").GetDouble());
        Assert.False(Parse(runtime.DispatchJson("{\"protocol\":\"candu-playtest-v2\",\"type\":\"reset\"}")).GetProperty("accepted").GetBoolean());
        Assert.False(Parse(runtime.Initialize("{\"protocol\":\"candu-playtest-v2\"}")).GetProperty("accepted").GetBoolean());
        progress = Parse(runtime.ContinueDailyDispatchJson());
        Assert.True(progress.GetProperty("completed").GetBoolean());
        var day = Parse(progress.GetProperty("responseJson").GetString()!);
        Assert.True(day.GetProperty("accepted").GetBoolean());
        var patch = day.GetProperty("snapshotPatch");
        Assert.Equal(86400, patch.GetProperty("simulationTimeSeconds").GetDouble());
        Assert.Equal(1, patch.GetProperty("completedDays").GetInt32());
        var full = Parse(runtime.GetSnapshotJson());
        Assert.Equal(patch.GetProperty("lastDayResult").GetRawText(), full.GetProperty("lastDayResult").GetRawText());
        var repeat = Parse(runtime.DispatchJson("{\"protocol\":\"candu-playtest-v2\",\"type\":\"commit-day\",\"expectedCompletedDays\":0,\"channelIndices\":[]}"));
        Assert.False(repeat.GetProperty("accepted").GetBoolean());
        Assert.Equal(86400, repeat.GetProperty("snapshot").GetProperty("simulationTimeSeconds").GetDouble());
        var reset = Parse(runtime.DispatchJson("{\"protocol\":\"candu-playtest-v2\",\"type\":\"reset\"}"));
        Assert.Equal("daily-turn", reset.GetProperty("snapshot").GetProperty("pacingMode").GetString());
        Assert.Equal(0, reset.GetProperty("snapshot").GetProperty("completedDays").GetInt32());
    }

    [Theory]
    [InlineData("{\"type\":\"commit-day\",\"expectedCompletedDays\":0,\"channelIndices\":[210,210]}")]
    [InlineData("{\"type\":\"commit-day\",\"expectedCompletedDays\":0,\"channelIndices\":[-1]}")]
    [InlineData("{\"type\":\"commit-day\",\"expectedCompletedDays\":0,\"channelIndices\":[380]}")]
    [InlineData("{\"type\":\"commit-day\",\"expectedCompletedDays\":0,\"channelIndices\":[1.5]}")]
    [InlineData("{\"type\":\"step\",\"simulationSeconds\":86400}")]
    [InlineData("{\"type\":\"resume\"}")]
    public void MalformedAndBypassCommandsCannotConsumeTimeOrFuel(string command)
    {
        var runtime = new PlaytestRuntime();
        runtime.Initialize("{\"protocol\":\"candu-playtest-v2\"}");
        var response = Parse(runtime.DispatchJson("{\"protocol\":\"candu-playtest-v2\",\"payload\":" + command + "}"));
        Assert.False(response.GetProperty("accepted").GetBoolean());
        Assert.Equal(0, response.GetProperty("snapshot").GetProperty("simulationTimeSeconds").GetDouble());
        Assert.Equal(0, response.GetProperty("snapshot").GetProperty("shift").GetProperty("fuelConsumed").GetInt32());
    }
}
