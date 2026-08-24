using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P4T04SpatialConvergenceTests
{
    private static readonly double[] UnequalGroup1Flux = { 1.0, 2.0 };

    private static readonly double[] UnitGroup2Flux = { 1.0, 1.0 };

    private static readonly double[] NegativeGroup1Flux = { -1.0, 1.0 };

    [Fact]
    public void OuterSolveConvergesOnlyAfterAllConditionsPass()
    {
        ContractValidationResult<SpatialEigenSolve> solve = CreateSolve();
        Assert.True(solve.IsValid, solve.IsValid ? string.Empty : solve.FirstDiagnostic.ToString());

        ContractValidationResult<SpatialSolveResult> result = solve.Value.TrySolve();

        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        Assert.Equal(SpatialSolveStatus.Converged, result.Value.Status);
        Assert.True(result.Value.HasUsableState);
        Assert.NotNull(result.Value.FinalState);
        Assert.Equal(2, result.Value.Diagnostics.IterationCount);
        Assert.Equal("converged", result.Value.Diagnostics.ConvergenceReason);
        Assert.Equal(SpatialInnerSolveStatus.Succeeded, result.Value.Diagnostics.InnerSolveStatus);
        Assert.Equal(0.0, result.Value.Diagnostics.ResidualAbsoluteInfinity!.Value, 12);
        Assert.Equal(0.0, result.Value.Diagnostics.ResidualRelativeInfinity!.Value, 12);
        Assert.Equal(0.0, result.Value.Diagnostics.SourceShapeChangeInfinity!.Value, 12);
        Assert.Equal(0.0, result.Value.Diagnostics.PowerBalanceRelative!.Value, 12);
        Assert.Equal(0, result.Value.Diagnostics.ClampCount);
        Assert.Equal(0, result.Value.Diagnostics.ForbiddenClampCount);
        Assert.Equal(2, result.Value.FinalState!.Iteration);
    }

    [Fact]
    public void MaximumIterationsReturnsDiagnosticsWithoutUsableState()
    {
        ContractValidationResult<SpatialEigenSolve> solve = CreateSolve(maximumIterations: 1);
        Assert.True(solve.IsValid, solve.IsValid ? string.Empty : solve.FirstDiagnostic.ToString());

        ContractValidationResult<SpatialSolveResult> result = solve.Value.TrySolve();

        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        Assert.Equal(SpatialSolveStatus.Nonconverged, result.Value.Status);
        Assert.False(result.Value.HasUsableState);
        Assert.Null(result.Value.FinalState);
        Assert.Equal(1, result.Value.Diagnostics.IterationCount);
        Assert.Equal("maximum_iterations_exhausted", result.Value.Diagnostics.ConvergenceReason);
        Assert.Equal("SpatialEigenSolve.Nonconverged", result.Value.Diagnostics.FailureDiagnostic!.Code);
        Assert.Equal(5.0 / 16.0, result.Value.Diagnostics.EigenvalueChangeAbsolute!.Value, 12);
        Assert.Equal(5.0 / 16.0, result.Value.Diagnostics.EigenvalueChangeRelative!.Value, 12);
    }

    [Fact]
    public void ConvergencePolicyRejectsEveryInvalidThresholdClass()
    {
        ContractValidationResult<SpatialConvergencePolicy> invalidKAbsolute =
            SpatialConvergencePolicy.TryCreate(0.0, 1e-8, 1e-8, 1e-8, 1e-8, 2);
        ContractValidationResult<SpatialConvergencePolicy> invalidKRelative =
            SpatialConvergencePolicy.TryCreate(1e-8, double.NaN, 1e-8, 1e-8, 1e-8, 2);
        ContractValidationResult<SpatialConvergencePolicy> invalidResidual =
            SpatialConvergencePolicy.TryCreate(1e-8, 1e-8, double.PositiveInfinity, 1e-8, 1e-8, 2);
        ContractValidationResult<SpatialConvergencePolicy> invalidShape =
            SpatialConvergencePolicy.TryCreate(1e-8, 1e-8, 1e-8, -1.0, 1e-8, 2);
        ContractValidationResult<SpatialConvergencePolicy> invalidPower =
            SpatialConvergencePolicy.TryCreate(1e-8, 1e-8, 1e-8, 1e-8, 0.0, 2);
        ContractValidationResult<SpatialConvergencePolicy> invalidLimit =
            SpatialConvergencePolicy.TryCreate(1e-8, 1e-8, 1e-8, 1e-8, 1e-8, 0);

        Assert.False(invalidKAbsolute.IsValid);
        Assert.Equal("SpatialConvergencePolicy.KAbsoluteTolerance.Invalid", invalidKAbsolute.FirstDiagnostic.Code);
        Assert.False(invalidKRelative.IsValid);
        Assert.Equal("SpatialConvergencePolicy.KRelativeTolerance.Invalid", invalidKRelative.FirstDiagnostic.Code);
        Assert.False(invalidResidual.IsValid);
        Assert.Equal("SpatialConvergencePolicy.ResidualTolerance.Invalid", invalidResidual.FirstDiagnostic.Code);
        Assert.False(invalidShape.IsValid);
        Assert.Equal("SpatialConvergencePolicy.SourceShapeTolerance.Invalid", invalidShape.FirstDiagnostic.Code);
        Assert.False(invalidPower.IsValid);
        Assert.Equal("SpatialConvergencePolicy.PowerBalanceTolerance.Invalid", invalidPower.FirstDiagnostic.Code);
        Assert.False(invalidLimit.IsValid);
        Assert.Equal("SpatialConvergencePolicy.IterationLimit.Invalid", invalidLimit.FirstDiagnostic.Code);
    }

    [Fact]
    public void FailedInnerSolveReturnsFailureDiagnosticsAndNoState()
    {
        ContractValidationResult<SpatialEigenSolve> solve = CreateSolve(
            innerMaximumIterations: 1,
            initialGroup1Flux: UnequalGroup1Flux);
        Assert.True(solve.IsValid, solve.IsValid ? string.Empty : solve.FirstDiagnostic.ToString());

        ContractValidationResult<SpatialSolveResult> result = solve.Value.TrySolve();

        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        Assert.Equal(SpatialSolveStatus.Failed, result.Value.Status);
        Assert.False(result.Value.HasUsableState);
        Assert.Equal(SpatialInnerSolveStatus.Failed, result.Value.Diagnostics.InnerSolveStatus);
        Assert.Equal("inner_solve_failed", result.Value.Diagnostics.ConvergenceReason);
        Assert.Equal(1, result.Value.Diagnostics.FailedInnerSolveCount);
        Assert.Equal("SpatialEigenIteration.InnerSolve.Nonconverged", result.Value.Diagnostics.FailureDiagnostic!.Code);
    }

    [Fact]
    public void InvalidNumericInitialStateReturnsFailClosedDiagnostic()
    {
        ContractValidationResult<SpatialEigenSolve> solve = CreateSolve();
        Assert.True(solve.IsValid, solve.IsValid ? string.Empty : solve.FirstDiagnostic.ToString());

        ReplaceInitialState(solve.Value, double.NaN, UnitGroup2Flux, UnitGroup2Flux);

        ContractValidationResult<SpatialSolveResult> result = solve.Value.TrySolve();

        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        Assert.Equal(SpatialSolveStatus.Failed, result.Value.Status);
        Assert.Null(result.Value.FinalState);
        Assert.Equal("invalid_state", result.Value.Diagnostics.ConvergenceReason);
        Assert.Equal("SpatialEigenSolve.State.Scalar.NonFinite", result.Value.Diagnostics.FailureDiagnostic!.Code);
        Assert.Equal(1, result.Value.Diagnostics.NonFiniteValueCount);
    }

    [Fact]
    public void NegativeInitialFluxReturnsNegativeFluxCounter()
    {
        ContractValidationResult<SpatialEigenSolve> solve = CreateSolve();
        Assert.True(solve.IsValid, solve.IsValid ? string.Empty : solve.FirstDiagnostic.ToString());
        ReplaceInitialState(solve.Value, 1.0, NegativeGroup1Flux, UnitGroup2Flux);

        ContractValidationResult<SpatialSolveResult> result = solve.Value.TrySolve();

        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        Assert.Equal(SpatialSolveStatus.Failed, result.Value.Status);
        Assert.Equal("SpatialEigenSolve.State.Flux.Negative", result.Value.Diagnostics.FailureDiagnostic!.Code);
        Assert.Equal(1, result.Value.Diagnostics.NegativeFluxCount);
        Assert.Equal(0, result.Value.Diagnostics.NonFiniteValueCount);
        Assert.Null(result.Value.FinalState);
    }

    [Fact]
    public void NonFiniteJacobiUpdateReturnsNonFiniteCounter()
    {
        ContractValidationResult<SpatialEigenSolve> solve = CreateSolve(
            innerMaximumIterations: 8,
            absorptionGroup1: double.Epsilon,
            absorptionGroup2: 1.0,
            downscatter: 0.0,
            fissionGroup1: double.Epsilon,
            fissionGroup2: 0.0,
            nuFissionGroup1: 8e-16,
            nuFissionGroup2: 0.0,
            energyPerFission: double.MaxValue,
            edgeConductance: double.Epsilon);
        Assert.True(solve.IsValid, solve.IsValid ? string.Empty : solve.FirstDiagnostic.ToString());

        ContractValidationResult<SpatialSolveResult> result = solve.Value.TrySolve();

        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        Assert.Equal(SpatialSolveStatus.Failed, result.Value.Status);
        Assert.Equal("SpatialEigenIteration.InnerFlux.NonFinite", result.Value.Diagnostics.FailureDiagnostic!.Code);
        Assert.Equal(1, result.Value.Diagnostics.NonFiniteValueCount);
        Assert.Equal(0, result.Value.Diagnostics.NegativeFluxCount);
        Assert.Null(result.Value.FinalState);
    }

    [Fact]
    public void ReorderedCoefficientInputsProduceIdenticalOuterDiagnostics()
    {
        ContractValidationResult<SpatialEigenSolve> firstSolve = CreateSolve();
        ContractValidationResult<SpatialEigenSolve> secondSolve = CreateSolve(reverseRecords: true);
        Assert.True(firstSolve.IsValid, firstSolve.IsValid ? string.Empty : firstSolve.FirstDiagnostic.ToString());
        Assert.True(secondSolve.IsValid, secondSolve.IsValid ? string.Empty : secondSolve.FirstDiagnostic.ToString());

        SpatialSolveResult first = firstSolve.Value.TrySolve().Value;
        SpatialSolveResult second = secondSolve.Value.TrySolve().Value;

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.Diagnostics.IterationCount, second.Diagnostics.IterationCount);
        Assert.Equal(first.Diagnostics.ConvergenceReason, second.Diagnostics.ConvergenceReason);
        Assert.Equal(first.Diagnostics.EigenvalueChangeAbsolute, second.Diagnostics.EigenvalueChangeAbsolute);
        Assert.Equal(first.Diagnostics.EigenvalueChangeRelative, second.Diagnostics.EigenvalueChangeRelative);
        Assert.Equal(first.Diagnostics.ResidualAbsoluteInfinity, second.Diagnostics.ResidualAbsoluteInfinity);
        Assert.Equal(first.Diagnostics.ResidualRelativeInfinity, second.Diagnostics.ResidualRelativeInfinity);
        Assert.Equal(first.Diagnostics.SourceShapeChangeInfinity, second.Diagnostics.SourceShapeChangeInfinity);
        Assert.Equal(first.Diagnostics.PowerBalanceRelative, second.Diagnostics.PowerBalanceRelative);
        Assert.Equal(first.FinalState!.Eigenvalue, second.FinalState!.Eigenvalue, 12);
        Assert.Equal(first.FinalState.Group1Flux, second.FinalState.Group1Flux);
        Assert.Equal(first.FinalState.Group2Flux, second.FinalState.Group2Flux);
    }

    private static ContractValidationResult<SpatialEigenSolve> CreateSolve(
        int maximumIterations = 2,
        int innerMaximumIterations = 512,
        double[]? initialGroup1Flux = null,
        bool reverseRecords = false,
        double absorptionGroup1 = 0.3,
        double absorptionGroup2 = 0.2,
        double downscatter = 0.1,
        double fissionGroup1 = 0.1,
        double fissionGroup2 = 0.1,
        double nuFissionGroup1 = 0.15,
        double nuFissionGroup2 = 0.25,
        double energyPerFission = 1.0,
        double edgeConductance = 0.5)
    {
        SpatialStencil stencil = CreateStencil();
        SpatialNodeCoefficients[] nodes = stencil.Nodes
            .Select(node => new SpatialNodeCoefficients(
                node.Node,
                1.0,
                absorptionGroup1,
                absorptionGroup2,
                downscatter,
                fissionGroup1,
                fissionGroup2,
                nuFissionGroup1,
                nuFissionGroup2,
                1.0,
                0.0,
                energyPerFission))
            .ToArray();
        SpatialEdgeConductance[] edges =
        {
            new SpatialEdgeConductance(
                new NodeKey(new ChannelId(0), new BundlePosition(0)),
                new NodeKey(new ChannelId(0), new BundlePosition(1)),
                edgeConductance,
                edgeConductance)
        };
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
        Assert.True(coefficientResult.IsValid, coefficientResult.IsValid ? string.Empty : coefficientResult.FirstDiagnostic.ToString());

        ContractValidationResult<SpatialLinearSolvePolicy> linearPolicyResult =
            SpatialLinearSolvePolicy.TryCreate(
                SpatialLinearSolvePolicy.DeterministicJacobiMethodId,
                SpatialLinearSolvePolicy.DeterministicJacobiMethodVersion,
                1e-14,
                1e-14,
                innerMaximumIterations);
        Assert.True(linearPolicyResult.IsValid, linearPolicyResult.IsValid ? string.Empty : linearPolicyResult.FirstDiagnostic.ToString());

        ContractValidationResult<SpatialEigenIteration> iterationResult = SpatialEigenIteration.TryCreate(
            stencil,
            coefficientResult.Value,
            linearPolicyResult.Value,
            0.4,
            1.0,
            initialGroup1Flux,
            initialGroup1Flux == null ? null : UnitGroup2Flux);
        Assert.True(iterationResult.IsValid, iterationResult.IsValid ? string.Empty : iterationResult.FirstDiagnostic.ToString());

        ContractValidationResult<SpatialConvergencePolicy> convergencePolicyResult =
            SpatialConvergencePolicy.TryCreate(
                0.01,
                0.01,
                1e-12,
                1e-12,
                1e-12,
                maximumIterations);
        Assert.True(convergencePolicyResult.IsValid, convergencePolicyResult.IsValid ? string.Empty : convergencePolicyResult.FirstDiagnostic.ToString());

        return SpatialEigenSolve.TryCreate(iterationResult.Value, convergencePolicyResult.Value);
    }

    private static SpatialStencil CreateStencil()
    {
        ChannelId channelId = new ChannelId(0);
        BundlePosition firstPosition = new BundlePosition(0);
        BundlePosition secondPosition = new BundlePosition(1);
        var neighbors = new[]
        {
            new NeighborRecord(
                channelId,
                firstPosition,
                channelId,
                secondPosition,
                NeighborDirection.TowardEndB),
            new NeighborRecord(
                channelId,
                secondPosition,
                channelId,
                firstPosition,
                NeighborDirection.TowardEndA)
        };
        var boundaries = new List<BoundaryFaceRecord>();
        foreach (BundlePosition position in new[] { firstPosition, secondPosition })
        {
            foreach (TopologyFace face in new[]
                     {
                         TopologyFace.North,
                         TopologyFace.East,
                         TopologyFace.South,
                         TopologyFace.West
                     })
            {
                boundaries.Add(new BoundaryFaceRecord(
                    channelId,
                    position,
                    face,
                    BoundaryClassification.Reflective));
            }
        }

        boundaries.Add(new BoundaryFaceRecord(
            channelId,
            firstPosition,
            TopologyFace.EndA,
            BoundaryClassification.Reflective));
        boundaries.Add(new BoundaryFaceRecord(
            channelId,
            secondPosition,
            TopologyFace.EndB,
            BoundaryClassification.Reflective));

        ChannelTopology channel = new ChannelTopology(
            channelId,
            0,
            0,
            FlowDirection.EndAtoEndB,
            firstPosition,
            secondPosition,
            neighbors,
            boundaries);
        ContractValidationResult<CoreTopology> topologyResult = CoreTopology.TryCreate(
            1,
            2,
            new[] { channel });
        Assert.True(topologyResult.IsValid, topologyResult.IsValid ? string.Empty : topologyResult.FirstDiagnostic.ToString());

        ContractValidationResult<SpatialStencil> stencilResult = SpatialStencil.TryCreate(topologyResult.Value);
        Assert.True(stencilResult.IsValid, stencilResult.IsValid ? string.Empty : stencilResult.FirstDiagnostic.ToString());
        return stencilResult.Value;
    }

    private static void ReplaceInitialState(
        SpatialEigenSolve solve,
        double eigenvalue,
        double[] group1Flux,
        double[] group2Flux)
    {
        FieldInfo iterationField = typeof(SpatialEigenSolve).GetField(
            "_iteration",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        SpatialEigenIteration iteration = (SpatialEigenIteration)iterationField.GetValue(solve)!;
        ConstructorInfo stateConstructor = typeof(SpatialEigenIterationState).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: new[]
            {
                typeof(SpatialEigenIteration),
                typeof(int),
                typeof(double),
                typeof(double),
                typeof(double),
                typeof(double),
                typeof(double[]),
                typeof(double[])
            },
            modifiers: null)!;
        SpatialEigenIterationState replacement = (SpatialEigenIterationState)stateConstructor.Invoke(new object[]
        {
            iteration,
            0,
            eigenvalue,
            1.0,
            0.4,
            0.8,
            group1Flux,
            group2Flux
        });
        FieldInfo initialStateField = typeof(SpatialEigenIteration).GetField(
            "_initialState",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        initialStateField.SetValue(iteration, replacement);
    }
}
