using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P7T01KineticsTests
{
    private static readonly double[] NominalPrecursor = { 0.5, 0.25 };
    private static readonly double[] NominalInitialPrecursor = { 0.5, 0.25 };
    private static readonly double[] ExpectedPrecursor = { 0.475, 0.25 };
    private static readonly double[] ExpectedPrecursorDerivative = { -0.05, 0.0 };
    private static readonly double[] ZeroPrecursor = { 0.0 };
    private static readonly double[] MaxPrecursor = { double.MaxValue };

    [Fact]
    public void AppliesApprovedEulerUpdateAndRecordsExactBinding()
    {
        DelayedNeutronDataV1 data = CreateData();
        KineticStateV1 state = CreateState(data, amplitude: 1.0, precursor: NominalPrecursor);

        ContractValidationResult<KineticIntegrationResultV1> result =
            KineticIntegrationTransitionV1.TryApply(state, data, 0.5);

        AssertValid(result);
        KineticStateV1 next = result.Value.ResultingState;
        KineticStepRecordV1 record = result.Value.Step;

        Assert.Equal(0.5, next.SimulationTimeSeconds, 12);
        Assert.Equal(1.125, next.Amplitude, 12);
        Assert.Equal(ExpectedPrecursor, next.Precursor);
        Assert.Equal(state.InitialAmplitude, next.InitialAmplitude);
        Assert.Equal(state.InitialPrecursor, next.InitialPrecursor);
        Assert.Equal(1UL, next.KineticStepIndex);
        Assert.Equal(state.SpatialSolveId, next.SpatialSolveId);
        Assert.Equal(state.SpatialStateVersion, next.SpatialStateVersion);
        Assert.Equal(state.FeedbackOverlayDigest, next.FeedbackOverlayDigest);
        Assert.Equal(0.05, record.PromptDerivative, 12);
        Assert.Equal(0.2, record.DelayedSource, 12);
        Assert.Equal(0.25, record.AmplitudeDerivative, 12);
        Assert.Equal(ExpectedPrecursorDerivative, record.PrecursorDerivative);
        Assert.Equal(1.0, record.AmplitudeBefore, 12);
        Assert.Equal(1.125, record.AmplitudeAfter, 12);
        Assert.Equal(0UL, record.KineticStepIndexBefore);
        Assert.Equal(1UL, record.KineticStepIndexAfter);
        Assert.Equal(data.DataId, record.DataId);
        Assert.Equal(data.DataVersion, record.DataVersion);
        Assert.Equal(data.DataDigest, record.DataDigest);
    }

    [Fact]
    public void DoesNotMutateCallerVectorsAndPreservesStateImmutability()
    {
        DelayedNeutronDataV1 data = CreateData();
        double[] precursor = { 0.5, 0.25 };
        double[] initial = { 0.1, 0.2 };
        ContractValidationResult<KineticStateV1> created = KineticStateV1.TryCreate(
            0.0,
            1.0,
            precursor,
            1.0,
            initial,
            1000.0,
            data,
            OptionalStableId.Applicable(Id("22222222-2222-4222-8222-222222222222")),
            OptionalUInt64.Applicable(7),
            0.4,
            OptionalDigest32.Applicable(Digest(0x22)),
            0);

        AssertValid(created);
        precursor[0] = 99.0;
        initial[0] = 99.0;

        Assert.Equal(0.5, created.Value.Precursor[0], 12);
        Assert.Equal(0.1, created.Value.InitialPrecursor[0], 12);
        Assert.Throws<NotSupportedException>(() => ((IList<double>)created.Value.Precursor)[0] = 3.0);
        Assert.Throws<NotSupportedException>(() => ((IList<double>)created.Value.InitialPrecursor)[0] = 3.0);
    }

    [Fact]
    public void RequiresACompleteSpatialBindingForPositiveDuration()
    {
        DelayedNeutronDataV1 data = CreateData();
        ContractValidationResult<KineticStateV1> unbound = KineticStateV1.TryCreate(
            0.0,
            1.0,
            NominalPrecursor,
            1.0,
            NominalInitialPrecursor,
            1000.0,
            data,
            OptionalStableId.NotApplicable,
            OptionalUInt64.NotApplicable,
            0.0,
            OptionalDigest32.NotApplicable,
            0);

        AssertValid(unbound);
        ContractValidationResult<KineticIntegrationResultV1> rejected =
            KineticIntegrationTransitionV1.TryApply(unbound.Value, data, 0.5);

        AssertInvalid(rejected, "KineticIntegration.SpatialBinding.Missing");

        ContractValidationResult<KineticStateV1> bound = unbound.Value.TryBindSpatialSolve(
            Id("33333333-3333-4333-8333-333333333333"),
            9,
            -0.01,
            Digest(0x33));
        AssertValid(bound);
        Assert.True(bound.Value.HasSpatialBinding);
    }

    [Fact]
    public void RejectsInvalidDelayedNeutronDataBeforeIntegration()
    {
        ContractValidationResult<DelayedNeutronGroupV1> invalidLambda =
            DelayedNeutronGroupV1.TryCreate(0, 0.1, 0.0);
        AssertInvalid(invalidLambda, "DelayedNeutronGroup.DecayConstant.Invalid");

        ContractValidationResult<DelayedNeutronGroupV1> invalidBeta =
            DelayedNeutronGroupV1.TryCreate(0, -0.1, 0.1);
        AssertInvalid(invalidBeta, "DelayedNeutronGroup.BetaFraction.Invalid");

        ContractValidationResult<DelayedNeutronGroupV1> first =
            DelayedNeutronGroupV1.TryCreate(1, 0.2, 0.1);
        ContractValidationResult<DelayedNeutronGroupV1> second =
            DelayedNeutronGroupV1.TryCreate(2, 0.2, 0.1);
        AssertValid(first);
        AssertValid(second);

        ContractValidationResult<DelayedNeutronDataV1> invalidOrder =
            DelayedNeutronDataV1.TryCreate(
                Id("44444444-4444-4444-8444-444444444444"),
                "synthetic-p7-invalid-order",
                Digest(0x44),
                2.0,
                new[] { first.Value, second.Value });
        AssertInvalid(invalidOrder, "DelayedNeutronData.Groups.Order.Invalid");

        ContractValidationResult<DelayedNeutronGroupV1> betaA =
            DelayedNeutronGroupV1.TryCreate(0, 0.6, 0.1);
        ContractValidationResult<DelayedNeutronGroupV1> betaB =
            DelayedNeutronGroupV1.TryCreate(1, 0.4, 0.1);
        AssertValid(betaA);
        AssertValid(betaB);
        ContractValidationResult<DelayedNeutronDataV1> invalidSum =
            DelayedNeutronDataV1.TryCreate(
                Id("55555555-5555-4555-8555-555555555555"),
                "synthetic-p7-invalid-sum",
                Digest(0x55),
                2.0,
                new[] { betaA.Value, betaB.Value });
        AssertInvalid(invalidSum, "DelayedNeutronData.BetaFractionSum.Invalid");
    }

    [Fact]
    public void RejectsNegativeZeroAndNonfiniteKineticValues()
    {
        AssertInvalid(
            DelayedNeutronGroupV1.TryCreate(0, NegativeZero, 0.1),
            "DelayedNeutronGroup.BetaFraction.Invalid");
        AssertInvalid(
            DelayedNeutronGroupV1.TryCreate(0, 0.1, double.PositiveInfinity),
            "DelayedNeutronGroup.DecayConstant.Invalid");

        DelayedNeutronGroupV1 validGroup = CreateGroup(0, 0.0, 1.0);
        AssertInvalid(
            DelayedNeutronDataV1.TryCreate(
                Id("99999999-9999-4999-8999-999999999999"),
                "synthetic-p7-invalid-prompt-time",
                Digest(0x99),
                NegativeZero,
                new[] { validGroup }),
            "DelayedNeutronData.PromptGenerationTime.Invalid");

        DelayedNeutronDataV1 data = CreateSingleGroupData(0.0, 1.0, 0x9A);
        AssertInvalid(
            KineticStateV1.TryCreate(
                0.0,
                double.PositiveInfinity,
                ZeroPrecursor,
                1.0,
                ZeroPrecursor,
                1000.0,
                data,
                OptionalStableId.Applicable(Id("9a9a9a9a-9a9a-49a9-89a9-9a9a9a9a9a9a")),
                OptionalUInt64.Applicable(0),
                0.0,
                OptionalDigest32.Applicable(Digest(0x9B)),
                0),
            "KineticState.Amplitude.Invalid");

        AssertInvalid(
            KineticStateV1.TryCreate(
                NegativeZero,
                0.0,
                ZeroPrecursor,
                0.0,
                ZeroPrecursor,
                1000.0,
                data,
                OptionalStableId.Applicable(Id("9c9c9c9c-9c9c-49c9-89c9-9c9c9c9c9c9c")),
                OptionalUInt64.Applicable(0),
                0.0,
                OptionalDigest32.Applicable(Digest(0x9C)),
                0),
            "KineticState.SimulationTime.Invalid");

        KineticStateV1 zeroAmplitude = CreateBoundState(
            data,
            simulationTimeSeconds: 0.0,
            amplitude: 0.0,
            precursor: ZeroPrecursor,
            initialPrecursor: ZeroPrecursor,
            spatialReactivity: -1.0,
            kineticStepIndex: 0);
        AssertInvalid(
            KineticIntegrationTransitionV1.TryApply(zeroAmplitude, data, 1.0),
            "KineticIntegration.PromptDerivative.Invalid");
        AssertInvalid(
            KineticIntegrationTransitionV1.TryApply(zeroAmplitude, data, NegativeZero),
            "KineticIntegration.Interval.Invalid");
    }

    [Fact]
    public void RejectsSimulationTimeAndStepIndexOverflow()
    {
        DelayedNeutronDataV1 data = CreateSingleGroupData(0.0, 1.0, 0x9D);
        KineticStateV1 atMaximumTime = CreateBoundState(
            data,
            simulationTimeSeconds: double.MaxValue,
            amplitude: 1.0,
            precursor: ZeroPrecursor,
            initialPrecursor: ZeroPrecursor,
            spatialReactivity: 0.0,
            kineticStepIndex: 0);

        AssertInvalid(
            KineticIntegrationTransitionV1.TryApply(atMaximumTime, data, double.MaxValue),
            "KineticIntegration.Time.Invalid");
        AssertInvalid(
            KineticIntegrationTransitionV1.TryApply(atMaximumTime, data, double.Epsilon),
            "KineticIntegration.Time.Invalid");

        KineticStateV1 atMaximumStep = CreateBoundState(
            data,
            simulationTimeSeconds: 0.0,
            amplitude: 1.0,
            precursor: ZeroPrecursor,
            initialPrecursor: ZeroPrecursor,
            spatialReactivity: 0.0,
            kineticStepIndex: ulong.MaxValue);
        AssertInvalid(
            KineticIntegrationTransitionV1.TryApply(atMaximumStep, data, 1.0),
            "KineticIntegration.StepIndex.Overflow");
    }

    [Fact]
    public void RejectsAmplitudeDelayedSourceAndPrecursorOverflow()
    {
        DelayedNeutronDataV1 amplitudeData = CreateSingleGroupData(0.0, 1.0, 0x9E);
        KineticStateV1 amplitudeOverflow = CreateBoundState(
            amplitudeData,
            simulationTimeSeconds: 0.0,
            amplitude: double.MaxValue,
            precursor: ZeroPrecursor,
            initialPrecursor: ZeroPrecursor,
            spatialReactivity: 2.0,
            kineticStepIndex: 0);
        AssertInvalid(
            KineticIntegrationTransitionV1.TryApply(amplitudeOverflow, amplitudeData, 1.0),
            "KineticIntegration.PromptDerivative.Invalid");

        DelayedNeutronDataV1 delayedSourceData = CreateSingleGroupData(0.0, 2.0, 0x9F);
        KineticStateV1 delayedSourceOverflow = CreateBoundState(
            delayedSourceData,
            simulationTimeSeconds: 0.0,
            amplitude: 0.0,
            precursor: MaxPrecursor,
            initialPrecursor: ZeroPrecursor,
            spatialReactivity: 0.0,
            kineticStepIndex: 0);
        AssertInvalid(
            KineticIntegrationTransitionV1.TryApply(delayedSourceOverflow, delayedSourceData, 1.0),
            "KineticIntegration.DelayedSource.Invalid");

        DelayedNeutronDataV1 precursorData = CreateSingleGroupData(0.5, 1.0, 0xA0);
        KineticStateV1 precursorOverflow = CreateBoundState(
            precursorData,
            simulationTimeSeconds: 0.0,
            amplitude: double.MaxValue,
            precursor: ZeroPrecursor,
            initialPrecursor: ZeroPrecursor,
            spatialReactivity: 0.5,
            kineticStepIndex: 0);
        AssertInvalid(
            KineticIntegrationTransitionV1.TryApply(precursorOverflow, precursorData, 4.0),
            "KineticIntegration.PrecursorResult.Invalid");
    }

    [Fact]
    public void PreservesDataAndStepVectorImmutability()
    {
        DelayedNeutronGroupV1[] inputGroups =
        {
            CreateGroup(0, 0.1, 0.2),
            CreateGroup(1, 0.2, 0.4)
        };
        ContractValidationResult<DelayedNeutronDataV1> createdData =
            DelayedNeutronDataV1.TryCreate(
                Id("a1a1a1a1-a1a1-41a1-81a1-a1a1a1a1a1a1"),
                "synthetic-p7-immutable-data",
                Digest(0xA1),
                2.0,
                inputGroups);

        AssertValid(createdData);
        DelayedNeutronDataV1 data = createdData.Value;
        inputGroups[0] = CreateGroup(0, 0.8, 0.8);
        Assert.Equal(0.1, data.Groups[0].BetaFraction, 12);
        Assert.Throws<NotSupportedException>(
            () => ((IList<DelayedNeutronGroupV1>)data.Groups)[0] = inputGroups[0]);

        KineticStateV1 state = CreateState(data, amplitude: 1.0, precursor: NominalPrecursor);
        KineticIntegrationResultV1 result = AssertValid(
            KineticIntegrationTransitionV1.TryApply(state, data, 0.5)).Value;
        Assert.Throws<NotSupportedException>(
            () => ((IList<double>)result.Step.PrecursorBefore)[0] = 0.0);
        Assert.Throws<NotSupportedException>(
            () => ((IList<double>)result.Step.PrecursorAfter)[0] = 0.0);
        Assert.Throws<NotSupportedException>(
            () => ((IList<double>)result.Step.PrecursorDerivative)[0] = 0.0);
    }

    [Fact]
    public void RejectsStaleDataInvalidIntervalAndNegativeResultWithoutClamping()
    {
        DelayedNeutronDataV1 data = CreateData();
        KineticStateV1 state = CreateState(data, amplitude: 1.0, precursor: NominalPrecursor);

        ContractValidationResult<DelayedNeutronDataV1> otherData = DelayedNeutronDataV1.TryCreate(
            Id("66666666-6666-4666-8666-666666666666"),
            data.DataVersion,
            data.DataDigest,
            data.PromptGenerationTimeSeconds,
            data.Groups);
        AssertValid(otherData);

        ContractValidationResult<KineticIntegrationResultV1> stale =
            KineticIntegrationTransitionV1.TryApply(state, otherData.Value, 0.5);
        AssertInvalid(stale, "KineticIntegration.Data.Stale");

        AssertInvalid(
            KineticIntegrationTransitionV1.TryApply(state, data, 0.0),
            "KineticIntegration.Interval.Invalid");
        AssertInvalid(
            KineticIntegrationTransitionV1.TryApply(state, data, double.NaN),
            "KineticIntegration.Interval.Invalid");

        ContractValidationResult<DelayedNeutronDataV1> zeroBeta = DelayedNeutronDataV1.TryCreate(
            Id("77777777-7777-4777-8777-777777777777"),
            "synthetic-p7-negative-result",
            Digest(0x77),
            1.0,
            new[] { CreateGroup(0, 0.0, 1.0) });
        AssertValid(zeroBeta);
        ContractValidationResult<KineticStateV1> negativeState = KineticStateV1.TryCreate(
            10.0,
            1.0,
            ZeroPrecursor,
            1.0,
            ZeroPrecursor,
            1000.0,
            zeroBeta.Value,
            OptionalStableId.Applicable(Id("88888888-8888-4888-8888-888888888888")),
            OptionalUInt64.Applicable(3),
            -1.0,
            OptionalDigest32.Applicable(Digest(0x88)),
            4);
        AssertValid(negativeState);

        ContractValidationResult<KineticIntegrationResultV1> negative =
            KineticIntegrationTransitionV1.TryApply(negativeState.Value, zeroBeta.Value, 2.0);
        AssertInvalid(negative, "KineticIntegration.AmplitudeResult.Invalid");
        Assert.Equal(1.0, negativeState.Value.Amplitude, 12);
        Assert.Equal(10.0, negativeState.Value.SimulationTimeSeconds, 12);
        Assert.Equal(4UL, negativeState.Value.KineticStepIndex);
    }

    [Fact]
    public void RepeatedIdenticalInputsProduceIdenticalResults()
    {
        DelayedNeutronDataV1 data = CreateData();
        KineticStateV1 state = CreateState(data, amplitude: 1.0, precursor: NominalPrecursor);

        KineticIntegrationResultV1 first = AssertValid(
            KineticIntegrationTransitionV1.TryApply(state, data, 0.5)).Value;
        KineticIntegrationResultV1 second = AssertValid(
            KineticIntegrationTransitionV1.TryApply(state, data, 0.5)).Value;

        Assert.Equal(first.ResultingState.SimulationTimeSeconds, second.ResultingState.SimulationTimeSeconds);
        Assert.Equal(first.ResultingState.Amplitude, second.ResultingState.Amplitude);
        Assert.Equal(first.ResultingState.Precursor, second.ResultingState.Precursor);
        Assert.Equal(first.Step.PrecursorDerivative, second.Step.PrecursorDerivative);
        Assert.Equal(first.Step.SpatialSolveId, second.Step.SpatialSolveId);
        Assert.Equal(first.Step.FeedbackOverlayDigest, second.Step.FeedbackOverlayDigest);
    }

    private static DelayedNeutronDataV1 CreateData()
    {
        ContractValidationResult<DelayedNeutronDataV1> result = DelayedNeutronDataV1.TryCreate(
            Id("11111111-1111-4111-8111-111111111111"),
            "synthetic-p7-kinetics-v1",
            Digest(0x11),
            2.0,
            new[]
            {
                CreateGroup(0, 0.1, 0.2),
                CreateGroup(1, 0.2, 0.4)
            });
        return AssertValid(result).Value;
    }

    private static DelayedNeutronDataV1 CreateSingleGroupData(
        double betaFraction,
        double decayConstantPerSecond,
        byte digestValue)
    {
        ContractValidationResult<DelayedNeutronDataV1> result = DelayedNeutronDataV1.TryCreate(
            Id("abababab-abab-4bab-8bab-abababababab"),
            "synthetic-p7-single-group-" + digestValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Digest(digestValue),
            1.0,
            new[] { CreateGroup(0, betaFraction, decayConstantPerSecond) });
        return AssertValid(result).Value;
    }

    private static DelayedNeutronGroupV1 CreateGroup(
        int groupIndex,
        double betaFraction,
        double decayConstantPerSecond)
    {
        ContractValidationResult<DelayedNeutronGroupV1> result =
            DelayedNeutronGroupV1.TryCreate(groupIndex, betaFraction, decayConstantPerSecond);
        return AssertValid(result).Value;
    }

    private static KineticStateV1 CreateState(
        DelayedNeutronDataV1 data,
        double amplitude,
        IEnumerable<double> precursor)
    {
        return CreateBoundState(
            data,
            simulationTimeSeconds: 0.0,
            amplitude: amplitude,
            precursor: precursor,
            initialPrecursor: NominalInitialPrecursor,
            spatialReactivity: 0.4,
            kineticStepIndex: 0);
    }

    private static KineticStateV1 CreateBoundState(
        DelayedNeutronDataV1 data,
        double simulationTimeSeconds,
        double amplitude,
        IEnumerable<double> precursor,
        IEnumerable<double> initialPrecursor,
        double spatialReactivity,
        ulong kineticStepIndex)
    {
        ContractValidationResult<KineticStateV1> result = KineticStateV1.TryCreate(
            simulationTimeSeconds,
            amplitude,
            precursor,
            1.0,
            initialPrecursor,
            1000.0,
            data,
            OptionalStableId.Applicable(Id("22222222-2222-4222-8222-222222222222")),
            OptionalUInt64.Applicable(7),
            spatialReactivity,
            OptionalDigest32.Applicable(Digest(0x22)),
            kineticStepIndex);
        return AssertValid(result).Value;
    }

    private static double NegativeZero
    {
        get { return BitConverter.Int64BitsToDouble(long.MinValue); }
    }

    private static StableId Id(string value)
    {
        return StableId.Parse(value);
    }

    private static Digest32 Digest(byte value)
    {
        return new Digest32(Enumerable.Repeat(value, 32).ToArray());
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
