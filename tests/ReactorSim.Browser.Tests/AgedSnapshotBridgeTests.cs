using System.Text.Json;
using ReactorSim.Browser;
using Xunit;

namespace ReactorSim.Browser.Tests;

public sealed partial class PlaytestBridgeTests
{
    [Fact]
    public void SeededInitializeAndResetAreReproducibleAndInvalidSeedsAreAtomic()
    {
        string Init(uint seed) => PlaytestBridgeV2.Initialize(
            $"{{\"protocol\":\"candu-playtest-v2\",\"mode\":\"play\",\"seed\":{seed}}}");
        string Digest(string response) => JsonDocument.Parse(response).RootElement.GetProperty("stateDigest").GetString()!;
        string Core(string response) => JsonDocument.Parse(response).RootElement.GetProperty("snapshot").GetProperty("core").GetRawText();
        string first = Digest(Init(1001));
        string secondResponse = Init(1002);
        string second = Digest(secondResponse);
        Assert.NotEqual(first, second);
        Assert.Equal(first, Digest(Init(1001)));
        string reset = PlaytestBridgeV2.Dispatch("{\"protocol\":\"candu-playtest-v2\",\"type\":\"reset\",\"seed\":1002}");
        Assert.Equal(Core(secondResponse), Core(reset));
        string resetDigest = Digest(reset);
        Assert.Equal(resetDigest, Digest(PlaytestBridgeV2.Dispatch("{\"protocol\":\"candu-playtest-v2\",\"type\":\"reset\"}")));
        foreach (string seed in new[] { "-1", "1.5", "4294967296", "\"1001\"", "null" })
        {
            using var rejected = JsonDocument.Parse(PlaytestBridgeV2.Dispatch(
                $"{{\"protocol\":\"candu-playtest-v2\",\"type\":\"reset\",\"seed\":{seed}}}"));
            Assert.False(rejected.RootElement.GetProperty("accepted").GetBoolean());
            Assert.Equal(resetDigest, rejected.RootElement.GetProperty("stateDigest").GetString());
            using var rejectedInit = JsonDocument.Parse(PlaytestBridgeV2.Initialize(
                $"{{\"protocol\":\"candu-playtest-v2\",\"mode\":\"play\",\"seed\":{seed}}}"));
            Assert.False(rejectedInit.RootElement.GetProperty("ok").GetBoolean());
        }
    }
}
