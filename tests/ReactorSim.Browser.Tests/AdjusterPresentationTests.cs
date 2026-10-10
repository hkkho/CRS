using System.Linq;
using ReactorSim.Browser;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Browser.Tests;

public sealed partial class PlaytestBridgeTests
{
    [Fact]
    public void FullAndReplacementSnapshotsCarryCoreAuthoredAdjusterGeometry()
    {
        var runtime = new PlaytestRuntime("real-time");
        var core = Parse(runtime.Initialize(PlayRequest)).GetProperty("snapshot").GetProperty("core");
        var rods = core.GetProperty("adjusters").EnumerateArray().ToArray();
        Assert.Equal(21, rods.Length);
        var tubes = core.GetProperty("liquidZoneTubes").EnumerateArray().ToArray();
        Assert.Equal(14, tubes.Length);
        Assert.Equal(6, tubes.Select(t => (t.GetProperty("gridColumn").GetDouble(), t.GetProperty("axialPosition").GetDouble())).Distinct().Count());
        Assert.All(tubes, t => Assert.Equal(new[] { t.GetProperty("zoneId").GetInt32() < 7 ? 2 : 8,
            t.GetProperty("zoneId").GetInt32() < 7 ? 3 : 9 }, t.GetProperty("bundlePositions").EnumerateArray().Select(p => p.GetInt32())));
        foreach (var rod in rods)
        {
            int id = rod.GetProperty("id").GetInt32();
            var source = PracticeAdjustersV1.Rods.Single(r => r.Id == id);
            Assert.Equal(10.5 + source.HorizontalCentreM / PracticeAdjustersV1.LatticePitchM,
                rod.GetProperty("gridColumn").GetDouble(), 12);
            Assert.Equal(source.AxialCentreM / PracticeAdjustersV1.BundleLengthM - .5,
                rod.GetProperty("axialPosition").GetDouble(), 12);
            Assert.Equal(4.5, rod.GetProperty("gridRowStart").GetDouble(), 12);
            Assert.Equal(16.5, rod.GetProperty("gridRowEnd").GetDouble(), 12);
            var cells = PracticeAdjustersV1.Cells.Where(c => c.RodId == id).ToArray();
            Assert.Equal(cells.Select(c => c.Node.ChannelId.Value).Distinct().OrderBy(c => c),
                rod.GetProperty("affectedChannels").EnumerateArray().Select(c => c.GetUInt32()));
            Assert.Equal(cells.Select(c => c.Node.Position.Value).Distinct().OrderBy(p => p),
                rod.GetProperty("bundlePositions").EnumerateArray().Select(p => p.GetUInt32()));
        }
        var compact = Parse(runtime.Dispatch(CompactCommand(0, "commit-refuel", "\"request\":" + PlayRefuelRequest)));
        Assert.True(compact.GetProperty("accepted").GetBoolean());
        Assert.Equal(core.GetProperty("adjusters").GetRawText(),
            compact.GetProperty("coreReplacement").GetProperty("adjusters").GetRawText());
    }
}
