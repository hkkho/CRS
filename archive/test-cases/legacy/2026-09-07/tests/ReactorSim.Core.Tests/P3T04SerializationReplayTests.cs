using System;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P3T04SerializationReplayTests
{
    [Fact]
    public void StateArchiveRoundTripsAcceptedSnapshotWithCanonicalBytes()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture);
        StableId solveId = StableId.Parse("00000000-0000-0000-0000-000000000501");
        StableId snapshotId = StableId.Parse("00000000-0000-0000-0000-000000000502");
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
            Digest(0x51),
            Digest(0x52),
            0.0);
        Assert.True(accepted.IsValid, accepted.IsValid ? string.Empty : accepted.FirstDiagnostic.ToString());

        ContractValidationResult<StateSnapshotV1> snapshot = StateSnapshotV1.TryCreate(
            snapshotId,
            accepted.Value.PowerSnapshotVersion,
            0.0,
            fixture.Inventory,
            accepted.Value,
            Digest(0x52));
        Assert.True(snapshot.IsValid, snapshot.IsValid ? string.Empty : snapshot.FirstDiagnostic.ToString());

        ContractValidationResult<string> serialized = StateArchiveCodecV1.Serialize(snapshot.Value);
        Assert.True(serialized.IsValid, serialized.IsValid ? string.Empty : serialized.FirstDiagnostic.ToString());

        ContractValidationResult<StateSnapshotV1> restored = StateArchiveCodecV1.Deserialize(
            serialized.Value,
            fixture.Configuration);
        Assert.True(restored.IsValid, restored.IsValid ? string.Empty : restored.FirstDiagnostic.ToString());
        Assert.Equal(snapshot.Value.SnapshotId, restored.Value.SnapshotId);
        Assert.Equal(snapshot.Value.SnapshotVersion, restored.Value.SnapshotVersion);
        Assert.Equal(snapshot.Value.SnapshotDigest.Bytes, restored.Value.SnapshotDigest.Bytes);
        Assert.Equal(snapshot.Value.Inventory.OccupiedCount, restored.Value.Inventory.OccupiedCount);
        Assert.Equal(snapshot.Value.Lifecycle.CoreStateVersion, restored.Value.Lifecycle.CoreStateVersion);
        Assert.True(restored.Value.StateBinding.PowerSnapshotId.IsApplicable);

        ContractValidationResult<string> serializedAgain = StateArchiveCodecV1.Serialize(restored.Value);
        Assert.True(serializedAgain.IsValid, serializedAgain.IsValid ? string.Empty : serializedAgain.FirstDiagnostic.ToString());
        Assert.Equal(serialized.Value, serializedAgain.Value);
    }

    [Fact]
    public void StateArchiveRejectsUnsupportedOrderVersionAndSignedNegativeZero()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        StateSnapshotV1 snapshot = CreateSnapshot(fixture);
        ContractValidationResult<string> serialized = StateArchiveCodecV1.Serialize(snapshot);
        Assert.True(serialized.IsValid);

        string reordered = serialized.Value.Replace(
            "\"schema_id\":\"ReactorSim.StateArchiveV2\",\"schema_version\":2",
            "\"schema_version\":2,\"schema_id\":\"ReactorSim.StateArchiveV2\",");
        ContractValidationResult<StateSnapshotV1> reorderedResult = StateArchiveCodecV1.Deserialize(
            reordered,
            fixture.Configuration);
        Assert.False(reorderedResult.IsValid);

        string commented = serialized.Value.Insert(1, "/*comment*/");
        ContractValidationResult<StateSnapshotV1> commentedResult = StateArchiveCodecV1.Deserialize(
            commented,
            fixture.Configuration);
        Assert.False(commentedResult.IsValid);

        string unsupported = serialized.Value.Replace(
            "\"schema_id\":\"ReactorSim.StateArchiveV2\",\"schema_version\":2",
            "\"schema_id\":\"ReactorSim.StateArchiveV2\",\"schema_version\":3");
        ContractValidationResult<StateSnapshotV1> unsupportedResult = StateArchiveCodecV1.Deserialize(
            unsupported,
            fixture.Configuration);
        Assert.False(unsupportedResult.IsValid);
        Assert.Equal("Serialization.Schema.Unsupported", unsupportedResult.FirstDiagnostic.Code);

        string negativeZero = serialized.Value
            .Replace("\"simulation_time_s\":0.0", "\"simulation_time_s\":-0.0")
            .Replace("\"simulation_time_s\":0,", "\"simulation_time_s\":-0.0,");
        Assert.Contains("-0.0", negativeZero);
        ContractValidationResult<StateSnapshotV1> negativeZeroResult = StateArchiveCodecV1.Deserialize(
            negativeZero,
            fixture.Configuration);
        Assert.False(negativeZeroResult.IsValid);
    }

    [Fact]
    public void CommandReplayRoundTripsAndRepeatsDeterministically()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture);
        ContractValidationResult<SimulationClockV1> clock = SimulationClockV1.TryCreate(1, 0.0, 0);
        Assert.True(clock.IsValid);
        StableId commandId = StableId.Parse("00000000-0000-0000-0000-000000000601");
        ContractValidationResult<SimulationCommandV1> command = SimulationCommandV1.TryCreate(
            EventRankV1.ControllerCommandGeneration,
            10,
            commandId,
            EventOwnerV1.NotApplicable,
            EventBodyV1.NotApplicable,
            0.0,
            2.5,
            lifecycle.CreateStateBinding());
        Assert.True(command.IsValid, command.IsValid ? string.Empty : command.FirstDiagnostic.ToString());

        ContractValidationResult<CommandQueueV1> initialQueue = CommandQueueV1.TryCreate(
            10,
            10,
            Array.Empty<StableId>(),
            Array.Empty<SimulationCommandV1>(),
            clock.Value);
        Assert.True(initialQueue.IsValid, initialQueue.IsValid ? string.Empty : initialQueue.FirstDiagnostic.ToString());
        ContractValidationResult<SimulationClockV1> releaseClock = SimulationClockV1.TryCreate(1, 2.5, 1);
        Assert.True(releaseClock.IsValid);

        ContractValidationResult<CommandReplayEntryV1> enqueue = CommandReplayEntryV1.TryEnqueue(command.Value);
        ContractValidationResult<CommandReplayEntryV1> release = CommandReplayEntryV1.TryReleaseDue(
            releaseClock.Value,
            new[] { commandId });
        Assert.True(enqueue.IsValid);
        Assert.True(release.IsValid);
        ContractValidationResult<CommandReplayLogV1> log = CommandReplayLogV1.TryCreate(
            new[] { enqueue.Value, release.Value });
        Assert.True(log.IsValid);

        ContractValidationResult<CommandReplayResultV1> firstReplay = log.Value.TryReplay(initialQueue.Value);
        ContractValidationResult<CommandReplayResultV1> secondReplay = log.Value.TryReplay(initialQueue.Value);
        Assert.True(firstReplay.IsValid, firstReplay.IsValid ? string.Empty : firstReplay.FirstDiagnostic.ToString());
        Assert.True(secondReplay.IsValid, secondReplay.IsValid ? string.Empty : secondReplay.FirstDiagnostic.ToString());
        Assert.Equal(firstReplay.Value.FinalQueue.NextSequence, secondReplay.Value.FinalQueue.NextSequence);
        Assert.Equal(firstReplay.Value.FinalQueue.CurrentStepIndex, secondReplay.Value.FinalQueue.CurrentStepIndex);
        Assert.Equal(commandId, firstReplay.Value.ReleasedCommandIdsByEntry[1].Single());
        Assert.Equal(firstReplay.Value.ReleasedCommandIdsByEntry[1], secondReplay.Value.ReleasedCommandIdsByEntry[1]);

        StateBindingV1 replayBinding = lifecycle.CreateStateBinding();
        ContractValidationResult<string> serialized = CommandReplayCodecV1.Serialize(
            initialQueue.Value,
            log.Value,
            fixture.Configuration,
            replayBinding);
        Assert.True(serialized.IsValid, serialized.IsValid ? string.Empty : serialized.FirstDiagnostic.ToString());
        ContractValidationResult<CommandReplayArchiveV1> restored = CommandReplayCodecV1.Deserialize(
            serialized.Value,
            fixture.Configuration,
            replayBinding);
        Assert.True(restored.IsValid, restored.IsValid ? string.Empty : restored.FirstDiagnostic.ToString());
        ContractValidationResult<CommandReplayResultV1> restoredReplay = restored.Value.Log.TryReplay(
            restored.Value.InitialQueue);
        Assert.True(restoredReplay.IsValid, restoredReplay.IsValid ? string.Empty : restoredReplay.FirstDiagnostic.ToString());
        Assert.Empty(restoredReplay.Value.FinalQueue.PendingCommands);

        ContractValidationResult<string> serializedAgain = CommandReplayCodecV1.Serialize(
            restored.Value.InitialQueue,
            restored.Value.Log,
            fixture.Configuration,
            replayBinding);
        Assert.True(serializedAgain.IsValid, serializedAgain.IsValid ? string.Empty : serializedAgain.FirstDiagnostic.ToString());
        Assert.Equal(serialized.Value, serializedAgain.Value);
    }

    [Fact]
    public void CommandReplayRejectsOutOfRangeOwnerAndStaleBinding()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture);
        StateBindingV1 replayBinding = lifecycle.CreateStateBinding();
        ContractValidationResult<SimulationClockV1> clock = SimulationClockV1.TryCreate(1, 0.0, 0);
        Assert.True(clock.IsValid);
        StableId commandId = StableId.Parse("00000000-0000-0000-0000-000000000604");
        ContractValidationResult<SimulationCommandV1> command = SimulationCommandV1.TryCreate(
            EventRankV1.ControllerCommandGeneration,
            1,
            commandId,
            EventOwnerV1.NotApplicable,
            EventBodyV1.NotApplicable,
            0.0,
            1.0,
            replayBinding);
        Assert.True(command.IsValid);
        ContractValidationResult<CommandQueueV1> queue = CommandQueueV1.TryCreate(
            1,
            1,
            Array.Empty<StableId>(),
            Array.Empty<SimulationCommandV1>(),
            clock.Value);
        Assert.True(queue.IsValid);
        ContractValidationResult<CommandReplayEntryV1> enqueue = CommandReplayEntryV1.TryEnqueue(command.Value);
        Assert.True(enqueue.IsValid);
        ContractValidationResult<CommandReplayLogV1> log = CommandReplayLogV1.TryCreate(new[] { enqueue.Value });
        Assert.True(log.IsValid);
        ContractValidationResult<string> serialized = CommandReplayCodecV1.Serialize(
            queue.Value,
            log.Value,
            fixture.Configuration,
            replayBinding);
        Assert.True(serialized.IsValid, serialized.IsValid ? string.Empty : serialized.FirstDiagnostic.ToString());

        string outOfRangeOwner = serialized.Value.Replace(
            "\"kind\":255,\"channel_id\":0,\"key_hex\":\"\"",
            "\"kind\":0,\"channel_id\":999,\"key_hex\":\"\"");
        ContractValidationResult<CommandReplayArchiveV1> ownerResult = CommandReplayCodecV1.Deserialize(
            outOfRangeOwner,
            fixture.Configuration,
            replayBinding);
        Assert.False(ownerResult.IsValid);
        Assert.Equal("EventOwner.Channel.OutOfRange", ownerResult.FirstDiagnostic.Code);

        string staleBinding = serialized.Value.Replace(
            "\"core_state_version\":100",
            "\"core_state_version\":999");
        ContractValidationResult<CommandReplayArchiveV1> bindingResult = CommandReplayCodecV1.Deserialize(
            staleBinding,
            fixture.Configuration,
            replayBinding);
        Assert.False(bindingResult.IsValid);
        Assert.Equal("Serialization.Input.Invalid", bindingResult.FirstDiagnostic.Code);

        ContractValidationResult<DataPackDescriptor> alternateDataPack = DataPackDescriptor.TryCreate(
            DataPackDescriptor.CurrentSchemaVersion,
            fixture.DataPack.DataPackId,
            "alternate-p3-t04-data-pack",
            fixture.DataPack.TopologySchemaId,
            fixture.DataPack.ChannelCount,
            fixture.DataPack.BundlePositionCount,
            fixture.DataPack.UnitsProfileId,
            fixture.DataPack.TopologyDigest.ToArray(),
            fixture.DataPack.ContentDigest.ToArray());
        Assert.True(alternateDataPack.IsValid);
        ContractValidationResult<SimulationConfiguration> alternateConfiguration = SimulationConfiguration.TryCreate(
            SimulationConfiguration.CurrentSchemaVersion,
            fixture.Topology,
            alternateDataPack.Value,
            0.0,
            100,
            200,
            300);
        Assert.True(alternateConfiguration.IsValid);
        ContractValidationResult<CommandReplayArchiveV1> provenanceResult = CommandReplayCodecV1.Deserialize(
            serialized.Value,
            alternateConfiguration.Value,
            replayBinding);
        Assert.False(provenanceResult.IsValid);
        Assert.Equal("CommandReplayArchive.StateBinding.ProvenanceMismatch", provenanceResult.FirstDiagnostic.Code);

        ContractValidationResult<DataPackDescriptor> alternateDigestDataPack = DataPackDescriptor.TryCreate(
            DataPackDescriptor.CurrentSchemaVersion,
            fixture.DataPack.DataPackId,
            fixture.DataPack.DataPackVersion,
            fixture.DataPack.TopologySchemaId,
            fixture.DataPack.ChannelCount,
            fixture.DataPack.BundlePositionCount,
            fixture.DataPack.UnitsProfileId,
            Digest(0x71).Bytes.ToArray(),
            Digest(0x72).Bytes.ToArray());
        Assert.True(alternateDigestDataPack.IsValid);
        ContractValidationResult<SimulationConfiguration> alternateDigestConfiguration = SimulationConfiguration.TryCreate(
            SimulationConfiguration.CurrentSchemaVersion,
            fixture.Topology,
            alternateDigestDataPack.Value,
            0.0,
            100,
            200,
            300);
        Assert.True(alternateDigestConfiguration.IsValid);
        ContractValidationResult<CommandReplayArchiveV1> digestResult = CommandReplayCodecV1.Deserialize(
            serialized.Value,
            alternateDigestConfiguration.Value,
            replayBinding);
        Assert.False(digestResult.IsValid);
        Assert.Equal("Serialization.Input.Invalid", digestResult.FirstDiagnostic.Code);
        Assert.Equal("command_replay_archive.topology_digest", digestResult.FirstDiagnostic.Path);
    }

    [Fact]
    public void CommandReplayRejectsRecordedReleaseOrderMismatch()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture);
        ContractValidationResult<SimulationClockV1> clock = SimulationClockV1.TryCreate(1, 0.0, 0);
        Assert.True(clock.IsValid);
        StableId commandId = StableId.Parse("00000000-0000-0000-0000-000000000602");
        ContractValidationResult<SimulationCommandV1> command = SimulationCommandV1.TryCreate(
            EventRankV1.Burnup,
            1,
            commandId,
            EventOwnerV1.NotApplicable,
            EventBodyV1.NotApplicable,
            0.0,
            1.0,
            lifecycle.CreateStateBinding());
        Assert.True(command.IsValid);
        ContractValidationResult<CommandQueueV1> queue = CommandQueueV1.TryCreate(
            1,
            1,
            Array.Empty<StableId>(),
            Array.Empty<SimulationCommandV1>(),
            clock.Value);
        Assert.True(queue.IsValid);
        ContractValidationResult<CommandReplayEntryV1> enqueue = CommandReplayEntryV1.TryEnqueue(command.Value);
        ContractValidationResult<SimulationClockV1> releaseClock = SimulationClockV1.TryCreate(1, 1.0, 1);
        Assert.True(releaseClock.IsValid);
        ContractValidationResult<CommandReplayEntryV1> wrongRelease = CommandReplayEntryV1.TryReleaseDue(
            releaseClock.Value,
            new[] { StableId.Parse("00000000-0000-0000-0000-000000000603") });
        Assert.True(enqueue.IsValid);
        Assert.True(wrongRelease.IsValid);
        ContractValidationResult<CommandReplayLogV1> log = CommandReplayLogV1.TryCreate(
            new[] { enqueue.Value, wrongRelease.Value });
        Assert.True(log.IsValid);

        ContractValidationResult<CommandReplayResultV1> replay = log.Value.TryReplay(queue.Value);
        Assert.False(replay.IsValid);
        Assert.Equal("CommandReplay.Release.Ids.Mismatch", replay.FirstDiagnostic.Code);
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

    private static StateSnapshotV1 CreateSnapshot(SyntheticCoreFixture fixture)
    {
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture);
        StableId snapshotId = StableId.Parse("00000000-0000-0000-0000-000000000512");
        ContractValidationResult<VersionLifecycleV1> accepted = lifecycle.TryAcceptSpatialSolve(
            lifecycle.CoreStateVersion,
            lifecycle.BundleNuclideVersions,
            lifecycle.TopologyVersion,
            lifecycle.DataPackVersion,
            lifecycle.TopologyDigest.Value!,
            lifecycle.DataPackDigest.Value!,
            lifecycle.StateDigest.Value!,
            StableId.Parse("00000000-0000-0000-0000-000000000511"),
            snapshotId,
            Digest(0x61),
            Digest(0x62),
            0.0);
        Assert.True(accepted.IsValid, accepted.IsValid ? string.Empty : accepted.FirstDiagnostic.ToString());
        ContractValidationResult<StateSnapshotV1> snapshot = StateSnapshotV1.TryCreate(
            snapshotId,
            accepted.Value.PowerSnapshotVersion,
            0.0,
            fixture.Inventory,
            accepted.Value,
            Digest(0x62));
        Assert.True(snapshot.IsValid, snapshot.IsValid ? string.Empty : snapshot.FirstDiagnostic.ToString());
        return snapshot.Value;
    }

    private static Digest32 Digest(byte value)
    {
        return new Digest32(Enumerable.Repeat(value, 32).ToArray());
    }
}
