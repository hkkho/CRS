#if RESEARCH_EXPERIMENTS
using System;
using System.Text.Json;
using ReactorSim.Browser;
using Xunit;

namespace ReactorSim.Browser.Tests
{
    public sealed partial class PlaytestBridgeTests
    {
        [Fact]
        public void CoupledGpuExperimentIsReadOnlyAndRejectsResultsAfterReset()
        {
            PlaytestBridgeV2.Initialize(PlayRequest);
            string before = PlaytestBridgeV2.GetSnapshotJson();
            using var fixture = JsonDocument.Parse(PlaytestBridgeV2.GetGpuPrototypeFixtureJson(
                "{\"protocol\":\"candu-spatial-gpu-prototype-v1\",\"kind\":\"coupled\",\"cold\":false}"));
            Assert.Equal(4560 * 12, fixture.RootElement.GetProperty("nodes").GetArrayLength());
            Assert.Equal(ReactorSim.Core.PracticeLiquidZoneRrsIdentityV1.RegionalShapeTolerancePercentagePoints,
                fixture.RootElement.GetProperty("regionalAgreementTolerancePercentagePoints").GetDouble());
            Assert.Equal(ReactorSim.Core.PracticeLiquidZoneRrsIdentityV1.CriticalityToleranceMk,
                fixture.RootElement.GetProperty("reactivityAgreementToleranceMk").GetDouble());
            Assert.Equal(before, PlaytestBridgeV2.GetSnapshotJson());
            string id = fixture.RootElement.GetProperty("experimentId").GetString()!;
            PlaytestBridgeV2.Initialize(PlayRequest);
            before = PlaytestBridgeV2.GetSnapshotJson();
            Assert.Throws<InvalidOperationException>(() => PlaytestBridgeV2.GetGpuPrototypeFixtureJson(
                "{\"protocol\":\"candu-spatial-gpu-prototype-v1\",\"kind\":\"verify-coupled\",\"experimentId\":\"" + id + "\"}"));
            Assert.Equal(before, PlaytestBridgeV2.GetSnapshotJson());
        }

        [Fact]
        public void GpuFixturesDoNotChangeLiveSnapshotOrCommandSequence()
        {
            PlaytestBridgeV2.Initialize(PlayRequest);
            string before = PlaytestBridgeV2.GetSnapshotJson();
            using var fixture = JsonDocument.Parse(PlaytestBridgeV2.GetGpuPrototypeFixtureJson(
                "{\"protocol\":\"candu-spatial-gpu-prototype-v1\",\"group\":2,\"iterations\":32}"));
            Assert.Equal(4560, fixture.RootElement.GetProperty("nodeCount").GetInt32());
            Assert.Equal(4560, fixture.RootElement.GetProperty("referenceFlux").GetArrayLength());
            Assert.Equal(before, PlaytestBridgeV2.GetSnapshotJson());
            Assert.Throws<ArgumentOutOfRangeException>(() => PlaytestBridgeV2.GetGpuPrototypeFixtureJson(
                "{\"protocol\":\"candu-spatial-gpu-prototype-v1\",\"group\":0,\"iterations\":32}"));
            Assert.Equal(before, PlaytestBridgeV2.GetSnapshotJson());
        }
    }
}

#endif
