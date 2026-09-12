using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P5T04BurnupTransitionTests
{
    [Fact]
    public void AppliesCanonicalLeftEndpointPowerAndPreservesSourceInventory()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        BundleState[] sourceBundles = fixture.Inventory.EnumerateOccupied().ToArray();
        BundlePowerSampleV1[] samples = sourceBundles
            .Reverse()
            .Select((bundle, index) => CreatePowerSample(bundle, index + 1.0))
            .ToArray();
        AcceptedBundlePowerSnapshotV1 snapshot = CreateSnapshot(
            samples,
            10.0,
            7);

        ContractValidationResult<BurnupIntervalResultV1> result =
            BurnupIntervalTransition.TryApply(
                fixture.Inventory,
                7,
                10.0,
                20.0,
                snapshot.StateDigest,
                snapshot);

        AssertValid(result);
        BurnupIntervalResultV1 applied = result.Value;
        Assert.Same(fixture.Inventory, applied.SourceInventory);
        Assert.NotSame(fixture.Inventory, applied.ResultingInventory);
        Assert.Equal(10.0, applied.DeltaTimeSeconds, 12);
        Assert.Equal(7UL, applied.CoreStateVersionBefore);
        Assert.Equal(8UL, applied.CoreStateVersionAfter);
        Assert.True(applied.PowerSnapshotInvalidated);
        Assert.Equal(sourceBundles.Length, applied.Records.Count);

        BundleState[] canonicalSource = sourceBundles
            .OrderBy(bundle => bundle.ChannelId.Value)
            .ThenBy(bundle => bundle.Position.Value)
            .ThenBy(bundle => bundle.BundleId)
            .ToArray();
        for (int index = 0; index < canonicalSource.Length; index++)
        {
            BundleState before = canonicalSource[index];
            BurnupIntervalRecordV1 record = applied.Records[index];
            BundleState after = applied.ResultingInventory.Get(before.Node)!;
            double expectedPower = samples[canonicalSource.Length - 1 - index].PowerWatts;
            double expectedEnergy = expectedPower * 10.0;

            Assert.Equal(before.BundleId, record.BundleId);
            Assert.Equal(before.Node, new NodeKey(record.ChannelId, record.Position));
            Assert.Equal(expectedPower, record.PowerWatts, 12);
            Assert.Equal(expectedEnergy, record.DeltaFissionEnergyJ, 12);
            Assert.Equal(expectedEnergy, record.NewCumulativeFissionEnergyJ, 12);
            Assert.Equal(expectedEnergy / before.HeavyMetalMassKg, record.NewBurnupJPerKgHm, 12);
            Assert.Equal(expectedEnergy, after.CumulativeFissionEnergyJ, 12);
            Assert.Equal(record.NewBurnupJPerKgHm, after.CurrentBurnupJPerKgHm, 12);
            Assert.Same(before, fixture.Inventory.Get(before.Node));
        }
    }

    [Fact]
    public void SupportsDeterministicMultistepEnergyAndMonotoneBurnup()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        BundleState[] sourceBundles = fixture.Inventory.EnumerateOccupied().ToArray();
        AcceptedBundlePowerSnapshotV1 firstSnapshot = CreateSnapshot(
            sourceBundles.Select(bundle => CreatePowerSample(bundle, 2.0)),
            0.0,
            0);

        ContractValidationResult<BurnupIntervalResultV1> first =
            BurnupIntervalTransition.TryApply(
                fixture.Inventory,
                0,
                0.0,
                5.0,
                firstSnapshot.StateDigest,
                firstSnapshot);
        AssertValid(first);

        BundleState[] firstBundles = first.Value.ResultingInventory.EnumerateOccupied().ToArray();
        AcceptedBundlePowerSnapshotV1 secondSnapshot = CreateSnapshot(
            firstBundles.Select(bundle => CreatePowerSample(bundle, 3.0)),
            5.0,
            first.Value.CoreStateVersionAfter);
        ContractValidationResult<BurnupIntervalResultV1> second =
            BurnupIntervalTransition.TryApply(
                first.Value.ResultingInventory,
                first.Value.CoreStateVersionAfter,
                5.0,
                9.0,
                secondSnapshot.StateDigest,
                secondSnapshot);
        AssertValid(second);

        foreach (BundleState source in sourceBundles)
        {
            BundleState final = second.Value.ResultingInventory.Get(source.Node)!;
            Assert.Equal(22.0, final.CumulativeFissionEnergyJ, 12);
            Assert.Equal(22.0 / final.HeavyMetalMassKg, final.CurrentBurnupJPerKgHm, 12);
            BurnupIntervalRecordV1 firstRecord = first.Value.Records.Single(record => record.BundleId == source.BundleId);
            BurnupIntervalRecordV1 secondRecord = second.Value.Records.Single(record => record.BundleId == source.BundleId);
            Assert.True(secondRecord.NewBurnupJPerKgHm >= firstRecord.NewBurnupJPerKgHm);
        }
    }

    [Fact]
    public void RejectsStaleIncompleteOrOverflowingInputsWithoutChangingSource()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        BundleState original = fixture.Inventory.Get(new NodeKey(new ChannelId(0), new BundlePosition(0)))!;
        BundleState[] bundles = fixture.Inventory.EnumerateOccupied().ToArray();

        AcceptedBundlePowerSnapshotV1 staleSnapshot = CreateSnapshot(
            bundles.Select(bundle => CreatePowerSample(bundle, 1.0)),
            4.0,
            0,
            17);
        ContractValidationResult<BurnupIntervalResultV1> stale =
            BurnupIntervalTransition.TryApply(
                fixture.Inventory,
                0,
                5.0,
                6.0,
                staleSnapshot.StateDigest,
                staleSnapshot);
        AssertInvalid(stale, "BurnupInterval.PowerSnapshot.Time.Stale");

        AcceptedBundlePowerSnapshotV1 staleDigestSnapshot = CreateSnapshot(
            bundles.Select(bundle => CreatePowerSample(bundle, 1.0)),
            5.0,
            0,
            18);
        ContractValidationResult<BurnupIntervalResultV1> staleDigest =
            BurnupIntervalTransition.TryApply(
                fixture.Inventory,
                0,
                5.0,
                6.0,
                CreateDigest(19),
                staleDigestSnapshot);
        AssertInvalid(staleDigest, "BurnupInterval.PowerSnapshot.StateDigest.Stale");

        AcceptedBundlePowerSnapshotV1 missingSnapshot = CreateSnapshot(
            bundles.Skip(1).Select(bundle => CreatePowerSample(bundle, 1.0)),
            5.0,
            0);
        ContractValidationResult<BurnupIntervalResultV1> missing =
            BurnupIntervalTransition.TryApply(
                fixture.Inventory,
                0,
                5.0,
                6.0,
                missingSnapshot.StateDigest,
                missingSnapshot);
        AssertInvalid(missing, "BurnupInterval.PowerSnapshot.BundlePower.CountMismatch");

        AcceptedBundlePowerSnapshotV1 overflowSnapshot = CreateSnapshot(
            bundles.Select(bundle => CreatePowerSample(bundle, double.MaxValue)),
            5.0,
            0);
        ContractValidationResult<BurnupIntervalResultV1> overflow =
            BurnupIntervalTransition.TryApply(
                fixture.Inventory,
                0,
                5.0,
                7.0,
                overflowSnapshot.StateDigest,
                overflowSnapshot);
        AssertInvalid(overflow, "BurnupInterval.EnergyIncrement.Invalid");

        BundleState nearCapacity = new BundleState(
            original.BundleId,
            original.ChannelId,
            original.Position,
            original.MaterialVariantId,
            0.0,
            double.MaxValue,
            1.0,
            original.InsertedAtSeconds);
        ContractValidationResult<BundleInventory> nearCapacityInventory = BundleInventory.TryCreate(
            fixture.Topology,
            bundles.Select(bundle => bundle.BundleId == original.BundleId ? nearCapacity : bundle));
        AssertValid(nearCapacityInventory);
        AcceptedBundlePowerSnapshotV1 roundedAwaySnapshot = CreateSnapshot(
            nearCapacityInventory.Value.EnumerateOccupied().Select(bundle => CreatePowerSample(bundle, 1.0)),
            5.0,
            0);
        ContractValidationResult<BurnupIntervalResultV1> roundedAway =
            BurnupIntervalTransition.TryApply(
                nearCapacityInventory.Value,
                0,
                5.0,
                6.0,
                roundedAwaySnapshot.StateDigest,
                roundedAwaySnapshot);
        AssertInvalid(roundedAway, "BurnupInterval.CumulativeEnergy.NoRepresentableIncrease");

        ContractValidationResult<BundlePowerSampleV1> negativePower =
            BundlePowerSampleV1.TryCreate(
                bundles[0].BundleId,
                bundles[0].ChannelId,
                bundles[0].Position,
                -1.0);
        AssertInvalid(negativePower, "BundlePowerSample.Power.Invalid");

        ContractValidationResult<BundlePowerSampleV1> nonFinitePower =
            BundlePowerSampleV1.TryCreate(
                bundles[0].BundleId,
                bundles[0].ChannelId,
                bundles[0].Position,
                double.NaN);
        AssertInvalid(nonFinitePower, "BundlePowerSample.Power.Invalid");

        BundleState overflowedBurnup = new BundleState(
            original.BundleId,
            original.ChannelId,
            original.Position,
            original.MaterialVariantId,
            double.MaxValue,
            double.MaxValue,
            1.0,
            original.InsertedAtSeconds);
        ContractValidationResult<BundleInventory> invalidInventory = BundleInventory.TryCreate(
            fixture.Topology,
            bundles.Select(bundle => bundle.BundleId == original.BundleId ? overflowedBurnup : bundle));
        AssertInvalid(invalidInventory, "BundleInventory.Burnup.Invalid");

        Assert.Same(original, fixture.Inventory.Get(original.Node));
        Assert.Equal(0.0, original.CumulativeFissionEnergyJ);
        Assert.Equal(0.0, original.CurrentBurnupJPerKgHm);
    }

    private static BundlePowerSampleV1 CreatePowerSample(BundleState bundle, double powerWatts)
    {
        ContractValidationResult<BundlePowerSampleV1> result = BundlePowerSampleV1.TryCreate(
            bundle.BundleId,
            bundle.ChannelId,
            bundle.Position,
            powerWatts);
        AssertValid(result);
        return result.Value;
    }

    private static AcceptedBundlePowerSnapshotV1 CreateSnapshot(
        IEnumerable<BundlePowerSampleV1> samples,
        double snapshotTimeSeconds,
        ulong coreStateVersion,
        byte digestMarker = 17)
    {
        ContractValidationResult<AcceptedBundlePowerSnapshotV1> result =
            AcceptedBundlePowerSnapshotV1.TryCreate(
                StableId.Parse("00000000-0000-0000-0000-0000000007a1"),
                snapshotTimeSeconds,
                coreStateVersion,
                CreateDigest(digestMarker),
                samples);
        AssertValid(result);
        return result.Value;
    }

    private static Digest32 CreateDigest(byte marker)
    {
        byte[] bytes = new byte[32];
        bytes[0] = marker;
        return new Digest32(bytes);
    }

    private static void AssertValid<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
    }

    private static void AssertInvalid<T>(ContractValidationResult<T> result, string code)
    {
        Assert.False(result.IsValid);
        Assert.Equal(code, result.FirstDiagnostic.Code);
    }
}
