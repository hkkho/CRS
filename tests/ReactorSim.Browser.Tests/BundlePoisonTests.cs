using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ReactorSim.Browser;
using Xunit;

namespace ReactorSim.Browser.Tests;

public sealed partial class PlaytestBridgeTests
{
    [Theory]
    [InlineData(210, "toward-end-a")]
    [InlineData(211, "toward-end-b")]
    public void BundlePoisonSurvivesTransportAndFreshFuelStartsAtZero(int channel, string direction)
    {
        var runtime = new PlaytestRuntime("real-time");
        JsonElement before = Parse(runtime.Initialize(PlayRequest)).GetProperty("snapshot");
        var old = ReadBundlePoison(before.GetProperty("core"), before.GetProperty("xenon"));
        Assert.All(old.Values, pair => { Assert.True(pair.I > 0); Assert.True(pair.X > 0); });
        var paused = Parse(runtime.Dispatch(CompactCommand(0, "pause")));
        AssertAccepted(paused);
        var request = JsonSerializer.Serialize(new
        {
            channelIndex = channel,
            directionId = direction,
            shiftCount = 8,
            fuelTypeId = "NAT-U-SYNTHETIC"
        });
        var committed = Parse(runtime.Dispatch(CompactCommand(1, "commit-refuel", "\"request\":" + request)));
        AssertAccepted(committed);
        var core = committed.GetProperty("coreReplacement");
        var poison = committed.GetProperty("snapshotPatch").GetProperty("xenon");
        var moved = ReadBundlePoison(core, poison);
        var inserted = moved.Keys.Except(old.Keys).ToArray();
        Assert.Equal(8, inserted.Length);
        Assert.All(inserted, id => Assert.Equal((0.0, 0.0), moved[id]));
        foreach (var id in moved.Keys.Intersect(old.Keys)) Assert.Equal(old[id], moved[id]);
        foreach (var summary in core.GetProperty("channels").EnumerateArray())
        {
            var pairs = summary.GetProperty("bundles").EnumerateArray()
                .Select(bundle => moved[bundle.GetProperty("bundleId").GetString()!]).ToArray();
            Assert.InRange(System.Math.Abs(pairs.Average(p => p.I) / summary.GetProperty("xenon").GetProperty("meanI135NumberDensityM3").GetDouble() - 1), 0, 1e-12);
            Assert.InRange(System.Math.Abs(pairs.Average(p => p.X) / summary.GetProperty("xenon").GetProperty("meanXe135NumberDensityM3").GetDouble() - 1), 0, 1e-12);
        }
        AssertAccepted(Parse(runtime.Dispatch(CompactCommand(2, "resume"))));
        var tick = Parse(runtime.Dispatch(CompactCommand(3, "advance", "\"wallMilliseconds\":100")));
        AssertAccepted(tick);
        Assert.True(tick.TryGetProperty("coreReplacement", out var refreshedCore));
        var latest = ReadBundlePoison(refreshedCore, tick.GetProperty("snapshotPatch").GetProperty("xenon"));
        Assert.All(inserted, id => { Assert.True(latest[id].I > 0); Assert.True(latest[id].X > 0); });
        var exact = Parse(runtime.GetSnapshotJson());
        Assert.Equal(exact.GetProperty("xenon").GetRawText(), tick.GetProperty("snapshotPatch").GetProperty("xenon").GetRawText());
    }

    private static Dictionary<string, (double I, double X)> ReadBundlePoison(JsonElement core, JsonElement poison)
    {
        var iodine = poison.GetProperty("nodeI135NumberDensityM3");
        var xenon = poison.GetProperty("nodeXe135NumberDensityM3");
        Assert.Equal(4560, iodine.GetArrayLength());
        Assert.Equal(4560, xenon.GetArrayLength());
        var pairs = new Dictionary<string, (double I, double X)>();
        foreach (var channel in core.GetProperty("channels").EnumerateArray())
            foreach (var bundle in channel.GetProperty("bundles").EnumerateArray())
            {
                int node = channel.GetProperty("channelIndex").GetInt32() * 12 + bundle.GetProperty("position").GetInt32();
                pairs.Add(bundle.GetProperty("bundleId").GetString()!, (iodine[node].GetDouble(), xenon[node].GetDouble()));
            }
        return pairs;
    }
}
