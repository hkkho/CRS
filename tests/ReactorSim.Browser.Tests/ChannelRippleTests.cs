using System;
using System.Linq;
using System.Text.Json;
using Xunit;
using ReactorSim.Browser;

namespace ReactorSim.Browser.Tests;

public sealed partial class PlaytestBridgeTests
{
    [Fact]
    public void FullAndCompactSnapshotsCarryFixedReferenceAndLiveRipple()
    {
        var runtime = new PlaytestRuntime();
        var before = Parse(runtime.Initialize(PlayRequest)).GetProperty("snapshot");
        var reference = before.GetProperty("ripple").GetProperty("referenceChannelPowerWatts");
        Assert.Equal(380, reference.GetArrayLength());
        Assert.Equal(2_064_000_000, reference.EnumerateArray().Sum(p => p.GetDouble()), 3);
        Assert.Equal("practice-channel-ripple-v3", before.GetProperty("scorePolicyId").GetString());
        var tick = Parse(runtime.Dispatch(CompactCommand(0, "advance", "\"wallMilliseconds\":100")));
        AssertAccepted(tick);
        Assert.False(tick.TryGetProperty("coreReplacement", out _));
        var measurements = tick.GetProperty("coreMeasurements");
        var fullAfter = Parse(runtime.GetSnapshotJson());
        Assert.Equal(4560, measurements.GetProperty("bundleBurnupMwdPerKg").GetArrayLength());
        Assert.Equal(fullAfter.GetProperty("core").GetProperty("channels")[0].GetProperty("bundles")[0].GetProperty("currentBurnupMwdPerKg").GetDouble(),
            measurements.GetProperty("bundleBurnupMwdPerKg")[0].GetDouble());
        var patch = tick.GetProperty("snapshotPatch");
        var ripple = patch.GetProperty("ripple");
        Assert.Equal(reference.GetRawText(), ripple.GetProperty("referenceChannelPowerWatts").GetRawText());
        Assert.Equal(380, ripple.GetProperty("channelRippleFractions").GetArrayLength());
        double sum = ripple.GetProperty("channelRippleFractions").EnumerateArray().Sum(p => Math.Pow(p.GetDouble() - 1, 2));
        Assert.Equal(Math.Sqrt(sum / 380), ripple.GetProperty("rmsDeviationFraction").GetDouble(), 12);
        Assert.Equal(before.GetProperty("ripple").GetProperty("pointsPerHour").GetDouble() * 180 / 3600,
            patch.GetProperty("scoreTotal").GetDouble(), 12);
        var exact = Parse(runtime.GetSnapshotJson());
        Assert.Equal(exact.GetProperty("ripple").GetRawText(), ripple.GetRawText());
    }
}
