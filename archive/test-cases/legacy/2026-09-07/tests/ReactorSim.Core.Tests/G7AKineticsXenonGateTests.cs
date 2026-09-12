using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class G7AKineticsXenonGateTests
{
    private static readonly double[] EquilibriumPrecursor = { 0.25, 0.25 };

    [Fact]
    public void LongKineticsAndXenonHistoriesRemainDeterministicFiniteAndNonnegative()
    {
        DelayedNeutronDataV1 kineticData = CreateKineticData();
        IntegrationStabilityPolicyV1 kineticPolicy = CreateKineticPolicy(0.1);
        IntegrationSubstepScheduleV1 kineticSchedule = AssertValid(
            kineticPolicy.TryPartition(204.8, 0.4, 0.5)).Value;
        KineticStateV1 kineticInitial = CreateKineticState(
            kineticData,
            amplitude: 1.0,
            precursor: EquilibriumPrecursor,
            spatialReactivity: 0.005);

        KineticStateV1 firstKinetics = AdvanceKinetics(
            kineticInitial,
            kineticData,
            kineticSchedule);
        KineticStateV1 secondKinetics = AdvanceKinetics(
            kineticInitial,
            kineticData,
            kineticSchedule);

        Assert.Equal(2048UL, firstKinetics.KineticStepIndex);
        Assert.Equal(204.8, firstKinetics.SimulationTimeSeconds, 10);
        AssertFiniteNonnegative(firstKinetics.Amplitude, "kinetic amplitude");
        Assert.All(firstKinetics.Precursor, value => AssertFiniteNonnegative(value, "precursor"));
        Assert.Equal(firstKinetics.Amplitude, secondKinetics.Amplitude, 12);
        Assert.Equal(firstKinetics.Precursor, secondKinetics.Precursor);

        NuclideDataV1 nuclideData = CreateNuclideData();
        IntegrationStabilityPolicyV1 nuclidePolicy = CreateNuclidePolicy(0.1);
        IntegrationSubstepScheduleV1 nuclideSchedule = AssertValid(
            nuclidePolicy.TryPartition(102.4, 0.1, 0.0)).Value;
        NuclideStateEnvelopeV1 firstXenon = RunXenonHistory(
            nuclideData,
            nuclideSchedule,
            highPowerSteps: 256);
        NuclideStateEnvelopeV1 secondXenon = RunXenonHistory(
            nuclideData,
            nuclideSchedule,
            highPowerSteps: 256);

        Assert.Equal((ulong)nuclideSchedule.Count, firstXenon.NuclideStateVersion);
        Assert.Equal(nuclideSchedule.Count, firstXenon.I135XeHistory.Count);
        AssertFiniteNonnegative(firstXenon.I135AtomInventory, "final I-135 inventory");
        AssertFiniteNonnegative(firstXenon.Xe135AtomInventory, "final Xe-135 inventory");
        AssertFiniteNonnegative(firstXenon.I135NumberDensity, "final I-135 density");
        AssertFiniteNonnegative(firstXenon.Xe135NumberDensity, "final Xe-135 density");
        Assert.All(firstXenon.I135XeHistory, record =>
        {
            AssertFiniteNonnegative(record.I135AtomInventoryAfter, "I-135 history inventory");
            AssertFiniteNonnegative(record.Xe135AtomInventoryAfter, "Xe-135 history inventory");
            AssertFiniteNonnegative(record.I135NumberDensityAfter, "I-135 history density");
            AssertFiniteNonnegative(record.Xe135NumberDensityAfter, "Xe-135 history density");
        });

        double peakXenon = firstXenon.I135XeHistory.Max(
            record => record.Xe135AtomInventoryAfter);
        Assert.True(peakXenon > 0.0);
        Assert.True(firstXenon.Xe135AtomInventory < peakXenon);
        Assert.Equal(firstXenon.NuclideHistoryDigest, secondXenon.NuclideHistoryDigest);
        Assert.Equal(firstXenon.NuclideStateDigest, secondXenon.NuclideStateDigest);
    }

    [Fact]
    public void KineticsAndXenonFinalStatesConvergeWhenTheExplicitStepIsRefined()
    {
        DelayedNeutronDataV1 kineticData = CreateKineticData();
        double coarseKinetics = RunKineticsAtStep(kineticData, 0.2, 20.0);
        double mediumKinetics = RunKineticsAtStep(kineticData, 0.1, 20.0);
        double fineKinetics = RunKineticsAtStep(kineticData, 0.05, 20.0);
        double coarseKineticError = Math.Abs(coarseKinetics - fineKinetics);
        double mediumKineticError = Math.Abs(mediumKinetics - fineKinetics);

        AssertFiniteNonnegative(coarseKinetics, "coarse kinetic amplitude");
        AssertFiniteNonnegative(mediumKinetics, "medium kinetic amplitude");
        AssertFiniteNonnegative(fineKinetics, "fine kinetic amplitude");
        Assert.True(
            mediumKineticError < coarseKineticError,
            $"Kinetic refinement did not reduce final-state error: coarse={coarseKineticError:R}, medium={mediumKineticError:R}.");

        NuclideDataV1 nuclideData = CreateNuclideData();
        double coarseXenon = RunXenonAtStep(nuclideData, 0.4, 8.0);
        double mediumXenon = RunXenonAtStep(nuclideData, 0.2, 8.0);
        double fineXenon = RunXenonAtStep(nuclideData, 0.1, 8.0);
        double coarseXenonError = Math.Abs(coarseXenon - fineXenon);
        double mediumXenonError = Math.Abs(mediumXenon - fineXenon);

        AssertFiniteNonnegative(coarseXenon, "coarse Xe-135 inventory");
        AssertFiniteNonnegative(mediumXenon, "medium Xe-135 inventory");
        AssertFiniteNonnegative(fineXenon, "fine Xe-135 inventory");
        Assert.True(
            mediumXenonError < coarseXenonError,
            $"Xenon refinement did not reduce final-state error: coarse={coarseXenonError:R}, medium={mediumXenonError:R}.");
    }

    private static double RunKineticsAtStep(
        DelayedNeutronDataV1 data,
        double stepSeconds,
        double durationSeconds)
    {
        IntegrationStabilityPolicyV1 policy = CreateKineticPolicy(stepSeconds);
        IntegrationSubstepScheduleV1 schedule = AssertValid(
            policy.TryPartition(durationSeconds, 0.4, 0.5)).Value;
        KineticStateV1 initial = CreateKineticState(
            data,
            amplitude: 1.0,
            precursor: EquilibriumPrecursor,
            spatialReactivity: 0.005);
        return AdvanceKinetics(initial, data, schedule).Amplitude;
    }

    private static double RunXenonAtStep(
        NuclideDataV1 data,
        double stepSeconds,
        double durationSeconds)
    {
        IntegrationStabilityPolicyV1 policy = CreateNuclidePolicy(stepSeconds);
        IntegrationSubstepScheduleV1 schedule = AssertValid(
            policy.TryPartition(durationSeconds, 0.1, 0.0)).Value;
        NuclideStateEnvelopeV1 final = RunXenonHistory(
            data,
            schedule,
            highPowerSteps: schedule.Count);
        return final.Xe135AtomInventory;
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
                KineticIntegrationTransitionV1.TryApply(
                    current,
                    data,
                    deltaTimeSeconds)).Value.ResultingState;
        }

        return current;
    }

    private static NuclideStateEnvelopeV1 RunXenonHistory(
        NuclideDataV1 data,
        IntegrationSubstepScheduleV1 schedule,
        int highPowerSteps)
    {
        NuclideStateEnvelopeV1 state = AssertValid(
            NuclideStateEnvelopeV1.TryCreateFresh(
                Id("74000000-0000-4000-8000-000000000110"),
                0.0,
                0.0,
                1.0,
                0,
                data)).Value;
        double eventTimeSeconds = 0.0;
        for (int index = 0; index < schedule.Count; index++)
        {
            double deltaTimeSeconds = schedule[index];
            bool highPower = index < highPowerSteps;
            NuclideIntegrationInputV1 input = AssertValid(
                NuclideIntegrationInputV1.TryCreate(
                    StepId(index),
                    (ulong)index,
                    EventRankV1.KineticNuclideStep,
                    eventTimeSeconds,
                    deltaTimeSeconds,
                    0,
                    highPower ? 10.0 : 0.0,
                    highPower ? 2.0 : 0.0,
                    highPower ? 1.0 : 0.0,
                    data,
                    Digest(0x60))).Value;
            state = AssertValid(
                NuclideIntegrationTransitionV1.TryApply(state, input)).Value.ResultingState;
            eventTimeSeconds += deltaTimeSeconds;
        }

        return state;
    }

    private static DelayedNeutronDataV1 CreateKineticData()
    {
        return AssertValid(DelayedNeutronDataV1.TryCreate(
            Id("74000000-0000-4000-8000-000000000101"),
            "synthetic-p7a-gate-kinetics-v1",
            Digest(0x61),
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
            OptionalStableId.Applicable(Id("74000000-0000-4000-8000-000000000102")),
            OptionalUInt64.Applicable(0),
            spatialReactivity,
            OptionalDigest32.Applicable(Digest(0x62)),
            0)).Value;
    }

    private static NuclideDataV1 CreateNuclideData()
    {
        return AssertValid(NuclideDataV1.TryCreate(
            new MaterialVariantId("synthetic-phase7a-gate"),
            "synthetic-p7a-gate-ixe-v1",
            Digest(0x63),
            0.5,
            0.25,
            0.1,
            0.05,
            0.01,
            0.02)).Value;
    }

    private static IntegrationStabilityPolicyV1 CreateKineticPolicy(double stepSeconds)
    {
        return AssertValid(IntegrationStabilityPolicyV1.TryCreate(
            "synthetic-p7a-gate-kinetics-policy-v1-" + stepSeconds.ToString("R", CultureInfo.InvariantCulture),
            Digest(0x64),
            stepSeconds,
            stepSeconds,
            stepSeconds,
            0.4,
            0.5,
            0.1)).Value;
    }

    private static IntegrationStabilityPolicyV1 CreateNuclidePolicy(double stepSeconds)
    {
        return AssertValid(IntegrationStabilityPolicyV1.TryCreate(
            "synthetic-p7a-gate-ixe-policy-v1-" + stepSeconds.ToString("R", CultureInfo.InvariantCulture),
            Digest(0x65),
            stepSeconds,
            stepSeconds,
            stepSeconds,
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

    private static void AssertFiniteNonnegative(double value, string name)
    {
        Assert.True(
            value >= 0.0 && !double.IsNaN(value) && !double.IsInfinity(value),
            $"{name} must be finite and nonnegative, actual={value:R}.");
    }

    private static ContractValidationResult<T> AssertValid<T>(
        ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result;
    }
}
