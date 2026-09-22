using System;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class CanduTwoGroupLatticeReferenceTests
{
    private static readonly double[] UnitFlux = { 1.0 };

    [Fact]
    public void UserReferenceRowsRunThroughOneNodeReflectiveEigenSolve()
    {
        CanduTwoGroupLatticeReferenceV1 reference = Require(
            CanduTwoGroupLatticeReferenceV1.TryCreateUserSupplied());

        foreach (CanduTwoGroupLatticeReferenceStateV1 row in reference.States)
        {
            CanduTwoGroupReflectiveLatticeV1 lattice = Require(
                CanduTwoGroupReflectiveLatticeV1.TryCreate(row));
            SpatialLinearSolvePolicy linearPolicy = Require(
                SpatialLinearSolvePolicy.TryCreate(
                    SpatialLinearSolvePolicy.DeterministicJacobiMethodId,
                    SpatialLinearSolvePolicy.DeterministicJacobiMethodVersion,
                    absoluteResidualTolerance: 1e-13,
                    relativeResidualTolerance: 1e-13,
                    maximumInnerIterations: 128));
            double targetPower =
                row.HGroup1WattsPerFluxM2Second +
                row.HGroup2WattsPerFluxM2Second;
            SpatialEigenIteration iteration = Require(
                SpatialEigenIteration.TryCreate(
                    lattice.Stencil,
                    lattice.Coefficients,
                    linearPolicy,
                    targetPower,
                    initialEigenvalue: 1.0,
                    initialGroup1Flux: UnitFlux,
                    initialGroup2Flux: UnitFlux));
            SpatialEigenSolve solve = Require(
                SpatialEigenSolve.TryCreate(
                    iteration,
                    Require(SpatialConvergencePolicy.TryCreate(
                        kAbsoluteTolerance: 1e-11,
                        kRelativeTolerance: 1e-11,
                        residualTolerance: 1e-11,
                        sourceShapeTolerance: 1e-11,
                        powerBalanceTolerance: 1e-11,
                        maximumIterations: 128))));

            SpatialSolveResult result = Require(solve.TrySolve());

            Assert.Equal(SpatialSolveStatus.Converged, result.Status);
            Assert.NotNull(result.FinalState);
            Assert.Equal(row.KInfinite, result.FinalState!.Eigenvalue, 10);
            Assert.Equal(targetPower, result.FinalState.TotalPowerW, 15);
            Assert.Equal(
                row.ThermalFluxToFastFluxRatio,
                result.FinalState.Group2Flux[0] / result.FinalState.Group1Flux[0],
                10);
            Assert.Equal(0.0, result.Diagnostics.PowerBalanceRelative!.Value, 15);
        }
    }

    [Fact]
    public void UserReferenceLatticeHasOneNodeSixReflectiveFacesAndNoEdges()
    {
        CanduTwoGroupLatticeReferenceV1 reference = Require(
            CanduTwoGroupLatticeReferenceV1.TryCreateUserSupplied());
        CanduTwoGroupReflectiveLatticeV1 lattice = Require(
            CanduTwoGroupReflectiveLatticeV1.TryCreate(reference.States[0]));

        Assert.Equal(1, lattice.Stencil.NodeCount);
        Assert.Empty(lattice.Stencil.Nodes[0].NeighborTerms);
        Assert.Equal(6, lattice.Stencil.Nodes[0].BoundaryTerms.Count);
        Assert.All(
            lattice.Stencil.Nodes[0].BoundaryTerms,
            boundary => Assert.Equal(BoundaryClassification.Reflective, boundary.Classification));
        Assert.Equal(0, lattice.Coefficients.EdgeCount);
        Assert.Equal(6, lattice.Coefficients.BoundaryCount);
    }

    [Fact]
    public void UserReferenceLookupInterpolatesAndRejectsExtrapolation()
    {
        CanduTwoGroupLatticeReferenceV1 reference = Require(
            CanduTwoGroupLatticeReferenceV1.TryCreateUserSupplied());

        CanduTwoGroupLatticeLookupResultV1 lookup = Require(reference.TryLookup(0.65));
        Assert.Equal(1, lookup.BracketLowerIndex);
        Assert.Equal(2, lookup.BracketUpperIndex);
        Assert.Equal(0.5, lookup.InterpolationFraction, 14);
        Assert.Equal(0.65, lookup.State.IrradiationNkb, 14);
        Assert.False(reference.TryLookup(-0.01).IsValid);
        Assert.False(reference.TryLookup(1.81).IsValid);
    }

    [Fact]
    public void FourFactorAndBucklingMetricsMatchThePublishedRoundedValues()
    {
        CanduTwoGroupLatticeReferenceV1 reference = Require(
            CanduTwoGroupLatticeReferenceV1.TryCreateUserSupplied());
        double[] expectedEpsilon = { 1.2306, 1.2210, 1.2637, 1.3050 };
        double[] expectedResonanceEscape = { 0.6269, 0.6258, 0.6247, 0.6234 };
        double[] expectedEtaF = { 1.1758, 1.2187, 1.0130, 0.8683 };
        double[] expectedKInfinite = { 0.9071, 0.9312, 0.7997, 0.7064 };
        double[] expectedL1Squared = { 56.73, 56.72, 56.75, 56.77 };
        double[] expectedL2Squared = { 166.46, 159.96, 150.93, 143.42 };
        double[] expectedEffectiveK = { 0.8495, 0.8737, 0.7523, 0.6660 };

        Assert.Equal(expectedKInfinite.Length, reference.States.Count);
        for (int index = 0; index < expectedKInfinite.Length; index++)
        {
            CanduTwoGroupLatticeReferenceStateV1 row = reference.States[index];
            CanduTwoGroupReflectiveLatticeV1 lattice = Require(
                CanduTwoGroupReflectiveLatticeV1.TryCreate(row));
            CanduTwoGroupLeakageResultV1 leakage = lattice.CalculateFourFactorLeakage(
                CanduTwoGroupLatticeReferenceV1.ReferenceGeometricBucklingInverseCmSquared *
                10000.0);

            Assert.Equal(expectedEpsilon[index], row.EpsilonFastFission, 4);
            Assert.Equal(expectedResonanceEscape[index], row.ResonanceEscapeProbability, 4);
            Assert.Equal(expectedEtaF[index], row.EtaFThermalUtilization, 4);
            Assert.Equal(expectedKInfinite[index], row.KInfinite, 4);
            Assert.Equal(expectedL1Squared[index], row.L1SquaredCm2, 2);
            Assert.Equal(expectedL2Squared[index], row.L2SquaredCm2, 2);
            Assert.Equal(expectedEffectiveK[index], leakage.KEffective, 4);
        }
    }

    [Fact]
    public void ReferenceDAndTransportRowsAreConsistentWithOneOverThreeSigmaTransport()
    {
        CanduTwoGroupLatticeReferenceV1 reference = Require(
            CanduTwoGroupLatticeReferenceV1.TryCreateUserSupplied());

        foreach (CanduTwoGroupLatticeReferenceStateV1 row in reference.States)
        {
            Assert.Equal(
                1.0,
                3.0 * row.DiffusionGroup1Cm * row.TransportGroup1PerCm,
                3);
            Assert.Equal(
                1.0,
                3.0 * row.DiffusionGroup2Cm * row.TransportGroup2PerCm,
                3);
        }
    }

    [Fact]
    public void ReferenceAuditConfirmsUnitsAndNoInventedFissionCrossSection()
    {
        CanduTwoGroupLatticeReferenceV1 reference = Require(
            CanduTwoGroupLatticeReferenceV1.TryCreateUserSupplied());

        CanduTwoGroupLatticeAuditV1 audit = Require(reference.TryAudit());

        Assert.True(
            audit.Passed,
            string.Join(
                "; ",
                audit.Checks
                    .Where(check => !check.Passed)
                    .Select(check => check.Id + " actual=" + check.Actual + " expected=" + check.Expected)));
        Assert.Equal("user-supplied; unverified", reference.SourceProvenance);
        Assert.Equal(0.625, reference.ThermalCutoffElectronVolts, 12);
        Assert.Equal("fast,thermal", reference.EnergyGroupOrder);
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        if (!result.IsValid)
        {
            throw new InvalidOperationException(result.FirstDiagnostic.ToString());
        }

        return result.Value;
    }
}
