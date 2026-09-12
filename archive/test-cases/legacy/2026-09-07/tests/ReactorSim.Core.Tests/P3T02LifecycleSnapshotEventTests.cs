using System;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P3T02LifecycleSnapshotEventTests
{
    [Fact]
    public void LifecycleUsesExplicitInitialCountersAndNotApplicableBindings()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture);

        Assert.Equal(100UL, lifecycle.InitialCoreStateVersion);
        Assert.Equal(100UL, lifecycle.CoreStateVersion);
        Assert.Equal(200UL, lifecycle.SpatialStateVersion);
        Assert.Equal(300UL, lifecycle.PowerSnapshotVersion);
        Assert.Equal(BindingStatusV1.Invalid, lifecycle.SpatialBindingStatus);

        Assert.Equal(BindingStatusV1.Invalid, lifecycle.PowerBindingStatus);
        Assert.False(lifecycle.CreateStateBinding().SpatialStateVersion.IsApplicable);
        Assert.False(lifecycle.CreateStateBinding().PowerSnapshotVersion.IsApplicable);
        Assert.Equal(6, lifecycle.BundleNuclideVersions.Count);
    }

    [Fact]
    public void AcceptedSpatialSolveIncrementsBothCountersAtomically()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture);
        StableId solveId = StableId.Parse("00000000-0000-0000-0000-000000000401");
        StableId snapshotId = StableId.Parse("00000000-0000-0000-0000-000000000402");

        ContractValidationResult<VersionLifecycleV1> accepted = lifecycle.TryAcceptSpatialSolve(
            lifecycle.CoreStateVersion,
            lifecycle.BundleNuclideVersions,
            lifecycle.TopologyVersion,
            lifecycle.DataPackVersion,
            lifecycle.TopologyDigest.Value!,
            lifecycle.DataPackDigest.Value!,
            lifecycle.StateDigest.Value!,
            solveId,
            snapshotId,
            Digest(0x41),
            Digest(0x42),
            0.0);

        Assert.True(accepted.IsValid);
        Assert.Equal(100UL, lifecycle.CoreStateVersion);
        Assert.Equal(200UL, lifecycle.SpatialStateVersion);
        Assert.Equal(300UL, lifecycle.PowerSnapshotVersion);
        Assert.Equal(201UL, accepted.Value.SpatialStateVersion);
        Assert.Equal(301UL, accepted.Value.PowerSnapshotVersion);
        Assert.Equal(BindingStatusV1.Valid, accepted.Value.SpatialBindingStatus);
        Assert.Equal(BindingStatusV1.Valid, accepted.Value.PowerBindingStatus);
        Assert.Equal(solveId, accepted.Value.SpatialSolveId.Value);
        Assert.Equal(snapshotId, accepted.Value.PowerSnapshotId.Value);
        Assert.Equal(201UL, accepted.Value.CreateStateBinding().SpatialStateVersion.Value);
        Assert.Equal(301UL, accepted.Value.CreateStateBinding().PowerSnapshotVersion.Value);
    }

    [Fact]
    public void StaleOrOverflowedSpatialSolveLeavesLifecycleUnchanged()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture);
        ContractValidationResult<VersionLifecycleV1> stale = lifecycle.TryAcceptSpatialSolve(
            lifecycle.CoreStateVersion + 1,
            lifecycle.BundleNuclideVersions,
            lifecycle.TopologyVersion,
            lifecycle.DataPackVersion,
            lifecycle.TopologyDigest.Value!,
            lifecycle.DataPackDigest.Value!,
            lifecycle.StateDigest.Value!,
            StableId.Parse("00000000-0000-0000-0000-000000000411"),
            StableId.Parse("00000000-0000-0000-0000-000000000412"),
            Digest(0x51),
            Digest(0x52),
            0.0);

        Assert.False(stale.IsValid);
        Assert.Equal("VersionLifecycle.SpatialSolve.CoreVersion.Stale", stale.FirstDiagnostic.Code);
        Assert.Equal(200UL, lifecycle.SpatialStateVersion);
        Assert.Equal(300UL, lifecycle.PowerSnapshotVersion);
        Assert.Equal(BindingStatusV1.Invalid, lifecycle.SpatialBindingStatus);

        ContractValidationResult<VersionLifecycleV1> staleProvenance = lifecycle.TryAcceptSpatialSolve(
            lifecycle.CoreStateVersion,
            lifecycle.BundleNuclideVersions,
            "wrong-topology",
            lifecycle.DataPackVersion,
            lifecycle.TopologyDigest.Value!,
            lifecycle.DataPackDigest.Value!,
            lifecycle.StateDigest.Value!,
            StableId.Parse("00000000-0000-0000-0000-000000000415"),
            StableId.Parse("00000000-0000-0000-0000-000000000416"),
            Digest(0x56),
            Digest(0x57),
            0.0);
        Assert.False(staleProvenance.IsValid);
        Assert.Equal("VersionLifecycle.SpatialSolve.TopologyVersion.Stale", staleProvenance.FirstDiagnostic.Code);

        ContractValidationResult<VersionLifecycleV1> staleTime = lifecycle.TryAcceptSpatialSolve(
            lifecycle.CoreStateVersion,
            lifecycle.BundleNuclideVersions,
            lifecycle.TopologyVersion,
            lifecycle.DataPackVersion,
            lifecycle.TopologyDigest.Value!,
            lifecycle.DataPackDigest.Value!,
            lifecycle.StateDigest.Value!,
            StableId.Parse("00000000-0000-0000-0000-000000000417"),
            StableId.Parse("00000000-0000-0000-0000-000000000418"),
            Digest(0x58),
            Digest(0x59),
            1.0);
        Assert.False(staleTime.IsValid);
        Assert.Equal("VersionLifecycle.SpatialSolve.Time.Stale", staleTime.FirstDiagnostic.Code);

        ContractValidationResult<SimulationConfiguration> maxConfiguration = SimulationConfiguration.TryCreate(
            SimulationConfiguration.CurrentSchemaVersion,
            fixture.Topology,
            fixture.DataPack,
            0.0,
            1,
            ulong.MaxValue,
            ulong.MaxValue);
        Assert.True(maxConfiguration.IsValid);
        ContractValidationResult<VersionLifecycleV1> maxLifecycle = VersionLifecycleV1.TryCreate(
            maxConfiguration.Value,
            fixture.Inventory,
            fixture.Inventory.EnumerateOccupied()
                .Select(bundle => new BundleNuclideVersionV1(bundle.BundleId, 0, 0)),
            Digest(0x53));
        Assert.True(maxLifecycle.IsValid);

        ContractValidationResult<VersionLifecycleV1> overflow = maxLifecycle.Value.TryAcceptSpatialSolve(
            maxLifecycle.Value.CoreStateVersion,
            maxLifecycle.Value.BundleNuclideVersions,
            maxLifecycle.Value.TopologyVersion,
            maxLifecycle.Value.DataPackVersion,
            maxLifecycle.Value.TopologyDigest.Value!,
            maxLifecycle.Value.DataPackDigest.Value!,
            maxLifecycle.Value.StateDigest.Value!,
            StableId.Parse("00000000-0000-0000-0000-000000000413"),
            StableId.Parse("00000000-0000-0000-0000-000000000414"),
            Digest(0x54),
            Digest(0x55),
            0.0);

        Assert.False(overflow.IsValid);
        Assert.Equal("VersionLifecycle.SpatialStateVersion.Overflow", overflow.FirstDiagnostic.Code);
        Assert.Equal(ulong.MaxValue, maxLifecycle.Value.SpatialStateVersion);
        Assert.Equal(BindingStatusV1.Invalid, maxLifecycle.Value.SpatialBindingStatus);
    }

    [Fact]
    public void LifecycleRejectsSameSizeStructurallyDifferentTopology()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        ChannelTopology original = fixture.Topology.GetChannel(new ChannelId(0));
        BoundaryFaceRecord[] alteredFaces = original.BoundaryFaces
            .Select((face, index) => index == 0
                ? new BoundaryFaceRecord(face.ChannelId, face.Position, face.Face, BoundaryClassification.Vacuum)
                : face)
            .ToArray();
        ContractValidationResult<CoreTopology> alteredTopology = CoreTopology.TryCreate(
            2,
            3,
            new[]
            {
                new ChannelTopology(
                    original.ChannelId,
                    original.CoordinateX,
                    original.CoordinateY,
                    original.FlowDirection,
                    original.InletPosition,
                    original.OutletPosition,
                    original.Neighbors,
                    alteredFaces),
                fixture.Topology.GetChannel(new ChannelId(1))
            });
        Assert.True(alteredTopology.IsValid);
        ContractValidationResult<BundleInventory> alteredInventory = BundleInventory.TryCreate(
            alteredTopology.Value,
            fixture.Inventory.EnumerateOccupied());
        Assert.True(alteredInventory.IsValid);

        ContractValidationResult<VersionLifecycleV1> result = VersionLifecycleV1.TryCreate(
            fixture.Configuration,
            alteredInventory.Value,
            alteredInventory.Value.EnumerateOccupied()
                .Select(bundle => new BundleNuclideVersionV1(bundle.BundleId, 0, 0)),
            Digest(0x34));
        Assert.False(result.IsValid);
        Assert.Equal("VersionLifecycle.Inventory.TopologyMismatch", result.FirstDiagnostic.Code);
    }

    [Fact]
    public void CoreCommitIncrementsOnlyCoreAndInvalidatesAcceptedSpatialBinding()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture);
        ContractValidationResult<VersionLifecycleV1> accepted = lifecycle.TryAcceptSpatialSolve(
            lifecycle.CoreStateVersion,
            lifecycle.BundleNuclideVersions,
            lifecycle.TopologyVersion,
            lifecycle.DataPackVersion,
            lifecycle.TopologyDigest.Value!,
            lifecycle.DataPackDigest.Value!,
            lifecycle.StateDigest.Value!,
            StableId.Parse("00000000-0000-0000-0000-000000000421"),
            StableId.Parse("00000000-0000-0000-0000-000000000422"),
            Digest(0x61),
            Digest(0x62),
            0.0);
        Assert.True(accepted.IsValid);

        ContractValidationResult<VersionLifecycleV1> committed = accepted.Value.TryCommitCoreState(
            accepted.Value.CoreStateVersion,
            Digest(0x63));

        Assert.True(committed.IsValid);
        Assert.Equal(101UL, committed.Value.CoreStateVersion);
        Assert.Equal(201UL, committed.Value.SpatialStateVersion);
        Assert.Equal(301UL, committed.Value.PowerSnapshotVersion);
        Assert.Equal(BindingStatusV1.Invalid, committed.Value.SpatialBindingStatus);
        Assert.Equal(BindingStatusV1.Invalid, committed.Value.PowerBindingStatus);
        Assert.False(committed.Value.CreateStateBinding().SpatialSolveId.IsApplicable);
        Assert.False(committed.Value.CreateStateBinding().PowerSnapshotId.IsApplicable);
        Assert.Equal(201UL, accepted.Value.SpatialStateVersion);
        Assert.Equal(BindingStatusV1.Valid, accepted.Value.SpatialBindingStatus);
    }

    [Fact]
    public void SnapshotCopiesDigestAndRejectsLifecycleIdentityMismatch()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture);
        ContractValidationResult<VersionLifecycleV1> accepted = lifecycle.TryAcceptSpatialSolve(
            lifecycle.CoreStateVersion,
            lifecycle.BundleNuclideVersions,
            lifecycle.TopologyVersion,
            lifecycle.DataPackVersion,
            lifecycle.TopologyDigest.Value!,
            lifecycle.DataPackDigest.Value!,
            lifecycle.StateDigest.Value!,
            StableId.Parse("00000000-0000-0000-0000-000000000430"),
            StableId.Parse("00000000-0000-0000-0000-000000000431"),
            Digest(0x70),
            Digest(0x71),
            0.0);
        Assert.True(accepted.IsValid);
        byte[] digestBytes = Enumerable.Repeat((byte)0x71, 32).ToArray();
        ContractValidationResult<StateSnapshotV1> snapshot = StateSnapshotV1.TryCreate(
            StableId.Parse("00000000-0000-0000-0000-000000000431"),
            accepted.Value.PowerSnapshotVersion,
            0.0,
            fixture.Inventory,
            accepted.Value,
            new Digest32(digestBytes));
        digestBytes[0] = 0xff;

        Assert.True(snapshot.IsValid);
        Assert.Equal((byte)0x71, snapshot.Value.SnapshotDigest.Bytes[0]);
        Assert.Equal(accepted.Value.CoreStateVersion, snapshot.Value.StateBinding.CoreStateVersion);
        Assert.True(snapshot.Value.StateBinding.SpatialStateVersion.IsApplicable);

        BundleState original = fixture.Inventory.Get(new NodeKey(new ChannelId(0), new BundlePosition(0)))!;
        BundleState different = new BundleState(
            StableId.Parse("00000000-0000-0000-0000-0000000004ff"),
            original.ChannelId,
            original.Position,
            original.MaterialVariantId,
            original.InitialBurnupJPerKgHm,
            original.CumulativeFissionEnergyJ,
            original.HeavyMetalMassKg,
            original.InsertedAtSeconds);
        ContractValidationResult<BundleInventory> alteredInventory = BundleInventory.TryCreate(
            fixture.Topology,
            fixture.Inventory.EnumerateOccupied()
                .Where(bundle => bundle.BundleId != original.BundleId)
                .Concat(new[] { different }));
        Assert.True(alteredInventory.IsValid);
        ContractValidationResult<StateSnapshotV1> mismatch = StateSnapshotV1.TryCreate(
            StableId.Parse("00000000-0000-0000-0000-000000000431"),
            accepted.Value.PowerSnapshotVersion,
            0.0,
            alteredInventory.Value,
            accepted.Value,
            Digest(0x71));
        Assert.False(mismatch.IsValid);
        Assert.Equal("StateSnapshot.StateMismatch", mismatch.FirstDiagnostic.Code);

        ContractValidationResult<StateSnapshotV1> initialSnapshot = StateSnapshotV1.TryCreate(
            StableId.Parse("00000000-0000-0000-0000-000000000432"),
            lifecycle.PowerSnapshotVersion,
            0.0,
            fixture.Inventory,
            lifecycle,
            Digest(0x72));
        Assert.False(initialSnapshot.IsValid);
        Assert.Equal("StateSnapshot.Binding.Mismatch", initialSnapshot.FirstDiagnostic.Code);
    }

    [Fact]
    public void EventAndDiagnosticCollectionsUseCanonicalOrderingAndAtomicAppend()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture);
        StateBindingV1 binding = lifecycle.CreateStateBinding();
        EventOwnerV1 owner = EventOwnerV1.NotApplicable;
        EventBodyV1 body = EventBodyV1.NotApplicable;
        ContractValidationResult<EventOwnerV1> invalidOwner = EventOwnerV1.TryForOpaque(
            (EventOwnerKindV1)99,
            new byte[] { 1 });
        Assert.False(invalidOwner.IsValid);
        Assert.Equal("EventOwner.Kind.Invalid", invalidOwner.FirstDiagnostic.Code);
        Assert.True(EventOwnerV1.TryForChannel(fixture.Topology, new ChannelId(1)).IsValid);
        ContractValidationResult<EventOwnerV1> invalidChannel = EventOwnerV1.TryForChannel(
            fixture.Topology,
            new ChannelId(uint.MaxValue));
        Assert.False(invalidChannel.IsValid);
        Assert.Equal("EventOwner.Channel.OutOfRange", invalidChannel.FirstDiagnostic.Code);
        ContractValidationResult<EventRecordV1> later = EventRecordV1.TryCreate(
            EventRankV1.SpatialSolve,
            2,
            StableId.Parse("00000000-0000-0000-0000-000000000442"),
            owner,
            body,
            10.0,
            binding,
            CommitStatusV1.Committed,
            null);
        ContractValidationResult<EventRecordV1> earlier = EventRecordV1.TryCreate(
            EventRankV1.Burnup,
            1,
            StableId.Parse("00000000-0000-0000-0000-000000000441"),
            owner,
            body,
            10.0,
            binding,
            CommitStatusV1.Committed,
            null);
        Assert.True(later.IsValid);
        Assert.True(earlier.IsValid);

        ContractValidationResult<EventLogV1> log = EventLogV1.TryCreate(new[] { later.Value, earlier.Value });
        Assert.True(log.IsValid);
        Assert.Equal(earlier.Value.EventId, log.Value.Records[0].EventId);
        Assert.Equal(later.Value.EventId, log.Value.Records[1].EventId);
        ContractValidationResult<EventLogV1> duplicate = log.Value.TryAppend(earlier.Value);
        Assert.False(duplicate.IsValid);
        Assert.Equal(2, log.Value.Records.Count);

        ContractValidationResult<EventRecordV1> sameIdLater = EventRecordV1.TryCreate(
            EventRankV1.SpatialSolve,
            3,
            earlier.Value.EventId,
            owner,
            body,
            20.0,
            binding,
            CommitStatusV1.Committed,
            null);
        Assert.True(sameIdLater.IsValid);
        ContractValidationResult<EventLogV1> nonAdjacentDuplicate = EventLogV1.TryCreate(
            new[] { sameIdLater.Value, later.Value, earlier.Value });
        Assert.False(nonAdjacentDuplicate.IsValid);
        Assert.Equal("EventLog.EventId.Duplicate", nonAdjacentDuplicate.FirstDiagnostic.Code);

        DiagnosticRecordV1 first = new DiagnosticRecordV1(
            DiagnosticSeverityV1.Error,
            1,
            "State.Invalid",
            "state",
            "invalid state",
            OptionalStableId.NotApplicable,
            OptionalStableId.NotApplicable);
        DiagnosticRecordV1 laterDiagnostic = new DiagnosticRecordV1(
            DiagnosticSeverityV1.Fatal,
            2,
            "State.Fatal",
            "state",
            "fatal state",
            OptionalStableId.NotApplicable,
            OptionalStableId.NotApplicable);
        ContractValidationResult<DiagnosticLogV1> diagnostics = DiagnosticLogV1.TryCreate(
            new[] { laterDiagnostic, first });
        Assert.True(diagnostics.IsValid);
        Assert.Same(first, diagnostics.Value.FirstFailure);

        ContractValidationResult<EventRecordV1> rejected = EventRecordV1.TryCreate(
            EventRankV1.Refuelling,
            4,
            StableId.Parse("00000000-0000-0000-0000-000000000443"),
            owner,
            body,
            10.0,
            binding,
            CommitStatusV1.Rejected,
            first);
        Assert.True(rejected.IsValid);
        ContractValidationResult<EventLogV1> rejectedLog = EventLogV1.TryCreate(new[] { rejected.Value });
        Assert.False(rejectedLog.IsValid);
        Assert.Equal("EventLog.CommitStatus.Invalid", rejectedLog.FirstDiagnostic.Code);

        DiagnosticRecordV1 warning = new DiagnosticRecordV1(
            DiagnosticSeverityV1.Warning,
            3,
            "State.Warning",
            "state",
            "warning only",
            OptionalStableId.NotApplicable,
            OptionalStableId.NotApplicable);
        ContractValidationResult<EventRecordV1> warningOnly = EventRecordV1.TryCreate(
            EventRankV1.Refuelling,
            5,
            StableId.Parse("00000000-0000-0000-0000-000000000444"),
            owner,
            body,
            10.0,
            binding,
            CommitStatusV1.Rejected,
            warning);
        Assert.False(warningOnly.IsValid);
        Assert.Equal("Event.Diagnostic.Missing", warningOnly.FirstDiagnostic.Code);

        ContractValidationResult<EventRecordV1> notApplicable = EventRecordV1.TryCreate(
            EventRankV1.Refuelling,
            6,
            StableId.Parse("00000000-0000-0000-0000-000000000445"),
            owner,
            body,
            10.0,
            binding,
            CommitStatusV1.NotApplicable,
            null);
        Assert.True(notApplicable.IsValid);
        ContractValidationResult<EventLogV1> notApplicableLog = EventLogV1.TryCreate(new[] { notApplicable.Value });
        Assert.False(notApplicableLog.IsValid);
        Assert.Equal("EventLog.CommitStatus.Invalid", notApplicableLog.FirstDiagnostic.Code);
    }

    private static VersionLifecycleV1 CreateLifecycle(SyntheticCoreFixture fixture)
    {
        ContractValidationResult<VersionLifecycleV1> result = VersionLifecycleV1.TryCreate(
            fixture.Configuration,
            fixture.Inventory,
            fixture.Inventory.EnumerateOccupied()
                .Select(bundle => new BundleNuclideVersionV1(bundle.BundleId, 0, 0)),
            Digest(0x31));
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private static Digest32 Digest(byte value)
    {
        return new Digest32(Enumerable.Repeat(value, 32).ToArray());
    }
}
