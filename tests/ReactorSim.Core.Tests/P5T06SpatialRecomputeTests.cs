using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P5T06SpatialRecomputeTests
{
    [Fact]
    public void RebindsBurnupCoefficientsAndAdvancesSpatialLifecycleAtomically()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        SpatialStencil stencil = CreateStencil(fixture);
        BundleInventory inventory = fixture.Inventory;
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture, inventory);
        SpatialRecomputeRequestV1 request = CreateRequest(
            fixture,
            stencil,
            inventory,
            lifecycle,
            CreateTable(fixture, secondRow: false),
            CreateCadence(0),
            0.0);

        ContractValidationResult<SpatialRecomputeResultV1> result =
            SpatialStateRecomputeV1.TryApply(request);

        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        SpatialRecomputeResultV1 applied = result.Value;
        Assert.True(applied.SpatialSolve.IsConverged);
        Assert.Equal(stencil.NodeCount, applied.LookupBindings.Count);
        Assert.All(applied.LookupBindings, binding =>
        {
            Assert.Equal(0, binding.Lookup.BracketLowerIndex);
            Assert.Equal(0, binding.Lookup.BracketUpperIndex);
            Assert.Equal(0.0, binding.Lookup.InterpolationFraction, 12);
        });
        Assert.Equal(stencil.NodeCount, applied.Coefficients.NodeCount);
        Assert.Equal(7, applied.Coefficients.EdgeCount);
        Assert.Equal(22, applied.Coefficients.BoundaryCount);
        Assert.Equal(201UL, applied.AcceptedLifecycle.SpatialStateVersion);
        Assert.Equal(301UL, applied.AcceptedLifecycle.PowerSnapshotVersion);
        Assert.Equal(BindingStatusV1.Valid, applied.AcceptedLifecycle.SpatialBindingStatus);
        Assert.Equal(BindingStatusV1.Valid, applied.AcceptedLifecycle.PowerBindingStatus);
        Assert.Equal(200UL, lifecycle.SpatialStateVersion);
        Assert.Equal(300UL, lifecycle.PowerSnapshotVersion);
        Assert.Equal(BindingStatusV1.Invalid, lifecycle.SpatialBindingStatus);
        Assert.Equal(1UL, applied.NextCadence.EventIndex);
        Assert.Equal(10.0, applied.NextCadence.TryGetNextEventTime().Value, 12);
        Assert.Equal(request.SpatialSolveId, applied.SpatialSolveId);
        Assert.Equal(request.PowerSnapshotId, applied.PowerSnapshotId);
    }

    [Fact]
    public void UsesExactBurnupBracketAndProducesRepeatableSpatialState()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        SpatialStencil stencil = CreateStencil(fixture);
        BundleInventory inventory = CreateBurnedInventory(fixture, 50_000_000.0);
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture, inventory);
        BurnupCoefficientTableV1 table = CreateTable(fixture, secondRow: true);

        SpatialRecomputeRequestV1 firstRequest = CreateRequest(
            fixture,
            stencil,
            inventory,
            lifecycle,
            table,
            CreateCadence(0),
            0.0);
        SpatialRecomputeRequestV1 reorderedRequest = CreateRequest(
            fixture,
            stencil,
            inventory,
            lifecycle,
            table,
            CreateCadence(0),
            0.0,
            reverseTables: true,
            reverseVolumes: true);

        ContractValidationResult<SpatialRecomputeResultV1> first =
            SpatialStateRecomputeV1.TryApply(firstRequest);
        ContractValidationResult<SpatialRecomputeResultV1> reordered =
            SpatialStateRecomputeV1.TryApply(reorderedRequest);

        Assert.True(first.IsValid, first.IsValid ? string.Empty : first.FirstDiagnostic.ToString());
        Assert.True(reordered.IsValid, reordered.IsValid ? string.Empty : reordered.FirstDiagnostic.ToString());
        Assert.All(first.Value.LookupBindings, binding =>
        {
            Assert.Equal(0, binding.Lookup.BracketLowerIndex);
            Assert.Equal(1, binding.Lookup.BracketUpperIndex);
            Assert.Equal(0.5, binding.Lookup.InterpolationFraction, 12);
            Assert.Equal(0.35, binding.Lookup.Coefficients.AbsorptionGroup1PerM, 12);
            Assert.Equal(0.25, binding.Lookup.Coefficients.AbsorptionGroup2PerM, 12);
        });
        Assert.Equal(
            first.Value.SpatialSolve.FinalState!.Group1Flux,
            reordered.Value.SpatialSolve.FinalState!.Group1Flux);
        Assert.Equal(
            first.Value.SpatialSolve.FinalState!.Group2Flux,
            reordered.Value.SpatialSolve.FinalState!.Group2Flux);
        Assert.Equal(
            first.Value.SpatialSolve.FinalState!.Eigenvalue,
            reordered.Value.SpatialSolve.FinalState!.Eigenvalue,
            12);
    }

    [Fact]
    public void RejectsCadenceMismatchAndMissingMaterialTableWithoutLifecycleMutation()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        SpatialStencil stencil = CreateStencil(fixture);
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture, fixture.Inventory);
        SpatialRecomputeRequestV1 cadenceRequest = CreateRequest(
            fixture,
            stencil,
            fixture.Inventory,
            lifecycle,
            CreateTable(fixture, secondRow: false),
            CreateCadence(0, 5.0),
            0.0);

        ContractValidationResult<SpatialRecomputeResultV1> cadenceResult =
            SpatialStateRecomputeV1.TryApply(cadenceRequest);

        Assert.False(cadenceResult.IsValid);
        Assert.Equal("SpatialRecompute.Cadence.TimeMismatch", cadenceResult.FirstDiagnostic.Code);
        Assert.Equal(200UL, lifecycle.SpatialStateVersion);
        Assert.Equal(BindingStatusV1.Invalid, lifecycle.SpatialBindingStatus);

        SpatialRecomputeRequestV1 missingTableRequest = CreateRequest(
            fixture,
            stencil,
            fixture.Inventory,
            lifecycle,
            CreateTable(fixture, secondRow: false, materialId: "other-material"),
            CreateCadence(0),
            0.0);
        ContractValidationResult<SpatialRecomputeResultV1> missingTableResult =
            SpatialStateRecomputeV1.TryApply(missingTableRequest);

        Assert.False(missingTableResult.IsValid);
        Assert.Equal("SpatialRecompute.Table.Missing", missingTableResult.FirstDiagnostic.Code);
        Assert.Equal(200UL, lifecycle.SpatialStateVersion);
        Assert.Equal(300UL, lifecycle.PowerSnapshotVersion);
        Assert.Equal(BindingStatusV1.Invalid, lifecycle.PowerBindingStatus);
    }

    [Fact]
    public void RejectsNonconvergedSolveBeforeLifecycleAcceptance()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        SpatialStencil stencil = CreateStencil(fixture);
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture, fixture.Inventory);
        SpatialRecomputeRequestV1 request = CreateRequest(
            fixture,
            stencil,
            fixture.Inventory,
            lifecycle,
            CreateTable(fixture, secondRow: false),
            CreateCadence(0),
            0.0,
            convergenceMaximumIterations: 1,
            initialGroup1Flux: Enumerable.Repeat(1.0, stencil.NodeCount).Select((value, index) => index == 0 ? 2.0 : value).ToArray());

        ContractValidationResult<SpatialRecomputeResultV1> result =
            SpatialStateRecomputeV1.TryApply(request);

        Assert.False(result.IsValid);
        Assert.Equal("SpatialEigenSolve.Nonconverged", result.FirstDiagnostic.Code);
        Assert.Equal(200UL, lifecycle.SpatialStateVersion);
        Assert.Equal(300UL, lifecycle.PowerSnapshotVersion);
        Assert.Equal(BindingStatusV1.Invalid, lifecycle.SpatialBindingStatus);
    }

    [Fact]
    public void RejectsForeignSameSizedTopologyAndFloatingPointCadenceNoProgress()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        SyntheticCoreFixture foreignFixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        SpatialStencil foreignStencil = CreateStencil(foreignFixture);
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture, fixture.Inventory);
        SpatialNodeVolumeV1[] volumes = foreignStencil.Nodes
            .Select(node => SpatialNodeVolumeV1.TryCreate(node.Node, 1.0).Value)
            .ToArray();
        ContractValidationResult<SpatialRecomputeCadenceV1> cadence =
            SpatialRecomputeCadenceV1.TryCreate(0.0, 10.0, 0);
        Assert.True(cadence.IsValid, cadence.IsValid ? string.Empty : cadence.FirstDiagnostic.ToString());
        ContractValidationResult<SpatialRecomputeRequestV1> request =
            SpatialRecomputeRequestV1.TryCreate(
                lifecycle,
                fixture.Inventory,
                CreateConductanceSource(foreignStencil),
                new[] { CreateTable(fixture, secondRow: false) },
                volumes,
                CreateLinearPolicy(),
                CreateConvergencePolicy(512),
                0.4,
                1.0,
                null,
                null,
                cadence.Value,
                0.0,
                fixture.DataPack.DataPackVersion,
                StableId.Parse("00000000-0000-0000-0000-0000000006b1"),
                StableId.Parse("00000000-0000-0000-0000-0000000006b2"),
                Digest(0x63),
                Digest(0x64));

        Assert.False(request.IsValid);
        Assert.Equal("SpatialRecompute.Topology.IdentityMismatch", request.FirstDiagnostic.Code);

        ContractValidationResult<SpatialRecomputeCadenceV1> noProgress =
            SpatialRecomputeCadenceV1.TryCreate(1e20, 1.0, 0);
        Assert.True(noProgress.IsValid, noProgress.IsValid ? string.Empty : noProgress.FirstDiagnostic.ToString());
        ContractValidationResult<SpatialRecomputeCadenceV1> advanced = noProgress.Value.TryAdvance();
        Assert.False(advanced.IsValid);
        Assert.Equal("SpatialRecompute.Cadence.NextTime.NoProgress", advanced.FirstDiagnostic.Code);
    }

    private static SpatialRecomputeRequestV1 CreateRequest(
        SyntheticCoreFixture fixture,
        SpatialStencil stencil,
        BundleInventory inventory,
        VersionLifecycleV1 lifecycle,
        BurnupCoefficientTableV1 table,
        SpatialRecomputeCadenceV1 cadence,
        double exactTime,
        bool reverseTables = false,
        bool reverseVolumes = false,
        int convergenceMaximumIterations = 512,
        double[]? initialGroup1Flux = null)
    {
        SpatialCoefficientSet conductances = CreateConductanceSource(stencil);
        SpatialLinearSolvePolicy linearPolicy = CreateLinearPolicy();
        SpatialConvergencePolicy convergencePolicy = CreateConvergencePolicy(convergenceMaximumIterations);
        SpatialNodeVolumeV1[] volumes = stencil.Nodes
            .Select(node => SpatialNodeVolumeV1.TryCreate(node.Node, 1.0).Value)
            .ToArray();
        if (reverseVolumes)
        {
            volumes = volumes.Reverse().ToArray();
        }

        BurnupCoefficientTableV1[] tables = { table };
        if (reverseTables)
        {
            tables = tables.Reverse().ToArray();
        }

        ContractValidationResult<SpatialRecomputeRequestV1> request =
            SpatialRecomputeRequestV1.TryCreate(
                lifecycle,
                inventory,
                conductances,
                tables,
                volumes,
                linearPolicy,
                convergencePolicy,
                0.4,
                1.0,
                initialGroup1Flux,
                initialGroup1Flux == null ? null : Enumerable.Repeat(1.0, stencil.NodeCount),
                cadence,
                exactTime,
                fixture.DataPack.DataPackVersion,
                StableId.Parse("00000000-0000-0000-0000-0000000006a1"),
                StableId.Parse("00000000-0000-0000-0000-0000000006a2"),
                Digest(0x61),
                Digest(0x62));
        Assert.True(request.IsValid, request.IsValid ? string.Empty : request.FirstDiagnostic.ToString());
        return request.Value;
    }

    private static SpatialRecomputeCadenceV1 CreateCadence(
        ulong eventIndex,
        double epochTimeSeconds = 0.0)
    {
        ContractValidationResult<SpatialRecomputeCadenceV1> result =
            SpatialRecomputeCadenceV1.TryCreate(epochTimeSeconds, 10.0, eventIndex);
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private static SpatialStencil CreateStencil(SyntheticCoreFixture fixture)
    {
        ContractValidationResult<SpatialStencil> result = SpatialStencil.TryCreate(fixture.Topology);
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private static SpatialCoefficientSet CreateConductanceSource(SpatialStencil stencil)
    {
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
        var edges = new List<SpatialEdgeConductance>();
        var seenEdges = new HashSet<string>(StringComparer.Ordinal);
        foreach (SpatialNodeStencil node in stencil.Nodes)
        {
            foreach (SpatialNeighborTerm neighbor in node.NeighborTerms)
            {
                NodeKey first = node.Node.CompareTo(neighbor.TargetNode) <= 0
                    ? node.Node
                    : neighbor.TargetNode;
                NodeKey second = node.Node.CompareTo(neighbor.TargetNode) <= 0
                    ? neighbor.TargetNode
                    : node.Node;
                string key = first + "-" + second;
                if (seenEdges.Add(key))
                {
                    edges.Add(new SpatialEdgeConductance(first, second, 0.5, 0.5));
                }
            }
        }

        SpatialBoundaryConductance[] boundaries = stencil.Nodes
            .SelectMany(node => node.BoundaryTerms.Select(term => new SpatialBoundaryConductance(
                node.Node,
                term.Face,
                0.0,
                0.0)))
            .ToArray();
        ContractValidationResult<SpatialCoefficientSet> result = SpatialCoefficientSet.TryCreate(
            stencil,
            nodes,
            edges,
            boundaries);
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private static BurnupCoefficientTableV1 CreateTable(
        SyntheticCoreFixture fixture,
        bool secondRow,
        string materialId = "synthetic-fuel")
    {
        BurnupCoefficientValuesV1 firstValues = CreateValues(
            0.3,
            0.2,
            0.1,
            0.1,
            0.15,
            0.25,
            0.1,
            1.0,
            1.0);
        ContractValidationResult<BurnupCoefficientRowV1> firstRow =
            BurnupCoefficientRowV1.TryCreate(0.0, firstValues);
        Assert.True(firstRow.IsValid, firstRow.IsValid ? string.Empty : firstRow.FirstDiagnostic.ToString());

        BurnupCoefficientRowV1[] rows = { firstRow.Value };
        if (secondRow)
        {
            BurnupCoefficientValuesV1 secondValues = CreateValues(
                0.4,
                0.3,
                0.1,
                0.1,
                0.15,
                0.25,
                0.1,
                1.0,
                1.0);
            ContractValidationResult<BurnupCoefficientRowV1> second =
                BurnupCoefficientRowV1.TryCreate(100_000.0, secondValues);
            Assert.True(second.IsValid, second.IsValid ? string.Empty : second.FirstDiagnostic.ToString());
            rows = new[] { firstRow.Value, second.Value };
        }

        ContractValidationResult<BurnupCoefficientTableV1> table = BurnupCoefficientTableV1.TryCreate(
            StableId.Parse("00000000-0000-0000-0000-0000000060a1"),
            BurnupCoefficientTableV1.CurrentSchemaVersion,
            fixture.DataPack.DataPackVersion,
            new MaterialVariantId(materialId),
            fixture.DataPack.UnitsProfileId,
            "synthetic:P5-T06",
            Digest((byte)(secondRow ? 0x72 : 0x71)),
            rows);
        Assert.True(table.IsValid, table.IsValid ? string.Empty : table.FirstDiagnostic.ToString());
        return table.Value;
    }

    private static BurnupCoefficientValuesV1 CreateValues(
        double absorptionGroup1,
        double absorptionGroup2,
        double fissionGroup1,
        double fissionGroup2,
        double nuFissionGroup1,
        double nuFissionGroup2,
        double downscatter,
        double chiGroup1,
        double energyPerFission)
    {
        ContractValidationResult<BurnupCoefficientValuesV1> result = BurnupCoefficientValuesV1.TryCreate(
            absorptionGroup1,
            absorptionGroup2,
            fissionGroup1,
            fissionGroup2,
            nuFissionGroup1,
            nuFissionGroup2,
            downscatter,
            chiGroup1,
            energyPerFission);
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private static BundleInventory CreateBurnedInventory(
        SyntheticCoreFixture fixture,
        double cumulativeEnergyJ)
    {
        ContractValidationResult<BundleInventory> result = BundleInventory.TryCreate(
            fixture.Topology,
            fixture.Inventory.EnumerateOccupied().Select(bundle => bundle.WithEnergy(cumulativeEnergyJ)));
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private static VersionLifecycleV1 CreateLifecycle(
        SyntheticCoreFixture fixture,
        BundleInventory inventory)
    {
        ContractValidationResult<VersionLifecycleV1> result = VersionLifecycleV1.TryCreate(
            fixture.Configuration,
            inventory,
            inventory.EnumerateOccupied()
                .Select(bundle => new BundleNuclideVersionV1(bundle.BundleId, 0, 0)),
            Digest(0x31));
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private static SpatialLinearSolvePolicy CreateLinearPolicy()
    {
        ContractValidationResult<SpatialLinearSolvePolicy> result = SpatialLinearSolvePolicy.TryCreate(
            SpatialLinearSolvePolicy.DeterministicJacobiMethodId,
            SpatialLinearSolvePolicy.DeterministicJacobiMethodVersion,
            1e-14,
            1e-14,
            512);
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private static SpatialConvergencePolicy CreateConvergencePolicy(int maximumIterations)
    {
        ContractValidationResult<SpatialConvergencePolicy> result = SpatialConvergencePolicy.TryCreate(
            0.01,
            0.01,
            1e-12,
            1e-12,
            1e-12,
            maximumIterations);
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private static Digest32 Digest(byte value)
    {
        return new Digest32(Enumerable.Repeat(value, 32).ToArray());
    }
}
