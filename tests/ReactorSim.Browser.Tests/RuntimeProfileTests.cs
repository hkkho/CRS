using System;
using System.Linq;
using System.Text.Json;
using ReactorSim.Browser;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Browser.Tests
{
    public sealed partial class PlaytestBridgeTests
    {
        [Fact]
        public void ProfilingPreservesCommandResponseAndDoesNotLeakIntoFollowingCommands()
        {
            if (!RuntimeProfile.ScopesEnabled)
            {
                Assert.Throws<NotSupportedException>(() => PlaytestBridgeV2.DispatchProfileJson("{}"));
                return;
            }
            const string resume = "{\"protocol\":\"candu-playtest-v2\",\"payload\":{\"type\":\"resume\"}}";
            const string command = "{\"protocol\":\"candu-playtest-v2\",\"payload\":{\"type\":\"advance\",\"wallMilliseconds\":100}}";
            PlaytestBridgeV2.Initialize(PlayRequest);
            PlaytestBridgeV2.Dispatch(resume);
            string normal = PlaytestBridgeV2.Dispatch(command);
            PlaytestBridgeV2.Initialize(PlayRequest);
            PlaytestBridgeV2.Dispatch(resume);
            using var envelope = JsonDocument.Parse(PlaytestBridgeV2.DispatchProfileJson(command));
            Assert.Equal(normal, envelope.RootElement.GetProperty("resultJson").GetString());
            var rows = envelope.RootElement.GetProperty("profile").EnumerateArray().ToArray();
            Assert.Contains(rows, row => row.GetProperty("name").GetString() == "game-snapshot");
            Assert.Contains(rows, row => row.GetProperty("name").GetString() == "isotopes");
            Assert.All(rows, row =>
            {
                Assert.True(row.GetProperty("calls").GetInt32() > 0);
                Assert.True(row.GetProperty("inclusiveMs").GetDouble() >= row.GetProperty("exclusiveMs").GetDouble());
                Assert.True(row.GetProperty("exclusiveMs").GetDouble() >= 0);
            });
            Assert.Null(RuntimeProfile.Measure("disabled"));
        }

        [Fact]
        public void TimingScopesSeparateParentAndChildAndResetAfterFailure()
        {
            using (var profile = RuntimeProfile.Begin())
            {
                using (RuntimeProfile.Measure("parent"))
                {
                    using (RuntimeProfile.Measure("child")) { }
                }
                Assert.Equal(2, profile.Rows.Count);
                var parent = profile.Rows.Single(row => row.Name == "parent");
                var child = profile.Rows.Single(row => row.Name == "child");
                Assert.Equal(parent.InclusiveMs, parent.ExclusiveMs + child.InclusiveMs, 6);
                Assert.Throws<InvalidOperationException>(() => RuntimeProfile.Begin());
            }
            Assert.Throws<InvalidOperationException>((Action)(() =>
            {
                using var profile = RuntimeProfile.Begin();
                using var scope = RuntimeProfile.Measure("failure");
                throw new InvalidOperationException();
            }));
            Assert.Null(RuntimeProfile.Measure("disabled"));
        }
    }
}
