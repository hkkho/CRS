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
        Assert.Equal(session.CurrentLiquidZoneRrs.DecisionCode, session.Snapshot.Rrs.DecisionCode);
        Assert.NotEmpty(session.Snapshot.Rrs.DecisionExplanation);
        var limiting = session.Snapshot.Rrs.GetZone((uint)session.Snapshot.Rrs.LimitingZoneId);
        Assert.Equal(session.Snapshot.Rrs.Zones.Min(z => Math.Min(z.FillFraction, 1 - z.FillFraction)),
            Math.Min(limiting.FillFraction, 1 - limiting.FillFraction));
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
        Assert.Equal(terminalFill == 0 ? "exhausted-empty" : "exhausted-full", before.Rrs.DecisionCode);
        Assert.Contains("exhausted", before.Rrs.DecisionExplanation);

        SyntheticGameCoreStateV1 beforeCoreState = session.CoreState;
        EquilibriumCoreProjectionV1 beforeProjection = session.CurrentEquilibriumProjection;
        PracticeLiquidZoneRrsV1 beforeRrs = session.CurrentLiquidZoneRrs;

        Assert.True(before.IsGameOver);
        Assert.Equal(expectedReason, before.GameOverReason);

        GameSessionCommandResult[] rejectedCommands =
        {
            session.AdvanceWallMilliseconds(1_000),
            session.QueuePowerTarget(0.95),
            session.SetPlaybackMode(PracticeGameSessionFactory.DebugPlaybackModeId),
            session.Pause(),
            session.Resume(),
            session.RefuelChannel(189, "toward-end-b", 8, "NAT-U-SYNTHETIC")
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

    [Theory]
    [InlineData(0.09, true, "below 10%")]
    [InlineData(0.10, false, "")]
    [InlineData(0.90, false, "")]
    [InlineData(0.91, true, "above 90%")]
    public void AverageLevelLimitsEndTheRunBeforePhysicalExhaustion(double level, bool terminal, string reason)
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest(challenge: true);
        ReplacePracticeRrs(session, CreateUniformFillState(session.CurrentLiquidZoneRrs, level));
        var before = session.Snapshot;
        Assert.False(before.Rrs.IsGameOver);
        Assert.Equal(terminal, before.IsGameOver);
        Assert.Contains(reason, before.GameOverReason);
        if (!terminal) return;
        Assert.Equal("ended", before.Shift.Outcome);
        Assert.False(before.Shift.RewardEarned);
        var rejected = session.AdvanceWallMilliseconds(1000);
        Assert.False(rejected.Accepted);
        Assert.Equal(before.SimulationTimeSeconds, rejected.Snapshot.SimulationTimeSeconds);
        Assert.Equal(before.Shift.ThermalEnergyMwh, rejected.Snapshot.Shift.ThermalEnergyMwh);
        Assert.False(session.RefuelChannel(189, "toward-end-b", 8, "NAT-U-SYNTHETIC").Accepted);
    }

    [Fact]
    public void OperatingLossTakesPrecedenceOverHorizonCompletion()
    {
        var session = PracticeGameSessionFactory.Create();
        Assert.Equal("completed", session.AdvanceWallMilliseconds(60_000).Snapshot.RunStatus);
        ReplacePracticeRrs(session, CreateUniformFillState(session.CurrentLiquidZoneRrs, 0.91));
        var ended = session.Snapshot;
        Assert.Equal("ended", ended.RunStatus);
        Assert.Equal("ended", ended.Shift.Outcome);
        Assert.Contains("above 90%", ended.GameOverReason);
        Assert.False(ended.Shift.RewardEarned);
    }

    [Fact]
    public void ShortTickReusesProjectionWhileBaseSecondRunsHalfHourRrsAndChangesKeff()
    {
        GameSession session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        var initialProjection = session.CurrentEquilibriumProjection;
        double[] initialFills = session.CurrentLiquidZoneRrs.ZoneFills.ToArray();

        GameSessionCommandResult shortTick = session.AdvanceWallMilliseconds(100);

        Assert.True(shortTick.Accepted, shortTick.DiagnosticMessage);
        Assert.Same(initialProjection, session.CurrentEquilibriumProjection);
        Assert.Equal(initialFills, session.CurrentLiquidZoneRrs.ZoneFills);

        GameSessionCommandResult boundary = session.AdvanceWallMilliseconds(900);

        Assert.True(boundary.Accepted, boundary.DiagnosticMessage);
        Assert.NotSame(initialProjection, session.CurrentEquilibriumProjection);
        Assert.Equal(1_800.0, boundary.Snapshot.SimulationTimeSeconds);
        Assert.NotEqual(initialProjection.EffectiveK, boundary.Snapshot.Physics.EffectiveK);
        // A settled aged core can retain fills when the tiny burnup change
        // offers no improvement to the combined spatial/criticality residual.
        Assert.Equal(1_800.0, session.CurrentLiquidZoneRrs.SimulationTimeSeconds);
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
            189, "toward-end-b", 8, "NAT-U-SYNTHETIC");
        GameSessionCommandResult secondResult = second.RefuelChannel(
            189, "toward-end-b", 8, "NAT-U-SYNTHETIC");

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
            189, "toward-end-b", 8, "UNSUPPORTED-FUEL");

        Assert.False(rejected.Accepted);
        Assert.Same(acceptedRrs, first.CurrentLiquidZoneRrs);
        Assert.Same(acceptedProjection, first.CurrentEquilibriumProjection);
    }

    [Fact]
    public void AgedCoreFeedbackRegulatesCriticalityWithinCandidateAndFillBounds()
    {
        GameSession session = PracticeGameSessionFactory.Create();
        PracticeLiquidZoneRrsV1 before = session.CurrentLiquidZoneRrs;

        GameSessionCommandResult result = session.RefuelChannel(
            75, "toward-end-a", 8, "NAT-U-SYNTHETIC");

        Assert.True(result.Accepted, result.DiagnosticMessage);
        PracticeLiquidZoneRrsV1 after = session.CurrentLiquidZoneRrs;
        Assert.Equal(1, after.BaseCandidateSolveCount);
        Assert.Equal(1, after.ControlledBaselineCandidateSolveCount);
        Assert.InRange(after.VerificationCandidateSolveCount, 0, 1);
        // An aged start can already meet tolerance after its first response;
        // the extra secant solve is conditional on that measured residual.
        Assert.InRange(after.CorrectionCandidateSolveCount, 0, 1);
        Assert.Equal(2 + after.VerificationCandidateSolveCount + after.CorrectionCandidateSolveCount, after.CandidateSolveCount);
        Assert.True(after.AverageFillFraction > before.AverageFillFraction);
        Assert.True(after.CombinedWeightedResidual <= after.ControlledBaselineWeightedResidual +
            PracticeLiquidZoneRrsIdentityV1.ResidualAcceptanceTolerance);
        Assert.InRange(Math.Abs(after.CompensatedNetReactivity), 0.0, 2e-5);
        Assert.All(after.AppliedFillCommand, movement => Assert.InRange(
            Math.Abs(movement), 0.0, PracticeLiquidZoneRrsIdentityV1.MaxFillMovementPerEvent + 1.0e-12));
        var effective = session.CurrentEquilibriumProjection.StaticAbsorptionOverlay!;
        Assert.NotEqual(after.OverlayDigest, effective.OverlayDigest);
        foreach (var zoneEntry in after.AbsorptionOverlay.Entries)
        {
            Assert.True(effective.TryGetEntry(zoneEntry.Node, out var entry));
            Assert.True(session.CurrentXenonState.Overlay.TryGetEntry(zoneEntry.Node, out var poison));
            Assert.Equal(zoneEntry.DeltaAbsorptionGroup2PerM + poison!.DeltaAbsorptionGroup2PerM,
                entry!.DeltaAbsorptionGroup2PerM);
        }
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
                source.CorrectionApplied,
                source.DecisionCode
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
        Assert.Same(expectedProjection, session.CurrentSpatialCandidate);
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
