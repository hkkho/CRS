using System;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class PracticeChannelPowerReferenceTests
{
    [Fact]
    public void CircularReferenceHasFourMwRimAndHigherMiddleAtUnchangedTotalPower()
    {
        var pack = FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6().Value;
        var reference = PracticeChannelPowerReference.Create(pack, 2_064_000_000);
        Assert.NotNull(pack.RadialBoundary);
        Assert.InRange(reference.ChannelPowerWatts.Min() / 1000, 4000, 4300);
        Assert.InRange(reference.ChannelPowerWatts.Max() / 1000, 6300, 6700);
        Assert.InRange(Math.Abs(reference.ChannelPowerWatts.Sum() - 2_064_000_000), 0, .001);
        var middle = reference.ChannelPowerWatts.Where((p, i) =>
        {
            var position = Candu6CoreTopologyFactoryV1.GetPosition((uint)i);
            return Math.Pow(position.Column - 10.5, 2) + Math.Pow(position.CartesianY - 10.5, 2) < 16;
        });
        Assert.InRange(middle.Average() / 1000, 6200, 6600);
    }

    [Fact]
    public void CycleAverageIntegratesAcrossKnotsRatherThanLookingUpMeanBurnup()
    {
        var table = FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6().Value.CoefficientTables[0];
        double beginning = table.Rows[0].BurnupJPerKgHm, end = table.Rows[4].BurnupJPerKgHm;
        var average = PracticeChannelPowerReference.AverageCoefficients(table, beginning, end);
        double integral = 0;
        for (int i = 1; i <= 4; i++)
            integral += (table.Rows[i].BurnupJPerKgHm - table.Rows[i - 1].BurnupJPerKgHm) *
                (table.Rows[i].Coefficients.FissionGroup2PerM + table.Rows[i - 1].Coefficients.FissionGroup2PerM) / 2;
        Assert.Equal(integral / (end - beginning), average.FissionGroup2PerM, 12);
        Assert.NotEqual(table.TryLookup((beginning + end) / 2).Value.Coefficients.FissionGroup2PerM, average.FissionGroup2PerM);
        Assert.Throws<ArgumentOutOfRangeException>(() => PracticeChannelPowerReference.AverageCoefficients(table, end, beginning));
        Assert.Throws<ArgumentOutOfRangeException>(() => PracticeChannelPowerReference.AverageCoefficients(table, beginning, table.Rows[table.Rows.Count - 1].BurnupJPerKgHm + 1));
    }
}
