using System.IO;
using ReactorSim.TestInfrastructure;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class TestDataLocatorTests
{
    [Fact]
    public void DeclaredAssetResolvesFromTheTestOutputDirectory()
    {
        string path = TestDataLocator.RequireRepositoryFile(
            "data/packs/p6-t02-synthetic-liquid-zone-map-v1.json");

        Assert.True(File.Exists(path));
        Assert.Contains(
            "TestData",
            Path.GetRelativePath(AppContext.BaseDirectory, path));
    }

    [Fact]
    public void MissingDeclaredAssetProducesAnActionableDiagnostic()
    {
        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() =>
            TestDataLocator.RequireFile(
                "TestData/missing/declared-fixture.json",
                "TEST-INFRA-01 declared fixture"));

        Assert.Contains("TEST-INFRA-01 declared fixture", exception.Message);
        Assert.Contains("AppContext.BaseDirectory", exception.Message);
        Assert.Contains("CopyToOutputDirectory", exception.Message);
    }
}
