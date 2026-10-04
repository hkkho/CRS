using System;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class PracticeChannelPowerReferenceTests
{
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
