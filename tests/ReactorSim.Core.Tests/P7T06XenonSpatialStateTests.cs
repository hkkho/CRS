using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P7T06XenonSpatialStateTests
{
    private static readonly Lazy<CoreFixture> Shared = new Lazy<CoreFixture>(CreateFixture);

    [Fact]
    public void Creates4560NodeStateAndZeroOverlayPreservesBaseSolve()
    {
        CoreFixture fixture = Shared.Value;
        XenonSpatialStateV1 state = Require(
            XenonSpatialStateV1.TryCreate(
                fixture.Model,
                fixture.CoreState.EnumerateBundles(),
                fixture.NuclideData));

        Assert.Equal(4560, state.NodeInputs.Count);
        Assert.Equal(4560, state.NodeStates.Count);
        Assert.Equal(0.0, state.MeanXe135NumberDensityM3);
        Assert.Equal(0.0, state.MaxXe135NumberDensityM3);
        Assert.Equal(0UL, state.CoreStateVersion);
        Assert.Equal(0.0, state.SimulationTimeSeconds);

        XenonSpatialCouplingResultV1 coupling = Require(
            fixture.Model.TryCreateXenonCoupling(
                fixture.CoreState.EnumerateBundles(),
                state,
                1.0));
        Assert.Equal(XenonBasisV1.Excluded, coupling.XenonBasis);
        Assert.Equal(4560, coupling.Overlays.Count);
        Assert.All(
            coupling.Overlays,
            overlay => Assert.Equal(0.0, overlay.DynamicAbsorptionGroup2PerM));

        FullCoreDiffusionSolveResultV1 coupledSolve = Require(
            fixture.Model.TrySolve(
                fixture.CoreState.EnumerateBundles(),
                coupling,
                1_000_000_000.0,
                fixture.BaseSolve.EffectiveK,
                fixture.BaseSolve.Group1Flux,
                fixture.BaseSolve.Group2Flux));
        Assert.True(coupledSolve.HasXenonOverlay);
        Assert.Equal(fixture.BaseSolve.TotalPowerWatts, coupledSolve.TotalPowerWatts, 3);
        Assert.InRange(
            Math.Abs(fixture.BaseSolve.EffectiveK - coupledSolve.EffectiveK),
            0.0,
            5.0e-4);
        Assert.Equal(fixture.BaseSolve.Group1Flux.Count, coupledSolve.Group1Flux.Count);
        Assert.Equal(fixture.BaseSolve.Group2Flux.Count, coupledSolve.Group2Flux.Count);
    }

    [Fact]
    public void LocalizedXeChangesOnlyItsNodeOverlayAndBindsBundleIdentity()
    {
        CoreFixture fixture = Shared.Value;
        XenonSpatialStateV1 state = CreateStateWithXe(fixture, 0, 1.0e22);
        XenonSpatialCouplingResultV1 coupling = Require(
            fixture.Model.TryCreateXenonCoupling(
                fixture.CoreState.EnumerateBundles(),
                state,
                1.0));

        XenonSpatialOverlayValueV1 local = coupling.Overlays[0];
        XenonSpatialOverlayValueV1 neighbor = coupling.Overlays[1];
        Assert.Equal(fixture.CoreState.GetBundle(0, 0).BundleId, local.BundleId);
        Assert.True(local.Xe135NumberDensity > neighbor.Xe135NumberDensity);
        Assert.True(local.DynamicAbsorptionGroup2PerM > neighbor.DynamicAbsorptionGroup2PerM);
        Assert.Equal(
            fixture.BaseSolve.Coefficients.Nodes[1].AbsorptionGroup2PerM,
            coupling.Coefficients.Nodes[1].AbsorptionGroup2PerM,
            14);
        Assert.True(
            coupling.Coefficients.Nodes[0].AbsorptionGroup2PerM >
            fixture.BaseSolve.Coefficients.Nodes[0].AbsorptionGroup2PerM);
    }

    [Fact]
    public void FourToEightHourBurnoutAndEightToTwentyFourHourBuildupAreFiniteAndDeterministic()
    {
        CoreFixture fixture = Shared.Value;
        XenonSpatialStateV1 burnout = CreateStateWithXe(fixture, 0, 1.0e22);
        double burnoutAtFourHours = 0.0;
        double burnoutAtEightHours = 0.0;
        for (int hour = 1; hour <= 8; hour++)
        {
            burnout = AdvanceOneHour(fixture, burnout, hour);
            if (hour == 4)
            {
                burnoutAtFourHours = burnout.NodeStates[0].Xe135NumberDensity;
            }

            if (hour == 8)
            {
                burnoutAtEightHours = burnout.NodeStates[0].Xe135NumberDensity;
            }
        }

        XenonSpatialStateV1 buildup = Require(
            XenonSpatialStateV1.TryCreate(
                fixture.Model,
                fixture.CoreState.EnumerateBundles(),
                fixture.NuclideData));
        double buildupAtEightHours = 0.0;
        double buildupAtTwentyFourHours = 0.0;
        for (int hour = 1; hour <= 24; hour++)
        {
            buildup = AdvanceOneHour(fixture, buildup, 1000 + hour);
            if (hour == 8)
            {
                buildupAtEightHours = buildup.MeanXe135NumberDensityM3;
            }

            if (hour == 24)
            {
                buildupAtTwentyFourHours = buildup.MeanXe135NumberDensityM3;
            }
        }

        Assert.True(
            burnoutAtEightHours < burnoutAtFourHours,
            "A high local Xe inventory should burn down under the accepted local flux.");
        Assert.True(
            buildupAtTwentyFourHours > buildupAtEightHours,
            "A fresh I/Xe state should build Xe over the longer practice interval.");
        Assert.True(double.IsFinite(burnoutAtEightHours));
        Assert.True(double.IsFinite(buildupAtTwentyFourHours));

        XenonSpatialStateV1 replay = CreateStateWithXe(fixture, 0, 1.0e22);
        for (int hour = 1; hour <= 8; hour++)
        {
            replay = AdvanceOneHour(fixture, replay, hour);
        }

        Assert.Equal(burnout.StateDigest, replay.StateDigest);
        Assert.Equal(burnout.NodeStates[0].NuclideStateVersion, replay.NodeStates[0].NuclideStateVersion);
    }

    [Fact]
    public void StaleBindingAndFailedNodeAdvanceDoNotMutateState()
    {
        CoreFixture fixture = Shared.Value;
        XenonSpatialStateV1 state = Require(
            XenonSpatialStateV1.TryCreate(
                fixture.Model,
                fixture.CoreState.EnumerateBundles(),
                fixture.NuclideData));
        XenonSpatialStateBindingV1 binding = Require(state.TryCreateBinding(1.0));
        XenonSpatialStateV1 advanced = Require(
            state.TryAdvance(
                binding,
                3_600.0,
                1.0,
                fixture.BaseSolve.Coefficients,
                fixture.BaseSolve.Group1Flux,
                fixture.BaseSolve.Group2Flux,
                1.0,
                StableId.Parse("00000000-0000-0000-0000-000000000701"))
            ).ResultingState;

        ContractValidationResult<XenonSpatialAdvanceResultV1> stale = advanced.TryAdvance(
            binding,
            7_200.0,
            1.0,
            fixture.BaseSolve.Coefficients,
            fixture.BaseSolve.Group1Flux,
            fixture.BaseSolve.Group2Flux,
            1.0,
            StableId.Parse("00000000-0000-0000-0000-000000000702"));
        Assert.False(stale.IsValid);
        Assert.Equal("XenonSpatialBinding.Time.Stale", stale.FirstDiagnostic.Code);

        double[] overflowingFlux = Enumerable.Repeat(double.MaxValue, fixture.Model.NodeCount).ToArray();
        ContractValidationResult<XenonSpatialAdvanceResultV1> failed = state.TryAdvance(
            binding,
            3_600.0,
            1.0,
            fixture.BaseSolve.Coefficients,
            overflowingFlux,
            overflowingFlux,
            1.0,
            StableId.Parse("00000000-0000-0000-0000-000000000703"));
        Assert.False(failed.IsValid);
        Assert.Equal("NuclideIntegration.Result.Invalid", failed.FirstDiagnostic.Code);
        Assert.Equal(0.0, state.SimulationTimeSeconds);
        Assert.Equal(0UL, state.CoreStateVersion);
        Assert.Equal(binding.StateDigest, state.StateDigest);
        Assert.Equal(0UL, state.NodeStates[0].NuclideStateVersion);
    }

    [Fact]
    public void ModelRejectsStalePackBindingAndReusedOverlayInventory()
    {
        CoreFixture fixture = Shared.Value;
        XenonSpatialStateV1 state = Require(
            XenonSpatialStateV1.TryCreate(
                fixture.Model,
                fixture.CoreState.EnumerateBundles(),
                fixture.NuclideData));
        XenonSpatialStateBindingV1 validBinding = Require(state.TryCreateBinding(1.0));
        XenonSpatialStateBindingV1 staleBinding = Require(
            XenonSpatialStateBindingV1.TryCreate(
                validBinding.SimulationTimeSeconds,
                validBinding.KineticAmplitude,
                validBinding.CoreStateVersion,
                validBinding.StateDigest,
                Digest(0x72),
                validBinding.DataPackDigest,
                validBinding.NodeVersions));

        ContractValidationResult<XenonSpatialCouplingResultV1> stale =
            fixture.Model.TryCreateXenonCoupling(
                fixture.CoreState.EnumerateBundles(),
                staleBinding,
                state.NodeInputs);
        Assert.False(stale.IsValid);
        Assert.Equal("FullCoreDiffusionCoupling.BindingDigest.Mismatch", stale.FirstDiagnostic.Code);

        XenonSpatialCouplingResultV1 coupling = Require(
            fixture.Model.TryCreateXenonCoupling(
                fixture.CoreState.EnumerateBundles(),
                state,
                1.0));
        SyntheticGameCoreStateV1 refuelled = Require(
            fixture.CoreState.TryRefuel(
                0,
                GameRefuellingDirectionV1.TowardEndB,
                4,
                "NAT-U-SYNTHETIC",
                0.0)).ResultingState;
        ContractValidationResult<FullCoreDiffusionSolveResultV1> reused = fixture.Model.TrySolve(
            refuelled.EnumerateBundles(),
            coupling,
            1_000_000_000.0,
            fixture.BaseSolve.EffectiveK,
            fixture.BaseSolve.Group1Flux,
            fixture.BaseSolve.Group2Flux);
        Assert.False(reused.IsValid);
        Assert.Equal("FullCoreDiffusionSolve.XenonCoupling.BaseDigest.Stale", reused.FirstDiagnostic.Code);
    }

    private static CoreFixture CreateFixture()
    {
        FullCoreDiffusionDataPackV1 pack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        FullCoreDiffusionModelV1 model = Require(
            FullCoreDiffusionModelV1.TryCreateCandu6(pack));
        SyntheticGameCoreStateV1 coreState = SyntheticGameCoreStateV1.CreatePractice();
        FullCoreDiffusionSolveResultV1 baseSolve = Require(
            model.TrySolve(coreState.EnumerateBundles(), 1_000_000_000.0));
        NuclideDataV1 data = Require(
            NuclideDataV1.TryCreate(
                new MaterialVariantId("NAT-U-SYNTHETIC"),
                "candu6-practice-xenon-v1",
                Digest(0x71),
                0.05,
                0.01,
                2.91e-5,
                2.09e-5,
                1.0e-24,
                3.0e-24));
        return new CoreFixture(model, coreState, baseSolve, data);
    }

    private static XenonSpatialStateV1 CreateStateWithXe(
        CoreFixture fixture,
        int localNodeIndex,
        double xeInventory)
    {
        NuclideStateEnvelopeV1[] states = fixture.CoreState
            .EnumerateBundles()
            .Select((bundle, index) => Require(
                NuclideStateEnvelopeV1.TryCreateFresh(
                    bundle.BundleId,
                    0.0,
                    index == localNodeIndex ? xeInventory : 0.0,
                    fixture.Model.DataPack.NodeVolumeM3,
                    0UL,
                    fixture.NuclideData)))
            .ToArray();
        return Require(
            XenonSpatialStateV1.TryCreate(
                fixture.Model,
                fixture.CoreState.EnumerateBundles(),
                states));
    }

    private static XenonSpatialStateV1 AdvanceOneHour(
        CoreFixture fixture,
        XenonSpatialStateV1 state,
        int ownerSequence)
    {
        XenonSpatialStateBindingV1 binding = Require(state.TryCreateBinding(1.0));
        return Require(
            state.TryAdvance(
                binding,
                state.SimulationTimeSeconds + 3_600.0,
                1.0,
                fixture.BaseSolve.Coefficients,
                fixture.BaseSolve.Group1Flux,
                fixture.BaseSolve.Group2Flux,
                1.0,
                StableId.Parse(
                    "00000000-0000-0000-0000-" + ownerSequence.ToString("D12", CultureInfo.InvariantCulture))))
            .ResultingState;
    }

    private sealed class CoreFixture
    {
        public CoreFixture(
            FullCoreDiffusionModelV1 model,
            SyntheticGameCoreStateV1 coreState,
            FullCoreDiffusionSolveResultV1 baseSolve,
            NuclideDataV1 nuclideData)
        {
            Model = model;
            CoreState = coreState;
            BaseSolve = baseSolve;
            NuclideData = nuclideData;
        }

        public FullCoreDiffusionModelV1 Model { get; }

        public SyntheticGameCoreStateV1 CoreState { get; }

        public FullCoreDiffusionSolveResultV1 BaseSolve { get; }

        public NuclideDataV1 NuclideData { get; }
    }

    private static Digest32 Digest(byte value)
    {
        return new Digest32(Enumerable.Repeat(value, 32).ToArray());
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }
}
