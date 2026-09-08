using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class TopologyInventoryRefuellingTests
{
    private const uint PositionCount = 12;

    // CORE-REFUEL-005 is owned by GameSession: Core exposes no preview
    // transition, and the vertical Game suite covers preview non-mutation.

    [Fact]
    public void CanonicalTopologyHasStableFlatteningAndExplicitAdjacency()
    {
        CoreTopology topology = Candu6CoreTopologyFactoryV1.Create();

        Assert.Equal(380u, topology.ChannelCount);
        Assert.Equal(PositionCount, topology.BundlePositionCount);
        Assert.Equal(4560, topology.SlotCount);

        NodeKey[] nodes = topology.EnumerateNodes().ToArray();
        Assert.Equal(topology.SlotCount, nodes.Length);
        Assert.Equal(nodes.Length, nodes.Distinct().Count());
        Assert.Equal(Enumerable.Range(0, topology.SlotCount), nodes.Select(topology.GetFlatIndex));

        for (uint channelIndex = 0; channelIndex < topology.ChannelCount; channelIndex++)
        {
            Candu6GridPositionV1 grid = Candu6CoreTopologyFactoryV1.GetPosition(channelIndex);
            ChannelTopology channel = topology.GetChannel(new ChannelId(channelIndex));

            Assert.Equal(grid.Column, channel.CoordinateX);
            Assert.Equal(grid.CartesianY, channel.CoordinateY);
            Assert.InRange(grid.Column, 0, Candu6CoreTopologyFactoryV1.GridWidth - 1);
            Assert.InRange(grid.DisplayRow, 0, Candu6CoreTopologyFactoryV1.GridHeight - 1);
            Assert.InRange(grid.CartesianY, 0, Candu6CoreTopologyFactoryV1.GridHeight - 1);
            Assert.True(
                Candu6CoreTopologyFactoryV1.TryGetChannelIndex(
                    grid.Column,
                    grid.DisplayRow,
                    out uint roundTripChannel));
            Assert.Equal(channelIndex, roundTripChannel);
            Assert.Equal(
                Candu6CoreTopologyFactoryV1.GetFlowDirection(grid),
                channel.FlowDirection);

            BundlePosition expectedInlet = channel.FlowDirection == FlowDirection.EndAtoEndB
                ? new BundlePosition(0)
                : new BundlePosition(PositionCount - 1);
            BundlePosition expectedOutlet = channel.FlowDirection == FlowDirection.EndAtoEndB
                ? new BundlePosition(PositionCount - 1)
                : new BundlePosition(0);
            Assert.Equal(expectedInlet, channel.InletPosition);
            Assert.Equal(expectedOutlet, channel.OutletPosition);

            for (uint position = 0; position < PositionCount; position++)
            {
                NodeKey node = new(new ChannelId(channelIndex), new BundlePosition(position));
                int flatIndex = topology.GetFlatIndex(node);
                Assert.Equal(node, nodes[flatIndex]);
                Assert.Equal(
                    checked((int)(channelIndex * PositionCount + position)),
                    flatIndex);

                NeighborRecord[] axial = channel.Neighbors
                    .Where(neighbor => neighbor.SourcePosition.Value == position &&
                                       (neighbor.Direction == NeighborDirection.TowardEndA ||
                                        neighbor.Direction == NeighborDirection.TowardEndB))
                    .ToArray();
                Assert.Equal(position == 0 || position == PositionCount - 1 ? 1 : 2, axial.Length);

                if (position > 0)
                {
                    NeighborRecord towardEndA = Assert.Single(
                        axial,
                        neighbor => neighbor.Direction == NeighborDirection.TowardEndA);
                    Assert.Equal(position - 1, towardEndA.TargetPosition.Value);
                    Assert.Equal(channelIndex, towardEndA.TargetChannelId.Value);
                }
                else
                {
                    Assert.DoesNotContain(axial, neighbor => neighbor.Direction == NeighborDirection.TowardEndA);
                    Assert.Contains(channel.BoundaryFaces, boundary =>
                        boundary.Position.Value == 0 && boundary.Face == TopologyFace.EndA);
                }

                if (position + 1 < PositionCount)
                {
                    NeighborRecord towardEndB = Assert.Single(
                        axial,
                        neighbor => neighbor.Direction == NeighborDirection.TowardEndB);
                    Assert.Equal(position + 1, towardEndB.TargetPosition.Value);
                    Assert.Equal(channelIndex, towardEndB.TargetChannelId.Value);
                }
                else
                {
                    Assert.DoesNotContain(axial, neighbor => neighbor.Direction == NeighborDirection.TowardEndB);
                    Assert.Contains(channel.BoundaryFaces, boundary =>
                        boundary.Position.Value == PositionCount - 1 && boundary.Face == TopologyFace.EndB);
                }

                foreach (NeighborDirection direction in new[]
                {
                    NeighborDirection.North,
                    NeighborDirection.East,
                    NeighborDirection.South,
                    NeighborDirection.West
                })
                {
                    int faceCoverage = channel.Neighbors.Count(neighbor =>
                        neighbor.SourcePosition.Value == position && neighbor.Direction == direction) +
                        channel.BoundaryFaces.Count(boundary =>
                            boundary.Position.Value == position &&
                            boundary.Face == (TopologyFace)(byte)direction);
                    Assert.Equal(1, faceCoverage);
                }
            }
        }
    }

    [Fact]
    public void ManufacturedInventoryKeepsLiveIdentityAndLocationSetsUnique()
    {
        CoreTopology topology = CreateManufacturedTopology();
        BundleState[] bundles =
        {
            CreateBundle(1, 0, 0, "fresh-fuel", 0.0, 0.0),
            CreateBundle(2, 0, 1, "resident-fuel", 1200.0, 2400.0),
            CreateBundle(3, 1, 0, "resident-fuel", 900.0, 1800.0)
        };

        ContractValidationResult<BundleInventory> result = BundleInventory.TryCreate(topology, bundles);
        AssertValid(result);
        BundleInventory inventory = result.Value;

        BundleState[] live = inventory.EnumerateOccupied().ToArray();
        Assert.Equal(bundles.Length, live.Length);
        Assert.Equal(live.Length, live.Select(bundle => bundle.BundleId).Distinct().Count());
        Assert.Equal(live.Length, live.Select(bundle => bundle.Node).Distinct().Count());
        Assert.All(live, bundle => Assert.False(string.IsNullOrWhiteSpace(bundle.MaterialVariantId.Value)));
        Assert.Equal("fresh-fuel", inventory.Get(Node(0, 0))!.MaterialVariantId.Value);
        Assert.Equal("resident-fuel", inventory.Get(Node(0, 1))!.MaterialVariantId.Value);

        ContractValidationResult<BundleInventory> duplicateId = BundleInventory.TryCreate(
            topology,
            bundles.Concat(new[] { CreateBundle(1, 1, 1, "resident-fuel", 100.0, 0.0) }));
        AssertInvalid(duplicateId, "BundleInventory.BundleId.Duplicate");

        ContractValidationResult<BundleInventory> duplicateLocation = BundleInventory.TryCreate(
            topology,
            bundles.Concat(new[] { CreateBundle(4, 0, 0, "resident-fuel", 100.0, 0.0) }));
        AssertInvalid(duplicateLocation, "BundleInventory.Location.Duplicate");
    }

    [Theory]
    [InlineData(0u, (ushort)4, (byte)1)]
    [InlineData(1u, (ushort)4, (byte)0)]
    [InlineData(0u, (ushort)8, (byte)1)]
    [InlineData(1u, (ushort)8, (byte)0)]
    public void ValidShiftsMoveStableIdentitiesAndConserveOwnership(
        uint channelIndex,
        ushort shiftCount,
        byte directionValue)
    {
        RefuelShiftDirection direction = (RefuelShiftDirection)directionValue;
        CoreTopology topology = CreateManufacturedTopology();
        BundleInventory inventory = CreateFullInventory(topology);
        ChannelTopology channel = topology.GetChannel(new ChannelId(channelIndex));
        RefuelSchemePositionPlan plan = CreatePlan(shiftCount, channel.FlowDirection);
        Assert.Equal(direction, plan.ShiftDirection);
        BundleState[] inserted = CreateInsertedBundles(
            channelIndex,
            plan,
            10000 + checked((int)(channelIndex * 1000 + shiftCount * 10 + directionValue * 100)),
            200.0);

        ContractValidationResult<RefuelShiftResult> result = RefuelShiftTransition.TryApply(
            inventory,
            new ChannelId(channelIndex),
            plan,
            inserted,
            100.0,
            200.0);
        AssertValid(result);

        RefuelShiftResult shift = result.Value;
        BundleInventory resulting = shift.ResultingInventory;
        Assert.Same(inventory, shift.SourceInventory);
        Assert.Equal(inventory.OccupiedCount, resulting.OccupiedCount);
        Assert.Equal(
            resulting.OccupiedCount,
            resulting.EnumerateOccupied().Select(bundle => bundle.BundleId).Distinct().Count());
        Assert.Equal(
            plan.DischargedPositions.Select(position => inventory.Get(new NodeKey(
                new ChannelId(channelIndex), position))!.BundleId),
            shift.DischargedBundles.Select(bundle => bundle.BundleId));

        for (int index = 0; index < inserted.Length; index++)
        {
            BundleState actual = resulting.Get(new NodeKey(
                new ChannelId(channelIndex),
                plan.InsertedPositions[index]))!;
            Assert.Equal(inserted[index].BundleId, actual.BundleId);
            Assert.Equal(0.0, actual.CurrentBurnupJPerKgHm);
            Assert.Equal(0.0, actual.CumulativeFissionEnergyJ);
            Assert.Equal(200.0, actual.InsertedAtSeconds);
        }

        HashSet<BundlePosition> insertedPositions = plan.InsertedPositions.ToHashSet();
        HashSet<BundlePosition> dischargedPositions = plan.DischargedPositions.ToHashSet();
        for (uint oldPosition = 0; oldPosition < PositionCount; oldPosition++)
        {
            BundlePosition old = new(oldPosition);
            if (insertedPositions.Contains(old) || dischargedPositions.Contains(old))
            {
                continue;
            }

            BundleState before = inventory.Get(Node(channelIndex, oldPosition))!;
            uint newPosition = direction == RefuelShiftDirection.TowardEndB
                ? oldPosition + shiftCount
                : oldPosition - shiftCount;
            BundleState after = resulting.Get(Node(channelIndex, newPosition))!;
            Assert.Equal(before.BundleId, after.BundleId);
            Assert.Equal(before.CurrentBurnupJPerKgHm, after.CurrentBurnupJPerKgHm);
            Assert.Equal(before.MaterialVariantId, after.MaterialVariantId);
            Assert.NotSame(before, after);
        }

        for (uint position = 0; position < PositionCount; position++)
        {
            Assert.Same(
                inventory.Get(Node(channelIndex == 0 ? 1u : 0u, position)),
                resulting.Get(Node(channelIndex == 0 ? 1u : 0u, position)));
        }

        StableId[] expectedOwnership = inventory.EnumerateOccupied()
            .Select(bundle => bundle.BundleId)
            .Concat(inserted.Select(bundle => bundle.BundleId))
            .OrderBy(id => id)
            .ToArray();
        StableId[] actualOwnership = resulting.EnumerateOccupied()
            .Select(bundle => bundle.BundleId)
            .Concat(shift.DischargedBundles.Select(bundle => bundle.BundleId))
            .OrderBy(id => id)
            .ToArray();
        Assert.Equal(expectedOwnership, actualOwnership);
        Assert.Empty(resulting.EnumerateOccupied()
            .Select(bundle => bundle.BundleId)
            .Intersect(shift.DischargedBundles.Select(bundle => bundle.BundleId)));
    }

    [Fact]
    public void InvalidCoreRequestsUseStableDiagnosticsAndLeaveSourcesUntouched()
    {
        CoreTopology topology = CreateManufacturedTopology();
        BundleInventory inventory = CreateFullInventory(topology);
        RefuelSchemePositionPlan validPlan = CreatePlan(4, FlowDirection.EndAtoEndB);
        BundleState[] inserted = CreateInsertedBundles(0, validPlan, 12000, 300.0);
        StableId originalBundleId = inventory.Get(Node(0, 0))!.BundleId;

        AssertInvalid(
            RefuelShiftTransition.TryApply(
                inventory,
                new ChannelId(2),
                validPlan,
                inserted,
                300.0,
                300.0),
            "RefuelShift.Channel.OutOfRange");

        RefuelSchemePositionPlan oppositePlan = CreatePlan(4, FlowDirection.EndBtoEndA);
        AssertInvalid(
            RefuelShiftTransition.TryApply(
                inventory,
                new ChannelId(0),
                oppositePlan,
                CreateInsertedBundles(0, oppositePlan, 12100, 300.0),
                300.0,
                300.0),
            "RefuelShift.FlowDirection.Mismatch");

        ContractValidationResult<RefuelSchemeDefinition> unsupportedScheme =
            RefuelSchemeDefinition.TryCreate(
                "S6",
                6,
                "fresh-fuel",
                RefuelSchemeDefinition.CurrentSchemaVersion,
                Array.Empty<BundlePosition>(),
                Array.Empty<BundlePosition>());
        AssertInvalid(unsupportedScheme, "RefuelScheme.ShiftCount.Unsupported");

        BundleState[] partialBundles = inventory.EnumerateOccupied()
            .Where(bundle => bundle.Node != Node(0, PositionCount - 1))
            .ToArray();
        ContractValidationResult<BundleInventory> partialInventory = BundleInventory.TryCreate(
            topology,
            partialBundles);
        AssertValid(partialInventory);
        AssertInvalid(
            RefuelShiftTransition.TryApply(
                partialInventory.Value,
                new ChannelId(0),
                validPlan,
                inserted,
                300.0,
                300.0),
            "RefuelShift.Channel.NotFullyOccupied");

        Assert.Equal(originalBundleId, inventory.Get(Node(0, 0))!.BundleId);
        Assert.Equal(24, inventory.OccupiedCount);
    }

    [Fact]
    public void PracticeRefuellingRejectsInvalidRequestsAndInsufficientFreshFuelAtomically()
    {
        SyntheticGameCoreStateV1 state = SyntheticGameCoreStateV1.CreatePractice();
        StableId originalBundleId = state.GetBundle(0, 0).BundleId;

        AssertInvalid(
            state.TryRefuel(380, GameRefuellingDirectionV1.TowardEndB, 4, "NAT-U-SYNTHETIC", 0.0),
            "GameRefuelling.Channel.OutOfRange");
        AssertInvalid(
            state.TryRefuel(0, (GameRefuellingDirectionV1)99, 4, "NAT-U-SYNTHETIC", 0.0),
            "GameRefuelling.Direction.Invalid");
        AssertInvalid(
            state.TryRefuel(0, GameRefuellingDirectionV1.TowardEndB, 6, "NAT-U-SYNTHETIC", 0.0),
            "GameRefuelling.ShiftCount.Unsupported");
        AssertInvalid(
            state.TryRefuel(0, GameRefuellingDirectionV1.TowardEndB, 4, string.Empty, 0.0),
            "GameRefuelling.FuelType.Missing");

        SyntheticGameCoreStateV1 depleted = state;
        for (int operation = 0; operation < 16; operation++)
        {
            ContractValidationResult<GameRefuellingResultV1> accepted = depleted.TryRefuel(
                (uint)operation,
                GameRefuellingDirectionV1.TowardEndB,
                8,
                "NAT-U-SYNTHETIC",
                0.0);
            AssertValid(accepted);
            depleted = accepted.Value.ResultingState;
        }

        Assert.Equal(0u, depleted.FreshBundlesAvailable);
        AssertInvalid(
            depleted.TryRefuel(
                0,
                GameRefuellingDirectionV1.TowardEndB,
                4,
                "NAT-U-SYNTHETIC",
                0.0),
            "GameRefuelling.FreshInventory.Insufficient");
        Assert.Equal(128u, state.FreshBundlesAvailable);
        Assert.Equal(0u, state.RefuellingOperationCount);
        Assert.Equal(0u, depleted.FreshBundlesAvailable);
        Assert.Equal(originalBundleId, state.GetBundle(0, 0).BundleId);
        Assert.Equal(16u, depleted.RefuellingOperationCount);
    }

    [Fact]
    public void StalePreparedRefuellingRequestFailsBeforeMutation()
    {
        CoreTopology topology = CreateManufacturedTopology();
        BundleInventory sourceInventory = CreateSparseRefuelInventory(topology);
        VersionLifecycleV1 lifecycle = CreateLifecycle(topology, sourceInventory);
        RefuelSchemePositionPlan plan = CreatePlan(4, FlowDirection.EndAtoEndB);
        ContractValidationResult<RefuelShiftResult> shiftResult = RefuelShiftTransition.TryApply(
            sourceInventory,
            new ChannelId(0),
            plan,
            CreateInsertedBundles(0, plan, 13000, 0.0),
            0.0,
            0.0);
        AssertValid(shiftResult);

        ContractValidationResult<CompleteRefuellingRequestV1> requestResult =
            CompleteRefuellingRequestV1.TryCreate(shiftResult.Value, lifecycle);
        AssertValid(requestResult);
        CompleteRefuellingRequestV1 request = requestResult.Value;
        StateBindingV1 acceptedBinding = lifecycle.CreateStateBinding();
        BundleState[] sourceLocations = sourceInventory.EnumerateOccupied().ToArray();

        VersionLifecycleV1 staleLifecycle = CreateLifecycle(topology, sourceInventory, 4);
        ContractValidationResult<CompleteRefuellingTransitionResultV1> staleLifecycleResult =
            CompleteRefuellingTransitionV1.TryApply(
                request,
                sourceInventory,
                staleLifecycle,
                Array.Empty<SpatialNodeVolumeV1>(),
                Id(14000),
                Digest(9));
        AssertInvalid(staleLifecycleResult, "CompleteRefuelling.SourceLifecycleDigest.Stale");

        BundleState movedNonTarget = sourceInventory.Get(Node(1, 0))!;
        BundleState[] staleBundles = sourceInventory.EnumerateOccupied()
            .Select(bundle => bundle.BundleId == movedNonTarget.BundleId
                ? bundle.WithLocation(new ChannelId(1), new BundlePosition(1))
                : bundle)
            .ToArray();
        ContractValidationResult<BundleInventory> staleInventoryResult = BundleInventory.TryCreate(
            topology,
            staleBundles);
        AssertValid(staleInventoryResult);

        ContractValidationResult<CompleteRefuellingTransitionResultV1> rejected =
            CompleteRefuellingTransitionV1.TryApply(
                request,
                staleInventoryResult.Value,
                lifecycle,
                Array.Empty<SpatialNodeVolumeV1>(),
                Id(14000),
                Digest(9));

        AssertInvalid(rejected, "CompleteRefuelling.SourceInventoryDigest.Stale");
        Assert.Same(sourceInventory, request.Shift.SourceInventory);
        Assert.Equal(lifecycle.CoreStateVersion, acceptedBinding.CoreStateVersion);
        Assert.Equal(lifecycle.CreateStateBinding().CoreStateVersion, acceptedBinding.CoreStateVersion);
        Assert.Equal(
            sourceLocations.Select(bundle => bundle.BundleId),
            sourceInventory.EnumerateOccupied().Select(bundle => bundle.BundleId));
        Assert.Equal(
            sourceLocations.Select(bundle => bundle.Node),
            sourceInventory.EnumerateOccupied().Select(bundle => bundle.Node));
        Assert.Equal(new Digest32(DigestBytes(3)), lifecycle.StateDigest.Value);
        Assert.Equal(acceptedBinding.TopologyVersion, lifecycle.CreateStateBinding().TopologyVersion);
        Assert.Equal(acceptedBinding.DataPackVersion, lifecycle.CreateStateBinding().DataPackVersion);
        Assert.Equal(0u, lifecycle.CoreStateVersion);
    }

    private static CoreTopology CreateManufacturedTopology()
    {
        var channels = new[]
        {
            CreateManufacturedChannel(0, FlowDirection.EndAtoEndB, 0),
            CreateManufacturedChannel(1, FlowDirection.EndBtoEndA, 1)
        };
        ContractValidationResult<CoreTopology> result = CoreTopology.TryCreate(2, PositionCount, channels);
        AssertValid(result);
        return result.Value;
    }

    private static ChannelTopology CreateManufacturedChannel(
        uint channelIndex,
        FlowDirection flowDirection,
        int coordinateX)
    {
        ChannelId channelId = new(channelIndex);
        var neighbors = new List<NeighborRecord>();
        var boundaries = new List<BoundaryFaceRecord>();
        for (uint position = 0; position + 1 < PositionCount; position++)
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

        for (uint position = 0; position < PositionCount; position++)
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
            new BundlePosition(PositionCount - 1),
            TopologyFace.EndB,
            BoundaryClassification.Reflective));

        return new ChannelTopology(
            channelId,
            coordinateX,
            0,
            flowDirection,
            flowDirection == FlowDirection.EndAtoEndB
                ? new BundlePosition(0)
                : new BundlePosition(PositionCount - 1),
            flowDirection == FlowDirection.EndAtoEndB
                ? new BundlePosition(PositionCount - 1)
                : new BundlePosition(0),
            neighbors,
            boundaries);
    }

    private static BundleInventory CreateFullInventory(CoreTopology topology)
    {
        var bundles = new List<BundleState>();
        for (uint channel = 0; channel < topology.ChannelCount; channel++)
        {
            for (uint position = 0; position < topology.BundlePositionCount; position++)
            {
                int number = checked(100 + (int)(channel * 100 + position));
                bundles.Add(CreateBundle(
                    number,
                    channel,
                    position,
                    "resident-fuel",
                    1000.0 + number,
                    10000.0 + number));
            }
        }

        ContractValidationResult<BundleInventory> result = BundleInventory.TryCreate(topology, bundles);
        AssertValid(result);
        return result.Value;
    }

    private static BundleInventory CreateSparseRefuelInventory(CoreTopology topology)
    {
        var bundles = new List<BundleState>();
        for (uint position = 0; position < PositionCount; position++)
        {
            bundles.Add(CreateBundle(
                checked(100 + (int)position),
                0,
                position,
                "resident-fuel",
                1000.0 + position,
                10000.0 + position));
        }

        bundles.Add(CreateBundle(200, 1, 0, "resident-fuel", 1200.0, 12000.0));
        ContractValidationResult<BundleInventory> result = BundleInventory.TryCreate(topology, bundles);
        AssertValid(result);
        return result.Value;
    }

    private static RefuelSchemePositionPlan CreatePlan(
        ushort shiftCount,
        FlowDirection flowDirection)
    {
        ContractValidationResult<RefuelSchemeDefinition> schemeResult =
            RefuelSchemeDefinition.TryCreate(
                "S" + shiftCount.ToString(CultureInfo.InvariantCulture),
                shiftCount,
                "fresh-fuel",
                RefuelSchemeDefinition.CurrentSchemaVersion,
                Enumerable.Range((int)(PositionCount - shiftCount), shiftCount)
                    .Select(value => new BundlePosition((uint)value)),
                Enumerable.Range(0, shiftCount)
                    .Select(value => new BundlePosition((uint)value)));
        AssertValid(schemeResult);

        ContractValidationResult<RefuelSchemePositionPlan> planResult =
            schemeResult.Value.TryCreatePositionPlan(PositionCount, flowDirection);
        AssertValid(planResult);
        return planResult.Value;
    }

    private static BundleState[] CreateInsertedBundles(
        uint channelIndex,
        RefuelSchemePositionPlan plan,
        int firstId,
        double effectiveTimeSeconds)
    {
        return plan.InsertedPositions
            .Select((position, index) => CreateBundle(
                firstId + index,
                channelIndex,
                position.Value,
                "fresh-fuel",
                0.0,
                0.0,
                effectiveTimeSeconds))
            .ToArray();
    }

    private static BundleState CreateBundle(
        int number,
        uint channelIndex,
        uint positionIndex,
        string materialVariant,
        double initialBurnup,
        double cumulativeEnergy,
        double insertedAtSeconds = 0.0)
    {
        return new BundleState(
            Id(number),
            new ChannelId(channelIndex),
            new BundlePosition(positionIndex),
            new MaterialVariantId(materialVariant),
            initialBurnup,
            cumulativeEnergy,
            1000.0,
            insertedAtSeconds);
    }

    private static VersionLifecycleV1 CreateLifecycle(
        CoreTopology topology,
        BundleInventory inventory,
        byte stateDigestValue = 3)
    {
        ContractValidationResult<DataPackDescriptor> dataPackResult = DataPackDescriptor.TryCreate(
            DataPackDescriptor.CurrentSchemaVersion,
            Id(15000),
            "manufactured-wave0-v1",
            "manufactured-2x12-v1",
            topology.ChannelCount,
            topology.BundlePositionCount,
            "SI-v1",
            DigestBytes(1),
            DigestBytes(2));
        AssertValid(dataPackResult);

        ContractValidationResult<SimulationConfiguration> configurationResult =
            SimulationConfiguration.TryCreate(
                SimulationConfiguration.CurrentSchemaVersion,
                topology,
                dataPackResult.Value,
                0.0,
                0,
                0,
                0);
        AssertValid(configurationResult);

        BundleNuclideVersionV1[] versions = inventory.EnumerateOccupied()
            .Select(bundle => new BundleNuclideVersionV1(bundle.BundleId, 0, 0))
            .ToArray();
        ContractValidationResult<VersionLifecycleV1> lifecycleResult = VersionLifecycleV1.TryCreate(
            configurationResult.Value,
            inventory,
            versions,
            new Digest32(DigestBytes(stateDigestValue)));
        AssertValid(lifecycleResult);
        return lifecycleResult.Value;
    }

    private static NodeKey Node(uint channelIndex, uint positionIndex)
    {
        return new NodeKey(new ChannelId(channelIndex), new BundlePosition(positionIndex));
    }

    private static StableId Id(int number)
    {
        return StableId.Parse(
            "00000000-0000-0000-0000-" +
            number.ToString("D12", CultureInfo.InvariantCulture));
    }

    private static Digest32 Digest(byte value)
    {
        return new Digest32(DigestBytes(value));
    }

    private static byte[] DigestBytes(byte value)
    {
        return Enumerable.Repeat(value, 32).ToArray();
    }

    private static void AssertValid<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
    }

    private static void AssertInvalid<T>(ContractValidationResult<T> result, string expectedCode)
    {
        Assert.False(result.IsValid);
        Assert.Equal(expectedCode, result.FirstDiagnostic.Code);
    }
}
