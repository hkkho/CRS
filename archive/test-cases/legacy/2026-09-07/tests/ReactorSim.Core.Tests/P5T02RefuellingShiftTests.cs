using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P5T02RefuellingShiftTests
{
    private static readonly int[] ExpectedS4FreshIds = { 1000, 1001, 1002, 1003 };
    private static readonly int[] ExpectedS4RetainedIds = { 1, 2, 3, 4, 5, 6, 7, 8 };
    private static readonly int[] ExpectedS4DischargedIds = { 9, 10, 11, 12 };
    private static readonly int[] ExpectedS8RetainedIds = { 9, 10, 11, 12 };
    private static readonly int[] ExpectedS8FreshIds = { 1100, 1101, 1102, 1103, 1104, 1105, 1106, 1107 };
    private static readonly int[] ExpectedS8DischargedIds = { 1, 2, 3, 4, 5, 6, 7, 8 };

    [Fact]
    public void TowardEndBS4MovesRetainedStatesAndReturnsOrderedDischargeAtomically()
    {
        CoreTopology topology = CreateTopology(1, FlowDirection.EndAtoEndB);
        BundleInventory inventory = CreateInventory(topology);
        BundleState[] original = inventory.EnumerateOccupied().ToArray();
        RefuelSchemePositionPlan plan = CreatePlan(4, FlowDirection.EndAtoEndB);
        BundleState[] inserted = CreateInserted(plan, 1000, 200.0);

        ContractValidationResult<RefuelShiftResult> result = RefuelShiftTransition.TryApply(
            inventory,
            new ChannelId(0),
            plan,
            inserted,
            100.0,
            200.0);

        AssertValid(result);
        BundleInventory resulting = result.Value.ResultingInventory;
        Assert.Equal(12, resulting.OccupiedCount);
        Assert.Equal(ExpectedS4FreshIds,
            Enumerable.Range(0, 4).Select(position => NumericId(resulting.Get(Node(0, position)))).ToArray());
        Assert.Equal(ExpectedS4RetainedIds,
            Enumerable.Range(4, 8).Select(position => NumericId(resulting.Get(Node(0, position)))).ToArray());
        Assert.Equal(ExpectedS4DischargedIds,
            result.Value.DischargedBundles.Select(bundle => NumericId(bundle.BundleId)).ToArray());

        for (int source = 0; source < 8; source++)
        {
            BundleState before = inventory.Get(Node(0, source))!;
            BundleState after = resulting.Get(Node(0, source + 4))!;
            Assert.Equal(before.BundleId, after.BundleId);
            Assert.Equal(before.MaterialVariantId, after.MaterialVariantId);
            Assert.Equal(before.InitialBurnupJPerKgHm, after.InitialBurnupJPerKgHm);
            Assert.Equal(before.CumulativeFissionEnergyJ, after.CumulativeFissionEnergyJ);
            Assert.Equal(before.HeavyMetalMassKg, after.HeavyMetalMassKg);
            Assert.Equal(before.InsertedAtSeconds, after.InsertedAtSeconds);
            Assert.NotSame(before, after);
        }

        for (int position = 0; position < 12; position++)
        {
            Assert.Same(original[position], inventory.Get(Node(0, position)));
        }

        Assert.Equal(0.0, inserted[0].CumulativeFissionEnergyJ);
        Assert.Equal(200.0, inserted[0].InsertedAtSeconds);
    }

    [Fact]
    public void TowardEndAS8UsesOppositeEndpointAndPreservesOtherChannels()
    {
        CoreTopology topology = CreateTopology(2, FlowDirection.EndBtoEndA);
        BundleInventory inventory = CreateInventory(topology);
        BundleState otherChannelBundle = inventory.Get(Node(1, 0))!;
        RefuelSchemePositionPlan plan = CreatePlan(8, FlowDirection.EndBtoEndA);
        BundleState[] inserted = CreateInserted(plan, 1100, 300.0);

        ContractValidationResult<RefuelShiftResult> result = RefuelShiftTransition.TryApply(
            inventory,
            new ChannelId(0),
            plan,
            inserted,
            300.0,
            300.0);

        AssertValid(result);
        BundleInventory resulting = result.Value.ResultingInventory;
        Assert.Equal(ExpectedS8RetainedIds,
            Enumerable.Range(0, 4).Select(position => NumericId(resulting.Get(Node(0, position)))).ToArray());
        Assert.Equal(ExpectedS8FreshIds,
            Enumerable.Range(4, 8).Select(position => NumericId(resulting.Get(Node(0, position)))).ToArray());
        Assert.Equal(ExpectedS8DischargedIds,
            result.Value.DischargedBundles.Select(bundle => NumericId(bundle.BundleId)));
        Assert.Same(otherChannelBundle, resulting.Get(Node(1, 0)));
        Assert.Equal(RefuelShiftDirection.TowardEndA, result.Value.PositionPlan.ShiftDirection);
    }

    [Fact]
    public void RejectsWrongOrderAndFreshStateViolationsWithoutChangingInput()
    {
        CoreTopology topology = CreateTopology(1, FlowDirection.EndAtoEndB);
        BundleInventory inventory = CreateInventory(topology);
        BundleState originalAtZero = inventory.Get(Node(0, 0))!;
        RefuelSchemePositionPlan plan = CreatePlan(4, FlowDirection.EndAtoEndB);
        BundleState[] inserted = CreateInserted(plan, 1200, 400.0);

        BundleState wrongPosition = inserted[1].WithLocation(new ChannelId(0), new BundlePosition(0));
        BundleState[] wrongOrder = inserted.ToArray();
        wrongOrder[1] = wrongPosition;
        ContractValidationResult<RefuelShiftResult> orderResult = RefuelShiftTransition.TryApply(
            inventory,
            new ChannelId(0),
            plan,
            wrongOrder,
            400.0,
            400.0);
        AssertInvalid(orderResult, "RefuelShift.InsertedBundle.PositionMismatch");

        BundleState nonFresh = inserted[0].WithEnergy(1.0);
        BundleState[] nonFreshSet = inserted.ToArray();
        nonFreshSet[0] = nonFresh;
        ContractValidationResult<RefuelShiftResult> energyResult = RefuelShiftTransition.TryApply(
            inventory,
            new ChannelId(0),
            plan,
            nonFreshSet,
            400.0,
            400.0);
        AssertInvalid(energyResult, "RefuelShift.InsertedBundle.Energy.NonZero");

        Assert.Same(originalAtZero, inventory.Get(Node(0, 0)));
        Assert.Equal(12, inventory.OccupiedCount);
    }

    [Fact]
    public void RejectsMismatchedFlowAndNonMonotonicTimeBeforeBuildingAResult()
    {
        CoreTopology topology = CreateTopology(1, FlowDirection.EndAtoEndB);
        BundleInventory inventory = CreateInventory(topology);
        RefuelSchemePositionPlan wrongPlan = CreatePlan(4, FlowDirection.EndBtoEndA);
        BundleState[] inserted = CreateInserted(wrongPlan, 1300, 500.0);

        ContractValidationResult<RefuelShiftResult> flowResult = RefuelShiftTransition.TryApply(
            inventory,
            new ChannelId(0),
            wrongPlan,
            inserted,
            500.0,
            500.0);
        AssertInvalid(flowResult, "RefuelShift.FlowDirection.Mismatch");

        RefuelSchemePositionPlan validPlan = CreatePlan(4, FlowDirection.EndAtoEndB);
        BundleState[] validInserted = CreateInserted(validPlan, 1400, 500.0);
        ContractValidationResult<RefuelShiftResult> timeResult = RefuelShiftTransition.TryApply(
            inventory,
            new ChannelId(0),
            validPlan,
            validInserted,
            501.0,
            500.0);
        AssertInvalid(timeResult, "RefuelShift.EffectiveTime.BeforeCurrent");
    }

    [Fact]
    public void RejectsPartiallyOccupiedTargetChannel()
    {
        CoreTopology topology = CreateTopology(1, FlowDirection.EndAtoEndB);
        BundleState[] bundles = CreateBundles(topology).Where(bundle => bundle.Position.Value != 11).ToArray();
        ContractValidationResult<BundleInventory> inventoryResult = BundleInventory.TryCreate(topology, bundles);
        AssertValid(inventoryResult);

        RefuelSchemePositionPlan plan = CreatePlan(4, FlowDirection.EndAtoEndB);
        ContractValidationResult<RefuelShiftResult> result = RefuelShiftTransition.TryApply(
            inventoryResult.Value,
            new ChannelId(0),
            plan,
            CreateInserted(plan, 1500, 600.0),
            600.0,
            600.0);

        AssertInvalid(result, "RefuelShift.Channel.NotFullyOccupied");
    }

    private static CoreTopology CreateTopology(int channelCount, FlowDirection flowDirection)
    {
        const int positionCount = 12;
        var channels = new List<ChannelTopology>();
        for (int channelIndex = 0; channelIndex < channelCount; channelIndex++)
        {
            var neighbors = new List<NeighborRecord>();
            var boundaries = new List<BoundaryFaceRecord>();
            ChannelId channelId = new ChannelId((uint)channelIndex);

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
                boundaries.Add(new BoundaryFaceRecord(
                    channelId,
                    new BundlePosition((uint)position),
                    TopologyFace.North,
                    BoundaryClassification.Reflective));
                boundaries.Add(new BoundaryFaceRecord(
                    channelId,
                    new BundlePosition((uint)position),
                    TopologyFace.South,
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

            if (channelIndex == 0)
            {
                for (int position = 0; position < positionCount; position++)
                {
                    if (channelCount == 1)
                    {
                        boundaries.Add(new BoundaryFaceRecord(
                            channelId,
                            new BundlePosition((uint)position),
                            TopologyFace.East,
                            BoundaryClassification.Reflective));
                    }
                    else
                    {
                        neighbors.Add(new NeighborRecord(
                            channelId,
                            new BundlePosition((uint)position),
                            new ChannelId(1),
                            new BundlePosition((uint)position),
                            NeighborDirection.East));
                    }

                    boundaries.Add(new BoundaryFaceRecord(
                        channelId,
                        new BundlePosition((uint)position),
                        TopologyFace.West,
                        BoundaryClassification.Reflective));
                }
            }
            else
            {
                for (int position = 0; position < positionCount; position++)
                {
                    neighbors.Add(new NeighborRecord(
                        channelId,
                        new BundlePosition((uint)position),
                        new ChannelId(0),
                        new BundlePosition((uint)position),
                        NeighborDirection.West));
                    boundaries.Add(new BoundaryFaceRecord(
                        channelId,
                        new BundlePosition((uint)position),
                        TopologyFace.East,
                        BoundaryClassification.Reflective));
                }
            }

            channels.Add(new ChannelTopology(
                channelId,
                channelIndex,
                0,
                flowDirection,
                flowDirection == FlowDirection.EndAtoEndB
                    ? new BundlePosition(0)
                    : new BundlePosition(positionCount - 1),
                flowDirection == FlowDirection.EndAtoEndB
                    ? new BundlePosition(positionCount - 1)
                    : new BundlePosition(0),
                neighbors,
                boundaries));
        }

        ContractValidationResult<CoreTopology> result = CoreTopology.TryCreate(
            (uint)channelCount,
            positionCount,
            channels);
        AssertValid(result);
        return result.Value;
    }

    private static BundleInventory CreateInventory(CoreTopology topology)
    {
        ContractValidationResult<BundleInventory> result = BundleInventory.TryCreate(
            topology,
            CreateBundles(topology));
        AssertValid(result);
        return result.Value;
    }

    private static IEnumerable<BundleState> CreateBundles(CoreTopology topology)
    {
        foreach (ChannelTopology channel in topology.Channels)
        {
            for (int position = 0; position < topology.BundlePositionCount; position++)
            {
                int number = checked((int)(channel.ChannelId.Value * 100 + (uint)position + 1));
                yield return new BundleState(
                    Id(number),
                    channel.ChannelId,
                    new BundlePosition((uint)position),
                    new MaterialVariantId("MAT-OLD"),
                    1000.0 + number,
                    10000.0 + number,
                    1000.0,
                    0.0);
            }
        }
    }

    private static RefuelSchemePositionPlan CreatePlan(ushort shiftCount, FlowDirection flowDirection)
    {
        uint towardEndAFirst = 12u - shiftCount;
        ContractValidationResult<RefuelSchemeDefinition> schemeResult =
            RefuelSchemeDefinition.TryCreate(
                "S" + shiftCount,
                shiftCount,
                "FT-SYN-1000KG",
                RefuelSchemeDefinition.CurrentSchemaVersion,
                Enumerable.Range((int)towardEndAFirst, shiftCount)
                    .Select(value => new BundlePosition((uint)value)),
                Enumerable.Range(0, shiftCount)
                    .Select(value => new BundlePosition((uint)value)));
        AssertValid(schemeResult);

        ContractValidationResult<RefuelSchemePositionPlan> planResult =
            schemeResult.Value.TryCreatePositionPlan(12, flowDirection);
        AssertValid(planResult);
        return planResult.Value;
    }

    private static BundleState[] CreateInserted(
        RefuelSchemePositionPlan plan,
        int firstId,
        double effectiveTimeSeconds)
    {
        return plan.InsertedPositions
            .Select((position, index) => new BundleState(
                Id(firstId + index),
                new ChannelId(0),
                position,
                new MaterialVariantId("FT-SYN-1000KG"),
                0.0,
                0.0,
                1000.0,
                effectiveTimeSeconds))
            .ToArray();
    }

    private static NodeKey Node(int channel, int position)
    {
        return new NodeKey(new ChannelId((uint)channel), new BundlePosition((uint)position));
    }

    private static StableId Id(int number)
    {
        return StableId.Parse("00000000-0000-0000-0000-" + number.ToString("D12", CultureInfo.InvariantCulture));
    }

    private static int NumericId(BundleState? bundle)
    {
        Assert.NotNull(bundle);
        return NumericId(bundle!.BundleId);
    }

    private static int NumericId(StableId id)
    {
        return int.Parse(id.ToString().AsSpan(24), CultureInfo.InvariantCulture);
    }

    private static void AssertValid<T>(ContractValidationResult<T> result)
        where T : class
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
    }

    private static void AssertInvalid<T>(ContractValidationResult<T> result, string code)
    {
        Assert.False(result.IsValid);
        Assert.Equal(code, result.FirstDiagnostic.Code);
    }
}

internal static class P5T02BundleStateTestExtensions
{
    public static BundleState WithEnergy(this BundleState bundle, double energy)
    {
        return new BundleState(
            bundle.BundleId,
            bundle.ChannelId,
            bundle.Position,
            bundle.MaterialVariantId,
            bundle.InitialBurnupJPerKgHm,
            energy,
            bundle.HeavyMetalMassKg,
            bundle.InsertedAtSeconds);
    }
}
