using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P6T01LiquidZoneContractTests
{
    [Fact]
    public void GroupingRequiresFourteenLogicalZonesAndCanonicalizesInputOrder()
    {
        LiquidZoneAssemblyBindingV1[] mappings = CreateMappings();
        StableId mappingId = Id(100);
        Digest32 mappingDigest = LiquidZoneGroupingV1.ComputeDigest(
            LiquidZoneGroupingV1.CurrentSchemaVersion,
            mappingId,
            "zone-grouping-v1",
            mappings);

        LiquidZoneGroupingV1 ordered = Require(LiquidZoneGroupingV1.TryCreate(
            LiquidZoneGroupingV1.CurrentSchemaVersion,
            mappingId,
            "zone-grouping-v1",
            mappings,
            mappingDigest));
        LiquidZoneGroupingV1 shuffled = Require(LiquidZoneGroupingV1.TryCreate(
            LiquidZoneGroupingV1.CurrentSchemaVersion,
            mappingId,
            "zone-grouping-v1",
            mappings.Reverse(),
            mappingDigest));

        Assert.Equal(14, ordered.Mappings.Count);
        Assert.Equal(6, ordered.Mappings.Select(mapping => mapping.PhysicalAssemblyId).Distinct().Count());
        Assert.Equal(Enumerable.Range(0, 14).Select(value => (uint)value),
            ordered.Mappings.Select(mapping => mapping.LogicalZoneId));
        Assert.Equal(ordered.MappingDigest, shuffled.MappingDigest);
        Assert.Equal(ordered.ToCanonicalBytes(), shuffled.ToCanonicalBytes());
    }

    [Fact]
    public void GroupingRejectsDuplicateLogicalIdentityAndMissingPhysicalAssembly()
    {
        LiquidZoneAssemblyBindingV1[] duplicateLogical = CreateMappings();
        duplicateLogical[13] = Require(LiquidZoneAssemblyBindingV1.TryCreate(0, 1));
        Digest32 duplicateDigest = LiquidZoneGroupingV1.ComputeDigest(
            1,
            Id(101),
            "duplicate-fixture",
            duplicateLogical);
        ContractValidationResult<LiquidZoneGroupingV1> duplicateResult = LiquidZoneGroupingV1.TryCreate(
            1,
            Id(101),
            "duplicate-fixture",
            duplicateLogical,
            duplicateDigest);

        Assert.False(duplicateResult.IsValid);
        Assert.Equal("LiquidZoneGrouping.LogicalZoneId.Duplicate", duplicateResult.FirstDiagnostic.Code);

        LiquidZoneAssemblyBindingV1[] missingPhysical = CreateMappings();
        missingPhysical[10] = Require(LiquidZoneAssemblyBindingV1.TryCreate(10, 4));
        missingPhysical[11] = Require(LiquidZoneAssemblyBindingV1.TryCreate(11, 4));
        Digest32 missingDigest = LiquidZoneGroupingV1.ComputeDigest(
            1,
            Id(102),
            "missing-physical-fixture",
            missingPhysical);
        ContractValidationResult<LiquidZoneGroupingV1> missingResult = LiquidZoneGroupingV1.TryCreate(
            1,
            Id(102),
            "missing-physical-fixture",
            missingPhysical,
            missingDigest);

        Assert.False(missingResult.IsValid);
        Assert.Equal("LiquidZoneGrouping.PhysicalAssemblyId.Missing", missingResult.FirstDiagnostic.Code);
    }

    [Fact]
    public void MappingAndStateRejectInvalidDigestCountNullNumericAndModeInputs()
    {
        LiquidZoneAssemblyBindingV1[] mappings = CreateMappings();
        ContractValidationResult<LiquidZoneGroupingV1> digestMismatch = LiquidZoneGroupingV1.TryCreate(
            1,
            Id(103),
            "digest-mismatch-fixture",
            mappings,
            Digest(0xff));
        Assert.False(digestMismatch.IsValid);
        Assert.Equal("LiquidZoneGrouping.MappingDigest.Mismatch", digestMismatch.FirstDiagnostic.Code);

        ContractValidationResult<LiquidZoneGroupingV1> countMismatch = LiquidZoneGroupingV1.TryCreate(
            1,
            Id(104),
            "count-mismatch-fixture",
            mappings.Take(13),
            Digest(0xfe));
        Assert.False(countMismatch.IsValid);
        Assert.Equal("LiquidZoneGrouping.Mappings.CountMismatch", countMismatch.FirstDiagnostic.Code);

        LiquidZoneAssemblyBindingV1[] nullEntry = mappings.ToArray();
        nullEntry[4] = null!;
        ContractValidationResult<LiquidZoneGroupingV1> nullMapping = LiquidZoneGroupingV1.TryCreate(
            1,
            Id(105),
            "null-fixture",
            nullEntry,
            Digest(0xfd));
        Assert.False(nullMapping.IsValid);
        Assert.Equal("LiquidZoneGrouping.Mapping.Null", nullMapping.FirstDiagnostic.Code);

        ContractValidationResult<LiquidZoneAssemblyBindingV1> outOfRange =
            LiquidZoneAssemblyBindingV1.TryCreate(14, 0);
        Assert.False(outOfRange.IsValid);
        Assert.Equal(
            "LiquidZoneAssemblyBinding.LogicalZoneId.OutOfRange",
            outOfRange.FirstDiagnostic.Code);

        LiquidZoneStateV1 disabled = CreateZone(0, Id(210), CreateGrouping());
        ContractValidationResult<LiquidZoneStateV1> nonFinite = LiquidZoneStateV1.TryCreate(
            1,
            0,
            0,
            double.NaN,
            0.0,
            0.0,
            false,
            LiquidZoneModeV1.Disabled,
            0.0,
            0.0,
            Id(301),
            "zone-data-v1",
            Digest(0x31),
            0.0,
            disabled.QueueBinding);
        Assert.False(nonFinite.IsValid);
        Assert.Equal("LiquidZoneState.FillFraction.Invalid", nonFinite.FirstDiagnostic.Code);

        ContractValidationResult<LiquidZoneStateV1> enabledMismatch = LiquidZoneStateV1.TryCreate(
            1,
            0,
            0,
            0.0,
            0.0,
            0.0,
            true,
            LiquidZoneModeV1.Disabled,
            0.0,
            0.0,
            Id(301),
            "zone-data-v1",
            Digest(0x31),
            0.0,
            disabled.QueueBinding);
        Assert.False(enabledMismatch.IsValid);
        Assert.Equal("LiquidZoneState.Mode.EnabledMismatch", enabledMismatch.FirstDiagnostic.Code);

        ContractValidationResult<LiquidZoneDisabledZeroAssertionV1> enabledZero =
            CreateZone(1, Id(210), CreateGrouping()).TryGetDisabledZeroAssertion();
        Assert.False(enabledZero.IsValid);
        Assert.Equal("LiquidZoneState.DisabledZero.NotApplicable", enabledZero.FirstDiagnostic.Code);
    }

    [Fact]
    public void ZoneStateBindsQueueAndExposesExactDisabledZeroWithoutChangingFillState()
    {
        StableId branchId = Id(200);
        LiquidZoneStateV1 disabled = CreateZone(0, branchId, CreateGrouping());

        ContractValidationResult<LiquidZoneDisabledZeroAssertionV1> zeroResult = disabled.TryGetDisabledZeroAssertion();
        LiquidZoneDisabledZeroAssertionV1 zero = Require(zeroResult);

        Assert.False(disabled.Enabled);
        Assert.Equal(LiquidZoneModeV1.Disabled, disabled.Mode);
        Assert.Equal(0.25, disabled.StateFillFraction);
        Assert.Equal(0.0, zero.ExactZeroOverlayMInverse);
        Assert.False(zero.Enabled);
        Assert.Equal(disabled.DataDigest, zero.InfluenceMapDigest);

        double negativeZero = BitConverter.Int64BitsToDouble(long.MinValue);
        ContractValidationResult<LiquidZoneStateV1> negativeZeroResult = LiquidZoneStateV1.TryCreate(
            1,
            0,
            0,
            negativeZero,
            0.0,
            0.0,
            false,
            LiquidZoneModeV1.Disabled,
            0.0,
            0.0,
            Id(300),
            "zone-data-v1",
            Digest(0x30),
            0.0,
            disabled.QueueBinding);
        Assert.False(negativeZeroResult.IsValid);
        Assert.Equal("LiquidZoneState.FillFraction.Invalid", negativeZeroResult.FirstDiagnostic.Code);

        ContractValidationResult<LiquidZoneStateV1> missingQueueResult = LiquidZoneStateV1.TryCreate(
            1,
            0,
            0,
            0.0,
            0.0,
            0.0,
            false,
            LiquidZoneModeV1.Disabled,
            0.0,
            0.0,
            Id(300),
            "zone-data-v1",
            Digest(0x30),
            0.0,
            null);
        Assert.False(missingQueueResult.IsValid);
        Assert.Equal("LiquidZoneState.QueueBinding.Missing", missingQueueResult.FirstDiagnostic.Code);
    }

    [Fact]
    public void SystemStateBindsGroupingOwnersAndProducesRepeatableCanonicalDigest()
    {
        StableId branchId = Id(201);
        LiquidZoneGroupingV1 grouping = CreateGrouping();
        LiquidZoneStateV1[] zones = CreateZones(branchId, grouping);
        LiquidZoneSystemStateV1 first = Require(LiquidZoneSystemStateV1.TryCreate(
            1,
            branchId,
            42,
            "topology-v1",
            "data-pack-v1",
            grouping,
            zones));
        LiquidZoneSystemStateV1 repeated = Require(LiquidZoneSystemStateV1.TryCreate(
            1,
            branchId,
            42,
            "topology-v1",
            "data-pack-v1",
            grouping,
            zones.Reverse(),
            first.StateDigest));

        Assert.Equal(first.StateDigest, repeated.StateDigest);
        Assert.Equal(first.ToCanonicalBytes(), repeated.ToCanonicalBytes());
        LiquidZoneDisabledZeroAssertionV1 zero = Require(first.TryGetDisabledZeroAssertion(0));
        Assert.Equal(0.0, zero.ExactZeroOverlayMInverse);

        LiquidZoneQueueBindingV1 changedQueue = Require(LiquidZoneQueueBindingV1.TryCreate(
            1,
            zones[2].QueueBinding.QueueId,
            branchId,
            Digest(0x55)));
        LiquidZoneStateV1 changedState = CreateZone(2, branchId, grouping, changedQueue);
        Assert.NotEqual(zones[2].StateDigest, changedState.StateDigest);
        Assert.NotEqual(zones[2].ToCanonicalBytes(), changedState.ToCanonicalBytes());

        ContractValidationResult<LiquidZoneSystemStateV1> wrongDigest = LiquidZoneSystemStateV1.TryCreate(
            1,
            branchId,
            42,
            "topology-v1",
            "data-pack-v1",
            grouping,
            zones,
            Digest(0xee));
        Assert.False(wrongDigest.IsValid);
        Assert.Equal("LiquidZoneSystemState.StateDigest.Mismatch", wrongDigest.FirstDiagnostic.Code);

        LiquidZoneQueueBindingV1 wrongOwnerQueue = Require(LiquidZoneQueueBindingV1.TryCreate(
            1,
            Id(999),
            Id(998),
            Digest(0x99)));
        LiquidZoneStateV1 wrongOwnerZone = CreateZone(3, branchId, grouping, wrongOwnerQueue);
        LiquidZoneStateV1[] wrongOwnerZones = zones.ToArray();
        wrongOwnerZones[3] = wrongOwnerZone;
        ContractValidationResult<LiquidZoneSystemStateV1> wrongOwner = LiquidZoneSystemStateV1.TryCreate(
            1,
            branchId,
            42,
            "topology-v1",
            "data-pack-v1",
            grouping,
            wrongOwnerZones);
        Assert.False(wrongOwner.IsValid);
        Assert.Equal("LiquidZoneSystemState.QueueOwner.Mismatch", wrongOwner.FirstDiagnostic.Code);
    }

    private static LiquidZoneAssemblyBindingV1[] CreateMappings()
    {
        uint[] physicalAssemblyIds = { 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 0, 1 };
        return physicalAssemblyIds
            .Select((physicalAssemblyId, logicalZoneId) =>
                Require(LiquidZoneAssemblyBindingV1.TryCreate((uint)logicalZoneId, physicalAssemblyId)))
            .ToArray();
    }

    private static LiquidZoneGroupingV1 CreateGrouping()
    {
        LiquidZoneAssemblyBindingV1[] mappings = CreateMappings();
        StableId mappingId = Id(1000);
        Digest32 mappingDigest = LiquidZoneGroupingV1.ComputeDigest(
            1,
            mappingId,
            "zone-grouping-v1",
            mappings);
        return Require(LiquidZoneGroupingV1.TryCreate(
            1,
            mappingId,
            "zone-grouping-v1",
            mappings,
            mappingDigest));
    }

    private static LiquidZoneStateV1[] CreateZones(
        StableId branchId,
        LiquidZoneGroupingV1 grouping)
    {
        return Enumerable.Range(0, 14)
            .Select(logicalZoneId => CreateZone((uint)logicalZoneId, branchId, grouping))
            .ToArray();
    }

    private static LiquidZoneStateV1 CreateZone(
        uint logicalZoneId,
        StableId branchId,
        LiquidZoneGroupingV1 grouping,
        LiquidZoneQueueBindingV1? queueBinding = null)
    {
        Assert.True(grouping.TryGetPhysicalAssemblyId(logicalZoneId, out uint physicalAssemblyId));
        LiquidZoneModeV1 mode = logicalZoneId == 0
            ? LiquidZoneModeV1.Disabled
            : logicalZoneId == 1
                ? LiquidZoneModeV1.Prescribed
                : LiquidZoneModeV1.RateLimited;
        bool enabled = mode != LiquidZoneModeV1.Disabled;
        LiquidZoneQueueBindingV1 queue = queueBinding ?? Require(LiquidZoneQueueBindingV1.TryCreate(
            1,
            Id(2000 + logicalZoneId),
            branchId,
            Digest((byte)(logicalZoneId + 1))));

        return Require(LiquidZoneStateV1.TryCreate(
            1,
            logicalZoneId,
            physicalAssemblyId,
            logicalZoneId == 0 ? 0.25 : 0.50,
            0.50,
            logicalZoneId == 0 ? 0.75 : 0.60,
            enabled,
            mode,
            0.25,
            1.0,
            Id(3000),
            "zone-data-v1",
            Digest(0x40),
            logicalZoneId,
            queue));
    }

    private static StableId Id(uint value)
    {
        return StableId.Parse(
            "00000000-0000-0000-0000-" + value.ToString("x12", CultureInfo.InvariantCulture));
    }

    private static Digest32 Digest(byte value)
    {
        return new Digest32(Enumerable.Repeat(value, 32).ToArray());
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }
}
