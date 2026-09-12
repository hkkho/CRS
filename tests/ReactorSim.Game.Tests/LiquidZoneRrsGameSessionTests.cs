using System;
using System.Linq;
using System.Reflection;
using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class LiquidZoneRrsGameSessionTests
{
    [Fact]
    public void SnapshotPublishesAuthoritativeRrsRunStatus()
    {
        GameSession session = PracticeGameSessionFactory.Create();

        Assert.Equal(
            session.CurrentLiquidZoneRrs.IsGameOver,
            session.Snapshot.IsGameOver);
        Assert.Equal(
            session.CurrentLiquidZoneRrs.GameOverReason,
            session.Snapshot.GameOverReason);
        Assert.False(session.Snapshot.IsGameOver);
        Assert.Empty(session.Snapshot.GameOverReason);
    }

    [Theory]
    [InlineData(0.0, "liquid-zone-average-empty")]
    [InlineData(1.0, "liquid-zone-average-full")]
    public void ExhaustedRrsLocksOrdinaryCommandsWithoutChangingAcceptedState(
        double terminalFill,
        string expectedReason)
    {
        GameSession session = PracticeGameSessionFactory.Create();
        PracticeLiquidZoneRrsV1 terminal = CreateUniformFillState(
            session.CurrentLiquidZoneRrs,
            terminalFill);
        ReplacePracticeRrs(session, terminal);

        GameSessionSnapshot before = session.Snapshot;
        SyntheticGameCoreStateV1 beforeCoreState = session.CoreState;
        EquilibriumCoreProjectionV1 beforeProjection = session.CurrentEquilibriumProjection;
        PracticeLiquidZoneRrsV1 beforeRrs = session.CurrentLiquidZoneRrs;

        Assert.True(before.IsGameOver);
        Assert.Equal(expectedReason, before.GameOverReason);

        GameSessionCommandResult[] rejectedCommands =
        {
            session.AdvanceWallMilliseconds(1_000),
            session.QueuePowerTarget(0.95),
            session.QueueTiltTarget(0.05),
            session.SetPlaybackMode(PracticeGameSessionFactory.DebugPlaybackModeId),
            session.Pause(),
            session.Resume(),
            session.RefuelChannel(189, "toward-end-b", 4, "NAT-U-SYNTHETIC")
        };

        foreach (GameSessionCommandResult rejected in rejectedCommands)
        {
            Assert.False(rejected.Accepted);
            Assert.Equal("GameSession.Run.GameOver", rejected.DiagnosticCode);
            Assert.Equal(expectedReason, rejected.Snapshot.GameOverReason);
            AssertTerminalStateUnchanged(
                before,
                rejected.Snapshot,
                session,
                beforeCoreState,
                beforeProjection,
                beforeRrs);
        }
    }

    [Fact]
    public void DebugRestartCreatesAFreshNonterminalPracticeRun()
    {
        GameSession exhausted = PracticeGameSessionFactory.Create();
        ReplacePracticeRrs(
            exhausted,
            CreateUniformFillState(exhausted.CurrentLiquidZoneRrs, 1.0));

        Assert.True(exhausted.Snapshot.IsGameOver);

        GameSession restarted = PracticeGameSessionFactory.Create();
        Assert.False(restarted.Snapshot.IsGameOver);
        Assert.Empty(restarted.Snapshot.GameOverReason);

        GameSessionCommandResult advance = restarted.AdvanceWallMilliseconds(100);
        Assert.True(advance.Accepted, advance.DiagnosticMessage);
        Assert.False(advance.Snapshot.IsGameOver);
    }

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

    private static PracticeLiquidZoneRrsV1 CreateUniformFillState(
        PracticeLiquidZoneRrsV1 source,
        double fill)
    {
        double[] fills = Enumerable.Repeat(
            fill,
            (int)PracticeLiquidZoneRrsIdentityV1.LogicalZoneCount).ToArray();
        StaticAbsorptionOverlayV1 overlay = Require(
            source.Mapping.TryBuildOverlay(fills));
        ConstructorInfo constructor = typeof(PracticeLiquidZoneRrsV1)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();
        return (PracticeLiquidZoneRrsV1)constructor.Invoke(
            new object?[]
            {
                source.Mapping,
                fills,
                source.ReferenceZonalPowerFractions,
                source.TargetZonalPowerFractions,
                source.MeasuredZonalPowerFractions,
                source.ZonalShapeErrors,
                overlay,
                source.TargetPowerWatts,
                source.MeasuredPowerWatts,
                source.CoreReactivity,
                source.CompensatedNetReactivity,
                source.CommonModeRhoCorrection,
                source.ControllerIterationCount,
                source.ControllerConverged,
                source.SimulationTimeSeconds,
                source.ResponseModel,
                source.CorrectionResponseModel,
                source.AppliedFillCommand,
                source.UncompensatedWeightedResidual,
                source.ControlledBaselineWeightedResidual,
                source.CombinedWeightedResidual,
                source.BaseCandidateSolveCount,
                source.ControlledBaselineCandidateSolveCount,
                source.VerificationCandidateSolveCount,
                source.CorrectionCandidateSolveCount,
                source.CorrectionApplied
            });
    }

    private static void ReplacePracticeRrs(
        GameSession session,
        PracticeLiquidZoneRrsV1 replacement)
    {
        FieldInfo? field = typeof(GameSession).GetField(
            "_practiceRrs",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException(
                "The GameSession practice RRS field was not found.");
        }

        field.SetValue(session, replacement);
    }

    private static void AssertTerminalStateUnchanged(
        GameSessionSnapshot expected,
        GameSessionSnapshot actual,
        GameSession session,
        SyntheticGameCoreStateV1 expectedCoreState,
        EquilibriumCoreProjectionV1 expectedProjection,
        PracticeLiquidZoneRrsV1 expectedRrs)
    {
        Assert.Equal(expected.SimulationTimeSeconds, actual.SimulationTimeSeconds);
        Assert.Equal(expected.WallElapsedSeconds, actual.WallElapsedSeconds);
        Assert.Equal(expected.PlaybackModeId, actual.PlaybackModeId);
        Assert.Equal(expected.PendingActionCount, actual.PendingActionCount);
        Assert.Equal(expected.ScoreTotal, actual.ScoreTotal);
        Assert.Equal(expected.TurnSummaryCount, actual.TurnSummaryCount);
        Assert.Equal(expected.IsPaused, actual.IsPaused);
        Assert.Equal(expected.FreshBundlesAvailable, actual.FreshBundlesAvailable);
        Assert.Equal(expected.RefuellingOperationCount, actual.RefuellingOperationCount);
        Assert.Equal(expected.Core.Physics.BindingVersion, actual.Core.Physics.BindingVersion);
        Assert.Equal(
            expected.Core.Physics.ReactivityBindingDigestHex,
            actual.Core.Physics.ReactivityBindingDigestHex);
        Assert.Equal(expected.Rrs.StateDigestHex, actual.Rrs.StateDigestHex);
        Assert.Equal(expected.Rrs.OverlayDigestHex, actual.Rrs.OverlayDigestHex);
        Assert.Same(expectedCoreState, session.CoreState);
        Assert.Same(expectedProjection, session.CurrentEquilibriumProjection);
        Assert.Same(expectedProjection.LegacyPresentationProjection, session.CurrentSpatialCandidate);
        Assert.Same(expectedRrs, session.CurrentLiquidZoneRrs);
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        if (!result.IsValid)
        {
            Assert.Fail(result.FirstDiagnostic.Message);
        }

        return result.Value;
    }
}
