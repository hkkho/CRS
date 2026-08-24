using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P3T03CommandClockTests
{
    [Fact]
    public void ClockUsesExplicitTimeAndIsImmutable()
    {
        ContractValidationResult<SimulationClockV1> created = SimulationClockV1.TryCreate(
            SimulationClockV1.CurrentSchemaVersion,
            0.0,
            7);
        Assert.True(created.IsValid);

        ContractValidationResult<SimulationClockV1> advanced = created.Value.TryAdvanceBy(2.5);
        Assert.True(advanced.IsValid);
        Assert.Equal(0.0, created.Value.CurrentSimulationTimeSeconds);
        Assert.Equal(7UL, created.Value.StepIndex);
        Assert.Equal(2.5, advanced.Value.CurrentSimulationTimeSeconds);
        Assert.Equal(8UL, advanced.Value.StepIndex);

        ContractValidationResult<SimulationClockV1> targeted = advanced.Value.TryAdvanceTo(10.0);
        Assert.True(targeted.IsValid);
        Assert.Equal(10.0, targeted.Value.CurrentSimulationTimeSeconds);
        Assert.Equal(9UL, targeted.Value.StepIndex);

        ContractValidationResult<SimulationClockV1> sameTime = targeted.Value.TryAdvanceTo(10.0);
        Assert.True(sameTime.IsValid);
        Assert.Same(targeted.Value, sameTime.Value);
    }

    [Fact]
    public void ClockRejectsInvalidBackwardAndOverflowAdvances()
    {
        ContractValidationResult<SimulationClockV1> invalid = SimulationClockV1.TryCreate(
            SimulationClockV1.CurrentSchemaVersion,
            double.NaN,
            0);
        Assert.False(invalid.IsValid);
        Assert.Equal("SimulationClock.Time.Invalid", invalid.FirstDiagnostic.Code);

        SimulationClockV1 clock = SimulationClockV1.TryCreate(
            SimulationClockV1.CurrentSchemaVersion,
            2.0,
            0).Value;
        ContractValidationResult<SimulationClockV1> zero = clock.TryAdvanceBy(0.0);
        Assert.False(zero.IsValid);
        Assert.Equal("SimulationClock.Advance.Interval.Invalid", zero.FirstDiagnostic.Code);

        ContractValidationResult<SimulationClockV1> backward = clock.TryAdvanceTo(1.0);
        Assert.False(backward.IsValid);
        Assert.Equal("SimulationClock.TargetTime.Backward", backward.FirstDiagnostic.Code);

        ContractValidationResult<SimulationClockV1> noProgress = clock.TryAdvanceBy(double.Epsilon);
        Assert.False(noProgress.IsValid);
        Assert.Equal("SimulationClock.Time.NoProgress", noProgress.FirstDiagnostic.Code);
        Assert.Equal(2.0, clock.CurrentSimulationTimeSeconds);
        Assert.Equal(0UL, clock.StepIndex);

        SimulationClockV1 maxStep = SimulationClockV1.TryCreate(
            SimulationClockV1.CurrentSchemaVersion,
            0.0,
            ulong.MaxValue).Value;
        ContractValidationResult<SimulationClockV1> stepOverflow = maxStep.TryAdvanceBy(1.0);
        Assert.False(stepOverflow.IsValid);
        Assert.Equal("SimulationClock.StepIndex.Overflow", stepOverflow.FirstDiagnostic.Code);

        SimulationClockV1 maxTime = SimulationClockV1.TryCreate(
            SimulationClockV1.CurrentSchemaVersion,
            double.MaxValue,
            0).Value;
        ContractValidationResult<SimulationClockV1> timeOverflow = maxTime.TryAdvanceBy(double.MaxValue);
        Assert.False(timeOverflow.IsValid);
        Assert.Equal("SimulationClock.Time.Overflow", timeOverflow.FirstDiagnostic.Code);
        Assert.Equal(double.MaxValue, maxTime.CurrentSimulationTimeSeconds);
    }

    [Fact]
    public void QueueCanonicalizesCommandsAndReleasesOnlyAtExactDueTime()
    {
        VersionLifecycleV1 lifecycle = CreateLifecycle();
        SimulationClockV1 clock = CreateClock(0.0, 0);
        SimulationCommandV1 later = CreateCommand(
            lifecycle,
            "00000000-0000-0000-0000-000000000501",
            EventRankV1.SpatialSolve,
            10,
            0.0,
            2.0);
        SimulationCommandV1 earlier = CreateCommand(
            lifecycle,
            "00000000-0000-0000-0000-000000000502",
            EventRankV1.Burnup,
            11,
            0.0,
            1.0);

        ContractValidationResult<CommandQueueV1> created = CommandQueueV1.TryCreate(
            10,
            12,
            new[] { later.CommandId, earlier.CommandId },
            new[] { later, earlier },
            clock);
        Assert.True(created.IsValid);
        Assert.Equal(earlier.CommandId, created.Value.PendingCommands[0].CommandId);
        Assert.Equal(later.CommandId, created.Value.PendingCommands[1].CommandId);
        Assert.Equal(1.0, created.Value.NextDueTimeSeconds);
        Assert.Equal(2, created.Value.AllocatedCommandIds.Count);

        SimulationClockV1 atOne = clock.TryAdvanceTo(1.0).Value;
        ContractValidationResult<CommandQueueReleaseV1> released = created.Value.TryReleaseDue(atOne);
        Assert.True(released.IsValid);
        Assert.Single(released.Value.ReleasedCommands);
        Assert.Equal(earlier.CommandId, released.Value.ReleasedCommands[0].CommandId);
        Assert.Single(released.Value.Queue.PendingCommands);
        Assert.Equal(2, released.Value.Queue.AllocatedCommandIds.Count);
        Assert.Equal(12UL, released.Value.Queue.NextSequence);
        Assert.Equal(1.0, released.Value.Queue.CurrentSimulationTimeSeconds);
        Assert.Equal(2, created.Value.PendingCommands.Count);

        SimulationClockV1 atOnePointFive = clock.TryAdvanceTo(1.5).Value;
        ContractValidationResult<CommandQueueReleaseV1> missed = created.Value.TryReleaseDue(atOnePointFive);
        Assert.False(missed.IsValid);
        Assert.Equal("CommandQueue.DueTime.Missed", missed.FirstDiagnostic.Code);
        Assert.Equal(2, created.Value.PendingCommands.Count);

        ContractValidationResult<CommandQueueReleaseV1> backward = released.Value.Queue.TryReleaseDue(clock);
        Assert.False(backward.IsValid);
        Assert.Equal("CommandQueue.Clock.Backward", backward.FirstDiagnostic.Code);

        SimulationClockV1 atTwo = atOne.TryAdvanceTo(2.0).Value;
        ContractValidationResult<CommandQueueReleaseV1> releasedLater = released.Value.Queue.TryReleaseDue(atTwo);
        Assert.True(releasedLater.IsValid);
        Assert.Single(releasedLater.Value.ReleasedCommands);
        Assert.Equal(later.CommandId, releasedLater.Value.ReleasedCommands[0].CommandId);
        Assert.Empty(releasedLater.Value.Queue.PendingCommands);
    }

    [Fact]
    public void EnqueueIsAtomicAndRequiresExactClockAndNextSequence()
    {
        VersionLifecycleV1 lifecycle = CreateLifecycle();
        SimulationClockV1 clock = CreateClock(0.0, 0);
        ContractValidationResult<CommandQueueV1> empty = CommandQueueV1.TryCreate(
            20,
            20,
            Array.Empty<StableId>(),
            Array.Empty<SimulationCommandV1>(),
            clock);
        Assert.True(empty.IsValid);

        SimulationCommandV1 stale = CreateCommand(
            lifecycle,
            "00000000-0000-0000-0000-000000000503",
            EventRankV1.Refuelling,
            20,
            1.0,
            2.0);
        ContractValidationResult<CommandQueueV1> staleResult = empty.Value.TryEnqueue(clock, stale);
        Assert.False(staleResult.IsValid);
        Assert.Equal("CommandQueue.EnqueueTime.Stale", staleResult.FirstDiagnostic.Code);
        Assert.Empty(empty.Value.AllocatedCommandIds);
        Assert.Empty(empty.Value.PendingCommands);

        SimulationClockV1 sameTimeDifferentStep = CreateClock(0.0, 1);
        SimulationCommandV1 sameTimeCommand = CreateCommand(
            lifecycle,
            "00000000-0000-0000-0000-00000000050b",
            EventRankV1.Refuelling,
            20,
            0.0,
            2.0);
        ContractValidationResult<CommandQueueV1> boundaryMismatch = empty.Value.TryEnqueue(
            sameTimeDifferentStep,
            sameTimeCommand);
        Assert.False(boundaryMismatch.IsValid);
        Assert.Equal("CommandQueue.Clock.BoundaryMismatch", boundaryMismatch.FirstDiagnostic.Code);
        Assert.Empty(empty.Value.AllocatedCommandIds);

        SimulationCommandV1 wrongSequence = CreateCommand(
            lifecycle,
            "00000000-0000-0000-0000-000000000504",
            EventRankV1.Refuelling,
            19,
            0.0,
            2.0);
        ContractValidationResult<CommandQueueV1> wrongSequenceResult = empty.Value.TryEnqueue(clock, wrongSequence);
        Assert.False(wrongSequenceResult.IsValid);
        Assert.Equal("CommandQueue.Sequence.Expected", wrongSequenceResult.FirstDiagnostic.Code);
        Assert.Empty(empty.Value.AllocatedCommandIds);

        SimulationCommandV1 valid = CreateCommand(
            lifecycle,
            "00000000-0000-0000-0000-000000000505",
            EventRankV1.Refuelling,
            20,
            0.0,
            2.0);
        ContractValidationResult<CommandQueueV1> appended = empty.Value.TryEnqueue(clock, valid);
        Assert.True(appended.IsValid);
        Assert.Equal(21UL, appended.Value.NextSequence);
        Assert.Single(appended.Value.AllocatedCommandIds);
        Assert.Empty(empty.Value.PendingCommands);
    }

    [Fact]
    public void QueueRejectsMalformedPendingStateAndSequenceOverflow()
    {
        VersionLifecycleV1 lifecycle = CreateLifecycle();
        SimulationClockV1 clock = CreateClock(0.0, 0);
        SimulationCommandV1 command = CreateCommand(
            lifecycle,
            "00000000-0000-0000-0000-000000000506",
            EventRankV1.Refuelling,
            3,
            0.0,
            2.0);

        ContractValidationResult<CommandQueueV1> countMismatch = CommandQueueV1.TryCreate(
            3,
            4,
            Array.Empty<StableId>(),
            new[] { command },
            clock);
        Assert.False(countMismatch.IsValid);
        Assert.Equal("CommandQueue.Sequence.CountMismatch", countMismatch.FirstDiagnostic.Code);

        ContractValidationResult<CommandQueueV1> duplicateIds = CommandQueueV1.TryCreate(
            3,
            5,
            new[] { command.CommandId, command.CommandId },
            new[] { command },
            clock);
        Assert.False(duplicateIds.IsValid);
        Assert.Equal("CommandQueue.CommandId.Duplicate", duplicateIds.FirstDiagnostic.Code);

        SimulationClockV1 atThree = clock.TryAdvanceTo(3.0).Value;
        ContractValidationResult<CommandQueueV1> stalePending = CommandQueueV1.TryCreate(
            3,
            4,
            new[] { command.CommandId },
            new[] { command },
            atThree);
        Assert.False(stalePending.IsValid);
        Assert.Equal("CommandQueue.DueTime.Stale", stalePending.FirstDiagnostic.Code);

        ContractValidationResult<CommandQueueV1> maxQueue = CommandQueueV1.TryCreate(
            ulong.MaxValue,
            ulong.MaxValue,
            Array.Empty<StableId>(),
            Array.Empty<SimulationCommandV1>(),
            clock);
        Assert.True(maxQueue.IsValid);
        SimulationCommandV1 maxCommand = CreateCommand(
            lifecycle,
            "00000000-0000-0000-0000-000000000507",
            EventRankV1.Refuelling,
            ulong.MaxValue,
            0.0,
            2.0);
        ContractValidationResult<CommandQueueV1> sequenceOverflow = maxQueue.Value.TryEnqueue(clock, maxCommand);
        Assert.False(sequenceOverflow.IsValid);
        Assert.Equal("CommandQueue.Sequence.Overflow", sequenceOverflow.FirstDiagnostic.Code);
        Assert.Empty(maxQueue.Value.AllocatedCommandIds);
    }

    [Fact]
    public void ClockAndCommandTimesRejectSignedNegativeZero()
    {
        ContractValidationResult<SimulationClockV1> clock = SimulationClockV1.TryCreate(
            SimulationClockV1.CurrentSchemaVersion,
            -0.0,
            0);
        Assert.False(clock.IsValid);
        Assert.Equal("SimulationClock.Time.Invalid", clock.FirstDiagnostic.Code);

        VersionLifecycleV1 lifecycle = CreateLifecycle();
        ContractValidationResult<SimulationCommandV1> enqueueNegativeZero = SimulationCommandV1.TryCreate(
            EventRankV1.Refuelling,
            0,
            StableId.Parse("00000000-0000-0000-0000-00000000050c"),
            EventOwnerV1.NotApplicable,
            EventBodyV1.NotApplicable,
            -0.0,
            0.0,
            lifecycle.CreateStateBinding());
        Assert.False(enqueueNegativeZero.IsValid);
        Assert.Equal("SimulationCommand.EnqueueTime.Invalid", enqueueNegativeZero.FirstDiagnostic.Code);

        ContractValidationResult<SimulationCommandV1> dueNegativeZero = SimulationCommandV1.TryCreate(
            EventRankV1.Refuelling,
            0,
            StableId.Parse("00000000-0000-0000-0000-00000000050d"),
            EventOwnerV1.NotApplicable,
            EventBodyV1.NotApplicable,
            0.0,
            -0.0,
            lifecycle.CreateStateBinding());
        Assert.False(dueNegativeZero.IsValid);
        Assert.Equal("SimulationCommand.DueTime.Invalid", dueNegativeZero.FirstDiagnostic.Code);
    }

    [Fact]
    public void QueueRejectsNonAdjacentDuplicatePendingIdentityAndSequence()
    {
        VersionLifecycleV1 lifecycle = CreateLifecycle();
        SimulationClockV1 clock = CreateClock(0.0, 0);
        SimulationCommandV1 first = CreateCommand(
            lifecycle,
            "00000000-0000-0000-0000-000000000508",
            EventRankV1.Burnup,
            8,
            0.0,
            1.0);
        SimulationCommandV1 second = CreateCommand(
            lifecycle,
            "00000000-0000-0000-0000-000000000509",
            EventRankV1.Refuelling,
            9,
            0.0,
            2.0);
        SimulationCommandV1 duplicateIdentity = CreateCommand(
            lifecycle,
            "00000000-0000-0000-0000-000000000508",
            EventRankV1.SpatialSolve,
            9,
            0.0,
            3.0);

        ContractValidationResult<CommandQueueV1> duplicateIdentityResult = CommandQueueV1.TryCreate(
            8,
            10,
            new[] { first.CommandId, second.CommandId },
            new[] { first, second, duplicateIdentity },
            clock);
        Assert.False(duplicateIdentityResult.IsValid);
        Assert.Equal("CommandQueue.PendingCommand.Duplicate", duplicateIdentityResult.FirstDiagnostic.Code);

        SimulationCommandV1 uniqueThird = CreateCommand(
            lifecycle,
            "00000000-0000-0000-0000-00000000050a",
            EventRankV1.SpatialSolve,
            9,
            0.0,
            3.0);
        ContractValidationResult<CommandQueueV1> duplicateSequenceResult = CommandQueueV1.TryCreate(
            8,
            11,
            new[] { first.CommandId, second.CommandId, uniqueThird.CommandId },
            new[] { first, second, uniqueThird },
            clock);
        Assert.False(duplicateSequenceResult.IsValid);
        Assert.Equal("CommandQueue.Sequence.Duplicate", duplicateSequenceResult.FirstDiagnostic.Code);
    }

    [Fact]
    public void QueueReleasesEqualDueCommandsInCanonicalRankThenSequenceOrder()
    {
        VersionLifecycleV1 lifecycle = CreateLifecycle();
        SimulationClockV1 clock = CreateClock(0.0, 0);
        SimulationCommandV1 spatial = CreateCommand(
            lifecycle,
            "00000000-0000-0000-0000-000000000510",
            EventRankV1.SpatialSolve,
            30,
            0.0,
            1.0);
        SimulationCommandV1 burnup = CreateCommand(
            lifecycle,
            "00000000-0000-0000-0000-000000000511",
            EventRankV1.Burnup,
            32,
            0.0,
            1.0);
        SimulationCommandV1 refuelling = CreateCommand(
            lifecycle,
            "00000000-0000-0000-0000-000000000512",
            EventRankV1.Refuelling,
            31,
            0.0,
            1.0);

        ContractValidationResult<CommandQueueV1> created = CommandQueueV1.TryCreate(
            30,
            33,
            new[] { spatial.CommandId, burnup.CommandId, refuelling.CommandId },
            new[] { spatial, refuelling, burnup },
            clock);
        Assert.True(created.IsValid);

        CommandQueueReleaseV1 released = created.Value
            .TryReleaseDue(clock.TryAdvanceTo(1.0).Value)
            .Value;

        Assert.Equal(
            new[] { burnup.CommandId, refuelling.CommandId, spatial.CommandId },
            released.ReleasedCommands.Select(command => command.CommandId));
    }

    [Fact]
    public void QueueCopiesCallerCollectionsAndKeepsInvalidIdDiagnosticsCanonical()
    {
        VersionLifecycleV1 lifecycle = CreateLifecycle();
        SimulationClockV1 clock = CreateClock(0.0, 0);
        SimulationCommandV1 command = CreateCommand(
            lifecycle,
            "00000000-0000-0000-0000-000000000513",
            EventRankV1.Refuelling,
            0,
            0.0,
            1.0);
        StableId validId = command.CommandId;
        List<StableId> allocatedIds = new List<StableId> { validId };
        List<SimulationCommandV1> pendingCommands = new List<SimulationCommandV1> { command };

        ContractValidationResult<CommandQueueV1> created = CommandQueueV1.TryCreate(
            0,
            1,
            allocatedIds,
            pendingCommands,
            clock);
        Assert.True(created.IsValid);

        allocatedIds.Clear();
        pendingCommands.Clear();
        Assert.Single(created.Value.AllocatedCommandIds);
        Assert.Single(created.Value.PendingCommands);

        ContractValidationResult<CommandQueueV1> invalidFirst = CommandQueueV1.TryCreate(
            0,
            1,
            new[] { validId, StableId.Empty },
            Array.Empty<SimulationCommandV1>(),
            clock);
        ContractValidationResult<CommandQueueV1> invalidSecond = CommandQueueV1.TryCreate(
            0,
            1,
            new[] { StableId.Empty, validId },
            Array.Empty<SimulationCommandV1>(),
            clock);

        Assert.False(invalidFirst.IsValid);
        Assert.False(invalidSecond.IsValid);
        Assert.Equal("CommandQueue.CommandId.Empty", invalidFirst.FirstDiagnostic.Code);
        Assert.Equal(invalidFirst.FirstDiagnostic.Code, invalidSecond.FirstDiagnostic.Code);
        Assert.Equal(invalidFirst.FirstDiagnostic.Path, invalidSecond.FirstDiagnostic.Path);
        Assert.Equal("allocated_command_ids[0]", invalidFirst.FirstDiagnostic.Path);
    }

    private static VersionLifecycleV1 CreateLifecycle()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
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

    private static SimulationCommandV1 CreateCommand(
        VersionLifecycleV1 lifecycle,
        string commandId,
        EventRankV1 rank,
        ulong sequence,
        double enqueueTime,
        double dueTime)
    {
        ContractValidationResult<SimulationCommandV1> result = SimulationCommandV1.TryCreate(
            rank,
            sequence,
            StableId.Parse(commandId),
            EventOwnerV1.NotApplicable,
            EventBodyV1.NotApplicable,
            enqueueTime,
            dueTime,
            lifecycle.CreateStateBinding());
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private static Digest32 Digest(byte value)
    {
        return new Digest32(Enumerable.Repeat(value, 32).ToArray());
    }
}
