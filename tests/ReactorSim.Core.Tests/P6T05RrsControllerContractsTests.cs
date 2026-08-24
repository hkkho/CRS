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

public sealed class P6T05RrsControllerContractsTests
{
    private readonly ITestOutputHelper _output;

    public P6T05RrsControllerContractsTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ApprovedControllerAndMapAreCanonicalAcrossInputOrder()
    {
        RrsFixture fixture = CreateFixture();
        RrsInfluenceMapV1 reordered = Require(RrsInfluenceMapV1.TryCreate(
            fixture.Map.SchemaVersion,
            fixture.Map.ControllerId,
            fixture.Map.MapId,
            fixture.Map.TopologyId,
            fixture.Map.RegionSetId,
            fixture.Map.TargetNodes.Reverse(),
            fixture.Map.TopologyDigest,
            fixture.Map.RegionSetDigest,
            fixture.Map.DataVersion,
            fixture.Map.MapVersion,
            fixture.Map.OwnerId,
            fixture.Map.SignCertificate,
            fixture.Map.Normalization,
            fixture.Map.SourceUnit,
            fixture.Map.TargetUnit,
            fixture.Map.ControlPolarity,
            fixture.Map.ReferenceActuatorStates.Reverse(),
            fixture.Map.ReferenceStateDigest,
            fixture.Map.Entries.Reverse(),
            fixture.Map.MapDigest));

        Assert.Equal(2, fixture.RegionSet.Regions.Count);
        Assert.Equal(6, fixture.RegionSet.TargetNodes.Count);
        Assert.Equal(24, fixture.Map.Entries.Count);
        Assert.Equal(RrsFixtureV1.FixtureId, fixture.Map.DataVersion == RrsFixtureV1.ApprovedDataVersion
            ? RrsFixtureV1.FixtureId
            : string.Empty);
        Assert.Equal(RrsFixtureV1.OwnerId, fixture.Map.OwnerId);
        Assert.Equal(RrsFixtureV1.SourceUnit, fixture.Map.SourceUnit);
        Assert.Equal(RrsFixtureV1.TargetUnit, fixture.Map.TargetUnit);
        Assert.Equal(fixture.Map.MapDigest, reordered.MapDigest);
        Assert.Equal(fixture.Map.ToCanonicalBytes(), reordered.ToCanonicalBytes());
        Assert.Equal(
            RrsQueueStateV1.ComputeDigest(
                fixture.Queue.SchemaVersion,
                fixture.Queue.QueueId,
                fixture.Queue.OwnerControllerId,
                fixture.Queue.GenerationCadenceOrNA,
                fixture.Queue.InitialNextSequence,
                fixture.Queue.NextSequence,
                fixture.Queue.LastMotionTimeSeconds,
                fixture.Queue.AvailableCommands,
                fixture.Queue.PendingCommands,
                fixture.Queue.AppliedSourceEventIds,
                fixture.Queue.AllocatedCommandIds),
            fixture.Queue.QueueDigest);
        Assert.Equal(
            RrsControllerStateV1.ComputeDigest(
                fixture.State.SchemaVersion,
                fixture.State.ControllerId,
                fixture.State.Mode,
                fixture.State.PowerSetpointWatts,
                fixture.State.MeasuredPowerWatts,
                fixture.State.PowerErrorWatts,
                fixture.State.IntegralErrorWattSeconds,
                fixture.State.RegionSet,
                fixture.State.LeftMeasuredFraction,
                fixture.State.RightMeasuredFraction,
                fixture.State.LeftTiltError,
                fixture.State.RightTiltError,
                fixture.State.LeftTiltIntegralSeconds,
                fixture.State.RightTiltIntegralSeconds,
                fixture.State.AutomaticCadenceSeconds,
                fixture.State.LastUpdateTimeSeconds,
                fixture.State.Actuators,
                fixture.State.InfluenceMapId,
                fixture.State.MapVersion,
                fixture.State.InfluenceMapDigest,
                fixture.State.QueueId,
                fixture.State.ControlPolarity,
                fixture.State.SignCertificateId,
                fixture.State.Normalization,
                fixture.State.FeedbackReferenceStateDigest,
                fixture.State.QueueState),
            fixture.State.StateDigest);

        _output.WriteLine("P6_T05_TOPOLOGY_DIGEST=" + Hex(fixture.RegionSet.TopologyDigest));
        _output.WriteLine("P6_T05_REGION_SET_DIGEST=" + Hex(fixture.RegionSet.RegionSetDigest));
        _output.WriteLine("P6_T05_REFERENCE_STATE_DIGEST=" + Hex(fixture.Map.ReferenceStateDigest));
        _output.WriteLine("P6_T05_MAP_DIGEST=" + Hex(fixture.Map.MapDigest));
        _output.WriteLine("P6_T05_QUEUE_DIGEST=" + Hex(fixture.Queue.QueueDigest));
        _output.WriteLine("P6_T05_CONTROLLER_DIGEST=" + Hex(fixture.State.StateDigest));
        _output.WriteLine("P6_T05_TOPOLOGY_CANONICAL_LENGTH=" + fixture.RegionSet.TargetNodes.Count.ToString(CultureInfo.InvariantCulture));
        _output.WriteLine("P6_T05_REGION_SET_CANONICAL_LENGTH=" + fixture.RegionSet.ToCanonicalBytes().Length.ToString(CultureInfo.InvariantCulture));
        _output.WriteLine("P6_T05_MAP_CANONICAL_LENGTH=" + fixture.Map.ToCanonicalBytes().Length.ToString(CultureInfo.InvariantCulture));
        _output.WriteLine("P6_T05_QUEUE_CANONICAL_LENGTH=" + fixture.Queue.ToCanonicalBytes().Length.ToString(CultureInfo.InvariantCulture));
        _output.WriteLine("P6_T05_CONTROLLER_CANONICAL_LENGTH=" + fixture.State.ToCanonicalBytes().Length.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void AutomaticProjectionMatchesApprovedVectorAndDoesNotMutateInputs()
    {
        RrsFixture fixture = CreateFixture();
        RrsMeasurementSnapshotV1 measurement = Require(
            RrsMeasurementSnapshotV1.TryCreate(
                fixture.RegionSet,
                1.0,
                800.0,
                480.0,
                320.0));
        byte[] stateBefore = fixture.State.ToCanonicalBytes();
        byte[] queueBefore = fixture.Queue.ToCanonicalBytes();
        byte[] mapBefore = fixture.Map.ToCanonicalBytes();

        RrsControllerProjectionV1 projection = Require(
            RrsControllerProjectionV1.TryProjectAutomatic(
                fixture.State,
                measurement,
                1.0));

        Assert.Equal(200.0, projection.PowerErrorWatts);
        Assert.Equal(200.0, projection.IntegralAfterWattSeconds);
        Assert.Equal(-0.1, projection.LeftTiltError, 12);
        Assert.Equal(0.1, projection.RightTiltError, 12);
        Assert.Equal(-0.1, projection.LeftTiltIntegralAfterSeconds, 12);
        Assert.Equal(0.1, projection.RightTiltIntegralAfterSeconds, 12);
        Assert.Equal(2, projection.Commands.Count);

        RrsCommandProjectionV1 total = projection.Commands.Single(command =>
            command.ActuatorId == RrsFixtureV1.TotalPowerActuatorId);
        RrsCommandProjectionV1 tilt = projection.Commands.Single(command =>
            command.ActuatorId == RrsFixtureV1.TiltActuatorId);
        Assert.Equal(0.72, total.RequestedCommand, 12);
        Assert.Equal(0.72, total.BoundedCommand, 12);
        Assert.Equal(0.61, tilt.RequestedCommand, 12);
        Assert.Equal(0.61, tilt.BoundedCommand, 12);
        Assert.All(projection.Commands, command =>
        {
            Assert.InRange(command.BoundedCommand, 0.0, 1.0);
            Assert.Equal(0.1, command.RateLimitPerSecond);
            Assert.Equal(0.5, command.DelaySeconds);
        });
        Assert.Equal(stateBefore, fixture.State.ToCanonicalBytes());
        Assert.Equal(queueBefore, fixture.Queue.ToCanonicalBytes());
        Assert.Equal(mapBefore, fixture.Map.ToCanonicalBytes());

        _output.WriteLine("P6_T05_MEASUREMENT_DIGEST=" + Hex(measurement.SnapshotDigest));
        _output.WriteLine("P6_T05_PROJECTION_DIGEST=" + Hex(projection.ProjectionDigest));
        _output.WriteLine("P6_T05_CONTROLLER_DIGEST=" + Hex(fixture.State.StateDigest));
        _output.WriteLine("P6_T05_QUEUE_DIGEST=" + Hex(fixture.Queue.QueueDigest));
    }

    [Fact]
    public void ManualProjectionIsExplicitAndDoesNotAdvanceIntegralOrTime()
    {
        RrsFixture fixture = CreateFixture(RrsModeV1.Manual);
        RrsControllerProjectionV1 projection = Require(
            RrsControllerProjectionV1.TryProjectManual(
                fixture.State,
                RrsFixtureV1.TotalPowerActuatorId,
                0.65,
                2.0));

        Assert.Equal(RrsModeV1.Manual, projection.Mode);
        Assert.Single(projection.Commands);
        Assert.Equal(0.65, projection.Commands[0].RequestedCommand);
        Assert.Equal(0.65, projection.Commands[0].BoundedCommand);
        Assert.Equal(0.0, projection.DeltaTimeSeconds);
        Assert.Equal(0.0, projection.IntegralAfterWattSeconds);
        Assert.Equal(0.0, projection.LeftTiltIntegralAfterSeconds);
        Assert.Equal(0.0, projection.RightTiltIntegralAfterSeconds);
        Assert.Equal(0.0, projection.LastUpdateAfterSeconds);

        ContractValidationResult<RrsControllerProjectionV1> implicitTransition =
            RrsControllerProjectionV1.TryProjectAutomatic(
                fixture.State,
                Require(RrsMeasurementSnapshotV1.TryCreate(
                    fixture.RegionSet,
                    2.0,
                    800.0,
                    480.0,
                    320.0)),
                2.0);
        Assert.False(implicitTransition.IsValid);
        Assert.Equal("RrsProjection.Mode.NotAutomatic", implicitTransition.FirstDiagnostic.Code);
    }

    [Fact]
    public void HeldProjectionKeepsCommandsIntegralsAndLastUpdateUnchanged()
    {
        RrsFixture fixture = CreateFixture(RrsModeV1.Held);
        RrsControllerProjectionV1 projection = Require(
            RrsControllerProjectionV1.TryProjectHeld(fixture.State, 4.0));

        Assert.Equal(RrsModeV1.Held, projection.Mode);
        Assert.Equal(2, projection.Commands.Count);
        Assert.All(projection.Commands, command => Assert.Equal(0.5, command.BoundedCommand));
        Assert.Equal(0.0, projection.IntegralAfterWattSeconds);
        Assert.Equal(0.0, projection.LeftTiltIntegralAfterSeconds);
        Assert.Equal(0.0, projection.RightTiltIntegralAfterSeconds);
        Assert.Equal(0.0, projection.LastUpdateAfterSeconds);
    }

    [Fact]
    public void OverlayUsesReferenceZeroAndApprovedSignedLocalAbsorption()
    {
        RrsFixture fixture = CreateFixture();
        RrsActuatorSnapshotV1 totalReference = Require(
            RrsActuatorSnapshotV1.TryCreate(
                RrsFixtureV1.TotalPowerActuatorId,
                0.5,
                0.5));
        RrsActuatorSnapshotV1 tiltReference = Require(
            RrsActuatorSnapshotV1.TryCreate(
                RrsFixtureV1.TiltActuatorId,
                0.5,
                0.5));
        RrsOverlayEvaluationV1 reference = Require(
            RrsOverlayV1.TryEvaluate(fixture.Map, new[] { totalReference, tiltReference }));
        Assert.Equal(12, reference.Values.Count);
        Assert.All(reference.Values, value => Assert.Equal(0.0, value.DeltaSigmaAMInverse));

        RrsActuatorSnapshotV1 totalRaised = Require(
            RrsActuatorSnapshotV1.TryCreate(
                RrsFixtureV1.TotalPowerActuatorId,
                0.5,
                0.7));
        RrsActuatorSnapshotV1 tiltRaised = Require(
            RrsActuatorSnapshotV1.TryCreate(
                RrsFixtureV1.TiltActuatorId,
                0.5,
                0.6));
        RrsOverlayEvaluationV1 overlay = Require(
            RrsOverlayV1.TryEvaluate(fixture.Map, new[] { totalRaised, tiltRaised }));
        Assert.Equal(-0.003, RequireValue(overlay, new NodeKey(new ChannelId(0), new BundlePosition(0)), 0).DeltaSigmaAMInverse, 12);
        Assert.Equal(-0.005, RequireValue(overlay, new NodeKey(new ChannelId(3), new BundlePosition(0)), 0).DeltaSigmaAMInverse, 12);
        Assert.Equal(-0.0014, RequireValue(overlay, new NodeKey(new ChannelId(0), new BundlePosition(0)), 1).DeltaSigmaAMInverse, 12);
        Assert.Equal(-0.0026, RequireValue(overlay, new NodeKey(new ChannelId(3), new BundlePosition(0)), 1).DeltaSigmaAMInverse, 12);

        RrsOverlayEvaluationV1 disabled = Require(
            RrsOverlayV1.TryEvaluate(
                fixture.Map,
                new[] { totalRaised, tiltRaised },
                enabled: false));
        Assert.True(disabled.DisabledZeroAssertion);
        Assert.All(disabled.Values, value => Assert.Equal(0.0, value.DeltaSigmaAMInverse));
        Assert.Equal(fixture.Map.MapDigest, overlay.MapDigest);
        Assert.Equal(fixture.Map.ReferenceStateDigest, overlay.ReferenceStateDigest);
        Assert.Equal(reference.OverlayDigest, Require(
            RrsOverlayV1.TryEvaluate(fixture.Map, new[] { totalReference, tiltReference })).OverlayDigest);

        _output.WriteLine("P6_T05_REFERENCE_STATE_DIGEST=" + Hex(fixture.Map.ReferenceStateDigest));
        _output.WriteLine("P6_T05_MAP_DIGEST=" + Hex(fixture.Map.MapDigest));
        _output.WriteLine("P6_T05_REFERENCE_OVERLAY_DIGEST=" + Hex(reference.OverlayDigest));
        _output.WriteLine("P6_T05_OVERLAY_DIGEST=" + Hex(overlay.OverlayDigest));
    }

    [Fact]
    public void InvalidUnitsSignsRegionsDigestsAndBindingsFailClosed()
    {
        RrsFixture fixture = CreateFixture();
        ContractValidationResult<RrsInfluenceMapEntryV1> wrongWeight =
            RrsInfluenceMapEntryV1.TryCreate(
                RrsFixtureV1.TotalPowerActuatorId,
                fixture.Map.TargetNodes[0],
                0,
                0.02,
                RrsFixtureV1.SourceUnit,
                RrsFixtureV1.TargetUnit,
                0.5,
                RrsFixtureV1.ApprovedMapVersion,
                fixture.Map.ReferenceStateDigest);
        Assert.False(wrongWeight.IsValid);
        Assert.Equal("RrsInfluenceMapEntry.Weight.Unapproved", wrongWeight.FirstDiagnostic.Code);

        ContractValidationResult<RrsQueueStateV1> missingCadence =
            RrsQueueStateV1.TryCreate(
                fixture.Queue.SchemaVersion,
                fixture.Queue.QueueId,
                fixture.Queue.OwnerControllerId,
                null,
                fixture.Queue.InitialNextSequence,
                fixture.Queue.NextSequence,
                fixture.Queue.LastMotionTimeSeconds,
                fixture.Queue.AvailableCommands,
                fixture.Queue.PendingCommands,
                fixture.Queue.AppliedSourceEventIds,
                fixture.Queue.AllocatedCommandIds,
                fixture.Queue.QueueDigest);
        Assert.False(missingCadence.IsValid);
        Assert.Equal("RrsQueue.GenerationCadence.Unapproved", missingCadence.FirstDiagnostic.Code);

        ContractValidationResult<RrsQueueStateV1> nonzeroSequence =
            RrsQueueStateV1.TryCreate(
                fixture.Queue.SchemaVersion,
                fixture.Queue.QueueId,
                fixture.Queue.OwnerControllerId,
                fixture.Queue.GenerationCadenceOrNA,
                1,
                1,
                fixture.Queue.LastMotionTimeSeconds,
                fixture.Queue.AvailableCommands,
                fixture.Queue.PendingCommands,
                fixture.Queue.AppliedSourceEventIds,
                fixture.Queue.AllocatedCommandIds,
                fixture.Queue.QueueDigest);
        Assert.False(nonzeroSequence.IsValid);
        Assert.Equal("RrsQueue.Sequence.Invalid", nonzeroSequence.FirstDiagnostic.Code);

        ContractValidationResult<RrsInfluenceMapV1> wrongRegionSetDigest =
            RrsInfluenceMapV1.TryCreate(
                fixture.Map.SchemaVersion,
                fixture.Map.ControllerId,
                fixture.Map.MapId,
                fixture.Map.TopologyId,
                fixture.Map.RegionSetId,
                fixture.Map.TargetNodes,
                fixture.Map.TopologyDigest,
                new Digest32(new byte[32]),
                fixture.Map.DataVersion,
                fixture.Map.MapVersion,
                fixture.Map.OwnerId,
                fixture.Map.SignCertificate,
                fixture.Map.Normalization,
                fixture.Map.SourceUnit,
                fixture.Map.TargetUnit,
                fixture.Map.ControlPolarity,
                fixture.Map.ReferenceActuatorStates,
                fixture.Map.ReferenceStateDigest,
                fixture.Map.Entries,
                fixture.Map.MapDigest);
        Assert.False(wrongRegionSetDigest.IsValid);
        Assert.Equal("RrsInfluenceMap.RegionSetDigest.Unapproved", wrongRegionSetDigest.FirstDiagnostic.Code);

        ContractValidationResult<RrsMeasurementSnapshotV1> staleTotal =
            RrsMeasurementSnapshotV1.TryCreate(
                fixture.RegionSet,
                1.0,
                0.0,
                0.0,
                0.0);
        Assert.False(staleTotal.IsValid);
        Assert.Equal("RrsMeasurement.Values.Invalid", staleTotal.FirstDiagnostic.Code);

        ContractValidationResult<RrsControllerProjectionV1> wrongCadence =
            RrsControllerProjectionV1.TryProjectAutomatic(
                fixture.State,
                Require(RrsMeasurementSnapshotV1.TryCreate(
                    fixture.RegionSet,
                    2.0,
                    800.0,
                    480.0,
                    320.0)),
                2.0);
        Assert.False(wrongCadence.IsValid);
        Assert.Equal("RrsProjection.Cadence.Mismatch", wrongCadence.FirstDiagnostic.Code);

        ContractValidationResult<RrsInfluenceMapV1> wrongMapDigest =
            RrsInfluenceMapV1.TryCreate(
                fixture.Map.SchemaVersion,
                fixture.Map.ControllerId,
                fixture.Map.MapId,
                fixture.Map.TopologyId,
                fixture.Map.RegionSetId,
                fixture.Map.TargetNodes,
                fixture.Map.TopologyDigest,
                fixture.Map.RegionSetDigest,
                fixture.Map.DataVersion,
                fixture.Map.MapVersion,
                fixture.Map.OwnerId,
                fixture.Map.SignCertificate,
                fixture.Map.Normalization,
                fixture.Map.SourceUnit,
                fixture.Map.TargetUnit,
                fixture.Map.ControlPolarity,
                fixture.Map.ReferenceActuatorStates,
                fixture.Map.ReferenceStateDigest,
                fixture.Map.Entries,
                new Digest32(new byte[32]));
        Assert.False(wrongMapDigest.IsValid);
        Assert.Equal("RrsInfluenceMap.MapDigest.Mismatch", wrongMapDigest.FirstDiagnostic.Code);

        ContractValidationResult<RrsControllerStateV1> wrongControllerMapDigest =
            RrsControllerStateV1.TryCreate(
                fixture.State.SchemaVersion,
                fixture.State.ControllerId,
                fixture.State.Mode,
                fixture.State.PowerSetpointWatts,
                fixture.State.MeasuredPowerWatts,
                fixture.State.PowerErrorWatts,
                fixture.State.IntegralErrorWattSeconds,
                fixture.State.RegionSet,
                fixture.State.LeftMeasuredFraction,
                fixture.State.RightMeasuredFraction,
                fixture.State.LeftTiltError,
                fixture.State.RightTiltError,
                fixture.State.LeftTiltIntegralSeconds,
                fixture.State.RightTiltIntegralSeconds,
                fixture.State.AutomaticCadenceSeconds,
                fixture.State.LastUpdateTimeSeconds,
                fixture.State.Actuators,
                fixture.State.InfluenceMapId,
                fixture.State.MapVersion,
                new Digest32(new byte[32]),
                fixture.State.QueueId,
                fixture.State.ControlPolarity,
                fixture.State.SignCertificateId,
                fixture.State.Normalization,
                fixture.State.FeedbackReferenceStateDigest,
                fixture.State.QueueState,
                fixture.State.StateDigest);
        Assert.False(wrongControllerMapDigest.IsValid);
        Assert.Equal("RrsController.BindingDigest.Unapproved", wrongControllerMapDigest.FirstDiagnostic.Code);

        ContractValidationResult<RrsOverlayEvaluationV1> missingSnapshots =
            RrsOverlayV1.TryEvaluate(fixture.Map, null);
        Assert.False(missingSnapshots.IsValid);
        Assert.Equal("RrsOverlay.ActuatorSnapshots.Missing", missingSnapshots.FirstDiagnostic.Code);
    }

    [Fact]
    public void PackageAndManifestBindTheTypedApprovedFixtureAndArtifactHash()
    {
        RrsFixture fixture = CreateFixture();
        string packagePath = Path.Combine(
            RepositoryRoot(),
            "data",
            "packs",
            "p6-t05-synthetic-rrs-controller-map-v1.json");
        string manifestPath = Path.Combine(
            RepositoryRoot(),
            "data",
            "packs",
            "p6-t05-synthetic-rrs-controller-map-v1.manifest.json");
        Assert.True(File.Exists(packagePath));
        Assert.True(File.Exists(manifestPath));

        byte[] packageBytes = File.ReadAllBytes(packagePath);
        string packageHash = Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant();
        using JsonDocument packageDocument = JsonDocument.Parse(packageBytes);
        using JsonDocument manifestDocument = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        JsonElement package = packageDocument.RootElement;
        JsonElement manifest = manifestDocument.RootElement;

        Assert.Equal("CANDU-RRS-INFLUENCE-MAP-PACKAGE-V1", package.GetProperty("schema_id").GetString());
        Assert.Equal(RrsFixtureV1.FixtureId, package.GetProperty("fixture_id").GetString());
        Assert.Equal("SYNTHETIC_TEST_ONLY", package.GetProperty("status").GetString());
        Assert.Equal("Kevin Ho", package.GetProperty("owner_id").GetString());
        Assert.Equal("2026-08-20", package.GetProperty("approval_date").GetString());
        Assert.Equal(
            "docs/tasks/P6-T05-OWNER-APPROVAL.md",
            package.GetProperty("approval_record").GetString());
        Assert.Equal(
            Hex(fixture.Map.MapDigest),
            package.GetProperty("digests").GetProperty("map_digest").GetString());
        Assert.Equal(
            Hex(fixture.State.StateDigest),
            package.GetProperty("digests").GetProperty("controller_digest").GetString());
        Assert.Equal(
            Hex(fixture.Queue.QueueDigest),
            package.GetProperty("supplied_queue").GetProperty("queue_digest").GetString());
        Assert.Equal(6, package.GetProperty("topology").GetProperty("ordered_target_nodes").GetArrayLength());
        Assert.Equal(2, package.GetProperty("regions").GetProperty("ordered_regions").GetArrayLength());
        Assert.Equal(24, package.GetProperty("entries").GetArrayLength());
        Assert.Equal(1000.0, package.GetProperty("controller_reference").GetProperty("power_setpoint_w").GetDouble());
        Assert.Equal(0.72, package.GetProperty("deterministic_check_vector").GetProperty("automatic_projection").GetProperty("total_power_requested_command").GetDouble());
        Assert.Equal(0.61, package.GetProperty("deterministic_check_vector").GetProperty("automatic_projection").GetProperty("tilt_requested_command").GetDouble());
        Assert.Equal(0.65, package.GetProperty("deterministic_check_vector").GetProperty("manual_projection").GetProperty("requested_command").GetDouble());
        Assert.Equal("RESERVED_P6-T06", package.GetProperty("boundary").GetProperty("queue_allocation").GetString());
        Assert.Equal("RESERVED_P6-T07", package.GetProperty("boundary").GetProperty("scenario_packages").GetString());

        JsonElement packageEntries = package.GetProperty("entries");
        for (int index = 0; index < fixture.Map.Entries.Count; index++)
        {
            RrsInfluenceMapEntryV1 typedEntry = fixture.Map.Entries[index];
            JsonElement packageEntry = packageEntries[index];
            JsonElement target = packageEntry.GetProperty("target_node");
            Assert.Equal(typedEntry.ActuatorId.ToString(), packageEntry.GetProperty("actuator_id").GetString());
            Assert.Equal(typedEntry.TargetNode.ChannelId.Value, target.GetProperty("channel_id").GetUInt32());
            Assert.Equal(typedEntry.TargetNode.Position.Value, target.GetProperty("bundle_position").GetUInt32());
            Assert.Equal(typedEntry.GroupIndex, packageEntry.GetProperty("group_index").GetUInt16());
            Assert.Equal(typedEntry.WeightMInversePerActuatorUnit, packageEntry.GetProperty("weight_m_inverse_per_1").GetDouble());
            Assert.Equal(typedEntry.SourceUnit, packageEntry.GetProperty("source_unit").GetString());
            Assert.Equal(typedEntry.TargetUnit, packageEntry.GetProperty("target_unit").GetString());
        }

        Assert.Equal("CANDU-SYNTHETIC-PACK-MANIFEST-V1", manifest.GetProperty("manifest_schema_id").GetString());
        Assert.Equal(RrsFixtureV1.FixtureId, manifest.GetProperty("fixture_id").GetString());
        Assert.Equal(packageBytes.Length, manifest.GetProperty("artifact").GetProperty("byte_length").GetInt64());
        Assert.Equal(packageHash, manifest.GetProperty("artifact").GetProperty("sha256").GetString());
        Assert.Equal("91b5a5ec140217152557667e863a13da8c8359c1c90d39e463032ff3a6cf771f", packageHash);
        Assert.Equal(
            Hex(fixture.Map.ReferenceStateDigest),
            manifest.GetProperty("digests").GetProperty("reference_state_digest").GetString());

        _output.WriteLine("P6_T05_PACKAGE_BYTE_LENGTH=" + packageBytes.Length.ToString(CultureInfo.InvariantCulture));
        _output.WriteLine("P6_T05_PACKAGE_SHA256=" + packageHash);
    }

    private static RrsOverlayValueV1 RequireValue(
        RrsOverlayEvaluationV1 evaluation,
        NodeKey node,
        ushort groupIndex)
    {
        RrsOverlayValueV1? value = evaluation.Find(node, groupIndex);
        Assert.NotNull(value);
        return value!;
    }

    private static RrsFixture CreateFixture(RrsModeV1 mode = RrsModeV1.Automatic)
    {
        RrsRegionSetV1 regionSet = CreateRegionSet();
        KeyValuePair<StableId, double>[] references =
        {
            new KeyValuePair<StableId, double>(RrsFixtureV1.TotalPowerActuatorId, 0.5),
            new KeyValuePair<StableId, double>(RrsFixtureV1.TiltActuatorId, 0.5)
        };
        Digest32 referenceDigest = RrsInfluenceMapV1.ComputeReferenceStateDigest(
            RrsFixtureV1.ControllerId,
            RrsFixtureV1.MapId,
            RrsFixtureV1.TopologyId,
            RrsFixtureV1.RegionSetId,
            regionSet.TopologyDigest,
            RrsFixtureV1.ApprovedDataVersion,
            RrsFixtureV1.ApprovedMapVersion,
            RrsFixtureV1.OwnerId,
            RrsFixtureV1.SignCertificate,
            RrsFixtureV1.Normalization,
            RrsFixtureV1.SourceUnit,
            RrsFixtureV1.TargetUnit,
            RrsControlPolarityV1.NegativeFeedback,
            references);
        List<RrsInfluenceMapEntryV1> entries = new List<RrsInfluenceMapEntryV1>();
        foreach (StableId actuatorId in new[]
        {
            RrsFixtureV1.TotalPowerActuatorId,
            RrsFixtureV1.TiltActuatorId
        })
        {
            foreach (NodeKey node in regionSet.TargetNodes)
            {
                for (ushort groupIndex = 0; groupIndex < RrsFixtureV1.GroupCount; groupIndex++)
                {
                    entries.Add(Require(RrsInfluenceMapEntryV1.TryCreate(
                        actuatorId,
                        node,
                        groupIndex,
                        RrsInfluenceMapEntryV1.ExpectedWeight(actuatorId, node, groupIndex),
                        RrsFixtureV1.SourceUnit,
                        RrsFixtureV1.TargetUnit,
                        0.5,
                        RrsFixtureV1.ApprovedMapVersion,
                        referenceDigest)));
                }
            }
        }
        Digest32 mapDigest = RrsInfluenceMapV1.ComputeMapDigest(
            1,
            RrsFixtureV1.ControllerId,
            RrsFixtureV1.MapId,
            RrsFixtureV1.TopologyId,
            RrsFixtureV1.RegionSetId,
            regionSet.TargetNodes,
            regionSet.TopologyDigest,
            regionSet.RegionSetDigest,
            RrsFixtureV1.ApprovedDataVersion,
            RrsFixtureV1.ApprovedMapVersion,
            RrsFixtureV1.OwnerId,
            RrsFixtureV1.SignCertificate,
            RrsFixtureV1.Normalization,
            RrsFixtureV1.SourceUnit,
            RrsFixtureV1.TargetUnit,
            RrsControlPolarityV1.NegativeFeedback,
            references,
            referenceDigest,
            entries);
        RrsInfluenceMapV1 map = Require(RrsInfluenceMapV1.TryCreate(
            1,
            RrsFixtureV1.ControllerId,
            RrsFixtureV1.MapId,
            RrsFixtureV1.TopologyId,
            RrsFixtureV1.RegionSetId,
            regionSet.TargetNodes,
            regionSet.TopologyDigest,
            regionSet.RegionSetDigest,
            RrsFixtureV1.ApprovedDataVersion,
            RrsFixtureV1.ApprovedMapVersion,
            RrsFixtureV1.OwnerId,
            RrsFixtureV1.SignCertificate,
            RrsFixtureV1.Normalization,
            RrsFixtureV1.SourceUnit,
            RrsFixtureV1.TargetUnit,
            RrsControlPolarityV1.NegativeFeedback,
            references,
            referenceDigest,
            entries,
            mapDigest));

        RrsAvailableCommandV1[] available =
        {
            Require(RrsAvailableCommandV1.TryCreate(
                RrsFixtureV1.TotalPowerActuatorId,
                0.5,
                0.0,
                1.0,
                0.1)),
            Require(RrsAvailableCommandV1.TryCreate(
                RrsFixtureV1.TiltActuatorId,
                0.5,
                0.0,
                1.0,
                0.1))
        };
        Digest32 queueDigest = RrsQueueStateV1.ComputeDigest(
            1,
            RrsFixtureV1.QueueId,
            RrsFixtureV1.ControllerId,
            1.0,
            0,
            0,
            0.0,
            available,
            Array.Empty<RrsPendingCommandV1>(),
            Array.Empty<StableId>(),
            Array.Empty<StableId>());
        RrsQueueStateV1 queue = Require(RrsQueueStateV1.TryCreate(
            1,
            RrsFixtureV1.QueueId,
            RrsFixtureV1.ControllerId,
            1.0,
            0,
            0,
            0.0,
            available,
            Array.Empty<RrsPendingCommandV1>(),
            Array.Empty<StableId>(),
            Array.Empty<StableId>(),
            queueDigest));

        RrsActuatorStateV1[] actuators =
        {
            Require(RrsActuatorStateV1.TryCreate(
                RrsFixtureV1.TotalPowerActuatorId,
                0.5,
                0.001,
                0.0001,
                0.0,
                0.0,
                0.0,
                0.0,
                0.0,
                1.0,
                0.1,
                0.5,
                0.5,
                0.5,
                0.5,
                0.5)),
            Require(RrsActuatorStateV1.TryCreate(
                RrsFixtureV1.TiltActuatorId,
                0.5,
                0.0,
                0.0,
                -0.5,
                0.5,
                -0.05,
                0.05,
                0.0,
                1.0,
                0.1,
                0.5,
                0.5,
                0.5,
                0.5,
                0.5))
        };
        Digest32 stateDigest = RrsControllerStateV1.ComputeDigest(
            1,
            RrsFixtureV1.ControllerId,
            mode,
            1000.0,
            1000.0,
            0.0,
            0.0,
            regionSet,
            0.5,
            0.5,
            0.0,
            0.0,
            0.0,
            0.0,
            1.0,
            0.0,
            actuators,
            RrsFixtureV1.MapId,
            RrsFixtureV1.ApprovedMapVersion,
            map.MapDigest,
            RrsFixtureV1.QueueId,
            RrsControlPolarityV1.NegativeFeedback,
            RrsFixtureV1.SignCertificate,
            RrsFixtureV1.Normalization,
            referenceDigest,
            queue);
        RrsControllerStateV1 state = Require(RrsControllerStateV1.TryCreate(
            1,
            RrsFixtureV1.ControllerId,
            mode,
            1000.0,
            1000.0,
            0.0,
            0.0,
            regionSet,
            0.5,
            0.5,
            0.0,
            0.0,
            0.0,
            0.0,
            1.0,
            0.0,
            actuators,
            RrsFixtureV1.MapId,
            RrsFixtureV1.ApprovedMapVersion,
            map.MapDigest,
            RrsFixtureV1.QueueId,
            RrsControlPolarityV1.NegativeFeedback,
            RrsFixtureV1.SignCertificate,
            RrsFixtureV1.Normalization,
            referenceDigest,
            queue,
            stateDigest));

        return new RrsFixture(regionSet, map, queue, state);
    }

    private static RrsRegionSetV1 CreateRegionSet()
    {
        NodeKey[] targets = RrsFixtureV1.TargetNodes();
        Digest32 topologyDigest = RrsRegionSetV1.ComputeTopologyDigest(
            RrsFixtureV1.TopologyId,
            targets);
        RrsRegionDefinitionV1 left = Require(RrsRegionDefinitionV1.TryCreate(
            RrsFixtureV1.LeftRegionId,
            0.5,
            RrsFixtureV1.LeftNodes()));
        RrsRegionDefinitionV1 right = Require(RrsRegionDefinitionV1.TryCreate(
            RrsFixtureV1.RightRegionId,
            0.5,
            RrsFixtureV1.RightNodes()));
        Digest32 regionSetDigest = RrsRegionSetV1.ComputeRegionSetDigest(
            RrsFixtureV1.RegionSetId,
            RrsFixtureV1.TopologyId,
            targets,
            topologyDigest,
            new[] { left, right });
        return Require(RrsRegionSetV1.TryCreate(
            1,
            RrsFixtureV1.RegionSetId,
            RrsFixtureV1.TopologyId,
            targets,
            topologyDigest,
            new[] { left, right },
            regionSetDigest));
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

    private static string RepositoryRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(directory))
        {
            if (File.Exists(Path.Combine(directory, "ReactorSim.sln")))
            {
                return directory;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new DirectoryNotFoundException("The repository root could not be located from the test base directory.");
    }

    private sealed class RrsFixture
    {
        public RrsFixture(
            RrsRegionSetV1 regionSet,
            RrsInfluenceMapV1 map,
            RrsQueueStateV1 queue,
            RrsControllerStateV1 state)
        {
            RegionSet = regionSet;
            Map = map;
            Queue = queue;
            State = state;
        }

        public RrsRegionSetV1 RegionSet { get; }

        public RrsInfluenceMapV1 Map { get; }

        public RrsQueueStateV1 Queue { get; }

        public RrsControllerStateV1 State { get; }
    }
}
