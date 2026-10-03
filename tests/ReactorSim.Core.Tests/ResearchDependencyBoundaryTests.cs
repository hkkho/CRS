using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class ResearchDependencyBoundaryTests
{
    [Fact]
    public void GameplayAssembliesDoNotReferenceLegacyResearchImplementation()
    {
        foreach (var assembly in new[] { typeof(BundleInventory).Assembly, typeof(GameSession).Assembly })
        {
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(),
                reference => reference.Name == "ReactorSim.Core.Research");
        }
        var core = typeof(BundleInventory).Assembly;
        var research = typeof(KineticIntegrationTransitionV1).Assembly;
        Assert.Equal("ReactorSim.Core.Research", research.GetName().Name);
        foreach (var name in new[] { "KineticIntegrationTransitionV1", "StateArchiveCodecV1",
            "ReducedCorePowerModelV1", "P6T06QueueTransitionV1", "CompleteRefuellingTransitionV1" })
        {
            Assert.Null(core.GetType("ReactorSim.Core." + name));
            Assert.NotNull(research.GetType("ReactorSim.Core." + name));
        }
        Assert.Equal(core, typeof(CompleteStateDigestV1).Assembly);
        Assert.Equal(core, typeof(XenonSpatialCouplingV1).Assembly);
    }
}
