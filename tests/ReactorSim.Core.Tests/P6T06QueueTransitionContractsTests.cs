using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P6T06QueueTransitionContractsTests
{
    private static readonly double[] OneSecond = { 1.0 };
    private static readonly double[] TwoSeconds = { 2.0 };
    private static readonly double[] HalfSecond = { 0.5 };
    private static readonly ulong[] FirstTwoSequences = { 0UL, 1UL };
    private static readonly ulong[] FirstThreeSequences = { 0UL, 1UL, 2UL };

    [Fact]
    public void RrsEnqueueAndMotionAreCausalAndPreserveBinding()
    {
        StableId queueId = Id(0xd601);
        P6T06OwnerKeyV1 owner = Require(
            P6T06OwnerKeyV1.TryForFamily(P6T06QueueFamilyV1.Rrs, RrsFixtureV1.ControllerId));
        P6T06TargetKeyV1 total = Require(
            P6T06TargetKeyV1.TryForRrsActuator(RrsFixtureV1.TotalPowerActuatorId));
        P6T06TargetKeyV1 tilt = Require(
            P6T06TargetKeyV1.TryForRrsActuator(RrsFixtureV1.TiltActuatorId));
        P6T06AvailableCommandV1 totalAvailable = Require(
            P6T06AvailableCommandV1.TryCreate(
                P6T06QueueFamilyV1.Rrs,
                total,
                0.2,
                0.0,
                1.0,
                0.1));
        P6T06AvailableCommandV1 tiltAvailable = Require(
            P6T06AvailableCommandV1.TryCreate(
                P6T06QueueFamilyV1.Rrs,
                tilt,
                0.2,
                0.0,
                1.0,
                0.1));
        P6T06QueueStateV1 queue = CreateQueue(
            P6T06QueueFamilyV1.Rrs,
            queueId,
            owner,
            1.0,
            8.0,
            totalAvailable,
            tiltAvailable);
        Digest32 frozenMeasurementBinding = Digest(0x11);

        P6T06CommandCandidateV1 candidate = Require(
            P6T06CommandCandidateV1.TryCreate(
                P6T06QueueFamilyV1.Rrs,
                total,
                P6T06SourceKindV1.Controller,
                EventRankV1.ControllerCommandGeneration,
                0.0,
                0.9,
                0.0,
                1.0,
                0.1));
        P6T06EnqueueResultV1 enqueue = Require(
            queue.TryEnqueueBatch(
                Binding(0xd611, frozenMeasurementBinding),
                10.0,
                Require(queue.TryCreatePhaseToken(10.0)),
                new[] { candidate }));

        Assert.Equal(10.0, enqueue.Commands[0].DueTimeSeconds);
        Assert.Equal(frozenMeasurementBinding, enqueue.Commands[0].SourceStateBindingDigest);
        Assert.Equal(0.9, enqueue.Commands[0].BoundedCommand);
        Assert.False(enqueue.Commands[0].IsSaturated);
        Assert.Equal(10.0, enqueue.Commands[0].EnqueueTimeSeconds);
        Assert.Equal(1UL, enqueue.Queue.NextSequence);
        Assert.Single(enqueue.Queue.PendingCommands);
        Assert.Equal(queue.QueueDigest, enqueue.Transition.QueueBeforeDigest);
        Assert.Equal(enqueue.Queue.QueueDigest, enqueue.Transition.QueueAfterDigest);
        Assert.Equal(
            enqueue.Commands[0].CommandId,
            P6T06QueueCommandV1.DeriveCommandId(
                enqueue.Commands[0].Family,
                enqueue.Commands[0].QueueId,
                enqueue.Commands[0].SourceEventId,
                enqueue.Commands[0].OwnerKey,
                enqueue.Commands[0].Target,
                enqueue.Commands[0].Sequence));
        Assert.NotEmpty(enqueue.Commands[0].ToCanonicalBytes());
        Assert.NotEmpty(enqueue.Transition.ToCanonicalBytes());

        P6T06ActuatorStateV1 totalState = Require(
            P6T06ActuatorStateV1.TryCreate(
                P6T06QueueFamilyV1.Rrs,
                total,
                0.0,
                0.1));
        P6T06ActuatorStateV1 tiltState = Require(
            P6T06ActuatorStateV1.TryCreate(
                P6T06QueueFamilyV1.Rrs,
                tilt,
                0.2,
                0.1));
        P6T06MotionResultV1 motion = Require(
            enqueue.Queue.TryMotionAndConsume(
                10.0,
                P6T06MotionModeV1.Automatic,
                new[] { tiltState, totalState },
                TwoSeconds));

        P6T06ActuatorStateV1 nextTotal = motion.ActuatorStates.Single(
            state => state.Target.Equals(total));
        P6T06AvailableCommandV1 availableAfter = motion.Queue.AvailableCommands.Single(
            command => command.Target.Equals(total));
        Assert.Equal(0.2, nextTotal.PhysicalState, 12);
        Assert.Equal(0.9, availableAfter.BoundedCommand, 12);
        Assert.Equal(10.0, motion.Queue.LastMotionTimeSeconds);
        Assert.Empty(motion.Queue.PendingCommands);
        Assert.Single(motion.ConsumedCommands);
        Assert.Equal(0.2, motion.MotionDiagnostics.Single(
            diagnostic => diagnostic.Target.Equals(total)).AvailableBefore.BoundedCommand, 12);
        Assert.Equal(0.9, motion.MotionDiagnostics.Single(
            diagnostic => diagnostic.Target.Equals(total)).AvailableAfter.BoundedCommand, 12);
        Assert.Equal(P6T06QueueTransitionKindV1.MotionAndConsume, motion.Transition.TransitionKind);
        P6T06MotionDiagnosticV1 totalDiagnostic = motion.MotionDiagnostics.Single(
            diagnostic => diagnostic.Target.Equals(total));
        Assert.Equal(queue.QueueId, totalDiagnostic.QueueId);
        Assert.Equal(32, totalDiagnostic.StateDigest.Bytes.Count);
        Assert.NotEmpty(totalDiagnostic.ToCanonicalBytes());

        P6T06MotionResultV1 nextBoundary = Require(
            motion.Queue.TryMotionAndConsume(
                12.0,
                P6T06MotionModeV1.Automatic,
                motion.ActuatorStates,
                TwoSeconds));
        Assert.Equal(0.4, nextBoundary.ActuatorStates.Single(
            state => state.Target.Equals(total)).PhysicalState, 12);
        Assert.Empty(nextBoundary.ConsumedCommands);
        Assert.Equal(12.0, nextBoundary.Queue.LastMotionTimeSeconds);

        P6T06MotionResultV1 held = Require(
            nextBoundary.Queue.TryMotionAndConsume(
                14.0,
                P6T06MotionModeV1.Held,
                nextBoundary.ActuatorStates,
                TwoSeconds));
        Assert.Equal(0.4, held.ActuatorStates.Single(
            state => state.Target.Equals(total)).PhysicalState, 12);
        Assert.Equal(14.0, held.Queue.LastMotionTimeSeconds);
        Assert.NotEmpty(held.Transition.ToCanonicalBytes());
    }

    [Fact]
    public void SameTimeOrderingSaturationAndReplayAreDeterministic()
    {
        P6T06QueueStateV1 first = CreateRrsQueueAtTimeZero();
        P6T06QueueStateV1 second = CreateRrsQueueAtTimeZero();
        P6T06OwnerKeyV1 owner = first.OwnerKey;
        P6T06TargetKeyV1 total = Require(
            P6T06TargetKeyV1.TryForRrsActuator(RrsFixtureV1.TotalPowerActuatorId));
        P6T06TargetKeyV1 tilt = Require(
            P6T06TargetKeyV1.TryForRrsActuator(RrsFixtureV1.TiltActuatorId));

        P6T06CommandCandidateV1 totalCandidate = Require(
            P6T06CommandCandidateV1.TryCreate(
                P6T06QueueFamilyV1.Rrs,
                total,
                P6T06SourceKindV1.Manual,
                EventRankV1.ControllerCommandGeneration,
                0.0,
                1.5,
                0.0,
                1.0,
                0.1));
        P6T06CommandCandidateV1 tiltCandidate = Require(
            P6T06CommandCandidateV1.TryCreate(
                P6T06QueueFamilyV1.Rrs,
                tilt,
                P6T06SourceKindV1.Manual,
                EventRankV1.ControllerCommandGeneration,
                0.0,
                0.6,
                0.0,
                1.0,
                0.1));

        P6T06EnqueueResultV1 firstEnqueue = Require(
            first.TryEnqueueBatch(
                Binding(0xd621, Digest(0x21)),
                0.0,
                Require(first.TryCreatePhaseToken(0.0)),
                new[] { tiltCandidate, totalCandidate }));
        P6T06EnqueueResultV1 secondEnqueue = Require(
            second.TryEnqueueBatch(
                Binding(0xd621, Digest(0x21)),
                0.0,
                Require(second.TryCreatePhaseToken(0.0)),
                new[] { totalCandidate, tiltCandidate }));

        Assert.Equal(firstEnqueue.Queue.ToCanonicalBytes(), secondEnqueue.Queue.ToCanonicalBytes());
        Assert.Equal(firstEnqueue.Commands.Select(command => command.CommandId),
            secondEnqueue.Commands.Select(command => command.CommandId));
        Assert.Equal(P6T06SaturationStateV1.UpperBound,
            firstEnqueue.SaturationDiagnostics.Single(
                diagnostic => diagnostic.CommandId == firstEnqueue.Commands.Single(
                    command => command.Target.Equals(total)).CommandId).SaturationState);
        Assert.True(firstEnqueue.SaturationDiagnostics.Single(
            diagnostic => diagnostic.CommandId == firstEnqueue.Commands.Single(
                command => command.Target.Equals(total)).CommandId).Saturated);

        P6T06CommandCandidateV1 lowerCandidate = Require(
            P6T06CommandCandidateV1.TryCreate(
                P6T06QueueFamilyV1.Rrs,
                total,
                P6T06SourceKindV1.Scheduled,
                EventRankV1.ControllerCommandGeneration,
                0.0,
                0.1,
                0.0,
                1.0,
                0.1));
        P6T06EnqueueResultV1 secondBatch = Require(
            firstEnqueue.Queue.TryEnqueueBatch(
                Binding(0xd622, Digest(0x22)),
                0.0,
                Require(firstEnqueue.Queue.TryCreatePhaseToken(0.0)),
                new[] { lowerCandidate }));
        Assert.Equal(FirstThreeSequences,
            secondBatch.Queue.PendingCommands.Select(command => command.Sequence).ToArray());

        P6T06MotionResultV1 consumed = Require(
            secondBatch.Queue.TryMotionAndConsume(
                0.0,
                P6T06MotionModeV1.Manual,
                new[]
                {
                    Require(P6T06ActuatorStateV1.TryCreate(
                        P6T06QueueFamilyV1.Rrs,
                        total,
                        0.5,
                        0.1)),
                    Require(P6T06ActuatorStateV1.TryCreate(
                        P6T06QueueFamilyV1.Rrs,
                        tilt,
                        0.5,
                        0.1))
                },
                Array.Empty<double>()));
        Assert.Equal(FirstThreeSequences,
            consumed.ConsumedCommands.Select(command => command.Sequence).ToArray());
        Assert.Equal(0.1, consumed.Queue.AvailableCommands.Single(
            command => command.Target.Equals(total)).BoundedCommand, 12);
        Assert.Equal(0.5, consumed.ActuatorStates.Single(
            state => state.Target.Equals(total)).PhysicalState, 12);

        ContractValidationResult<P6T06EnqueueResultV1> late = consumed.Queue.TryEnqueueBatch(
            Binding(0xd623, Digest(0x23)),
            0.0,
            Require(consumed.Queue.TryCreatePhaseToken(0.0)).CloseDueBatchCutoff(),
            new[] { lowerCandidate });
        Assert.False(late.IsValid);
        Assert.Equal("P6T06.Enqueue.CutoffClosed", late.FirstDiagnostic.Code);
    }

    [Fact]
    public void BranchQueuesUseRankFourAndTheSameCausalTransitionContract()
    {
        P6T06TargetKeyV1 zone = Require(P6T06TargetKeyV1.TryForLiquidZone(3));
        P6T06OwnerKeyV1 zoneOwner = Require(
            P6T06OwnerKeyV1.TryForFamily(
                P6T06QueueFamilyV1.LiquidZone,
                Id(0xd631)));
        P6T06QueueStateV1 zoneQueue = CreateQueue(
            P6T06QueueFamilyV1.LiquidZone,
            Id(0xd632),
            zoneOwner,
            null,
            0.0,
            Require(P6T06AvailableCommandV1.TryCreate(
                P6T06QueueFamilyV1.LiquidZone,
                zone,
                0.5,
                0.0,
                1.0,
                0.1)));
        P6T06CommandCandidateV1 zoneCandidate = Require(
            P6T06CommandCandidateV1.TryCreate(
                P6T06QueueFamilyV1.LiquidZone,
                zone,
                P6T06SourceKindV1.Scheduled,
                EventRankV1.BranchUpdate,
                1.0,
                1.5,
                0.0,
                1.0,
                0.1));
        P6T06EnqueueResultV1 zoneEnqueue = Require(
            zoneQueue.TryEnqueueBatch(
                Binding(0xd633, Digest(0x31)),
                0.0,
                Require(zoneQueue.TryCreatePhaseToken(0.0)),
                new[] { zoneCandidate }));
        Assert.Equal(1.0, zoneEnqueue.Commands[0].DueTimeSeconds);
        P6T06MotionResultV1 zoneAtDue = Require(
            zoneEnqueue.Queue.TryMotionAndConsume(
                1.0,
                P6T06MotionModeV1.RateLimited,
                new[]
                {
                    Require(P6T06ActuatorStateV1.TryCreate(
                        P6T06QueueFamilyV1.LiquidZone,
                        zone,
                        0.5,
                        0.1))
                },
                OneSecond));
        Assert.Equal(0.5, zoneAtDue.ActuatorStates[0].PhysicalState, 12);
        Assert.Equal(1.0, zoneAtDue.Queue.AvailableCommands[0].BoundedCommand, 12);
        Assert.Equal((ushort)EventRankV1.BranchUpdate,
            (ushort)zoneAtDue.ConsumedCommands[0].EventRank);

        ContractValidationResult<P6T06MotionResultV1> invalidZoneHeld =
            zoneEnqueue.Queue.TryMotionAndConsume(
                0.0,
                P6T06MotionModeV1.Held,
                new[]
                {
                    Require(P6T06ActuatorStateV1.TryCreate(
                        P6T06QueueFamilyV1.LiquidZone,
                        zone,
                        0.5,
                        0.1))
                },
                Array.Empty<double>());
        Assert.False(invalidZoneHeld.IsValid);
        Assert.Equal("P6T06.Motion.Mode.Invalid", invalidZoneHeld.FirstDiagnostic.Code);

        P6T06TargetKeyV1 bank = Require(
            P6T06TargetKeyV1.TryForAdjusterBank(AdjusterBankGroupingV1.ApprovedBankBId));
        P6T06OwnerKeyV1 adjusterOwner = Require(
            P6T06OwnerKeyV1.TryForFamily(
                P6T06QueueFamilyV1.Adjuster,
                StableId.Parse("00000000-0000-0000-0000-00000000a709")));
        P6T06QueueStateV1 adjusterQueue = CreateQueue(
            P6T06QueueFamilyV1.Adjuster,
            StableId.Parse("00000000-0000-0000-0000-00000000a708"),
            adjusterOwner,
            null,
            0.0,
            Require(P6T06AvailableCommandV1.TryCreate(
                P6T06QueueFamilyV1.Adjuster,
                bank,
                0.5,
                0.0,
                1.0,
                0.1)));
        P6T06CommandCandidateV1 bankCandidate = Require(
            P6T06CommandCandidateV1.TryCreate(
                P6T06QueueFamilyV1.Adjuster,
                bank,
                P6T06SourceKindV1.Manual,
                EventRankV1.BranchUpdate,
                1.0,
                0.9,
                0.0,
                1.0,
                0.1));
        P6T06EnqueueResultV1 bankEnqueue = Require(
            adjusterQueue.TryEnqueueBatch(
                Binding(0xd641, Digest(0x41)),
                0.0,
                Require(adjusterQueue.TryCreatePhaseToken(0.0)),
                new[] { bankCandidate }));
        P6T06MotionResultV1 bankAfter = Require(
            bankEnqueue.Queue.TryMotionAndConsume(
                1.0,
                P6T06MotionModeV1.RateLimited,
                new[]
                {
                    Require(P6T06ActuatorStateV1.TryCreate(
                        P6T06QueueFamilyV1.Adjuster,
                        bank,
                        0.5,
                        0.1))
                },
                OneSecond));
        Assert.Equal(0.5, bankAfter.ActuatorStates[0].PhysicalState, 12);
        Assert.Equal(0.9, bankAfter.Queue.AvailableCommands[0].BoundedCommand, 12);

        IReadOnlyList<P6T06QueueStateV1> orderedBranchQueues = Require(
            P6T06QueueStateV1.TryOrderBranchQueues(new[] { zoneQueue, adjusterQueue }));
        Assert.Equal(adjusterOwner.EntityId, orderedBranchQueues[0].OwnerKey.EntityId);
        Assert.Equal(zoneOwner.EntityId, orderedBranchQueues[1].OwnerKey.EntityId);

        P6T06QueueStateV1 duplicateBranchQueue = CreateQueue(
            P6T06QueueFamilyV1.Adjuster,
            zoneQueue.QueueId,
            adjusterOwner,
            null,
            0.0,
            Require(P6T06AvailableCommandV1.TryCreate(
                P6T06QueueFamilyV1.Adjuster,
                bank,
                0.5,
                0.0,
                1.0,
                0.1)));
        ContractValidationResult<IReadOnlyList<P6T06QueueStateV1>> duplicateOrder =
            P6T06QueueStateV1.TryOrderBranchQueues(
                new[] { zoneQueue, duplicateBranchQueue, adjusterQueue });
        Assert.False(duplicateOrder.IsValid);
        Assert.Equal("P6T06.BranchQueue.Identity.Duplicate", duplicateOrder.FirstDiagnostic.Code);
    }

    [Fact]
    public void TransitionCommandProjectionUsesCanonicalDueOrder()
    {
        P6T06QueueStateV1 queue = CreateRrsQueueAtTimeZero();
        P6T06TargetKeyV1 total = Require(
            P6T06TargetKeyV1.TryForRrsActuator(RrsFixtureV1.TotalPowerActuatorId));
        P6T06TargetKeyV1 tilt = Require(
            P6T06TargetKeyV1.TryForRrsActuator(RrsFixtureV1.TiltActuatorId));
        P6T06CommandCandidateV1 totalCandidate = Require(
            P6T06CommandCandidateV1.TryCreate(
                P6T06QueueFamilyV1.Rrs,
                total,
                P6T06SourceKindV1.Manual,
                EventRankV1.ControllerCommandGeneration,
                2.0,
                0.6,
                0.0,
                1.0,
                0.1));
        P6T06CommandCandidateV1 tiltCandidate = Require(
            P6T06CommandCandidateV1.TryCreate(
                P6T06QueueFamilyV1.Rrs,
                tilt,
                P6T06SourceKindV1.Manual,
                EventRankV1.ControllerCommandGeneration,
                0.0,
                0.7,
                0.0,
                1.0,
                0.1));

        P6T06EnqueueResultV1 result = Require(
            queue.TryEnqueueBatch(
                Binding(0xd671, Digest(0x71)),
                0.0,
                Require(queue.TryCreatePhaseToken(0.0)),
                new[] { totalCandidate, tiltCandidate }));

        Assert.Equal(
            new[] { tilt, total },
            result.Transition.Commands.Select(command => command.Target).ToArray());
        Assert.Equal(
            new[] { tilt, total },
            result.Commands.Select(command => command.Target).ToArray());
    }

    [Fact]
    public void QueueStateRejectsNonCanonicalArrayOrder()
    {
        P6T06QueueStateV1 queue = CreateRrsQueueAtTimeZero();
        ContractValidationResult<P6T06QueueStateV1> reversed = P6T06QueueStateV1.TryCreate(
            queue.Family,
            queue.QueueId,
            queue.OwnerKey,
            queue.GenerationCadenceOrNA,
            queue.InitialNextSequence,
            queue.NextSequence,
            queue.LastMotionTimeSeconds,
            queue.AvailableCommands.Reverse(),
            queue.PendingCommands,
            queue.AppliedSourceEventIds,
            queue.AllocatedCommandIds,
            queue.QueueDigest);

        Assert.False(reversed.IsValid);
        Assert.Equal("P6T06.Queue.Order.NonCanonical", reversed.FirstDiagnostic.Code);
    }

    [Fact]
    public void OverflowReuseMissedDueAndInvalidMotionRollBackByteForByte()
    {
        P6T06QueueStateV1 queue = CreateOverflowQueue();
        byte[] before = queue.ToCanonicalBytes();
        P6T06TargetKeyV1 total = Require(
            P6T06TargetKeyV1.TryForRrsActuator(RrsFixtureV1.TotalPowerActuatorId));
        P6T06TargetKeyV1 tilt = Require(
            P6T06TargetKeyV1.TryForRrsActuator(RrsFixtureV1.TiltActuatorId));
        P6T06CommandCandidateV1 candidate = Require(
            P6T06CommandCandidateV1.TryCreate(
                P6T06QueueFamilyV1.Rrs,
                total,
                P6T06SourceKindV1.Controller,
                EventRankV1.ControllerCommandGeneration,
                0.0,
                0.8,
                0.0,
                1.0,
                0.1));
        P6T06CommandCandidateV1 secondCandidate = Require(
            P6T06CommandCandidateV1.TryCreate(
                P6T06QueueFamilyV1.Rrs,
                tilt,
                P6T06SourceKindV1.Controller,
                EventRankV1.ControllerCommandGeneration,
                0.0,
                0.7,
                0.0,
                1.0,
                0.1));

        ContractValidationResult<P6T06EnqueueResultV1> overflow = queue.TryEnqueueBatch(
            Binding(0xd651, Digest(0x51)),
            0.0,
            Require(queue.TryCreatePhaseToken(0.0)),
            new[] { candidate, secondCandidate });
        Assert.False(overflow.IsValid);
        Assert.Equal("P6T06.Enqueue.Capacity", overflow.FirstDiagnostic.Code);
        Assert.Equal(before, queue.ToCanonicalBytes());

        P6T06EnqueueResultV1 one = Require(queue.TryEnqueueBatch(
            Binding(0xd651, Digest(0x51)),
            0.0,
            Require(queue.TryCreatePhaseToken(0.0)),
            new[] { candidate }));
        Assert.Equal(ulong.MaxValue, one.Queue.NextSequence);
        byte[] afterOne = one.Queue.ToCanonicalBytes();
        ContractValidationResult<P6T06EnqueueResultV1> reuse = one.Queue.TryEnqueueBatch(
            Binding(0xd651, Digest(0x51)),
            0.0,
            Require(one.Queue.TryCreatePhaseToken(0.0)),
            new[] { candidate });
        Assert.False(reuse.IsValid);
        Assert.Equal("P6T06.Enqueue.SourceEvent.Reuse", reuse.FirstDiagnostic.Code);
        Assert.Equal(afterOne, one.Queue.ToCanonicalBytes());

        P6T06ActuatorStateV1 state = Require(
            P6T06ActuatorStateV1.TryCreate(
                P6T06QueueFamilyV1.Rrs,
                total,
                0.0,
                0.1));
        P6T06ActuatorStateV1 tiltState = Require(
            P6T06ActuatorStateV1.TryCreate(
                P6T06QueueFamilyV1.Rrs,
                tilt,
                0.5,
                0.1));
        ContractValidationResult<P6T06MotionResultV1> missed = one.Queue.TryMotionAndConsume(
            1.0,
            P6T06MotionModeV1.Automatic,
            new[] { state, tiltState },
            OneSecond);
        Assert.False(missed.IsValid);
        Assert.Equal("P6T06.Motion.DueTime.Missed", missed.FirstDiagnostic.Code);
        Assert.Equal(afterOne, one.Queue.ToCanonicalBytes());

        ContractValidationResult<P6T06MotionResultV1> badPartition = one.Queue.TryMotionAndConsume(
            0.0,
            P6T06MotionModeV1.Automatic,
            new[] { state, tiltState },
            HalfSecond);
        Assert.False(badPartition.IsValid);
        Assert.Equal("P6T06.Motion.Substep.PartitionMismatch", badPartition.FirstDiagnostic.Code);
        Assert.Equal(afterOne, one.Queue.ToCanonicalBytes());
    }

    [Fact]
    public void InvalidFamilyRankAndOwnerBindingsFailClosed()
    {
        P6T06TargetKeyV1 total = Require(
            P6T06TargetKeyV1.TryForRrsActuator(RrsFixtureV1.TotalPowerActuatorId));
        ContractValidationResult<P6T06CommandCandidateV1> wrongRank =
            P6T06CommandCandidateV1.TryCreate(
                P6T06QueueFamilyV1.Rrs,
                total,
                P6T06SourceKindV1.Controller,
                EventRankV1.BranchUpdate,
                0.0,
                0.5,
                0.0,
                1.0,
                0.1);
        Assert.False(wrongRank.IsValid);
        Assert.Equal("P6T06.Candidate.SourceRank.Invalid", wrongRank.FirstDiagnostic.Code);

        ContractValidationResult<P6T06QueueStateV1> wrongOwner =
            P6T06QueueStateV1.TryCreate(
                P6T06QueueFamilyV1.Rrs,
                Id(0xd661),
                Require(P6T06OwnerKeyV1.TryForFamily(
                    P6T06QueueFamilyV1.LiquidZone,
                    Id(0xd662))),
                1.0,
                0,
                0,
                0.0,
                Array.Empty<P6T06AvailableCommandV1>(),
                Array.Empty<P6T06QueueCommandV1>(),
                Array.Empty<StableId>(),
                Array.Empty<StableId>());
        Assert.False(wrongOwner.IsValid);
        Assert.Equal("P6T06.Queue.Identity.Invalid", wrongOwner.FirstDiagnostic.Code);
    }

    private static P6T06QueueStateV1 CreateRrsQueueAtTimeZero()
    {
        P6T06OwnerKeyV1 owner = Require(
            P6T06OwnerKeyV1.TryForFamily(P6T06QueueFamilyV1.Rrs, RrsFixtureV1.ControllerId));
        P6T06TargetKeyV1 total = Require(
            P6T06TargetKeyV1.TryForRrsActuator(RrsFixtureV1.TotalPowerActuatorId));
        P6T06TargetKeyV1 tilt = Require(
            P6T06TargetKeyV1.TryForRrsActuator(RrsFixtureV1.TiltActuatorId));
        return CreateQueue(
            P6T06QueueFamilyV1.Rrs,
            Id(0xd602),
            owner,
            1.0,
            0.0,
            new[]
            {
                Require(P6T06AvailableCommandV1.TryCreate(
                    P6T06QueueFamilyV1.Rrs,
                    total,
                    0.5,
                    0.0,
                    1.0,
                    0.1)),
                Require(P6T06AvailableCommandV1.TryCreate(
                    P6T06QueueFamilyV1.Rrs,
                    tilt,
                    0.5,
                    0.0,
                    1.0,
                    0.1))
            });
    }

    private static P6T06QueueStateV1 CreateOverflowQueue()
    {
        P6T06OwnerKeyV1 owner = Require(
            P6T06OwnerKeyV1.TryForFamily(P6T06QueueFamilyV1.Rrs, RrsFixtureV1.ControllerId));
        P6T06TargetKeyV1 total = Require(
            P6T06TargetKeyV1.TryForRrsActuator(RrsFixtureV1.TotalPowerActuatorId));
        P6T06TargetKeyV1 tilt = Require(
            P6T06TargetKeyV1.TryForRrsActuator(RrsFixtureV1.TiltActuatorId));
        return CreateQueue(
            P6T06QueueFamilyV1.Rrs,
            Id(0xd652),
            owner,
            null,
            0.0,
            new[]
            {
                Require(P6T06AvailableCommandV1.TryCreate(
                    P6T06QueueFamilyV1.Rrs,
                    total,
                    0.5,
                    0.0,
                    1.0,
                    0.1)),
                Require(P6T06AvailableCommandV1.TryCreate(
                    P6T06QueueFamilyV1.Rrs,
                    tilt,
                    0.5,
                    0.0,
                    1.0,
                    0.1))
            },
            initialNextSequence: ulong.MaxValue - 1,
            nextSequence: ulong.MaxValue - 1);
    }

    private static P6T06QueueStateV1 CreateQueue(
        P6T06QueueFamilyV1 family,
        StableId queueId,
        P6T06OwnerKeyV1 owner,
        double? cadence,
        double lastMotionTimeSeconds,
        params P6T06AvailableCommandV1[] available)
    {
        return CreateQueue(
            family,
            queueId,
            owner,
            cadence,
            lastMotionTimeSeconds,
            available,
            initialNextSequence: 0,
            nextSequence: 0);
    }

    private static P6T06QueueStateV1 CreateQueue(
        P6T06QueueFamilyV1 family,
        StableId queueId,
        P6T06OwnerKeyV1 owner,
        double? cadence,
        double lastMotionTimeSeconds,
        IEnumerable<P6T06AvailableCommandV1> available,
        ulong initialNextSequence,
        ulong nextSequence)
    {
        return Require(P6T06QueueStateV1.TryCreate(
            family,
            queueId,
            owner,
            cadence,
            initialNextSequence,
            nextSequence,
            lastMotionTimeSeconds,
            available,
            Array.Empty<P6T06QueueCommandV1>(),
            Array.Empty<StableId>(),
            Array.Empty<StableId>()));
    }

    private static StableId Id(int suffix)
    {
        return StableId.Parse(
            "00000000-0000-0000-0000-" + suffix.ToString("x12", CultureInfo.InvariantCulture));
    }

    private static Digest32 Digest(byte value)
    {
        return new Digest32(Enumerable.Repeat(value, 32).ToArray());
    }

    private static P6T06SourceBindingTokenV1 Binding(int eventSuffix, Digest32 digest)
    {
        return Require(P6T06SourceBindingTokenV1.TryCreate(Id(eventSuffix), digest));
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }
}
