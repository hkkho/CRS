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

public sealed class P6T04BulkPoisonContractsTests
{
    private readonly ITestOutputHelper _output;

    public P6T04BulkPoisonContractsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ApprovedMapAndStateAreCanonicalAcrossInputOrder()
    {
        BulkPoisonInfluenceMapV1 map = CreateMap();
        BulkPoisonStateV1 state = CreateState(map);

        BulkPoisonInfluenceMapV1 reordered = Require(BulkPoisonInfluenceMapV1.TryCreate(
            map.SchemaVersion,
            map.PoisonSourceId,
            map.MapId,
            map.TopologyId,
            map.TargetNodes.Reverse(),
            map.TopologyDigest,
            map.DataVersion,
            map.OwnerId,
            map.SignCertificate,
            map.Normalization,
            map.SourceUnit,
            map.TargetUnit,
            map.ModeratorVolumeM3,
            map.ReferenceConcentrationKgPerM3,
            map.AddRateKgPerSecond,
            map.WithdrawRateKgPerSecond,
            map.SetupMaximumMassKg,
            map.ReferenceStateDigest,
            map.Entries.Reverse(),
            map.MapDigest));

        Assert.Equal(6, map.TargetNodes.Count);
        Assert.Equal(12, map.Entries.Count);
        Assert.Equal(BulkPoisonInfluenceMapV1.ApprovedDataVersion, map.DataVersion);
        Assert.Equal("Kevin Ho", map.OwnerId);
        Assert.Equal("kg/m^3", map.SourceUnit);
        Assert.Equal("m^-1", map.TargetUnit);
        Assert.Equal(map.MapDigest, reordered.MapDigest);
        Assert.Equal(map.ToCanonicalBytes(), reordered.ToCanonicalBytes());
        Assert.Equal(0.1, state.PoisonMassConcentrationKgPerM3);
        Assert.Equal(BulkPoisonStateV1.ComputeDigest(
            state.SchemaVersion,
            state.PoisonSourceId,
            state.Enabled,
            state.Mode,
            state.PoisonMassKg,
            state.ModeratorVolumeM3,
            state.PoisonMassConcentrationKgPerM3,
            state.ReferenceConcentrationKgPerM3,
            state.AddRateKgPerSecond,
            state.WithdrawRateKgPerSecond,
            state.InfluenceMapId,
            state.DataVersion,
            state.InfluenceMapDigest,
            state.UpdateTimeSeconds), state.StateDigest);

        BulkPoisonOverlayEvaluationV1 overlay = Require(
            BulkPoisonOverlayV1.TryEvaluate(map, state));
        BulkPoisonActionResultV1 addAction = Require(
            BulkPoisonAccountingV1.TryAdvance(state, 2.0));
        _output.WriteLine("P6T04_TOPOLOGY_DIGEST=" + Hex(map.TopologyDigest));
        _output.WriteLine("P6T04_REFERENCE_STATE_DIGEST=" + Hex(map.ReferenceStateDigest));
        _output.WriteLine("P6T04_MAP_DIGEST=" + Hex(map.MapDigest));
        _output.WriteLine("P6T04_STATE_DIGEST=" + Hex(state.StateDigest));
        _output.WriteLine("P6T04_OVERLAY_DIGEST=" + Hex(overlay.OverlayDigest));
        _output.WriteLine("P6T04_ACTION_DIGEST=" + Hex(addAction.ActionDigest));
        _output.WriteLine("P6T04_MAP_CANONICAL_LENGTH=" + map.ToCanonicalBytes().Length.ToString(CultureInfo.InvariantCulture));
        _output.WriteLine("P6T04_STATE_CANONICAL_LENGTH=" + state.ToCanonicalBytes().Length.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void AddWithdrawAndPrescribedActionsAreExactAndNonMutating()
    {
        BulkPoisonInfluenceMapV1 map = CreateMap();
        BulkPoisonStateV1 addState = CreateState(map);
        byte[] stateBefore = addState.ToCanonicalBytes();
        byte[] mapBefore = map.ToCanonicalBytes();

        BulkPoisonActionResultV1 add = Require(
            BulkPoisonAccountingV1.TryAdvance(addState, 2.0));
        Assert.Equal(0.2, add.RequestedMassDeltaKg);
        Assert.Equal(0.2, add.AppliedMassDeltaKg);
        Assert.Equal(0.7, add.PoisonMassAfterKg);
        Assert.Equal(0.7 / 5.0, add.ConcentrationAfterKgPerM3);
        Assert.Equal(stateBefore, addState.ToCanonicalBytes());
        Assert.Equal(mapBefore, map.ToCanonicalBytes());

        BulkPoisonStateV1 withdrawState = CreateState(
            map,
            BulkPoisonModeV1.Withdraw,
            poisonMassKg: 0.05);
        BulkPoisonActionResultV1 withdraw = Require(
            BulkPoisonAccountingV1.TryAdvance(withdrawState, 10.0));
        Assert.Equal(-0.1, withdraw.RequestedMassDeltaKg);
        Assert.Equal(-0.05, withdraw.AppliedMassDeltaKg);
        Assert.Equal(0.0, withdraw.PoisonMassAfterKg);
        Assert.Equal(0.0, withdraw.ConcentrationAfterKgPerM3);
        Assert.NotEqual(long.MinValue, BitConverter.DoubleToInt64Bits(withdraw.AppliedMassDeltaKg));

        BulkPoisonStateV1 prescribedState = CreateState(
            map,
            BulkPoisonModeV1.Prescribed);
        BulkPoisonActionResultV1 setup = Require(
            BulkPoisonAccountingV1.TryAdvance(prescribedState, 0.0));
        Assert.Equal(0.0, setup.AppliedMassDeltaKg);
        Assert.Equal(0.5, setup.PoisonMassAfterKg);

        ContractValidationResult<BulkPoisonActionResultV1> prescribedAdvance =
            BulkPoisonAccountingV1.TryAdvance(prescribedState, 1.0);
        Assert.False(prescribedAdvance.IsValid);
        Assert.Equal(
            "BulkPoisonAccounting.PrescribedPositiveDuration",
            prescribedAdvance.FirstDiagnostic.Code);
    }

    [Fact]
    public void OverlayUsesDerivedConcentrationPositiveSignAndDisabledZero()
    {
        BulkPoisonInfluenceMapV1 map = CreateMap();
        BulkPoisonStateV1 state = CreateState(map);
        byte[] stateBefore = state.ToCanonicalBytes();
        byte[] mapBefore = map.ToCanonicalBytes();

        BulkPoisonOverlayEvaluationV1 overlay = Require(
            BulkPoisonOverlayV1.TryEvaluate(map, state));
        Assert.Equal(12, overlay.Values.Count);
        Assert.Equal(0.008, Require(overlay.TryGetValue(
            new NodeKey(new ChannelId(0), new BundlePosition(0)),
            0)).DeltaSigmaAMInverse);
        Assert.Equal(0.05 * (0.5 / 5.0), Require(overlay.TryGetValue(
            new NodeKey(new ChannelId(0), new BundlePosition(0)),
            1)).DeltaSigmaAMInverse);
        Assert.Equal(stateBefore, state.ToCanonicalBytes());
        Assert.Equal(mapBefore, map.ToCanonicalBytes());

        BulkPoisonOverlayEvaluationV1 repeated = Require(
            BulkPoisonOverlayV1.TryEvaluate(map, state));
        Assert.Equal(overlay.OverlayDigest, repeated.OverlayDigest);
        Assert.Equal(overlay.ToCanonicalBytes(), repeated.ToCanonicalBytes());

        BulkPoisonStateV1 disabledState = CreateState(
            map,
            BulkPoisonModeV1.Disabled,
            enabled: false);
        BulkPoisonOverlayEvaluationV1 disabled = Require(
            BulkPoisonOverlayV1.TryEvaluate(map, disabledState));
        Assert.Single(disabled.DisabledZeroAssertions);
        Assert.All(disabled.Values, value => Assert.Equal(0.0, value.DeltaSigmaAMInverse));
        Assert.True(BitConverter.DoubleToInt64Bits(
            disabled.Values[0].DeltaSigmaAMInverse) >= 0);
    }

    [Fact]
    public void InvalidUnitsBoundsDigestsAndBindingsFailClosed()
    {
        BulkPoisonInfluenceMapV1 map = CreateMap();

        ContractValidationResult<BulkPoisonInfluenceMapEntryV1> wrongWeight =
            BulkPoisonInfluenceMapEntryV1.TryCreate(
                map.PoisonSourceId,
                map.TargetNodes[0],
                0,
                0.081,
                map.SourceUnit,
                map.TargetUnit);
        Assert.False(wrongWeight.IsValid);
        Assert.Equal("BulkPoisonInfluenceMapEntry.Weight.Unapproved", wrongWeight.FirstDiagnostic.Code);

        ContractValidationResult<BulkPoisonStateV1> negativeMass =
            BulkPoisonStateV1.TryCreate(
                1,
                map.PoisonSourceId,
                true,
                BulkPoisonModeV1.Add,
                -0.0,
                map.ModeratorVolumeM3,
                map.ReferenceConcentrationKgPerM3,
                map.AddRateKgPerSecond,
                map.WithdrawRateKgPerSecond,
                map.MapId,
                map.DataVersion,
                map.MapDigest,
                0.0,
                new Digest32(new byte[32]));
        Assert.False(negativeMass.IsValid);
        Assert.Equal("BulkPoisonState.Mass.Invalid", negativeMass.FirstDiagnostic.Code);

        StableId wrongMapId = StableId.Parse(
            "00000000-0000-0000-0000-00000000b402");
        Digest32 wrongMapStateDigest = BulkPoisonStateV1.ComputeDigest(
            1,
            map.PoisonSourceId,
            true,
            BulkPoisonModeV1.Add,
            0.5,
            map.ModeratorVolumeM3,
            0.5 / map.ModeratorVolumeM3,
            map.ReferenceConcentrationKgPerM3,
            map.AddRateKgPerSecond,
            map.WithdrawRateKgPerSecond,
            wrongMapId,
            map.DataVersion,
            map.MapDigest,
            0.0);
        ContractValidationResult<BulkPoisonStateV1> wrongMapBinding =
            BulkPoisonStateV1.TryCreate(
                1,
                map.PoisonSourceId,
                true,
                BulkPoisonModeV1.Add,
                0.5,
                map.ModeratorVolumeM3,
                map.ReferenceConcentrationKgPerM3,
                map.AddRateKgPerSecond,
                map.WithdrawRateKgPerSecond,
                wrongMapId,
                map.DataVersion,
                map.MapDigest,
                0.0,
                wrongMapStateDigest);
        Assert.False(wrongMapBinding.IsValid);
        Assert.Equal("BulkPoisonState.MapId.Unapproved", wrongMapBinding.FirstDiagnostic.Code);

        BulkPoisonStateV1 state = CreateState(map);

        ContractValidationResult<BulkPoisonOverlayEvaluationV1> missingMap =
            BulkPoisonOverlayV1.TryEvaluate(null, state);
        Assert.False(missingMap.IsValid);
        Assert.Equal("BulkPoisonOverlay.Map.Missing", missingMap.FirstDiagnostic.Code);
    }

    [Fact]
    public void LocalScratchOverlayRollbackPreservesCanonicalInputs()
    {
        BulkPoisonInfluenceMapV1 map = CreateMap();
        BulkPoisonStateV1 state = CreateState(
            map,
            BulkPoisonModeV1.Withdraw,
            poisonMassKg: 0.8);
        byte[] stateBefore = state.ToCanonicalBytes();
        byte[] mapBefore = map.ToCanonicalBytes();

        BulkPoisonActionResultV1 action = Require(
            BulkPoisonAccountingV1.TryAdvance(state, 3.0));
        BulkPoisonOverlayEvaluationV1 overlay = Require(
            BulkPoisonOverlayV1.TryEvaluate(map, state));

        Assert.Equal(-0.03, action.AppliedMassDeltaKg);
        Assert.Equal(0.08 * (0.8 / 5.0), Require(overlay.TryGetValue(
            new NodeKey(new ChannelId(0), new BundlePosition(0)),
            0)).DeltaSigmaAMInverse * 1.0);
        Assert.Equal(stateBefore, state.ToCanonicalBytes());
        Assert.Equal(mapBefore, map.ToCanonicalBytes());
        Assert.Equal(state.StateDigest, overlay.StateDigest);
        Assert.Equal(map.MapDigest, overlay.MapDigest);
    }

    [Fact]
    public void PackageAndManifestBindCanonicalFixtureAndArtifactHash()
    {
        string packagePath = TestDataLocator.RequireRepositoryFile(
            "data/packs/p6-t04-synthetic-bulk-poison-map-v1.json");
        string manifestPath = TestDataLocator.RequireRepositoryFile(
            "data/packs/p6-t04-synthetic-bulk-poison-map-v1.manifest.json");
        Assert.True(File.Exists(packagePath), "The approved P6-T04 package must exist.");
        Assert.True(File.Exists(manifestPath), "The approved P6-T04 manifest must exist.");

        BulkPoisonInfluenceMapV1 map = CreateMap();
        BulkPoisonStateV1 state = CreateState(map);
        BulkPoisonOverlayEvaluationV1 overlay = Require(
            BulkPoisonOverlayV1.TryEvaluate(map, state));
        BulkPoisonActionResultV1 action = Require(
            BulkPoisonAccountingV1.TryAdvance(state, 2.0));

        byte[] packageBytes = File.ReadAllBytes(packagePath);
        using JsonDocument package = JsonDocument.Parse(packageBytes);
        JsonElement root = package.RootElement;
        Assert.Equal(
            "CANDU-BULK-POISON-INFLUENCE-MAP-PACKAGE-V1",
            root.GetProperty("schema_id").GetString());
        Assert.Equal(
            "P6-T04-SYNTHETIC-BULK-POISON-MAP-V1",
            root.GetProperty("fixture_id").GetString());
        Assert.Equal("SYNTHETIC_TEST_ONLY", root.GetProperty("status").GetString());
        Assert.Equal("Kevin Ho", root.GetProperty("owner_id").GetString());
        Assert.Equal("2026-08-20", root.GetProperty("approval_date").GetString());
        Assert.Equal(
            "docs/tasks/P6-T04-OWNER-APPROVAL.md",
            root.GetProperty("approval_record").GetString());
        Assert.Equal(map.PoisonSourceId.ToString(), root.GetProperty("poison_source_id").GetString());
        Assert.Equal(map.MapId.ToString(), root.GetProperty("map_id").GetString());
        Assert.Equal(map.TopologyId.ToString(), root.GetProperty("topology_id").GetString());
        Assert.Equal(map.DataVersion, root.GetProperty("data_version").GetString());
        Assert.Equal(map.SignCertificate, root.GetProperty("sign_certificate").GetString());
        Assert.Equal(map.Normalization, root.GetProperty("normalization").GetString());
        Assert.Equal(Hex(map.TopologyDigest), root.GetProperty("topology").GetProperty("topology_digest").GetString());
        Assert.Equal(Hex(map.ReferenceStateDigest), root.GetProperty("reference_state").GetProperty("reference_state_digest").GetString());
        Assert.Equal(6, root.GetProperty("topology").GetProperty("ordered_target_nodes").GetArrayLength());
        Assert.Equal(12, root.GetProperty("entries").GetArrayLength());
        JsonElement packageUnits = root.GetProperty("units");
        Assert.Equal(map.SourceUnit, packageUnits.GetProperty("source").GetString());
        Assert.Equal(map.TargetUnit, packageUnits.GetProperty("target").GetString());
        Assert.Equal("kg", packageUnits.GetProperty("mass").GetString());
        Assert.Equal("m^3", packageUnits.GetProperty("volume").GetString());
        Assert.Equal(map.SourceUnit, packageUnits.GetProperty("concentration").GetString());
        Assert.Equal("kg/s", packageUnits.GetProperty("rate").GetString());
        Assert.Equal("s", packageUnits.GetProperty("time").GetString());
        JsonElement packageLimits = root.GetProperty("limits");
        Assert.Equal(map.ModeratorVolumeM3, packageLimits.GetProperty("moderator_volume_m3").GetDouble());
        Assert.Equal(map.ReferenceConcentrationKgPerM3, packageLimits.GetProperty("reference_concentration_kg_per_m3").GetDouble());
        Assert.Equal(map.SetupMaximumMassKg, packageLimits.GetProperty("setup_maximum_mass_kg").GetDouble());
        Assert.Equal(map.AddRateKgPerSecond, packageLimits.GetProperty("add_rate_kg_per_second").GetDouble());
        Assert.Equal(map.WithdrawRateKgPerSecond, packageLimits.GetProperty("withdraw_rate_kg_per_second").GetDouble());

        JsonElement packageEntries = root.GetProperty("entries");
        for (int index = 0; index < map.Entries.Count; index++)
        {
            BulkPoisonInfluenceMapEntryV1 typedEntry = map.Entries[index];
            JsonElement packageEntry = packageEntries[index];
            JsonElement packageTarget = packageEntry.GetProperty("target_node");
            Assert.Equal(typedEntry.PoisonSourceId.ToString(), packageEntry.GetProperty("poison_source_id").GetString());
            Assert.Equal(typedEntry.TargetNode.ChannelId.Value, packageTarget.GetProperty("channel_id").GetUInt32());
            Assert.Equal(typedEntry.TargetNode.Position.Value, packageTarget.GetProperty("bundle_position").GetUInt32());
            Assert.Equal(typedEntry.GroupIndex, packageEntry.GetProperty("group_index").GetUInt16());
            Assert.Equal(typedEntry.WeightMInversePerKgPerM3, packageEntry.GetProperty("weight_m_inverse_per_kg_per_m3").GetDouble());
            Assert.Equal(typedEntry.SourceUnit, packageEntry.GetProperty("source_unit").GetString());
            Assert.Equal(typedEntry.TargetUnit, packageEntry.GetProperty("target_unit").GetString());
        }

        JsonElement packageTopology = root.GetProperty("topology").GetProperty("ordered_target_nodes");
        for (int index = 0; index < map.TargetNodes.Count; index++)
        {
            JsonElement packageTarget = packageTopology[index];
            Assert.Equal(map.TargetNodes[index].ChannelId.Value, packageTarget.GetProperty("channel_id").GetUInt32());
            Assert.Equal(map.TargetNodes[index].Position.Value, packageTarget.GetProperty("bundle_position").GetUInt32());
        }

        JsonElement packageReference = root.GetProperty("reference_state");
        Assert.Equal(map.ModeratorVolumeM3, packageReference.GetProperty("moderator_volume_m3").GetDouble());
        Assert.Equal(map.ReferenceConcentrationKgPerM3, packageReference.GetProperty("reference_concentration_kg_per_m3").GetDouble());
        Assert.Equal(map.AddRateKgPerSecond, packageReference.GetProperty("add_rate_kg_per_second").GetDouble());
        Assert.Equal(map.WithdrawRateKgPerSecond, packageReference.GetProperty("withdraw_rate_kg_per_second").GetDouble());
        Assert.Equal(map.SetupMaximumMassKg, packageReference.GetProperty("setup_maximum_mass_kg").GetDouble());

        JsonElement packageState = root.GetProperty("state");
        Assert.Equal(state.PoisonMassKg, packageState.GetProperty("poison_mass_kg").GetDouble());
        Assert.Equal(state.PoisonMassConcentrationKgPerM3, packageState.GetProperty("poison_mass_concentration_kg_per_m3").GetDouble());
        Assert.Equal(Hex(state.StateDigest), packageState.GetProperty("state_digest").GetString());
        Assert.Equal(Hex(map.MapDigest), packageState.GetProperty("influence_map_digest").GetString());
        JsonElement packageOverlay = root.GetProperty("overlay");
        Assert.Equal(0.008, packageOverlay.GetProperty("reference_state_group0_delta_sigma_a_m_inverse").GetDouble());
        Assert.Equal(0.005, packageOverlay.GetProperty("reference_state_group1_delta_sigma_a_m_inverse").GetDouble());
        Assert.Equal(Hex(overlay.OverlayDigest), packageOverlay.GetProperty("overlay_digest").GetString());
        JsonElement packageAction = root.GetProperty("accounting_examples")[0];
        Assert.Equal(action.RequestedMassDeltaKg, packageAction.GetProperty("requested_mass_delta_kg").GetDouble());
        Assert.Equal(action.AppliedMassDeltaKg, packageAction.GetProperty("applied_mass_delta_kg").GetDouble());
        Assert.Equal(action.PoisonMassAfterKg, packageAction.GetProperty("mass_after_kg").GetDouble());
        Assert.Equal(
            action.ConcentrationAfterKgPerM3,
            packageAction.GetProperty("concentration_after_kg_per_m3").GetDouble(),
            12);
        Assert.Equal(Hex(action.ActionDigest), packageAction.GetProperty("action_digest").GetString());
        Assert.Equal("RESERVED_P6-T06", root.GetProperty("boundary").GetProperty("queue_allocation").GetString());
        Assert.Equal("RESERVED_P6-T06", root.GetProperty("boundary").GetProperty("queue_rollback").GetString());
        Assert.Equal("RESERVED_P6-T07", root.GetProperty("boundary").GetProperty("scenario_packages").GetString());
        Assert.Equal("FORBIDDEN", root.GetProperty("boundary").GetProperty("hidden_chemistry_decay_transport_cleanup").GetString());
        Assert.Equal("FORBIDDEN", root.GetProperty("boundary").GetProperty("production_external_golden_claim").GetString());

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        JsonElement manifestRoot = manifest.RootElement;
        Assert.Equal("CANDU-SYNTHETIC-PACK-MANIFEST-V1", manifestRoot.GetProperty("manifest_schema_id").GetString());
        Assert.Equal(root.GetProperty("fixture_id").GetString(), manifestRoot.GetProperty("fixture_id").GetString());
        Assert.Equal("SYNTHETIC_TEST_ONLY", manifestRoot.GetProperty("status").GetString());
        Assert.Equal("Kevin Ho", manifestRoot.GetProperty("owner_id").GetString());
        Assert.Equal("2026-08-20", manifestRoot.GetProperty("approval_date").GetString());
        Assert.Equal(map.DataVersion, manifestRoot.GetProperty("data_version").GetString());
        Assert.Equal(map.SignCertificate, manifestRoot.GetProperty("sign_certificate").GetString());
        Assert.Equal(map.Normalization, manifestRoot.GetProperty("normalization").GetString());
        JsonElement manifestIdentity = manifestRoot.GetProperty("identity");
        Assert.Equal(map.PoisonSourceId.ToString(), manifestIdentity.GetProperty("poison_source_id").GetString());
        Assert.Equal(map.MapId.ToString(), manifestIdentity.GetProperty("map_id").GetString());
        Assert.Equal(map.TopologyId.ToString(), manifestIdentity.GetProperty("topology_id").GetString());
        JsonElement manifestDigests = manifestRoot.GetProperty("digests");
        Assert.Equal(Hex(map.TopologyDigest), manifestDigests.GetProperty("topology_digest").GetString());
        Assert.Equal(Hex(map.ReferenceStateDigest), manifestDigests.GetProperty("reference_state_digest").GetString());
        Assert.Equal(Hex(map.MapDigest), manifestDigests.GetProperty("map_digest").GetString());
        Assert.Equal(Hex(state.StateDigest), manifestDigests.GetProperty("state_digest").GetString());
        Assert.Equal(Hex(overlay.OverlayDigest), manifestDigests.GetProperty("overlay_digest").GetString());
        Assert.Equal(Hex(action.ActionDigest), manifestDigests.GetProperty("action_digest").GetString());
        JsonElement manifestUnits = manifestRoot.GetProperty("units");
        Assert.Equal(packageUnits.GetProperty("source").GetString(), manifestUnits.GetProperty("source").GetString());
        Assert.Equal(packageUnits.GetProperty("target").GetString(), manifestUnits.GetProperty("target").GetString());
        Assert.Equal(packageUnits.GetProperty("mass").GetString(), manifestUnits.GetProperty("mass").GetString());
        Assert.Equal(packageUnits.GetProperty("volume").GetString(), manifestUnits.GetProperty("volume").GetString());
        Assert.Equal(packageUnits.GetProperty("concentration").GetString(), manifestUnits.GetProperty("concentration").GetString());
        Assert.Equal(packageUnits.GetProperty("rate").GetString(), manifestUnits.GetProperty("rate").GetString());
        Assert.Equal(packageUnits.GetProperty("time").GetString(), manifestUnits.GetProperty("time").GetString());
        JsonElement manifestLimits = manifestRoot.GetProperty("limits");
        foreach (string limitField in new[]
        {
            "moderator_volume_m3",
            "reference_concentration_kg_per_m3",
            "setup_maximum_mass_kg",
            "add_rate_kg_per_second",
            "withdraw_rate_kg_per_second"
        })
        {
            Assert.Equal(
                packageLimits.GetProperty(limitField).GetDouble(),
                manifestLimits.GetProperty(limitField).GetDouble());
        }
        foreach (string boundaryField in new[]
        {
            "queue_allocation",
            "queue_enqueue",
            "queue_consume",
            "queue_transition",
            "queue_rollback",
            "scenario_packages",
            "local_state_map_scratch_rollback",
            "hidden_chemistry_decay_transport_cleanup",
            "production_external_golden_claim"
        })
        {
            Assert.Equal(
                root.GetProperty("boundary").GetProperty(boundaryField).GetString(),
                manifestRoot.GetProperty("boundary").GetProperty(boundaryField).GetString());
        }
        JsonElement artifact = manifestRoot.GetProperty("artifact");
        Assert.Equal(
            "data/packs/p6-t04-synthetic-bulk-poison-map-v1.json",
            artifact.GetProperty("path").GetString());
        Assert.Equal(packageBytes.Length, artifact.GetProperty("byte_length").GetInt64());
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant(),
            artifact.GetProperty("sha256").GetString());
    }

    private static BulkPoisonInfluenceMapV1 CreateMap()
    {
        NodeKey[] targetNodes = Enumerable.Range(0, 6)
            .Select(index => new NodeKey(
                new ChannelId((uint)index),
                new BundlePosition(0)))
            .ToArray();
        Digest32 topologyDigest = BulkPoisonInfluenceMapV1.ComputeTopologyDigest(
            BulkPoisonInfluenceMapV1.ApprovedTopologyId,
            targetNodes);
        Digest32 referenceStateDigest = BulkPoisonInfluenceMapV1.ComputeReferenceStateDigest(
            BulkPoisonInfluenceMapV1.ApprovedPoisonSourceId,
            BulkPoisonInfluenceMapV1.ApprovedMapId,
            BulkPoisonInfluenceMapV1.ApprovedTopologyId,
            topologyDigest,
            BulkPoisonInfluenceMapV1.ApprovedDataVersion,
            BulkPoisonInfluenceMapV1.ApprovedOwnerId,
            BulkPoisonInfluenceMapV1.ApprovedSignCertificate,
            BulkPoisonInfluenceMapV1.ApprovedNormalization,
            BulkPoisonInfluenceMapV1.SourceUnitKgPerM3,
            BulkPoisonInfluenceMapV1.TargetUnitMInverse,
            BulkPoisonInfluenceMapV1.ApprovedModeratorVolumeM3,
            BulkPoisonInfluenceMapV1.ApprovedReferenceConcentrationKgPerM3,
            BulkPoisonInfluenceMapV1.ApprovedAddRateKgPerSecond,
            BulkPoisonInfluenceMapV1.ApprovedWithdrawRateKgPerSecond,
            BulkPoisonInfluenceMapV1.ApprovedSetupMaximumMassKg);
        BulkPoisonInfluenceMapEntryV1[] entries = targetNodes
            .SelectMany(target => new[]
            {
                Require(BulkPoisonInfluenceMapEntryV1.TryCreate(
                    BulkPoisonInfluenceMapV1.ApprovedPoisonSourceId,
                    target,
                    0,
                    BulkPoisonInfluenceMapV1.ApprovedGroup0WeightMInversePerKgPerM3,
                    BulkPoisonInfluenceMapV1.SourceUnitKgPerM3,
                    BulkPoisonInfluenceMapV1.TargetUnitMInverse)),
                Require(BulkPoisonInfluenceMapEntryV1.TryCreate(
                    BulkPoisonInfluenceMapV1.ApprovedPoisonSourceId,
                    target,
                    1,
                    BulkPoisonInfluenceMapV1.ApprovedGroup1WeightMInversePerKgPerM3,
                    BulkPoisonInfluenceMapV1.SourceUnitKgPerM3,
                    BulkPoisonInfluenceMapV1.TargetUnitMInverse))
            })
            .ToArray();
        Digest32 mapDigest = BulkPoisonInfluenceMapV1.ComputeDigest(
            1,
            BulkPoisonInfluenceMapV1.ApprovedPoisonSourceId,
            BulkPoisonInfluenceMapV1.ApprovedMapId,
            BulkPoisonInfluenceMapV1.ApprovedTopologyId,
            targetNodes,
            topologyDigest,
            BulkPoisonInfluenceMapV1.ApprovedDataVersion,
            BulkPoisonInfluenceMapV1.ApprovedOwnerId,
            BulkPoisonInfluenceMapV1.ApprovedSignCertificate,
            BulkPoisonInfluenceMapV1.ApprovedNormalization,
            BulkPoisonInfluenceMapV1.SourceUnitKgPerM3,
            BulkPoisonInfluenceMapV1.TargetUnitMInverse,
            BulkPoisonInfluenceMapV1.ApprovedModeratorVolumeM3,
            BulkPoisonInfluenceMapV1.ApprovedReferenceConcentrationKgPerM3,
            BulkPoisonInfluenceMapV1.ApprovedAddRateKgPerSecond,
            BulkPoisonInfluenceMapV1.ApprovedWithdrawRateKgPerSecond,
            BulkPoisonInfluenceMapV1.ApprovedSetupMaximumMassKg,
            referenceStateDigest,
            entries);
        return Require(BulkPoisonInfluenceMapV1.TryCreate(
            1,
            BulkPoisonInfluenceMapV1.ApprovedPoisonSourceId,
            BulkPoisonInfluenceMapV1.ApprovedMapId,
            BulkPoisonInfluenceMapV1.ApprovedTopologyId,
            targetNodes,
            topologyDigest,
            BulkPoisonInfluenceMapV1.ApprovedDataVersion,
            BulkPoisonInfluenceMapV1.ApprovedOwnerId,
            BulkPoisonInfluenceMapV1.ApprovedSignCertificate,
            BulkPoisonInfluenceMapV1.ApprovedNormalization,
            BulkPoisonInfluenceMapV1.SourceUnitKgPerM3,
            BulkPoisonInfluenceMapV1.TargetUnitMInverse,
            BulkPoisonInfluenceMapV1.ApprovedModeratorVolumeM3,
            BulkPoisonInfluenceMapV1.ApprovedReferenceConcentrationKgPerM3,
            BulkPoisonInfluenceMapV1.ApprovedAddRateKgPerSecond,
            BulkPoisonInfluenceMapV1.ApprovedWithdrawRateKgPerSecond,
            BulkPoisonInfluenceMapV1.ApprovedSetupMaximumMassKg,
            referenceStateDigest,
            entries,
            mapDigest));
    }

    private static BulkPoisonStateV1 CreateState(
        BulkPoisonInfluenceMapV1 map,
        BulkPoisonModeV1 mode = BulkPoisonModeV1.Add,
        bool enabled = true,
        double poisonMassKg = 0.5,
        double updateTimeSeconds = 0.0,
        StableId? mapId = null)
    {
        StableId effectiveMapId = mapId ?? map.MapId;
        double concentration = poisonMassKg / map.ModeratorVolumeM3;
        Digest32 stateDigest = BulkPoisonStateV1.ComputeDigest(
            1,
            map.PoisonSourceId,
            enabled,
            mode,
            poisonMassKg,
            map.ModeratorVolumeM3,
            concentration,
            map.ReferenceConcentrationKgPerM3,
            map.AddRateKgPerSecond,
            map.WithdrawRateKgPerSecond,
            effectiveMapId,
            map.DataVersion,
            map.MapDigest,
            updateTimeSeconds);
        return Require(BulkPoisonStateV1.TryCreate(
            1,
            map.PoisonSourceId,
            enabled,
            mode,
            poisonMassKg,
            map.ModeratorVolumeM3,
            map.ReferenceConcentrationKgPerM3,
            map.AddRateKgPerSecond,
            map.WithdrawRateKgPerSecond,
            effectiveMapId,
            map.DataVersion,
            map.MapDigest,
            updateTimeSeconds,
            stateDigest));
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private static string Hex(Digest32 digest)
    {
        return Convert.ToHexString(digest.ToArray()).ToLowerInvariant();
    }

}
