using System.Text.Json;
using ReactorSim.Browser;
using Xunit;

namespace ReactorSim.Browser.Tests;

public sealed class DemoModeTests
{
    [Fact]
    public void TimeBasedRefuelsImmediatelyAndAdvancesAtomicHalfDaysWithCompactSnapshots()
    {
        var runtime = new PlaytestRuntime();
        var initial = Parse(runtime.Initialize("{\"protocol\":\"candu-playtest-v2\",\"pacingMode\":\"demo\",\"seed\":1001}"));
        Assert.True(initial.GetProperty("accepted").GetBoolean());
        Assert.Equal("demo", initial.GetProperty("snapshot").GetProperty("pacingMode").GetString());
        var invalid = Send(runtime, "\"type\":\"commit-refuel\",\"request\":{\"channelIndex\":380,\"directionId\":\"toward-end-a\",\"shiftCount\":8,\"fuelTypeId\":\"NAT-U-SYNTHETIC\"}");
        Assert.False(invalid.GetProperty("accepted").GetBoolean());
        Assert.Equal(0, invalid.GetProperty("snapshot").GetProperty("shift").GetProperty("fuelConsumed").GetInt32());
        var fuel = Send(runtime, "\"type\":\"commit-refuel\",\"request\":{\"channelIndex\":210,\"directionId\":\"toward-end-a\",\"shiftCount\":8,\"fuelTypeId\":\"NAT-U-SYNTHETIC\"}");
        Assert.True(fuel.GetProperty("accepted").GetBoolean());
        Assert.Equal(0, fuel.GetProperty("snapshot").GetProperty("simulationTimeSeconds").GetDouble());
        Assert.Equal(8, fuel.GetProperty("snapshot").GetProperty("shift").GetProperty("fuelConsumed").GetInt32());
        var day = Send(runtime, "\"type\":\"commit-day\",\"expectedCompletedDays\":0,\"channelIndices\":[],\"responseMode\":\"compact\",\"baseSequence\":" + fuel.GetProperty("sequence").GetUInt64());
        Assert.True(day.GetProperty("accepted").GetBoolean());
        var patch = day.GetProperty("snapshotPatch");
        Assert.Equal("demo", patch.GetProperty("pacingMode").GetString());
        Assert.Equal(0, patch.GetProperty("completedDays").GetInt32());
        Assert.Equal(43200, patch.GetProperty("simulationTimeSeconds").GetDouble());
        Assert.Equal(8, patch.GetProperty("shift").GetProperty("fuelConsumed").GetInt32());
        var second = Send(runtime, "\"type\":\"commit-day\",\"expectedCompletedDays\":0,\"channelIndices\":[]");
        Assert.True(second.GetProperty("accepted").GetBoolean());
        Assert.Equal(86400, second.GetProperty("snapshot").GetProperty("simulationTimeSeconds").GetDouble());
        Assert.Equal(1, second.GetProperty("snapshot").GetProperty("completedDays").GetInt32());
        Assert.False(Send(runtime, "\"type\":\"commit-day\",\"expectedCompletedDays\":0,\"channelIndices\":[]").GetProperty("accepted").GetBoolean());
        var reset = Send(runtime, "\"type\":\"reset\"");
        Assert.Equal("demo", reset.GetProperty("snapshot").GetProperty("pacingMode").GetString());
        Assert.Equal(0, reset.GetProperty("snapshot").GetProperty("completedDays").GetInt32());
        var daily = Send(runtime, "\"type\":\"reset\",\"pacingMode\":\"daily-turn\"");
        Assert.Equal("daily-turn", daily.GetProperty("snapshot").GetProperty("pacingMode").GetString());
        Assert.False(Send(runtime, "\"type\":\"commit-refuel\",\"request\":{\"channelIndex\":210,\"directionId\":\"toward-end-a\",\"shiftCount\":8,\"fuelTypeId\":\"NAT-U-SYNTHETIC\"}").GetProperty("accepted").GetBoolean());
    }

    private static JsonElement Send(PlaytestRuntime runtime, string command) => Parse(runtime.DispatchJson("{\"protocol\":\"candu-playtest-v2\"," + command + "}"));
    private static JsonElement Parse(string json) { using var doc = JsonDocument.Parse(json); return doc.RootElement.Clone(); }
}
