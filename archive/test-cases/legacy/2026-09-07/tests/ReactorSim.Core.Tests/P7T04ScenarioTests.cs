using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P7T04ScenarioTests
{
    private static readonly double[] EquilibriumPrecursor = { 0.25, 0.25 };

    [Fact]
    public void StartupFromExplicitEquilibriumIsDeterministicWithoutInferringInitialValues()
    {
        DelayedNeutronDataV1 data = CreateKineticData();
        KineticStateV1 initial = CreateKineticState(
            data,
            amplitude: 1.0,
            precursor: EquilibriumPrecursor,
            spatialReactivity: 0.0);
        IntegrationStabilityPolicyV1 policy = CreateKineticPolicy();
        IntegrationSubstepScheduleV1 schedule = AssertValid(
            policy.TryPartition(0.3, 0.4, 0.4)).Value;

        KineticStateV1 first = AdvanceKinetics(initial, data, schedule);
        KineticStateV1 second = AdvanceKinetics(initial, data, schedule);

        Assert.Equal(1.0, first.Amplitude, 12);
        Assert.Equal(EquilibriumPrecursor, first.Precursor);
        Assert.Equal(initial.InitialAmplitude, first.InitialAmplitude, 12);
        Assert.Equal(initial.InitialPrecursor, first.InitialPrecursor);
        Assert.Equal(first.Amplitude, second.Amplitude, 12);
        Assert.Equal(first.Precursor, second.Precursor);
        Assert.Equal(0.3, first.SimulationTimeSeconds, 12);
        Assert.Equal(3UL, first.KineticStepIndex);
    }

    [Fact]
    public void PowerChangeAndOrdinaryReductionUseOnlyExplicitReactivityWithoutSafetyState()
    {
        DelayedNeutronDataV1 data = CreateKineticData();
        IntegrationStabilityPolicyV1 policy = CreateKineticPolicy();
        KineticStateV1 initial = CreateKineticState(
            data,
            amplitude: 1.0,
            precursor: EquilibriumPrecursor,
            spatialReactivity: 0.0);

        KineticStateV1 increasedBinding = AssertValid(initial.TryBindSpatialSolve(
            Id("74000000-0000-4000-8000-000000000002"),
            1,
            0.1,
            Digest(0x42))).Value;
        KineticStateV1 increased = AdvanceKinetics(
            increasedBinding,
            data,
            AssertValid(policy.TryPartition(0.2, 0.4, 0.4)).Value);
        Assert.True(increased.Amplitude > initial.Amplitude);

        KineticStateV1 reductionBinding = AssertValid(increased.TryBindSpatialSolve(
            Id("74000000-0000-4000-8000-000000000003"),
            2,
            -0.5,
            Digest(0x43))).Value;
        double amplitudeBeforeReduction = reductionBinding.Amplitude;
        KineticStateV1 reduced = AdvanceKinetics(
            reductionBinding,
            data,
            AssertValid(policy.TryPartition(0.2, 0.4, 0.4)).Value);

        Assert.True(reduced.Amplitude < amplitudeBeforeReduction);
        Assert.True(reduced.Amplitude >= 0.0);
        Assert.Equal(2UL, reductionBinding.KineticStepIndex);
        Assert.Equal(4UL, reduced.KineticStepIndex);
        Assert.Equal(-0.5, reduced.SpatialReactivity, 12);
    }

    [Fact]
    public void XenonPowerHistoryRisesAndRelaxesDeterministicallyWithNonnegativeInventories()
    {
        NuclideDataV1 data = CreateNuclideData();
        IntegrationStabilityPolicyV1 policy = CreateNuclidePolicy();
        IntegrationSubstepScheduleV1 schedule = AssertValid(
            policy.TryPartition(12.0, 0.09, 0.0)).Value;

        XenonTrace first = RunXenonHistory(data, schedule);
        XenonTrace second = RunXenonHistory(data, schedule);

        Assert.Equal(24, schedule.Count);
        Assert.Equal(first.XeTrace, second.XeTrace);
        Assert.Equal(first.FinalState.NuclideStateDigest, second.FinalState.NuclideStateDigest);
        Assert.Equal(24UL, first.FinalState.NuclideStateVersion);
        Assert.Equal(24, first.FinalState.I135XeHistory.Count);
        Assert.All(first.XeTrace, value => Assert.True(value >= 0.0 && !double.IsNaN(value) && !double.IsInfinity(value)));
        Assert.True(first.XeTrace.Skip(1).Max() > first.XeTrace[0]);
        Assert.True(first.XeTrace.Last() < first.XeTrace.Skip(1).Max());
    }

    private static KineticStateV1 AdvanceKinetics(
        KineticStateV1 initial,
        DelayedNeutronDataV1 data,
        IntegrationSubstepScheduleV1 schedule)
    {
        KineticStateV1 current = initial;
        foreach (double deltaTimeSeconds in schedule)
        {
            current = AssertValid(
                KineticIntegrationTransitionV1.TryApply(current, data, deltaTimeSeconds)).Value.ResultingState;
        }

        return current;
    }

    private static XenonTrace RunXenonHistory(
        NuclideDataV1 data,
        IntegrationSubstepScheduleV1 schedule)
    {
        NuclideStateEnvelopeV1 state = AssertValid(
            NuclideStateEnvelopeV1.TryCreateFresh(
                Id("74000000-0000-4000-8000-000000000010"),
                0.0,
                0.0,
                1.0,
                0,
                data)).Value;
        List<double> xeTrace = new() { state.Xe135AtomInventory };
        double eventTimeSeconds = 0.0;
        for (int index = 0; index < schedule.Count; index++)
        {
            double deltaTimeSeconds = schedule[index];
            bool highPower = index < 4;
            double fissionRateDensity = highPower ? 10.0 : 0.0;
            double fluxGroup1 = highPower ? 2.0 : 0.0;
            double fluxGroup2 = highPower ? 1.0 : 0.0;
            NuclideIntegrationInputV1 input = AssertValid(
                NuclideIntegrationInputV1.TryCreate(
                    StepId(index),
                    (ulong)index,
                    EventRankV1.KineticNuclideStep,
                    eventTimeSeconds,
                    deltaTimeSeconds,
                    0,
                    fissionRateDensity,
                    fluxGroup1,
                    fluxGroup2,
                    data,
                    Digest(0x50))).Value;
            state = AssertValid(
                NuclideIntegrationTransitionV1.TryApply(state, input)).Value.ResultingState;
            xeTrace.Add(state.Xe135AtomInventory);
            eventTimeSeconds += deltaTimeSeconds;
        }

        return new XenonTrace(state, xeTrace.ToArray());
    }

    private static DelayedNeutronDataV1 CreateKineticData()
    {
        return AssertValid(DelayedNeutronDataV1.TryCreate(
            Id("74000000-0000-4000-8000-000000000001"),
            "synthetic-p7-scenarios-kinetics-v1",
            Digest(0x40),
            2.0,
            new[]
            {
                AssertValid(DelayedNeutronGroupV1.TryCreate(0, 0.1, 0.2)).Value,
                AssertValid(DelayedNeutronGroupV1.TryCreate(1, 0.2, 0.4)).Value
            })).Value;
    }

    private static KineticStateV1 CreateKineticState(
        DelayedNeutronDataV1 data,
        double amplitude,
        IEnumerable<double> precursor,
        double spatialReactivity)
    {
        return AssertValid(KineticStateV1.TryCreate(
            0.0,
            amplitude,
            precursor,
            amplitude,
            precursor,
            1000.0,
            data,
            OptionalStableId.Applicable(Id("74000000-0000-4000-8000-000000000004")),
            OptionalUInt64.Applicable(0),
            spatialReactivity,
            OptionalDigest32.Applicable(Digest(0x41)),
            0)).Value;
    }

    private static NuclideDataV1 CreateNuclideData()
    {
        return AssertValid(NuclideDataV1.TryCreate(
            new MaterialVariantId("synthetic-phase7-scenarios"),
            "synthetic-p7-scenarios-ixe-v1",
            Digest(0x51),
            0.5,
            0.25,
            0.1,
            0.05,
            0.01,
            0.02)).Value;
    }

    private static IntegrationStabilityPolicyV1 CreateKineticPolicy()
    {
        return AssertValid(IntegrationStabilityPolicyV1.TryCreate(
            "synthetic-p7-scenarios-kinetics-policy-v1",
            Digest(0x44),
            0.1,
            0.1,
            0.1,
            0.4,
            0.5,
            0.1)).Value;
    }

    private static IntegrationStabilityPolicyV1 CreateNuclidePolicy()
    {
        return AssertValid(IntegrationStabilityPolicyV1.TryCreate(
            "synthetic-p7-scenarios-ixe-policy-v1",
            Digest(0x52),
            0.5,
            0.5,
            0.5,
            0.1,
            0.2,
            0.1)).Value;
    }

    private static StableId StepId(int index)
    {
        return Id("74000000-0000-4000-8000-" + index.ToString("x12", CultureInfo.InvariantCulture));
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

    private sealed class XenonTrace
    {
        public XenonTrace(NuclideStateEnvelopeV1 finalState, double[] xeTrace)
        {
            FinalState = finalState;
            XeTrace = xeTrace;
        }

        public NuclideStateEnvelopeV1 FinalState { get; }

        public double[] XeTrace { get; }
    }
}
