using System;
using System.Collections.Generic;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P7T05XenonCouplingTests
{
    [Fact]
    public void AppliesDynamicXeAbsorptionOnlyAndPreservesValidatedBaseState()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        SpatialStencil stencil = Require(SpatialStencil.TryCreate(fixture.Topology));
        SpatialCoefficientSet baseCoefficients = CreateBaseCoefficients(stencil);
        SpatialNodeCoefficients baseFirst = baseCoefficients.Nodes[0];
        NuclideDataV1 data = CreateNuclideData(0.01, 0.02);
        XenonSpatialNodeInputV1[] inputs = CreateInputs(fixture, stencil, data, 0.0);
        XenonSpatialStateBindingV1 stateBinding = CreateStateBinding(stencil, inputs, 0.0, 3);

        XenonSpatialCouplingResultV1 coupling = Require(
            XenonSpatialCouplingV1.TryApply(
                baseCoefficients,
                BaseDigest(baseCoefficients),
                stateBinding,
                inputs));

        XenonSpatialOverlayValueV1 firstOverlay = coupling.Overlays[0];
        SpatialNodeCoefficients effectiveFirst = coupling.Coefficients.Nodes[0];
        Assert.Equal(0.01 * firstOverlay.Xe135NumberDensity, firstOverlay.DynamicAbsorptionGroup1PerM, 12);
        Assert.Equal(0.02 * firstOverlay.Xe135NumberDensity, firstOverlay.DynamicAbsorptionGroup2PerM, 12);
        Assert.Equal(baseFirst.AbsorptionGroup1PerM + firstOverlay.DynamicAbsorptionGroup1PerM,
            effectiveFirst.AbsorptionGroup1PerM, 12);
        Assert.Equal(baseFirst.AbsorptionGroup2PerM + firstOverlay.DynamicAbsorptionGroup2PerM,
            effectiveFirst.AbsorptionGroup2PerM, 12);
        Assert.Equal(baseFirst.VolumeM3, effectiveFirst.VolumeM3);
        Assert.Equal(baseFirst.DownscatterGroup1To2PerM, effectiveFirst.DownscatterGroup1To2PerM);
        Assert.Equal(baseFirst.FissionGroup1PerM, effectiveFirst.FissionGroup1PerM);
        Assert.Equal(baseFirst.FissionGroup2PerM, effectiveFirst.FissionGroup2PerM);
        Assert.Equal(baseFirst.NuFissionGroup1PerM, effectiveFirst.NuFissionGroup1PerM);
        Assert.Equal(baseFirst.NuFissionGroup2PerM, effectiveFirst.NuFissionGroup2PerM);
        Assert.Equal(1.0, baseFirst.AbsorptionGroup1PerM, 12);
        Assert.Equal(0.5, baseFirst.AbsorptionGroup2PerM, 12);

        ContractValidationResult<SpatialOperator> operatorResult =
            SpatialOperator.TryCreate(stencil, coupling.Coefficients);
        Assert.True(operatorResult.IsValid, operatorResult.IsValid ? string.Empty : operatorResult.FirstDiagnostic.ToString());
        Assert.Equal(stencil.NodeCount, coupling.Overlays.Count);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<XenonSpatialOverlayValueV1>)coupling.Overlays)[0] = firstOverlay);

        XenonSpatialCouplingResultV1 reordered = Require(
            XenonSpatialCouplingV1.TryApply(
                baseCoefficients,
                BaseDigest(baseCoefficients),
                stateBinding,
                inputs.Reverse()));
        Assert.Equal(coupling.DynamicXenonDigest, reordered.DynamicXenonDigest);
        Assert.Equal(coupling.EffectiveCoefficientDigest, reordered.EffectiveCoefficientDigest);
    }

    [Fact]
    public void RejectsNonExcludedReferenceAndNodeBindingFailures()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        SpatialStencil stencil = Require(SpatialStencil.TryCreate(fixture.Topology));
        SpatialCoefficientSet baseCoefficients = CreateBaseCoefficients(stencil);
        NuclideDataV1 data = CreateNuclideData(0.01, 0.02);
        XenonSpatialNodeInputV1[] inputs = CreateInputs(fixture, stencil, data, 0.0);
        XenonSpatialStateBindingV1 stateBinding = CreateStateBinding(stencil, inputs, 0.0, 3);
        SpatialCoefficientSet includedCoefficients = CreateBaseCoefficients(stencil, XenonBasisV1.Included);
        SpatialCoefficientSet nonzeroReferenceCoefficients = CreateBaseCoefficients(
            stencil,
            XenonBasisV1.Excluded,
            1.0);

        ContractValidationResult<XenonSpatialCouplingResultV1> included = XenonSpatialCouplingV1.TryApply(
            includedCoefficients,
            BaseDigest(includedCoefficients),
            stateBinding,
            inputs);
        Assert.False(included.IsValid);
        Assert.Equal("XenonSpatial.Basis.Invalid", included.FirstDiagnostic.Code);

        ContractValidationResult<XenonSpatialCouplingResultV1> equilibrium = XenonSpatialCouplingV1.TryApply(
            CreateBaseCoefficients(stencil, XenonBasisV1.Equilibrium),
            BaseDigest(CreateBaseCoefficients(stencil, XenonBasisV1.Equilibrium)),
            stateBinding,
            inputs);
        Assert.False(equilibrium.IsValid);
        Assert.Equal("XenonSpatial.Basis.Invalid", equilibrium.FirstDiagnostic.Code);

        ContractValidationResult<XenonSpatialCouplingResultV1> nonzeroReference = XenonSpatialCouplingV1.TryApply(
            nonzeroReferenceCoefficients,
            BaseDigest(nonzeroReferenceCoefficients),
            stateBinding,
            inputs);
        Assert.False(nonzeroReference.IsValid);
        Assert.Equal("XenonSpatial.ReferenceXe.Invalid", nonzeroReference.FirstDiagnostic.Code);

        ContractValidationResult<SpatialCoefficientSet> negativeZeroReference = CreateBaseCoefficientResult(
            stencil,
            XenonBasisV1.Excluded,
            BitConverter.Int64BitsToDouble(long.MinValue));
        Assert.False(negativeZeroReference.IsValid);
        Assert.Equal("SpatialCoefficients.ReferenceXe.Invalid", negativeZeroReference.FirstDiagnostic.Code);

        ContractValidationResult<XenonSpatialCouplingResultV1> missing = XenonSpatialCouplingV1.TryApply(
            baseCoefficients,
            BaseDigest(baseCoefficients),
            stateBinding,
            inputs.Take(inputs.Length - 1));
        Assert.False(missing.IsValid);
        Assert.Equal("XenonSpatial.Nodes.CountMismatch", missing.FirstDiagnostic.Code);

        XenonSpatialNodeInputV1[] duplicateInputs = inputs
            .Take(inputs.Length - 1)
            .Concat(new[] { inputs[0] })
            .ToArray();
        ContractValidationResult<XenonSpatialCouplingResultV1> duplicate = XenonSpatialCouplingV1.TryApply(
            baseCoefficients,
            BaseDigest(baseCoefficients),
            stateBinding,
            duplicateInputs);
        Assert.False(duplicate.IsValid);
        Assert.Equal("XenonSpatial.Node.Duplicate", duplicate.FirstDiagnostic.Code);

        NuclideStateEnvelopeV1 movedState = Require(inputs[0].State.TryMoveToVolume(3.0));
        XenonSpatialNodeInputV1[] mismatchedVolume = inputs
            .Select(input => input.Node == stencil.Nodes[0].Node
                ? Require(XenonSpatialNodeInputV1.TryCreate(input.Node, movedState))
                : input)
            .ToArray();
        ContractValidationResult<XenonSpatialCouplingResultV1> volume = XenonSpatialCouplingV1.TryApply(
            baseCoefficients,
            BaseDigest(baseCoefficients),
            stateBinding,
            mismatchedVolume);
        Assert.False(volume.IsValid);
        Assert.Equal("XenonSpatial.NodeVolume.Mismatch", volume.FirstDiagnostic.Code);
    }

    [Fact]
    public void RejectsOverflowAndMissingDigestWithoutMutatingBaseCoefficients()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        SpatialStencil stencil = Require(SpatialStencil.TryCreate(fixture.Topology));
        SpatialCoefficientSet baseCoefficients = CreateBaseCoefficients(stencil);
        double beforeAbsorption = baseCoefficients.Nodes[0].AbsorptionGroup1PerM;
        NuclideDataV1 overflowData = CreateNuclideData(double.MaxValue, double.MaxValue);
        XenonSpatialNodeInputV1[] inputs = CreateInputs(fixture, stencil, CreateNuclideData(0.01, 0.02), 0.0);
        XenonSpatialStateBindingV1 stateBinding = CreateStateBinding(stencil, inputs, 0.0, 3);
        BundleState firstBundle = RequireBundle(fixture, stencil.Nodes[0].Node);
        NuclideStateEnvelopeV1 overflowState = Require(
            NuclideStateEnvelopeV1.TryCreateFresh(
                firstBundle.BundleId,
                0.0,
                1e308,
                2.0,
                0,
                overflowData));
        inputs[0] = Require(XenonSpatialNodeInputV1.TryCreate(inputs[0].Node, overflowState));

        ContractValidationResult<XenonSpatialCouplingResultV1> overflow = XenonSpatialCouplingV1.TryApply(
            baseCoefficients,
            BaseDigest(baseCoefficients),
            stateBinding,
            inputs);
        Assert.False(overflow.IsValid);
        Assert.Equal("XenonSpatial.DynamicAbsorption.Invalid", overflow.FirstDiagnostic.Code);
        Assert.Equal(beforeAbsorption, baseCoefficients.Nodes[0].AbsorptionGroup1PerM);

        ContractValidationResult<XenonSpatialCouplingResultV1> missingDigest = XenonSpatialCouplingV1.TryApply(
            baseCoefficients,
            null!,
            stateBinding,
            CreateInputs(fixture, stencil, CreateNuclideData(0.01, 0.02), 0.0));
        Assert.False(missingDigest.IsValid);
        Assert.Equal("XenonSpatial.BaseDigest.Missing", missingDigest.FirstDiagnostic.Code);

        ContractValidationResult<XenonSpatialCouplingResultV1> mismatchedDigest = XenonSpatialCouplingV1.TryApply(
            baseCoefficients,
            Digest(0x32),
            stateBinding,
            inputs);
        Assert.False(mismatchedDigest.IsValid);
        Assert.Equal("XenonSpatial.BaseDigest.Mismatch", mismatchedDigest.FirstDiagnostic.Code);
    }

    [Fact]
    public void SolvesAtExplicitCadenceAndRejectsStaleXenonDigestBinding()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        SpatialStencil stencil = Require(SpatialStencil.TryCreate(fixture.Topology));
        SpatialCoefficientSet baseCoefficients = CreateBaseCoefficients(stencil);
        NuclideDataV1 data = CreateNuclideData(0.01, 0.02);
        XenonSpatialNodeInputV1[] inputs = CreateInputs(fixture, stencil, data, 0.0);
        XenonSpatialStateBindingV1 stateBinding = CreateStateBinding(stencil, inputs, 0.0, 3);
        XenonSpatialCouplingResultV1 coupling = Require(XenonSpatialCouplingV1.TryApply(
            baseCoefficients,
            BaseDigest(baseCoefficients),
            stateBinding,
            inputs));
        SpatialRecomputeCadenceV1 cadence = Require(SpatialRecomputeCadenceV1.TryCreate(0.0, 1.0, 0));
        SpatialLinearSolvePolicy linearPolicy = Require(SpatialLinearSolvePolicy.TryCreate(
            SpatialLinearSolvePolicy.DeterministicJacobiMethodId,
            SpatialLinearSolvePolicy.DeterministicJacobiMethodVersion,
            1e-14,
            1e-14,
            512));
        SpatialConvergencePolicy convergencePolicy = Require(SpatialConvergencePolicy.TryCreate(
            0.01,
            0.01,
            1e-12,
            1e-12,
            1e-12,
            512));

        XenonSpatialSolveResultV1 solve = Require(XenonSpatialSolveV1.TryApply(
            coupling,
            stencil,
            cadence,
            0.0,
            linearPolicy,
            convergencePolicy,
            6.0,
            1.0));
        Assert.True(solve.IsConverged, solve.SpatialSolve.Diagnostics.FailureDiagnostic?.ToString());
        Assert.Equal(1UL, solve.NextCadence.EventIndex);
        Assert.True(Require(solve.TryValidateForPositiveDuration(stateBinding, coupling)));

        XenonSpatialCouplingResultV1 changedCoupling = Require(XenonSpatialCouplingV1.TryApply(
            baseCoefficients,
            BaseDigest(baseCoefficients),
            CreateStateBinding(stencil, CreateInputs(fixture, stencil, data, 0.5), 0.0, 3),
            CreateInputs(fixture, stencil, data, 0.5)));
        Assert.NotEqual(coupling.DynamicXenonDigest, changedCoupling.DynamicXenonDigest);
        ContractValidationResult<bool> stale = solve.TryValidateForPositiveDuration(
            changedCoupling.StateBinding,
            changedCoupling);
        Assert.False(stale.IsValid);
        Assert.Equal("XenonSpatialSolveBinding.Coupling.Stale", stale.FirstDiagnostic.Code);
        ContractValidationResult<bool> staleDigest = coupling.TryValidateSpatialSolveBinding(
            changedCoupling.DynamicXenonDigest,
            changedCoupling.EffectiveCoefficientDigest);
        Assert.False(staleDigest.IsValid);
        Assert.Equal("XenonSpatialSolveBinding.DynamicDigest.Stale", staleDigest.FirstDiagnostic.Code);

        XenonSpatialStateBindingV1 staleTimeBinding = CreateStateBinding(
            stencil,
            inputs,
            1.0,
            3);
        ContractValidationResult<bool> staleTime = solve.TryValidateForPositiveDuration(
            staleTimeBinding,
            coupling);
        Assert.False(staleTime.IsValid);
        Assert.Equal("XenonSpatialBinding.Time.Stale", staleTime.FirstDiagnostic.Code);

        XenonSpatialStateBindingV1 staleVersionBinding = CreateStateBinding(
            stencil,
            inputs,
            0.0,
            4);
        ContractValidationResult<bool> staleVersion = solve.TryValidateForPositiveDuration(
            staleVersionBinding,
            coupling);
        Assert.False(staleVersion.IsValid);
        Assert.Equal("XenonSpatialBinding.CoreStateVersion.Stale", staleVersion.FirstDiagnostic.Code);

        ContractValidationResult<XenonSpatialSolveResultV1> mismatchTime = XenonSpatialSolveV1.TryApply(
            coupling,
            stencil,
            Require(SpatialRecomputeCadenceV1.TryCreate(0.0, 1.0, 0)),
            1.0,
            linearPolicy,
            convergencePolicy,
            6.0,
            1.0);
        Assert.False(mismatchTime.IsValid);
        Assert.Equal("XenonSpatialSolve.Cadence.TimeMismatch", mismatchTime.FirstDiagnostic.Code);

        ContractValidationResult<XenonSpatialSolveResultV1> nonconverged = XenonSpatialSolveV1.TryApply(
            coupling,
            stencil,
            Require(SpatialRecomputeCadenceV1.TryCreate(0.0, 1.0, 0)),
            0.0,
            linearPolicy,
            Require(SpatialConvergencePolicy.TryCreate(0.01, 0.01, 1e-12, 1e-12, 1e-12, 1)),
            6.0,
            1.0);
        Assert.False(nonconverged.IsValid);
        Assert.Equal("XenonSpatialSolve.Solve.NotConverged", nonconverged.FirstDiagnostic.Code);
    }

    private static NuclideDataV1 CreateNuclideData(double sigmaGroup1, double sigmaGroup2)
    {
        return Require(NuclideDataV1.TryCreate(
            new MaterialVariantId("synthetic-fuel"),
            "synthetic-p7-xe-v1",
            Digest(0x44),
            0.5,
            0.25,
            0.1,
            0.05,
            sigmaGroup1,
            sigmaGroup2));
    }

    private static XenonSpatialNodeInputV1[] CreateInputs(
        SyntheticCoreFixture fixture,
        SpatialStencil stencil,
        NuclideDataV1 data,
        double xeOffset)
    {
        return stencil.Nodes
            .Select((node, index) =>
            {
                BundleState bundle = RequireBundle(fixture, node.Node);
                NuclideStateEnvelopeV1 state = Require(
                    NuclideStateEnvelopeV1.TryCreateFresh(
                        bundle.BundleId,
                        0.0,
                        2.0 + index + xeOffset,
                        2.0,
                        0UL,
                        data));
                return Require(XenonSpatialNodeInputV1.TryCreate(node.Node, state));
            })
            .ToArray();
    }

    private static SpatialCoefficientSet CreateBaseCoefficients(
        SpatialStencil stencil,
        XenonBasisV1 basis = XenonBasisV1.Excluded,
        double referenceXeNumberDensityM3 = 0.0)
    {
        return Require(CreateBaseCoefficientResult(stencil, basis, referenceXeNumberDensityM3));
    }

    private static ContractValidationResult<SpatialCoefficientSet> CreateBaseCoefficientResult(
        SpatialStencil stencil,
        XenonBasisV1 basis,
        double referenceXeNumberDensityM3)
    {
        SpatialNodeCoefficients[] nodes = stencil.Nodes
            .Select(node => new SpatialNodeCoefficients(
                node.Node,
                2.0,
                1.0,
                0.5,
                0.25,
                0.1,
                0.1,
                0.2,
                0.2,
                0.5,
                0.5,
                1.0))
            .Reverse()
            .ToArray();
        SpatialEdgeConductance[] edges = stencil.Nodes
            .SelectMany(node => node.NeighborTerms.Select(term => (node.Node, term.TargetNode)))
            .Select(pair => new
            {
                Key = new[] { pair.Node, pair.TargetNode }.OrderBy(node => node).ToArray()
            })
            .GroupBy(pair => string.Join("/", pair.Key.Select(node => node.ToString())), StringComparer.Ordinal)
            .Select(group => new SpatialEdgeConductance(group.First().Key[0], group.First().Key[1], 2.0, 2.0))
            .Reverse()
            .ToArray();
        SpatialBoundaryConductance[] boundaries = stencil.Nodes
            .SelectMany(node => node.BoundaryTerms.Select(term => new SpatialBoundaryConductance(
                node.Node,
                term.Face,
                0.0,
                0.0)))
            .Reverse()
            .ToArray();
        return SpatialCoefficientSet.TryCreateWithXenonBasis(
            stencil,
            nodes,
            edges,
            boundaries,
            basis,
            referenceXeNumberDensityM3);
    }

    private static BundleState RequireBundle(SyntheticCoreFixture fixture, NodeKey node)
    {
        Assert.True(fixture.Inventory.TryGet(node, out BundleState? bundle));
        Assert.NotNull(bundle);
        return bundle!;
    }

    private static Digest32 Digest(byte value)
    {
        return new Digest32(Enumerable.Repeat(value, 32).ToArray());
    }

    private static Digest32 BaseDigest(SpatialCoefficientSet coefficients)
    {
        return XenonSpatialCouplingV1.ComputeBaseCoefficientDigest(coefficients);
    }

    private static XenonSpatialStateBindingV1 CreateStateBinding(
        SpatialStencil stencil,
        IEnumerable<XenonSpatialNodeInputV1> inputs,
        double simulationTimeSeconds,
        ulong coreStateVersion)
    {
        XenonSpatialNuclideVersionV1[] versions = inputs
            .Select(input => Require(XenonSpatialNuclideVersionV1.TryCreate(
                input.Node,
                input.State.NuclideStateVersion)))
            .ToArray();
        _ = stencil;
        return Require(XenonSpatialStateBindingV1.TryCreate(
            simulationTimeSeconds,
            1.0,
            coreStateVersion,
            Digest(0x51),
            Digest(0x52),
            Digest(0x53),
            versions));
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }
}
