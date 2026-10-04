using System;
using System.Linq;
using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Game.Tests;

public sealed class PracticeXenonGameTests
{
    [Fact]
    public void EquilibriumStartIsCoupledAndPausePreservesPoison()
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        var before = session.Snapshot;
        Assert.True(before.Xenon.HasCoupling);
        Assert.True(before.Xenon.MeanI135NumberDensityM3 > 0);
        Assert.True(before.Xenon.MeanXe135NumberDensityM3 > 0);
        Assert.All(before.Rrs.Zones, z => Assert.True(z.MeanXe135NumberDensityM3 > 0));
        Assert.True(session.CurrentXenonState.Overlay.IsZero);
        Assert.True(session.Pause().Accepted);
        Assert.True(session.AdvanceWallMilliseconds(1000).Accepted);
        Assert.Equal(before.Xenon.StateDigestHex, session.Snapshot.Xenon.StateDigestHex);
    }

    [Theory]
    [InlineData("toward-end-a", 8)]
    [InlineData("toward-end-b", 8)]
    public void RefuellingCarriesRetainedBundlePoisonAndFreshFuelIsClean(string direction, ushort shift)
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        var old = session.CoreState.EnumerateBundles().Select((b, n) =>
            (b.BundleId, I: session.CurrentXenonState.Iodine[n], X: session.CurrentXenonState.Xenon[n]))
            .ToDictionary(b => b.BundleId);
        var result = session.RefuelChannel(210, direction, shift, "NAT-U-SYNTHETIC");
        Assert.True(result.Accepted, result.DiagnosticMessage);
        int fresh = 0, node = 0;
        foreach (var bundle in session.CoreState.EnumerateBundles())
        {
            if (old.TryGetValue(bundle.BundleId, out var retained))
            {
                Assert.Equal(retained.I, session.CurrentXenonState.Iodine[node]);
                Assert.Equal(retained.X, session.CurrentXenonState.Xenon[node]);
            }
            else
            {
                fresh++;
                Assert.Equal(0, session.CurrentXenonState.Iodine[node]);
                Assert.Equal(0, session.CurrentXenonState.Xenon[node]);
            }
            node++;
        }
        Assert.Equal(shift, fresh);
        var poison = session.CurrentXenonState;
        Assert.False(session.RefuelChannel(210, direction, 3, "NAT-U-SYNTHETIC").Accepted);
        Assert.Same(poison, session.CurrentXenonState);
    }

    [Fact]
    public void AsymmetricXenonChangesLocalPowerAndZoneResponse()
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        var initial = session.CurrentEquilibriumProjection;
        var rrs = session.CurrentLiquidZoneRrs;
        var moved = session.CoreState.TryRefuel(210, GameRefuellingDirectionV1.TowardEndB, 4, "NAT-U-SYNTHETIC", 0);
        Assert.True(moved.IsValid, moved.IsValid ? null : moved.FirstDiagnostic.ToString());
        var poison = session.CurrentXenonState.Rebind(moved.Value.ResultingState);
        var model = FullCoreDiffusionModelV1.TryCreateCandu6(initial.DataPack).Value;
        var solver = EquilibriumCoreSolverV1.TryCreate(model, session.CoreState.EnumerateBundles(),
            PracticeGameSessionFactory.PracticeReferencePowerWatts).Value;
        var withXenon = PracticeLiquidZoneRrsV1.TryRunEquilibrium(solver,
            moved.Value.ResultingState.EnumerateBundles(), rrs, 0, backgroundOverlay: poison.Overlay);
        var withoutXenon = PracticeLiquidZoneRrsV1.TryRunEquilibrium(solver,
            moved.Value.ResultingState.EnumerateBundles(), rrs, 0);
        Assert.True(withXenon.IsValid, withXenon.IsValid ? null : withXenon.FirstDiagnostic.ToString());
        Assert.True(withoutXenon.IsValid, withoutXenon.IsValid ? null : withoutXenon.FirstDiagnostic.ToString());
        Assert.True(withXenon.Value.Projection.ShapeChannelPowerWatts[210] >
            withoutXenon.Value.Projection.ShapeChannelPowerWatts[210]);
        // The first refuel correction can saturate the fill movement limit in
        // both branches. Compare their settled response, keeping Xe frozen.
        for (int pass = 0; pass < 4; pass++)
        {
            withXenon = PracticeLiquidZoneRrsV1.TryRunEquilibrium(solver,
                moved.Value.ResultingState.EnumerateBundles(), withXenon.Value.State, 0,
                withXenon.Value.Projection.SpatialSolve, poison.Overlay);
            withoutXenon = PracticeLiquidZoneRrsV1.TryRunEquilibrium(solver,
                moved.Value.ResultingState.EnumerateBundles(), withoutXenon.Value.State, 0,
                withoutXenon.Value.Projection.SpatialSolve);
            Assert.True(withXenon.IsValid);
            Assert.True(withoutXenon.IsValid);
        }
        // Small poison differences need not move fills inside the gameplay bands.
        Assert.True(withXenon.Value.State.ControllerConverged);
        Assert.True(withoutXenon.Value.State.ControllerConverged);

        // A larger poison transient must still feed back into zone regulation.
        var transient = poison.Advance(withXenon.Value.Projection, 0.5, 6 * 3600);
        var regulated = PracticeLiquidZoneRrsV1.TryRunEquilibrium(solver,
            moved.Value.ResultingState.EnumerateBundles(), withXenon.Value.State, 6 * 3600,
            withXenon.Value.Projection.SpatialSolve, transient.Overlay);
        Assert.True(regulated.IsValid, regulated.IsValid ? null : regulated.FirstDiagnostic.ToString());
        Assert.True(regulated.Value.State.ZoneFills.Where((f, n) =>
            Math.Abs(f - withXenon.Value.State.ZoneFills[n]) > 1e-8).Any());
    }

    [Fact]
    public void HalfHourCouplingAgreesWithFiveMinuteReferenceAfterRefuelling()
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        Assert.True(session.RefuelChannel(210, "toward-end-b", 8, "NAT-U-SYNTHETIC").Accepted);
        var model = FullCoreDiffusionModelV1.TryCreateCandu6(session.CurrentEquilibriumProjection.DataPack).Value;
        var solver = EquilibriumCoreSolverV1.TryCreate(model, session.CoreState.EnumerateBundles(),
            PracticeGameSessionFactory.PracticeReferencePowerWatts).Value;
        (PracticeXenonStateV1 Poison, PracticeLiquidZoneRrsV1 Rrs, EquilibriumCoreProjectionV1 Projection) Run(double step)
        {
            var poison = session.CurrentXenonState;
            var rrs = session.CurrentLiquidZoneRrs;
            var projection = session.CurrentEquilibriumProjection;
            for (double time = step; time <= 3600; time += step)
            {
                poison = poison.Advance(projection, 0.8, step);
                var solved = PracticeLiquidZoneRrsV1.TryRunEquilibrium(solver,
                    session.CoreState.EnumerateBundles(), rrs, time, projection.SpatialSolve, poison.Overlay);
                Assert.True(solved.IsValid, solved.IsValid ? null : solved.FirstDiagnostic.ToString());
                rrs = solved.Value.State; projection = solved.Value.Projection;
            }
            return (poison, rrs, projection);
        }
        var production = Run(1800); var reference = Run(300);
        double peak = reference.Poison.Xenon.Max();
        double poisonError = production.Poison.Xenon.Select((x, n) => Math.Abs(x - reference.Poison.Xenon[n]) / peak).Max();
        double fillError = production.Rrs.ZoneFills.Select((f, n) => Math.Abs(f - reference.Rrs.ZoneFills[n])).Max();
        double powerError = production.Rrs.MeasuredZonalPowerFractions.Select((p, n) =>
            Math.Abs(p - reference.Rrs.MeasuredZonalPowerFractions[n])).Max();
        Assert.True(poisonError < 0.01, $"Peak-normalized Xe error {poisonError:G6}");
        Assert.True(fillError < 0.02, $"Maximum zone fill difference {fillError:G6}");
        Assert.True(powerError < 0.005, $"Maximum zonal power fraction difference {powerError:G6}");
    }
}
