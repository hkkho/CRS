using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class JsonSerializationCompatibilityProbeTests
{
    [Fact]
    public void ManualJsonTokenPathRoundTrips()
    {
        string payload = JsonSerializationCompatibilityProbe.Run();

        Assert.Equal("{\"probe\":\"reactor-sim\",\"value\":42}", payload);
    }
}
