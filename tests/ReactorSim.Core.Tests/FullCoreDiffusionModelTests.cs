using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class FullCoreDiffusionModelTests
{
    [Fact]
    public void EmbeddedPackBuildsCanonicalCandu6Topology()
    {
        ContractValidationResult<FullCoreDiffusionDataPackV1> packResult =
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6();
        Assert.True(packResult.IsValid, packResult.IsValid ? string.Empty : packResult.FirstDiagnostic.ToString());

        ContractValidationResult<FullCoreDiffusionModelV1> modelResult =
            FullCoreDiffusionModelV1.TryCreateCandu6(packResult.Value);
        Assert.True(modelResult.IsValid, modelResult.IsValid ? string.Empty : modelResult.FirstDiagnostic.ToString());
        Assert.Equal(380u, modelResult.Value.Topology.ChannelCount);
        Assert.Equal(12u, modelResult.Value.Topology.BundlePositionCount);
        Assert.Equal(4560, modelResult.Value.NodeCount);
        Assert.Equal("fast", packResult.Value.EnergyGroupOrder[0]);
        Assert.Equal("thermal", packResult.Value.EnergyGroupOrder[1]);
        Assert.Equal(FullCoreDiffusionDataPackV1.SupportedUnitsProfileId, packResult.Value.Descriptor.UnitsProfileId);
        Assert.Equal(FullCoreDiffusionDataPackV1.SupportedModelId, packResult.Value.ModelId);
        Assert.Equal(FullCoreDiffusionDataPackV1.SupportedSolverId, packResult.Value.SolverId);
        Assert.Equal("synthetic-precalibration", packResult.Value.EvidenceClass);
        Assert.Equal(6, Candu6CoreTopologyFactoryV1.GetRowLength(0));
        Assert.Equal(22, Candu6CoreTopologyFactoryV1.GetRowLength(8));
        Assert.True(Candu6CoreTopologyFactoryV1.TryGetChannelIndex(8, 0, out uint firstChannel));
        Assert.Equal(0u, firstChannel);
        Assert.False(Candu6CoreTopologyFactoryV1.TryGetChannelIndex(0, 0, out _));
        Assert.True(Candu6CoreTopologyFactoryV1.TryGetChannelIndex(0, 8, out _));
        Assert.Equal(21, modelResult.Value.Topology.Channels[0].CoordinateY);
        Assert.Equal(FlowDirection.EndAtoEndB, modelResult.Value.Topology.Channels[0].FlowDirection);
        Assert.Equal(FlowDirection.EndBtoEndA, modelResult.Value.Topology.Channels[1].FlowDirection);
        Assert.Contains(
            modelResult.Value.Topology.Channels[0].BoundaryFaces,
            boundary => boundary.Face == TopologyFace.North &&
                        boundary.Position == new BundlePosition(0));
        Assert.Contains(
            modelResult.Value.Topology.Channels[0].BoundaryFaces,
            boundary => boundary.Face == TopologyFace.EndA &&
                        boundary.Position == new BundlePosition(0));
    }

    [Fact]
    public void FullCoreSolveConvergesAndNormalizesPower()
    {
        FullCoreDiffusionDataPackV1 pack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        FullCoreDiffusionModelV1 model = Require(
            FullCoreDiffusionModelV1.TryCreateCandu6(pack));
        SyntheticGameCoreStateV1 state = SyntheticGameCoreStateV1.CreatePractice();

        ContractValidationResult<FullCoreDiffusionSolveResultV1> result = model.TrySolve(
            state.EnumerateBundles(),
            1_000_000_000.0);

        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        Assert.Equal(4560, result.Value.NodePowerWatts.Count);
        Assert.Equal(4560, result.Value.Group1Flux.Count);
        Assert.Equal(4560, result.Value.Group2Flux.Count);
        Assert.Equal(1_000_000_000.0, result.Value.TotalPowerWatts, 3);
        Assert.InRange(result.Value.PowerBalanceRelativeError, 0.0, 1e-12);
        Assert.True(result.Value.EffectiveK > 0.0);
        ContractValidationResult<SyntheticGameCoreStateV1> advanced =
            state.TryAddFissionEnergy(
                result.Value.NodePowerWatts.Select(power => power * 86400.0).ToArray());
        Assert.True(advanced.IsValid, advanced.IsValid ? string.Empty : advanced.FirstDiagnostic.ToString());
        ContractValidationResult<FullCoreDiffusionSolveResultV1> burned = model.TrySolve(
            advanced.Value.EnumerateBundles(),
            1_000_000_000.0,
            result.Value.EffectiveK,
            result.Value.Group1Flux,
            result.Value.Group2Flux);
        Assert.True(burned.IsValid, burned.IsValid ? string.Empty : burned.FirstDiagnostic.ToString());
        double reactivityChangePerFullPowerDay =
            (burned.Value.Reactivity - result.Value.Reactivity) * 1000.0;
        Assert.InRange(reactivityChangePerFullPowerDay, -0.65, -0.40);
        Assert.True(result.Value.NodePowerWatts.Max() > result.Value.NodePowerWatts.Min());
        Assert.True(result.Value.IterationCount > 0);
    }

    [Fact]
    public void SixteenFreshBundlesProducePositiveReactivityStep()
    {
        FullCoreDiffusionDataPackV1 pack = Require(
            FullCoreDiffusionDataPackV1.TryLoadEmbeddedCandu6());
        FullCoreDiffusionModelV1 model = Require(
            FullCoreDiffusionModelV1.TryCreateCandu6(pack));
        SyntheticGameCoreStateV1 state = SyntheticGameCoreStateV1.CreatePractice();
        FullCoreDiffusionSolveResultV1 baseline = Require(
            model.TrySolve(state.EnumerateBundles(), 1_000_000_000.0));
        ContractValidationResult<GameRefuellingResultV1> first = state.TryRefuel(
            189,
            GameRefuellingDirectionV1.TowardEndB,
            8,
            "NAT-U-SYNTHETIC",
            0.0);
        Assert.True(first.IsValid, first.IsValid ? string.Empty : first.FirstDiagnostic.ToString());
        ContractValidationResult<GameRefuellingResultV1> second = first.Value.ResultingState.TryRefuel(
            190,
            GameRefuellingDirectionV1.TowardEndB,
            8,
            "NAT-U-SYNTHETIC",
            0.0);
        Assert.True(second.IsValid, second.IsValid ? string.Empty : second.FirstDiagnostic.ToString());
        FullCoreDiffusionSolveResultV1 fresh = Require(
            model.TrySolve(
                second.Value.ResultingState.EnumerateBundles(),
                1_000_000_000.0,
                baseline.EffectiveK,
                baseline.Group1Flux,
                baseline.Group2Flux));

        double reactivityStepMk = (fresh.Reactivity - baseline.Reactivity) * 1000.0;
        Assert.InRange(reactivityStepMk, 0.35, 0.65);
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }
}
