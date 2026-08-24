using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P5T08DeterministicHistoryTests
{
    [Fact]
    public void ProducesRepeatableS4HistoryAcrossShuffledAcceptedPowerInputs()
    {
        HistoryTrace first = RunHistory(
            FlowDirection.EndAtoEndB,
            4,
            32,
            reverseAcceptedPowerOrder: false);
        HistoryTrace replay = RunHistory(
            FlowDirection.EndAtoEndB,
            4,
            32,
            reverseAcceptedPowerOrder: true);

        Assert.Equal(first.Fingerprint, replay.Fingerprint);
        Assert.Equal(32, first.BurnupIntervalCount);
        Assert.Equal(32, first.RefuelShiftCount);
        Assert.Equal(64UL, first.FinalCoreStateVersion - 100UL);
        Assert.Equal(320.0, first.FinalTimeSeconds);
        Assert.Equal(140, first.IntroducedIdentityCount);
        Assert.Equal(12, first.FinalInventory.OccupiedCount);
    }

    [Fact]
    public void ProducesRepeatableS8HistoryAcrossShuffledAcceptedPowerInputs()
    {
        HistoryTrace first = RunHistory(
            FlowDirection.EndBtoEndA,
            8,
            24,
            reverseAcceptedPowerOrder: false);
        HistoryTrace replay = RunHistory(
            FlowDirection.EndBtoEndA,
            8,
            24,
            reverseAcceptedPowerOrder: true);

        Assert.Equal(first.Fingerprint, replay.Fingerprint);
        Assert.Equal(24, first.BurnupIntervalCount);
        Assert.Equal(24, first.RefuelShiftCount);
        Assert.Equal(48UL, first.FinalCoreStateVersion - 100UL);
        Assert.Equal(240.0, first.FinalTimeSeconds);
        Assert.Equal(204, first.IntroducedIdentityCount);
        Assert.Equal(12, first.FinalInventory.OccupiedCount);
    }

    [Fact]
    public void RejectsIncompleteHistoryIntervalWithoutAdvancingSourceState()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        BundleState[] bundles = CanonicalBundles(fixture.Inventory).ToArray();
        Digest32 sourceDigest = ComputeInventoryDigest(fixture.Inventory);
        ContractValidationResult<AcceptedBundlePowerSnapshotV1> snapshot =
            AcceptedBundlePowerSnapshotV1.TryCreate(
                Id(7000),
                0.0,
                100,
                sourceDigest,
                bundles.Skip(1).Select(bundle => CreatePowerSample(bundle, 1.0)));
        AssertValid(snapshot);

        ContractValidationResult<BurnupIntervalResultV1> rejected =
            BurnupIntervalTransition.TryApply(
                fixture.Inventory,
                100,
                0.0,
                10.0,
                sourceDigest,
                snapshot.Value);

        AssertInvalid(rejected, "BurnupInterval.PowerSnapshot.BundlePower.CountMismatch");
        foreach (BundleState original in bundles)
        {
            Assert.Same(original, fixture.Inventory.Get(original.Node));
        }

        Assert.Equal(0.0, fixture.Inventory.EnumerateOccupied()
            .Sum(bundle => bundle.CumulativeFissionEnergyJ));
    }

    private static HistoryTrace RunHistory(
        FlowDirection flowDirection,
        ushort shiftCount,
        int cycleCount,
        bool reverseAcceptedPowerOrder)
    {
        CoreTopology topology = CreateSingleChannelTopology(flowDirection);
        BundleInventory inventory = CreateInventory(topology);
        RefuelSchemePositionPlan plan = CreatePlan(shiftCount, flowDirection);
        var introducedIds = new HashSet<StableId>(CanonicalBundles(inventory).Select(bundle => bundle.BundleId));
        var liveBurnupById = CanonicalBundles(inventory)
            .ToDictionary(bundle => bundle.BundleId, bundle => bundle.CurrentBurnupJPerKgHm);
        var trace = new StringBuilder();
        ulong coreStateVersion = 100;
        double currentTimeSeconds = 0.0;
        int burnupIntervalCount = 0;
        int refuelShiftCount = 0;

        for (int cycle = 0; cycle < cycleCount; cycle++)
        {
            BundleState[] canonical = CanonicalBundles(inventory).ToArray();
            BundlePowerSampleV1[] samples = canonical
                .Select(bundle => CreatePowerSample(
                    bundle,
                    20.0 + (NumericId(bundle.BundleId) % 13) + (cycle % 7)))
                .ToArray();
            if (reverseAcceptedPowerOrder)
            {
                samples = samples.Reverse().ToArray();
            }

            Digest32 sourceDigest = ComputeInventoryDigest(inventory);
            ContractValidationResult<AcceptedBundlePowerSnapshotV1> snapshot =
                AcceptedBundlePowerSnapshotV1.TryCreate(
                    Id(7000 + cycle),
                    currentTimeSeconds,
                    coreStateVersion,
                    sourceDigest,
                    samples);
            AssertValid(snapshot);

            ContractValidationResult<BurnupIntervalResultV1> burnup =
                BurnupIntervalTransition.TryApply(
                    inventory,
                    coreStateVersion,
                    currentTimeSeconds,
                    currentTimeSeconds + 10.0,
                    sourceDigest,
                    snapshot.Value);
            AssertValid(burnup);
            ContractValidationResult<Phase5InvariantReportV1> burnupInvariant =
                Phase5InvariantValidatorV1.TryValidateBurnupInterval(
                    burnup.Value,
                    topology.SlotCount);
            AssertValid(burnupInvariant);
            Assert.Equal(topology.SlotCount, burnupInvariant.Value.EnergyRecordCount);
            Assert.Equal(topology.SlotCount, burnupInvariant.Value.MonotonicBurnupCount);
            Assert.Equal(
                samples.Sum(sample => sample.PowerWatts * 10.0),
                burnupInvariant.Value.TotalIntervalEnergyJ);

            inventory = burnup.Value.ResultingInventory;
            foreach (BundleState afterBurnup in CanonicalBundles(inventory))
            {
                Assert.True(
                    afterBurnup.CurrentBurnupJPerKgHm >= liveBurnupById[afterBurnup.BundleId],
                    "A live bundle's burnup must remain nondecreasing across history intervals.");
                liveBurnupById[afterBurnup.BundleId] = afterBurnup.CurrentBurnupJPerKgHm;
            }

            coreStateVersion = burnup.Value.CoreStateVersionAfter;
            currentTimeSeconds += 10.0;
            burnupIntervalCount++;

            BundleState[] inserted = plan.InsertedPositions
                .Select((position, index) => new BundleState(
                    Id(10000 + (cycle * shiftCount) + index),
                    new ChannelId(0),
                    position,
                    new MaterialVariantId("MAT-FRESH"),
                    0.0,
                    0.0,
                    1000.0,
                    currentTimeSeconds))
                .ToArray();
            foreach (BundleState fresh in inserted)
            {
                Assert.True(introducedIds.Add(fresh.BundleId));
            }

            ContractValidationResult<RefuelShiftResult> shift = RefuelShiftTransition.TryApply(
                inventory,
                new ChannelId(0),
                plan,
                inserted,
                currentTimeSeconds,
                currentTimeSeconds);
            AssertValid(shift);
            ContractValidationResult<Phase5InvariantReportV1> shiftInvariant =
                Phase5InvariantValidatorV1.TryValidateRefuelShift(
                    shift.Value,
                    topology.SlotCount);
            AssertValid(shiftInvariant);
            Assert.Equal(topology.SlotCount - shiftCount, shiftInvariant.Value.PreservedBundleCount);
            Assert.Equal(shiftCount, shiftInvariant.Value.InsertedBundleCount);
            Assert.Equal(shiftCount, shiftInvariant.Value.DischargedBundleCount);

            foreach (BundleState discharged in shift.Value.DischargedBundles)
            {
                Assert.True(liveBurnupById.Remove(discharged.BundleId));
            }

            inventory = shift.Value.ResultingInventory;
            foreach (BundleState afterShift in CanonicalBundles(inventory))
            {
                double previousBurnup;
                if (liveBurnupById.TryGetValue(afterShift.BundleId, out previousBurnup))
                {
                    Assert.Equal(previousBurnup, afterShift.CurrentBurnupJPerKgHm);
                }
                else
                {
                    Assert.Equal(afterShift.InitialBurnupJPerKgHm, afterShift.CurrentBurnupJPerKgHm);
                }

                liveBurnupById[afterShift.BundleId] = afterShift.CurrentBurnupJPerKgHm;
            }

            coreStateVersion++;
            refuelShiftCount++;
            AppendTrace(
                trace,
                cycle,
                coreStateVersion,
                currentTimeSeconds,
                inventory,
                burnup.Value,
                shift.Value,
                burnupInvariant.Value,
                shiftInvariant.Value);
        }

        return new HistoryTrace(
            trace.ToString(),
            burnupIntervalCount,
            refuelShiftCount,
            coreStateVersion,
            currentTimeSeconds,
            introducedIds.Count,
            inventory);
    }

    private static void AppendTrace(
        StringBuilder trace,
        int cycle,
        ulong coreStateVersion,
        double currentTimeSeconds,
        BundleInventory inventory,
        BurnupIntervalResultV1 burnup,
        RefuelShiftResult shift,
        Phase5InvariantReportV1 burnupInvariant,
        Phase5InvariantReportV1 shiftInvariant)
    {
        trace.Append(cycle.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(coreStateVersion.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(currentTimeSeconds.ToString("R", CultureInfo.InvariantCulture))
            .Append('|')
            .Append(burnupInvariant.TotalIntervalEnergyJ.ToString("R", CultureInfo.InvariantCulture))
            .Append('|')
            .Append(shiftInvariant.PreservedBundleCount.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(shiftInvariant.InsertedBundleCount.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append(shiftInvariant.DischargedBundleCount.ToString(CultureInfo.InvariantCulture))
            .Append('|')
            .Append("records=");

        foreach (BurnupIntervalRecordV1 record in burnup.Records)
        {
            trace.Append(record.BundleId)
                .Append(',')
                .Append(record.ChannelId.Value.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(record.Position.Value.ToString(CultureInfo.InvariantCulture))
                .Append(',')
                .Append(record.DeltaTimeSeconds.ToString("R", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(record.PowerWatts.ToString("R", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(record.DeltaFissionEnergyJ.ToString("R", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(record.OldCumulativeFissionEnergyJ.ToString("R", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(record.NewCumulativeFissionEnergyJ.ToString("R", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(record.OldBurnupJPerKgHm.ToString("R", CultureInfo.InvariantCulture))
                .Append(',')
                .Append(record.NewBurnupJPerKgHm.ToString("R", CultureInfo.InvariantCulture))
                .Append(';');
        }

        trace.Append("discharged=");
        foreach (BundleState discharged in shift.DischargedBundles)
        {
            AppendBundleState(trace, discharged);
        }

        trace.Append('|')
            .Append(Convert.ToHexString(ComputeInventoryDigest(inventory).ToArray()))
            .Append(';');
    }

    private static void AppendBundleState(StringBuilder trace, BundleState bundle)
    {
        trace.Append(bundle.BundleId)
            .Append(',')
            .Append(bundle.ChannelId.Value.ToString(CultureInfo.InvariantCulture))
            .Append(',')
            .Append(bundle.Position.Value.ToString(CultureInfo.InvariantCulture))
            .Append(',')
            .Append(bundle.MaterialVariantId.Value)
            .Append(',')
            .Append(bundle.InitialBurnupJPerKgHm.ToString("R", CultureInfo.InvariantCulture))
            .Append(',')
            .Append(bundle.CumulativeFissionEnergyJ.ToString("R", CultureInfo.InvariantCulture))
            .Append(',')
            .Append(bundle.HeavyMetalMassKg.ToString("R", CultureInfo.InvariantCulture))
            .Append(',')
            .Append(bundle.InsertedAtSeconds.ToString("R", CultureInfo.InvariantCulture))
            .Append(';');
    }

    private static IEnumerable<BundleState> CanonicalBundles(BundleInventory inventory)
    {
        return inventory.EnumerateOccupied()
            .OrderBy(bundle => bundle.ChannelId.Value)
            .ThenBy(bundle => bundle.Position.Value)
            .ThenBy(bundle => bundle.BundleId);
    }

    private static Digest32 ComputeInventoryDigest(BundleInventory inventory)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            foreach (BundleState bundle in CanonicalBundles(inventory))
            {
                writer.Write(bundle.BundleId.ToCanonicalBytes());
                writer.Write(bundle.ChannelId.Value);
                writer.Write(bundle.Position.Value);
                writer.Write(bundle.MaterialVariantId.Value);
                writer.Write(bundle.InitialBurnupJPerKgHm);
                writer.Write(bundle.CumulativeFissionEnergyJ);
                writer.Write(bundle.HeavyMetalMassKg);
                writer.Write(bundle.InsertedAtSeconds);
            }
        }

        return new Digest32(SHA256.HashData(stream.ToArray()));
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

    private static CoreTopology CreateSingleChannelTopology(FlowDirection flowDirection)
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
            BundlePosition bundlePosition = new BundlePosition((uint)position);
            foreach (TopologyFace face in new[]
            {
                TopologyFace.North,
                TopologyFace.South,
                TopologyFace.East,
                TopologyFace.West
            })
            {
                boundaries.Add(new BoundaryFaceRecord(
                    channelId,
                    bundlePosition,
                    face,
                    BoundaryClassification.Reflective));
            }
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

        ContractValidationResult<CoreTopology> result = CoreTopology.TryCreate(
            1,
            positionCount,
            new[]
            {
                new ChannelTopology(
                    channelId,
                    0,
                    0,
                    flowDirection,
                    flowDirection == FlowDirection.EndAtoEndB
                        ? new BundlePosition(0)
                        : new BundlePosition(positionCount - 1),
                    flowDirection == FlowDirection.EndAtoEndB
                        ? new BundlePosition(positionCount - 1)
                        : new BundlePosition(0),
                    neighbors,
                    boundaries)
            });
        AssertValid(result);
        return result.Value;
    }

    private static BundleInventory CreateInventory(CoreTopology topology)
    {
        BundleState[] bundles = Enumerable.Range(0, topology.SlotCount)
            .Select(position => new BundleState(
                Id(position + 1),
                new ChannelId(0),
                new BundlePosition((uint)position),
                new MaterialVariantId("MAT-OLD"),
                1000.0 + position,
                100.0 + position,
                1000.0,
                0.0))
            .ToArray();
        ContractValidationResult<BundleInventory> result = BundleInventory.TryCreate(topology, bundles);
        AssertValid(result);
        return result.Value;
    }

    private static RefuelSchemePositionPlan CreatePlan(ushort shiftCount, FlowDirection flowDirection)
    {
        ContractValidationResult<RefuelSchemeDefinition> scheme = RefuelSchemeDefinition.TryCreate(
            "S" + shiftCount.ToString(CultureInfo.InvariantCulture),
            shiftCount,
            "MAT-FRESH",
            RefuelSchemeDefinition.CurrentSchemaVersion,
            Enumerable.Range(12 - shiftCount, shiftCount)
                .Select(position => new BundlePosition((uint)position)),
            Enumerable.Range(0, shiftCount)
                .Select(position => new BundlePosition((uint)position)));
        AssertValid(scheme);

        ContractValidationResult<RefuelSchemePositionPlan> plan =
            scheme.Value.TryCreatePositionPlan(12, flowDirection);
        AssertValid(plan);
        return plan.Value;
    }

    private static StableId Id(int number)
    {
        return StableId.Parse(
            "00000000-0000-0000-0000-" +
            number.ToString("D12", CultureInfo.InvariantCulture));
    }

    private static int NumericId(StableId id)
    {
        return int.Parse(id.ToString().AsSpan(24), CultureInfo.InvariantCulture);
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

    private sealed class HistoryTrace
    {
        public HistoryTrace(
            string fingerprint,
            int burnupIntervalCount,
            int refuelShiftCount,
            ulong finalCoreStateVersion,
            double finalTimeSeconds,
            int introducedIdentityCount,
            BundleInventory finalInventory)
        {
            Fingerprint = fingerprint;
            BurnupIntervalCount = burnupIntervalCount;
            RefuelShiftCount = refuelShiftCount;
            FinalCoreStateVersion = finalCoreStateVersion;
            FinalTimeSeconds = finalTimeSeconds;
            IntroducedIdentityCount = introducedIdentityCount;
            FinalInventory = finalInventory;
        }

        public string Fingerprint { get; }

        public int BurnupIntervalCount { get; }

        public int RefuelShiftCount { get; }

        public ulong FinalCoreStateVersion { get; }

        public double FinalTimeSeconds { get; }

        public int IntroducedIdentityCount { get; }

        public BundleInventory FinalInventory { get; }
    }
}
