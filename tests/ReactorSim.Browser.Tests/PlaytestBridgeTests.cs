using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ReactorSim.Browser;
using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Browser.Tests
{
    public sealed class PlaytestBridgeTests
    {
        private const string Protocol = "candu-playtest-v1";
        private const string PlayRequest =
            "{\"protocol\":\"candu-playtest-v1\",\"mode\":\"play\"}";
        private const string LabRequest =
            "{\"protocol\":\"candu-playtest-v1\",\"mode\":\"lab\"}";
        private const string PlayRefuelRequest =
            "{\"channelIndex\":189,\"directionId\":\"toward-end-b\",\"shiftCount\":4,\"fuelTypeId\":\"NAT-U-SYNTHETIC\"}";
        private const string LabRefuelFields =
            "\"channelIndex\":0,\"directionId\":\"toward-end-b\",\"shiftCount\":4,\"fuelTypeId\":\"LAB-FRESH-SYNTHETIC\"";
        private const string LabRefuelRequest =
            "{" + LabRefuelFields + "}";
        private static readonly string[] ExpectedOperations =
        {
            "GetCapabilities",
            "Initialize",
            "Dispatch"
        };

        [Fact]
        public void CapabilitiesAndValidInitializationsExposeAuthoritativeContracts()
        {
            JsonElement capabilities = Parse(PlaytestBridgeV1.GetCapabilities());

            Assert.Equal(Protocol, capabilities.GetProperty("protocol").GetString());
            Assert.Equal(1U, capabilities.GetProperty("schemaVersion").GetUInt32());
            Assert.Equal(
                ExpectedOperations,
                capabilities.GetProperty("operations")
                    .EnumerateArray()
                    .Select(value => value.GetString())
                    .ToArray());

            JsonElement playMode = FindMode(capabilities, "play");
            Assert.Equal("ReactorSim.Game.GameSession", playMode.GetProperty("authoritativeModel").GetString());
            Assert.Equal(
                PracticeGameSessionFactory.DiffusionDataPackVersion,
                playMode.GetProperty("fixtureId").GetString());
            Assert.Contains("advance", playMode.GetProperty("commands").EnumerateArray().Select(value => value.GetString()));
            Assert.Contains("preview-refuel", playMode.GetProperty("commands").EnumerateArray().Select(value => value.GetString()));
            Assert.Contains("commit-refuel", playMode.GetProperty("commands").EnumerateArray().Select(value => value.GetString()));

            JsonElement labMode = FindMode(capabilities, "lab");
            Assert.Equal("ReactorSim.Core.SpatialEigenSolve", labMode.GetProperty("authoritativeModel").GetString());
            Assert.Equal(LabPlaytestSessionFixtureId, labMode.GetProperty("fixtureId").GetString());
            Assert.Contains("solve", labMode.GetProperty("commands").EnumerateArray().Select(value => value.GetString()));
            Assert.Contains("commit-refuel", labMode.GetProperty("commands").EnumerateArray().Select(value => value.GetString()));

            JsonElement metadata = capabilities.GetProperty("metadata");
            JsonElement units = metadata.GetProperty("units");
            Assert.Equal("s", units.GetProperty("time").GetString());
            Assert.Equal("ms", units.GetProperty("wallTime").GetString());
            Assert.Equal("W", units.GetProperty("power").GetString());
            Assert.Equal("MWd/kg_HM", units.GetProperty("burnup").GetString());
            Assert.Equal("m^-3", units.GetProperty("numberDensity").GetString());

            JsonElement groups = metadata.GetProperty("energyGroups");
            Assert.Equal(2, groups.GetArrayLength());
            Assert.Equal("group-1", groups[0].GetProperty("id").GetString());
            Assert.Equal(1, groups[0].GetProperty("ordinal").GetInt32());
            Assert.Equal("fast-to-thermal", groups[0].GetProperty("ordering").GetString());
            Assert.Equal("group-2", groups[1].GetProperty("id").GetString());
            Assert.Equal(2, groups[1].GetProperty("ordinal").GetInt32());
            Assert.Equal("fast-to-thermal", groups[1].GetProperty("ordering").GetString());
            Assert.Equal(AdiabaticKineticsIdentityV1.FormulationId, metadata.GetProperty("formulationId").GetString());
            Assert.Equal(AdiabaticKineticsIdentityV1.ShapeMethodId, metadata.GetProperty("shapeMethodId").GetString());
            Assert.Equal(AdiabaticKineticsIdentityV1.AmplitudeMethodId, metadata.GetProperty("amplitudeMethodId").GetString());
            Assert.Equal(AdiabaticKineticsIdentityV1.ReactivityMethodId, metadata.GetProperty("reactivityMethodId").GetString());

            JsonElement play = Parse(PlaytestBridgeV1.Initialize(PlayRequest));
            AssertAccepted(play);
            Assert.Equal("play", play.GetProperty("mode").GetString());
            AssertPlaySnapshot(play.GetProperty("snapshot"));
            Assert.Equal(PracticeGameSessionFactory.KineticsDataPackVersion, play.GetProperty("snapshot").GetProperty("dataPackId").GetString());
            Assert.Equal(AdiabaticKineticsIdentityV1.ModelId, play.GetProperty("snapshot").GetProperty("physics").GetProperty("sourceId").GetString());
            Assert.True(play.GetProperty("snapshot").GetProperty("physics").GetProperty("isAuthoritative").GetBoolean());

            JsonElement lab = Parse(PlaytestBridgeV1.Initialize(LabRequest));
            AssertAccepted(lab);
            Assert.Equal("lab", lab.GetProperty("mode").GetString());
            JsonElement labSnapshot = lab.GetProperty("lab");
            Assert.Equal(LabPlaytestSessionFixtureId, labSnapshot.GetProperty("fixtureId").GetString());
            Assert.Equal(LabPlaytestSessionFixtureId, labSnapshot.GetProperty("core").GetProperty("fixtureId").GetString());
            Assert.Equal(2, labSnapshot.GetProperty("core").GetProperty("channelCount").GetInt32());
            Assert.Equal(8, labSnapshot.GetProperty("core").GetProperty("bundlePositionCount").GetInt32());
            Assert.Equal(2, labSnapshot.GetProperty("core").GetProperty("channels").GetArrayLength());
            Assert.True(labSnapshot.GetProperty("spatialSolve").GetProperty("isConverged").GetBoolean());
            Assert.True(labSnapshot.GetProperty("spatialSolve").GetProperty("hasUsableState").GetBoolean());
        }

        [Fact]
        public void InvalidProtocolModeTypeAndNumberInputsFailClosedWithoutChangingAuthority()
        {
            JsonElement labBaseline = Parse(PlaytestBridgeV1.Initialize(LabRequest));
            string[] invalidInitializations =
            {
                "{\"mode\":\"play\"}",
                "{\"protocol\":\"other-v1\",\"mode\":\"play\"}",
                "{\"protocol\":\"candu-playtest-v1\",\"mode\":\"unsupported\"}",
                "{\"protocol\":\"candu-playtest-v1\",\"mode\":7}",
                "{\"protocol\":\"candu-playtest-v1\",\"mode\":[]}",
                "{not-json"
            };

            foreach (string invalidInitialization in invalidInitializations)
            {
                JsonElement rejected = Parse(PlaytestBridgeV1.Initialize(invalidInitialization));
                AssertRejected(rejected);
                Assert.Equal("lab", rejected.GetProperty("mode").GetString());
                Assert.Equal(labBaseline.GetProperty("sequence").GetUInt64(), rejected.GetProperty("sequence").GetUInt64());
                Assert.Equal(labBaseline.GetProperty("stateDigest").GetString(), rejected.GetProperty("stateDigest").GetString());
                Assert.Equal(labBaseline.GetProperty("replayDigest").GetString(), rejected.GetProperty("replayDigest").GetString());
                Assert.Equal(labBaseline.GetProperty("lab").GetRawText(), rejected.GetProperty("lab").GetRawText());
                AssertAuthoritativeSnapshotUnchanged(
                    labBaseline.GetProperty("snapshot"),
                    rejected.GetProperty("snapshot"));
            }

            JsonElement playBaseline = Parse(PlaytestBridgeV1.Initialize(PlayRequest));
            string[] invalidCommands =
            {
                "{not-json",
                "{\"type\":\"advance\",\"wallMilliseconds\":100}",
                "{\"protocol\":\"other-v1\",\"type\":\"advance\",\"wallMilliseconds\":100}",
                "{\"protocol\":\"candu-playtest-v1\",\"type\":7}",
                "{\"protocol\":\"candu-playtest-v1\",\"type\":\"advance\",\"wallMilliseconds\":\"100\"}",
                "{\"protocol\":\"candu-playtest-v1\",\"type\":\"queue-power-target\",\"targetFraction\":1e999}"
            };

            foreach (string invalidCommand in invalidCommands)
            {
                JsonElement rejected = Parse(PlaytestBridgeV1.Dispatch(invalidCommand));
                AssertRejected(rejected);
                Assert.Equal("play", rejected.GetProperty("mode").GetString());
                AssertAuthoritativeSnapshotUnchanged(
                    playBaseline.GetProperty("snapshot"),
                    rejected.GetProperty("snapshot"));
            }
        }

        [Fact]
        public void PlayCommandSequencePreservesPreviewAtomicCommitAndRejectedState()
        {
            JsonElement initialized = Parse(PlaytestBridgeV1.Initialize(PlayRequest));
            Assert.Equal(0UL, initialized.GetProperty("sequence").GetUInt64());

            JsonElement advanced = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"advance\",\"wallMilliseconds\":2000}"));
            AssertAccepted(advanced);
            Assert.Equal(1UL, advanced.GetProperty("sequence").GetUInt64());
            Assert.Equal(3_600.0, advanced.GetProperty("snapshot").GetProperty("simulationTimeSeconds").GetDouble(), 12);
            Assert.Equal(2.0, advanced.GetProperty("snapshot").GetProperty("wallElapsedSeconds").GetDouble(), 12);
            AssertPlaySnapshot(advanced.GetProperty("snapshot"));

            JsonElement beforePreview = advanced.GetProperty("snapshot");
            JsonElement preview = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"preview-refuel\",\"request\":" + PlayRefuelRequest + "}"));
            AssertAccepted(preview);
            Assert.Equal(2UL, preview.GetProperty("sequence").GetUInt64());
            AssertPlaySnapshot(preview.GetProperty("snapshot"));
            AssertAuthoritativeSnapshotUnchanged(beforePreview, preview.GetProperty("snapshot"));
            Assert.Equal(0, preview.GetProperty("snapshot").GetProperty("refuellingOperationCount").GetInt32());
            Assert.Equal(128, preview.GetProperty("snapshot").GetProperty("freshBundlesAvailable").GetInt32());

            JsonElement previewDetails = preview.GetProperty("preview");
            Assert.Equal(4, previewDetails.GetProperty("insertedBundleIds").GetArrayLength());
            Assert.Equal(4, previewDetails.GetProperty("dischargedBundleIds").GetArrayLength());
            AssertFinite(previewDetails.GetProperty("predictedReactivityDelta").GetDouble());

            JsonElement committed = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"commit-refuel\",\"request\":" + PlayRefuelRequest + "}"));
            AssertAccepted(committed);
            Assert.Equal(3UL, committed.GetProperty("sequence").GetUInt64());
            AssertPlaySnapshot(committed.GetProperty("snapshot"));
            Assert.Equal(1, committed.GetProperty("snapshot").GetProperty("refuellingOperationCount").GetInt32());
            Assert.Equal(124, committed.GetProperty("snapshot").GetProperty("freshBundlesAvailable").GetInt32());
            Assert.Equal(189, committed.GetProperty("snapshot").GetProperty("lastRefuelledChannel").GetInt32());
            Assert.Equal(189, committed.GetProperty("snapshot").GetProperty("xenon").GetProperty("selectedChannelIndex").GetInt32());
            Assert.Equal(189, committed.GetProperty("snapshot").GetProperty("xenon").GetProperty("selectedChannel").GetProperty("channelIndex").GetInt32());
            AssertInsertedIdsAreInChannel(
                previewDetails.GetProperty("insertedBundleIds"),
                committed.GetProperty("snapshot").GetProperty("core").GetProperty("channels")[189]);

            JsonElement rejected = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"commit-refuel\",\"request\":{\"channelIndex\":999,\"directionId\":\"toward-end-b\",\"shiftCount\":4,\"fuelTypeId\":\"NAT-U-SYNTHETIC\"}}"));
            AssertRejected(rejected);
            Assert.Equal(4UL, rejected.GetProperty("sequence").GetUInt64());
            AssertPlaySnapshot(rejected.GetProperty("snapshot"));
            AssertAuthoritativeSnapshotUnchanged(
                committed.GetProperty("snapshot"),
                rejected.GetProperty("snapshot"));
            Assert.Equal(1, rejected.GetProperty("snapshot").GetProperty("refuellingOperationCount").GetInt32());
            Assert.Equal(124, rejected.GetProperty("snapshot").GetProperty("freshBundlesAvailable").GetInt32());
        }

        [Fact]
        public void RepeatedPlayCommandStreamsHaveIdenticalDigestsAndCompactSelectedXenon()
        {
            JsonElement first = RunDeterministicPlayStream();
            JsonElement second = RunDeterministicPlayStream();

            Assert.Equal(first.GetProperty("stateDigest").GetString(), second.GetProperty("stateDigest").GetString());
            Assert.Equal(first.GetProperty("replayDigest").GetString(), second.GetProperty("replayDigest").GetString());
            Assert.Equal(first.GetProperty("snapshot").GetRawText(), second.GetProperty("snapshot").GetRawText());
            Assert.Equal(first.GetProperty("snapshot").GetProperty("xenon").GetRawText(), second.GetProperty("snapshot").GetProperty("xenon").GetRawText());

            JsonElement snapshot = first.GetProperty("snapshot");
            JsonElement xenon = snapshot.GetProperty("xenon");
            Assert.Equal(12, xenon.GetProperty("selectedChannelIndex").GetInt32());
            Assert.Equal(12, xenon.GetProperty("selectedChannel").GetProperty("channelIndex").GetInt32());
            Assert.Equal(
                snapshot.GetProperty("core").GetProperty("channels")[12].GetProperty("xenon").GetProperty("meanXe135NumberDensityM3").GetDouble(),
                xenon.GetProperty("selectedChannel").GetProperty("meanXe135NumberDensityM3").GetDouble(),
                12);
            Assert.DoesNotContain("nodeInputs", first.GetRawText(), StringComparison.Ordinal);
            Assert.DoesNotContain("nodeStates", first.GetRawText(), StringComparison.Ordinal);
            Assert.DoesNotContain("overlays", first.GetRawText(), StringComparison.Ordinal);
            Assert.DoesNotContain("group1Flux", first.GetRawText(), StringComparison.Ordinal);
            Assert.DoesNotContain("group2Flux", first.GetRawText(), StringComparison.Ordinal);
        }

        [Fact]
        public void BrowserPlayStreamMapsTheSameAuthoritativePracticeGameSessionStream()
        {
            JsonElement initialized = Parse(PlaytestBridgeV1.Initialize(PlayRequest));
            GameSession direct = PracticeGameSessionFactory.CreateBrowserPlaytest();
            AssertGameSnapshotMaps(initialized.GetProperty("snapshot"), direct.Snapshot, 189);

            GameSessionCommandResult expectedAdvance = direct.AdvanceWallMilliseconds(2000);
            JsonElement browserAdvance = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"advance\",\"wallMilliseconds\":2000}"));
            Assert.True(expectedAdvance.Accepted);
            AssertAccepted(browserAdvance);
            AssertGameSnapshotMaps(browserAdvance.GetProperty("snapshot"), expectedAdvance.Snapshot, 189);

            GameSessionCommandResult expectedPreview = direct.PreviewRefuelChannel(
                189,
                "toward-end-b",
                4,
                "NAT-U-SYNTHETIC");
            JsonElement browserPreview = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"preview-refuel\",\"request\":" + PlayRefuelRequest + "}"));
            Assert.True(expectedPreview.Accepted);
            AssertAccepted(browserPreview);
            AssertGameSnapshotMaps(browserPreview.GetProperty("snapshot"), expectedPreview.Snapshot, 189);
            Assert.NotNull(expectedPreview.PreviewCore);
            string[] expectedInsertedBundleIds = expectedPreview.PreviewCore!.GetChannel(189).Bundles
                .Where(bundle => !expectedPreview.Snapshot.Core.GetChannel(189).Bundles
                    .Any(previous => previous.BundleId == bundle.BundleId))
                .Select(bundle => bundle.BundleId)
                .ToArray();
            string[] browserInsertedBundleIds = browserPreview.GetProperty("preview")
                .GetProperty("insertedBundleIds")
                .EnumerateArray()
                .Select(value => value.GetString()!)
                .ToArray();
            Assert.Equal(
                expectedInsertedBundleIds,
                browserInsertedBundleIds);

            GameSessionCommandResult expectedCommit = direct.RefuelChannel(
                189,
                "toward-end-b",
                4,
                "NAT-U-SYNTHETIC");
            JsonElement browserCommit = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"commit-refuel\",\"request\":" + PlayRefuelRequest + "}"));
            Assert.True(expectedCommit.Accepted);
            AssertAccepted(browserCommit);
            AssertGameSnapshotMaps(browserCommit.GetProperty("snapshot"), expectedCommit.Snapshot, 189);
            Assert.Equal(
                expectedCommit.Snapshot.Core.GetChannel(189).Bundles.Select(bundle => bundle.BundleId),
                browserCommit.GetProperty("snapshot").GetProperty("core").GetProperty("channels")[189]
                    .GetProperty("bundles").EnumerateArray().Select(bundle => bundle.GetProperty("bundleId").GetString()));
        }

        [Fact]
        public void CompactPlayResponsesCarryPatchWithoutCoreAndCommitCarriesReplacement()
        {
            JsonElement initialized = Parse(PlaytestBridgeV1.Initialize(PlayRequest));
            AssertAccepted(initialized);
            int legacyBytes = initialized.GetRawText().Length;
            Assert.True(legacyBytes > 1_000_000);

            JsonElement paused = Parse(
                PlaytestBridgeV1.Dispatch(
                    CompactCommand(0, "pause")));
            AssertCompactPatch(paused, 0, 1);
            Assert.True(paused.GetProperty("snapshotPatch").GetProperty("isPaused").GetBoolean());
            Assert.DoesNotContain("channels", paused.GetRawText(), StringComparison.Ordinal);
            Assert.True(paused.GetRawText().Length <= 32_768);
            Assert.True(paused.GetRawText().Length <= legacyBytes * 0.05);

            JsonElement preview = Parse(
                PlaytestBridgeV1.Dispatch(
                    CompactCommand(
                        1,
                        "preview-refuel",
                        "\"request\":" + PlayRefuelRequest)));
            AssertCompactPatch(preview, 1, 2);
            Assert.True(preview.TryGetProperty("preview", out _));
            Assert.DoesNotContain("channels", preview.GetRawText(), StringComparison.Ordinal);

            JsonElement resumed = Parse(
                PlaytestBridgeV1.Dispatch(
                    CompactCommand(2, "resume")));
            AssertCompactPatch(resumed, 2, 3);

            JsonElement advanced = Parse(
                PlaytestBridgeV1.Dispatch(
                    CompactCommand(3, "advance", "\"wallMilliseconds\":100")));
            AssertCompactPatch(advanced, 3, 4);
            Assert.DoesNotContain("channels", advanced.GetRawText(), StringComparison.Ordinal);
            JsonElement exactAfterAdvance = Parse(PlaytestBridgeV1.GetSnapshotJson());
            JsonElement advancePatch = advanced.GetProperty("snapshotPatch");
            Assert.Equal(advancePatch.GetProperty("simulationTimeSeconds").GetDouble(), exactAfterAdvance.GetProperty("simulationTimeSeconds").GetDouble(), 12);
            Assert.Equal(advancePatch.GetProperty("wallElapsedSeconds").GetDouble(), exactAfterAdvance.GetProperty("wallElapsedSeconds").GetDouble(), 12);
            Assert.Equal(advancePatch.GetProperty("scoreTotal").GetDouble(), exactAfterAdvance.GetProperty("scoreTotal").GetDouble(), 12);
            Assert.Equal(advancePatch.GetProperty("physics").GetRawText(), exactAfterAdvance.GetProperty("physics").GetRawText());
            Assert.Equal(advancePatch.GetProperty("xenon").GetRawText(), exactAfterAdvance.GetProperty("xenon").GetRawText());
            Assert.Equal(advancePatch.GetProperty("diagnostics").GetRawText(), exactAfterAdvance.GetProperty("diagnostics").GetRawText());
            Assert.Equal(advancePatch.GetProperty("lastEvent").GetRawText(), exactAfterAdvance.GetProperty("lastEvent").GetRawText());

            JsonElement committed = Parse(
                PlaytestBridgeV1.Dispatch(
                    CompactCommand(
                        4,
                        "commit-refuel",
                        "\"request\":" + PlayRefuelRequest)));
            AssertCompactPatch(committed, 4, 5);
            Assert.True(committed.TryGetProperty("coreReplacement", out JsonElement replacement));
            Assert.Equal(380, replacement.GetProperty("channels").GetArrayLength());
            Assert.True(committed.GetProperty("snapshotPatch").GetProperty("refuellingOperationCount").GetUInt32() > 0);
            JsonElement exact = Parse(PlaytestBridgeV1.GetSnapshotJson());
            Assert.Equal(
                replacement.GetRawText(),
                exact.GetProperty("core").GetRawText());
        }

        [Fact]
        public void OrdinaryCompactCommandsDoNotMaterializeBrowserCoreDto()
        {
            Parse(PlaytestBridgeV1.Initialize(PlayRequest));
            PlaytestBridgeV1.ResetCoreSnapshotMaterializationCount();

            JsonElement paused = Parse(
                PlaytestBridgeV1.Dispatch(
                    CompactCommand(0, "pause")));
            AssertCompactPatch(paused, 0, 1);
            Assert.Equal(0, PlaytestBridgeV1.CoreSnapshotMaterializationCount);

            PlaytestBridgeV1.ResetCoreSnapshotMaterializationCount();
            JsonElement resumed = Parse(
                PlaytestBridgeV1.Dispatch(
                    CompactCommand(1, "resume")));
            AssertCompactPatch(resumed, 1, 2);
            Assert.Equal(0, PlaytestBridgeV1.CoreSnapshotMaterializationCount);

            PlaytestBridgeV1.ResetCoreSnapshotMaterializationCount();
            JsonElement committed = Parse(
                PlaytestBridgeV1.Dispatch(
                    CompactCommand(
                        2,
                        "commit-refuel",
                        "\"request\":" + PlayRefuelRequest)));
            AssertCompactPatch(committed, 2, 3);
            Assert.True(PlaytestBridgeV1.CoreSnapshotMaterializationCount > 0);
        }

        [Fact]
        public void CompactWrongBaseSequenceRequestsResyncWithoutMutatingAuthority()
        {
            JsonElement initialized = Parse(PlaytestBridgeV1.Initialize(PlayRequest));
            JsonElement paused = Parse(
                PlaytestBridgeV1.Dispatch(
                    CompactCommand(0, "pause")));
            AssertCompactPatch(paused, 0, 1);

            JsonElement mismatch = Parse(
                PlaytestBridgeV1.Dispatch(
                    CompactCommand(0, "resume")));
            Assert.Equal("compact", mismatch.GetProperty("responseKind").GetString());
            Assert.True(mismatch.GetProperty("requiresResync").GetBoolean());
            Assert.False(mismatch.GetProperty("accepted").GetBoolean());
            Assert.Equal(1UL, mismatch.GetProperty("sequence").GetUInt64());
            Assert.False(mismatch.TryGetProperty("snapshot", out _));
            Assert.False(mismatch.TryGetProperty("snapshotPatch", out _));

            JsonElement exact = Parse(PlaytestBridgeV1.GetSnapshotJson());
            Assert.Equal(1UL, exact.GetProperty("sequence").GetUInt64());
            Assert.True(exact.GetProperty("isPaused").GetBoolean());
            Assert.Equal(380, exact.GetProperty("core").GetProperty("channels").GetArrayLength());
        }

        [Fact]
        public void LabRefuelCommitsThenFailedSolvePreservesInventoryCoreSolveAndDigest()
        {
            JsonElement initialized = Parse(PlaytestBridgeV1.Initialize(LabRequest));
            JsonElement beforeLab = initialized.GetProperty("lab");

            JsonElement preview = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"preview-refuel\"," + LabRefuelFields + "}"));
            AssertAccepted(preview);
            Assert.Equal(beforeLab.GetRawText(), preview.GetProperty("lab").GetRawText());
            Assert.Equal(0, preview.GetProperty("lab").GetProperty("refuellingOperationCount").GetInt32());
            Assert.Equal(32, preview.GetProperty("lab").GetProperty("freshBundlesAvailable").GetInt32());
            Assert.Contains(
                preview.GetProperty("labPreview").GetProperty("core").GetProperty("channels")[0]
                    .GetProperty("bundles").EnumerateArray(),
                bundle => bundle.GetProperty("fuelTypeId").GetString() == "LAB-FRESH-SYNTHETIC");

            JsonElement committed = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"commit-refuel\"," + LabRefuelFields + "}"));
            AssertAccepted(committed);
            JsonElement committedLab = committed.GetProperty("lab");
            Assert.Equal(1, committedLab.GetProperty("refuellingOperationCount").GetInt32());
            Assert.Equal(28, committedLab.GetProperty("freshBundlesAvailable").GetInt32());
            Assert.True(committedLab.GetProperty("spatialSolve").GetProperty("isConverged").GetBoolean());
            Assert.True(committedLab.GetProperty("spatialSolve").GetProperty("hasUsableState").GetBoolean());
            Assert.Contains(
                committedLab.GetProperty("core").GetProperty("channels")[0]
                    .GetProperty("bundles").EnumerateArray(),
                bundle => bundle.GetProperty("fuelTypeId").GetString() == "LAB-FRESH-SYNTHETIC");

            JsonElement failed = Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"solve\",\"solver\":{\"maximumIterations\":1}}"));
            AssertRejected(failed);
            Assert.Contains(
                failed.GetProperty("diagnostics").EnumerateArray(),
                diagnostic => diagnostic.GetProperty("code").GetString() == "Lab.Solve.Failed" ||
                    diagnostic.GetProperty("code").GetString() == "SpatialEigenSolve.Nonconverged");
            Assert.Equal(committedLab.GetRawText(), failed.GetProperty("lab").GetRawText());
            Assert.Equal(committedLab.GetProperty("core").GetRawText(), failed.GetProperty("lab").GetProperty("core").GetRawText());
            Assert.Equal(committedLab.GetProperty("spatialSolve").GetRawText(), failed.GetProperty("lab").GetProperty("spatialSolve").GetRawText());
            Assert.Equal(1, failed.GetProperty("lab").GetProperty("refuellingOperationCount").GetInt32());
            Assert.Equal(28, failed.GetProperty("lab").GetProperty("freshBundlesAvailable").GetInt32());
        }

        private const string LabPlaytestSessionFixtureId = "lab-2x8-synthetic-v1";

        private static JsonElement RunDeterministicPlayStream()
        {
            Parse(PlaytestBridgeV1.Initialize(PlayRequest));
            Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"advance\",\"wallMilliseconds\":2000}"));
            Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"preview-refuel\",\"request\":{\"channelIndex\":12,\"directionId\":\"toward-end-b\",\"shiftCount\":4,\"fuelTypeId\":\"NAT-U-SYNTHETIC\"}}"));
            return Parse(
                PlaytestBridgeV1.Dispatch(
                    "{\"protocol\":\"candu-playtest-v1\",\"type\":\"commit-refuel\",\"request\":{\"channelIndex\":12,\"directionId\":\"toward-end-b\",\"shiftCount\":4,\"fuelTypeId\":\"NAT-U-SYNTHETIC\"}}"));
        }

        private static string CompactCommand(
            ulong baseSequence,
            string commandType,
            string fields = "")
        {
            string suffix = string.IsNullOrWhiteSpace(fields) ? string.Empty : "," + fields;
            return "{\"protocol\":\"candu-playtest-v1\",\"type\":\"command\",\"responseMode\":\"compact\",\"baseSequence\":" +
                baseSequence +
                ",\"payload\":{\"type\":\"" + commandType + "\"" + suffix + "}}";
        }

        private static JsonElement FindMode(JsonElement capabilities, string modeId)
        {
            foreach (JsonElement mode in capabilities.GetProperty("modes").EnumerateArray())
            {
                if (mode.GetProperty("id").GetString() == modeId)
                {
                    return mode;
                }
            }

            throw new InvalidOperationException("The capability descriptor did not expose mode " + modeId + ".");
        }

        private static void AssertPlaySnapshot(JsonElement snapshot)
        {
            Assert.Equal(Protocol, snapshot.GetProperty("protocol").GetString());
            Assert.Equal("wasm", snapshot.GetProperty("source").GetString());
            Assert.Equal(380, snapshot.GetProperty("core").GetProperty("channelCount").GetInt32());
            Assert.Equal(12, snapshot.GetProperty("core").GetProperty("bundlePositionCount").GetInt32());
            Assert.Equal(380, snapshot.GetProperty("core").GetProperty("channels").GetArrayLength());
            Assert.Equal(12, snapshot.GetProperty("core").GetProperty("channels")[0].GetProperty("bundles").GetArrayLength());
            Assert.True(snapshot.GetProperty("physics").GetProperty("isAuthoritative").GetBoolean());
            Assert.Equal("converged", snapshot.GetProperty("physics").GetProperty("solveState").GetString());
            Assert.Equal(380 * 12, snapshot.GetProperty("xenon").GetProperty("nodeCount").GetInt32());
            Assert.NotEmpty(snapshot.GetProperty("diagnostics").GetProperty("checks").EnumerateArray());
        }

        private static void AssertAccepted(JsonElement response)
        {
            Assert.True(response.GetProperty("ok").GetBoolean());
            Assert.True(response.GetProperty("accepted").GetBoolean());
            Assert.Empty(response.GetProperty("diagnostics").EnumerateArray());
        }

        private static void AssertCompactPatch(
            JsonElement response,
            ulong expectedBaseSequence,
            ulong expectedSequence)
        {
            Assert.True(response.GetProperty("ok").GetBoolean());
            Assert.True(response.GetProperty("accepted").GetBoolean());
            Assert.Equal("compact", response.GetProperty("responseKind").GetString());
            Assert.Equal(expectedBaseSequence, response.GetProperty("baseSequence").GetUInt64());
            Assert.Equal(expectedSequence, response.GetProperty("sequence").GetUInt64());
            Assert.False(response.TryGetProperty("snapshot", out _));
            Assert.True(response.TryGetProperty("snapshotPatch", out JsonElement patch));
            Assert.True(patch.TryGetProperty("physics", out _));
            Assert.True(patch.TryGetProperty("xenon", out _));
            Assert.True(patch.TryGetProperty("diagnostics", out _));
            Assert.False(response.TryGetProperty("requiresResync", out JsonElement requiresResync) &&
                requiresResync.GetBoolean());
            Assert.Empty(response.GetProperty("diagnostics").EnumerateArray());
        }

        private static void AssertRejected(JsonElement response)
        {
            Assert.False(response.GetProperty("ok").GetBoolean());
            Assert.False(response.GetProperty("accepted").GetBoolean());
            Assert.NotEmpty(response.GetProperty("diagnostics").EnumerateArray());
            JsonElement diagnostic = response.GetProperty("diagnostics")[0];
            Assert.False(string.IsNullOrWhiteSpace(diagnostic.GetProperty("code").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(diagnostic.GetProperty("message").GetString()));
        }

        private static void AssertAuthoritativeSnapshotUnchanged(JsonElement before, JsonElement after)
        {
            string[] scalarProperties =
            {
                "scenarioId",
                "dataPackId",
                "simulationTimeSeconds",
                "wallElapsedSeconds",
                "normalizedPowerFraction",
                "targetPowerFraction",
                "absoluteTiltFraction",
                "targetTiltFraction",
                "controlMarginFraction",
                "deviceAvailableFraction",
                "pendingActionCount",
                "scoreTotal",
                "isPaused",
                "playbackModeId",
                "freshBundlesAvailable",
                "refuellingOperationCount",
                "lastRefuelledChannel",
                "lastRefuellingDirectionId",
                "lastRefuellingShiftCount"
            };

            foreach (string property in scalarProperties)
            {
                bool beforeHasProperty = before.TryGetProperty(property, out JsonElement beforeValue);
                bool afterHasProperty = after.TryGetProperty(property, out JsonElement afterValue);
                Assert.Equal(beforeHasProperty, afterHasProperty);
                if (beforeHasProperty)
                {
                    Assert.Equal(beforeValue.GetRawText(), afterValue.GetRawText());
                }
            }

            Assert.Equal(before.GetProperty("physics").GetRawText(), after.GetProperty("physics").GetRawText());
            Assert.Equal(before.GetProperty("xenon").GetRawText(), after.GetProperty("xenon").GetRawText());
            Assert.Equal(before.GetProperty("core").GetRawText(), after.GetProperty("core").GetRawText());
        }

        private static void AssertInsertedIdsAreInChannel(JsonElement insertedIds, JsonElement channel)
        {
            string[] bundleIds = channel.GetProperty("bundles")
                .EnumerateArray()
                .Select(bundle => bundle.GetProperty("bundleId").GetString()!)
                .ToArray();
            foreach (JsonElement insertedId in insertedIds.EnumerateArray())
            {
                Assert.Contains(insertedId.GetString(), bundleIds);
            }
        }

        private static void AssertGameSnapshotMaps(
            JsonElement browser,
            GameSessionSnapshot expected,
            uint inspectedChannelIndex)
        {
            Assert.Equal(expected.ScenarioId, browser.GetProperty("scenarioId").GetString());
            Assert.Equal(expected.SimulationTimeSeconds, browser.GetProperty("simulationTimeSeconds").GetDouble(), 12);
            Assert.Equal(expected.WallElapsedSeconds, browser.GetProperty("wallElapsedSeconds").GetDouble(), 12);
            Assert.Equal(expected.NormalizedPowerFraction, browser.GetProperty("normalizedPowerFraction").GetDouble(), 12);
            Assert.Equal(expected.AbsoluteTiltFraction, browser.GetProperty("absoluteTiltFraction").GetDouble(), 12);
            Assert.Equal(expected.ControlMarginFraction, browser.GetProperty("controlMarginFraction").GetDouble(), 12);
            Assert.Equal(expected.DeviceAvailableFraction, browser.GetProperty("deviceAvailableFraction").GetDouble(), 12);
            Assert.Equal(expected.PendingActionCount, browser.GetProperty("pendingActionCount").GetUInt32());
            Assert.Equal(expected.ScoreTotal, browser.GetProperty("scoreTotal").GetDouble(), 12);
            Assert.Equal(expected.IsPaused, browser.GetProperty("isPaused").GetBoolean());
            Assert.Equal(expected.PlaybackModeId == PracticeGameSessionFactory.RealTimePlaybackModeId ? "1x" : expected.PlaybackModeId, browser.GetProperty("playbackModeId").GetString());
            Assert.Equal(expected.FreshBundlesAvailable, browser.GetProperty("freshBundlesAvailable").GetUInt32());
            Assert.Equal(expected.RefuellingOperationCount, browser.GetProperty("refuellingOperationCount").GetUInt32());
            Assert.Equal(expected.LastRefuelledChannel, browser.GetProperty("lastRefuelledChannel").GetInt32());
            Assert.Equal(expected.LastRefuellingShiftCount, browser.GetProperty("lastRefuellingShiftCount").GetUInt16());
            if (expected.RefuellingOperationCount == 0)
            {
                Assert.False(browser.TryGetProperty("lastRefuellingDirectionId", out _));
            }
            else
            {
                Assert.Equal(expected.LastRefuellingDirectionId, browser.GetProperty("lastRefuellingDirectionId").GetString());
            }

            JsonElement physics = browser.GetProperty("physics");
            Assert.Equal(expected.Physics.SourceId, physics.GetProperty("sourceId").GetString());
            Assert.Equal(expected.Physics.FormulationId, physics.GetProperty("formulationId").GetString());
            Assert.Equal(expected.Physics.ShapeMethodId, physics.GetProperty("shapeMethodId").GetString());
            Assert.Equal(expected.Physics.AmplitudeMethodId, physics.GetProperty("amplitudeMethodId").GetString());
            Assert.Equal(expected.Physics.ReactivityMethodId, physics.GetProperty("reactivityMethodId").GetString());
            Assert.Equal(expected.Physics.SolveState, physics.GetProperty("solveState").GetString());
            Assert.Equal(expected.Physics.IsAuthoritative, physics.GetProperty("isAuthoritative").GetBoolean());
            Assert.Equal(expected.Physics.TotalPowerWatts, physics.GetProperty("totalPowerWatts").GetDouble(), 10);
            Assert.Equal(expected.Physics.EffectiveK, physics.GetProperty("effectiveK").GetDouble(), 12);
            Assert.Equal(expected.Physics.Reactivity, physics.GetProperty("reactivity").GetDouble(), 12);

            JsonElement expectedChannel = browser.GetProperty("core").GetProperty("channels")[(int)inspectedChannelIndex];
            GameChannelPresentationSnapshot directChannel = expected.Core.GetChannel(inspectedChannelIndex);
            Assert.Equal(directChannel.ChannelIndex, expectedChannel.GetProperty("channelIndex").GetUInt32());
            Assert.Equal(directChannel.GridColumn, expectedChannel.GetProperty("gridColumn").GetInt32());
            Assert.Equal(directChannel.GridRow, expectedChannel.GetProperty("gridRow").GetInt32());
            Assert.Equal(
                directChannel.FlowDirection == FlowDirection.EndAtoEndB ? "toward-end-b" : "toward-end-a",
                expectedChannel.GetProperty("flowDirection").GetString());
            Assert.Equal(directChannel.AverageBurnupMwDayPerKg, expectedChannel.GetProperty("averageBurnupMwdPerKg").GetDouble(), 12);
            Assert.Equal(directChannel.PowerWatts, expectedChannel.GetProperty("powerWatts").GetDouble(), 10);
            Assert.Equal(directChannel.LocalPowerFraction, expectedChannel.GetProperty("localPowerFraction").GetDouble(), 12);
            Assert.Equal(directChannel.LocalTiltFraction, expectedChannel.GetProperty("localTiltFraction").GetDouble(), 12);
            Assert.Equal(directChannel.Bundles.Count, expectedChannel.GetProperty("bundles").GetArrayLength());
            for (int index = 0; index < directChannel.Bundles.Count; index++)
            {
                GameBundlePresentationSnapshot directBundle = directChannel.Bundles[index];
                JsonElement browserBundle = expectedChannel.GetProperty("bundles")[index];
                Assert.Equal(directBundle.Position, browserBundle.GetProperty("position").GetUInt32());
                Assert.Equal(directBundle.BundleId, browserBundle.GetProperty("bundleId").GetString());
                Assert.Equal(directBundle.FuelTypeId, browserBundle.GetProperty("fuelTypeId").GetString());
                Assert.Equal(directBundle.CurrentBurnupMwDayPerKg, browserBundle.GetProperty("currentBurnupMwdPerKg").GetDouble(), 12);
                Assert.Equal(directBundle.PowerWatts, browserBundle.GetProperty("powerWatts").GetDouble(), 10);
                Assert.Equal(directBundle.InsertedAtSeconds, browserBundle.GetProperty("insertedAtSeconds").GetDouble(), 12);
                Assert.Equal(directBundle.StateVersion, browserBundle.GetProperty("stateVersion").GetUInt64());
            }

            JsonElement xenon = browser.GetProperty("xenon");
            Assert.Equal(expected.Xenon.StateIdentity, xenon.GetProperty("stateIdentity").GetString());
            Assert.Equal(expected.Xenon.StateDigestHex, xenon.GetProperty("stateDigestHex").GetString());
            Assert.Equal(expected.Xenon.StateVersion, xenon.GetProperty("stateVersion").GetUInt64());
            Assert.Equal(expected.Xenon.NodeCount, xenon.GetProperty("nodeCount").GetInt32());
            Assert.Equal(expected.Xenon.SelectedChannelIndex, xenon.GetProperty("selectedChannelIndex").GetInt32());
        }

        private static void AssertFinite(double value)
        {
            Assert.False(double.IsNaN(value));
            Assert.False(double.IsInfinity(value));
        }

        private static JsonElement Parse(string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }
}
