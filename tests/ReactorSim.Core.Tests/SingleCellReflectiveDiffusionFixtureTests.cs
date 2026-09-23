using System;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class SingleCellReflectiveDiffusionFixtureTests
{
    [Fact]
    public void FreshFixtureExposesOneNodeSixReflectiveFacesAndPackBoundCoefficients()
    {
        SingleCellReflectiveDiffusionFixtureV1 fixture = Require(
            SingleCellReflectiveDiffusionFixtureV1.TryCreate());

        Assert.Equal("NAT-U-SYNTHETIC", fixture.MaterialVariantId.Value);
        Assert.Equal(0.0, fixture.FreshBurnupJPerKgHm, 14);
        Assert.Equal("synthetic-calibrated", fixture.DataPack.EvidenceClass);
        Assert.Contains("surrogate", fixture.DataPack.SourceProvenance, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(1u, fixture.Topology.ChannelCount);
        Assert.Equal(1u, fixture.Topology.BundlePositionCount);
        Assert.Equal(1, fixture.Stencil.NodeCount);
        Assert.Empty(fixture.Stencil.Nodes[0].NeighborTerms);
        Assert.Equal(6, fixture.Stencil.Nodes[0].BoundaryTerms.Count);
        Assert.All(
            fixture.Stencil.Nodes[0].BoundaryTerms,
            boundary => Assert.Equal(BoundaryClassification.Reflective, boundary.Classification));

        Assert.Equal(0, fixture.Coefficients.EdgeCount);
        Assert.Equal(6, fixture.Coefficients.BoundaryCount);
        Assert.All(
            fixture.Coefficients.Nodes,
            coefficients => Assert.Equal(fixture.DataPack.NodeVolumeM3, coefficients.VolumeM3, 14));
        Assert.All(
            fixture.Stencil.Nodes[0].BoundaryTerms,
            boundary =>
            {
                Assert.True(fixture.Coefficients.TryGetBoundary(
                    new SpatialBoundaryKey(fixture.Stencil.Nodes[0].Node, boundary.Face),
                    out SpatialConductancePair conductance));
                Assert.Equal(0.0, conductance.Group1, 14);
                Assert.Equal(0.0, conductance.Group2, 14);
            });
    }

    [Fact]
    public void FreshPackRowHasIndependentOneCellAnalyticCheck()
    {
        SingleCellReflectiveDiffusionFixtureV1 fixture = Require(
            SingleCellReflectiveDiffusionFixtureV1.TryCreate());
        SpatialNodeCoefficients coefficients = Assert.Single(fixture.Coefficients.Nodes);

        double thermalToFastFluxRatio =
            coefficients.DownscatterGroup1To2PerM / coefficients.AbsorptionGroup2PerM;
        double kInfinite =
            (coefficients.NuFissionGroup1PerM +
             (coefficients.NuFissionGroup2PerM * thermalToFastFluxRatio)) /
            (coefficients.AbsorptionGroup1PerM + coefficients.DownscatterGroup1To2PerM);

        Assert.Equal(1.25, thermalToFastFluxRatio, 12);
        Assert.Equal(1.109625, kInfinite, 12);
        Assert.Equal(
            fixture.MaterialLookup.Coefficients.AbsorptionGroup1PerM,
            coefficients.AbsorptionGroup1PerM,
            14);
        Assert.Equal(
            fixture.MaterialLookup.Coefficients.AbsorptionGroup2PerM,
            coefficients.AbsorptionGroup2PerM,
            14);
        Assert.Equal(
            fixture.MaterialLookup.Coefficients.DownscatterGroup1To2PerM,
            coefficients.DownscatterGroup1To2PerM,
            14);
        Assert.Equal(
            fixture.MaterialLookup.Coefficients.EnergyPerFissionJ,
            coefficients.EnergyPerFissionJ,
            14);
    }

    [Fact]
    public void TrySolveRunsSpatialEigenSolverAndNormalizesToOneWatt()
    {
        SingleCellReflectiveDiffusionFixtureV1 fixture = Require(
            SingleCellReflectiveDiffusionFixtureV1.TryCreate());
        SpatialEigenIteration iteration = Require(fixture.TryCreateIteration());
        SpatialEigenSolve solve = Require(fixture.TryCreateSolve());
        SpatialSolveResult result = Require(solve.TrySolve());

        Assert.Equal(1.0, iteration.TargetPowerW, 14);
        Assert.Equal(SpatialSolveStatus.Converged, result.Status);
        Assert.True(result.HasUsableState);
        Assert.NotNull(result.FinalState);
        SpatialEigenIterationState state = result.FinalState!;

        Assert.Equal(1.109625, state.Eigenvalue, 10);
        Assert.Equal(1.0, state.TotalPowerW, 12);
        Assert.Equal(1.25, state.Group2Flux[0] / state.Group1Flux[0], 10);
        SpatialNodeCoefficients coefficients = Assert.Single(fixture.Coefficients.Nodes);
        double thermalToFastFluxRatio =
            coefficients.DownscatterGroup1To2PerM / coefficients.AbsorptionGroup2PerM;
        double expectedFastFlux = SingleCellReflectiveDiffusionFixtureV1.TargetPowerWatts /
            (coefficients.VolumeM3 *
             coefficients.EnergyPerFissionJ *
             (coefficients.FissionGroup1PerM +
              (coefficients.FissionGroup2PerM * thermalToFastFluxRatio)));
        double expectedThermalFlux = expectedFastFlux * thermalToFastFluxRatio;
        Assert.InRange(
            Math.Abs((state.Group1Flux[0] / expectedFastFlux) - 1.0),
            0.0,
            1.0e-11);
        Assert.InRange(
            Math.Abs((state.Group2Flux[0] / expectedThermalFlux) - 1.0),
            0.0,
            1.0e-11);
        Assert.InRange(result.Diagnostics.ResidualRelativeInfinity!.Value, 0.0, 1.0e-12);
        Assert.InRange(result.Diagnostics.PowerBalanceRelative!.Value, 0.0, 1.0e-12);
        Assert.Single(state.Group1Flux);
        Assert.Single(state.Group2Flux);
        Assert.True(double.IsFinite(state.Group1Flux[0]) && state.Group1Flux[0] > 0.0);
        Assert.True(double.IsFinite(state.Group2Flux[0]) && state.Group2Flux[0] > 0.0);
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }
}
