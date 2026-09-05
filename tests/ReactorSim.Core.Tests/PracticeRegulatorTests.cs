using System;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class PracticeRegulatorTests
{
    [Fact]
    public void PositiveCorePerturbationSettlesTowardZeroNetReactivity()
    {
        SyntheticPracticeRegulatorV1 initial = Require(
            SyntheticPracticeRegulatorV1.TryCreate(0.0));
        SyntheticPracticeRegulatorV1 perturbed = Require(
            initial.TryBindCoreReactivity(0.01, 0.0));

        Assert.Equal(0.01, perturbed.CoreReactivity, 12);
        Assert.Equal(0.01, perturbed.CompensatedNetReactivity, 12);
        Assert.InRange(perturbed.CompensationState, -0.25, 0.25);

        SyntheticPracticeRegulatorV1 afterResponse = Require(
            perturbed.TryAdvance(0.01, 1.0));
        SyntheticPracticeRegulatorV1 settled = Require(
            afterResponse.TryAdvance(0.01, 601.0));

        Assert.True(
            Math.Abs(afterResponse.CompensatedNetReactivity) <
            Math.Abs(perturbed.CompensatedNetReactivity));
        Assert.True(
            Math.Abs(settled.CompensatedNetReactivity) <
            Math.Abs(afterResponse.CompensatedNetReactivity));
        Assert.Equal(-0.01, settled.CompensationState, 12);
        Assert.Equal(-0.01, settled.CompensationCommand, 12);
        Assert.InRange(settled.CompensationState, settled.LowerBound, settled.UpperBound);
        Assert.InRange(settled.CompensationCommand, settled.LowerBound, settled.UpperBound);
        Assert.Equal(4.0, settled.ResponseTimeSeconds, 12);
        Assert.Equal(
            SyntheticPracticeRegulatorIdentityV1.CadenceIdentity,
            settled.CadenceIdentity);
    }

    [Fact]
    public void SaturationAndNonFiniteInputsFailClosed()
    {
        SyntheticPracticeRegulatorV1 initial = Require(
            SyntheticPracticeRegulatorV1.TryCreate(0.0));
        SyntheticPracticeRegulatorV1 saturated = Require(
            initial.TryBindCoreReactivity(1.0, 0.0));

        Assert.True(saturated.CompensationSaturated);
        Assert.Equal(-0.25, saturated.CompensationCommand, 12);
        Assert.Equal(1.0, saturated.CompensatedNetReactivity, 12);
        Assert.Equal(0.0, saturated.CompensationState, 12);
        Assert.True(saturated.CompensationState >= saturated.LowerBound);
        Assert.True(saturated.CompensationState <= saturated.UpperBound);
        ContractValidationResult<SyntheticPracticeRegulatorV1> saturatedAdvance =
            saturated.TryAdvance(1.0, 600.0);
        Assert.True(
            saturatedAdvance.IsValid,
            saturatedAdvance.IsValid ? string.Empty : saturatedAdvance.FirstDiagnostic.ToString());

        ContractValidationResult<SyntheticPracticeRegulatorV1> nonFinite =
            initial.TryBindCoreReactivity(double.NaN, 0.0);
        Assert.False(nonFinite.IsValid);
        Assert.Equal(
            "SyntheticPracticeRegulator.Binding.Invalid",
            nonFinite.FirstDiagnostic.Code);

        ContractValidationResult<SyntheticPracticeRegulatorV1> backwards =
            initial.TryAdvance(0.0, -1.0);
        Assert.False(backwards.IsValid);
        Assert.Equal(
            "SyntheticPracticeRegulator.Advance.TimeInvalid",
            backwards.FirstDiagnostic.Code);

        ContractValidationResult<SyntheticPracticeRegulatorV1> badResponse =
            SyntheticPracticeRegulatorV1.TryCreate(0.0, 0.0, 1.0, -0.25, 0.25);
        Assert.False(badResponse.IsValid);
        Assert.Equal(
            "SyntheticPracticeRegulator.ResponseTime.Invalid",
            badResponse.FirstDiagnostic.Code);
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }
}
