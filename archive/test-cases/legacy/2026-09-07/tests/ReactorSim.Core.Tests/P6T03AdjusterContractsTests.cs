using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using ReactorSim.Core;
using ReactorSim.TestInfrastructure;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P6T03AdjusterContractsTests
{
    private readonly ITestOutputHelper _output;

    public P6T03AdjusterContractsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ApprovedGroupingAndMapAreCanonicalAcrossInputOrder()
    {
        AdjusterInfluenceMapV1 map = CreateMap();
        KeyValuePair<StableId, double>[] references = map.ReferenceFractionsByBank
            .Reverse()
            .ToArray();
        AdjusterInfluenceMapV1 reordered = Require(AdjusterInfluenceMapV1.TryCreate(
            1,
            map.AdjusterSetId,
            map.MapId,
            map.Grouping,
            map.TopologyId,
            map.TargetNodes.Reverse(),
            map.TopologyDigest,
            map.DataVersion,
            map.OwnerId,
            map.SignCertificate,
            map.Normalization,
            map.SourceUnit,
            map.TargetUnit,
            references,
            map.ReferenceStateDigest,
            map.Entries.Reverse(),
            map.MapDigest));

        Assert.Equal(2, map.Grouping.Mappings.Select(entry => entry.BankId).Distinct().Count());
        Assert.Equal(6, map.Grouping.Mappings.Count);
        Assert.Equal(6, map.TargetNodes.Count);
        Assert.Equal(12, map.Entries.Count);
        Assert.Equal(AdjusterBankGroupingV1.ApprovedMappingVersion, map.MappingVersion);
        Assert.Equal(AdjusterInfluenceMapV1.ApprovedDataVersion, map.DataVersion);
        Assert.Equal(AdjusterInfluenceMapV1.ApprovedSignCertificate, map.SignCertificate);
        Assert.Equal("None", map.Normalization);
        Assert.Equal(
            "10548c8327f72dc9fa94f61b7698d3fdd5357ea040bae4ff32a422cbbe0d6060",
            Hex(map.GroupingDigest));
        Assert.Equal(map.MapDigest, reordered.MapDigest);
        Assert.Equal(map.ToCanonicalBytes(), reordered.ToCanonicalBytes());

        _output.WriteLine("P6T03_GROUPING_DIGEST=" + Hex(map.GroupingDigest));
        _output.WriteLine("P6T03_TOPOLOGY_DIGEST=" + Hex(map.TopologyDigest));
        _output.WriteLine("P6T03_REFERENCE_STATE_DIGEST=" + Hex(map.ReferenceStateDigest));
        _output.WriteLine("P6T03_MAP_DIGEST=" + Hex(map.MapDigest));
        _output.WriteLine("P6T03_MAP_CANONICAL_LENGTH=" + map.ToCanonicalBytes().Length.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void BankStateBindsModesBoundsRateDelayOwnerAndCompleteQueue()
    {
        AdjusterInfluenceMapV1 map = CreateMap();
        StableId branchId = ApprovedBranchId;
        AdjusterSetStateV1 state = CreateSetState(map, branchId);
        AdjusterBankStateV1 manual = state.Banks.Single(bank =>
            bank.BankId == AdjusterBankGroupingV1.ApprovedBankAId);
        AdjusterBankStateV1 rateLimited = state.Banks.Single(bank =>
            bank.BankId == AdjusterBankGroupingV1.ApprovedBankBId);

        Assert.Equal(AdjusterMotionModeV1.Manual, manual.Mode);
        Assert.Null(manual.QueueState);
        Assert.Equal(AdjusterMotionModeV1.RateLimited, rateLimited.Mode);
        Assert.NotNull(rateLimited.QueueState);
        Assert.Equal(0UL, rateLimited.QueueState!.InitialNextSequence);
        Assert.Equal(0UL, rateLimited.QueueState.NextSequence);
        Assert.Single(rateLimited.QueueState.AvailableCommands);
        Assert.Empty(rateLimited.QueueState.PendingCommands);
        Assert.Empty(rateLimited.QueueState.AppliedSourceEventIds);
        Assert.Empty(rateLimited.QueueState.AllocatedCommandIds);
        Assert.Equal(0.1, rateLimited.QueueState.AvailableCommands[0].RateLimitPerSecond);
        Assert.Equal(2.0, rateLimited.DelaySeconds);
        Assert.Equal(branchId, rateLimited.QueueState.OwnerBranchId);
        Assert.Equal(rateLimited.StateDigest, rateLimited.StateDigest);

        _output.WriteLine("P6T03_QUEUE_DIGEST=" + Hex(rateLimited.QueueState.QueueDigest));
        _output.WriteLine("P6T03_BANK_A_STATE_DIGEST=" + Hex(manual.StateDigest));
        _output.WriteLine("P6T03_BANK_B_STATE_DIGEST=" + Hex(rateLimited.StateDigest));
        _output.WriteLine("P6T03_SET_STATE_DIGEST=" + Hex(state.StateDigest));

        ContractValidationResult<AdjusterBankStateV1> signedZero = AdjusterBankStateV1.TryCreate(
            1,
            manual.BankId,
            true,
            AdjusterMotionModeV1.Manual,
            -0.0,
            0.5,
            0.5,
            0.1,
            2.0,
            map.MapId,
            map.DataVersion,
            map.MapDigest,
            0.0,
            null);
        Assert.False(signedZero.IsValid);
        Assert.Equal("AdjusterBankState.Fraction.Invalid", signedZero.FirstDiagnostic.Code);

        ContractValidationResult<AdjusterBankStateV1> manualQueue = AdjusterBankStateV1.TryCreate(
            1,
            manual.BankId,
            true,
            AdjusterMotionModeV1.Manual,
            0.5,
            0.5,
            0.5,
            0.1,
            2.0,
            map.MapId,
            map.DataVersion,
            map.MapDigest,
            0.0,
            rateLimited.QueueState);
        Assert.False(manualQueue.IsValid);
        Assert.Equal("AdjusterBankState.QueueState.NotApplicableMismatch", manualQueue.FirstDiagnostic.Code);

        AdjusterQueueStateV1 wrongOwnerQueue = CreateQueue(
            map,
            Id(0xa70a),
            0.5,
            0.1,
            0.0);
        AdjusterBankStateV1 wrongOwnerBank = Require(AdjusterBankStateV1.TryCreate(
            1,
            rateLimited.BankId,
            true,
            AdjusterMotionModeV1.RateLimited,
            0.5,
            0.5,
            0.5,
            0.1,
            2.0,
            map.MapId,
            map.DataVersion,
            map.MapDigest,
            0.0,
            wrongOwnerQueue));
        ContractValidationResult<AdjusterSetStateV1> ownerResult = AdjusterSetStateV1.TryCreate(
            1,
            map.AdjusterSetId,
            branchId,
            42,
            "p6-t03-synthetic-topology-v1",
            map.DataVersion,
            map,
            map.Grouping,
            new[] { manual, wrongOwnerBank });
        Assert.False(ownerResult.IsValid);
        Assert.Equal("AdjusterSetState.QueueOwner.Mismatch", ownerResult.FirstDiagnostic.Code);
    }

    [Fact]
    public void OverlayUsesStateOnlyIsDeterministicAndSupportsLocalRollbackEvidence()
    {
        AdjusterInfluenceMapV1 map = CreateMap();
        AdjusterSetStateV1 state = CreateSetState(
            map,
            ApprovedBranchId,
            bankAStateFraction: 0.75,
            bankBStateFraction: 0.25,
            bankBAvailableFraction: 0.5);
        byte[] stateBefore = state.ToCanonicalBytes();

        AdjusterOverlayEvaluationV1 overlay = Require(AdjusterOverlayV1.TryEvaluate(map, state));
        Assert.Equal(12, overlay.Values.Count);
        Assert.Equal(stateBefore, state.ToCanonicalBytes());

        AdjusterOverlayValueV1 aGroup0 = Require(overlay.TryGetValue(
            new NodeKey(new ChannelId(0), new BundlePosition(0)),
            0));
        AdjusterOverlayValueV1 aGroup1 = Require(overlay.TryGetValue(
            new NodeKey(new ChannelId(0), new BundlePosition(0)),
            1));
        AdjusterOverlayValueV1 bGroup0 = Require(overlay.TryGetValue(
            new NodeKey(new ChannelId(3), new BundlePosition(0)),
            0));
        AdjusterOverlayValueV1 bGroup1 = Require(overlay.TryGetValue(
            new NodeKey(new ChannelId(3), new BundlePosition(0)),
            1));
        Assert.Equal(0.0005, aGroup0.DeltaSigmaAMInverse);
        Assert.Equal(0.00025, aGroup1.DeltaSigmaAMInverse);
        Assert.Equal(-0.000375, bGroup0.DeltaSigmaAMInverse);
        Assert.Equal(-0.0001875, bGroup1.DeltaSigmaAMInverse);

        AdjusterOverlayEvaluationV1 repeated = Require(AdjusterOverlayV1.TryEvaluate(map, state));
        Assert.Equal(overlay.OverlayDigest, repeated.OverlayDigest);
        Assert.Equal(overlay.ToCanonicalBytes(), repeated.ToCanonicalBytes());

        AdjusterSetStateV1 disabledBankState = CreateSetState(
            map,
            ApprovedBranchId,
            bankAEnabled: false,
            bankAStateFraction: 0.75,
            bankBStateFraction: 0.5,
            bankBAvailableFraction: 0.5);
        AdjusterOverlayEvaluationV1 disabledOverlay = Require(
            AdjusterOverlayV1.TryEvaluate(map, disabledBankState));
        Assert.All(disabledOverlay.Values.Where(value => value.TargetNode.ChannelId.Value < 3), value =>
            Assert.Equal(0.0, value.DeltaSigmaAMInverse));

        byte[] mapBefore = map.ToCanonicalBytes();
        ContractValidationResult<AdjusterOverlayEvaluationV1> missingMap =
            AdjusterOverlayV1.TryEvaluate(null, state);
        Assert.False(missingMap.IsValid);
        Assert.Equal("AdjusterOverlay.Map.Missing", missingMap.FirstDiagnostic.Code);
        Assert.Equal(mapBefore, map.ToCanonicalBytes());
    }

    [Fact]
    public void MotionUsesCompleteQueueProjectionDelayAndCausalRateLimit()
    {
        AdjusterInfluenceMapV1 map = CreateMap();
        AdjusterSetStateV1 state = CreateSetState(
            map,
            ApprovedBranchId,
            bankBStateFraction: 0.5,
            bankBAvailableFraction: 0.9);
        AdjusterBankStateV1 rateLimited = state.Banks.Single(bank =>
            bank.BankId == AdjusterBankGroupingV1.ApprovedBankBId);

        AdjusterMotionProjectionV1 beforeDue = Require(AdjusterMotionV1.TryAdvance(
            rateLimited,
            2.0,
            1.0));
        Assert.False(beforeDue.CommandDelaySatisfied);
        Assert.Equal(rateLimited.StateFraction, beforeDue.StateFractionAfter);
        Assert.Equal(0.0, beforeDue.AppliedDeltaFraction);
        Assert.True(beforeDue.AvailableCommandFraction.IsApplicable);
        Assert.Equal(0.9, beforeDue.AvailableCommandFraction.Value);
        Assert.True(beforeDue.QueueDigest.IsApplicable);

        AdjusterMotionProjectionV1 afterDue = Require(AdjusterMotionV1.TryAdvance(
            rateLimited,
            2.0,
            2.5));
        Assert.True(afterDue.CommandDelaySatisfied);
        Assert.Equal(0.25, afterDue.AppliedDeltaFraction);
        Assert.Equal(0.75, afterDue.StateFractionAfter);
        Assert.Equal(rateLimited.StateDigest, afterDue.SourceStateDigest);

        AdjusterBankStateV1 manual = state.Banks.Single(bank =>
            bank.BankId == AdjusterBankGroupingV1.ApprovedBankAId);
        AdjusterMotionProjectionV1 manualResult = Require(AdjusterMotionV1.TryAdvance(
            manual,
            10.0,
            10.0));
        Assert.Equal(manual.StateFraction, manualResult.StateFractionAfter);
        Assert.Equal(0.0, manualResult.AppliedDeltaFraction);
        Assert.False(manualResult.QueueDigest.IsApplicable);
        Assert.False(manualResult.AvailableCommandFraction.IsApplicable);
        Assert.False(manualResult.LastMotionTimeSeconds.IsApplicable);

        AdjusterBankStateV1 prescribed = Require(AdjusterBankStateV1.TryCreate(
            1,
            manual.BankId,
            true,
            AdjusterMotionModeV1.Prescribed,
            0.5,
            0.8,
            0.5,
            0.1,
            2.0,
            map.MapId,
            map.DataVersion,
            map.MapDigest,
            0.0,
            null));
        AdjusterMotionProjectionV1 prescribedResult = Require(AdjusterMotionV1.TryAdvance(
            prescribed,
            1.0,
            5.0));
        Assert.Equal(prescribed.StateFraction, prescribedResult.StateFractionAfter);
        Assert.Equal(0.0, prescribedResult.AppliedDeltaFraction);
        Assert.True(prescribedResult.CommandDelaySatisfied);
        Assert.False(prescribedResult.QueueDigest.IsApplicable);

        AdjusterQueueStateV1 delayedQueue = CreateQueue(
            map,
            ApprovedBranchId,
            0.9,
            0.1,
            2.0);
        AdjusterBankStateV1 delayedBank = Require(AdjusterBankStateV1.TryCreate(
            1,
            rateLimited.BankId,
            true,
            AdjusterMotionModeV1.RateLimited,
            0.5,
            0.5,
            0.5,
            0.1,
            2.0,
            map.MapId,
            map.DataVersion,
            map.MapDigest,
            0.0,
            delayedQueue));
        ContractValidationResult<AdjusterMotionProjectionV1> timeResult =
            AdjusterMotionV1.TryAdvance(delayedBank, 2.0, 1.0);
        Assert.False(timeResult.IsValid);
        Assert.Equal("AdjusterMotion.Time.Order", timeResult.FirstDiagnostic.Code);

        AdjusterQueueStateV1 zeroRateQueue = CreateQueue(
            map,
            ApprovedBranchId,
            0.9,
            0.0,
            0.0);
        AdjusterBankStateV1 zeroRateBank = Require(AdjusterBankStateV1.TryCreate(
            1,
            rateLimited.BankId,
            true,
            AdjusterMotionModeV1.RateLimited,
            0.5,
            0.5,
            0.5,
            0.0,
            2.0,
            map.MapId,
            map.DataVersion,
            map.MapDigest,
            0.0,
            zeroRateQueue));
        AdjusterMotionProjectionV1 zeroRateResult = Require(
            AdjusterMotionV1.TryAdvance(zeroRateBank, 1.0, 2.0));
        Assert.Equal(0.0, zeroRateResult.AppliedDeltaFraction);
        Assert.True(BitConverter.DoubleToInt64Bits(zeroRateResult.AppliedDeltaFraction) >= 0);
    }

    [Fact]
    public void PackageBindsCanonicalFixtureAndManifestHash()
    {
        string packagePath = TestDataLocator.RequireRepositoryFile(
            "data/packs/p6-t03-synthetic-adjuster-bank-map-v1.json");
        string manifestPath = TestDataLocator.RequireRepositoryFile(
            "data/packs/p6-t03-synthetic-adjuster-bank-map-v1.manifest.json");
        Assert.True(File.Exists(packagePath), "The approved P6-T03 package must exist.");
        Assert.True(File.Exists(manifestPath), "The approved P6-T03 manifest must exist.");

        byte[] packageBytes = File.ReadAllBytes(packagePath);
        using JsonDocument package = JsonDocument.Parse(packageBytes);
        JsonElement root = package.RootElement;
        AdjusterInfluenceMapV1 map = CreateMap();
        AdjusterSetStateV1 canonicalState = CreateSetState(map, ApprovedBranchId);
        AdjusterBankStateV1 canonicalBankA = canonicalState.Banks.Single(bank =>
            bank.BankId == AdjusterBankGroupingV1.ApprovedBankAId);
        AdjusterBankStateV1 canonicalBankB = canonicalState.Banks.Single(bank =>
            bank.BankId == AdjusterBankGroupingV1.ApprovedBankBId);
        AdjusterQueueStateV1 canonicalQueue = canonicalBankB.QueueState!;
        Assert.Equal("CANDU-ADJUSTER-INFLUENCE-MAP-PACKAGE-V1", root.GetProperty("schema_id").GetString());
        Assert.Equal("P6-T03-SYNTHETIC-ADJUSTER-BANK-MAP-V1", root.GetProperty("fixture_id").GetString());
        Assert.Equal("SYNTHETIC_TEST_ONLY", root.GetProperty("status").GetString());
        Assert.Equal("Kevin Ho", root.GetProperty("owner_id").GetString());
        Assert.Equal("2026-08-20", root.GetProperty("approval_date").GetString());
        Assert.Equal(AdjusterInfluenceMapV1.ApprovedDataVersion, root.GetProperty("data_version").GetString());
        Assert.Equal(canonicalState.AdjusterSetId.ToString(), root.GetProperty("adjuster_set_id").GetString());
        Assert.Equal(map.MapId.ToString(), root.GetProperty("map_id").GetString());
        Assert.Equal(Hex(map.MapDigest), root.GetProperty("map_digest").GetString());
        Assert.Equal(Hex(canonicalState.StateDigest), root.GetProperty("set_state_digest").GetString());
        Assert.Equal(Hex(map.GroupingDigest), root.GetProperty("grouping").GetProperty("grouping_digest").GetString());
        Assert.Equal(Hex(map.TopologyDigest), root.GetProperty("topology").GetProperty("topology_digest").GetString());
        Assert.Equal(Hex(map.ReferenceStateDigest), root.GetProperty("reference_state").GetProperty("reference_state_digest").GetString());
        Assert.Equal(12, root.GetProperty("entries").GetArrayLength());
        Assert.Equal(2, root.GetProperty("banks").GetArrayLength());
        Assert.Equal("RESERVED_P6-T06", root.GetProperty("boundary").GetProperty("queue_allocation").GetString());
        Assert.Equal("RESERVED_P6-T06", root.GetProperty("boundary").GetProperty("queue_rollback").GetString());
        Assert.Equal("RESERVED_P6-T07", root.GetProperty("boundary").GetProperty("scenario_packages").GetString());
        JsonElement packageBankA = root.GetProperty("banks")[0];
        JsonElement packageBankB = root.GetProperty("banks")[1];
        Assert.Equal("NotApplicable (Manual)", packageBankA.GetProperty("queue_state").GetString());
        Assert.Equal("NotApplicable", packageBankB.GetProperty("queue_state").GetProperty("generation_cadence_or_na").GetString());
        Assert.Equal(Hex(canonicalBankA.StateDigest), packageBankA.GetProperty("state_digest").GetString());
        Assert.Equal(Hex(canonicalQueue.QueueDigest), packageBankB.GetProperty("queue_state").GetProperty("queue_digest").GetString());
        Assert.Equal(Hex(canonicalBankB.StateDigest), packageBankB.GetProperty("state_digest").GetString());
        Assert.Equal(canonicalQueue.OwnerBranchId.ToString(), packageBankB.GetProperty("queue_state").GetProperty("owner_key").GetProperty("id").GetString());

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        JsonElement manifestRoot = manifest.RootElement;
        Assert.Equal("CANDU-SYNTHETIC-PACK-MANIFEST-V1", manifestRoot.GetProperty("manifest_schema_id").GetString());
        Assert.Equal(root.GetProperty("fixture_id").GetString(), manifestRoot.GetProperty("fixture_id").GetString());
        Assert.Equal("SYNTHETIC_TEST_ONLY", manifestRoot.GetProperty("status").GetString());
        Assert.Equal("Kevin Ho", manifestRoot.GetProperty("owner_id").GetString());
        Assert.Equal("2026-08-20", manifestRoot.GetProperty("approval_date").GetString());
        JsonElement manifestIdentity = manifestRoot.GetProperty("identity");
        Assert.Equal(canonicalState.AdjusterSetId.ToString(), manifestIdentity.GetProperty("adjuster_set_id").GetString());
        Assert.Equal(map.MapId.ToString(), manifestIdentity.GetProperty("map_id").GetString());
        Assert.Equal(map.GroupingId.ToString(), manifestIdentity.GetProperty("grouping_id").GetString());
        Assert.Equal(map.TopologyId.ToString(), manifestIdentity.GetProperty("topology_id").GetString());
        Assert.Equal(canonicalBankA.BankId.ToString(), manifestIdentity.GetProperty("bank_a_id").GetString());
        Assert.Equal(canonicalBankB.BankId.ToString(), manifestIdentity.GetProperty("bank_b_id").GetString());
        Assert.Equal(canonicalQueue.QueueId.ToString(), manifestIdentity.GetProperty("queue_b_id").GetString());
        Assert.Equal(canonicalQueue.OwnerBranchId.ToString(), manifestIdentity.GetProperty("owner_branch_id").GetString());
        JsonElement manifestDigests = manifestRoot.GetProperty("digests");
        Assert.Equal(Hex(map.GroupingDigest), manifestDigests.GetProperty("grouping_digest").GetString());
        Assert.Equal(Hex(map.TopologyDigest), manifestDigests.GetProperty("topology_digest").GetString());
        Assert.Equal(Hex(map.ReferenceStateDigest), manifestDigests.GetProperty("reference_state_digest").GetString());
        Assert.Equal(Hex(map.MapDigest), manifestDigests.GetProperty("map_digest").GetString());
        Assert.Equal(Hex(canonicalQueue.QueueDigest), manifestDigests.GetProperty("queue_digest").GetString());
        Assert.Equal(Hex(canonicalBankA.StateDigest), manifestDigests.GetProperty("bank_a_state_digest").GetString());
        Assert.Equal(Hex(canonicalBankB.StateDigest), manifestDigests.GetProperty("bank_b_state_digest").GetString());
        Assert.Equal(Hex(canonicalState.StateDigest), manifestDigests.GetProperty("set_state_digest").GetString());
        JsonElement manifestBoundary = manifestRoot.GetProperty("boundary");
        foreach (string boundaryField in new[]
        {
            "queue_allocation",
            "queue_enqueue",
            "queue_consume",
            "queue_transition",
            "queue_rollback",
            "scenario_packages",
            "local_state_map_scratch_rollback"
        })
        {
            Assert.Equal(
                root.GetProperty("boundary").GetProperty(boundaryField).GetString(),
                manifestBoundary.GetProperty(boundaryField).GetString());
        }
        JsonElement artifact = manifestRoot.GetProperty("artifact");
        Assert.Equal(packageBytes.Length, artifact.GetProperty("byte_length").GetInt64());
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant(),
            artifact.GetProperty("sha256").GetString());
    }

    private static AdjusterInfluenceMapV1 CreateMap()
    {
        AdjusterBankTargetBindingV1[] mappings = Enumerable.Range(0, 6)
            .Select(index => Require(AdjusterBankTargetBindingV1.TryCreate(
                (uint)(index < 3 ? 0 : 1),
                index < 3
                    ? AdjusterBankGroupingV1.ApprovedBankAId
                    : AdjusterBankGroupingV1.ApprovedBankBId,
                new NodeKey(new ChannelId((uint)index), new BundlePosition(0)))))
            .ToArray();
        Digest32 groupingDigest = AdjusterBankGroupingV1.ComputeDigest(
            1,
            AdjusterBankGroupingV1.ApprovedGroupingId,
            AdjusterBankGroupingV1.ApprovedMappingVersion,
            mappings);
        AdjusterBankGroupingV1 grouping = Require(AdjusterBankGroupingV1.TryCreate(
            1,
            AdjusterBankGroupingV1.ApprovedGroupingId,
            AdjusterBankGroupingV1.ApprovedMappingVersion,
            mappings,
            groupingDigest));

        NodeKey[] targetNodes = Enumerable.Range(0, 6)
            .Select(index => new NodeKey(new ChannelId((uint)index), new BundlePosition(0)))
            .ToArray();
        Digest32 topologyDigest = AdjusterInfluenceMapV1.ComputeTopologyDigest(
            AdjusterInfluenceMapV1.ApprovedTopologyId,
            targetNodes);
        KeyValuePair<StableId, double>[] references =
        {
            new KeyValuePair<StableId, double>(AdjusterBankGroupingV1.ApprovedBankAId, 0.5),
            new KeyValuePair<StableId, double>(AdjusterBankGroupingV1.ApprovedBankBId, 0.5)
        };
        Digest32 referenceStateDigest = AdjusterInfluenceMapV1.ComputeReferenceStateDigest(
            AdjusterInfluenceMapV1.ApprovedMapId,
            grouping,
            AdjusterInfluenceMapV1.ApprovedTopologyId,
            topologyDigest,
            AdjusterInfluenceMapV1.ApprovedDataVersion,
            AdjusterInfluenceMapV1.ApprovedOwnerId,
            AdjusterInfluenceMapV1.ApprovedSignCertificate,
            AdjusterInfluenceMapV1.ApprovedNormalization,
            AdjusterInfluenceMapV1.SourceUnitDimensionless,
            AdjusterInfluenceMapV1.TargetUnitMInverse,
            references);
        AdjusterInfluenceMapEntryV1[] entries = mappings
            .SelectMany(mapping => new[]
            {
                Require(AdjusterInfluenceMapEntryV1.TryCreate(
                    mapping.BankId,
                    mapping.TargetNode,
                    0,
                    mapping.BankId == AdjusterBankGroupingV1.ApprovedBankAId ? 0.0020 : 0.0015,
                    AdjusterInfluenceMapV1.SourceUnitDimensionless,
                    AdjusterInfluenceMapV1.TargetUnitMInverse)),
                Require(AdjusterInfluenceMapEntryV1.TryCreate(
                    mapping.BankId,
                    mapping.TargetNode,
                    1,
                    mapping.BankId == AdjusterBankGroupingV1.ApprovedBankAId ? 0.0010 : 0.00075,
                    AdjusterInfluenceMapV1.SourceUnitDimensionless,
                    AdjusterInfluenceMapV1.TargetUnitMInverse))
            })
            .ToArray();
        Digest32 mapDigest = AdjusterInfluenceMapV1.ComputeDigest(
            1,
            AdjusterInfluenceMapV1.ApprovedAdjusterSetId,
            AdjusterInfluenceMapV1.ApprovedMapId,
            grouping,
            AdjusterInfluenceMapV1.ApprovedTopologyId,
            targetNodes,
            topologyDigest,
            AdjusterInfluenceMapV1.ApprovedDataVersion,
            AdjusterInfluenceMapV1.ApprovedOwnerId,
            AdjusterInfluenceMapV1.ApprovedSignCertificate,
            AdjusterInfluenceMapV1.ApprovedNormalization,
            AdjusterInfluenceMapV1.SourceUnitDimensionless,
            AdjusterInfluenceMapV1.TargetUnitMInverse,
            references,
            referenceStateDigest,
            entries);
        return Require(AdjusterInfluenceMapV1.TryCreate(
            1,
            AdjusterInfluenceMapV1.ApprovedAdjusterSetId,
            AdjusterInfluenceMapV1.ApprovedMapId,
            grouping,
            AdjusterInfluenceMapV1.ApprovedTopologyId,
            targetNodes,
            topologyDigest,
            AdjusterInfluenceMapV1.ApprovedDataVersion,
            AdjusterInfluenceMapV1.ApprovedOwnerId,
            AdjusterInfluenceMapV1.ApprovedSignCertificate,
            AdjusterInfluenceMapV1.ApprovedNormalization,
            AdjusterInfluenceMapV1.SourceUnitDimensionless,
            AdjusterInfluenceMapV1.TargetUnitMInverse,
            references,
            referenceStateDigest,
            entries,
            mapDigest));
    }

    private static AdjusterSetStateV1 CreateSetState(
        AdjusterInfluenceMapV1 map,
        StableId branchId,
        bool bankAEnabled = true,
        double bankAStateFraction = 0.5,
        double bankBStateFraction = 0.5,
        double bankBAvailableFraction = 0.5)
    {
        AdjusterBankStateV1 bankA = Require(AdjusterBankStateV1.TryCreate(
            1,
            AdjusterBankGroupingV1.ApprovedBankAId,
            bankAEnabled,
            AdjusterMotionModeV1.Manual,
            0.5,
            0.5,
            bankAStateFraction,
            0.1,
            2.0,
            map.MapId,
            map.DataVersion,
            map.MapDigest,
            0.0,
            null));
        AdjusterBankStateV1 bankB = Require(AdjusterBankStateV1.TryCreate(
            1,
            AdjusterBankGroupingV1.ApprovedBankBId,
            true,
            AdjusterMotionModeV1.RateLimited,
            0.5,
            0.5,
            bankBStateFraction,
            0.1,
            2.0,
            map.MapId,
            map.DataVersion,
            map.MapDigest,
            0.0,
            CreateQueue(map, branchId, bankBAvailableFraction, 0.1, 0.0)));
        return Require(AdjusterSetStateV1.TryCreate(
            1,
            map.AdjusterSetId,
            branchId,
            42,
            "p6-t03-synthetic-topology-v1",
            map.DataVersion,
            map,
            map.Grouping,
            new[] { bankB, bankA }));
    }

    private static AdjusterQueueStateV1 CreateQueue(
        AdjusterInfluenceMapV1 map,
        StableId branchId,
        double availableFraction,
        double rateLimit,
        double lastMotionTimeSeconds)
    {
        AdjusterAvailableCommandV1 available = Require(AdjusterAvailableCommandV1.TryCreate(
            AdjusterBankGroupingV1.ApprovedBankBId,
            availableFraction,
            0.0,
            1.0,
            rateLimit));
        return Require(AdjusterQueueStateV1.TryCreate(
            1,
            StableId.Parse("00000000-0000-0000-0000-00000000a708"),
            branchId,
            AdjusterOptionalDoubleV1.NotApplicable,
            0,
            0,
            new[] { available },
            lastMotionTimeSeconds,
            Array.Empty<AdjusterPendingCommandV1>(),
            Array.Empty<StableId>(),
            Array.Empty<StableId>()));
    }

    private static StableId ApprovedBranchId
    {
        get { return StableId.Parse("00000000-0000-0000-0000-00000000a709"); }
    }

    private static string Hex(Digest32 digest)
    {
        return Convert.ToHexString(digest.ToArray()).ToLowerInvariant();
    }

    private static StableId Id(uint value)
    {
        return StableId.Parse(
            "00000000-0000-0000-0000-" + value.ToString("x12", CultureInfo.InvariantCulture));
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }
}
