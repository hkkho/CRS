using ReactorSim.Browser;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Browser.Tests;

public sealed class WebRuntimeDependencyTests
{
    [Fact]
    public void WebRuntimeContainsOnlyTheSupportedCpuBridge()
    {
        var core = typeof(EquilibriumCoreProjectionV1).Assembly;
        Assert.Null(core.GetType("ReactorSim.Core.GpuSpatialPrototypeFixtureV1"));
        Assert.DoesNotContain(core.GetManifestResourceNames(), name => name.EndsWith(".wgsl", StringComparison.Ordinal));
        Assert.Null(typeof(PlaytestBridgeV2).GetMethod("GetGpuPrototypeFixtureJson"));
        Assert.Null(typeof(PlaytestRuntime).GetMethod("GetGpuPrototypeFixtureJson"));
        Assert.DoesNotContain(core.GetReferencedAssemblies(), assembly => assembly.Name == "ReactorSim.Core.Research");
    }
}
