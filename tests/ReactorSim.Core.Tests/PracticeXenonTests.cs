using System;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class PracticeXenonTests
{
    [Theory]
    [InlineData(1e-6)]
    [InlineData(180)]
    [InlineData(1800)]
    [InlineData(864000)]
    public void FrozenSourceEquilibriumIsPreserved(double seconds)
    {
        const double f = 1e17, flux = 2e18;
        double i = PracticeXenonDataV1.GammaI * f / PracticeXenonDataV1.LambdaI;
        double x = (PracticeXenonDataV1.GammaI + PracticeXenonDataV1.GammaXe) * f /
            (PracticeXenonDataV1.LambdaXe + PracticeXenonDataV1.SigmaGroup2M2 * flux);
        var actual = PracticeXenonDataV1.Advance(i, x, f, 0, flux, seconds);
        Assert.InRange(Math.Abs(actual.Iodine / i - 1), 0, 1e-12);
        Assert.InRange(Math.Abs(actual.Xenon / x - 1), 0, 1e-12);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2e18)]
    [InlineData(1e23)]
    public void LongStepMatchesPartitionedAnalyticSteps(double flux)
    {
        const double duration = 86400, f = 1e17;
        var whole = PracticeXenonDataV1.Advance(1e21, 3e20, f, 0, flux, duration);
        (double Iodine, double Xenon) split = (1e21, 3e20);
        for (int n = 0; n < 1000; n++)
            split = PracticeXenonDataV1.Advance(split.Iodine, split.Xenon, f, 0, flux, duration / 1000);
        Assert.InRange(Math.Abs(split.Iodine / whole.Iodine - 1), 0, 1e-10);
        Assert.InRange(Math.Abs(split.Xenon / whole.Xenon - 1), 0, 1e-10);
    }

    [Fact]
    public void EqualRemovalRatesMatchTheBatemanLimit()
    {
        double flux = (PracticeXenonDataV1.LambdaI - PracticeXenonDataV1.LambdaXe) / PracticeXenonDataV1.SigmaGroup2M2;
        const double i = 1e21, x = 3e20, dt = 1800;
        var result = PracticeXenonDataV1.Advance(i, x, 0, 0, flux, dt);
        double expected = Math.Exp(-PracticeXenonDataV1.LambdaI * dt) * (x + PracticeXenonDataV1.LambdaI * i * dt);
        Assert.InRange(Math.Abs(result.Xenon / expected - 1), 0, 1e-12);
    }

    [Fact]
    public void PowerReductionInitiallyBuildsXenonThenApproachesLowerEquilibrium()
    {
        const double f = 1e17, flux = 2e18;
        double i = PracticeXenonDataV1.GammaI * f / PracticeXenonDataV1.LambdaI;
        double x = (PracticeXenonDataV1.GammaI + PracticeXenonDataV1.GammaXe) * f /
            (PracticeXenonDataV1.LambdaXe + PracticeXenonDataV1.SigmaGroup2M2 * flux);
        var early = PracticeXenonDataV1.Advance(i, x, .8 * f, 0, .8 * flux, 1800);
        var late = PracticeXenonDataV1.Advance(i, x, .8 * f, 0, .8 * flux, 86400);
        Assert.True(early.Iodine < i);
        Assert.True(early.Xenon > x);
        Assert.True(late.Xenon < x);
    }

    [Fact]
    public void AnalyticStepMatchesIndependentRk4Reference()
    {
        const double f = 1e17, flux = 2e18, dt = 1800;
        (double I, double X) state = (1e21, 3e20);
        var actual = PracticeXenonDataV1.Advance(state.I, state.X, f, 0, flux, dt);
        (double I, double X) Rate((double I, double X) y) =>
            (PracticeXenonDataV1.GammaI * f - PracticeXenonDataV1.LambdaI * y.I,
             PracticeXenonDataV1.GammaXe * f + PracticeXenonDataV1.LambdaI * y.I -
             (PracticeXenonDataV1.LambdaXe + PracticeXenonDataV1.SigmaGroup2M2 * flux) * y.X);
        for (int n = 0; n < 1800; n++)
        {
            var a = Rate(state); var b = Rate((state.I + a.I / 2, state.X + a.X / 2));
            var c = Rate((state.I + b.I / 2, state.X + b.X / 2));
            var d = Rate((state.I + c.I, state.X + c.X));
            state = (state.I + (a.I + 2 * b.I + 2 * c.I + d.I) / 6,
                state.X + (a.X + 2 * b.X + 2 * c.X + d.X) / 6);
        }
        Assert.InRange(Math.Abs(actual.Iodine / state.I - 1), 0, 1e-11);
        Assert.InRange(Math.Abs(actual.Xenon / state.X - 1), 0, 1e-11);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidInputsAreRejected(double seconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PracticeXenonDataV1.Advance(0, 0, 0, 0, 0, seconds));
}
