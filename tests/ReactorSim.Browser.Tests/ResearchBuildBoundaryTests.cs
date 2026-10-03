using ReactorSim.Browser;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Browser.Tests;

public sealed class ResearchBuildBoundaryTests
{
    [Fact]
    public void GpuResourcesTypesAndBridgeSurfaceFollowTheExplicitBuildOption()
    {
#if RESEARCH_EXPERIMENTS
        const bool expected = true;
#else
        const bool expected = false;
#endif
        var core = typeof(EquilibriumCoreProjectionV1).Assembly;
        Assert.Equal(expected, core.GetType("ReactorSim.Core.GpuSpatialPrototypeFixtureV1") != null);
        Assert.Equal(expected, core.GetManifestResourceNames().Any(name => name.EndsWith("spatial-prototype-v1.wgsl", StringComparison.Ordinal)));
        Assert.Equal(expected, typeof(PlaytestBridgeV2).GetMethod("GetGpuPrototypeFixtureJson") != null);
        Assert.Equal(expected, typeof(PlaytestRuntime).GetMethod("GetGpuPrototypeFixtureJson") != null);
    }
}
