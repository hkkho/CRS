using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P5T03RefuellingEventTests
{
    private static readonly int[] ExpectedMovedS4 = { 1, 2, 3, 4, 5, 6, 7, 8 };
    private static readonly int[] ExpectedInsertedS4 = { 1000, 1001, 1002, 1003 };
    private static readonly int[] ExpectedDischargedS4 = { 9, 10, 11, 12 };
    private static readonly int[] ExpectedMovedS8TowardA = { 9, 10, 11, 12 };
    private static readonly uint[] ExpectedInsertedPositionsS8TowardA = { 4, 5, 6, 7, 8, 9, 10, 11 };

    [Fact]
    public void BuildsCanonicalMappingAtomicityEventsAndBasicDischargeRecords()
    {
        CoreTopology topology = CreateTopology(FlowDirection.EndAtoEndB);
        BundleInventory inventory = CreateInventory(topology);
        RefuelSchemePositionPlan plan = CreatePlan(4, FlowDirection.EndAtoEndB);
        ContractValidationResult<RefuelShiftResult> shift = RefuelShiftTransition.TryApply(
            inventory,
            new ChannelId(0),
            plan,
            CreateInserted(plan, 1000, 10.0),
            0.0,
            10.0);
        AssertValid(shift);

        VersionLifecycleV1 lifecycle = CreateLifecycle(topology, inventory);
        ContractValidationResult<EventLogV1> log = EventLogV1.TryCreate(Array.Empty<EventRecordV1>());
        AssertValid(log);
        StableId commandId = Id(9000);
        StableId atomicityEventId = Id(9001);

        ContractValidationResult<RefuelAuditResultV1> audit = RefuelAuditTransitionV1.TryAppendCommitted(
            shift.Value,
            log.Value,
            commandId,
            atomicityEventId,
            20,
            Digest(0x11),
            Digest(0x22),
            lifecycle.CoreStateVersion,
            lifecycle.CreateStateBinding());

        AssertValid(audit);
        Assert.Empty(log.Value.Records);
        Assert.Equal(2, audit.Value.FinalEventLog.Records.Count);
        Assert.Equal(commandId, audit.Value.MappingEvent.EventId);
        Assert.Equal(atomicityEventId, audit.Value.AtomicityEvent.EventId);
        Assert.Equal(EventBodyKindV1.RefuelMapping, audit.Value.MappingEvent.Body.Kind);
        Assert.Equal(EventBodyKindV1.RefuelAtomicity, audit.Value.AtomicityEvent.Body.Kind);

        RefuelMappingBodyV1 mapping = Assert.IsType<RefuelMappingBodyV1>(audit.Value.MappingEvent.Body.Payload);
        Assert.Equal("S4", mapping.SchemeId);
        Assert.Equal((ushort)4, mapping.ShiftCount);
        Assert.Equal(ExpectedMovedS4, NumericIds(mapping.MovedBundleIds));
        Assert.Equal(ExpectedInsertedS4, NumericIds(mapping.InsertedBundleIds));
        Assert.Equal(ExpectedDischargedS4, NumericIds(mapping.DischargedBundleIds));
        Assert.Equal(16, mapping.PositionBindings.Count);
        Assert.Equal(MovementStatusV1.Moved, mapping.PositionBindings[0].MovementStatus);
        Assert.Equal((uint)0, mapping.PositionBindings[0].OldBundlePositionOrNA.Value.Value);
        Assert.Equal((uint)4, mapping.PositionBindings[0].NewBundlePositionOrNA.Value.Value);
        Assert.Equal(MovementStatusV1.Inserted, mapping.PositionBindings[8].MovementStatus);
        Assert.False(mapping.PositionBindings[8].OldBundlePositionOrNA.IsApplicable);
        Assert.Equal(MovementStatusV1.Discharged, mapping.PositionBindings[12].MovementStatus);
        Assert.False(mapping.PositionBindings[12].NewBundlePositionOrNA.IsApplicable);

        RefuelAtomicityBodyV1 atomicity = Assert.IsType<RefuelAtomicityBodyV1>(
            audit.Value.AtomicityEvent.Body.Payload);
        Assert.Equal(commandId, atomicity.CommandId);
        Assert.Equal(CommitStatusV1.Committed, atomicity.CommitStatus);
        Assert.False(atomicity.RollbackReasonOrNA.IsApplicable);
        Assert.False(atomicity.UnchangedDigestOrNA.IsApplicable);
        Assert.Equal(Digest(0x11), atomicity.BeforeDigest);
        Assert.Equal(Digest(0x22), atomicity.ProposedDigest);

        Assert.Equal(ExpectedDischargedS4, audit.Value.DischargeRecords.Select(record => NumericId(record.BundleId)));
        Assert.All(audit.Value.DischargeRecords, record =>
        {
            Assert.Equal(RefuelDischargeEndV1.EndB, record.DischargeEnd);
            Assert.Equal(10.0, record.DischargedAtSeconds);
            Assert.Equal(commandId, record.CommandId);
            Assert.Equal(lifecycle.CoreStateVersion, record.CoreStateVersionAtDischarge);
        });
    }

    [Fact]
    public void TowardEndAS8UsesEndAAndCanonicalRetainedIdentityOrder()
    {
        CoreTopology topology = CreateTopology(FlowDirection.EndBtoEndA);
        BundleInventory inventory = CreateInventory(topology);
        RefuelSchemePositionPlan plan = CreatePlan(8, FlowDirection.EndBtoEndA);
        ContractValidationResult<RefuelShiftResult> shift = RefuelShiftTransition.TryApply(
            inventory,
            new ChannelId(0),
            plan,
            CreateInserted(plan, 1100, 15.0),
            10.0,
            15.0);
        AssertValid(shift);
        VersionLifecycleV1 lifecycle = CreateLifecycle(topology, inventory);
        ContractValidationResult<EventLogV1> log = EventLogV1.TryCreate(Array.Empty<EventRecordV1>());
        AssertValid(log);

        ContractValidationResult<RefuelAuditResultV1> audit = RefuelAuditTransitionV1.TryAppendCommitted(
            shift.Value,
            log.Value,
            Id(9100),
            Id(9101),
            40,
            Digest(0x31),
            Digest(0x32),
            lifecycle.CoreStateVersion,
            lifecycle.CreateStateBinding());

        AssertValid(audit);
        RefuelMappingBodyV1 mapping = Assert.IsType<RefuelMappingBodyV1>(audit.Value.MappingEvent.Body.Payload);
        Assert.Equal(ExpectedMovedS8TowardA, NumericIds(mapping.MovedBundleIds));
        Assert.Equal(RefuelDischargeEndV1.EndA, audit.Value.DischargeRecords[0].DischargeEnd);
        Assert.Equal(ExpectedInsertedPositionsS8TowardA,
            mapping.PositionBindings
                .Where(binding => binding.MovementStatus == MovementStatusV1.Inserted)
                .Select(binding => binding.NewBundlePositionOrNA.Value.Value)
                .ToArray());
    }

    [Fact]
    public void RejectsInvalidNamedBodiesAndKeepsExistingEventLogUnchanged()
    {
        ContractValidationResult<RefuelAtomicityBodyV1> missingDigest = RefuelAtomicityBodyV1.TryCreate(
            Id(9200),
            null,
            Digest(0x41),
            CommitStatusV1.Committed,
            OptionalTextV1.NotApplicable,
            OptionalDigest32.NotApplicable);
        AssertInvalid(missingDigest, "RefuelAtomicity.Digest.Missing");

        ContractValidationResult<PositionBindingV1> mismatchedBinding = PositionBindingV1.TryCreate(
            Id(9201),
            OptionalChannelIdV1.NotApplicable,
            OptionalBundlePositionV1.NotApplicable,
            OptionalChannelIdV1.Applicable(new ChannelId(0)),
            OptionalBundlePositionV1.Applicable(new BundlePosition(0)),
            MovementStatusV1.Moved);
        AssertInvalid(mismatchedBinding, "PositionBinding.MovementStatus.MappingMismatch");

        CoreTopology bodyTopology = CreateTopology(FlowDirection.EndAtoEndB);
        BundleInventory bodyInventory = CreateInventory(bodyTopology);
        RefuelSchemePositionPlan bodyPlan = CreatePlan(4, FlowDirection.EndAtoEndB);
        ContractValidationResult<RefuelShiftResult> bodyShift = RefuelShiftTransition.TryApply(
            bodyInventory,
            new ChannelId(0),
            bodyPlan,
            CreateInserted(bodyPlan, 9250, 9.0),
            9.0,
            9.0);
        AssertValid(bodyShift);
        VersionLifecycleV1 bodyLifecycle = CreateLifecycle(bodyTopology, bodyInventory);
        ContractValidationResult<EventLogV1> bodyLog = EventLogV1.TryCreate(Array.Empty<EventRecordV1>());
        AssertValid(bodyLog);
        ContractValidationResult<RefuelAuditResultV1> bodyAudit = RefuelAuditTransitionV1.TryAppendCommitted(
            bodyShift.Value,
            bodyLog.Value,
            Id(9251),
            Id(9252),
            10,
            Digest(0x43),
            Digest(0x44),
            bodyLifecycle.CoreStateVersion,
            bodyLifecycle.CreateStateBinding());
        AssertValid(bodyAudit);
        RefuelMappingBodyV1 validMapping = Assert.IsType<RefuelMappingBodyV1>(
            bodyAudit.Value.MappingEvent.Body.Payload);
        PositionBindingV1 firstReordered = CreateMovedBinding(Id(1), 1, 5);
        PositionBindingV1 secondReordered = CreateMovedBinding(Id(2), 0, 4);
        PositionBindingV1[] reorderedBindings = validMapping.PositionBindings.ToArray();
        reorderedBindings[0] = firstReordered;
        reorderedBindings[1] = secondReordered;
        ContractValidationResult<RefuelMappingBodyV1> reordered = RefuelMappingBodyV1.TryCreate(
            validMapping.SchemeId,
            validMapping.EffectiveTimeSeconds,
            validMapping.ShiftCount,
            validMapping.MovedBundleIds,
            validMapping.InsertedBundleIds,
            validMapping.DischargedBundleIds,
            reorderedBindings);
        AssertInvalid(reordered, "RefuelMapping.PositionBinding.NotAscending");

        ContractValidationResult<EventRecordV1> wrongMappingTime = EventRecordV1.TryCreate(
            EventRankV1.Refuelling,
            12,
            Id(9253),
            bodyAudit.Value.MappingEvent.Owner,
            bodyAudit.Value.MappingEvent.Body,
            10.0,
            bodyLifecycle.CreateStateBinding(),
            CommitStatusV1.Committed,
            null);
        AssertInvalid(wrongMappingTime, "Event.Body.EnvelopeMismatch");

        ContractValidationResult<RefuelAtomicityBodyV1> rolledBackBody = RefuelAtomicityBodyV1.TryCreate(
            Id(9254),
            Digest(0x45),
            Digest(0x46),
            CommitStatusV1.RolledBack,
            OptionalTextV1.Applicable("postcondition"),
            OptionalDigest32.Applicable(Digest(0x45)));
        AssertValid(rolledBackBody);
        ContractValidationResult<EventRecordV1> wrongAtomicityStatus = EventRecordV1.TryCreate(
            EventRankV1.Refuelling,
            13,
            Id(9255),
            bodyAudit.Value.AtomicityEvent.Owner,
            EventBodyV1.FromRefuelAtomicity(rolledBackBody.Value),
            9.0,
            bodyLifecycle.CreateStateBinding(),
            CommitStatusV1.Committed,
            null);
        AssertInvalid(wrongAtomicityStatus, "Event.Body.EnvelopeMismatch");

        CoreTopology topology = CreateTopology(FlowDirection.EndAtoEndB);
        BundleInventory inventory = CreateInventory(topology);
        RefuelSchemePositionPlan plan = CreatePlan(4, FlowDirection.EndAtoEndB);
        ContractValidationResult<RefuelShiftResult> shift = RefuelShiftTransition.TryApply(
            inventory,
            new ChannelId(0),
            plan,
            CreateInserted(plan, 1200, 20.0),
            20.0,
            20.0);
        AssertValid(shift);
        VersionLifecycleV1 lifecycle = CreateLifecycle(topology, inventory);
        ContractValidationResult<EventLogV1> emptyLog = EventLogV1.TryCreate(Array.Empty<EventRecordV1>());
        AssertValid(emptyLog);
        ContractValidationResult<RefuelAuditResultV1> first = RefuelAuditTransitionV1.TryAppendCommitted(
            shift.Value,
            emptyLog.Value,
            Id(9300),
            Id(9301),
            50,
            Digest(0x51),
            Digest(0x52),
            lifecycle.CoreStateVersion,
            lifecycle.CreateStateBinding());
        AssertValid(first);

        ContractValidationResult<RefuelAuditResultV1> duplicate = RefuelAuditTransitionV1.TryAppendCommitted(
            shift.Value,
            first.Value.FinalEventLog,
            Id(9300),
            Id(9301),
            50,
            Digest(0x51),
            Digest(0x52),
            lifecycle.CoreStateVersion,
            lifecycle.CreateStateBinding());
        AssertInvalid(duplicate, "RefuelAudit.Sequence.Duplicate");
        Assert.Equal(2, first.Value.FinalEventLog.Records.Count);
    }

    private static CoreTopology CreateTopology(FlowDirection flowDirection)
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
            boundaries.Add(new BoundaryFaceRecord(
                channelId,
                new BundlePosition((uint)position),
                TopologyFace.East,
                BoundaryClassification.Reflective));
            boundaries.Add(new BoundaryFaceRecord(
                channelId,
                new BundlePosition((uint)position),
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

        ChannelTopology channel = new ChannelTopology(
            channelId,
            0,
            0,
            flowDirection,
            flowDirection == FlowDirection.EndAtoEndB ? new BundlePosition(0) : new BundlePosition(11),
            flowDirection == FlowDirection.EndAtoEndB ? new BundlePosition(11) : new BundlePosition(0),
            neighbors,
            boundaries);
        ContractValidationResult<CoreTopology> result = CoreTopology.TryCreate(1, 12, new[] { channel });
        AssertValid(result);
        return result.Value;
    }

    private static BundleInventory CreateInventory(CoreTopology topology)
    {
        var bundles = Enumerable.Range(1, 12)
            .Select(position => new BundleState(
                Id(position),
                new ChannelId(0),
                new BundlePosition((uint)(position - 1)),
                new MaterialVariantId("MAT-OLD"),
                1000.0 + position,
                10000.0 + position,
                1000.0,
                0.0))
            .ToArray();
        ContractValidationResult<BundleInventory> result = BundleInventory.TryCreate(topology, bundles);
        AssertValid(result);
        return result.Value;
    }

    private static RefuelSchemePositionPlan CreatePlan(ushort shiftCount, FlowDirection flowDirection)
    {
        uint towardEndAFirst = 12u - shiftCount;
        ContractValidationResult<RefuelSchemeDefinition> scheme = RefuelSchemeDefinition.TryCreate(
            "S" + shiftCount.ToString(CultureInfo.InvariantCulture),
            shiftCount,
            "FT-SYN-1000KG",
            RefuelSchemeDefinition.CurrentSchemaVersion,
            Enumerable.Range((int)towardEndAFirst, shiftCount)
                .Select(value => new BundlePosition((uint)value)),
            Enumerable.Range(0, shiftCount)
                .Select(value => new BundlePosition((uint)value)));
        AssertValid(scheme);
        ContractValidationResult<RefuelSchemePositionPlan> plan = scheme.Value.TryCreatePositionPlan(12, flowDirection);
        AssertValid(plan);
        return plan.Value;
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

    private static PositionBindingV1 CreateMovedBinding(StableId bundleId, uint oldPosition, uint newPosition)
    {
        ContractValidationResult<PositionBindingV1> result = PositionBindingV1.TryCreate(
            bundleId,
            OptionalChannelIdV1.Applicable(new ChannelId(0)),
            OptionalBundlePositionV1.Applicable(new BundlePosition(oldPosition)),
            OptionalChannelIdV1.Applicable(new ChannelId(0)),
            OptionalBundlePositionV1.Applicable(new BundlePosition(newPosition)),
            MovementStatusV1.Moved);
        AssertValid(result);
        return result.Value;
    }

    private static VersionLifecycleV1 CreateLifecycle(CoreTopology topology, BundleInventory inventory)
    {
        DataPackDescriptor dataPack = DataPackDescriptor.TryCreate(
            DataPackDescriptor.CurrentSchemaVersion,
            Id(8000),
            "p5-t03-data",
            "P2-T01",
            1,
            12,
            "SI",
            Enumerable.Repeat((byte)0x01, 32).ToArray(),
            Enumerable.Repeat((byte)0x02, 32).ToArray()).Value;
        SimulationConfiguration configuration = SimulationConfiguration.TryCreate(
            SimulationConfiguration.CurrentSchemaVersion,
            topology,
            dataPack,
            0.0,
            100,
            200,
            300).Value;
        ContractValidationResult<VersionLifecycleV1> result = VersionLifecycleV1.TryCreate(
            configuration,
            inventory,
            inventory.EnumerateOccupied()
                .Select(bundle => new BundleNuclideVersionV1(bundle.BundleId, 0, 0)),
            Digest(0x03));
        AssertValid(result);
        return result.Value;
    }

    private static Digest32 Digest(byte value)
    {
        return new Digest32(Enumerable.Repeat(value, 32).ToArray());
    }

    private static StableId Id(int number)
    {
        return StableId.Parse(
            "00000000-0000-0000-0000-" +
            number.ToString("D12", CultureInfo.InvariantCulture));
    }

    private static int[] NumericIds(IEnumerable<StableId> ids)
    {
        return ids.Select(id => int.Parse(id.ToString().AsSpan(24), CultureInfo.InvariantCulture)).ToArray();
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
