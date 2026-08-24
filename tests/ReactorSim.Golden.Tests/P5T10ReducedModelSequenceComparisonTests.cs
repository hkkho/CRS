using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using ReactorSim.Core;
using ReactorSim.TestInfrastructure;
using Xunit;

namespace ReactorSim.Golden.Tests;

public sealed class P5T10ReducedModelSequenceComparisonTests
{
    private const string ApprovedArtifactDirectory = "P4T06G4KData";
    private const string DefinitionArtifactDirectory = "P4T06G4JData";
    private const string ApprovedArtifactFileName = "approved-independent-authority-v1.json";
    private const string SequenceDefinitionFileName = "representative-definition-v1.json";
    private static readonly int[] EndAtoEndBInsertedPositions = { 0, 1, 2, 3 };
    private static readonly int[] EndAtoEndBDischargedPositions = { 8, 9, 10, 11 };
    private static readonly int[] EndBtoEndAInsertedPositions = { 8, 9, 10, 11 };
    private static readonly int[] EndBtoEndADischargedPositions = { 0, 1, 2, 3 };

    [Fact]
    public void ApprovedReducedModelSequenceBindsBothS4DirectionsAndBurnupProjection()
    {
        using JsonDocument definition = ReadJson(SequenceDefinitionFileName);
        using JsonDocument approved = ReadJson(ApprovedArtifactFileName);

        JsonElement definitionRoot = definition.RootElement;
        JsonElement approvedRoot = approved.RootElement;
        JsonElement definitionScenario = FindScenario(
            definitionRoot,
            "refuelled_4_bundle_shift");
        JsonElement approvedScenario = FindScenario(
            approvedRoot,
            "refuelled_4_bundle_shift");

        Assert.Equal("approved_golden", StringValue(approvedRoot, "status"));
        Assert.Equal("RepresentativeReducedModel", StringValue(approvedRoot, "coverage_class"));
        Assert.Equal("ReducedModel", StringValue(approvedRoot, "validation_domain"));
        Assert.Equal(2, approvedScenario.GetProperty("state").GetProperty("refuelling_audits").GetArrayLength());

        JsonElement definitionEvents = definitionScenario.GetProperty("events");
        Assert.Equal(4, definitionEvents.GetArrayLength());
        Assert.Equal("burnup_commit", StringValue(definitionEvents[1], "event_type"));
        Assert.Equal(86400.0, definitionEvents[1].GetProperty("time_s").GetDouble());
        Assert.Equal("refuel_shift", StringValue(definitionEvents[2], "event_type"));
        Assert.Equal("refuel_shift", StringValue(definitionEvents[3], "event_type"));

        JsonElement approvedEvents = approvedScenario.GetProperty("event_history");
        Assert.Equal(4, approvedEvents.GetArrayLength());
        Assert.Equal("burnup_commit", StringValue(approvedEvents[1], "event_type"));
        Assert.Equal("refuel_shift", StringValue(approvedEvents[2], "event_type"));
        Assert.Equal("refuel_shift", StringValue(approvedEvents[3], "event_type"));
        Assert.Equal(172800.0, approvedEvents[2].GetProperty("time_s").GetDouble());
        Assert.Equal(172800.0, approvedEvents[3].GetProperty("time_s").GetDouble());

        AssertAudit(
            approvedScenario.GetProperty("state").GetProperty("refuelling_audits")[0],
            channelId: 0,
            direction: "TowardEndB",
            inserted: EndAtoEndBInsertedPositions,
            discharged: EndAtoEndBDischargedPositions);
        AssertAudit(
            approvedScenario.GetProperty("state").GetProperty("refuelling_audits")[1],
            channelId: 1,
            direction: "TowardEndA",
            inserted: EndBtoEndAInsertedPositions,
            discharged: EndBtoEndADischargedPositions);

        double[] baseBurnup = ReadArray(definitionScenario.GetProperty("base_burnup_by_position_j_per_kg_hm"));
        double[] channelOffsets = ReadArray(definitionScenario.GetProperty("channel_burnup_offset_j_per_kg_hm"));
        foreach (JsonElement coefficient in approvedScenario.GetProperty("coefficients").EnumerateArray())
        {
            int channelId = coefficient.GetProperty("channel_id").GetInt32();
            int position = coefficient.GetProperty("position").GetInt32();
            bool isFresh = (channelId == 0 && position <= 3) ||
                           (channelId == 1 && position >= 8);
            int sourcePosition = channelId switch
            {
                0 => position - 4,
                1 => position + 4,
                _ => position
            };
            double expectedBurnup = isFresh
                ? 0.0
                : baseBurnup[sourcePosition] + channelOffsets[channelId];
            Assert.Equal(
                expectedBurnup,
                coefficient.GetProperty("burnup_j_per_kg_hm").GetDouble());
        }
    }

    [Fact]
    public void CoreSequencePreservesBurnupEnergyAndIdentityAcrossBothRefuellingEvents()
    {
        using JsonDocument definition = ReadJson(SequenceDefinitionFileName);
        using JsonDocument approved = ReadJson(ApprovedArtifactFileName);
        JsonElement definitionScenario = FindScenario(
            definition.RootElement,
            "equilibrium_like");
        JsonElement approvedScenario = FindScenario(
            approved.RootElement,
            "refuelled_4_bundle_shift");

        CoreTopology topology = CreateTopology();
        double[] baseBurnup = ReadArray(definitionScenario.GetProperty("base_burnup_by_position_j_per_kg_hm"));
        double[] channelOffsets = ReadArray(definitionScenario.GetProperty("channel_burnup_offset_j_per_kg_hm"));
        BundleInventory inventory = CreatePostBurnupInventory(topology, baseBurnup, channelOffsets);

        RefuelSchemeDefinition scheme = CreateS4Scheme();
        RefuelSchemePositionPlan endBPlan = CreatePlan(scheme, FlowDirection.EndAtoEndB);
        RefuelSchemePositionPlan endAPlan = CreatePlan(scheme, FlowDirection.EndBtoEndA);
        BundleState[] endBInserted = CreateInserted(endBPlan, 9000, 172800.0);
        BundleState[] endAInserted = CreateInserted(endAPlan, 9100, 172800.0);

        ContractValidationResult<RefuelShiftResult> endB = RefuelShiftTransition.TryApply(
            inventory,
            new ChannelId(0),
            endBPlan,
            endBInserted,
            172800.0,
            172800.0);
        AssertValid(endB);
        ContractValidationResult<Phase5InvariantReportV1> endBInvariant =
            Phase5InvariantValidatorV1.TryValidateRefuelShift(endB.Value, topology.SlotCount);
        AssertValid(endBInvariant);
        Assert.Equal(44, endBInvariant.Value.PreservedBundleCount);
        Assert.Equal(4, endBInvariant.Value.InsertedBundleCount);
        Assert.Equal(4, endBInvariant.Value.DischargedBundleCount);

        ContractValidationResult<RefuelShiftResult> endA = RefuelShiftTransition.TryApply(
            endB.Value.ResultingInventory,
            new ChannelId(1),
            endAPlan,
            endAInserted,
            172800.0,
            172800.0);
        AssertValid(endA);
        ContractValidationResult<Phase5InvariantReportV1> endAInvariant =
            Phase5InvariantValidatorV1.TryValidateRefuelShift(endA.Value, topology.SlotCount);
        AssertValid(endAInvariant);
        Assert.Equal(44, endAInvariant.Value.PreservedBundleCount);
        Assert.Equal(4, endAInvariant.Value.InsertedBundleCount);
        Assert.Equal(4, endAInvariant.Value.DischargedBundleCount);

        ContractValidationResult<Phase5InvariantReportV1> inventoryInvariant =
            Phase5InvariantValidatorV1.TryValidateInventory(endA.Value.ResultingInventory, topology.SlotCount);
        AssertValid(inventoryInvariant);
        Assert.Equal(48, inventoryInvariant.Value.ActualBundleCount);
        Assert.Equal(48, inventoryInvariant.Value.UniqueBundleIdCount);
        Assert.Equal(48, inventoryInvariant.Value.UniqueLocationCount);

        foreach (JsonElement coefficient in approvedScenario.GetProperty("coefficients").EnumerateArray())
        {
            int channelId = coefficient.GetProperty("channel_id").GetInt32();
            int position = coefficient.GetProperty("position").GetInt32();
            BundleState? actual = endA.Value.ResultingInventory.Get(Node(channelId, position));
            Assert.NotNull(actual);
            Assert.Equal(
                coefficient.GetProperty("burnup_j_per_kg_hm").GetDouble(),
                actual!.CurrentBurnupJPerKgHm);
            Assert.Equal(
                actual.InitialBurnupJPerKgHm + actual.CumulativeFissionEnergyJ / actual.HeavyMetalMassKg,
                actual.CurrentBurnupJPerKgHm);
        }

        Assert.Equal(
            endBInserted.Select(bundle => bundle.BundleId).ToArray(),
            Enumerable.Range(0, 4)
                .Select(position => endA.Value.ResultingInventory.Get(Node(0, position))!.BundleId)
                .ToArray());
        Assert.Equal(
            endAInserted.Select(bundle => bundle.BundleId).ToArray(),
            Enumerable.Range(8, 4)
                .Select(position => endA.Value.ResultingInventory.Get(Node(1, position))!.BundleId)
                .ToArray());

        AssertDischargePreservesEnergy(
            inventory,
            endB.Value.DischargedBundles,
            channelId: 0,
            positions: EndAtoEndBDischargedPositions);
        AssertDischargePreservesEnergy(
            inventory,
            endA.Value.DischargedBundles,
            channelId: 1,
            positions: EndBtoEndADischargedPositions);

        AssertPlanMatchesAudit(
            endB.Value.PositionPlan,
            approvedScenario.GetProperty("state").GetProperty("refuelling_audits")[0]);
        AssertPlanMatchesAudit(
            endA.Value.PositionPlan,
            approvedScenario.GetProperty("state").GetProperty("refuelling_audits")[1]);
    }

    private static void AssertDischargePreservesEnergy(
        BundleInventory original,
        IReadOnlyList<BundleState> discharged,
        int channelId,
        int[] positions)
    {
        Assert.Equal(positions.Length, discharged.Count);
        for (int index = 0; index < positions.Length; index++)
        {
            BundleState originalBundle = original.Get(Node(channelId, positions[index]))!;
            BundleState dischargedBundle = discharged[index];
            Assert.Equal(originalBundle.BundleId, dischargedBundle.BundleId);
            Assert.Equal(originalBundle.CurrentBurnupJPerKgHm, dischargedBundle.CurrentBurnupJPerKgHm);
            Assert.Equal(originalBundle.CumulativeFissionEnergyJ, dischargedBundle.CumulativeFissionEnergyJ);
            Assert.Equal(originalBundle.InsertedAtSeconds, dischargedBundle.InsertedAtSeconds);
        }
    }

    private static void AssertPlanMatchesAudit(
        RefuelSchemePositionPlan plan,
        JsonElement audit)
    {
        Assert.Equal(StringValue(audit, "scheme_id"), plan.SchemeId);
        Assert.Equal(StringValue(audit, "shift_direction"), plan.ShiftDirection.ToString());
        Assert.Equal(
            ReadIntArray(audit.GetProperty("inserted_positions")),
            plan.InsertedPositions.Select(position => checked((int)position.Value)).ToArray());
        Assert.Equal(
            ReadIntArray(audit.GetProperty("discharged_positions")),
            plan.DischargedPositions.Select(position => checked((int)position.Value)).ToArray());
    }

    private static void AssertAudit(
        JsonElement audit,
        int channelId,
        string direction,
        int[] inserted,
        int[] discharged)
    {
        Assert.Equal(channelId, audit.GetProperty("channel_id").GetInt32());
        Assert.Equal("S4", StringValue(audit, "scheme_id"));
        Assert.Equal(direction, StringValue(audit, "shift_direction"));
        Assert.Equal(inserted, ReadIntArray(audit.GetProperty("inserted_positions")));
        Assert.Equal(discharged, ReadIntArray(audit.GetProperty("discharged_positions")));
    }

    private static BundleInventory CreatePostBurnupInventory(
        CoreTopology topology,
        double[] baseBurnup,
        double[] channelOffsets)
    {
        var bundles = new List<BundleState>();
        foreach (ChannelTopology channel in topology.Channels)
        {
            for (int position = 0; position < topology.BundlePositionCount; position++)
            {
                double burnup = baseBurnup[position] + channelOffsets[channel.ChannelId.Value];
                bundles.Add(new BundleState(
                    Id(1000 + checked((int)(channel.ChannelId.Value * 100 + (uint)position))),
                    channel.ChannelId,
                    new BundlePosition((uint)position),
                    new MaterialVariantId("MAT-SYN"),
                    0.0,
                    burnup * 1000.0,
                    1000.0,
                    0.0));
            }
        }

        ContractValidationResult<BundleInventory> result = BundleInventory.TryCreate(topology, bundles);
        AssertValid(result);
        return result.Value;
    }

    private static BundleState[] CreateInserted(
        RefuelSchemePositionPlan plan,
        int firstId,
        double effectiveTimeSeconds)
    {
        return plan.InsertedPositions
            .Select((position, index) => new BundleState(
                Id(firstId + index),
                plan.FlowDirection == FlowDirection.EndAtoEndB
                    ? new ChannelId(0)
                    : new ChannelId(1),
                position,
                new MaterialVariantId("MAT-SYN"),
                0.0,
                0.0,
                1000.0,
                effectiveTimeSeconds))
            .ToArray();
    }

    private static RefuelSchemeDefinition CreateS4Scheme()
    {
        ContractValidationResult<RefuelSchemeDefinition> result = RefuelSchemeDefinition.TryCreate(
            "S4",
            4,
            "FT-SYN-1000KG",
            RefuelSchemeDefinition.CurrentSchemaVersion,
            Enumerable.Range(8, 4).Select(position => new BundlePosition((uint)position)),
            Enumerable.Range(0, 4).Select(position => new BundlePosition((uint)position)));
        AssertValid(result);
        return result.Value;
    }

    private static RefuelSchemePositionPlan CreatePlan(
        RefuelSchemeDefinition scheme,
        FlowDirection flowDirection)
    {
        ContractValidationResult<RefuelSchemePositionPlan> result =
            scheme.TryCreatePositionPlan(12, flowDirection);
        AssertValid(result);
        return result.Value;
    }

    private static CoreTopology CreateTopology()
    {
        const int channelCount = 4;
        const int positionCount = 12;
        var channels = new List<ChannelTopology>();
        FlowDirection[] flowDirections =
        {
            FlowDirection.EndAtoEndB,
            FlowDirection.EndBtoEndA,
            FlowDirection.EndAtoEndB,
            FlowDirection.EndBtoEndA
        };

        for (int channelIndex = 0; channelIndex < channelCount; channelIndex++)
        {
            ChannelId channelId = new ChannelId((uint)channelIndex);
            var neighbors = new List<NeighborRecord>();
            var boundaries = new List<BoundaryFaceRecord>();
            AddAxialNeighbors(channelId, positionCount, neighbors);
            AddTransverseNeighborsAndBoundaries(channelIndex, positionCount, neighbors, boundaries);
            boundaries.Add(new BoundaryFaceRecord(
                channelId,
                new BundlePosition(0),
                TopologyFace.EndA,
                BoundaryClassification.Reflective));
            boundaries.Add(new BoundaryFaceRecord(
                channelId,
                new BundlePosition((uint)(positionCount - 1)),
                TopologyFace.EndB,
                BoundaryClassification.Reflective));

            channels.Add(new ChannelTopology(
                channelId,
                channelIndex % 2,
                channelIndex / 2,
                flowDirections[channelIndex],
                flowDirections[channelIndex] == FlowDirection.EndAtoEndB
                    ? new BundlePosition(0)
                    : new BundlePosition(positionCount - 1),
                flowDirections[channelIndex] == FlowDirection.EndAtoEndB
                    ? new BundlePosition(positionCount - 1)
                    : new BundlePosition(0),
                neighbors,
                boundaries));
        }

        ContractValidationResult<CoreTopology> result = CoreTopology.TryCreate(
            (uint)channelCount,
            (uint)positionCount,
            channels);
        AssertValid(result);
        return result.Value;
    }

    private static void AddAxialNeighbors(
        ChannelId channelId,
        int positionCount,
        List<NeighborRecord> neighbors)
    {
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
    }

    private static void AddTransverseNeighborsAndBoundaries(
        int channelIndex,
        int positionCount,
        List<NeighborRecord> neighbors,
        List<BoundaryFaceRecord> boundaries)
    {
        int? east = channelIndex switch
        {
            0 => 1,
            2 => 3,
            _ => null
        };
        int? west = channelIndex switch
        {
            1 => 0,
            3 => 2,
            _ => null
        };
        int? north = channelIndex switch
        {
            0 => 2,
            1 => 3,
            _ => null
        };
        int? south = channelIndex switch
        {
            2 => 0,
            3 => 1,
            _ => null
        };

        AddTransverseFace(channelIndex, east, TopologyFace.East, TopologyFace.West, positionCount, neighbors, boundaries);
        AddTransverseFace(channelIndex, west, TopologyFace.West, TopologyFace.East, positionCount, neighbors, boundaries);
        AddTransverseFace(channelIndex, north, TopologyFace.North, TopologyFace.South, positionCount, neighbors, boundaries);
        AddTransverseFace(channelIndex, south, TopologyFace.South, TopologyFace.North, positionCount, neighbors, boundaries);
    }

    private static void AddTransverseFace(
        int channelIndex,
        int? targetChannel,
        TopologyFace face,
        TopologyFace reciprocalFace,
        int positionCount,
        List<NeighborRecord> neighbors,
        List<BoundaryFaceRecord> boundaries)
    {
        ChannelId source = new ChannelId((uint)channelIndex);
        if (!targetChannel.HasValue)
        {
            for (int position = 0; position < positionCount; position++)
            {
                boundaries.Add(new BoundaryFaceRecord(
                    source,
                    new BundlePosition((uint)position),
                    face,
                    BoundaryClassification.Reflective));
            }

            return;
        }

        NeighborDirection direction = face switch
        {
            TopologyFace.East => NeighborDirection.East,
            TopologyFace.West => NeighborDirection.West,
            TopologyFace.North => NeighborDirection.North,
            TopologyFace.South => NeighborDirection.South,
            _ => throw new InvalidOperationException("Unexpected transverse face.")
        };
        for (int position = 0; position < positionCount; position++)
        {
            neighbors.Add(new NeighborRecord(
                source,
                new BundlePosition((uint)position),
                new ChannelId((uint)targetChannel.Value),
                new BundlePosition((uint)position),
                direction));
        }
    }

    private static JsonDocument ReadJson(string fileName)
    {
        string directory = fileName == ApprovedArtifactFileName
            ? ApprovedArtifactDirectory
            : DefinitionArtifactDirectory;
        return JsonDocument.Parse(File.ReadAllBytes(TestDataLocator.RequireFile(
            Path.Combine(directory, fileName))));
    }

    private static JsonElement FindScenario(JsonElement root, string scenarioId)
    {
        foreach (JsonElement scenario in root.GetProperty("scenarios").EnumerateArray())
        {
            if (StringValue(scenario, "scenario_id") == scenarioId)
            {
                return scenario;
            }
        }

        throw new InvalidOperationException("Missing scenario " + scenarioId);
    }

    private static double[] ReadArray(JsonElement value)
    {
        return value.EnumerateArray().Select(item => item.GetDouble()).ToArray();
    }

    private static int[] ReadIntArray(JsonElement value)
    {
        return value.EnumerateArray().Select(item => item.GetInt32()).ToArray();
    }

    private static string StringValue(JsonElement parent, string property)
    {
        return parent.GetProperty(property).GetString()!;
    }

    private static NodeKey Node(int channelId, int position)
    {
        return new NodeKey(new ChannelId((uint)channelId), new BundlePosition((uint)position));
    }

    private static StableId Id(int number)
    {
        return StableId.Parse(
            "00000000-0000-0000-0000-" + number.ToString("D12", CultureInfo.InvariantCulture));
    }

    private static void AssertValid<T>(ContractValidationResult<T> result)
        where T : class
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
    }
}
