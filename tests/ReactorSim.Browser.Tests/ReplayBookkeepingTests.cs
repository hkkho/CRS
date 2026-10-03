using System;
using System.Text.Json;
using ReactorSim.Browser;
using Xunit;

namespace ReactorSim.Browser.Tests
{
    public sealed partial class PlaytestBridgeTests
    {
        [Fact]
        public void ReplayChainCommitsToAcceptedAndDispatchedRejectedCommandsOnly()
        {
            JsonElement initialized = Parse(PlaytestBridgeV2.Initialize(PlayRequest));
            var chain = new ReplayDigestChain("play", PlayRequest);
            Assert.Equal("sha256-chained-replay-v2", Parse(PlaytestBridgeV2.GetCapabilities()).GetProperty("metadata")
                .GetProperty("replayDigestAlgorithm").GetString());
            Assert.Equal(chain.Digest, initialized.GetProperty("replayDigest").GetString());
            Assert.Equal(PlaytestProtocolV2.ReplayDigestAlgorithm,
                initialized.GetProperty("replayDigestAlgorithm").GetString());

            foreach (string type in new[] { "pause", "unknown-command" })
            {
                string command = "{\"protocol\":\"candu-playtest-v2\",\"type\":\"" + type + "\"}";
                JsonElement response = Parse(PlaytestBridgeV2.Dispatch(command));
                chain.Append(Canonical(command));
                Assert.Equal(type == "pause", response.GetProperty("accepted").GetBoolean());
                Assert.Equal(chain.Digest, response.GetProperty("replayDigest").GetString());
                Assert.Equal(PlaytestProtocolV2.ReplayDigestAlgorithm,
                    response.GetProperty("replayDigestAlgorithm").GetString());
            }

            // Parse/protocol failures and stale compact requests never enter dispatch.
            foreach (string command in new[] {
                "{not-json",
                "{\"protocol\":\"other\",\"type\":\"pause\"}",
                "{\"protocol\":\"candu-playtest-v2\",\"type\":\"pause\",\"responseMode\":\"compact\",\"baseSequence\":0}"
            })
            {
                JsonElement response = Parse(PlaytestBridgeV2.Dispatch(command));
                Assert.False(response.GetProperty("accepted").GetBoolean());
                Assert.Equal(chain.Digest, response.GetProperty("replayDigest").GetString());
                Assert.Equal(2UL, response.GetProperty("sequence").GetUInt64());
            }

            string reset = "{\"protocol\":\"candu-playtest-v2\",\"type\":\"reset\",\"seed\":42}";
            JsonElement resetResponse = Parse(PlaytestBridgeV2.Dispatch(reset));
            var resetChain = new ReplayDigestChain("play",
                "{\"protocol\":\"candu-playtest-v2\",\"mode\":\"play\",\"seed\":42,\"shiftId\":\"free-practice\"}");
            resetChain.Append(Canonical(reset));
            Assert.Equal(resetChain.Digest, resetResponse.GetProperty("replayDigest").GetString());
            Assert.Equal(1UL, resetResponse.GetProperty("sequence").GetUInt64());
        }

        [Fact]
        public void LongReplayChainHasConstantStorageAndDeterministicOrderSensitiveIdentity()
        {
            var first = new ReplayDigestChain("play", PlayRequest);
            var second = new ReplayDigestChain("play", PlayRequest);
            var otherSeed = new ReplayDigestChain("play", "{\"seed\":42}");
            Assert.NotEqual(first.Digest, otherSeed.Digest);
            string start = first.Digest;
            for (int index = 0; index < 10000; index++)
            {
                string command = index % 2 == 0 ? "{\"type\":\"pause\"}" : "{\"type\":\"resume\"}";
                first.Append(command);
                second.Append(command);
            }
            Assert.Equal(first.Digest, second.Digest);
            Assert.NotEqual(start, first.Digest);
            Assert.Equal(71, first.Digest.Length);
            // No hidden retained command collections: the only instance state is the digest.
            Assert.Single(typeof(ReplayDigestChain).GetFields(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
            first.Append("{\"type\":\"pause\"}"); first.Append("{\"type\":\"resume\"}");
            second.Append("{\"type\":\"resume\"}"); second.Append("{\"type\":\"pause\"}");
            Assert.NotEqual(first.Digest, second.Digest);
        }

        private static string Canonical(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return PlaytestProtocolV2.CanonicalizeJson(document.RootElement);
        }
    }
}
