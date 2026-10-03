using System.Globalization;
using ReactorSim.Browser;
using Xunit;

namespace ReactorSim.Browser.Tests;

public sealed partial class PlaytestBridgeTests
{
    [Fact]
    public void RunProvenanceAgreesAcrossFullCompactAndResetAndDebugRoutesStayUnavailable()
    {
        Parse(PlaytestBridgeV2.Initialize(PlayRequest));
        var start = Parse(PlaytestBridgeV2.Dispatch("{\"protocol\":\"candu-playtest-v2\",\"type\":\"reset\",\"shiftId\":\"useful-fuel-day-v1\"}"));
        Assert.True(start.GetProperty("snapshot").GetProperty("provenance").GetProperty("eligibleForStandardChallenge").GetBoolean());
        var debug = Parse(PlaytestBridgeV2.Dispatch("{\"protocol\":\"candu-playtest-v2\",\"type\":\"debug-grant-fresh-bundles\",\"additionalBundles\":100}"));
        Assert.False(debug.GetProperty("accepted").GetBoolean());
        Assert.Equal(start.GetProperty("snapshot").GetProperty("provenance").GetRawText(), debug.GetProperty("snapshot").GetProperty("provenance").GetRawText());
        var edited = Parse(PlaytestBridgeV2.Dispatch(CompactCommand(debug.GetProperty("sequence").GetUInt64(), "configure-cell",
            "\"channelIndex\":0,\"position\":0,\"hasFuel\":false,\"reflectiveFaces\":[]")));
        AssertAccepted(edited);
        // Engineering commands intentionally return a full snapshot even when compact is requested.
        var provenance = edited.GetProperty("snapshot").GetProperty("provenance");
        var compact = Parse(PlaytestBridgeV2.Dispatch(CompactCommand(edited.GetProperty("sequence").GetUInt64(), "pause")));
        AssertAccepted(compact);
        Assert.Equal(provenance.GetRawText(), compact.GetProperty("snapshotPatch").GetProperty("provenance").GetRawText());
        Assert.Equal("modified-sandbox", provenance.GetProperty("kind").GetString());
        Assert.False(provenance.GetProperty("eligibleForStandardChallenge").GetBoolean());
        Assert.Equal(provenance.GetRawText(), Parse(PlaytestBridgeV2.GetSnapshotJson()).GetProperty("provenance").GetRawText());
        var retry = Parse(PlaytestBridgeV2.Dispatch("{\"protocol\":\"candu-playtest-v2\",\"type\":\"reset\"}"));
        AssertAccepted(retry);
        Assert.Equal(start.GetProperty("snapshot").GetProperty("provenance").GetRawText(), retry.GetProperty("snapshot").GetProperty("provenance").GetRawText());
    }

    [Fact]
    public void ShiftProgressAgreesAcrossFullCompactAndSeededRetries()
    {
        var initialized = Parse(PlaytestBridgeV2.Initialize(PlayRequest));
        var challenge = Parse(PlaytestBridgeV2.Dispatch("{\"protocol\":\"candu-playtest-v2\",\"type\":\"reset\",\"seed\":42,\"shiftId\":\"useful-fuel-day-v1\"}"));
        AssertAccepted(challenge);
        var shift = challenge.GetProperty("snapshot").GetProperty("shift");
        Assert.Equal("useful-fuel-day-v1", shift.GetProperty("id").GetString());
        Assert.Equal(42, shift.GetProperty("seed").GetInt32());
        Assert.Equal(86400, shift.GetProperty("horizonSeconds").GetDouble());
        var resumed = Parse(PlaytestBridgeV2.Dispatch("{\"protocol\":\"candu-playtest-v2\",\"type\":\"resume\"}"));
        var compact = Parse(PlaytestBridgeV2.Dispatch(CompactCommand(resumed.GetProperty("sequence").GetUInt64(), "advance", "\"wallMilliseconds\":100")));
        AssertAccepted(compact);
        var full = Parse(PlaytestBridgeV2.GetSnapshotJson());
        Assert.Equal(full.GetProperty("shift").GetRawText(), compact.GetProperty("snapshotPatch").GetProperty("shift").GetRawText());
        Assert.True(full.GetProperty("shift").GetProperty("thermalEnergyMwh").GetDouble() > 0);
        var invalid = Parse(PlaytestBridgeV2.Dispatch("{\"protocol\":\"candu-playtest-v2\",\"type\":\"reset\",\"shiftId\":\"unknown\"}"));
        Assert.False(invalid.GetProperty("accepted").GetBoolean());
        Assert.Equal(full.GetProperty("shift").GetRawText(), invalid.GetProperty("snapshot").GetProperty("shift").GetRawText());
        var retry = Parse(PlaytestBridgeV2.Dispatch("{\"protocol\":\"candu-playtest-v2\",\"type\":\"reset\"}"));
        AssertAccepted(retry);
        Assert.Equal(shift.GetRawText(), retry.GetProperty("snapshot").GetProperty("shift").GetRawText());
        var practice = Parse(PlaytestBridgeV2.Dispatch("{\"protocol\":\"candu-playtest-v2\",\"type\":\"reset\",\"seed\":43,\"shiftId\":\"free-practice\"}"));
        AssertAccepted(practice);
        Assert.Equal("free-practice", practice.GetProperty("snapshot").GetProperty("shift").GetProperty("id").GetString());
        Assert.Equal(43, practice.GetProperty("snapshot").GetProperty("shift").GetProperty("seed").GetInt32());
    }

    [Fact]
    public void FuelMovementAndPlansSurviveFullCompactAndRejectedResponses()
    {
        var start = Parse(PlaytestBridgeV2.Initialize(PlayRequest));
        Assert.Equal(4, start.GetProperty("snapshot").GetProperty("refuellingPlans").GetArrayLength());
        var moved = Parse(PlaytestBridgeV2.Dispatch(CompactCommand(start.GetProperty("sequence").GetUInt64(), "commit-refuel",
            "\"request\":{\"channelIndex\":210,\"directionId\":\"toward-end-a\",\"shiftCount\":8,\"fuelTypeId\":\"NAT-U-SYNTHETIC\"}")));
        AssertAccepted(moved);
        var full = Parse(PlaytestBridgeV2.GetSnapshotJson());
        Assert.Equal(full.GetProperty("lastFuelMovement").GetRawText(), moved.GetProperty("snapshotPatch").GetProperty("lastFuelMovement").GetRawText());
        var rrs = full.GetProperty("rrs");
        Assert.Equal(rrs.GetProperty("decisionCode").GetString(), moved.GetProperty("snapshotPatch").GetProperty("rrs").GetProperty("decisionCode").GetString());
        Assert.NotEmpty(rrs.GetProperty("decisionExplanation").GetString()!);
        Assert.InRange(rrs.GetProperty("limitingZoneId").GetInt32(), 0, 13);

        Assert.Equal(20, full.GetProperty("lastFuelMovement").GetProperty("bundles").GetArrayLength());
        var rejected = Parse(PlaytestBridgeV2.Dispatch("{\"protocol\":\"candu-playtest-v2\",\"type\":\"commit-refuel\",\"request\":{\"channelIndex\":999,\"directionId\":\"toward-end-b\",\"shiftCount\":4,\"fuelTypeId\":\"NAT-U-SYNTHETIC\"}}"));
        Assert.False(rejected.GetProperty("accepted").GetBoolean());
        Assert.Equal(full.GetProperty("lastFuelMovement").GetRawText(), rejected.GetProperty("snapshot").GetProperty("lastFuelMovement").GetRawText());
    }

    private static readonly double[] AppliedTargets = { 0.8, 0.95, 1.0, 1.2 };

    [Fact]
    public void AppliedTargetAndRunStatusAgreeInFullAndCompactResponses()
    {
        var initialized = Parse(PlaytestBridgeV2.Initialize(PlayRequest));
        Assert.Equal("running", initialized.GetProperty("snapshot").GetProperty("runStatus").GetString());
        foreach (double target in AppliedTargets)
        {
            var paused = Parse(PlaytestBridgeV2.Dispatch("{\"protocol\":\"candu-playtest-v2\",\"type\":\"pause\"}"));
            double previousTarget = paused.GetProperty("snapshot").GetProperty("targetPowerFraction").GetDouble();
            var queued = Parse(PlaytestBridgeV2.Dispatch(CompactCommand(paused.GetProperty("sequence").GetUInt64(),
                "queue-power-target", "\"targetFraction\":" + target.ToString(CultureInfo.InvariantCulture))));
            // The public UI requires resume before accepting a target.
            Assert.False(queued.GetProperty("accepted").GetBoolean());
            Assert.Equal(previousTarget, queued.GetProperty("snapshotPatch").GetProperty("targetPowerFraction").GetDouble());
            Assert.Equal("paused", queued.GetProperty("snapshotPatch").GetProperty("runStatus").GetString());
            var resumed = Parse(PlaytestBridgeV2.Dispatch("{\"protocol\":\"candu-playtest-v2\",\"type\":\"resume\"}"));
            queued = Parse(PlaytestBridgeV2.Dispatch(CompactCommand(resumed.GetProperty("sequence").GetUInt64(),
                "queue-power-target", "\"targetFraction\":" + target.ToString(CultureInfo.InvariantCulture))));
            AssertAccepted(queued);
            Assert.Equal(previousTarget, queued.GetProperty("snapshotPatch").GetProperty("targetPowerFraction").GetDouble());
            var advanced = Parse(PlaytestBridgeV2.Dispatch(CompactCommand(queued.GetProperty("sequence").GetUInt64(),
                "advance", "\"wallMilliseconds\":100")));
            AssertAccepted(advanced);
            var patch = advanced.GetProperty("snapshotPatch");
            var full = Parse(PlaytestBridgeV2.GetSnapshotJson());
            Assert.Equal(target, patch.GetProperty("targetPowerFraction").GetDouble(), 10);
            Assert.Equal(target, full.GetProperty("targetPowerFraction").GetDouble(), 10);
            Assert.Equal("running", patch.GetProperty("runStatus").GetString());
            Assert.Equal(full.GetProperty("runStatus").GetString(), patch.GetProperty("runStatus").GetString());
            Assert.Equal(string.Empty, patch.GetProperty("runEndReason").GetString());
            Assert.Equal(target, full.GetProperty("physics").GetProperty("targetPowerWatts").GetDouble() /
                full.GetProperty("physics").GetProperty("referencePowerWatts").GetDouble(), 10);
        }
    }
}
