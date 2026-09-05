using System;
using System.Text.Json;
using ReactorSim.Browser;
using Xunit;

namespace ReactorSim.Browser.Tests
{
    public sealed class PlaytestBridgeTests
    {
        [Fact]
        public void CapabilitiesDeclareTheVersionedPlayAndLabSeams()
        {
            using JsonDocument document = JsonDocument.Parse(PlaytestBridgeV1.GetCapabilities());
            JsonElement root = document.RootElement;

            Assert.Equal("candu-playtest-v1", root.GetProperty("protocol").GetString());
            Assert.Contains(
                root.GetProperty("modes").EnumerateArray(),
                mode => mode.GetProperty("id").GetString() == "play");
            Assert.Contains(
                root.GetProperty("modes").EnumerateArray(),
                mode => mode.GetProperty("id").GetString() == "lab");
            Assert.Equal(
                "fast-to-thermal",
                root.GetProperty("metadata")
                    .GetProperty("energyGroups")[0]
                    .GetProperty("ordering")
                    .GetString());
        }

        [Fact]
        public void PlayDispatchUsesTheFullPracticeTopologyAndAtomicRefuelling()
        {
            JsonElement initialized = Parse(
                PlaytestBridgeV1.Initialize("{\"protocol\":\"candu-playtest-v1\",\"mode\":\"play\"}"));
            Assert.Equal("play", initialized.GetProperty("mode").GetString());
            Assert.Equal(380, initialized.GetProperty("snapshot").GetProperty("core").GetProperty("channelCount").GetInt32());
            Assert.Equal(12, initialized.GetProperty("snapshot").GetProperty("core").GetProperty("bundlePositionCount").GetInt32());
            Assert.Equal(
                "toward-end-b",
                initialized.GetProperty("snapshot").GetProperty("core").GetProperty("channels")[0].GetProperty("flowDirection").GetString());
            Assert.Equal(
                "toward-end-a",
                initialized.GetProperty("snapshot").GetProperty("core").GetProperty("channels")[1].GetProperty("flowDirection").GetString());
            JsonElement initialPhysics = initialized.GetProperty("snapshot").GetProperty("physics");
            Assert.Equal("candu6-two-group-iqs-full-core-v1", initialPhysics.GetProperty("sourceId").GetString());
            Assert.Equal("converged", initialPhysics.GetProperty("solveState").GetString());
            Assert.True(initialPhysics.GetProperty("isAuthoritative").GetBoolean());
            Assert.Contains("spatial-eigen-iqs-v1", initialPhysics.GetProperty("solverIdentity").GetString());
            Assert.Contains("spatial-eigen-jacobi-v1", initialPhysics.GetProperty("solverIdentity").GetString());
            Assert.Equal(
                initialPhysics.GetProperty("targetPowerWatts").GetDouble(),
                initialPhysics.GetProperty("totalPowerWatts").GetDouble(),
                2);
            Assert.True(
                initialized.GetProperty("snapshot").GetProperty("core").GetProperty("channels")[0]
                    .GetProperty("powerWatts").GetDouble() > 0.0);

            JsonElement preview = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"preview-refuel\",\"request\":{\"channelIndex\":189,\"directionId\":\"toward-end-b\",\"shiftCount\":4,\"fuelTypeId\":\"NAT-U-SYNTHETIC\"}}"));
            Assert.True(preview.GetProperty("accepted").GetBoolean());
            Assert.NotEqual(
                preview.GetProperty("stateDigest").GetString(),
                initialized.GetProperty("stateDigest").GetString());
            Assert.Equal(
                0,
                preview.GetProperty("snapshot").GetProperty("refuellingOperationCount").GetInt32());
            Assert.True(
                preview.GetProperty("preview").GetProperty("predictedReactivityDelta").GetDouble() > 0.0);

            JsonElement committed = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"commit-refuel\",\"request\":{\"channelIndex\":189,\"directionId\":\"toward-end-b\",\"shiftCount\":4,\"fuelTypeId\":\"NAT-U-SYNTHETIC\"}}"));
            Assert.True(committed.GetProperty("accepted").GetBoolean());
            Assert.Equal(
                1,
                committed.GetProperty("snapshot").GetProperty("refuellingOperationCount").GetInt32());
            Assert.Equal(
                124,
                committed.GetProperty("snapshot").GetProperty("freshBundlesAvailable").GetInt32());

            JsonElement invalid = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"commit-refuel\",\"request\":{\"channelIndex\":999,\"directionId\":\"toward-end-b\",\"shiftCount\":4,\"fuelTypeId\":\"NAT-U-SYNTHETIC\"}}"));
            Assert.False(invalid.GetProperty("accepted").GetBoolean());
            Assert.Equal(
                1,
                invalid.GetProperty("snapshot").GetProperty("refuellingOperationCount").GetInt32());
        }

        [Fact]
        public void BrowserPlayUsesTheHourPerTwoSecondsClockAndResumesAfterDayJump()
        {
            JsonElement initialized = Parse(
                PlaytestBridgeV1.Initialize("{\"protocol\":\"candu-playtest-v1\",\"mode\":\"play\"}"));
            Assert.Equal("1x", initialized.GetProperty("snapshot").GetProperty("playbackModeId").GetString());

            JsonElement hour = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"advance\",\"wallMilliseconds\":2000}"));
            Assert.Equal(3_600.0, hour.GetProperty("snapshot").GetProperty("simulationTimeSeconds").GetDouble());
            Assert.Equal(2.0, hour.GetProperty("snapshot").GetProperty("wallElapsedSeconds").GetDouble());

            JsonElement paused = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"set-playback-mode\",\"modeId\":\"pause\"}"));
            Assert.True(paused.GetProperty("snapshot").GetProperty("isPaused").GetBoolean());

            JsonElement jumped = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"step\",\"simulationSeconds\":86400}"));
            Assert.True(jumped.GetProperty("snapshot").GetProperty("isPaused").GetBoolean());
            Assert.Equal(90_000.0, jumped.GetProperty("snapshot").GetProperty("simulationTimeSeconds").GetDouble());

            JsonElement resumed = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"set-playback-mode\",\"modeId\":\"1x\"}"));
            Assert.False(resumed.GetProperty("snapshot").GetProperty("isPaused").GetBoolean());

            JsonElement nextHour = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"advance\",\"wallMilliseconds\":2000}"));
            Assert.Equal(93_600.0, nextHour.GetProperty("snapshot").GetProperty("simulationTimeSeconds").GetDouble());
        }

        [Fact]
        public void LabRefuellingRequiresAConvergedCoupledSolve()
        {
            JsonElement initialized = Parse(
                PlaytestBridgeV1.Initialize("{\"protocol\":\"candu-playtest-v1\",\"mode\":\"lab\"}"));
            Assert.Equal("lab", initialized.GetProperty("mode").GetString());
            Assert.Equal(
                "lab-2x8-synthetic-v1",
                initialized.GetProperty("lab").GetProperty("fixtureId").GetString());
            Assert.Equal(
                2,
                initialized.GetProperty("lab").GetProperty("core").GetProperty("channelCount").GetInt32());

            JsonElement preview = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"preview-refuel\",\"channelIndex\":0,\"directionId\":\"toward-end-b\",\"shiftCount\":4,\"fuelTypeId\":\"LAB-FRESH-SYNTHETIC\"}"));
            Assert.True(preview.GetProperty("accepted").GetBoolean());
            Assert.Equal(
                0,
                preview.GetProperty("lab").GetProperty("refuellingOperationCount").GetInt32());
            Assert.Equal(
                "LAB-FRESH-SYNTHETIC",
                preview.GetProperty("labPreview")
                    .GetProperty("core")
                    .GetProperty("channels")[0]
                    .GetProperty("bundles")[0]
                    .GetProperty("fuelTypeId")
                    .GetString());

            JsonElement committed = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"commit-refuel\",\"channelIndex\":0,\"directionId\":\"toward-end-b\",\"shiftCount\":4,\"fuelTypeId\":\"LAB-FRESH-SYNTHETIC\"}"));
            Assert.True(committed.GetProperty("accepted").GetBoolean());
            Assert.Equal(
                1,
                committed.GetProperty("lab").GetProperty("refuellingOperationCount").GetInt32());
            Assert.True(
                committed.GetProperty("lab")
                    .GetProperty("spatialSolve")
                    .GetProperty("hasUsableState")
                    .GetBoolean());

            JsonElement beforeRejected = Parse(PlaytestBridgeV1.GetSnapshotJson());
            JsonElement rejected = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"solve\",\"solver\":{\"maximumIterations\":1}}"));
            Assert.False(rejected.GetProperty("accepted").GetBoolean());
            Assert.Equal(
                beforeRejected.GetProperty("lab").GetProperty("refuellingOperationCount").GetInt32(),
                rejected.GetProperty("lab").GetProperty("refuellingOperationCount").GetInt32());
            Assert.Contains(
                rejected.GetProperty("diagnostics").EnumerateArray(),
                diagnostic => diagnostic.GetProperty("code").GetString() == "Lab.Solve.Failed" ||
                    diagnostic.GetProperty("code").GetString() == "SpatialEigenSolve.Nonconverged");
        }

        private static JsonElement Parse(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }
}
