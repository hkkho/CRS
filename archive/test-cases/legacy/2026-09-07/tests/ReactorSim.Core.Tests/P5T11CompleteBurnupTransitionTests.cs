using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P5T11CompleteBurnupTransitionTests
{
    private static readonly TopologyFace[] TransverseFaces =
    {
        TopologyFace.North,
        TopologyFace.South,
        TopologyFace.East,
        TopologyFace.West
    };

    [Fact]
    public void CommitsCompleteBurnupWithLookupAndRollsBackBeforeLifecycleMutation()
    {
        CompleteWorld world = CreateWorld();
        BurnupCoefficientTableV1 table = CreateTable(world);
        CompletePowerSnapshotV1 snapshot = CreateInitialSnapshot(world, table);
        ContractValidationResult<CompletePowerSnapshotResultV1> accepted =
            CompletePowerSnapshotTransitionV1.TryApply(world.Inventory, world.Lifecycle, snapshot);

        Assert.True(accepted.IsValid, accepted.IsValid ? string.Empty : accepted.FirstDiagnostic.ToString());
        Digest32 nextDigest = Digest(0xc1);
        ContractValidationResult<CompleteBurnupIntervalResultV1> committed =
            CompleteBurnupIntervalTransitionV1.TryApply(
                accepted.Value.ResultingInventory,
                accepted.Value.ResultingLifecycle,
                snapshot,
                10.0,
                new[] { table },
                nextDigest);

        Assert.True(committed.IsValid, committed.IsValid ? string.Empty : committed.FirstDiagnostic.ToString());
        CompleteBurnupIntervalResultV1 result = committed.Value;
        Assert.Equal(8, result.Records.Count);
        Assert.Equal(8, result.LookupBindings.Count);
        Assert.Equal(0.0, result.CurrentTimeSeconds, 12);
        Assert.Equal(10.0, result.TargetTimeSeconds, 12);
        Assert.Equal(100UL, result.CoreStateVersionBefore);
        Assert.Equal(101UL, result.CoreStateVersionAfter);
        Assert.True(result.PowerSnapshotInvalidated);
        Assert.Equal(nextDigest, result.ResultingLifecycle.StateDigest.Value);
        Assert.Equal(BindingStatusV1.Invalid, result.ResultingLifecycle.PowerBindingStatus);
        Assert.False(result.ResultingLifecycle.PowerSnapshotId.IsApplicable);

        Assert.All(result.ResultingInventory.EnumerateOccupied(), bundle =>
        {
            Assert.Equal(100.0, bundle.CumulativeFissionEnergyJ, 12);
            Assert.Equal(0.1, bundle.CurrentBurnupJPerKgHm, 12);
            Assert.False(bundle.PowerWatts.IsApplicable);
            Assert.False(bundle.PowerSnapshotId.IsApplicable);
            Assert.Single(bundle.PowerHistory);
            Assert.NotNull(bundle.CoefficientBinding);
            Assert.Equal(table.TableId, bundle.CoefficientBinding!.TableId);
            Assert.Equal(table.Checksum, bundle.CoefficientBinding.TableDigest);
        });
        Assert.All(result.LookupBindings, binding =>
        {
            Assert.Equal(0.1, binding.BurnupJPerKgHm, 12);
            Assert.Equal(0, binding.Lookup.BracketLowerIndex);
            Assert.Equal(1, binding.Lookup.BracketUpperIndex);
            Assert.Equal(0.1 / 100_000.0, binding.Lookup.InterpolationFraction, 12);
        });

        ContractValidationResult<CompleteBurnupIntervalResultV1> stale =
            CompleteBurnupIntervalTransitionV1.TryApply(
                result.ResultingInventory,
                result.ResultingLifecycle,
                snapshot,
                20.0,
                new[] { table },
                Digest(0xc2));
        Assert.False(stale.IsValid);
        Assert.Equal("CompleteBurnup.PowerSnapshot.StateBinding.Stale", stale.FirstDiagnostic.Code);

        BurnupCoefficientTableV1 narrowTable = CreateTable(world, maximumBurnup: 0.05);
        ContractValidationResult<CompleteBurnupIntervalResultV1> rejected =
            CompleteBurnupIntervalTransitionV1.TryApply(
                accepted.Value.ResultingInventory,
                accepted.Value.ResultingLifecycle,
                snapshot,
                10.0,
                new[] { narrowTable },
                Digest(0xc3));
        Assert.False(rejected.IsValid);
        Assert.Equal("BurnupCoefficientLookup.OutOfRange", rejected.FirstDiagnostic.Code);
        Assert.All(accepted.Value.ResultingInventory.EnumerateOccupied(), bundle =>
        {
            Assert.True(bundle.PowerWatts.IsApplicable);
            Assert.Equal(snapshot.PowerSnapshotId, bundle.PowerSnapshotId.Value);
            Assert.Single(bundle.PowerHistory);
        });
        Assert.Equal(100UL, accepted.Value.ResultingLifecycle.CoreStateVersion);
        Assert.Equal(0.0, accepted.Value.ResultingLifecycle.CurrentSimulationTimeSeconds, 12);
    }

    [Fact]
    public void RejectsForgedBurnupEnergyWhenAcceptedPowerBindingsAreRetained()
    {
        CompleteWorld world = CreateWorld();
        BurnupCoefficientTableV1 table = CreateTable(world);
        CompletePowerSnapshotV1 snapshot = CreateInitialSnapshot(world, table);
        CompletePowerSnapshotResultV1 accepted =
            CompletePowerSnapshotTransitionV1.TryApply(
                world.Inventory,
                world.Lifecycle,
                snapshot).Value;
        BundleState target = accepted.ResultingInventory.EnumerateOccupied().First();
        BundleState forged = new BundleState(
            target.BundleId,
            target.ChannelId,
            target.Position,
            target.MaterialVariantId,
            target.InitialBurnupJPerKgHm,
            target.CumulativeFissionEnergyJ + 1.0,
            target.HeavyMetalMassKg,
            target.InsertedAtSeconds,
            target.NuclideState,
            target.PowerWatts,
            target.PowerSnapshotId,
            target.PowerHistory,
            target.CoefficientBinding,
            target.StateVersion);
        BundleInventory forgedInventory = BundleInventory.TryCreate(
            world.Topology,
            accepted.ResultingInventory.EnumerateOccupied()
                .Select(bundle => bundle.BundleId == target.BundleId ? forged : bundle)).Value;

        ContractValidationResult<CompleteBurnupIntervalResultV1> rejected =
            CompleteBurnupIntervalTransitionV1.TryApply(
                forgedInventory,
                accepted.ResultingLifecycle,
                snapshot,
                10.0,
                new[] { table },
                Digest(0xc7));

        Assert.False(rejected.IsValid);
        Assert.Equal("CompleteBurnup.AcceptedInventoryDigest.Stale", rejected.FirstDiagnostic.Code);
        Assert.Equal(0.0, target.CumulativeFissionEnergyJ, 12);
        Assert.Equal(100UL, accepted.ResultingLifecycle.CoreStateVersion);
        Assert.Equal(0.0, accepted.ResultingLifecycle.CurrentSimulationTimeSeconds, 12);
    }

    [Fact]
    public void RejectsEarlierPowerHistoryMutationWhileRetainingCurrentAcceptedRecord()
    {
        CompleteWorld world = CreateWorld();
        BurnupCoefficientTableV1 table = CreateTable(world);
        CompletePowerSnapshotV1 firstSnapshot = CreateInitialSnapshot(world, table);
        ContractValidationResult<CompletePowerSnapshotResultV1> firstAccepted =
            CompletePowerSnapshotTransitionV1.TryApply(world.Inventory, world.Lifecycle, firstSnapshot);
        Assert.True(firstAccepted.IsValid,
            firstAccepted.IsValid ? string.Empty : firstAccepted.FirstDiagnostic.ToString());

        CompletePowerSnapshotV1 secondSnapshot = CreateSnapshot(
            firstAccepted.Value.ResultingInventory,
            firstAccepted.Value.ResultingLifecycle,
            table,
            Id(9810),
            Id(9811),
            11.0);
        ContractValidationResult<CompletePowerSnapshotResultV1> secondAccepted =
            CompletePowerSnapshotTransitionV1.TryApply(
                firstAccepted.Value.ResultingInventory,
                firstAccepted.Value.ResultingLifecycle,
                secondSnapshot);
        Assert.True(secondAccepted.IsValid,
            secondAccepted.IsValid ? string.Empty : secondAccepted.FirstDiagnostic.ToString());

        BundleState target = secondAccepted.Value.ResultingInventory.EnumerateOccupied().First();
        BundleState forged = new BundleState(
            target.BundleId,
            target.ChannelId,
            target.Position,
            target.MaterialVariantId,
            target.InitialBurnupJPerKgHm,
            target.CumulativeFissionEnergyJ,
            target.HeavyMetalMassKg,
            target.InsertedAtSeconds,
            target.NuclideState,
            target.PowerWatts,
            target.PowerSnapshotId,
            target.PowerHistory.Skip(1),
            target.CoefficientBinding,
            target.StateVersion);
        BundleInventory forgedInventory = BundleInventory.TryCreate(
            world.Topology,
            secondAccepted.Value.ResultingInventory.EnumerateOccupied()
                .Select(bundle => bundle.BundleId == target.BundleId ? forged : bundle)).Value;

        ContractValidationResult<CompleteBurnupIntervalResultV1> rejected =
            CompleteBurnupIntervalTransitionV1.TryApply(
                forgedInventory,
                secondAccepted.Value.ResultingLifecycle,
                secondSnapshot,
                10.0,
                new[] { table },
                Digest(0xc8));

        Assert.False(rejected.IsValid);
        Assert.Equal("CompleteBurnup.AcceptedInventoryDigest.Stale", rejected.FirstDiagnostic.Code);
        Assert.Single(forged.PowerHistory);
        Assert.Equal(2, target.PowerHistory.Count);
        Assert.Equal(100UL, secondAccepted.Value.ResultingLifecycle.CoreStateVersion);
        Assert.Equal(0.0, secondAccepted.Value.ResultingLifecycle.CurrentSimulationTimeSeconds, 12);
    }

    [Fact]
    public void RejectsReconstructedInventoryAndSnapshotPairAgainstAcceptedLifecycleDigest()
    {
        CompleteWorld world = CreateWorld();
        BurnupCoefficientTableV1 table = CreateTable(world);
        CompletePowerSnapshotV1 snapshot = CreateInitialSnapshot(world, table);
        ContractValidationResult<CompletePowerSnapshotResultV1> accepted =
            CompletePowerSnapshotTransitionV1.TryApply(world.Inventory, world.Lifecycle, snapshot);
        Assert.True(accepted.IsValid,
            accepted.IsValid ? string.Empty : accepted.FirstDiagnostic.ToString());

        BundleState target = accepted.Value.ResultingInventory.EnumerateOccupied().First();
        BundleState forged = new BundleState(
            target.BundleId,
            target.ChannelId,
            target.Position,
            target.MaterialVariantId,
            target.InitialBurnupJPerKgHm,
            target.CumulativeFissionEnergyJ,
            target.HeavyMetalMassKg,
            target.InsertedAtSeconds,
            target.NuclideState,
            target.PowerWatts,
            target.PowerSnapshotId,
            target.PowerHistory.Skip(1),
            target.CoefficientBinding,
            target.StateVersion);
        BundleInventory forgedInventory = BundleInventory.TryCreate(
            world.Topology,
            accepted.Value.ResultingInventory.EnumerateOccupied()
                .Select(bundle => bundle.BundleId == target.BundleId ? forged : bundle)).Value;

        CompletePowerSnapshotV1 forgedSnapshot = CompletePowerSnapshotV1.TryCreate(
            snapshot.SpatialSolveId,
            snapshot.PowerSnapshotId,
            snapshot.SnapshotTimeSeconds,
            snapshot.CoreStateVersion,
            snapshot.SpatialStateVersion,
            snapshot.PowerSnapshotVersion,
            snapshot.StateDigest,
            snapshot.InventoryDigest,
            CompleteStateDigestV1.ComputeInventory(forgedInventory),
            snapshot.BurnupEnergyDigest,
            snapshot.CoefficientDigest,
            snapshot.TopologyDigest,
            snapshot.DataPackDigest,
            snapshot.SnapshotDigest,
            snapshot.Bundles,
            snapshot.BundleNuclideVersions).Value;

        ContractValidationResult<CompleteBurnupIntervalResultV1> rejected =
            CompleteBurnupIntervalTransitionV1.TryApply(
                forgedInventory,
                accepted.Value.ResultingLifecycle,
                forgedSnapshot,
                10.0,
                new[] { table },
                Digest(0xc9));

        Assert.False(rejected.IsValid);
        Assert.Equal("CompleteBurnup.PowerSnapshot.StateDigestBinding.Stale", rejected.FirstDiagnostic.Code);
        Assert.Equal(snapshot.AcceptedInventoryDigest,
            accepted.Value.ResultingLifecycle.PowerSnapshotAcceptedInventoryDigest.Value);
        Assert.Equal(100UL, accepted.Value.ResultingLifecycle.CoreStateVersion);
        Assert.Equal(0.0, accepted.Value.ResultingLifecycle.CurrentSimulationTimeSeconds, 12);
    }

    [Fact]
    public void RoundTripsCompleteAcceptedSnapshotAndBurnupThroughStateArchive()
    {
        CompleteWorld world = CreateWorld();
        BurnupCoefficientTableV1 table = CreateTable(world);
        CompletePowerSnapshotV1 snapshot = CreateInitialSnapshot(world, table);
        ContractValidationResult<CompletePowerSnapshotResultV1> accepted =
            CompletePowerSnapshotTransitionV1.TryApply(world.Inventory, world.Lifecycle, snapshot);
        Assert.True(accepted.IsValid,
            accepted.IsValid ? string.Empty : accepted.FirstDiagnostic.ToString());

        VersionLifecycleV1 acceptedLifecycle = accepted.Value.ResultingLifecycle;
        ContractValidationResult<StateSnapshotV1> stateSnapshot = StateSnapshotV1.TryCreate(
            acceptedLifecycle.PowerSnapshotId.Value,
            acceptedLifecycle.PowerSnapshotVersion,
            acceptedLifecycle.CurrentSimulationTimeSeconds,
            accepted.Value.ResultingInventory,
            acceptedLifecycle,
            acceptedLifecycle.SnapshotDigest.Value!);
        Assert.True(stateSnapshot.IsValid,
            stateSnapshot.IsValid ? string.Empty : stateSnapshot.FirstDiagnostic.ToString());

        ContractValidationResult<string> serialized = StateArchiveCodecV1.Serialize(stateSnapshot.Value);
        Assert.True(serialized.IsValid,
            serialized.IsValid ? string.Empty : serialized.FirstDiagnostic.ToString());
        ContractValidationResult<StateSnapshotV1> restored = StateArchiveCodecV1.Deserialize(
            serialized.Value,
            world.Configuration);
        Assert.True(restored.IsValid,
            restored.IsValid ? string.Empty : restored.FirstDiagnostic.ToString());
        Assert.Equal(
            acceptedLifecycle.PowerSnapshotInventoryDigest.Value,
            restored.Value.Lifecycle.PowerSnapshotInventoryDigest.Value);
        Assert.Equal(
            acceptedLifecycle.PowerSnapshotAcceptedInventoryDigest.Value,
            restored.Value.Lifecycle.PowerSnapshotAcceptedInventoryDigest.Value);
        Assert.Equal(
            acceptedLifecycle.PowerSnapshotBurnupEnergyDigest.Value,
            restored.Value.Lifecycle.PowerSnapshotBurnupEnergyDigest.Value);
        Assert.Equal(
            CompleteStateDigestV1.ComputeInventory(accepted.Value.ResultingInventory),
            CompleteStateDigestV1.ComputeInventory(restored.Value.Inventory));

        ContractValidationResult<CompletePowerSnapshotV1> restoredPowerSnapshot =
            CompletePowerSnapshotV1.TryReconstructFromAcceptedState(
                restored.Value.Inventory,
                restored.Value.Lifecycle);
        Assert.True(
            restoredPowerSnapshot.IsValid,
            restoredPowerSnapshot.IsValid ? string.Empty : restoredPowerSnapshot.FirstDiagnostic.ToString());
        Assert.Equal(snapshot.ToCanonicalBytes(), restoredPowerSnapshot.Value.ToCanonicalBytes());

        ContractValidationResult<CompleteBurnupIntervalResultV1> burnup =
            CompleteBurnupIntervalTransitionV1.TryApply(
                restored.Value.Inventory,
                restored.Value.Lifecycle,
                restoredPowerSnapshot.Value,
                10.0,
                new[] { table },
                Digest(0xca));
        Assert.True(burnup.IsValid,
            burnup.IsValid ? string.Empty : burnup.FirstDiagnostic.ToString());
        Assert.Equal(10.0, burnup.Value.TargetTimeSeconds, 12);
        Assert.Equal(101UL, burnup.Value.ResultingLifecycle.CoreStateVersion);
    }

    [Fact]
    public void RejectsTamperedCompleteSnapshotArchiveDigestsAndState()
    {
        CompleteWorld world = CreateWorld();
        BurnupCoefficientTableV1 table = CreateTable(world);
        CompletePowerSnapshotV1 snapshot = CreateInitialSnapshot(world, table);
        ContractValidationResult<CompletePowerSnapshotResultV1> accepted =
            CompletePowerSnapshotTransitionV1.TryApply(world.Inventory, world.Lifecycle, snapshot);
        Assert.True(accepted.IsValid,
            accepted.IsValid ? string.Empty : accepted.FirstDiagnostic.ToString());

        VersionLifecycleV1 acceptedLifecycle = accepted.Value.ResultingLifecycle;
        ContractValidationResult<StateSnapshotV1> stateSnapshot = StateSnapshotV1.TryCreate(
            acceptedLifecycle.PowerSnapshotId.Value,
            acceptedLifecycle.PowerSnapshotVersion,
            acceptedLifecycle.CurrentSimulationTimeSeconds,
            accepted.Value.ResultingInventory,
            acceptedLifecycle,
            acceptedLifecycle.SnapshotDigest.Value!);
        Assert.True(stateSnapshot.IsValid,
            stateSnapshot.IsValid ? string.Empty : stateSnapshot.FirstDiagnostic.ToString());
        ContractValidationResult<string> serialized = StateArchiveCodecV1.Serialize(stateSnapshot.Value);
        Assert.True(serialized.IsValid,
            serialized.IsValid ? string.Empty : serialized.FirstDiagnostic.ToString());

        string tamperedPower = serialized.Value.Replace(
            "\"value_w\":10.0",
            "\"value_w\":11.0");
        ContractValidationResult<StateSnapshotV1> tamperedPowerResult = StateArchiveCodecV1.Deserialize(
            tamperedPower,
            world.Configuration);
        Assert.False(tamperedPowerResult.IsValid);
        Assert.Equal("VersionLifecycle.Restore.PowerSnapshotState.Invalid", tamperedPowerResult.FirstDiagnostic.Code);

        string tamperedEnergy = serialized.Value.Replace(
            "\"cumulative_fission_energy_j\":0.0",
            "\"cumulative_fission_energy_j\":1.0");
        ContractValidationResult<StateSnapshotV1> tamperedEnergyResult = StateArchiveCodecV1.Deserialize(
            tamperedEnergy,
            world.Configuration);
        Assert.False(tamperedEnergyResult.IsValid);
        Assert.Equal("VersionLifecycle.Restore.PowerSnapshotState.Invalid", tamperedEnergyResult.FirstDiagnostic.Code);

        string acceptedDigest = Hex(acceptedLifecycle.PowerSnapshotAcceptedInventoryDigest.Value!);
        string tamperedLifecycleDigest = serialized.Value.Replace(
            acceptedDigest,
            Hex(Digest(0xfe)));
        ContractValidationResult<StateSnapshotV1> tamperedDigestResult = StateArchiveCodecV1.Deserialize(
            tamperedLifecycleDigest,
            world.Configuration);
        Assert.False(tamperedDigestResult.IsValid);
        Assert.Equal("VersionLifecycle.Restore.PowerSnapshotState.Invalid", tamperedDigestResult.FirstDiagnostic.Code);

        string tamperedLifecycleVersion = ReplaceFirstAfter(
            serialized.Value,
            "\"bundle_nuclide_versions\":[",
            "\"nuclide_state_version\":0",
            "\"nuclide_state_version\":1");
        ContractValidationResult<StateSnapshotV1> tamperedVersionResult = StateArchiveCodecV1.Deserialize(
            tamperedLifecycleVersion,
            world.Configuration);
        Assert.False(tamperedVersionResult.IsValid);
        Assert.Equal("VersionLifecycle.Restore.BundleVersion.ValueMismatch", tamperedVersionResult.FirstDiagnostic.Code);
    }

    [Fact]
    public void ComposesBurnupLookupRecomputeAndStrictRefuellingWithRepeatableHistory()
    {
        CompleteRefuellingTransitionResultV1 first = RunCompleteSequence();
        CompleteRefuellingTransitionResultV1 second = RunCompleteSequence();

        Assert.Equal(first.ResultingLifecycle.CoreStateVersion, second.ResultingLifecycle.CoreStateVersion);
        Assert.Equal(first.ResultingLifecycle.CurrentSimulationTimeSeconds,
            second.ResultingLifecycle.CurrentSimulationTimeSeconds,
            12);
        Assert.Equal(first.ResultingLifecycle.StateDigest.Value, second.ResultingLifecycle.StateDigest.Value);
        Assert.Equal(first.DischargeRecords.Count, second.DischargeRecords.Count);

        BundleState[] firstBundles = first.ResultingInventory.EnumerateOccupied()
            .OrderBy(bundle => bundle.ChannelId.Value)
            .ThenBy(bundle => bundle.Position.Value)
            .ThenBy(bundle => bundle.BundleId)
            .ToArray();
        BundleState[] secondBundles = second.ResultingInventory.EnumerateOccupied()
            .OrderBy(bundle => bundle.ChannelId.Value)
            .ThenBy(bundle => bundle.Position.Value)
            .ThenBy(bundle => bundle.BundleId)
            .ToArray();
        Assert.Equal(firstBundles.Length, secondBundles.Length);
        for (int index = 0; index < firstBundles.Length; index++)
        {
            BundleState left = firstBundles[index];
            BundleState right = secondBundles[index];
            Assert.Equal(left.BundleId, right.BundleId);
            Assert.Equal(left.ChannelId, right.ChannelId);
            Assert.Equal(left.Position, right.Position);
            Assert.Equal(left.CumulativeFissionEnergyJ, right.CumulativeFissionEnergyJ, 12);
            Assert.Equal(left.CurrentBurnupJPerKgHm, right.CurrentBurnupJPerKgHm, 12);
            Assert.Equal(left.NuclideState!.ToCanonicalBytes(), right.NuclideState!.ToCanonicalBytes());
            Assert.Equal(left.PowerHistory.Count, right.PowerHistory.Count);
            Assert.Equal(left.PowerHistory.Select(record => record.ToCanonicalBytes()),
                right.PowerHistory.Select(record => record.ToCanonicalBytes()));
            Assert.Equal(left.CoefficientBinding!.ToCanonicalBytes(),
                right.CoefficientBinding!.ToCanonicalBytes());
            Assert.False(left.PowerWatts.IsApplicable);
            Assert.False(left.PowerSnapshotId.IsApplicable);
        }

        Assert.Equal(4, first.DischargeRecords.Count);
        Assert.All(first.DischargeRecords, record => Assert.NotEmpty(record.ToCanonicalBytes()));
        Assert.Equal(102UL, first.ResultingLifecycle.CoreStateVersion);
        Assert.Equal(BindingStatusV1.Invalid, first.ResultingLifecycle.PowerBindingStatus);
        Assert.All(first.ResultingInventory.EnumerateOccupied(), bundle =>
        {
            Assert.NotNull(bundle.CoefficientBinding);
            Assert.False(bundle.PowerWatts.IsApplicable);
            Assert.False(bundle.PowerSnapshotId.IsApplicable);
        });

        Assert.Equal(4, first.ResultingInventory.EnumerateOccupied()
            .Count(bundle => bundle.PowerHistory.Count == 2));
        Assert.Equal(4, first.ResultingInventory.EnumerateOccupied()
            .Count(bundle => bundle.PowerHistory.Count == 0));
        Assert.All(first.ResultingInventory.EnumerateOccupied()
            .Where(bundle => bundle.PowerHistory.Count == 0), bundle => Assert.Empty(bundle.PowerHistory));
    }

    private static CompleteRefuellingTransitionResultV1 RunCompleteSequence()
    {
        CompleteWorld world = CreateWorld();
        BurnupCoefficientTableV1 table = CreateTable(world);
        CompletePowerSnapshotV1 initialSnapshot = CreateInitialSnapshot(world, table);
        ContractValidationResult<CompletePowerSnapshotResultV1> acceptedSnapshotResult =
            CompletePowerSnapshotTransitionV1.TryApply(
                world.Inventory,
                world.Lifecycle,
                initialSnapshot);
        Assert.True(acceptedSnapshotResult.IsValid,
            acceptedSnapshotResult.IsValid ? string.Empty : acceptedSnapshotResult.FirstDiagnostic.ToString());
        CompletePowerSnapshotResultV1 acceptedSnapshot = acceptedSnapshotResult.Value;
        ContractValidationResult<CompleteBurnupIntervalResultV1> burnupResult =
            CompleteBurnupIntervalTransitionV1.TryApply(
                acceptedSnapshot.ResultingInventory,
                acceptedSnapshot.ResultingLifecycle,
                initialSnapshot,
                10.0,
                new[] { table },
                Digest(0xc1));
        Assert.True(burnupResult.IsValid,
            burnupResult.IsValid ? string.Empty : burnupResult.FirstDiagnostic.ToString());
        CompleteBurnupIntervalResultV1 burnup = burnupResult.Value;

        SpatialStencil stencil = SpatialStencil.TryCreate(world.Topology).Value;
        SpatialRecomputeCadenceV1 cadence = SpatialRecomputeCadenceV1.TryCreate(10.0, 10.0, 0).Value;
        SpatialRecomputeRequestV1 request = SpatialRecomputeRequestV1.TryCreate(
            burnup.ResultingLifecycle,
            burnup.ResultingInventory,
            CreateConductanceSource(stencil),
            new[] { table },
            world.Volumes.Select(pair => SpatialNodeVolumeV1.TryCreate(pair.Key, pair.Value).Value),
            CreateLinearPolicy(),
            CreateConvergencePolicy(),
            0.4,
            1.0,
            null,
            null,
            cadence,
            10.0,
            world.Data.DataPackVersion,
            Id(10001),
            Id(10002),
            Digest(0xc4),
            Digest(0xc5)).Value;
        ContractValidationResult<SpatialRecomputeResultV1> recomputed =
            SpatialStateRecomputeV1.TryApply(request);
        Assert.True(recomputed.IsValid, recomputed.IsValid ? string.Empty : recomputed.FirstDiagnostic.ToString());
        Assert.Equal(8, recomputed.Value.LookupBindings.Count);

        Dictionary<StableId, SpatialCoefficientLookupBindingV1> lookupByBundle =
            recomputed.Value.LookupBindings.ToDictionary(binding => binding.BundleId, binding => binding);
        BundleNuclideVersionV1[] recomputedVersions = recomputed.Value.AcceptedLifecycle.BundleNuclideVersions.ToArray();
        CompletePowerSnapshotBundleV1[] entries = burnup.ResultingInventory.EnumerateOccupied()
            .OrderBy(bundle => bundle.ChannelId.Value)
            .ThenBy(bundle => bundle.Position.Value)
            .ThenBy(bundle => bundle.BundleId)
            .Select(bundle => CompletePowerSnapshotBundleV1.TryCreate(
                bundle.BundleId,
                bundle.ChannelId,
                bundle.Position,
                10.0,
                bundle.NuclideState!.NuclideStateVersion,
                BundleCoefficientBindingV1.TryCreate(
                    lookupByBundle[bundle.BundleId].Lookup.TableId,
                    lookupByBundle[bundle.BundleId].Lookup.BracketLowerIndex,
                    lookupByBundle[bundle.BundleId].Lookup.BracketUpperIndex,
                    lookupByBundle[bundle.BundleId].Lookup.InterpolationFraction,
                    lookupByBundle[bundle.BundleId].Lookup.Checksum).Value).Value)
            .ToArray();
        CompletePowerSnapshotV1 postRecomputeSnapshot = CompletePowerSnapshotV1.TryCreate(
            recomputed.Value.SpatialSolveId,
            recomputed.Value.PowerSnapshotId,
            10.0,
            recomputed.Value.AcceptedLifecycle.CoreStateVersion,
            recomputed.Value.AcceptedLifecycle.SpatialStateVersion,
            recomputed.Value.AcceptedLifecycle.PowerSnapshotVersion,
            recomputed.Value.AcceptedLifecycle.StateDigest.Value!,
            CompleteStateDigestV1.ComputeInventory(burnup.ResultingInventory),
            CompleteStateDigestV1.TryComputeAcceptedInventory(
                burnup.ResultingInventory,
                recomputed.Value.PowerSnapshotId,
                10.0,
                recomputed.Value.AcceptedLifecycle.CoreStateVersion,
                recomputed.Value.AcceptedLifecycle.SpatialStateVersion,
                recomputed.Value.AcceptedLifecycle.PowerSnapshotVersion,
                entries).Value,
            CompleteStateDigestV1.ComputeBurnupEnergy(burnup.ResultingInventory),
            recomputed.Value.CoefficientDigest,
            recomputed.Value.AcceptedLifecycle.TopologyDigest.Value!,
            recomputed.Value.AcceptedLifecycle.DataPackDigest.Value!,
            recomputed.Value.SnapshotDigest,
            entries,
            recomputedVersions).Value;
        ContractValidationResult<CompletePowerSnapshotResultV1> acceptedAtTargetResult =
            CompletePowerSnapshotTransitionV1.TryApply(
                burnup.ResultingInventory,
                burnup.ResultingLifecycle,
                postRecomputeSnapshot);
        Assert.True(acceptedAtTargetResult.IsValid,
            acceptedAtTargetResult.IsValid ? string.Empty : acceptedAtTargetResult.FirstDiagnostic.ToString());
        CompletePowerSnapshotResultV1 acceptedAtTarget = acceptedAtTargetResult.Value;

        RefuelSchemeDefinition scheme = RefuelSchemeDefinition.TryCreate(
            "S4",
            4,
            "fresh-template-v1",
            RefuelSchemeDefinition.CurrentSchemaVersion,
            Enumerable.Range(4, 4).Select(value => new BundlePosition((uint)value)),
            Enumerable.Range(0, 4).Select(value => new BundlePosition((uint)value))).Value;
        RefuelSchemePositionPlan plan = scheme.TryCreatePositionPlan(
            world.Topology.BundlePositionCount,
            FlowDirection.EndAtoEndB).Value;
        BundleState[] inserted = plan.InsertedPositions
            .Select((position, index) => CreateFreshBundle(world, Id(11000 + index), position, 10.0))
            .ToArray();
        ContractValidationResult<RefuelShiftResult> shiftResult = RefuelShiftTransition.TryApply(
            acceptedAtTarget.ResultingInventory,
            new ChannelId(0),
            plan,
            inserted,
            10.0,
            10.0);
        Assert.True(shiftResult.IsValid, shiftResult.IsValid ? string.Empty : shiftResult.FirstDiagnostic.ToString());
        RefuelShiftResult shift = shiftResult.Value;
        CompleteRefuellingRequestV1 requestForRefuelling = CompleteRefuellingRequestV1.TryCreate(
            shift,
            acceptedAtTarget.ResultingLifecycle).Value;
        ContractValidationResult<CompleteRefuellingTransitionResultV1> refuelled =
            CompleteRefuellingTransitionV1.TryApplyWithCoefficientTables(
                requestForRefuelling,
                acceptedAtTarget.ResultingInventory,
                acceptedAtTarget.ResultingLifecycle,
                world.Volumes.Select(pair => SpatialNodeVolumeV1.TryCreate(pair.Key, pair.Value).Value),
                Id(12000),
                Digest(0xc6),
                new[] { table });
        Assert.True(refuelled.IsValid, refuelled.IsValid ? string.Empty : refuelled.FirstDiagnostic.ToString());
        return refuelled.Value;
    }

    private static CompletePowerSnapshotV1 CreateInitialSnapshot(
        CompleteWorld world,
        BurnupCoefficientTableV1 table)
    {
        return CreateSnapshot(world.Inventory, world.Lifecycle, table, Id(9800), Id(9801), 10.0);
    }

    private static CompletePowerSnapshotV1 CreateSnapshot(
        BundleInventory inventory,
        VersionLifecycleV1 lifecycle,
        BurnupCoefficientTableV1 table,
        StableId spatialSolveId,
        StableId powerSnapshotId,
        double powerWatts)
    {
        BundleCoefficientBindingV1 binding = BundleCoefficientBindingV1.TryCreate(
            table.TableId,
            0,
            0,
            0.0,
            table.Checksum).Value;
        CompletePowerSnapshotBundleV1[] entries = inventory.EnumerateOccupied()
            .OrderBy(bundle => bundle.ChannelId.Value)
            .ThenBy(bundle => bundle.Position.Value)
            .ThenBy(bundle => bundle.BundleId)
            .Select(bundle => CompletePowerSnapshotBundleV1.TryCreate(
                bundle.BundleId,
                bundle.ChannelId,
                bundle.Position,
                powerWatts,
                bundle.NuclideState!.NuclideStateVersion,
                binding).Value)
            .ToArray();
        return CompletePowerSnapshotV1.TryCreate(
            spatialSolveId,
            powerSnapshotId,
            lifecycle.CurrentSimulationTimeSeconds,
            lifecycle.CoreStateVersion,
            lifecycle.SpatialStateVersion + 1,
            lifecycle.PowerSnapshotVersion + 1,
            lifecycle.StateDigest.Value!,
            CompleteStateDigestV1.ComputeInventory(inventory),
            CompleteStateDigestV1.TryComputeAcceptedInventory(
                inventory,
                powerSnapshotId,
                lifecycle.CurrentSimulationTimeSeconds,
                lifecycle.CoreStateVersion,
                lifecycle.SpatialStateVersion + 1,
                lifecycle.PowerSnapshotVersion + 1,
                entries).Value,
            CompleteStateDigestV1.ComputeBurnupEnergy(inventory),
            Digest(0x91),
            lifecycle.TopologyDigest.Value!,
            lifecycle.DataPackDigest.Value!,
            Digest(0x92),
            entries,
            lifecycle.BundleNuclideVersions).Value;
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
        for (uint position = 0; position < positionCount; position++)
        {
            foreach (TopologyFace face in TransverseFaces)
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
            "synthetic-p5-t11",
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
                StableId bundleId = Id(9700 + index);
                NuclideStateEnvelopeV1 state = NuclideStateEnvelopeV1.TryCreateFresh(
                    bundleId,
                    10.0 + index,
                    20.0 + index,
                    volumes[node],
                    0,
                    data).Value;
                return new BundleState(
                    bundleId,
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
        return new CompleteWorld(topology, dataPack, configuration, inventory, lifecycle, volumes);
    }

    private static BundleState CreateFreshBundle(
        CompleteWorld world,
        StableId bundleId,
        BundlePosition position,
        double insertedAt)
    {
        NodeKey node = new NodeKey(new ChannelId(0), position);
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
        NuclideStateEnvelopeV1 state = NuclideStateEnvelopeV1.TryCreateFresh(
            bundleId,
            3.0,
            4.0,
            world.Volumes[node],
            0,
            data).Value;
        return new BundleState(
            bundleId,
            new ChannelId(0),
            position,
            data.MaterialVariantId,
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

    private static BurnupCoefficientTableV1 CreateTable(
        CompleteWorld world,
        double maximumBurnup = 100_000.0)
    {
        BurnupCoefficientValuesV1 firstValues = BurnupCoefficientValuesV1.TryCreate(
            0.3,
            0.2,
            0.1,
            0.1,
            0.15,
            0.25,
            0.1,
            1.0,
            1.0).Value;
        BurnupCoefficientValuesV1 secondValues = BurnupCoefficientValuesV1.TryCreate(
            0.4,
            0.3,
            0.1,
            0.1,
            0.15,
            0.25,
            0.1,
            1.0,
            1.0).Value;
        BurnupCoefficientRowV1 firstRow = BurnupCoefficientRowV1.TryCreate(0.0, firstValues).Value;
        BurnupCoefficientRowV1 secondRow = BurnupCoefficientRowV1.TryCreate(maximumBurnup, secondValues).Value;
        return BurnupCoefficientTableV1.TryCreate(
            Id(9601),
            BurnupCoefficientTableV1.CurrentSchemaVersion,
            world.Data.DataPackVersion,
            new MaterialVariantId("synthetic-fuel"),
            world.Data.UnitsProfileId,
            "synthetic:P5-T11",
            Digest(0xd1),
            new[] { firstRow, secondRow }).Value;
    }

    private static SpatialCoefficientSet CreateConductanceSource(SpatialStencil stencil)
    {
        SpatialNodeCoefficients[] nodes = stencil.Nodes
            .Select(node => new SpatialNodeCoefficients(
                node.Node,
                1.0,
                0.3,
                0.2,
                0.1,
                0.1,
                0.1,
                0.15,
                0.25,
                1.0,
                0.0,
                1.0))
            .ToArray();
        var edges = new List<SpatialEdgeConductance>();
        var seenEdges = new HashSet<string>(StringComparer.Ordinal);
        foreach (SpatialNodeStencil node in stencil.Nodes)
        {
            foreach (SpatialNeighborTerm neighbor in node.NeighborTerms)
            {
                NodeKey first = node.Node.CompareTo(neighbor.TargetNode) <= 0
                    ? node.Node
                    : neighbor.TargetNode;
                NodeKey second = node.Node.CompareTo(neighbor.TargetNode) <= 0
                    ? neighbor.TargetNode
                    : node.Node;
                string key = first + "-" + second;
                if (seenEdges.Add(key))
                {
                    edges.Add(new SpatialEdgeConductance(first, second, 0.5, 0.5));
                }
            }
        }

        SpatialBoundaryConductance[] boundaries = stencil.Nodes
            .SelectMany(node => node.BoundaryTerms.Select(term => new SpatialBoundaryConductance(
                node.Node,
                term.Face,
                0.0,
                0.0)))
            .ToArray();
        return SpatialCoefficientSet.TryCreate(stencil, nodes, edges, boundaries).Value;
    }

    private static SpatialLinearSolvePolicy CreateLinearPolicy()
    {
        return SpatialLinearSolvePolicy.TryCreate(
            SpatialLinearSolvePolicy.DeterministicJacobiMethodId,
            SpatialLinearSolvePolicy.DeterministicJacobiMethodVersion,
            1e-14,
            1e-14,
            512).Value;
    }

    private static SpatialConvergencePolicy CreateConvergencePolicy()
    {
        return SpatialConvergencePolicy.TryCreate(
            0.01,
            0.01,
            1e-12,
            1e-12,
            1e-12,
            512).Value;
    }

    private static Digest32 Digest(byte value)
    {
        return new Digest32(Enumerable.Repeat(value, 32).ToArray());
    }

    private static string Hex(Digest32 digest)
    {
        return string.Concat(digest.Bytes.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    private static string ReplaceFirstAfter(
        string source,
        string marker,
        string oldValue,
        string newValue)
    {
        int markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, "The serialized archive marker was not found.");
        int valueIndex = source.IndexOf(oldValue, markerIndex, StringComparison.Ordinal);
        Assert.True(valueIndex >= 0, "The serialized archive value was not found after the marker.");
        return source.Remove(valueIndex, oldValue.Length).Insert(valueIndex, newValue);
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
            DataPackDescriptor data,
            SimulationConfiguration configuration,
            BundleInventory inventory,
            VersionLifecycleV1 lifecycle,
            Dictionary<NodeKey, double> volumes)
        {
            Topology = topology;
            Data = data;
            Configuration = configuration;
            Inventory = inventory;
            Lifecycle = lifecycle;
            Volumes = volumes;
        }

        public CoreTopology Topology { get; }

        public DataPackDescriptor Data { get; }

        public SimulationConfiguration Configuration { get; }

        public BundleInventory Inventory { get; }

        public VersionLifecycleV1 Lifecycle { get; }

        public Dictionary<NodeKey, double> Volumes { get; }
    }
}
