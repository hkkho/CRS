#if RESEARCH_EXPERIMENTS
using System;
using ReactorSim.Core;
using ReactorSim.Game;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class GpuCoupledSpatialTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CoupledReferencePassesIndependentEquationsAndRejectsInvalidFlux(bool cold)
    {
        var session = PracticeGameSessionFactory.CreateBrowserPlaytest();
        var solve = session.CurrentSpatialCandidate.SpatialSolve;
        var fixture = GpuCoupledSpatialFixtureV1.Create(solve, cold, session.CurrentLiquidZoneRrs.Mapping);
        var check = fixture.Validate(fixture.ReferenceK, fixture.ReferenceK, fixture.ReferenceFlux,
            fixture.ReferenceFlux, fixture.CpuIterations);
        Assert.True(check.CpuVerifiedConverged);
        Assert.True(check.AgreementPassed);
        Assert.True(check.RegionsChecked);
        Assert.InRange(check.Group1FluxError, 0, 1e-4);
        Assert.InRange(check.Group2FluxError, 0, 1e-4);
        Assert.InRange(check.NodePowerError, 0, 1e-4);
        Assert.InRange(check.RegionalFractionError, 0, .0005);
        Assert.InRange(check.Residual, 0, fixture.OuterPolicy.ResidualTolerance);
        Assert.InRange(check.PowerBalance, 0, fixture.OuterPolicy.PowerBalanceTolerance);
        var wrong = fixture.Validate(fixture.ReferenceK * 1.01, fixture.ReferenceK * 1.01,
            fixture.ReferenceFlux, fixture.ReferenceFlux, fixture.CpuIterations);
        Assert.False(wrong.CpuVerifiedConverged);
        Assert.False(wrong.AgreementPassed);
        Assert.Throws<ArgumentException>(() => fixture.Validate(1, 1, Array.Empty<double>(), fixture.ReferenceFlux, 1));
        var invalid = (double[])fixture.ReferenceFlux.Clone(); invalid[0] = -1;
        Assert.Throws<ArgumentException>(() => fixture.Validate(1, 1, invalid, fixture.ReferenceFlux, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => fixture.Validate(1, 1, fixture.ReferenceFlux, fixture.ReferenceFlux, 0));
    }
}

#endif
