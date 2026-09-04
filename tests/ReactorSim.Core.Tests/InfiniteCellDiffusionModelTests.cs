using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class InfiniteCellDiffusionModelTests
{
    [Fact]
    public void InfiniteCellUsesTwoGroupLossMatrixAndSeparatePowerCrossSections()
    {
        BurnupCoefficientValuesV1 coefficients = Require(
            BurnupCoefficientValuesV1.TryCreate(
                0.30,
                0.16,
                0.16,
                0.10,
                0.24,
                0.32,
                0.07,
                1.0,
                3.204353268e-11));
        InfiniteCellDiffusionModelV1 model = Require(
            InfiniteCellDiffusionModelV1.TryCreate(
                coefficients,
                33.0,
                20.6,
                7200.0));

        InfiniteCellDiffusionResultV1 result = Require(model.TrySolve());

        Assert.Equal("infinite-cell-two-group-diffusion-v1", result.ModelId);
        Assert.Equal("infinite-no-leakage-v1", result.BoundaryConditionId);
        Assert.Equal(1.027027027027027, result.InfiniteMultiplicationFactor, 12);
        Assert.Equal(0.4375, result.Group2FluxShape / result.Group1FluxShape, 12);
        Assert.Equal(0.16 + 0.10 * 0.4375, result.FissionRatePerUnitFluxPerM, 12);
        Assert.Equal(0.24 + 0.32 * 0.4375, result.NeutronProductionPerUnitFluxPerM, 12);
        Assert.Equal(20.6, result.HeavyMetalMassKg, 12);
        Assert.Equal(33.0, result.SpecificPowerWattsPerGramHm, 12);
        Assert.Equal(33_000.0, result.SpecificPowerWattsPerKgHm, 12);
        Assert.Equal(679_800.0, result.CellPowerWatts, 6);
    }

    [Theory]
    [InlineData(7000.0, 212.12121212121212)]
    [InlineData(7500.0, 227.27272727272728)]
    public void ThirtyThreeWattPerGramTwentyPointSixKgFuelReachesCanduBurnupWindow(
        double targetBurnupMwDayPerT,
        double expectedResidenceDays)
    {
        BurnupCoefficientValuesV1 coefficients = Require(
            BurnupCoefficientValuesV1.TryCreate(
                0.30,
                0.16,
                0.035,
                0.14,
                0.084,
                0.3402,
                0.20,
                1.0,
                3.204353268e-11));
        InfiniteCellDiffusionModelV1 model = Require(
            InfiniteCellDiffusionModelV1.TryCreate(
                coefficients,
                33.0,
                20.6,
                targetBurnupMwDayPerT));

        InfiniteCellDiffusionResultV1 result = Require(model.TrySolve());

        Assert.Equal(targetBurnupMwDayPerT, result.DischargeBurnupMwDayPerT, 12);
        Assert.Equal(targetBurnupMwDayPerT / 1000.0, result.DischargeBurnupMwDayPerKgHm, 12);
        Assert.Equal(expectedResidenceDays, result.ResidenceTimeDays, 10);
        Assert.InRange(result.ResidenceTimeDays, 7000.0 / 33.0, 7500.0 / 33.0);
        Assert.Equal(679_800.0, result.CellPowerWatts, 6);
    }

    [Fact]
    public void EmbeddedCalibratedNaturalUraniumRowFeedsTheReferenceCellModel()
    {
        FullCoreDiffusionDataPackV1 pack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        BurnupCoefficientRowV1 row = pack.CoefficientTables[0].Rows[4];
        Assert.Equal(7.5, row.BurnupJPerKgHm / InfiniteCellDiffusionModelV1.JoulesPerMegaWattDayPerKilogramHm, 12);

        InfiniteCellDiffusionModelV1 model = Require(
            InfiniteCellDiffusionModelV1.TryCreate(
                row.Coefficients,
                33.0,
                20.6,
                7200.0));
        InfiniteCellDiffusionResultV1 result = Require(model.TrySolve());

        Assert.Equal(679_800.0, result.CellPowerWatts, 6);
        Assert.InRange(result.ResidenceTimeDays, 7000.0 / 33.0, 7500.0 / 33.0);
        Assert.InRange(result.InfiniteMultiplicationFactor, 1.0, 1.1);
    }

    [Fact]
    public void RelativeCriticalityResponseChangesPowerWithoutChangingTheStaticSolveTarget()
    {
        ContractValidationResult<double> response =
            FullCorePowerResponseV1.TryComputeNormalizedPowerFraction(
                1.0,
                0.99,
                1.0);

        Assert.True(response.IsValid, response.IsValid ? string.Empty : response.FirstDiagnostic.ToString());
        Assert.Equal(0.99, response.Value, 12);
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }
}
