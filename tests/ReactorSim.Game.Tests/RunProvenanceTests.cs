using System;
using System.Collections.Generic;
using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class RunProvenanceTests
{
    [Fact]
    public void AcceptedPhysicalEditsPersistReasonsButRejectedAndNoOpEditsDoNot()
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest(1001, challenge: true);
        var standard = session.Snapshot.Provenance;
        Assert.True(standard.EligibleForStandardChallenge);
        Assert.True(session.ConfigureCell(0, 0, true, Array.Empty<TopologyFace>()).Accepted);
        Assert.True(session.Snapshot.Provenance.EligibleForStandardChallenge);
        Assert.False(session.ConfigureCell(999, 0, false, Array.Empty<TopologyFace>()).Accepted);
        Assert.True(session.Snapshot.Provenance.EligibleForStandardChallenge);
        Assert.True(session.SolveConfiguredCore().Accepted);
        Assert.True(session.Snapshot.Provenance.EligibleForStandardChallenge);
        var edited = session.ConfigureCell(0, 0, false, Array.Empty<TopologyFace>());
        Assert.True(edited.Accepted, edited.DiagnosticMessage);
        Assert.Equal("modified-sandbox", edited.Snapshot.Provenance.Kind);
        Assert.False(edited.Snapshot.Provenance.EligibleForStandardChallenge);
        Assert.Single(edited.Snapshot.Provenance.Reasons);
        Assert.Empty(standard.Reasons);
        var restored = session.ConfigureCell(0, 0, true, Array.Empty<TopologyFace>());
        Assert.True(restored.Accepted);
        Assert.Equal(edited.Snapshot.Provenance.Reasons, restored.Snapshot.Provenance.Reasons);
        Assert.False(session.DebugGrantFreshBundles(0).Accepted);
        Assert.Single(session.Snapshot.Provenance.Reasons);
        Assert.True(session.DebugGrantFreshBundles(1).Accepted);
        Assert.Contains("Developer fuel grant", session.Snapshot.Provenance.Reasons);
        Assert.Single(edited.Snapshot.Provenance.Reasons);
        Assert.True(session.RefuelChannel(0, "toward-end-b", 4, "NAT-U-SYNTHETIC").Accepted);
        Assert.True(session.DebugResetSyntheticResponse().Accepted);
        Assert.Contains("Developer score reset", session.Snapshot.Provenance.Reasons);
        Assert.True(PracticeGameSessionFactory.CreateBrowserPlaytest(1001, challenge: true).Snapshot.Provenance.EligibleForStandardChallenge);
    }

    [Fact]
    public void ModifiedChallengeCanMeetObjectiveWithoutEarningStandardReward()
    {
        var progress = new ShiftProgress(true, 1001, 86400, 86400, true, true, 8, 8, 0, 42, 12, 30, standardRun: false);
        Assert.Equal("success", progress.Outcome);
        Assert.False(progress.RewardEarned);
        var reasons = new List<string> { "Zone geometry edited" };
        var provenance = new RunProvenance(true, reasons);
        reasons.Clear();
        Assert.Single(provenance.Reasons);
        Assert.False(provenance.EligibleForStandardChallenge);
    }
}
