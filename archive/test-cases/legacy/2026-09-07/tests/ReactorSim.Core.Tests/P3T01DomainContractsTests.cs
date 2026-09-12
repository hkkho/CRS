using System;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P3T01DomainContractsTests
{
    [Fact]
    public void SyntheticFixtureUsesExplicitCanonicalTopologyAndFlatInventory()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();

        Assert.Equal((uint)2, fixture.Topology.ChannelCount);
        Assert.Equal((uint)3, fixture.Topology.BundlePositionCount);
        Assert.Equal(6, fixture.Topology.SlotCount);
        Assert.Equal(6, fixture.Inventory.OccupiedCount);
        Assert.Equal(new NodeKey(new ChannelId(1), new BundlePosition(2)),
            fixture.Inventory.EnumerateOccupied().Last().Node);
        Assert.Equal("00000000-0000-0000-0000-000000000201",
            fixture.Inventory.Get(new NodeKey(new ChannelId(0), new BundlePosition(0)))!.BundleId.ToString());
        Assert.Equal(100UL, fixture.Configuration.InitialCoreStateVersion);
    }

    [Fact]
    public void TopologyRejectsMissingCardinalFaceWithStableFirstDiagnostic()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        ChannelTopology original = fixture.Topology.GetChannel(new ChannelId(0));
        var alteredBoundaries = original.BoundaryFaces
            .Where(face => !(face.Position == new BundlePosition(1) && face.Face == TopologyFace.North))
            .ToArray();
        var altered = new[]
        {
            new ChannelTopology(
                original.ChannelId,
                original.CoordinateX,
                original.CoordinateY,
                original.FlowDirection,
                original.InletPosition,
                original.OutletPosition,
                original.Neighbors,
                alteredBoundaries),
            fixture.Topology.GetChannel(new ChannelId(1))
        };

        ContractValidationResult<CoreTopology> result = CoreTopology.TryCreate(2, 3, altered.Reverse());

        Assert.False(result.IsValid);
        Assert.Equal("Topology.FaceCoverage.Invalid", result.FirstDiagnostic.Code);
    }

    [Fact]
    public void TopologyRejectsNonReciprocalWithinChannelRelation()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        ChannelTopology original = fixture.Topology.GetChannel(new ChannelId(0));
        var neighbors = original.Neighbors
            .Where(neighbor => !(neighbor.SourcePosition == new BundlePosition(1) &&
                                 neighbor.Direction == NeighborDirection.TowardEndA))
            .ToArray();
        var altered = new[]
        {
            new ChannelTopology(
                original.ChannelId,
                original.CoordinateX,
                original.CoordinateY,
                original.FlowDirection,
                original.InletPosition,
                original.OutletPosition,
                neighbors,
                original.BoundaryFaces),
            fixture.Topology.GetChannel(new ChannelId(1))
        };

        ContractValidationResult<CoreTopology> result = CoreTopology.TryCreate(2, 3, altered);

        Assert.False(result.IsValid);
        Assert.Equal("Topology.WithinChannel.PairMissing", result.FirstDiagnostic.Code);
    }

    [Fact]
    public void TopologyRejectsNullNestedRecordsWithDiagnostics()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        ChannelTopology original = fixture.Topology.GetChannel(new ChannelId(0));
        var nullNeighborChannel = new ChannelTopology(
            original.ChannelId,
            original.CoordinateX,
            original.CoordinateY,
            original.FlowDirection,
            original.InletPosition,
            original.OutletPosition,
            original.Neighbors.Concat(new NeighborRecord[] { null! }),
            original.BoundaryFaces);
        var nullBoundaryChannel = new ChannelTopology(
            original.ChannelId,
            original.CoordinateX,
            original.CoordinateY,
            original.FlowDirection,
            original.InletPosition,
            original.OutletPosition,
            original.Neighbors,
            original.BoundaryFaces.Concat(new BoundaryFaceRecord[] { null! }));

        ContractValidationResult<CoreTopology> neighborResult = CoreTopology.TryCreate(
            2,
            3,
            new[] { nullNeighborChannel, fixture.Topology.GetChannel(new ChannelId(1)) });
        ContractValidationResult<CoreTopology> boundaryResult = CoreTopology.TryCreate(
            2,
            3,
            new[] { nullBoundaryChannel, fixture.Topology.GetChannel(new ChannelId(1)) });

        Assert.False(neighborResult.IsValid);
        Assert.Equal("Topology.Neighbor.Null", neighborResult.FirstDiagnostic.Code);
        Assert.False(boundaryResult.IsValid);
        Assert.Equal("Topology.Boundary.Null", boundaryResult.FirstDiagnostic.Code);
    }

    [Fact]
    public void CoordinateValidationDoesNotWrapAtIntBounds()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        ChannelTopology first = fixture.Topology.GetChannel(new ChannelId(0));
        ChannelTopology second = fixture.Topology.GetChannel(new ChannelId(1));
        var altered = new[]
        {
            new ChannelTopology(
                first.ChannelId,
                int.MaxValue,
                first.CoordinateY,
                first.FlowDirection,
                first.InletPosition,
                first.OutletPosition,
                first.Neighbors,
                first.BoundaryFaces),
            new ChannelTopology(
                second.ChannelId,
                int.MinValue,
                second.CoordinateY,
                second.FlowDirection,
                second.InletPosition,
                second.OutletPosition,
                second.Neighbors,
                second.BoundaryFaces)
        };

        ContractValidationResult<CoreTopology> result = CoreTopology.TryCreate(2, 3, altered);

        Assert.False(result.IsValid);
        Assert.Equal("Topology.Neighbor.CoordinateMismatch", result.FirstDiagnostic.Code);
    }

    [Fact]
    public void BoundaryFirstDiagnosticIsIndependentOfInputOrder()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        ChannelTopology original = fixture.Topology.GetChannel(new ChannelId(0));
        BoundaryFaceRecord[] baseBoundaries = original.BoundaryFaces
            .Where(face => !(face.Position == new BundlePosition(0) && face.Face == TopologyFace.North))
            .ToArray();
        var valid = new BoundaryFaceRecord(
            original.ChannelId,
            new BundlePosition(0),
            TopologyFace.North,
            BoundaryClassification.Reflective);
        var invalid = new BoundaryFaceRecord(
            original.ChannelId,
            new BundlePosition(0),
            TopologyFace.North,
            (BoundaryClassification)99);

        ContractValidationResult<CoreTopology>[] candidates =
        {
            BuildTopologyWithBoundaries(fixture, baseBoundaries.Concat(new[] { valid, invalid }).ToArray()),
            BuildTopologyWithBoundaries(fixture, baseBoundaries.Concat(new[] { invalid, valid }).ToArray())
        };

        Assert.All(candidates, candidate =>
        {
            Assert.False(candidate.IsValid);
            Assert.Equal("Topology.Boundary.Label.Invalid", candidate.FirstDiagnostic.Code);
        });
    }

    [Fact]
    public void InventoryRejectsDuplicateIdentityAndDuplicateLocation()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        BundleState first = fixture.Inventory.Get(new NodeKey(new ChannelId(0), new BundlePosition(0)))!;
        BundleState second = fixture.Inventory.Get(new NodeKey(new ChannelId(0), new BundlePosition(1)))!;

        ContractValidationResult<BundleInventory> duplicateId =
            BundleInventory.TryCreate(fixture.Topology, new[]
            {
                first,
                new BundleState(
                    first.BundleId,
                    second.ChannelId,
                    second.Position,
                    second.MaterialVariantId,
                    second.InitialBurnupJPerKgHm,
                    second.CumulativeFissionEnergyJ,
                    second.HeavyMetalMassKg,
                    second.InsertedAtSeconds)
            });
        Assert.False(duplicateId.IsValid);
        Assert.Equal("BundleInventory.BundleId.Duplicate", duplicateId.FirstDiagnostic.Code);

        ContractValidationResult<BundleInventory> duplicateLocation =
            BundleInventory.TryCreate(fixture.Topology, new[]
            {
                first,
                new BundleState(
                    StableId.Parse("00000000-0000-0000-0000-0000000002ff"),
                    first.ChannelId,
                    first.Position,
                    second.MaterialVariantId,
                    second.InitialBurnupJPerKgHm,
                    second.CumulativeFissionEnergyJ,
                    second.HeavyMetalMassKg,
                    second.InsertedAtSeconds)
            });
        Assert.False(duplicateLocation.IsValid);
        Assert.Equal("BundleInventory.Location.Duplicate", duplicateLocation.FirstDiagnostic.Code);
    }

    [Fact]
    public void DataPackAndConfigurationRejectDimensionMismatchAndPreserveDigests()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        byte[] originalDigest = fixture.DataPack.ContentDigest.ToArray();
        byte[] mutableInput = originalDigest.ToArray();
        mutableInput[0] ^= 0xff;

        ContractValidationResult<DataPackDescriptor> descriptor = DataPackDescriptor.TryCreate(
            DataPackDescriptor.CurrentSchemaVersion,
            StableId.Parse("00000000-0000-0000-0000-000000000301"),
            "synthetic-mismatch",
            "topology-v1",
            3,
            3,
            "SI-v1",
            fixture.DataPack.TopologyDigest.ToArray(),
            mutableInput);
        Assert.True(descriptor.IsValid);

        ContractValidationResult<SimulationConfiguration> configuration =
            SimulationConfiguration.TryCreate(
                SimulationConfiguration.CurrentSchemaVersion,
                fixture.Topology,
                descriptor.Value,
                0.0,
                1,
                2,
                3);
        Assert.False(configuration.IsValid);
        Assert.Equal("DataPack.Topology.DimensionMismatch", configuration.FirstDiagnostic.Code);
        Assert.Equal(originalDigest, fixture.DataPack.ContentDigest.ToArray());
    }

    [Fact]
    public void StableIdUsesCanonicalNetworkOrderAndDoesNotAcceptAmbiguousText()
    {
        StableId identifier = StableId.Parse("00112233-4455-6677-8899-aabbccddeeff");

        Assert.Equal("00112233-4455-6677-8899-aabbccddeeff", identifier.ToString());
        Assert.Equal(
            new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xaa, 0xbb, 0xcc, 0xdd, 0xee, 0xff },
            identifier.ToCanonicalBytes());
        Assert.False(StableId.TryParse("00112233445566778899aabbccddeeff", out _));
        Assert.False(StableId.TryParse("00112233-4455-6677-8899-AABBCCDDEEFF", out _));
    }

    private static ContractValidationResult<CoreTopology> BuildTopologyWithBoundaries(
        SyntheticCoreFixture fixture,
        BoundaryFaceRecord[] boundaries)
    {
        ChannelTopology original = fixture.Topology.GetChannel(new ChannelId(0));
        var channel = new ChannelTopology(
            original.ChannelId,
            original.CoordinateX,
            original.CoordinateY,
            original.FlowDirection,
            original.InletPosition,
            original.OutletPosition,
            original.Neighbors,
            boundaries);
        return CoreTopology.TryCreate(2, 3, new[] { channel, fixture.Topology.GetChannel(new ChannelId(1)) });
    }
}
