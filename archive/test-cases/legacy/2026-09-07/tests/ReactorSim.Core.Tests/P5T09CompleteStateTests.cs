using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P5T09CompleteStateTests
{
    [Fact]
    public void AppliesExplicitEulerIxeBatchAndInvalidatesStalePowerBinding()
    {
        CompleteWorld world = CreateWorld();
        BundleState target = world.Inventory.EnumerateOccupied().First();
        NuclideIntegrationInputV1 input = CreateInput(
            world.Data,
            Id(9001),
            0,
            0.0,
            0.5,
            8.0,
            3.0,
            5.0,
            world.Lifecycle.CoreStateVersion,
            world.Lifecycle.StateDigest.Value!);
        CompleteNuclideIntegrationRequestV1 request =
            CompleteNuclideIntegrationRequestV1.TryCreate(target.BundleId, input).Value;

        ContractValidationResult<CompleteNuclideIntegrationBatchResultV1> result =
            CompleteNuclideIntegrationBatchTransitionV1.TryApply(
                world.Inventory,
                world.Lifecycle,
                new[] { request },
                Digest(0x61));

        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        BundleState updated = result.Value.ResultingInventory.TryFind(target.BundleId, out BundleState? found)
            ? found!
            : throw new InvalidOperationException("The integrated bundle was not retained.");
        Assert.Equal(1UL, updated.NuclideState!.NuclideStateVersion);
        Assert.Equal(11.5, updated.NuclideState.I135AtomInventory, 12);
        Assert.Equal(19.7, updated.NuclideState.Xe135AtomInventory, 12);
        Assert.Equal(4.0, result.Value.Transitions[0].Transition.I135DirectProductionAtomsPerSecond);
        Assert.Equal(-1.0, result.Value.Transitions[0].Transition.I135DecayLossAtomsPerSecond);
        Assert.Equal(2.0, result.Value.Transitions[0].Transition.Xe135DirectProductionAtomsPerSecond);
        Assert.Equal(1.0, result.Value.Transitions[0].Transition.Xe135FromI135DecayAtomsPerSecond);
        Assert.Equal(-1.0, result.Value.Transitions[0].Transition.Xe135DecayLossAtomsPerSecond);
        Assert.Equal(-2.6, result.Value.Transitions[0].Transition.Xe135AbsorptionLossAtomsPerSecond, 12);
        Assert.Equal(1UL, result.Value.ResultingLifecycle.BundleNuclideVersions
            .Single(version => version.BundleId == target.BundleId).NuclideStateVersion);
        Assert.Equal(BindingStatusV1.Invalid, result.Value.ResultingLifecycle.PowerBindingStatus);
        Assert.False(updated.PowerWatts.IsApplicable);
        Assert.Same(world.Inventory.Get(target.Node), target);
    }

    [Fact]
    public void RejectsNegativeEulerResultWithoutClampingAndRejectsReorderedHistory()
    {
        CompleteWorld world = CreateWorld();
        BundleState target = world.Inventory.EnumerateOccupied().First();
        NuclideIntegrationInputV1 negativeInput = CreateInput(
            world.Data,
            Id(9010),
            0,
            0.0,
            2.0,
            0.0,
            0.0,
            0.0,
            world.Lifecycle.CoreStateVersion,
            world.Lifecycle.StateDigest.Value!,
            lambdaI: 20.0);
        ContractValidationResult<NuclideIntegrationResultV1> negative =
            NuclideIntegrationTransitionV1.TryApply(target.NuclideState!, negativeInput);
        Assert.False(negative.IsValid);
        Assert.Equal("NuclideIntegration.Result.Invalid", negative.FirstDiagnostic.Code);
        Assert.Equal(0UL, target.NuclideState!.NuclideStateVersion);

        NuclideIntegrationInputV1 staleBindingInput = CreateInput(
            world.Data,
            Id(9011),
            0,
            0.0,
            0.1,
            1.0,
            1.0,
            1.0,
            world.Lifecycle.CoreStateVersion,
            Digest(0xfe));
        CompleteNuclideIntegrationRequestV1 staleBindingRequest =
            CompleteNuclideIntegrationRequestV1.TryCreate(target.BundleId, staleBindingInput).Value;
        ContractValidationResult<CompleteNuclideIntegrationBatchResultV1> staleBinding =
            CompleteNuclideIntegrationBatchTransitionV1.TryApply(
                world.Inventory,
                world.Lifecycle,
                new[] { staleBindingRequest },
                Digest(0xfd));
        Assert.False(staleBinding.IsValid);
        Assert.Equal("NuclideBatch.Input.StateBinding.Stale", staleBinding.FirstDiagnostic.Code);

        NuclideIntegrationInputV1 firstInput = CreateInput(
            world.Data,
            Id(9020),
            0,
            0.0,
            0.1,
            1.0,
            1.0,
            1.0,
            world.Lifecycle.CoreStateVersion,
            world.Lifecycle.StateDigest.Value!);
        NuclideIntegrationResultV1 first =
            NuclideIntegrationTransitionV1.TryApply(target.NuclideState!, firstInput).Value;
        NuclideIntegrationInputV1 secondInput = CreateInput(
            world.Data,
            Id(9021),
            0,
            1.0,
            0.1,
            1.0,
            1.0,
            1.0,
            world.Lifecycle.CoreStateVersion,
            world.Lifecycle.StateDigest.Value!);
        NuclideIntegrationResultV1 second =
            NuclideIntegrationTransitionV1.TryApply(first.ResultingState, secondInput).Value;

        ContractValidationResult<NuclideStateEnvelopeV1> reordered = NuclideStateEnvelopeV1.TryCreate(
            target.BundleId,
            second.ResultingState.I135AtomInventory,
            second.ResultingState.Xe135AtomInventory,
            second.ResultingState.InitialI135,
            second.ResultingState.InitialXe135,
            second.ResultingState.NodeVolumeM3,
            second.ResultingState.NuclideStateVersion,
            world.Data,
            second.ResultingState.I135XeHistory.Reverse());
        Assert.False(reordered.IsValid);
        Assert.Equal("NuclideState.History.Order.Invalid", reordered.FirstDiagnostic.Code);

        ContractValidationResult<NuclideStateEnvelopeV1> moved =
            second.ResultingState.TryMoveToVolume(second.ResultingState.NodeVolumeM3 * 2.0);
        Assert.True(moved.IsValid, moved.IsValid ? string.Empty : moved.FirstDiagnostic.ToString());
        Assert.Equal(second.ResultingState.I135AtomInventory, moved.Value.I135AtomInventory);
        Assert.Equal(second.ResultingState.NuclideHistoryDigest.Bytes, moved.Value.NuclideHistoryDigest.Bytes);
        Assert.Equal(
            second.ResultingState.I135AtomInventory / moved.Value.NodeVolumeM3,
            moved.Value.I135NumberDensity);
    }

    [Fact]
    public void AcceptsCompletePowerSnapshotAndAppendsBoundHistoryToEveryBundle()
    {
        CompleteWorld world = CreateWorld();
        StableId solveId = Id(9100);
        StableId snapshotId = Id(9101);
        CompletePowerSnapshotBundleV1[] entries = world.Inventory.EnumerateOccupied()
            .OrderBy(bundle => bundle.BundleId)
            .Select((bundle, index) => CompletePowerSnapshotBundleV1.TryCreate(
                bundle.BundleId,
                bundle.ChannelId,
                bundle.Position,
                100.0 + index,
                bundle.NuclideState!.NuclideStateVersion,
                BundleCoefficientBindingV1.TryCreate(
                    Id(9200),
                    0,
                    1,
                    0.25,
                    Digest(0x71)).Value).Value)
            .ToArray();
        BundleNuclideVersionV1[] versions = world.Lifecycle.BundleNuclideVersions.ToArray();
        CompletePowerSnapshotV1 snapshot = CompletePowerSnapshotV1.TryCreate(
            solveId,
            snapshotId,
            world.Lifecycle.CurrentSimulationTimeSeconds,
            world.Lifecycle.CoreStateVersion,
            world.Lifecycle.SpatialStateVersion + 1,
            world.Lifecycle.PowerSnapshotVersion + 1,
            world.Lifecycle.StateDigest.Value!,
            CompleteStateDigestV1.ComputeInventory(world.Inventory),
            CompleteStateDigestV1.TryComputeAcceptedInventory(
                world.Inventory,
                snapshotId,
                world.Lifecycle.CurrentSimulationTimeSeconds,
                world.Lifecycle.CoreStateVersion,
                world.Lifecycle.SpatialStateVersion + 1,
                world.Lifecycle.PowerSnapshotVersion + 1,
                entries).Value,
            CompleteStateDigestV1.ComputeBurnupEnergy(world.Inventory),
            Digest(0x73),
            world.Lifecycle.TopologyDigest.Value!,
            world.Lifecycle.DataPackDigest.Value!,
            Digest(0x76),
            entries,
            versions).Value;

        ContractValidationResult<CompletePowerSnapshotResultV1> accepted =
            CompletePowerSnapshotTransitionV1.TryApply(world.Inventory, world.Lifecycle, snapshot);

        Assert.True(accepted.IsValid, accepted.IsValid ? string.Empty : accepted.FirstDiagnostic.ToString());
        Assert.Equal(201UL, accepted.Value.ResultingLifecycle.SpatialStateVersion);
        Assert.Equal(301UL, accepted.Value.ResultingLifecycle.PowerSnapshotVersion);
        Assert.Equal(BindingStatusV1.Valid, accepted.Value.ResultingLifecycle.PowerBindingStatus);
        foreach (BundleState bundle in accepted.Value.ResultingInventory.EnumerateOccupied())
        {
            Assert.True(bundle.PowerWatts.IsApplicable);
            Assert.Equal(snapshotId, bundle.PowerSnapshotId.Value);
            Assert.Single(bundle.PowerHistory);
            Assert.Equal(accepted.Value.ResultingLifecycle.PowerSnapshotVersion,
                bundle.PowerHistory[0].PowerSnapshotVersion);
        }

        ContractValidationResult<CompletePowerSnapshotResultV1> stale =
            CompletePowerSnapshotTransitionV1.TryApply(
                accepted.Value.ResultingInventory,
                accepted.Value.ResultingLifecycle,
                snapshot);
        Assert.False(stale.IsValid);
        Assert.Equal("PowerSnapshotTransition.InventoryDigest.Stale", stale.FirstDiagnostic.Code);
        Assert.All(world.Inventory.EnumerateOccupied(), bundle => Assert.Empty(bundle.PowerHistory));
    }

    [Fact]
    public void CommitsCompleteRefuellingWithDischargeStateAndFreshTemplatesAtomically()
    {
        CompleteWorld initialWorld = CreateWorld();
        RefuelSchemeDefinition scheme = RefuelSchemeDefinition.TryCreate(
            "S4",
            4,
            "fresh-template-v1",
            RefuelSchemeDefinition.CurrentSchemaVersion,
            Enumerable.Range(4, 4).Select(value => new BundlePosition((uint)value)),
            Enumerable.Range(0, 4).Select(value => new BundlePosition((uint)value))).Value;
        RefuelSchemePositionPlan plan = scheme.TryCreatePositionPlan(
            initialWorld.Topology.BundlePositionCount,
            FlowDirection.EndAtoEndB).Value;
        BundleState[] preparedInserted = plan.InsertedPositions
            .Select((position, index) => CreateFreshBundle(
                initialWorld,
                Id(9250 + index),
                position,
                0.0))
            .ToArray();
        ContractValidationResult<RefuelShiftResult> preparedShift = RefuelShiftTransition.TryApply(
            initialWorld.Inventory,
            new ChannelId(0),
            plan,
            preparedInserted,
            initialWorld.Lifecycle.CurrentSimulationTimeSeconds,
            initialWorld.Lifecycle.CurrentSimulationTimeSeconds);
        Assert.True(preparedShift.IsValid, preparedShift.IsValid ? string.Empty : preparedShift.FirstDiagnostic.ToString());
        CompleteRefuellingRequestV1 preparedRequest = CompleteRefuellingRequestV1.TryCreate(
            preparedShift.Value,
            initialWorld.Lifecycle).Value;

        BundleState initialTarget = initialWorld.Inventory.EnumerateOccupied().First();
        BundleState staleEnergyTarget = initialTarget.WithEnergy(
            initialTarget.CumulativeFissionEnergyJ + 123.0);
        BundleInventory staleEnergyInventory = BundleInventory.TryCreate(
            initialWorld.Topology,
            initialWorld.Inventory.EnumerateOccupied()
                .Select(bundle => bundle.BundleId == initialTarget.BundleId ? staleEnergyTarget : bundle)).Value;
        SpatialNodeVolumeV1[] initialVolumes = initialWorld.Volumes
            .Select(pair => SpatialNodeVolumeV1.TryCreate(pair.Key, pair.Value).Value)
            .ToArray();
        ContractValidationResult<CompleteRefuellingTransitionResultV1> staleEnergy =
            CompleteRefuellingTransitionV1.TryApply(
                preparedRequest,
                staleEnergyInventory,
                initialWorld.Lifecycle,
                initialVolumes,
                Id(9380),
                Digest(0x7f));
        Assert.False(staleEnergy.IsValid);
        Assert.Equal("CompleteRefuelling.SourceInventoryDigest.Stale", staleEnergy.FirstDiagnostic.Code);
        Assert.Equal(123.0, staleEnergyInventory.EnumerateOccupied()
            .Single(bundle => bundle.BundleId == initialTarget.BundleId)
            .CumulativeFissionEnergyJ);
        Assert.Equal(100UL, initialWorld.Lifecycle.CoreStateVersion);

        PowerHistoryRecordV1 stalePowerHistory = PowerHistoryRecordV1.TryCreate(
            0.0,
            12.0,
            initialWorld.Lifecycle.CoreStateVersion,
            initialWorld.Lifecycle.SpatialStateVersion + 1,
            initialWorld.Lifecycle.PowerSnapshotVersion + 1).Value;
        BundleCoefficientBindingV1 staleCoefficient = BundleCoefficientBindingV1.TryCreate(
            Id(9371),
            0,
            1,
            0.5,
            Digest(0x97)).Value;
        BundleState stalePowerTarget = initialTarget.TryWithAcceptedPowerSnapshot(
            Id(9370),
            stalePowerHistory,
            staleCoefficient).Value;
        BundleInventory stalePowerInventory = BundleInventory.TryCreate(
            initialWorld.Topology,
            initialWorld.Inventory.EnumerateOccupied()
                .Select(bundle => bundle.BundleId == initialTarget.BundleId ? stalePowerTarget : bundle)).Value;
        ContractValidationResult<CompleteRefuellingTransitionResultV1> stalePower =
            CompleteRefuellingTransitionV1.TryApply(
                preparedRequest,
                stalePowerInventory,
                initialWorld.Lifecycle,
                initialVolumes,
                Id(9381),
                Digest(0x7e));
        Assert.False(stalePower.IsValid);
        Assert.Equal("CompleteRefuelling.SourceInventoryDigest.Stale", stalePower.FirstDiagnostic.Code);
        Assert.Empty(initialWorld.Inventory.EnumerateOccupied().SelectMany(bundle => bundle.PowerHistory));

        BundleState integratedTarget = initialWorld.Inventory.EnumerateOccupied().First();
        NuclideIntegrationInputV1 integrationInput = CreateInput(
            initialWorld.Data,
            Id(9290),
            0,
            0.0,
            0.1,
            1.0,
            1.0,
            1.0,
            initialWorld.Lifecycle.CoreStateVersion,
            initialWorld.Lifecycle.StateDigest.Value!);
        CompleteNuclideIntegrationRequestV1 integrationRequest =
            CompleteNuclideIntegrationRequestV1.TryCreate(integratedTarget.BundleId, integrationInput).Value;
        CompleteNuclideIntegrationBatchResultV1 integrated =
            CompleteNuclideIntegrationBatchTransitionV1.TryApply(
                initialWorld.Inventory,
                initialWorld.Lifecycle,
                new[] { integrationRequest },
                Digest(0x80)).Value;
        CompleteWorld world = new CompleteWorld(
            initialWorld.Topology,
            initialWorld.Data,
            integrated.ResultingInventory,
            integrated.ResultingLifecycle,
            initialWorld.Volumes,
            initialWorld.Lifecycle.StateDigest.Value!);
        BundleState[] inserted = plan.InsertedPositions
            .Select((position, index) => CreateFreshBundle(
                world,
                Id(9300 + index),
                position,
                0.0))
            .ToArray();
        ContractValidationResult<RefuelShiftResult> shift = RefuelShiftTransition.TryApply(
            world.Inventory,
            new ChannelId(0),
            plan,
            inserted,
            world.Lifecycle.CurrentSimulationTimeSeconds,
            world.Lifecycle.CurrentSimulationTimeSeconds);
        Assert.True(shift.IsValid, shift.IsValid ? string.Empty : shift.FirstDiagnostic.ToString());

        SpatialNodeVolumeV1[] volumes = world.Volumes
            .Select(pair => SpatialNodeVolumeV1.TryCreate(pair.Key, pair.Value).Value)
            .ToArray();
        CompleteRefuellingRequestV1 validRequest = CompleteRefuellingRequestV1.TryCreate(
            shift.Value,
            world.Lifecycle).Value;
        ContractValidationResult<CompleteRefuellingTransitionResultV1> missingPostShiftTables =
            CompleteRefuellingTransitionV1.TryApply(
                validRequest,
                world.Inventory,
                world.Lifecycle,
                volumes,
                Id(9400),
                Digest(0x81));
        Assert.False(missingPostShiftTables.IsValid);
        Assert.Equal("CompleteRefuelling.CoefficientTables.Required",
            missingPostShiftTables.FirstDiagnostic.Code);

        ContractValidationResult<CompleteRefuellingTransitionResultV1> committed =
            CompleteRefuellingTransitionV1.TryApplyWithCoefficientTables(
                validRequest,
                world.Inventory,
                world.Lifecycle,
                volumes,
                Id(9400),
                Digest(0x81),
                new[] { CreateCoefficientTable(world) });

        Assert.True(committed.IsValid, committed.IsValid ? string.Empty : committed.FirstDiagnostic.ToString());
        Assert.Equal(world.Lifecycle.CoreStateVersion + 1, committed.Value.ResultingLifecycle.CoreStateVersion);
        Assert.Equal(BindingStatusV1.Invalid, committed.Value.ResultingLifecycle.PowerBindingStatus);
        Assert.Equal(4, committed.Value.DischargeRecords.Count);
        Assert.All(committed.Value.DischargeRecords, record =>
        {
            Assert.Equal(0UL, record.NuclideState.NuclideStateVersion);
            Assert.NotEmpty(record.ToCanonicalBytes());
            Assert.Equal(record.BundleId, record.NuclideState.BundleId);
        });
        foreach (BundleState bundle in committed.Value.ResultingInventory.EnumerateOccupied())
        {
            Assert.False(bundle.PowerWatts.IsApplicable);
            Assert.Equal(bundle.NuclideState!.I135AtomInventory / bundle.NuclideState.NodeVolumeM3,
                bundle.NuclideState.I135NumberDensity);
        }

        Assert.Equal(CommitStatusV1.Committed, committed.Value.Transaction.CommitStatus);
        Assert.False(committed.Value.Transaction.BeforeDigest.Equals(committed.Value.Transaction.ProposedDigest));
        Assert.False(committed.Value.Transaction.UnchangedDigestOrNA.IsApplicable);
        Assert.Equal(8, committed.Value.ResultingInventory.OccupiedCount);
        BundleNuclideVersionV1 retainedVersion = committed.Value.ResultingLifecycle.BundleNuclideVersions
            .Single(version => version.BundleId == integratedTarget.BundleId);
        Assert.Equal(0UL, retainedVersion.InitialNuclideStateVersion);
        Assert.Equal(1UL, retainedVersion.NuclideStateVersion);
    }

    private static BurnupCoefficientTableV1 CreateCoefficientTable(CompleteWorld world)
    {
        BurnupCoefficientValuesV1 values = BurnupCoefficientValuesV1.TryCreate(
            0.3,
            0.2,
            0.1,
            0.1,
            0.15,
            0.25,
            0.1,
            1.0,
            1.0).Value;
        BurnupCoefficientRowV1 row = BurnupCoefficientRowV1.TryCreate(0.0, values).Value;
        return BurnupCoefficientTableV1.TryCreate(
            Id(9450),
            BurnupCoefficientTableV1.CurrentSchemaVersion,
            world.Lifecycle.DataPackVersion,
            world.Data.MaterialVariantId,
            "SI-v1",
            "synthetic:P5-T12",
            Digest(0x95),
            new[] { row }).Value;
    }

    [Fact]
    public void CompleteStateDigestsAndSnapshotBytesAreRepeatable()
    {
        CompleteWorld first = CreateWorld();
        CompleteWorld second = CreateWorld();
        Assert.Equal(
            first.Inventory.EnumerateOccupied().Select(bundle => bundle.NuclideState!.NuclideStateDigest.Bytes),
            second.Inventory.EnumerateOccupied().Select(bundle => bundle.NuclideState!.NuclideStateDigest.Bytes));

        CompletePowerSnapshotV1 snapshot = CreateSnapshot(first, 9500);
        CompletePowerSnapshotV1 replay = CreateSnapshot(second, 9500);
        Assert.Equal(snapshot.ToCanonicalBytes(), replay.ToCanonicalBytes());
    }

    private static CompletePowerSnapshotV1 CreateSnapshot(CompleteWorld world, int idBase)
    {
        CompletePowerSnapshotBundleV1[] entries = world.Inventory.EnumerateOccupied()
            .Select((bundle, index) => CompletePowerSnapshotBundleV1.TryCreate(
                bundle.BundleId,
                bundle.ChannelId,
                bundle.Position,
                10.0 + index,
                0,
                BundleCoefficientBindingV1.TryCreate(
                    Id(idBase + 1),
                    0,
                    1,
                    0.5,
                    Digest(0x91)).Value).Value)
            .ToArray();
        return CompletePowerSnapshotV1.TryCreate(
            Id(idBase),
            Id(idBase + 2),
            0.0,
            world.Lifecycle.CoreStateVersion,
            world.Lifecycle.SpatialStateVersion + 1,
            world.Lifecycle.PowerSnapshotVersion + 1,
            Digest(0x92),
            CompleteStateDigestV1.ComputeInventory(world.Inventory),
            CompleteStateDigestV1.TryComputeAcceptedInventory(
                world.Inventory,
                Id(idBase + 2),
                0.0,
                world.Lifecycle.CoreStateVersion,
                world.Lifecycle.SpatialStateVersion + 1,
                world.Lifecycle.PowerSnapshotVersion + 1,
                entries).Value,
            CompleteStateDigestV1.ComputeBurnupEnergy(world.Inventory),
            Digest(0x93),
            Digest(0x94),
            Digest(0x95),
            Digest(0x96),
            entries,
            world.Lifecycle.BundleNuclideVersions).Value;
    }

    private static NuclideIntegrationInputV1 CreateInput(
        NuclideDataV1 data,
        StableId ownerEventId,
        ulong recordSequence,
        double eventTime,
        double deltaTime,
        double fissionRateDensity,
        double fluxGroup1,
        double fluxGroup2,
        ulong coreStateVersion,
        Digest32 stateBindingDigest,
        double lambdaI = 0.1)
    {
        NuclideDataV1 inputData = lambdaI == data.LambdaI
            ? data
            : NuclideDataV1.TryCreate(
                data.MaterialVariantId,
                data.DataId,
                data.DataDigest,
                data.GammaI,
                data.GammaXe,
                lambdaI,
                data.LambdaXe,
                data.SigmaXeGroup1M2,
                data.SigmaXeGroup2M2).Value;
        return NuclideIntegrationInputV1.TryCreate(
            ownerEventId,
            recordSequence,
            EventRankV1.KineticNuclideStep,
            eventTime,
            deltaTime,
            coreStateVersion,
            fissionRateDensity,
            fluxGroup1,
            fluxGroup2,
            inputData,
            stateBindingDigest).Value;
    }

    private static BundleState CreateFreshBundle(
        CompleteWorld world,
        StableId bundleId,
        BundlePosition position,
        double insertedAt)
    {
        NuclideStateEnvelopeV1 state = NuclideStateEnvelopeV1.TryCreateFresh(
            bundleId,
            3.0,
            4.0,
            world.Volumes[new NodeKey(new ChannelId(0), position)],
            0,
            world.Data).Value;
        return new BundleState(
            bundleId,
            new ChannelId(0),
            position,
            world.Data.MaterialVariantId,
            0.0,
            0.0,
            1000.0,
            insertedAt,
            state,
            OptionalPowerWattsV1.NotApplicable,
            OptionalStableId.NotApplicable,
            Array.Empty<PowerHistoryRecordV1>(),
            null,
            0);
    }

    private static CompleteWorld CreateWorld()
    {
        const uint positionCount = 8;
        var neighbors = new List<NeighborRecord>();
        for (uint position = 0; position + 1 < positionCount; position++)
        {
            neighbors.Add(new NeighborRecord(
                new ChannelId(0),
                new BundlePosition(position),
                new ChannelId(0),
                new BundlePosition(position + 1),
                NeighborDirection.TowardEndB));
            neighbors.Add(new NeighborRecord(
                new ChannelId(0),
                new BundlePosition(position + 1),
                new ChannelId(0),
                new BundlePosition(position),
                NeighborDirection.TowardEndA));
        }

        var boundaries = new List<BoundaryFaceRecord>();
        TopologyFace[] transverseFaces = { TopologyFace.North, TopologyFace.South, TopologyFace.East, TopologyFace.West };
        for (uint position = 0; position < positionCount; position++)
        {
            foreach (TopologyFace face in transverseFaces)
            {
                boundaries.Add(new BoundaryFaceRecord(
                    new ChannelId(0),
                    new BundlePosition(position),
                    face,
                    BoundaryClassification.Reflective));
            }
        }
        boundaries.Add(new BoundaryFaceRecord(
            new ChannelId(0),
            new BundlePosition(0),
            TopologyFace.EndA,
            BoundaryClassification.Reflective));
        boundaries.Add(new BoundaryFaceRecord(
            new ChannelId(0),
            new BundlePosition(positionCount - 1),
            TopologyFace.EndB,
            BoundaryClassification.Reflective));
        ChannelTopology channel = new ChannelTopology(
            new ChannelId(0),
            0,
            0,
            FlowDirection.EndAtoEndB,
            new BundlePosition(0),
            new BundlePosition(positionCount - 1),
            neighbors,
            boundaries);
        CoreTopology topology = CoreTopology.TryCreate(1, positionCount, new[] { channel }).Value;
        DataPackDescriptor dataPack = DataPackDescriptor.TryCreate(
            DataPackDescriptor.CurrentSchemaVersion,
            Id(9600),
            "synthetic-p5-t09",
            "topology-v1",
            1,
            positionCount,
            "SI-v1",
            Digest(0xa1).ToArray(),
            Digest(0xa2).ToArray()).Value;
        SimulationConfiguration configuration = SimulationConfiguration.TryCreate(
            SimulationConfiguration.CurrentSchemaVersion,
            topology,
            dataPack,
            0.0,
            100,
            200,
            300).Value;
        NuclideDataV1 data = NuclideDataV1.TryCreate(
            new MaterialVariantId("synthetic-fuel"),
            "synthetic-nuclide-v1",
            Digest(0xb1),
            0.5,
            0.25,
            0.1,
            0.05,
            0.01,
            0.02).Value;
        var volumes = topology.EnumerateNodes()
            .ToDictionary(node => node, node => 1.0 + node.Position.Value);
        BundleState[] bundles = topology.EnumerateNodes()
            .Select((node, index) =>
            {
                NuclideStateEnvelopeV1 state = NuclideStateEnvelopeV1.TryCreateFresh(
                    Id(9700 + index),
                    10.0 + index,
                    20.0 + index,
                    volumes[node],
                    0,
                    data).Value;
                return new BundleState(
                    Id(9700 + index),
                    node.ChannelId,
                    node.Position,
                    data.MaterialVariantId,
                    0.0,
                    0.0,
                    1000.0,
                    0.0,
                    state,
                    OptionalPowerWattsV1.NotApplicable,
                    OptionalStableId.NotApplicable,
                    Array.Empty<PowerHistoryRecordV1>(),
                    null,
                    0);
            })
            .ToArray();
        BundleInventory inventory = BundleInventory.TryCreate(topology, bundles).Value;
        VersionLifecycleV1 lifecycle = VersionLifecycleV1.TryCreate(
            configuration,
            inventory,
            bundles.Select(bundle => new BundleNuclideVersionV1(
                bundle.BundleId,
                bundle.NuclideState!.NuclideStateVersion,
                bundle.NuclideState.NuclideStateVersion)),
            Digest(0xb2)).Value;
        return new CompleteWorld(topology, data, inventory, lifecycle, volumes, Digest(0xb3));
    }

    private static Digest32 Digest(byte value)
    {
        return new Digest32(Enumerable.Repeat(value, 32).ToArray());
    }

    private static StableId Id(int value)
    {
        return StableId.Parse("00000000-0000-0000-0000-" +
                              value.ToString("x12", CultureInfo.InvariantCulture));
    }

    private sealed class CompleteWorld
    {
        public CompleteWorld(
            CoreTopology topology,
            NuclideDataV1 data,
            BundleInventory inventory,
            VersionLifecycleV1 lifecycle,
            Dictionary<NodeKey, double> volumes,
            Digest32 bindingDigest)
        {
            Topology = topology;
            Data = data;
            Inventory = inventory;
            Lifecycle = lifecycle;
            Volumes = volumes;
            BindingDigest = bindingDigest;
        }

        public CoreTopology Topology { get; }

        public NuclideDataV1 Data { get; }

        public BundleInventory Inventory { get; }

        public VersionLifecycleV1 Lifecycle { get; }

        public Dictionary<NodeKey, double> Volumes { get; }

        public Digest32 BindingDigest { get; }
    }
}
