using System;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P3T05CommandApplicationTests
{
    [Fact]
    public void AppliesAllDueCommandsAsOneImmutableEventBatch()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture);
        StateBindingV1 binding = lifecycle.CreateStateBinding();
        SimulationClockV1 initialClock = CreateClock(0.0, 0);
        StableId firstId = StableId.Parse("00000000-0000-0000-0000-000000000701");
        StableId secondId = StableId.Parse("00000000-0000-0000-0000-000000000702");
        SimulationCommandV1 first = CreateCommand(firstId, EventRankV1.SpatialSolve, 1, binding);
        SimulationCommandV1 second = CreateCommand(secondId, EventRankV1.Burnup, 2, binding);
        ContractValidationResult<CommandQueueV1> queue = CommandQueueV1.TryCreate(
            1,
            3,
            new[] { firstId, secondId },
            new[] { first, second },
            initialClock);
        Assert.True(queue.IsValid, queue.IsValid ? string.Empty : queue.FirstDiagnostic.ToString());
        ContractValidationResult<EventLogV1> log = EventLogV1.TryCreate(Array.Empty<EventRecordV1>());
        Assert.True(log.IsValid);
        SimulationClockV1 dueClock = CreateClock(1.0, 1);

        ContractValidationResult<CommandApplicationResultV1> applied = CommandApplicationV1.TryApply(
            queue.Value,
            log.Value,
            dueClock,
            fixture.Configuration,
            binding);

        Assert.True(applied.IsValid, applied.IsValid ? string.Empty : applied.FirstDiagnostic.ToString());
        Assert.Equal(2, applied.Value.AppliedEvents.Count);
        Assert.Equal(EventRankV1.Burnup, applied.Value.AppliedEvents[0].EventRank);
        Assert.Equal(EventRankV1.SpatialSolve, applied.Value.AppliedEvents[1].EventRank);
        Assert.Equal(secondId, applied.Value.AppliedEvents[0].EventId);
        Assert.Equal(firstId, applied.Value.AppliedEvents[1].EventId);
        Assert.All(applied.Value.AppliedEvents, record => Assert.Equal(CommitStatusV1.Committed, record.CommitStatus));
        Assert.Empty(applied.Value.FinalQueue.PendingCommands);
        Assert.Equal(1.0, applied.Value.FinalQueue.CurrentSimulationTimeSeconds);
        Assert.Equal(2, applied.Value.FinalEventLog.Records.Count);
        Assert.Equal(2, queue.Value.PendingCommands.Count);
    }

    [Fact]
    public void DuplicateEventIdentityRejectsWithoutChangingQueueOrLog()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture);
        StateBindingV1 binding = lifecycle.CreateStateBinding();
        StableId commandId = StableId.Parse("00000000-0000-0000-0000-000000000703");
        SimulationCommandV1 command = CreateCommand(commandId, EventRankV1.Burnup, 1, binding);
        SimulationClockV1 initialClock = CreateClock(0.0, 0);
        ContractValidationResult<CommandQueueV1> queue = CommandQueueV1.TryCreate(
            1,
            2,
            new[] { commandId },
            new[] { command },
            initialClock);
        Assert.True(queue.IsValid);
        ContractValidationResult<EventRecordV1> existingRecord = EventRecordV1.TryCreate(
            command.EventRank,
            command.Sequence,
            command.CommandId,
            command.Owner,
            command.Body,
            1.0,
            command.StateBinding,
            CommitStatusV1.Committed,
            null);
        Assert.True(existingRecord.IsValid);
        ContractValidationResult<EventLogV1> log = EventLogV1.TryCreate(new[] { existingRecord.Value });
        Assert.True(log.IsValid);

        ContractValidationResult<CommandApplicationResultV1> rejected = CommandApplicationV1.TryApply(
            queue.Value,
            log.Value,
            CreateClock(1.0, 1),
            fixture.Configuration,
            binding);

        Assert.False(rejected.IsValid);
        Assert.Equal("EventLog.EventId.Duplicate", rejected.FirstDiagnostic.Code);
        Assert.Single(queue.Value.PendingCommands);
        Assert.Single(log.Value.Records);
        Assert.Equal(0.0, queue.Value.CurrentSimulationTimeSeconds);
    }

    [Fact]
    public void StaleBindingRejectsBeforeAnyQueueTransition()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture);
        StateBindingV1 staleBinding = lifecycle.CreateStateBinding();
        ContractValidationResult<VersionLifecycleV1> advancedLifecycle = lifecycle.TryCommitCoreState(
            lifecycle.CoreStateVersion,
            Digest(0x71));
        Assert.True(advancedLifecycle.IsValid);
        StateBindingV1 currentBinding = advancedLifecycle.Value.CreateStateBinding();
        StableId commandId = StableId.Parse("00000000-0000-0000-0000-000000000704");
        SimulationCommandV1 command = CreateCommand(commandId, EventRankV1.Burnup, 1, staleBinding);
        ContractValidationResult<CommandQueueV1> queue = CommandQueueV1.TryCreate(
            1,
            2,
            new[] { commandId },
            new[] { command },
            CreateClock(0.0, 0));
        Assert.True(queue.IsValid);
        ContractValidationResult<EventLogV1> log = EventLogV1.TryCreate(Array.Empty<EventRecordV1>());
        Assert.True(log.IsValid);

        ContractValidationResult<CommandApplicationResultV1> rejected = CommandApplicationV1.TryApply(
            queue.Value,
            log.Value,
            CreateClock(1.0, 1),
            fixture.Configuration,
            currentBinding);

        Assert.False(rejected.IsValid);
        Assert.Equal("CommandApplication.StateBinding.Mismatch", rejected.FirstDiagnostic.Code);
        Assert.Single(queue.Value.PendingCommands);
        Assert.Equal(0.0, queue.Value.CurrentSimulationTimeSeconds);
        Assert.Empty(log.Value.Records);
    }

    [Fact]
    public void MissedDueBoundaryFailsClosedWithoutPartialRelease()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture);
        StateBindingV1 binding = lifecycle.CreateStateBinding();
        StableId commandId = StableId.Parse("00000000-0000-0000-0000-000000000705");
        SimulationCommandV1 command = CreateCommand(commandId, EventRankV1.Burnup, 1, binding);
        ContractValidationResult<CommandQueueV1> queue = CommandQueueV1.TryCreate(
            1,
            2,
            new[] { commandId },
            new[] { command },
            CreateClock(0.0, 0));
        Assert.True(queue.IsValid);
        ContractValidationResult<EventLogV1> log = EventLogV1.TryCreate(Array.Empty<EventRecordV1>());
        Assert.True(log.IsValid);

        ContractValidationResult<CommandApplicationResultV1> rejected = CommandApplicationV1.TryApply(
            queue.Value,
            log.Value,
            CreateClock(2.0, 1),
            fixture.Configuration,
            binding);

        Assert.False(rejected.IsValid);
        Assert.Equal("CommandQueue.DueTime.Missed", rejected.FirstDiagnostic.Code);
        Assert.Single(queue.Value.PendingCommands);
        Assert.Equal(0.0, queue.Value.CurrentSimulationTimeSeconds);
        Assert.Empty(log.Value.Records);
    }

    [Fact]
    public void ForeignExistingEventLogRejectsWithoutMixingAuthorities()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture);
        StateBindingV1 localBinding = lifecycle.CreateStateBinding();
        StableId commandId = StableId.Parse("00000000-0000-0000-0000-000000000707");
        SimulationCommandV1 command = CreateCommand(commandId, EventRankV1.Burnup, 1, localBinding);
        ContractValidationResult<CommandQueueV1> queue = CommandQueueV1.TryCreate(
            1,
            2,
            new[] { commandId },
            new[] { command },
            CreateClock(0.0, 0));
        Assert.True(queue.IsValid);

        ContractValidationResult<DataPackDescriptor> alternateDataPack = DataPackDescriptor.TryCreate(
            DataPackDescriptor.CurrentSchemaVersion,
            fixture.DataPack.DataPackId,
            "alternate-p3-t05-data-pack",
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
        ContractValidationResult<VersionLifecycleV1> foreignLifecycle = VersionLifecycleV1.TryCreate(
            alternateConfiguration.Value,
            fixture.Inventory,
            fixture.Inventory.EnumerateOccupied()
                .Select(bundle => new BundleNuclideVersionV1(bundle.BundleId, 0, 0)),
            Digest(0x72));
        Assert.True(foreignLifecycle.IsValid);
        ContractValidationResult<EventRecordV1> foreignRecord = EventRecordV1.TryCreate(
            EventRankV1.Burnup,
            1,
            StableId.Parse("00000000-0000-0000-0000-000000000706"),
            EventOwnerV1.NotApplicable,
            EventBodyV1.NotApplicable,
            0.0,
            foreignLifecycle.Value.CreateStateBinding(),
            CommitStatusV1.Committed,
            null);
        Assert.True(foreignRecord.IsValid);
        ContractValidationResult<EventLogV1> log = EventLogV1.TryCreate(new[] { foreignRecord.Value });
        Assert.True(log.IsValid);

        ContractValidationResult<CommandApplicationResultV1> rejected = CommandApplicationV1.TryApply(
            queue.Value,
            log.Value,
            CreateClock(1.0, 1),
            fixture.Configuration,
            localBinding);

        Assert.False(rejected.IsValid);
        Assert.Equal("CommandApplication.EventLog.StateBinding.ProvenanceMismatch", rejected.FirstDiagnostic.Code);
        Assert.Single(queue.Value.PendingCommands);
        Assert.Single(log.Value.Records);
        Assert.Equal(0.0, queue.Value.CurrentSimulationTimeSeconds);
    }

    [Fact]
    public void MissingApplicationInputsFailClosedDeterministically()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        VersionLifecycleV1 lifecycle = CreateLifecycle(fixture);
        StateBindingV1 binding = lifecycle.CreateStateBinding();
        ContractValidationResult<EventLogV1> log = EventLogV1.TryCreate(Array.Empty<EventRecordV1>());
        Assert.True(log.IsValid);
        ContractValidationResult<CommandQueueV1> queue = CommandQueueV1.TryCreate(
            1,
            1,
            Array.Empty<StableId>(),
            Array.Empty<SimulationCommandV1>(),
            CreateClock(0.0, 0));
        Assert.True(queue.IsValid);

        ContractValidationResult<CommandApplicationResultV1> missingQueue = CommandApplicationV1.TryApply(
            null!,
            log.Value,
            CreateClock(0.0, 0),
            fixture.Configuration,
            binding);
        ContractValidationResult<CommandApplicationResultV1> missingEventLog = CommandApplicationV1.TryApply(
            queue.Value,
            null!,
            CreateClock(0.0, 0),
            fixture.Configuration,
            binding);
        ContractValidationResult<CommandApplicationResultV1> missingBinding = CommandApplicationV1.TryApply(
            queue.Value,
            log.Value,
            CreateClock(0.0, 0),
            fixture.Configuration,
            null!);

        Assert.False(missingQueue.IsValid);
        Assert.Equal("CommandApplication.Queue.Missing", missingQueue.FirstDiagnostic.Code);
        Assert.False(missingEventLog.IsValid);
        Assert.Equal("CommandApplication.EventLog.Missing", missingEventLog.FirstDiagnostic.Code);
        Assert.False(missingBinding.IsValid);
        Assert.Equal("CommandApplication.StateBinding.Missing", missingBinding.FirstDiagnostic.Code);
    }

    private static SimulationCommandV1 CreateCommand(
        StableId commandId,
        EventRankV1 rank,
        ulong sequence,
        StateBindingV1 binding)
    {
        ContractValidationResult<SimulationCommandV1> result = SimulationCommandV1.TryCreate(
            rank,
            sequence,
            commandId,
            EventOwnerV1.NotApplicable,
            EventBodyV1.NotApplicable,
            0.0,
            1.0,
            binding);
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
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

    private static SimulationClockV1 CreateClock(double time, ulong step)
    {
        ContractValidationResult<SimulationClockV1> result = SimulationClockV1.TryCreate(
            SimulationClockV1.CurrentSchemaVersion,
            time,
            step);
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private static Digest32 Digest(byte value)
    {
        return new Digest32(Enumerable.Repeat(value, 32).ToArray());
    }
}
