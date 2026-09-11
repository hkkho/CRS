using System;
using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class LiquidZoneRrsGameSessionTests
{
    [Fact]
    public void ShortTickReusesProjectionWhileHourlyBoundaryRunsStaticRrs()
    {
        GameSession session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        var initialProjection = session.CurrentEquilibriumProjection;
        double[] initialFills = session.CurrentLiquidZoneRrs.ZoneFills.ToArray();

        GameSessionCommandResult shortTick = session.AdvanceWallMilliseconds(100);

        Assert.True(shortTick.Accepted, shortTick.DiagnosticMessage);
        Assert.Same(initialProjection, session.CurrentEquilibriumProjection);
        Assert.Equal(initialFills, session.CurrentLiquidZoneRrs.ZoneFills);

        GameSessionCommandResult boundary = session.AdvanceWallMilliseconds(1_900);

        Assert.True(boundary.Accepted, boundary.DiagnosticMessage);
        Assert.NotSame(initialProjection, session.CurrentEquilibriumProjection);
        Assert.Equal(3_600.0, boundary.Snapshot.SimulationTimeSeconds);
        Assert.Equal(1, session.CurrentLiquidZoneRrs.ControllerIterationCount);
        Assert.InRange(
            boundary.Snapshot.Rrs.CandidateSolveCount,
            2,
            PracticeLiquidZoneRrsIdentityV1.MaximumCandidateSolveCount);
    }

    [Fact]
    public void RefuelRunsDeterministicBoundedControlAndRejectedCommandRollsBackRrs()
    {
        GameSession first = PracticeGameSessionFactory.Create();
        GameSession second = PracticeGameSessionFactory.Create();

        GameSessionCommandResult firstResult = first.RefuelChannel(
            189, "toward-end-b", 4, "NAT-U-SYNTHETIC");
        GameSessionCommandResult secondResult = second.RefuelChannel(
            189, "toward-end-b", 4, "NAT-U-SYNTHETIC");

        Assert.True(firstResult.Accepted, firstResult.DiagnosticMessage);
        Assert.True(secondResult.Accepted, secondResult.DiagnosticMessage);
        Assert.Equal(
            first.CurrentLiquidZoneRrs.StateDigest,
            second.CurrentLiquidZoneRrs.StateDigest);
        Assert.Equal(
            first.CurrentEquilibriumProjection.ReactivityBindingDigest,
            second.CurrentEquilibriumProjection.ReactivityBindingDigest);
        Assert.Equal(1, first.CurrentLiquidZoneRrs.ControllerIterationCount);
        Assert.InRange(
            firstResult.Snapshot.Rrs.CandidateSolveCount,
            2,
            PracticeLiquidZoneRrsIdentityV1.MaximumCandidateSolveCount);
        Assert.Equal(
            first.CurrentLiquidZoneRrs.ResponseModelIdentity,
            firstResult.Snapshot.Rrs.ResponseModelIdentity);
        Assert.Equal(
            first.CurrentLiquidZoneRrs.AppliedFillCommand,
            firstResult.Snapshot.Rrs.AppliedFillCommand);
        Assert.Equal(
            first.CurrentLiquidZoneRrs.CombinedWeightedResidual,
            firstResult.Snapshot.Rrs.CombinedWeightedResidual);
        Assert.True(
            firstResult.Snapshot.Rrs.CombinedWeightedResidual <=
            firstResult.Snapshot.Rrs.ControlledBaselineWeightedResidual +
            PracticeLiquidZoneRrsIdentityV1.ResidualAcceptanceTolerance);
        Assert.All(first.CurrentLiquidZoneRrs.ZoneFills, fill => Assert.InRange(fill, 0.0, 1.0));
        Assert.True(
            Math.Abs(first.CurrentLiquidZoneRrs.CompensatedNetReactivity) <=
            Math.Abs(first.CurrentLiquidZoneRrs.CoreReactivity));

        var acceptedRrs = first.CurrentLiquidZoneRrs;
        var acceptedProjection = first.CurrentEquilibriumProjection;
        GameSessionCommandResult rejected = first.RefuelChannel(
            189, "toward-end-b", 4, "UNSUPPORTED-FUEL");

        Assert.False(rejected.Accepted);
        Assert.Same(acceptedRrs, first.CurrentLiquidZoneRrs);
        Assert.Same(acceptedProjection, first.CurrentEquilibriumProjection);
    }

    [Fact]
    public void RejectedVerificationDoesNotConsumeCorrectionSolveOrReplaceBaseline()
    {
        GameSession session = PracticeGameSessionFactory.Create();
        PracticeLiquidZoneRrsV1 before = session.CurrentLiquidZoneRrs;
        Digest32 baselineOverlayDigest =
            session.CurrentEquilibriumProjection.StaticAbsorptionOverlay!.OverlayDigest;

        GameSessionCommandResult result = session.RefuelChannel(
            189, "toward-end-b", 4, "NAT-U-SYNTHETIC");

        Assert.True(result.Accepted, result.DiagnosticMessage);
        PracticeLiquidZoneRrsV1 after = session.CurrentLiquidZoneRrs;
        Assert.Equal(1, after.BaseCandidateSolveCount);
        Assert.Equal(1, after.ControlledBaselineCandidateSolveCount);
        Assert.Equal(1, after.VerificationCandidateSolveCount);
        Assert.Equal(0, after.CorrectionCandidateSolveCount);
        Assert.Equal(3, after.CandidateSolveCount);
        Assert.False(after.CorrectionApplied);
        Assert.Equal(before.ZoneFills, after.ZoneFills);
        Assert.Equal(baselineOverlayDigest, after.OverlayDigest);
        Assert.Equal(
            baselineOverlayDigest,
            session.CurrentEquilibriumProjection.StaticAbsorptionOverlay!.OverlayDigest);
        Assert.All(after.AppliedFillCommand, command => Assert.Equal(0.0, command));
        Assert.Equal(
            after.ControlledBaselineWeightedResidual,
            after.CombinedWeightedResidual);
    }
}
