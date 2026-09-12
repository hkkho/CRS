using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P5T07Phase5InvariantTests
{
    [Fact]
    public void ValidatesExactInventoryCountIdentityLocationAndEnergyAccounting()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        ContractValidationResult<Phase5InvariantReportV1> valid =
            Phase5InvariantValidatorV1.TryValidateInventory(
                fixture.Inventory,
                fixture.Topology.SlotCount);

        AssertValid(valid);
        Assert.Equal(Phase5InvariantScopeV1.Inventory, valid.Value.Scope);
        Assert.Equal(fixture.Topology.SlotCount, valid.Value.ExpectedBundleCount);
        Assert.Equal(fixture.Topology.SlotCount, valid.Value.ActualBundleCount);
        Assert.Equal(fixture.Topology.SlotCount, valid.Value.UniqueBundleIdCount);
        Assert.Equal(fixture.Topology.SlotCount, valid.Value.UniqueLocationCount);
        Assert.Equal(fixture.Topology.SlotCount, valid.Value.NonnegativeBurnupCount);
        Assert.Equal(0.0, valid.Value.TotalCumulativeFissionEnergyJ);

        ContractValidationResult<Phase5InvariantReportV1> wrongCount =
            Phase5InvariantValidatorV1.TryValidateInventory(
                fixture.Inventory,
                fixture.Topology.SlotCount - 1);

        AssertInvalid(wrongCount, "Phase5Invariant.BundleCount.Mismatch");
    }

    [Fact]
    public void ValidatesBurnupIdentityLocationExactEnergyAndMonotonicity()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        BundleState[] bundles = fixture.Inventory.EnumerateOccupied().ToArray();
        AcceptedBundlePowerSnapshotV1 snapshot = CreateSnapshot(
            bundles.Reverse().Select((bundle, index) => CreatePowerSample(bundle, index + 1.0)),
            0.0,
            3);

        ContractValidationResult<BurnupIntervalResultV1> transition =
            BurnupIntervalTransition.TryApply(
                fixture.Inventory,
                3,
                0.0,
                10.0,
                snapshot.StateDigest,
                snapshot);

        AssertValid(transition);
        ContractValidationResult<Phase5InvariantReportV1> valid =
            Phase5InvariantValidatorV1.TryValidateBurnupInterval(
                transition.Value,
                fixture.Topology.SlotCount);

        AssertValid(valid);
        Assert.Equal(Phase5InvariantScopeV1.BurnupInterval, valid.Value.Scope);
        Assert.Equal(fixture.Topology.SlotCount, valid.Value.EnergyRecordCount);
        Assert.Equal(fixture.Topology.SlotCount, valid.Value.MonotonicBurnupCount);
        Assert.Equal(210.0, valid.Value.TotalIntervalEnergyJ);
        Assert.Equal(210.0, valid.Value.TotalCumulativeFissionEnergyJ);

        ContractValidationResult<Phase5InvariantReportV1> wrongCount =
            Phase5InvariantValidatorV1.TryValidateBurnupInterval(
                transition.Value,
                fixture.Topology.SlotCount - 1);

        AssertInvalid(wrongCount, "Phase5Invariant.BundleCount.Mismatch");
    }

    [Fact]
    public void RejectsPositiveEnergyWithNoRepresentableCumulativeIncrease()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        BundleState[] sourceBundles = fixture.Inventory.EnumerateOccupied().ToArray();
        BundleState nearCapacity = new BundleState(
            sourceBundles[0].BundleId,
            sourceBundles[0].ChannelId,
            sourceBundles[0].Position,
            sourceBundles[0].MaterialVariantId,
            sourceBundles[0].InitialBurnupJPerKgHm,
            double.MaxValue,
            1.0,
            sourceBundles[0].InsertedAtSeconds);
        BundleState[] nearCapacityBundles = sourceBundles
            .Select(bundle => bundle.BundleId == nearCapacity.BundleId ? nearCapacity : bundle)
            .ToArray();
        ContractValidationResult<BundleInventory> inventory = BundleInventory.TryCreate(
            fixture.Topology,
            nearCapacityBundles);
        AssertValid(inventory);

        AcceptedBundlePowerSnapshotV1 snapshot = CreateSnapshot(
            nearCapacityBundles.Select(bundle => CreatePowerSample(bundle, 1.0)),
            0.0,
            0);
        ContractValidationResult<BurnupIntervalResultV1> transition =
            BurnupIntervalTransition.TryApply(
                inventory.Value,
                0,
                0.0,
                1.0,
                snapshot.StateDigest,
                snapshot);

        AssertInvalid(transition, "BurnupInterval.CumulativeEnergy.NoRepresentableIncrease");
    }

    [Fact]
    public void ValidatesRefuelIdentityLocationPreservationAndDischargePartition()
    {
        CoreTopology topology = CreateSingleChannelTopology(FlowDirection.EndAtoEndB);
        BundleInventory inventory = CreateInventory(topology);
        RefuelSchemePositionPlan plan = CreatePlan(4, FlowDirection.EndAtoEndB);
        BundleState[] inserted = plan.InsertedPositions
            .Select((position, index) => new BundleState(
                Id(1000 + index),
                new ChannelId(0),
                position,
                new MaterialVariantId("MAT-FRESH"),
                0.0,
                0.0,
                1000.0,
                20.0))
            .ToArray();

        ContractValidationResult<RefuelShiftResult> transition =
            RefuelShiftTransition.TryApply(
                inventory,
                new ChannelId(0),
                plan,
                inserted,
                10.0,
                20.0);

        AssertValid(transition);
        ContractValidationResult<Phase5InvariantReportV1> valid =
            Phase5InvariantValidatorV1.TryValidateRefuelShift(
                transition.Value,
                topology.SlotCount);

        AssertValid(valid);
        Assert.Equal(Phase5InvariantScopeV1.RefuelShift, valid.Value.Scope);
        Assert.Equal(topology.SlotCount, valid.Value.ActualBundleCount);
        Assert.Equal(8, valid.Value.PreservedBundleCount);
        Assert.Equal(4, valid.Value.InsertedBundleCount);
        Assert.Equal(4, valid.Value.DischargedBundleCount);
        Assert.Equal(0, valid.Value.EnergyRecordCount);

        ContractValidationResult<Phase5InvariantReportV1> wrongCount =
            Phase5InvariantValidatorV1.TryValidateRefuelShift(
                transition.Value,
                topology.SlotCount - 1);

        AssertInvalid(wrongCount, "Phase5Invariant.BundleCount.Mismatch");
    }

    private static BundlePowerSampleV1 CreatePowerSample(BundleState bundle, double powerWatts)
    {
        ContractValidationResult<BundlePowerSampleV1> result = BundlePowerSampleV1.TryCreate(
            bundle.BundleId,
            bundle.ChannelId,
            bundle.Position,
            powerWatts);
        AssertValid(result);
        return result.Value;
    }

    private static AcceptedBundlePowerSnapshotV1 CreateSnapshot(
        IEnumerable<BundlePowerSampleV1> samples,
        double snapshotTimeSeconds,
        ulong coreStateVersion)
    {
        ContractValidationResult<AcceptedBundlePowerSnapshotV1> result =
            AcceptedBundlePowerSnapshotV1.TryCreate(
                StableId.Parse("00000000-0000-0000-0000-0000000007a7"),
                snapshotTimeSeconds,
                coreStateVersion,
                new Digest32(new byte[32]),
                samples);
        AssertValid(result);
        return result.Value;
    }

    private static CoreTopology CreateSingleChannelTopology(FlowDirection flowDirection)
    {
        const int positionCount = 12;
        var neighbors = new List<NeighborRecord>();
        var boundaries = new List<BoundaryFaceRecord>();
        ChannelId channelId = new ChannelId(0);

        for (int position = 0; position < positionCount - 1; position++)
        {
            neighbors.Add(new NeighborRecord(
                channelId,
                new BundlePosition((uint)position),
                channelId,
                new BundlePosition((uint)(position + 1)),
                NeighborDirection.TowardEndB));
            neighbors.Add(new NeighborRecord(
                channelId,
                new BundlePosition((uint)(position + 1)),
                channelId,
                new BundlePosition((uint)position),
                NeighborDirection.TowardEndA));
        }

        for (int position = 0; position < positionCount; position++)
        {
            BundlePosition bundlePosition = new BundlePosition((uint)position);
            boundaries.Add(new BoundaryFaceRecord(
                channelId,
                bundlePosition,
                TopologyFace.North,
                BoundaryClassification.Reflective));
            boundaries.Add(new BoundaryFaceRecord(
                channelId,
                bundlePosition,
                TopologyFace.South,
                BoundaryClassification.Reflective));
            boundaries.Add(new BoundaryFaceRecord(
                channelId,
                bundlePosition,
                TopologyFace.East,
                BoundaryClassification.Reflective));
            boundaries.Add(new BoundaryFaceRecord(
                channelId,
                bundlePosition,
                TopologyFace.West,
                BoundaryClassification.Reflective));
        }

        boundaries.Add(new BoundaryFaceRecord(
            channelId,
            new BundlePosition(0),
            TopologyFace.EndA,
            BoundaryClassification.Reflective));
        boundaries.Add(new BoundaryFaceRecord(
            channelId,
            new BundlePosition(positionCount - 1),
            TopologyFace.EndB,
            BoundaryClassification.Reflective));

        ContractValidationResult<CoreTopology> result = CoreTopology.TryCreate(
            1,
            positionCount,
            new[]
            {
                new ChannelTopology(
                    channelId,
                    0,
                    0,
                    flowDirection,
                    flowDirection == FlowDirection.EndAtoEndB
                        ? new BundlePosition(0)
                        : new BundlePosition(positionCount - 1),
                    flowDirection == FlowDirection.EndAtoEndB
                        ? new BundlePosition(positionCount - 1)
                        : new BundlePosition(0),
                    neighbors,
                    boundaries)
            });
        AssertValid(result);
        return result.Value;
    }

    private static BundleInventory CreateInventory(CoreTopology topology)
    {
        BundleState[] bundles = Enumerable.Range(0, topology.SlotCount)
            .Select(position => new BundleState(
                Id(position + 1),
                new ChannelId(0),
                new BundlePosition((uint)position),
                new MaterialVariantId("MAT-OLD"),
                1000.0 + position,
                100.0 + position,
                1000.0,
                0.0))
            .ToArray();
        ContractValidationResult<BundleInventory> result = BundleInventory.TryCreate(topology, bundles);
        AssertValid(result);
        return result.Value;
    }

    private static RefuelSchemePositionPlan CreatePlan(ushort shiftCount, FlowDirection flowDirection)
    {
        ContractValidationResult<RefuelSchemeDefinition> scheme = RefuelSchemeDefinition.TryCreate(
            "S" + shiftCount.ToString(CultureInfo.InvariantCulture),
            shiftCount,
            "MAT-FRESH",
            RefuelSchemeDefinition.CurrentSchemaVersion,
            Enumerable.Range(12 - shiftCount, shiftCount)
                .Select(position => new BundlePosition((uint)position)),
            Enumerable.Range(0, shiftCount)
                .Select(position => new BundlePosition((uint)position)));
        AssertValid(scheme);

        ContractValidationResult<RefuelSchemePositionPlan> plan =
            scheme.Value.TryCreatePositionPlan(12, flowDirection);
        AssertValid(plan);
        return plan.Value;
    }

    private static StableId Id(int number)
    {
        return StableId.Parse(
            "00000000-0000-0000-0000-" +
            number.ToString("D12", CultureInfo.InvariantCulture));
    }

    private static void AssertValid<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
    }

    private static void AssertInvalid<T>(ContractValidationResult<T> result, string code)
    {
        Assert.False(result.IsValid);
        Assert.Equal(code, result.FirstDiagnostic.Code);
    }
}
