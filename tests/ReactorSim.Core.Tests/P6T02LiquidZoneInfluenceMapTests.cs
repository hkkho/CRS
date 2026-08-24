using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P6T02LiquidZoneInfluenceMapTests
{
    private readonly ITestOutputHelper _output;

    public P6T02LiquidZoneInfluenceMapTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ApprovedSyntheticMapIsVersionedAndCanonicalAcrossInputOrder()
    {
        LiquidZoneInfluenceMapV1 map = CreateMap();
        LiquidZoneInfluenceMapV1 reordered = Require(LiquidZoneInfluenceMapV1.TryCreate(
            1,
            LiquidZoneInfluenceMapV1.ApprovedMapId,
            map.Grouping,
            LiquidZoneInfluenceMapV1.ApprovedTopologyId,
            map.TargetNodes.Reverse(),
            map.TopologyDigest,
            LiquidZoneInfluenceMapV1.ApprovedDataVersion,
            LiquidZoneInfluenceMapV1.ApprovedOwnerId,
            LiquidZoneInfluenceMapV1.ApprovedSignCertificate,
            LiquidZoneInfluenceMapV1.SourceUnitDimensionless,
            LiquidZoneInfluenceMapV1.TargetUnitMInverse,
            map.ReferenceFillFractions.Reverse(),
            map.ReferenceStateDigest,
            map.Entries.Reverse(),
            map.MapDigest));

        Assert.Equal(14, map.Grouping.Mappings.Count);
        Assert.Equal(6, map.TargetNodes.Count);
        Assert.Equal(28, map.Entries.Count);
        Assert.Equal(map.MappingId, LiquidZoneInfluenceMapV1.ApprovedMappingId);
        Assert.Equal(LiquidZoneInfluenceMapV1.ApprovedMappingVersion, map.MappingVersion);
        Assert.Equal(map.MapDigest, reordered.MapDigest);
        Assert.Equal(map.ToCanonicalBytes(), reordered.ToCanonicalBytes());

        _output.WriteLine("P6T02_MAP_ID=" + map.MapId);
        _output.WriteLine("P6T02_MAPPING_DIGEST=" + Hex(map.MappingDigest));
        _output.WriteLine("P6T02_TOPOLOGY_DIGEST=" + Hex(map.TopologyDigest));
        _output.WriteLine("P6T02_REFERENCE_STATE_DIGEST=" + Hex(map.ReferenceStateDigest));
        _output.WriteLine("P6T02_MAP_DIGEST=" + Hex(map.MapDigest));
        _output.WriteLine("P6T02_MAP_CANONICAL_LENGTH=" + map.ToCanonicalBytes().Length.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void MapRejectsIdentityDigestTopologyReferenceAndEntryFailures()
    {
        LiquidZoneInfluenceMapV1 map = CreateMap();

        ContractValidationResult<LiquidZoneInfluenceMapV1> mapIdResult = LiquidZoneInfluenceMapV1.TryCreate(
            1,
            Id(9001),
            map.Grouping,
            LiquidZoneInfluenceMapV1.ApprovedTopologyId,
            map.TargetNodes,
            map.TopologyDigest,
            LiquidZoneInfluenceMapV1.ApprovedDataVersion,
            LiquidZoneInfluenceMapV1.ApprovedOwnerId,
            LiquidZoneInfluenceMapV1.ApprovedSignCertificate,
            LiquidZoneInfluenceMapV1.SourceUnitDimensionless,
            LiquidZoneInfluenceMapV1.TargetUnitMInverse,
            map.ReferenceFillFractions,
            map.ReferenceStateDigest,
            map.Entries,
            map.MapDigest);
        Assert.False(mapIdResult.IsValid);
        Assert.Equal("LiquidZoneInfluenceMap.MapId.Unapproved", mapIdResult.FirstDiagnostic.Code);

        ContractValidationResult<LiquidZoneInfluenceMapV1> topologyResult = LiquidZoneInfluenceMapV1.TryCreate(
            1,
            map.MapId,
            map.Grouping,
            map.TopologyId,
            map.TargetNodes,
            Digest(0xe1),
            map.DataVersion,
            map.OwnerId,
            map.SignCertificate,
            map.SourceUnit,
            map.TargetUnit,
            map.ReferenceFillFractions,
            map.ReferenceStateDigest,
            map.Entries,
            map.MapDigest);
        Assert.False(topologyResult.IsValid);
        Assert.Equal("LiquidZoneInfluenceMap.TopologyDigest.Mismatch", topologyResult.FirstDiagnostic.Code);

        double[] wrongReference = map.ReferenceFillFractions.ToArray();
        wrongReference[4] = 0.25;
        ContractValidationResult<LiquidZoneInfluenceMapV1> referenceResult = LiquidZoneInfluenceMapV1.TryCreate(
            1,
            map.MapId,
            map.Grouping,
            map.TopologyId,
            map.TargetNodes,
            map.TopologyDigest,
            map.DataVersion,
            map.OwnerId,
            map.SignCertificate,
            map.SourceUnit,
            map.TargetUnit,
            wrongReference,
            map.ReferenceStateDigest,
            map.Entries,
            map.MapDigest);
        Assert.False(referenceResult.IsValid);
        Assert.Equal("LiquidZoneInfluenceMap.ReferenceFill.Unapproved", referenceResult.FirstDiagnostic.Code);

        LiquidZoneInfluenceMapEntryV1 duplicate = map.Entries[0];
        LiquidZoneInfluenceMapEntryV1[] duplicateEntries = map.Entries.ToArray();
        duplicateEntries[1] = duplicate;
        ContractValidationResult<LiquidZoneInfluenceMapV1> duplicateResult = LiquidZoneInfluenceMapV1.TryCreate(
            1,
            map.MapId,
            map.Grouping,
            map.TopologyId,
            map.TargetNodes,
            map.TopologyDigest,
            map.DataVersion,
            map.OwnerId,
            map.SignCertificate,
            map.SourceUnit,
            map.TargetUnit,
            map.ReferenceFillFractions,
            map.ReferenceStateDigest,
            duplicateEntries,
            map.MapDigest);
        Assert.False(duplicateResult.IsValid);
        Assert.Equal("LiquidZoneInfluenceMap.Entry.DuplicateKey", duplicateResult.FirstDiagnostic.Code);

        ContractValidationResult<LiquidZoneInfluenceMapV1> digestResult = LiquidZoneInfluenceMapV1.TryCreate(
            1,
            map.MapId,
            map.Grouping,
            map.TopologyId,
            map.TargetNodes,
            map.TopologyDigest,
            map.DataVersion,
            map.OwnerId,
            map.SignCertificate,
            map.SourceUnit,
            map.TargetUnit,
            map.ReferenceFillFractions,
            map.ReferenceStateDigest,
            map.Entries,
            Digest(0xe2));
        Assert.False(digestResult.IsValid);
        Assert.Equal("LiquidZoneInfluenceMap.MapDigest.Unapproved", digestResult.FirstDiagnostic.Code);

        LiquidZoneAssemblyBindingV1[] alternateMappings = map.Grouping.Mappings.ToArray();
        alternateMappings[0] = Require(LiquidZoneAssemblyBindingV1.TryCreate(0, 1));
        Digest32 alternateMappingDigest = LiquidZoneGroupingV1.ComputeDigest(
            1,
            LiquidZoneInfluenceMapV1.ApprovedMappingId,
            LiquidZoneInfluenceMapV1.ApprovedMappingVersion,
            alternateMappings);
        LiquidZoneGroupingV1 alternateGrouping = Require(LiquidZoneGroupingV1.TryCreate(
            1,
            LiquidZoneInfluenceMapV1.ApprovedMappingId,
            LiquidZoneInfluenceMapV1.ApprovedMappingVersion,
            alternateMappings,
            alternateMappingDigest));
        ContractValidationResult<LiquidZoneInfluenceMapV1> alternateGroupingResult =
            LiquidZoneInfluenceMapV1.TryCreate(
                1,
                map.MapId,
                alternateGrouping,
                map.TopologyId,
                map.TargetNodes,
                map.TopologyDigest,
                map.DataVersion,
                map.OwnerId,
                map.SignCertificate,
                map.SourceUnit,
                map.TargetUnit,
                map.ReferenceFillFractions,
                map.ReferenceStateDigest,
                map.Entries,
                map.MapDigest);
        Assert.False(alternateGroupingResult.IsValid);
        Assert.Equal(
            "LiquidZoneInfluenceMap.MappingContent.Unapproved",
            alternateGroupingResult.FirstDiagnostic.Code);
    }

    [Fact]
    public void RecordedPackageAndManifestBindToTheTypedApprovedFixture()
    {
        LiquidZoneInfluenceMapV1 map = CreateMap();
        string packagePath = Path.Combine(
            RepositoryRoot(),
            "data",
            "packs",
            "p6-t02-synthetic-liquid-zone-map-v1.json");
        string manifestPath = Path.Combine(
            RepositoryRoot(),
            "data",
            "packs",
            "p6-t02-synthetic-liquid-zone-map-v1.manifest.json");

        using (JsonDocument packageDocument = JsonDocument.Parse(File.ReadAllText(packagePath)))
        using (JsonDocument manifestDocument = JsonDocument.Parse(File.ReadAllText(manifestPath)))
        {
            JsonElement package = packageDocument.RootElement;
            Assert.Equal(
                "P6-T02-SYNTHETIC-LIQUID-ZONE-MAP-V1",
                package.GetProperty("fixture_id").GetString());
            Assert.Equal(map.MapId.ToString(), package.GetProperty("map_id").GetString());
            Assert.Equal(
                Hex(map.MappingDigest),
                package.GetProperty("mapping").GetProperty("mapping_digest").GetString());
            Assert.Equal(
                Hex(map.TopologyDigest),
                package.GetProperty("topology").GetProperty("topology_digest").GetString());
            Assert.Equal(
                Hex(map.ReferenceStateDigest),
                package.GetProperty("reference_state").GetProperty("reference_state_digest").GetString());
            Assert.Equal(Hex(map.MapDigest), package.GetProperty("map_digest").GetString());
            Assert.Equal(map.OwnerId, package.GetProperty("owner_id").GetString());

            JsonElement mappings = package.GetProperty("mapping").GetProperty("logical_to_physical");
            Assert.Equal(map.Grouping.Mappings.Count, mappings.GetArrayLength());
            for (int index = 0; index < mappings.GetArrayLength(); index++)
            {
                Assert.Equal(
                    (int)map.Grouping.Mappings[index].LogicalZoneId,
                    mappings[index].GetProperty("logical_zone_id").GetInt32());
                Assert.Equal(
                    (int)map.Grouping.Mappings[index].PhysicalAssemblyId,
                    mappings[index].GetProperty("physical_assembly_id").GetInt32());
            }

            JsonElement targets = package.GetProperty("topology").GetProperty("target_nodes");
            Assert.Equal(map.TargetNodes.Count, targets.GetArrayLength());
            for (int index = 0; index < targets.GetArrayLength(); index++)
            {
                Assert.Equal(
                    (int)map.TargetNodes[index].ChannelId.Value,
                    targets[index].GetProperty("channel_id").GetInt32());
                Assert.Equal(
                    (int)map.TargetNodes[index].Position.Value,
                    targets[index].GetProperty("bundle_position").GetInt32());
            }

            JsonElement entries = package.GetProperty("entries");
            Assert.Equal(map.Entries.Count, entries.GetArrayLength());
            for (int index = 0; index < entries.GetArrayLength(); index++)
            {
                LiquidZoneInfluenceMapEntryV1 typedEntry = map.Entries[index];
                JsonElement packageEntry = entries[index];
                Assert.Equal(
                    (int)typedEntry.LogicalZoneId,
                    packageEntry.GetProperty("logical_zone_id").GetInt32());
                Assert.Equal(
                    (int)typedEntry.TargetNode.ChannelId.Value,
                    packageEntry.GetProperty("target_node").GetProperty("channel_id").GetInt32());
                Assert.Equal(
                    (int)typedEntry.TargetNode.Position.Value,
                    packageEntry.GetProperty("target_node").GetProperty("bundle_position").GetInt32());
                Assert.Equal(
                    (int)typedEntry.GroupIndex,
                    packageEntry.GetProperty("group_index").GetInt32());
                Assert.Equal(
                    typedEntry.WeightMInversePerFillFraction,
                    packageEntry.GetProperty("weight_m_inverse_per_fill_fraction").GetDouble());
                Assert.Equal(typedEntry.SourceUnit, packageEntry.GetProperty("source_unit").GetString());
                Assert.Equal(typedEntry.TargetUnit, packageEntry.GetProperty("target_unit").GetString());
            }

            JsonElement manifest = manifestDocument.RootElement;
            byte[] packageBytes = File.ReadAllBytes(packagePath);
            string packageHash = Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant();
            Assert.Equal(
                packageBytes.Length,
                manifest.GetProperty("artifact_byte_length").GetInt32());
            Assert.Equal(
                packageHash,
                manifest.GetProperty("artifact_sha256").GetString());
            Assert.Equal(Hex(map.MapDigest), manifest.GetProperty("map_digest").GetString());
            Assert.False(manifest.GetProperty("golden_data_claim").GetBoolean());
            Assert.False(manifest.GetProperty("production_candu_claim").GetBoolean());
        }
    }

    [Fact]
    public void OverlayAggregatesActiveZonesAndSkipsDisabledZoneExactly()
    {
        LiquidZoneInfluenceMapV1 map = CreateMap();
        StableId branchId = Id(9100);
        LiquidZoneSystemStateV1 systemState = CreateSystemState(map, branchId);
        byte[] before = systemState.ToCanonicalBytes();

        LiquidZoneOverlayEvaluationV1 overlay = Require(
            LiquidZoneOverlayV1.TryEvaluate(map, systemState));

        Assert.Equal(12, overlay.Values.Count);
        Assert.Equal(before, systemState.ToCanonicalBytes());
        Assert.Single(overlay.DisabledZeroAssertions);
        LiquidZoneDisabledZeroAssertionV1 zero = Require(
            overlay.TryGetDisabledZeroAssertion(0));
        Assert.False(zero.Enabled);
        Assert.Equal(0.0, zero.ExactZeroOverlayMInverse);
        Assert.Equal(map.MapDigest, zero.InfluenceMapDigest);

        uint[] activeCountByPhysicalAssembly = { 2, 3, 2, 2, 2, 2 };
        for (int physicalAssemblyId = 0; physicalAssemblyId < 6; physicalAssemblyId++)
        {
            NodeKey node = map.TargetNodes[physicalAssemblyId];
            LiquidZoneOverlayValueV1 group0 = overlay.Values.Single(value =>
                value.TargetNode == node && value.GroupIndex == 0);
            LiquidZoneOverlayValueV1 group1 = overlay.Values.Single(value =>
                value.TargetNode == node && value.GroupIndex == 1);
            double expectedGroup0 = 0.0;
            double expectedGroup1 = 0.0;
            double expectedDeltaFill = 0.60 - 0.50;
            for (int activeIndex = 0;
                 activeIndex < activeCountByPhysicalAssembly[physicalAssemblyId];
                 activeIndex++)
            {
                expectedGroup0 += LiquidZoneInfluenceMapV1.ApprovedGroup0WeightMInversePerFillFraction * expectedDeltaFill;
                expectedGroup1 += LiquidZoneInfluenceMapV1.ApprovedGroup1WeightMInversePerFillFraction * expectedDeltaFill;
            }

            Assert.Equal(expectedGroup0, group0.DeltaSigmaAMInverse);
            Assert.Equal(expectedGroup1, group1.DeltaSigmaAMInverse);
        }

        LiquidZoneOverlayEvaluationV1 repeated = Require(
            LiquidZoneOverlayV1.TryEvaluate(
                map,
                Require(LiquidZoneSystemStateV1.TryCreate(
                    1,
                    branchId,
                    systemState.CoreStateVersion,
                    systemState.TopologyVersion,
                    systemState.DataPackVersion,
                    systemState.Grouping,
                    systemState.Zones.Reverse(),
                    systemState.StateDigest))));
        Assert.Equal(overlay.OverlayDigest, repeated.OverlayDigest);
        Assert.Equal(overlay.ToCanonicalBytes(), repeated.ToCanonicalBytes());
    }

    [Fact]
    public void OverlayRejectsStaleStateBindingAndPreservesInputsOnFailure()
    {
        LiquidZoneInfluenceMapV1 map = CreateMap();
        StableId branchId = Id(9200);
        LiquidZoneSystemStateV1 staleState = CreateSystemState(
            map,
            branchId,
            zoneDataDigest: Digest(0xb4));
        byte[] before = staleState.ToCanonicalBytes();

        ContractValidationResult<LiquidZoneOverlayEvaluationV1> result =
            LiquidZoneOverlayV1.TryEvaluate(map, staleState);
        Assert.False(result.IsValid);
        Assert.Equal("LiquidZoneOverlay.ZoneDataBinding.Mismatch", result.FirstDiagnostic.Code);
        Assert.Equal(before, staleState.ToCanonicalBytes());
    }

    [Fact]
    public void MotionUsesPreEventAvailableCommandRateLimitAndExplicitDelayProjection()
    {
        LiquidZoneInfluenceMapV1 map = CreateMap();
        LiquidZoneSystemStateV1 systemState = CreateSystemState(map, Id(9300));
        LiquidZoneStateV1 rateLimited = systemState.Zones[2];

        LiquidZoneMotionResultV1 beforeDue = Require(
            LiquidZoneOverlayV1.TryAdvance(
                rateLimited,
                0.9,
                1.0,
                5.0,
                3.0));
        Assert.False(beforeDue.CommandDelaySatisfied);
        Assert.Equal(rateLimited.StateFillFraction, beforeDue.StateFillFractionAfter);
        Assert.Equal(0.0, beforeDue.AppliedDeltaFraction);

        LiquidZoneMotionResultV1 rateLimitedResult = Require(
            LiquidZoneOverlayV1.TryAdvance(
                rateLimited,
                0.9,
                2.0,
                1.0,
                2.5));
        Assert.True(rateLimitedResult.CommandDelaySatisfied);
        Assert.Equal(0.125, rateLimitedResult.AppliedDeltaFraction);
        Assert.Equal(0.725, rateLimitedResult.StateFillFractionAfter);
        Assert.Equal(rateLimited.StateDigest, rateLimitedResult.SourceStateDigest);

        LiquidZoneStateV1 prescribed = systemState.Zones[1];
        LiquidZoneMotionResultV1 prescribedResult = Require(
            LiquidZoneOverlayV1.TryAdvance(
                prescribed,
                0.9,
                1.0,
                1.0,
                10.0));
        Assert.Equal(prescribed.StateFillFraction, prescribedResult.StateFillFractionAfter);
        Assert.Equal(0.0, prescribedResult.AppliedDeltaFraction);

        LiquidZoneStateV1 disabled = systemState.Zones[0];
        LiquidZoneMotionResultV1 disabledResult = Require(
            LiquidZoneOverlayV1.TryAdvance(
                disabled,
                0.9,
                1.0,
                1.0,
                10.0));
        Assert.Equal(disabled.StateFillFraction, disabledResult.StateFillFractionAfter);
        Assert.Equal(0.0, disabledResult.AppliedDeltaFraction);

        LiquidZoneStateV1 zeroRate = Require(LiquidZoneStateV1.TryCreate(
            1,
            rateLimited.LogicalZoneId,
            rateLimited.PhysicalAssemblyId,
            rateLimited.StateFillFraction,
            rateLimited.ReferenceFillFraction,
            rateLimited.CommandFillFraction,
            true,
            LiquidZoneModeV1.RateLimited,
            0.0,
            rateLimited.DelaySeconds,
            rateLimited.InfluenceMapId,
            rateLimited.DataVersion,
            rateLimited.DataDigest,
            rateLimited.UpdateTimeSeconds,
            rateLimited.QueueBinding));
        LiquidZoneMotionResultV1 zeroRateResult = Require(
            LiquidZoneOverlayV1.TryAdvance(zeroRate, 0.4, 1.0, 1.0, 2.0));
        Assert.Equal(0.0, zeroRateResult.AppliedDeltaFraction);
        Assert.True(BitConverter.DoubleToInt64Bits(zeroRateResult.AppliedDeltaFraction) >= 0);

        ContractValidationResult<LiquidZoneMotionResultV1> timeResult =
            LiquidZoneOverlayV1.TryAdvance(rateLimited, 0.9, 4.0, 1.0, 3.0);
        Assert.False(timeResult.IsValid);
        Assert.Equal("LiquidZoneMotion.Time.Order", timeResult.FirstDiagnostic.Code);
    }

    private static LiquidZoneInfluenceMapV1 CreateMap()
    {
        LiquidZoneGroupingV1 grouping = CreateGrouping();
        NodeKey[] targetNodes = Enumerable.Range(0, 6)
            .Select(value => new NodeKey(new ChannelId((uint)value), new BundlePosition(0)))
            .ToArray();
        Digest32 topologyDigest = LiquidZoneInfluenceMapV1.ComputeTopologyDigest(
            LiquidZoneInfluenceMapV1.ApprovedTopologyId,
            targetNodes);
        double[] referenceFillFractions = Enumerable.Repeat(
            LiquidZoneInfluenceMapV1.ApprovedReferenceFillFraction,
            14).ToArray();
        Digest32 referenceStateDigest = LiquidZoneInfluenceMapV1.ComputeReferenceStateDigest(
            LiquidZoneInfluenceMapV1.ApprovedMapId,
            grouping,
            LiquidZoneInfluenceMapV1.ApprovedTopologyId,
            topologyDigest,
            LiquidZoneInfluenceMapV1.ApprovedDataVersion,
            LiquidZoneInfluenceMapV1.ApprovedOwnerId,
            LiquidZoneInfluenceMapV1.ApprovedSignCertificate,
            LiquidZoneInfluenceMapV1.SourceUnitDimensionless,
            LiquidZoneInfluenceMapV1.TargetUnitMInverse,
            referenceFillFractions);
        LiquidZoneInfluenceMapEntryV1[] entries = grouping.Mappings
            .SelectMany(mapping => new[]
            {
                Require(LiquidZoneInfluenceMapEntryV1.TryCreate(
                    mapping.LogicalZoneId,
                    targetNodes[(int)mapping.PhysicalAssemblyId],
                    0,
                    LiquidZoneInfluenceMapV1.ApprovedGroup0WeightMInversePerFillFraction,
                    LiquidZoneInfluenceMapV1.SourceUnitDimensionless,
                    LiquidZoneInfluenceMapV1.TargetUnitMInverse)),
                Require(LiquidZoneInfluenceMapEntryV1.TryCreate(
                    mapping.LogicalZoneId,
                    targetNodes[(int)mapping.PhysicalAssemblyId],
                    1,
                    LiquidZoneInfluenceMapV1.ApprovedGroup1WeightMInversePerFillFraction,
                    LiquidZoneInfluenceMapV1.SourceUnitDimensionless,
                    LiquidZoneInfluenceMapV1.TargetUnitMInverse))
            })
            .ToArray();
        Digest32 mapDigest = LiquidZoneInfluenceMapV1.ComputeDigest(
            1,
            LiquidZoneInfluenceMapV1.ApprovedMapId,
            grouping,
            LiquidZoneInfluenceMapV1.ApprovedTopologyId,
            targetNodes,
            topologyDigest,
            LiquidZoneInfluenceMapV1.ApprovedDataVersion,
            LiquidZoneInfluenceMapV1.ApprovedOwnerId,
            LiquidZoneInfluenceMapV1.ApprovedSignCertificate,
            LiquidZoneInfluenceMapV1.SourceUnitDimensionless,
            LiquidZoneInfluenceMapV1.TargetUnitMInverse,
            referenceFillFractions,
            referenceStateDigest,
            entries);
        return Require(LiquidZoneInfluenceMapV1.TryCreate(
            1,
            LiquidZoneInfluenceMapV1.ApprovedMapId,
            grouping,
            LiquidZoneInfluenceMapV1.ApprovedTopologyId,
            targetNodes,
            topologyDigest,
            LiquidZoneInfluenceMapV1.ApprovedDataVersion,
            LiquidZoneInfluenceMapV1.ApprovedOwnerId,
            LiquidZoneInfluenceMapV1.ApprovedSignCertificate,
            LiquidZoneInfluenceMapV1.SourceUnitDimensionless,
            LiquidZoneInfluenceMapV1.TargetUnitMInverse,
            referenceFillFractions,
            referenceStateDigest,
            entries,
            mapDigest));
    }

    private static LiquidZoneGroupingV1 CreateGrouping()
    {
        uint[] physicalAssemblyIds = { 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 0, 1 };
        LiquidZoneAssemblyBindingV1[] mappings = physicalAssemblyIds
            .Select((physicalAssemblyId, logicalZoneId) =>
                Require(LiquidZoneAssemblyBindingV1.TryCreate((uint)logicalZoneId, physicalAssemblyId)))
            .ToArray();
        Digest32 mappingDigest = LiquidZoneGroupingV1.ComputeDigest(
            1,
            LiquidZoneInfluenceMapV1.ApprovedMappingId,
            LiquidZoneInfluenceMapV1.ApprovedMappingVersion,
            mappings);
        return Require(LiquidZoneGroupingV1.TryCreate(
            1,
            LiquidZoneInfluenceMapV1.ApprovedMappingId,
            LiquidZoneInfluenceMapV1.ApprovedMappingVersion,
            mappings,
            mappingDigest));
    }

    private static LiquidZoneSystemStateV1 CreateSystemState(
        LiquidZoneInfluenceMapV1 map,
        StableId branchId,
        Digest32? zoneDataDigest = null)
    {
        LiquidZoneStateV1[] zones = map.Grouping.Mappings
            .Select(mapping =>
            {
                LiquidZoneModeV1 mode = mapping.LogicalZoneId == 0
                    ? LiquidZoneModeV1.Disabled
                    : mapping.LogicalZoneId == 1
                        ? LiquidZoneModeV1.Prescribed
                        : LiquidZoneModeV1.RateLimited;
                bool enabled = mode != LiquidZoneModeV1.Disabled;
                LiquidZoneQueueBindingV1 queue = Require(LiquidZoneQueueBindingV1.TryCreate(
                    1,
                    Id(9400 + mapping.LogicalZoneId),
                    branchId,
                    Digest((byte)(mapping.LogicalZoneId + 1))));
                return Require(LiquidZoneStateV1.TryCreate(
                    1,
                    mapping.LogicalZoneId,
                    mapping.PhysicalAssemblyId,
                    mapping.LogicalZoneId == 0 ? 0.25 : 0.60,
                    0.50,
                    0.75,
                    enabled,
                    mode,
                    0.25,
                    1.0,
                    map.MapId,
                    map.DataVersion,
                    zoneDataDigest ?? map.MapDigest,
                    mapping.LogicalZoneId,
                    queue));
            })
            .ToArray();
        return Require(LiquidZoneSystemStateV1.TryCreate(
            1,
            branchId,
            42,
            "p6-t02-synthetic-topology-v1",
            map.DataVersion,
            map.Grouping,
            zones));
    }

    private static string Hex(Digest32 digest)
    {
        return Convert.ToHexString(digest.ToArray()).ToLowerInvariant();
    }

    private static string RepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            ".."));
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
