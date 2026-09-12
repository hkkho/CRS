using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P7T02IxeTests
{
    private static readonly double NegativeZero = BitConverter.Int64BitsToDouble(long.MinValue);

    [Fact]
    public void AppliesApprovedIxeEulerUsingFrozenNodeVolumeFluxAndFissionRate()
    {
        NuclideDataV1 data = CreateData();
        NuclideStateEnvelopeV1 state = CreateState(data, 10.0, 20.0, 2.0);
        NuclideIntegrationInputV1 input = CreateInput(
            data,
            Id("71000000-0000-4000-8000-000000000001"),
            0.0,
            0.5,
            4.0,
            3.0,
            5.0,
            Digest(0x72));

        NuclideIntegrationResultV1 result = AssertValid(
            NuclideIntegrationTransitionV1.TryApply(state, input)).Value;

        Assert.Equal(11.5, result.ResultingState.I135AtomInventory, 12);
        Assert.Equal(19.7, result.ResultingState.Xe135AtomInventory, 12);
        Assert.Equal(5.75, result.ResultingState.I135NumberDensity, 12);
        Assert.Equal(9.85, result.ResultingState.Xe135NumberDensity, 12);
        Assert.Equal(1UL, result.ResultingState.NuclideStateVersion);
        Assert.Equal(4.0, result.Transition.I135DirectProductionAtomsPerSecond, 12);
        Assert.Equal(-1.0, result.Transition.I135DecayLossAtomsPerSecond, 12);
        Assert.Equal(2.0, result.Transition.Xe135DirectProductionAtomsPerSecond, 12);
        Assert.Equal(1.0, result.Transition.Xe135FromI135DecayAtomsPerSecond, 12);
        Assert.Equal(-1.0, result.Transition.Xe135DecayLossAtomsPerSecond, 12);
        Assert.Equal(-2.6, result.Transition.Xe135AbsorptionLossAtomsPerSecond, 12);
        Assert.Equal(2.0, result.Transition.NodeVolumeM3, 12);
        Assert.Equal(data.DataId, result.Transition.NuclideDataId);
        Assert.Equal(data.DataDigest, result.Transition.NuclideDataDigest);
        Assert.Single(result.ResultingState.I135XeHistory);
    }

    [Fact]
    public void PreservesAtomsAndRecomputesDensitiesWhenTheBundleMovesToAnotherVolume()
    {
        NuclideDataV1 data = CreateData();
        NuclideStateEnvelopeV1 state = CreateState(data, 10.0, 20.0, 2.0);

        NuclideStateEnvelopeV1 moved = AssertValid(state.TryMoveToVolume(4.0)).Value;

        Assert.Equal(state.I135AtomInventory, moved.I135AtomInventory, 12);
        Assert.Equal(state.Xe135AtomInventory, moved.Xe135AtomInventory, 12);
        Assert.Equal(2.5, moved.I135NumberDensity, 12);
        Assert.Equal(5.0, moved.Xe135NumberDensity, 12);
        Assert.Equal(state.NuclideStateVersion, moved.NuclideStateVersion);
        Assert.Equal(state.NuclideHistoryDigest, moved.NuclideHistoryDigest);
        Assert.Equal(state.NuclideDataId, moved.NuclideDataId);
    }

    [Fact]
    public void CanonicalizesZeroLossTermsWithoutChangingTheExplicitZeroState()
    {
        NuclideDataV1 data = CreateData();
        NuclideStateEnvelopeV1 state = CreateState(data, 0.0, 0.0, 2.0);
        NuclideIntegrationInputV1 input = CreateInput(
            data,
            Id("71000000-0000-4000-8000-000000000002"),
            0.0,
            1.0,
            0.0,
            0.0,
            0.0,
            Digest(0x73));

        NuclideIntegrationResultV1 result = AssertValid(
            NuclideIntegrationTransitionV1.TryApply(state, input)).Value;

        Assert.Equal(0.0, result.ResultingState.I135AtomInventory);
        Assert.Equal(0.0, result.ResultingState.Xe135AtomInventory);
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(result.Transition.I135DecayLossAtomsPerSecond));
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(result.Transition.Xe135DecayLossAtomsPerSecond));
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(result.Transition.Xe135AbsorptionLossAtomsPerSecond));
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(result.Transition.Xe135FromI135DecayAtomsPerSecond));
    }

    [Fact]
    public void RejectsSignedZeroAndNonfiniteDataStateInputAndRecordValues()
    {
        AssertInvalid(
            NuclideDataV1.TryCreate(
                new MaterialVariantId("synthetic-phase7-ixe"),
                "synthetic-phase7-ixe-invalid-zero",
                Digest(0x74),
                NegativeZero,
                0.25,
                0.1,
                0.05,
                0.01,
                0.02),
            "NuclideData.NonFinite");
        AssertInvalid(
            NuclideDataV1.TryCreate(
                new MaterialVariantId("synthetic-phase7-ixe"),
                "synthetic-phase7-ixe-invalid-infinity",
                Digest(0x75),
                0.5,
                0.25,
                0.1,
                0.05,
                double.PositiveInfinity,
                0.02),
            "NuclideData.NonFinite");

        NuclideDataV1 data = CreateData();
        AssertInvalid(
            NuclideStateEnvelopeV1.TryCreateFresh(
                Id("71000000-0000-4000-8000-000000000003"),
                NegativeZero,
                0.0,
                2.0,
                0,
                data),
            "NuclideState.NonFinite");

        AssertInvalid(
            NuclideIntegrationInputV1.TryCreate(
                Id("71000000-0000-4000-8000-000000000004"),
                0,
                EventRankV1.KineticNuclideStep,
                0.0,
                NegativeZero,
                0,
                0.0,
                0.0,
                0.0,
                data,
                Digest(0x76)),
            "NuclideIntegrationInput.Time.Invalid");
        AssertInvalid(
            NuclideIntegrationInputV1.TryCreate(
                Id("71000000-0000-4000-8000-000000000005"),
                0,
                EventRankV1.KineticNuclideStep,
                0.0,
                1.0,
                0,
                NegativeZero,
                0.0,
                0.0,
                data,
                Digest(0x77)),
            "NuclideIntegrationInput.FluxOrRate.Invalid");

        AssertInvalid(
            NuclideTransitionRecordV1.TryCreate(
                Id("71000000-0000-4000-8000-000000000006"),
                0,
                EventRankV1.KineticNuclideStep,
                0.0,
                1.0,
                Id("71000000-0000-4000-8000-000000000007"),
                0,
                OptionalUInt64.Applicable(0),
                0,
                1,
                2.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                NegativeZero,
                0.0,
                0.0,
                0.0,
                0.0,
                data.DataId,
                data.DataDigest,
                Digest(0x78)),
            "NuclideTransition.NonFinite");
    }

    [Fact]
    public void RejectsProductionAndAbsorptionOverflowWithoutMutatingState()
    {
        NuclideDataV1 productionOverflowData = CreateData(
            gammaI: double.MaxValue,
            digestValue: 0x79);
        NuclideStateEnvelopeV1 productionState = CreateState(
            productionOverflowData,
            1.0,
            1.0,
            2.0);
        NuclideIntegrationInputV1 productionInput = CreateInput(
            productionOverflowData,
            Id("71000000-0000-4000-8000-000000000008"),
            0.0,
            1.0,
            2.0,
            0.0,
            0.0,
            Digest(0x7A));
        AssertInvalid(
            NuclideIntegrationTransitionV1.TryApply(productionState, productionInput),
            "NuclideIntegration.Result.Invalid");
        Assert.Equal(0UL, productionState.NuclideStateVersion);
        Assert.Equal(1.0, productionState.I135AtomInventory, 12);

        NuclideDataV1 absorptionOverflowData = CreateData(
            sigmaXeGroup1M2: double.MaxValue,
            digestValue: 0x7B);
        NuclideStateEnvelopeV1 absorptionState = CreateState(
            absorptionOverflowData,
            1.0,
            1.0,
            2.0);
        NuclideIntegrationInputV1 absorptionInput = CreateInput(
            absorptionOverflowData,
            Id("71000000-0000-4000-8000-000000000009"),
            0.0,
            1.0,
            0.0,
            2.0,
            0.0,
            Digest(0x7C));
        AssertInvalid(
            NuclideIntegrationTransitionV1.TryApply(absorptionState, absorptionInput),
            "NuclideIntegration.Result.Invalid");
        Assert.Equal(0UL, absorptionState.NuclideStateVersion);
        Assert.Equal(1.0, absorptionState.Xe135AtomInventory, 12);
    }

    [Fact]
    public void RejectsStaleDataAndVersionOverflowBeforeIntegration()
    {
        NuclideDataV1 data = CreateData();
        NuclideStateEnvelopeV1 state = CreateState(data, 1.0, 1.0, 2.0);
        NuclideDataV1 staleData = CreateData(digestValue: 0x7D);
        NuclideIntegrationInputV1 staleInput = CreateInput(
            staleData,
            Id("71000000-0000-4000-8000-000000000010"),
            0.0,
            1.0,
            1.0,
            0.0,
            0.0,
            Digest(0x7E));
        AssertInvalid(
            NuclideIntegrationTransitionV1.TryApply(state, staleInput),
            "NuclideIntegration.Data.Stale");

        NuclideStateEnvelopeV1 maxVersion = AssertValid(
            NuclideStateEnvelopeV1.TryCreateFresh(
                Id("71000000-0000-4000-8000-000000000011"),
                1.0,
                1.0,
                2.0,
                ulong.MaxValue,
                data)).Value;
        NuclideIntegrationInputV1 input = CreateInput(
            data,
            Id("71000000-0000-4000-8000-000000000012"),
            0.0,
            1.0,
            1.0,
            0.0,
            0.0,
            Digest(0x7F));
        AssertInvalid(
            NuclideIntegrationTransitionV1.TryApply(maxVersion, input),
            "NuclideIntegration.Version.Overflow");
    }

    [Fact]
    public void KeepsAcceptedIxeHistoryReadOnlyAndDeterministic()
    {
        NuclideDataV1 data = CreateData();
        NuclideStateEnvelopeV1 state = CreateState(data, 10.0, 20.0, 2.0);
        NuclideIntegrationInputV1 input = CreateInput(
            data,
            Id("71000000-0000-4000-8000-000000000013"),
            0.0,
            0.5,
            4.0,
            3.0,
            5.0,
            Digest(0x80));

        NuclideIntegrationResultV1 first = AssertValid(
            NuclideIntegrationTransitionV1.TryApply(state, input)).Value;
        NuclideIntegrationResultV1 second = AssertValid(
            NuclideIntegrationTransitionV1.TryApply(state, input)).Value;

        Assert.Equal(first.ResultingState.NuclideStateDigest, second.ResultingState.NuclideStateDigest);
        Assert.Equal(first.Transition.RecordDigest, second.Transition.RecordDigest);
        Assert.Throws<NotSupportedException>(
            () => ((IList<NuclideTransitionRecordV1>)first.ResultingState.I135XeHistory)[0] = first.Transition);
    }

    private static NuclideDataV1 CreateData(
        double gammaI = 0.5,
        double gammaXe = 0.25,
        double lambdaI = 0.1,
        double lambdaXe = 0.05,
        double sigmaXeGroup1M2 = 0.01,
        double sigmaXeGroup2M2 = 0.02,
        byte digestValue = 0x71)
    {
        return AssertValid(NuclideDataV1.TryCreate(
            new MaterialVariantId("synthetic-phase7-ixe"),
            "synthetic-phase7-ixe-" + digestValue.ToString("X2", CultureInfo.InvariantCulture),
            Digest(digestValue),
            gammaI,
            gammaXe,
            lambdaI,
            lambdaXe,
            sigmaXeGroup1M2,
            sigmaXeGroup2M2)).Value;
    }

    private static NuclideStateEnvelopeV1 CreateState(
        NuclideDataV1 data,
        double iInventory,
        double xeInventory,
        double nodeVolumeM3)
    {
        return AssertValid(NuclideStateEnvelopeV1.TryCreateFresh(
            Id("72000000-0000-4000-8000-000000000001"),
            iInventory,
            xeInventory,
            nodeVolumeM3,
            0,
            data)).Value;
    }

    private static NuclideIntegrationInputV1 CreateInput(
        NuclideDataV1 data,
        StableId ownerEventId,
        double eventTimeSeconds,
        double deltaTimeSeconds,
        double fissionRateDensity,
        double fluxGroup1,
        double fluxGroup2,
        Digest32 stateBindingDigest)
    {
        return AssertValid(NuclideIntegrationInputV1.TryCreate(
            ownerEventId,
            0,
            EventRankV1.KineticNuclideStep,
            eventTimeSeconds,
            deltaTimeSeconds,
            0,
            fissionRateDensity,
            fluxGroup1,
            fluxGroup2,
            data,
            stateBindingDigest)).Value;
    }

    private static StableId Id(string value)
    {
        return StableId.Parse(value);
    }

    private static Digest32 Digest(byte value)
    {
        return new Digest32(new[]
        {
            value, value, value, value, value, value, value, value,
            value, value, value, value, value, value, value, value,
            value, value, value, value, value, value, value, value,
            value, value, value, value, value, value, value, value
        });
    }

    private static ContractValidationResult<T> AssertValid<T>(
        ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result;
    }

    private static void AssertInvalid<T>(
        ContractValidationResult<T> result,
        string code)
    {
        Assert.False(result.IsValid);
        Assert.Equal(code, result.FirstDiagnostic.Code);
    }
}
