using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P4T03SpatialEigenIterationTests
{
    private static readonly double[] UnitFlux = { 1.0, 1.0 };

    private static readonly double[] InvalidGroup1Flux = { -1.0, 1.0 };

    private static readonly double[] UnequalGroup1Flux = { 1.0, 2.0 };

    [Fact]
    public void SourceIterationUpdatesEigenvalueAndNormalizesPower()
    {
        (SpatialStencil stencil, SpatialCoefficientSet coefficients) = CreateFixture();
        SpatialEigenIteration solver = CreateSolver(stencil, coefficients);
        SpatialEigenIterationState initial = solver.InitialState;

        Assert.Equal(0, initial.Iteration);
        Assert.Equal(1.0, initial.Eigenvalue, 12);
        Assert.Equal(1.0, initial.NormalizationScale, 12);
        Assert.Equal(0.4, initial.TotalPowerW, 12);
        Assert.Equal(0.8, initial.FissionProductionRate, 12);
        Assert.Equal(UnitFlux, initial.Group1Flux);
        Assert.Equal(UnitFlux, initial.Group2Flux);

        ContractValidationResult<SpatialEigenIterationState> stepResult = solver.TryStep(initial);

        Assert.True(stepResult.IsValid, stepResult.IsValid ? string.Empty : stepResult.FirstDiagnostic.ToString());
        SpatialEigenIterationState step = stepResult.Value;
        Assert.Equal(1, step.Iteration);
        Assert.Equal(11.0 / 16.0, step.Eigenvalue, 12);
        Assert.Equal(4.0 / 3.0, step.NormalizationScale, 12);
        Assert.Equal(0.4, step.TotalPowerW, 12);
        Assert.Equal(11.0 / 15.0, step.FissionProductionRate, 12);
        Assert.Equal(4.0 / 3.0, step.Group1Flux[0], 12);
        Assert.Equal(4.0 / 3.0, step.Group1Flux[1], 12);
        Assert.Equal(2.0 / 3.0, step.Group2Flux[0], 12);
        Assert.Equal(2.0 / 3.0, step.Group2Flux[1], 12);

        ContractValidationResult<SpatialEigenIterationState> secondResult = solver.TryStep(step);

        Assert.True(secondResult.IsValid, secondResult.IsValid ? string.Empty : secondResult.FirstDiagnostic.ToString());
        SpatialEigenIterationState second = secondResult.Value;
        Assert.Equal(step.Eigenvalue, second.Eigenvalue, 12);
        Assert.Equal(1.0, second.NormalizationScale, 12);
        Assert.Equal(step.TotalPowerW, second.TotalPowerW, 12);
        Assert.Equal(step.FissionProductionRate, second.FissionProductionRate, 12);
        Assert.Equal(step.Group1Flux[0], second.Group1Flux[0], 12);
        Assert.Equal(step.Group1Flux[1], second.Group1Flux[1], 12);
        Assert.Equal(step.Group2Flux[0], second.Group2Flux[0], 12);
        Assert.Equal(step.Group2Flux[1], second.Group2Flux[1], 12);
    }

    [Fact]
    public void OmittedInitialFluxUsesUnitVectorsAndStateIsImmutable()
    {
        (SpatialStencil stencil, SpatialCoefficientSet coefficients) = CreateFixture();
        SpatialEigenIteration solver = CreateSolver(stencil, coefficients);
        SpatialEigenIterationState state = solver.InitialState;

        Assert.Equal(UnitFlux, state.Group1Flux);
        Assert.Equal(UnitFlux, state.Group2Flux);

        var group1 = Assert.IsAssignableFrom<IList<double>>(state.Group1Flux);
        Assert.Throws<NotSupportedException>(() => group1[0] = 2.0);
        Assert.Equal(1.0, state.Group1Flux[0], 12);
    }

    [Fact]
    public void CoefficientOrderDoesNotChangeTheDeterministicStep()
    {
        (SpatialStencil firstStencil, SpatialCoefficientSet firstCoefficients) = CreateFixture();
        (SpatialStencil secondStencil, SpatialCoefficientSet secondCoefficients) = CreateFixture(reverseRecords: true);
        SpatialEigenIteration firstSolver = CreateSolver(firstStencil, firstCoefficients);
        SpatialEigenIteration secondSolver = CreateSolver(secondStencil, secondCoefficients);

        SpatialEigenIterationState first = firstSolver.TryStep(firstSolver.InitialState).Value;
        SpatialEigenIterationState second = secondSolver.TryStep(secondSolver.InitialState).Value;

        Assert.Equal(first.Eigenvalue, second.Eigenvalue, 12);
        Assert.Equal(first.NormalizationScale, second.NormalizationScale, 12);
        Assert.Equal(first.TotalPowerW, second.TotalPowerW, 12);
        Assert.Equal(first.FissionProductionRate, second.FissionProductionRate, 12);
        Assert.Equal(first.Group1Flux, second.Group1Flux);
        Assert.Equal(first.Group2Flux, second.Group2Flux);
    }

    [Fact]
    public void InvalidPoliciesFluxesAndForeignStatesFailClosed()
    {
        ContractValidationResult<SpatialLinearSolvePolicy> invalidPolicy =
            SpatialLinearSolvePolicy.TryCreate(
                SpatialLinearSolvePolicy.DeterministicJacobiMethodId,
                SpatialLinearSolvePolicy.DeterministicJacobiMethodVersion,
                0.0,
                1e-12,
                8);
        Assert.False(invalidPolicy.IsValid);
        Assert.Equal("SpatialLinearSolvePolicy.AbsoluteTolerance.Invalid", invalidPolicy.FirstDiagnostic.Code);

        (SpatialStencil stencil, SpatialCoefficientSet coefficients) = CreateFixture();
        SpatialLinearSolvePolicy policy = CreatePolicy();
        ContractValidationResult<SpatialEigenIteration> incompleteFlux = SpatialEigenIteration.TryCreate(
            stencil,
            coefficients,
            policy,
            0.4,
            1.0,
            UnitFlux,
            null);
        Assert.False(incompleteFlux.IsValid);
        Assert.Equal("SpatialEigenIteration.InitialFlux.Incomplete", incompleteFlux.FirstDiagnostic.Code);

        ContractValidationResult<SpatialEigenIteration> invalidFlux = SpatialEigenIteration.TryCreate(
            stencil,
            coefficients,
            policy,
            0.4,
            1.0,
            InvalidGroup1Flux,
            UnitFlux);
        Assert.False(invalidFlux.IsValid);
        Assert.Equal("SpatialEigenIteration.InitialFlux.Invalid", invalidFlux.FirstDiagnostic.Code);

        SpatialEigenIteration firstSolver = CreateSolver(stencil, coefficients);
        SpatialEigenIteration secondSolver = CreateSolver(stencil, coefficients);
        ContractValidationResult<SpatialEigenIterationState> foreignState =
            secondSolver.TryStep(firstSolver.InitialState);
        Assert.False(foreignState.IsValid);
        Assert.Equal("SpatialEigenIteration.State.OwnerMismatch", foreignState.FirstDiagnostic.Code);
    }

    [Fact]
    public void ExhaustedInnerSolveFailsClosed()
    {
        (SpatialStencil stencil, SpatialCoefficientSet coefficients) = CreateFixture();
        SpatialLinearSolvePolicy policy = CreatePolicy(maximumInnerIterations: 1);
        ContractValidationResult<SpatialEigenIteration> result = SpatialEigenIteration.TryCreate(
            stencil,
            coefficients,
            policy,
            0.4,
            1.0,
            UnequalGroup1Flux,
            UnitFlux);
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());

        ContractValidationResult<SpatialEigenIterationState> step = result.Value.TryStep(result.Value.InitialState);

        Assert.False(step.IsValid);
        Assert.Equal("SpatialEigenIteration.InnerSolve.Nonconverged", step.FirstDiagnostic.Code);
    }

    [Fact]
    public void IterationCounterOverflowFailsClosed()
    {
        (SpatialStencil stencil, SpatialCoefficientSet coefficients) = CreateFixture();
        SpatialEigenIteration solver = CreateSolver(stencil, coefficients);
        ConstructorInfo constructor = typeof(SpatialEigenIterationState).GetConstructor(
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
        SpatialEigenIterationState overflowState = (SpatialEigenIterationState)constructor.Invoke(new object[]
        {
            solver,
            int.MaxValue,
            1.0,
            1.0,
            0.4,
            0.8,
            UnitFlux,
            UnitFlux
        });

        ContractValidationResult<SpatialEigenIterationState> result = solver.TryStep(overflowState);

        Assert.False(result.IsValid);
        Assert.Equal("SpatialEigenIteration.Iteration.Overflow", result.FirstDiagnostic.Code);
    }

    private static SpatialEigenIteration CreateSolver(
        SpatialStencil stencil,
        SpatialCoefficientSet coefficients,
        SpatialLinearSolvePolicy? policy = null,
        double[]? initialGroup1Flux = null,
        double[]? initialGroup2Flux = null)
    {
        ContractValidationResult<SpatialEigenIteration> result = SpatialEigenIteration.TryCreate(
            stencil,
            coefficients,
            policy ?? CreatePolicy(),
            0.4,
            1.0,
            initialGroup1Flux,
            initialGroup2Flux);
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private static SpatialLinearSolvePolicy CreatePolicy(int maximumInnerIterations = 512)
    {
        ContractValidationResult<SpatialLinearSolvePolicy> result = SpatialLinearSolvePolicy.TryCreate(
            SpatialLinearSolvePolicy.DeterministicJacobiMethodId,
            SpatialLinearSolvePolicy.DeterministicJacobiMethodVersion,
            1e-14,
            1e-14,
            maximumInnerIterations);
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private static (SpatialStencil Stencil, SpatialCoefficientSet Coefficients) CreateFixture(
        bool reverseRecords = false)
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

        var channel = new ChannelTopology(
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

        SpatialStencil stencil = SpatialStencil.TryCreate(topologyResult.Value).Value;
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
        SpatialEdgeConductance[] edges =
        {
            new SpatialEdgeConductance(
                new NodeKey(channelId, firstPosition),
                new NodeKey(channelId, secondPosition),
                0.5,
                0.5)
        };
        SpatialBoundaryConductance[] boundaryConductances = stencil.Nodes
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
            boundaryConductances = boundaryConductances.Reverse().ToArray();
        }

        ContractValidationResult<SpatialCoefficientSet> coefficientResult = SpatialCoefficientSet.TryCreate(
            stencil,
            nodes,
            edges,
            boundaryConductances);
        Assert.True(coefficientResult.IsValid, coefficientResult.IsValid ? string.Empty : coefficientResult.FirstDiagnostic.ToString());
        return (stencil, coefficientResult.Value);
    }
}
