using System.Text.Json;
using System.Text.Json.Nodes;
using ReactorSim.Browser;
using Xunit;

namespace ReactorSim.Browser.Tests;

public sealed partial class PlaytestBridgeTests
{
    [Fact]
    public void ZoneLayoutIsProjectedAndEditsAreValidatedAtomically()
    {
        var initial = JsonNode.Parse(PlaytestBridgeV2.Initialize("{\"mode\":\"play\"}"))!;
        var nodes = new JsonArray();
        foreach (var channel in initial["snapshot"]!["core"]!["channels"]!.AsArray())
            foreach (var bundle in channel!["bundles"]!.AsArray())
                nodes.Add(new JsonObject
                {
                    ["channelIndex"] = channel["channelIndex"]!.DeepClone(),
                    ["position"] = bundle!["position"]!.DeepClone(),
                    ["logicalZoneId"] = bundle["logicalZoneId"]!.DeepClone(),
                    ["absorberZoneId"] = bundle["absorberZoneId"]!.DeepClone(),
                    ["group1AbsorptionPerMPerFillFraction"] = bundle["group1AbsorptionPerMPerFillFraction"]!.DeepClone(),
                    ["group2AbsorptionPerMPerFillFraction"] = bundle["group2AbsorptionPerMPerFillFraction"]!.DeepClone()
                });
        var command = new JsonObject { ["protocol"] = "candu-playtest-v2", ["type"] = "configure-zone-layout", ["nodes"] = nodes };
        var noop = JsonNode.Parse(PlaytestBridgeV2.Dispatch(command.ToJsonString()))!;
        Assert.True(noop["accepted"]!.GetValue<bool>());
        Assert.Equal(initial["snapshot"]!["rrs"]!["mappingDigestHex"]!.GetValue<string>(),
            noop["snapshot"]!["rrs"]!["mappingDigestHex"]!.GetValue<string>());
        Assert.False(noop["snapshot"]!["provenance"]!["isModified"]!.GetValue<bool>());
        nodes[0]!["logicalZoneId"] = (nodes[0]!["logicalZoneId"]!.GetValue<int>() + 1) % 14;
        var response = JsonNode.Parse(PlaytestBridgeV2.Dispatch(command.ToJsonString()))!;
        Assert.True(response["accepted"]!.GetValue<bool>(), response["message"]!.GetValue<string>());
        Assert.Equal(nodes[0]!["logicalZoneId"]!.GetValue<int>(), response["snapshot"]!["core"]!["channels"]![0]!["bundles"]![0]!["logicalZoneId"]!.GetValue<int>());
        string core = response["snapshot"]!["core"]!.ToJsonString();
        string rrsDigest = response["snapshot"]!["rrs"]!["stateDigestHex"]!.GetValue<string>();
        foreach (var invalid in new JsonNode?[] { JsonValue.Create("invalid"), JsonValue.Create(-0.01), JsonValue.Create(2.0) })
        {
            nodes[0]!["group1AbsorptionPerMPerFillFraction"] = invalid;
            using var rejected = JsonDocument.Parse(PlaytestBridgeV2.Dispatch(command.ToJsonString()));
            Assert.False(rejected.RootElement.GetProperty("accepted").GetBoolean());
            Assert.Equal(core, JsonNode.Parse(rejected.RootElement.GetProperty("snapshot").GetProperty("core").GetRawText())!.ToJsonString());
            Assert.Equal(rrsDigest, rejected.RootElement.GetProperty("snapshot").GetProperty("rrs").GetProperty("stateDigestHex").GetString());
        }
    }
}
