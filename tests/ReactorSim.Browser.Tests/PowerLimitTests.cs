using System.Reflection;
using System.Linq;
using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;
using ReactorSim.Browser;

namespace ReactorSim.Browser.Tests;

public sealed partial class PlaytestBridgeTests
{
    [Fact]
    public void PowerLimitEndingAndFrozenStateReachFullAndCompactBrowserSnapshots()
    {
        var runtime = new PlaytestRuntime();
        var initial = Parse(runtime.Initialize(PlayRequest)).GetProperty("snapshot");
        Assert.Equal("running", initial.GetProperty("runStatus").GetString());
        Assert.False(initial.GetProperty("rrs").GetProperty("isGameOver").GetBoolean());
        PrepareOverpowerFixture(runtime);
        Assert.True(Parse(runtime.Dispatch(CompactCommand(0, "queue-power-target", "\"targetFraction\":1.2"))).GetProperty("accepted").GetBoolean());
        var ending = Parse(runtime.Dispatch(CompactCommand(1, "advance", "\"wallMilliseconds\":100"))).GetProperty("snapshotPatch");
        Assert.Equal("Channel power exceeds 7,300 kW", ending.GetProperty("runEndReason").GetString());
        var rejected = Parse(runtime.Dispatch(CompactCommand(2, "advance", "\"wallMilliseconds\":100")));
        Assert.False(rejected.GetProperty("accepted").GetBoolean());
        var patch = rejected.GetProperty("snapshotPatch");
        Assert.Equal("ended", patch.GetProperty("runStatus").GetString());
        Assert.Equal(ending.GetProperty("runEndReason").GetString(), patch.GetProperty("runEndReason").GetString());
        Assert.Equal(ending.GetProperty("simulationTimeSeconds").GetDouble(), patch.GetProperty("simulationTimeSeconds").GetDouble());
        Assert.Equal(ending.GetProperty("scoreTotal").GetDouble(), patch.GetProperty("scoreTotal").GetDouble());
        Assert.Equal(ending.GetProperty("freshBundlesAvailable").GetUInt32(), patch.GetProperty("freshBundlesAvailable").GetUInt32());
    }
    private static void PrepareOverpowerFixture(PlaytestRuntime runtime)
    {
        // Isolate terminal serialization from the nominal pack's flatter peaks.
        var bridge = typeof(PlaytestRuntime).GetField("_runtimeInstance", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(runtime)!;
        var session = (GameSession)bridge.GetType().GetProperty("PlaySession")!.GetValue(bridge)!;
        double scale = 1.01 * 7_300_000 / (1.2 * session.CurrentEquilibriumProjection.ShapeChannelPowerWatts.Max());
        var model = FullCoreDiffusionModelV1.TryCreateCandu6(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6().Value).Value;
        var solver = EquilibriumCoreSolverV1.TryCreate(model, session.CoreState.EnumerateBundles(),
            PracticeGameSessionFactory.PracticeReferencePowerWatts * scale).Value;
        var overlay = PracticeLiquidZoneRrsMappingV1.TryCreateCandu6().Value
            .TryBuildOverlay(session.CurrentLiquidZoneRrs.ZoneFills).Value;
        var candidate = solver.TrySolveCandidate(session.CoreState.EnumerateBundles(), overlay);
        Assert.True(candidate.IsValid);
        Assert.True(solver.TryCommitCandidate(candidate.Value).IsValid);
        typeof(GameSession).GetField("_equilibriumSolver", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(session, solver);
    }

}
