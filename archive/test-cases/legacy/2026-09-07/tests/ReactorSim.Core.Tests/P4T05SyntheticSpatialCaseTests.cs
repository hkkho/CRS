using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P4T05SyntheticSpatialCaseTests
{
    private static readonly double[] TwoNodeInitialFlux = { 1.0, 1.0 };

    private static readonly double[] ThreeNodeInitialFlux = { 1.0, 1.0, 1.0 };

    [Fact]
    public void OneNodeLeakageFreeAlgebraicCaseMatchesFrozenContract()
    {
        const double absorptionGroup1 = 0.3;
        const double absorptionGroup2 = 0.2;
        const double downscatter = 0.1;
        const double fissionGroup1 = 0.1;
        const double fissionGroup2 = 0.1;
        const double nuFissionGroup1 = 0.15;
        const double nuFissionGroup2 = 0.25;

        double removalGroup1 = absorptionGroup1 + downscatter;
        double fluxRatio = downscatter / absorptionGroup2;
        double eigenvalue =
            (nuFissionGroup1 + nuFissionGroup2 * fluxRatio) / removalGroup1;

        Assert.Equal(0.5, fluxRatio, 12);
        Assert.Equal(0.6875, eigenvalue, 12);

        double initialScale = 0.30 /
            (fissionGroup1 * 1.0 + fissionGroup2 * 1.0);
        double trialGroup1 =
            (nuFissionGroup1 * initialScale + nuFissionGroup2 * initialScale) /
            removalGroup1;
        double trialGroup2 = downscatter * trialGroup1 / absorptionGroup2;
        double trialPower = fissionGroup1 * trialGroup1 + fissionGroup2 * trialGroup2;
        double secondScale = 0.30 / trialPower;

        Assert.Equal(1.5, initialScale, 12);
        Assert.Equal(1.5, trialGroup1, 12);
        Assert.Equal(0.75, trialGroup2, 12);
        Assert.Equal(4.0 / 3.0, secondScale, 12);
        Assert.Equal(2.0, trialGroup1 * secondScale, 12);
        Assert.Equal(1.0, trialGroup2 * secondScale, 12);
    }

    [Fact]
    public void SymmetricTwoNodeCasePreservesEqualFluxAndPower()
    {
        ContractValidationResult<SpatialEigenSolve> solve = CreateSolve(
            nodeCount: 2,
            targetPower: 0.60,
            initialGroup1Flux: TwoNodeInitialFlux,
            initialGroup2Flux: TwoNodeInitialFlux,
            edgeConductance: 0.6);
        AssertValid(solve);

        ContractValidationResult<SpatialSolveResult> result = solve.Value.TrySolve();
        AssertValid(result);

        SpatialSolveResult solved = result.Value;
        Assert.Equal(SpatialSolveStatus.Converged, solved.Status);
        Assert.True(solved.HasUsableState);
        Assert.Equal(2, solved.FinalState!.Group1Flux.Count);
        Assert.Equal(2, solved.FinalState.Group2Flux.Count);
        Assert.Equal(solved.FinalState.Group1Flux[0], solved.FinalState.Group1Flux[1], 12);
        Assert.Equal(solved.FinalState.Group2Flux[0], solved.FinalState.Group2Flux[1], 12);
        Assert.Equal(2.0, solved.FinalState.Group1Flux[0], 12);
        Assert.Equal(1.0, solved.FinalState.Group2Flux[0], 12);
        Assert.Equal(0.60, solved.FinalState.TotalPowerW, 12);
    }

    [Fact]
    public void HomogeneousThreeNodeCasePreservesNodewiseShape()
    {
        ContractValidationResult<SpatialEigenSolve> solve = CreateSolve(
            nodeCount: 3,
            targetPower: 0.90,
            initialGroup1Flux: ThreeNodeInitialFlux,
            initialGroup2Flux: ThreeNodeInitialFlux,
            edgeConductance: 0.6);
        AssertValid(solve);

        ContractValidationResult<SpatialSolveResult> result = solve.Value.TrySolve();
        AssertValid(result);

        SpatialSolveResult solved = result.Value;
        Assert.Equal(SpatialSolveStatus.Converged, solved.Status);
        Assert.True(solved.HasUsableState);
        Assert.Equal(3, solved.FinalState!.Group1Flux.Count);
        Assert.Equal(3, solved.FinalState.Group2Flux.Count);
        Assert.Equal(2.0, solved.FinalState.Group1Flux[0], 12);
        Assert.Equal(1.0, solved.FinalState.Group2Flux[0], 12);
        for (int nodeIndex = 1; nodeIndex < 3; nodeIndex++)
        {
            Assert.Equal(solved.FinalState.Group1Flux[0], solved.FinalState.Group1Flux[nodeIndex], 12);
            Assert.Equal(solved.FinalState.Group2Flux[0], solved.FinalState.Group2Flux[nodeIndex], 12);
        }

        Assert.Equal(0.90, solved.FinalState.TotalPowerW, 12);
        Assert.InRange(solved.Diagnostics.ResidualRelativeInfinity!.Value, 0.0, 1e-10);
    }

    [Fact]
    public void DeliberateNonconvergenceFailsClosed()
    {
        ContractValidationResult<SpatialEigenSolve> solve = CreateSolve(
            nodeCount: 2,
            targetPower: 0.60,
            initialGroup1Flux: TwoNodeInitialFlux,
            initialGroup2Flux: TwoNodeInitialFlux,
            maximumIterations: 1,
            kAbsoluteTolerance: 0.01,
            kRelativeTolerance: 0.01,
            residualTolerance: 0.000001,
            sourceShapeTolerance: 0.000001,
            powerBalanceTolerance: 0.000001,
            edgeConductance: 0.6);
        AssertValid(solve);

        ContractValidationResult<SpatialSolveResult> result = solve.Value.TrySolve();
        AssertValid(result);

        SpatialSolveResult failed = result.Value;
        Assert.Equal(SpatialSolveStatus.Nonconverged, failed.Status);
        Assert.False(failed.HasUsableState);
        Assert.Null(failed.FinalState);
        Assert.Equal(1, failed.Diagnostics.IterationCount);
        Assert.Equal("maximum_iterations_exhausted", failed.Diagnostics.ConvergenceReason);
        Assert.Equal("SpatialEigenSolve.Nonconverged", failed.Diagnostics.FailureDiagnostic!.Code);
        Assert.Equal(0.3125, failed.Diagnostics.EigenvalueChangeAbsolute!.Value, 12);
        Assert.Equal(0.3125, failed.Diagnostics.EigenvalueChangeRelative!.Value, 12);
        Assert.InRange(failed.Diagnostics.ResidualAbsoluteInfinity!.Value, 0.0, 1e-6);
        Assert.InRange(failed.Diagnostics.SourceShapeChangeInfinity!.Value, 0.0, 1e-6);
        Assert.InRange(failed.Diagnostics.PowerBalanceRelative!.Value, 0.0, 1e-12);
    }

    [Fact]
    public void HomogeneousCaseIsIndependentOfCoefficientRecordOrder()
    {
        ContractValidationResult<SpatialEigenSolve> firstSolve = CreateSolve(
            nodeCount: 3,
            targetPower: 0.90,
            initialGroup1Flux: ThreeNodeInitialFlux,
            initialGroup2Flux: ThreeNodeInitialFlux,
            edgeConductance: 0.6);
        ContractValidationResult<SpatialEigenSolve> reorderedSolve = CreateSolve(
            nodeCount: 3,
            targetPower: 0.90,
            initialGroup1Flux: ThreeNodeInitialFlux,
            initialGroup2Flux: ThreeNodeInitialFlux,
            edgeConductance: 0.6,
            reverseRecords: true);
        AssertValid(firstSolve);
        AssertValid(reorderedSolve);

        SpatialSolveResult first = firstSolve.Value.TrySolve().Value;
        SpatialSolveResult reordered = reorderedSolve.Value.TrySolve().Value;

        Assert.Equal(first.Status, reordered.Status);
        Assert.Equal(first.Diagnostics.IterationCount, reordered.Diagnostics.IterationCount);
        Assert.Equal(first.Diagnostics.ConvergenceReason, reordered.Diagnostics.ConvergenceReason);
        Assert.Equal(first.Diagnostics.EigenvalueChangeAbsolute, reordered.Diagnostics.EigenvalueChangeAbsolute);
        Assert.Equal(first.Diagnostics.EigenvalueChangeRelative, reordered.Diagnostics.EigenvalueChangeRelative);
        Assert.Equal(first.Diagnostics.ResidualAbsoluteInfinity, reordered.Diagnostics.ResidualAbsoluteInfinity);
        Assert.Equal(first.Diagnostics.ResidualRelativeInfinity, reordered.Diagnostics.ResidualRelativeInfinity);
        Assert.Equal(first.Diagnostics.SourceShapeChangeInfinity, reordered.Diagnostics.SourceShapeChangeInfinity);
        Assert.Equal(first.Diagnostics.PowerBalanceRelative, reordered.Diagnostics.PowerBalanceRelative);
        Assert.Equal(first.FinalState!.Eigenvalue, reordered.FinalState!.Eigenvalue, 12);
        Assert.Equal(first.FinalState.Group1Flux, reordered.FinalState.Group1Flux);
        Assert.Equal(first.FinalState.Group2Flux, reordered.FinalState.Group2Flux);
    }

    private static ContractValidationResult<SpatialEigenSolve> CreateSolve(
        int nodeCount,
        double targetPower,
        double[] initialGroup1Flux,
        double[] initialGroup2Flux,
        int maximumIterations = 2,
        int innerMaximumIterations = 512,
        double kAbsoluteTolerance = 0.000001,
        double kRelativeTolerance = 0.000001,
        double residualTolerance = 0.000001,
        double sourceShapeTolerance = 0.000001,
        double powerBalanceTolerance = 0.000001,
        bool reverseRecords = false,
        double edgeConductance = 0.0)
    {
        SpatialStencil stencil = CreateStencil(nodeCount);
        SpatialNodeCoefficients[] nodes = stencil.Nodes
            .Select(node => new SpatialNodeCoefficients(
                node.Node,
                1.0,
                0.3,
                0.2,
                0.1,
                0.1,
                0.1,
                0.15,
                0.25,
                1.0,
                0.0,
                1.0))
            .ToArray();
        SpatialEdgeConductance[] edges = Enumerable.Range(0, nodeCount - 1)
            .Select(index => new SpatialEdgeConductance(
                new NodeKey(new ChannelId(0), new BundlePosition((uint)index)),
                new NodeKey(new ChannelId(0), new BundlePosition((uint)(index + 1))),
                edgeConductance,
                edgeConductance))
            .ToArray();
        SpatialBoundaryConductance[] boundaries = stencil.Nodes
            .SelectMany(node => node.BoundaryTerms.Select(term => new SpatialBoundaryConductance(
                node.Node,
                term.Face,
                0.0,
                0.0)))
            .ToArray();

        if (reverseRecords)
        {
            nodes = nodes.Reverse().ToArray();
            edges = edges.Reverse().ToArray();
            boundaries = boundaries.Reverse().ToArray();
        }

        ContractValidationResult<SpatialCoefficientSet> coefficientResult =
            SpatialCoefficientSet.TryCreate(stencil, nodes, edges, boundaries);
        AssertValid(coefficientResult);

        ContractValidationResult<SpatialLinearSolvePolicy> linearPolicyResult =
            SpatialLinearSolvePolicy.TryCreate(
                SpatialLinearSolvePolicy.DeterministicJacobiMethodId,
                SpatialLinearSolvePolicy.DeterministicJacobiMethodVersion,
                1e-14,
                1e-14,
                innerMaximumIterations);
        AssertValid(linearPolicyResult);

        ContractValidationResult<SpatialEigenIteration> iterationResult =
            SpatialEigenIteration.TryCreate(
                stencil,
                coefficientResult.Value,
                linearPolicyResult.Value,
                targetPower,
                1.0,
                initialGroup1Flux,
                initialGroup2Flux);
        AssertValid(iterationResult);

        ContractValidationResult<SpatialConvergencePolicy> convergencePolicyResult =
            SpatialConvergencePolicy.TryCreate(
                kAbsoluteTolerance,
                kRelativeTolerance,
                residualTolerance,
                sourceShapeTolerance,
                powerBalanceTolerance,
                maximumIterations);
        AssertValid(convergencePolicyResult);

        return SpatialEigenSolve.TryCreate(iterationResult.Value, convergencePolicyResult.Value);
    }

    private static SpatialStencil CreateStencil(int nodeCount)
    {
        Assert.True(nodeCount > 0);
        ChannelId channelId = new ChannelId(0);
        var neighbors = new List<NeighborRecord>();
        for (uint position = 0; position + 1 < nodeCount; position++)
        {
            neighbors.Add(new NeighborRecord(
                channelId,
                new BundlePosition(position),
                channelId,
                new BundlePosition(position + 1),
                NeighborDirection.TowardEndB));
            neighbors.Add(new NeighborRecord(
                channelId,
                new BundlePosition(position + 1),
                channelId,
                new BundlePosition(position),
                NeighborDirection.TowardEndA));
        }

        var boundaries = new List<BoundaryFaceRecord>();
        TopologyFace[] cardinalFaces =
        {
            TopologyFace.North,
            TopologyFace.East,
            TopologyFace.South,
            TopologyFace.West
        };
        for (uint position = 0; position < nodeCount; position++)
        {
            foreach (TopologyFace face in cardinalFaces)
            {
                boundaries.Add(new BoundaryFaceRecord(
                    channelId,
                    new BundlePosition(position),
                    face,
                    BoundaryClassification.Reflective));
            }
        }

        boundaries.Add(new BoundaryFaceRecord(
            channelId,
            new BundlePosition(0),
            TopologyFace.EndA,
            BoundaryClassification.Reflective));
        boundaries.Add(new BoundaryFaceRecord(
            channelId,
            new BundlePosition((uint)(nodeCount - 1)),
            TopologyFace.EndB,
            BoundaryClassification.Reflective));

        ChannelTopology channel = new ChannelTopology(
            channelId,
            0,
            0,
            FlowDirection.EndAtoEndB,
            new BundlePosition(0),
            new BundlePosition((uint)(nodeCount - 1)),
            neighbors,
            boundaries);
        ContractValidationResult<CoreTopology> topologyResult = CoreTopology.TryCreate(
            1,
            (uint)nodeCount,
            new[] { channel });
        AssertValid(topologyResult);

        ContractValidationResult<SpatialStencil> stencilResult = SpatialStencil.TryCreate(topologyResult.Value);
        AssertValid(stencilResult);
        return stencilResult.Value;
    }

    private static void AssertValid<T>(ContractValidationResult<T> result)
        where T : class
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
    }
}
