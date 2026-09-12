using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReactorSim.Core;
using ReactorSim.TestInfrastructure;
using Xunit;

namespace ReactorSim.Core.Tests;

public sealed class P6T07ScenarioEvidenceTests
{
    private static readonly double[] TimestepOneStep = { 2.0 };
    private static readonly double[] TimestepTwoSteps = { 1.0, 1.0 };
    private static readonly string[] ScenarioIds =
    {
        "centered-perturbation",
        "regional-tilt",
        "zone-saturation",
        "adjuster-insertion-withdrawal",
        "poison-recovery"
    };

    private readonly ITestOutputHelper _output;

    public P6T07ScenarioEvidenceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ScenarioPackageBindsApprovedInputsAndExplicitBoundaries()
    {
        string packagePath = TestDataLocator.RequireRepositoryFile(
            "data/scenarios/p6-t07-synthetic-rrs-scenarios-v1.json");
        string manifestPath = packagePath.Replace(
            ".json",
            ".manifest.json",
            StringComparison.Ordinal);

        Assert.True(File.Exists(packagePath));
        Assert.True(File.Exists(manifestPath));

        byte[] packageBytes = File.ReadAllBytes(packagePath);
        using JsonDocument package = JsonDocument.Parse(packageBytes);
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));

        Assert.Equal(
            "CANDU-P6-T07-SCENARIO-PACKAGE-V1",
            package.RootElement.GetProperty("schema_id").GetString());
        Assert.Equal(
            "P6-T07-SYNTHETIC-RRS-SCENARIOS-V1",
            package.RootElement.GetProperty("fixture_id").GetString());
        Assert.Equal(5, package.RootElement.GetProperty("scenarios").GetArrayLength());
        Assert.Equal(
            "P6-T07-SYNTHETIC-RRS-SCENARIOS-V1",
            manifest.RootElement.GetProperty("fixture_id").GetString());
        Assert.Equal(
            packageBytes.Length,
            manifest.RootElement.GetProperty("artifact").GetProperty("byte_length").GetInt64());
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant(),
            manifest.RootElement.GetProperty("artifact").GetProperty("sha256").GetString());
        Assert.Equal(
            "RESERVED_G6",
            package.RootElement.GetProperty("boundary").GetProperty("approved_comparisons").GetString());
        Assert.Equal(
            "FORBIDDEN",
            package.RootElement.GetProperty("boundary").GetProperty("safety_behavior").GetString());
        Assert.Equal(
            ScenarioIds,
            package.RootElement
                .GetProperty("scenarios")
                .EnumerateArray()
                .Select(scenario => scenario.GetProperty("scenario_id").GetString())
                .ToArray());
        ValidateInputArtifacts(package.RootElement, manifest.RootElement);

        _output.WriteLine("P6_T07_SCENARIO_PACKAGE_BYTES=" + packageBytes.Length.ToString(CultureInfo.InvariantCulture));
        _output.WriteLine("P6_T07_SCENARIO_PACKAGE_SHA256=" + Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant());
    }

    [Fact]
    public void CenteredPerturbationIsCausalSignedAndReplayDeterministic()
    {
        JsonElement scenario = LoadScenario("centered-perturbation");
        ScenarioTrace first = RunCenteredPerturbation();
        ScenarioTrace replay = RunCenteredPerturbation();

        Assert.Equal(first.Bytes, replay.Bytes);
        Assert.Equal(first.Digest, replay.Digest);
        Assert.Equal(ScenarioDouble(scenario, "expected_controller_commands", "total_power"), first.TotalCommand, 12);
        Assert.Equal(ScenarioDouble(scenario, "expected_controller_commands", "tilt"), first.TiltCommand, 12);
        Assert.Equal(ScenarioDouble(scenario, "expected_physical_state_before_due"), first.PhysicalStateBeforeDue, 12);
        Assert.Equal(ScenarioDouble(scenario, "expected_physical_state_after_due"), first.PhysicalStateAfterDue, 12);
        Assert.True(first.TotalOverlay < 0.0);

        WriteTrace("centered-perturbation", first);
    }

    [Fact]
    public void ControllerProjectionAdmissionPreservesPreRefactorQueueBytes()
    {
        RrsControllerAdmissionFixture admission = CreateCenteredAdmissionFixture(
            1.0,
            800.0,
            0.6,
            0.4,
            1.0,
            0.1,
            Digest(0x61));
        byte[] queueBefore = admission.Queue.ToCanonicalBytes();
        P6T06CommandCandidateV1[] preRefactorCandidates = admission.Projection.Commands
            .Select(command => Require(P6T06CommandCandidateV1.TryCreate(
                P6T06QueueFamilyV1.Rrs,
                Require(P6T06TargetKeyV1.TryForRrsActuator(command.ActuatorId)),
                P6T06SourceKindV1.Controller,
                EventRankV1.ControllerCommandGeneration,
                command.DelaySeconds,
                command.RequestedCommand,
                command.LowerBound,
                command.UpperBound,
                command.RateLimitPerSecond)))
            .ToArray();
        P6T06EnqueueResultV1 preRefactor = Require(admission.Queue.TryEnqueueBatch(
            Require(P6T06SourceBindingTokenV1.TryCreate(
                admission.EventIdentity.SourceEventId,
                admission.EventIdentity.SourceBindingDigest)),
            admission.Projection.CurrentTimeSeconds,
            Require(admission.Queue.TryCreatePhaseToken(admission.Projection.CurrentTimeSeconds)),
            preRefactorCandidates));

        P6T06EnqueueResultV1 integrated = Require(
            admission.Queue.TryEnqueueRrsControllerProjection(
                admission.Projection,
                admission.EventIdentity,
                Require(admission.Queue.TryCreatePhaseToken(admission.Projection.CurrentTimeSeconds))));

        Assert.Equal(queueBefore, admission.Queue.ToCanonicalBytes());
        Assert.Equal(preRefactor.Queue.ToCanonicalBytes(), integrated.Queue.ToCanonicalBytes());
        Assert.Equal(preRefactor.Transition.ToCanonicalBytes(), integrated.Transition.ToCanonicalBytes());
        Assert.Equal(
            preRefactor.Commands.Select(command => command.ToCanonicalBytes()),
            integrated.Commands.Select(command => command.ToCanonicalBytes()));
        Assert.Equal(preRefactor.Queue.QueueDigest, integrated.Queue.QueueDigest);
        Assert.Equal(
            preRefactor.Commands.Select(command => command.CommandDigest),
            integrated.Commands.Select(command => command.CommandDigest));
    }

    [Fact]
    public void ControllerProjectionAdmissionRejectsDigestMismatchBeforeQueueMutation()
    {
        RrsControllerAdmissionFixture admission = CreateCenteredAdmissionFixture(
            1.0,
            800.0,
            0.6,
            0.4,
            1.0,
            0.1,
            Digest(0x61));
        P6T06RrsControllerEventIdentityV1 mismatchedIdentity = Require(
            P6T06RrsControllerEventIdentityV1.TryCreate(
                admission.EventIdentity.ControllerId,
                admission.EventIdentity.SourceEventId,
                admission.EventIdentity.SourceBindingDigest,
                Digest(0x62)));
        byte[] queueBefore = admission.Queue.ToCanonicalBytes();

        ContractValidationResult<P6T06EnqueueResultV1> result =
            admission.Queue.TryEnqueueRrsControllerProjection(
                admission.Projection,
                mismatchedIdentity,
                Require(admission.Queue.TryCreatePhaseToken(admission.Projection.CurrentTimeSeconds)));

        Assert.False(result.IsValid);
        Assert.Equal("P6T06.RrsProjection.Event.ProjectionDigestMismatch", result.FirstDiagnostic.Code);
        Assert.Equal(queueBefore, admission.Queue.ToCanonicalBytes());
    }

    [Fact]
    public void RegionalTiltProducesDistinctDeterministicSignedOverlay()
    {
        JsonElement scenario = LoadScenario("regional-tilt");
        ScenarioTrace first = RunRegionalTilt();
        ScenarioTrace replay = RunRegionalTilt();

        Assert.Equal(first.Bytes, replay.Bytes);
        Assert.Equal(ScenarioDouble(scenario, "expected_controller_commands", "total_power"), first.TotalCommand, 12);
        Assert.Equal(ScenarioDouble(scenario, "expected_controller_commands", "tilt"), first.TiltCommand, 12);
        Assert.True(first.LeftOverlay > first.RightOverlay);
        Assert.NotEqual(first.LeftOverlay, first.RightOverlay);

        WriteTrace("regional-tilt", first);
    }

    [Fact]
    public void ZoneSaturationUsesSharedQueueAndReachesExplicitBound()
    {
        JsonElement scenario = LoadScenario("zone-saturation");
        ScenarioTrace first = RunZoneSaturation();
        ScenarioTrace replay = RunZoneSaturation();

        Assert.Equal(first.Bytes, replay.Bytes);
        Assert.Equal(
            scenario.GetProperty("expected_saturation").GetString(),
            first.SaturationState.ToString());
        Assert.Equal(ScenarioDouble(scenario, "expected_final_fill_fraction"), first.FinalFillFraction, 12);
        Assert.Equal(ScenarioDouble(scenario, "motion_command_fraction"), first.FinalAvailableCommand, 12);
        Assert.Equal(ScenarioDouble(scenario, "expected_applied_delta_fraction"), first.AppliedDeltaFraction, 12);

        WriteTrace("zone-saturation", first);
    }

    [Fact]
    public void AdjusterInsertionAndWithdrawalRemainBoundedAndReplayable()
    {
        JsonElement scenario = LoadScenario("adjuster-insertion-withdrawal");
        ScenarioTrace first = RunAdjusterInsertionWithdrawal();
        ScenarioTrace replay = RunAdjusterInsertionWithdrawal();

        Assert.Equal(first.Bytes, replay.Bytes);
        Assert.Equal(ScenarioDouble(scenario, "expected_insertion_fraction"), first.InsertionFraction, 12);
        Assert.Equal(ScenarioDouble(scenario, "expected_withdrawal_fraction"), first.WithdrawalFraction, 12);
        Assert.True(first.InsertionOverlay > 0.0);
        Assert.True(first.WithdrawalOverlay < 0.0);

        WriteTrace("adjuster-insertion-withdrawal", first);
    }

    [Fact]
    public void PoisonRecoveryPreservesExactMassConcentrationAndSignedOverlay()
    {
        JsonElement scenario = LoadScenario("poison-recovery");
        ScenarioTrace first = RunPoisonRecovery();
        ScenarioTrace replay = RunPoisonRecovery();

        Assert.Equal(first.Bytes, replay.Bytes);
        Assert.Equal(ScenarioDouble(scenario, "expected_mass_after_add_kg"), first.MassAfterAddKg, 12);
        Assert.Equal(ScenarioDouble(scenario, "expected_mass_after_recovery_kg"), first.MassAfterRecoveryKg, 12);
        Assert.Equal(ScenarioDouble(scenario, "expected_concentration_after_recovery_kg_m^-3"), first.ConcentrationAfterRecoveryKgPerM3, 12);
        Assert.True(first.RecoveryOverlay < first.AddOverlay);

        WriteTrace("poison-recovery", first);
    }

    [Fact]
    public void ExplicitOneAndTwoStepPartitionsProduceIdenticalMotionEvidence()
    {
        using JsonDocument package = JsonDocument.Parse(File.ReadAllBytes(ScenarioPackagePath()));
        JsonElement timestep = package.RootElement.GetProperty("timestep_sensitivity");
        P6T06MotionResultV1 oneStep = RunRrsMotion(TimestepOneStep);
        P6T06MotionResultV1 twoSteps = RunRrsMotion(TimestepTwoSteps);

        Assert.Equal(
            timestep.GetProperty("expected_final_state_fraction").GetDouble(),
            oneStep.ActuatorStates.Single(state => state.Target.StableId == RrsFixtureV1.TotalPowerActuatorId).PhysicalState,
            12);
        Assert.Equal(
            oneStep.Queue.ToCanonicalBytes(),
            twoSteps.Queue.ToCanonicalBytes());
        Assert.Equal(
            oneStep.ActuatorStates.Select(ActuatorStateEvidence),
            twoSteps.ActuatorStates.Select(ActuatorStateEvidence));

        _output.WriteLine("P6_T07_TIMESTEP_QUEUE_DIGEST=" + Hex(oneStep.Queue.QueueDigest));
        _output.WriteLine("P6_T07_TIMESTEP_STATE_DIGEST=" + Hex(DigestOf(oneStep.ActuatorStates.Select(ActuatorStateEvidence).ToArray())));
    }

    [Fact]
    public void RefuellingBoundaryReleasesBeforeRrsMotionInCanonicalOrder()
    {
        string[] packagedOrder = LoadScenarioBoundary("same_time_order")
            .EnumerateArray()
            .Select(item => item.GetString()!)
            .ToArray();
        EventRankV1[] packagedRanks = packagedOrder
            .Select(value => Enum.Parse<EventRankV1>(value, ignoreCase: false))
            .ToArray();
        VersionLifecycleV1 lifecycle = CreateLifecycle();
        SimulationClockV1 clock = Require(SimulationClockV1.TryCreate(
            SimulationClockV1.CurrentSchemaVersion,
            0.0,
            0));
        EventRankV1[] ranks = packagedRanks;
        SimulationCommandV1[] commands = ranks
            .Select((rank, index) => Require(SimulationCommandV1.TryCreate(
                rank,
                (ulong)index,
                Id(0xf800u + (uint)index),
                EventOwnerV1.NotApplicable,
                EventBodyV1.NotApplicable,
                0.0,
                2.0,
                lifecycle.CreateStateBinding())))
            .ToArray();
        CommandQueueV1 queue = Require(CommandQueueV1.TryCreate(
            0,
            (ulong)commands.Length,
            commands.Select(command => command.CommandId),
            commands.Reverse(),
            clock));

        CommandQueueReleaseV1 released = Require(queue.TryReleaseDue(
            Require(clock.TryAdvanceTo(2.0))));
        Assert.Equal(ranks, released.ReleasedCommands.Select(command => command.EventRank));
        Assert.True((ushort)EventRankV1.Refuelling < (ushort)EventRankV1.ControllerCommandGeneration);
        Assert.True((ushort)EventRankV1.ControllerCommandGeneration < (ushort)EventRankV1.ActuatorMotion);
        Assert.Empty(released.Queue.PendingCommands);

        _output.WriteLine("P6_T07_REFUELLING_ORDER=" + string.Join(",", released.ReleasedCommands.Select(command => command.EventRank)));
    }

    private static string ScenarioPackagePath()
    {
        return TestDataLocator.RequireRepositoryFile(
            "data/scenarios/p6-t07-synthetic-rrs-scenarios-v1.json");
    }

    private static JsonElement LoadScenario(string scenarioId)
    {
        using JsonDocument package = JsonDocument.Parse(File.ReadAllBytes(ScenarioPackagePath()));
        JsonElement scenario = package.RootElement
            .GetProperty("scenarios")
            .EnumerateArray()
            .Single(candidate => candidate.GetProperty("scenario_id").GetString() == scenarioId);
        return scenario.Clone();
    }

    private static JsonElement LoadScenarioBoundary(string propertyName)
    {
        using JsonDocument package = JsonDocument.Parse(File.ReadAllBytes(ScenarioPackagePath()));
        return package.RootElement.GetProperty("refuelling_boundary").GetProperty(propertyName).Clone();
    }

    private static JsonElement LoadScenarioPackageProperty(string propertyName)
    {
        using JsonDocument package = JsonDocument.Parse(File.ReadAllBytes(ScenarioPackagePath()));
        return package.RootElement.GetProperty(propertyName).Clone();
    }

    private static double ScenarioDouble(JsonElement scenario, string propertyName)
    {
        return scenario.GetProperty(propertyName).GetDouble();
    }

    private static double ScenarioDouble(
        JsonElement scenario,
        string objectPropertyName,
        string valuePropertyName)
    {
        return scenario.GetProperty(objectPropertyName).GetProperty(valuePropertyName).GetDouble();
    }

    private static double[] ScenarioDoubles(JsonElement scenario, string propertyName)
    {
        return scenario.GetProperty(propertyName)
            .EnumerateArray()
            .Select(value => value.GetDouble())
            .ToArray();
    }

    private static byte[] ScenarioEvidence(JsonElement scenario)
    {
        return Encoding.UTF8.GetBytes(scenario.GetRawText());
    }

    private static void ValidateInputArtifacts(
        JsonElement package,
        JsonElement scenarioManifest)
    {
        JsonElement packageInputs = package.GetProperty("input_packages");
        JsonElement manifestInputs = scenarioManifest.GetProperty("input_packages");
        Assert.Equal(packageInputs.GetArrayLength(), manifestInputs.GetArrayLength());

        foreach (JsonElement packageInput in packageInputs.EnumerateArray())
        {
            string path = packageInput.GetString()!;
            JsonElement manifestInput = manifestInputs
                .EnumerateArray()
                .Single(candidate => candidate.GetProperty("path").GetString() == path);
            string manifestPath = manifestInput.GetProperty("manifest_path").GetString()!;
            byte[] artifactBytes = File.ReadAllBytes(ResolveRepositoryPath(path));
            string actualHash = Convert.ToHexString(SHA256.HashData(artifactBytes)).ToLowerInvariant();

            Assert.Equal(artifactBytes.Length, manifestInput.GetProperty("byte_length").GetInt64());
            Assert.Equal(actualHash, manifestInput.GetProperty("sha256").GetString());
            Assert.True(File.Exists(ResolveRepositoryPath(manifestPath)));

            using JsonDocument upstreamManifest = JsonDocument.Parse(
                File.ReadAllBytes(ResolveRepositoryPath(manifestPath)));
            (string declaredPath, long declaredLength, string declaredHash) =
                ReadArtifactIdentity(upstreamManifest.RootElement);
            Assert.Equal(path, declaredPath);
            Assert.Equal(artifactBytes.Length, declaredLength);
            Assert.Equal(actualHash, declaredHash);
        }
    }

    private static (string Path, long ByteLength, string Sha256) ReadArtifactIdentity(
        JsonElement manifest)
    {
        if (manifest.TryGetProperty("artifact", out JsonElement artifact))
        {
            return (
                artifact.GetProperty("path").GetString()!,
                artifact.GetProperty("byte_length").GetInt64(),
                artifact.GetProperty("sha256").GetString()!);
        }

        return (
            manifest.GetProperty("artifact_path").GetString()!,
            manifest.GetProperty("artifact_byte_length").GetInt64(),
            manifest.GetProperty("artifact_sha256").GetString()!);
    }

    private static string ResolveRepositoryPath(string relativePath)
    {
        return TestDataLocator.RequireRepositoryFile(relativePath);
    }

    private static ScenarioTrace RunCenteredPerturbation()
    {
        JsonElement scenario = LoadScenario("centered-perturbation");
        double measurementTime = ScenarioDouble(scenario, "measurement_time_s");
        double cadence = ScenarioDouble(scenario, "rrs_cadence_s");
        double delay = ScenarioDouble(scenario, "actuator_delay_s");
        double rate = ScenarioDouble(scenario, "actuator_rate_s^-1");
        double[] motionTimes = ScenarioDoubles(scenario, "queue_motion_times_s");
        Assert.Equal(2, motionTimes.Length);
        RrsControllerAdmissionFixture admission = CreateCenteredAdmissionFixture(
            measurementTime,
            ScenarioDouble(scenario, "measured_power_w"),
            ScenarioDouble(scenario, "left_measured_fraction"),
            ScenarioDouble(scenario, "right_measured_fraction"),
            cadence,
            rate,
            Digest(0x61));
        RrsFixture fixture = admission.Fixture;
        RrsMeasurementSnapshotV1 measurement = admission.Measurement;
        RrsControllerProjectionV1 projection = admission.Projection;
        Assert.All(projection.Commands, command =>
        {
            Assert.Equal(delay, command.DelaySeconds, 12);
            Assert.Equal(rate, command.RateLimitPerSecond, 12);
        });
        P6T06QueueStateV1 queue = admission.Queue;
        P6T06RrsControllerEventIdentityV1 eventIdentity = admission.EventIdentity;
        P6T06TargetKeyV1 totalTarget = admission.Queue.AvailableCommands.Single(
            command => command.Target.StableId == RrsFixtureV1.TotalPowerActuatorId).Target;
        P6T06TargetKeyV1 tiltTarget = admission.Queue.AvailableCommands.Single(
            command => command.Target.StableId == RrsFixtureV1.TiltActuatorId).Target;
        P6T06EnqueueResultV1 enqueue = Require(
            queue.TryEnqueueRrsControllerProjection(
                projection,
                eventIdentity,
                Require(queue.TryCreatePhaseToken(projection.CurrentTimeSeconds))));
        P6T06ActuatorStateV1[] initialStates =
        {
            Require(P6T06ActuatorStateV1.TryCreate(
                P6T06QueueFamilyV1.Rrs,
                totalTarget,
                0.5,
                rate)),
            Require(P6T06ActuatorStateV1.TryCreate(
                P6T06QueueFamilyV1.Rrs,
                tiltTarget,
                0.5,
                rate))
        };
        P6T06MotionResultV1 beforeDue = Require(enqueue.Queue.TryMotionAndConsume(
            motionTimes[0],
            P6T06MotionModeV1.Automatic,
            initialStates,
            new[] { motionTimes[0] }));
        P6T06MotionResultV1 afterDue = Require(beforeDue.Queue.TryMotionAndConsume(
            motionTimes[1],
            P6T06MotionModeV1.Automatic,
            beforeDue.ActuatorStates,
            new[] { motionTimes[1] - motionTimes[0] }));
        RrsOverlayEvaluationV1 overlay = Require(RrsOverlayV1.TryEvaluate(
            fixture.Map,
            afterDue.ActuatorStates
                .Select(state => Require(RrsActuatorSnapshotV1.TryCreate(
                    state.Target.StableId,
                    0.5,
                    state.PhysicalState)))
                .ToArray()));

        RrsOverlayValueV1 totalValue = RequireRrsValue(overlay, new NodeKey(new ChannelId(0), new BundlePosition(0)), 0);
        return new ScenarioTrace(
            EvidenceBytes(
                "centered-perturbation",
                ScenarioEvidence(scenario),
                measurement.ToCanonicalBytes(),
                ProjectionEvidence(projection),
                enqueue.Queue.ToCanonicalBytes(),
                beforeDue.Transition.ToCanonicalBytes(),
                afterDue.Queue.ToCanonicalBytes(),
                PackComponents(afterDue.ActuatorStates.Select(ActuatorStateEvidence)),
                overlay.ToCanonicalBytes()),
            projection.Commands.Single(command => command.ActuatorId == RrsFixtureV1.TotalPowerActuatorId).BoundedCommand,
            projection.Commands.Single(command => command.ActuatorId == RrsFixtureV1.TiltActuatorId).BoundedCommand,
            beforeDue.ActuatorStates.Single(state => state.Target.StableId == RrsFixtureV1.TotalPowerActuatorId).PhysicalState,
            afterDue.ActuatorStates.Single(state => state.Target.StableId == RrsFixtureV1.TotalPowerActuatorId).PhysicalState,
            totalValue.DeltaSigmaAMInverse,
            0.0,
            0.0,
            0.0,
            P6T06SaturationStateV1.NotApplicable,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0);
    }

    private static ScenarioTrace RunRegionalTilt()
    {
        JsonElement scenario = LoadScenario("regional-tilt");
        double measurementTime = ScenarioDouble(scenario, "measurement_time_s");
        double measuredPower = ScenarioDouble(scenario, "measured_power_w");
        RrsFixture fixture = CreateRrsFixture();
        RrsMeasurementSnapshotV1 measurement = Require(RrsMeasurementSnapshotV1.TryCreate(
            fixture.RegionSet,
            measurementTime,
            measuredPower,
            ScenarioDouble(scenario, "left_measured_fraction") * measuredPower,
            ScenarioDouble(scenario, "right_measured_fraction") * measuredPower));
        RrsControllerProjectionV1 projection = Require(
            RrsControllerProjectionV1.TryProjectAutomatic(fixture.State, measurement, measurementTime));
        RrsOverlayEvaluationV1 overlay = Require(RrsOverlayV1.TryEvaluate(
            fixture.Map,
            new[]
            {
                Require(RrsActuatorSnapshotV1.TryCreate(
                    RrsFixtureV1.TotalPowerActuatorId,
                    0.5,
                    ScenarioDouble(scenario, "overlay_total_state_fraction"))),
                Require(RrsActuatorSnapshotV1.TryCreate(
                    RrsFixtureV1.TiltActuatorId,
                    0.5,
                    ScenarioDouble(scenario, "overlay_tilt_state_fraction")))
            }));
        double left = RequireRrsValue(
            overlay,
            new NodeKey(new ChannelId(0), new BundlePosition(0)),
            0).DeltaSigmaAMInverse;
        double right = RequireRrsValue(
            overlay,
            new NodeKey(new ChannelId(3), new BundlePosition(0)),
            0).DeltaSigmaAMInverse;
        return new ScenarioTrace(
            EvidenceBytes(
                "regional-tilt",
                ScenarioEvidence(scenario),
                measurement.ToCanonicalBytes(),
                ProjectionEvidence(projection),
                overlay.ToCanonicalBytes()),
            projection.Commands.Single(command => command.ActuatorId == RrsFixtureV1.TotalPowerActuatorId).BoundedCommand,
            projection.Commands.Single(command => command.ActuatorId == RrsFixtureV1.TiltActuatorId).BoundedCommand,
            0.0,
            0.0,
            0.0,
            left,
            right,
            0.0,
            P6T06SaturationStateV1.NotApplicable,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0);
    }

    private static ScenarioTrace RunZoneSaturation()
    {
        JsonElement scenario = LoadScenario("zone-saturation");
        uint logicalZoneId = (uint)ScenarioDouble(scenario, "logical_zone_id");
        double initialFillFraction = ScenarioDouble(scenario, "initial_fill_fraction");
        double requestedCommandFraction = ScenarioDouble(scenario, "requested_command_fraction");
        double motionCommandFraction = ScenarioDouble(scenario, "motion_command_fraction");
        double rate = ScenarioDouble(scenario, "rate_limit_s^-1");
        double commandAvailableTime = ScenarioDouble(scenario, "command_available_time_s");
        double motionTime = ScenarioDouble(scenario, "motion_time_s");
        LiquidZoneInfluenceMapV1 map = CreateLiquidZoneMap();
        LiquidZoneSystemStateV1 system = CreateLiquidZoneSystem(
            map,
            Id(0xf702),
            initialFillFraction,
            rate);
        LiquidZoneStateV1 zone = system.Zones.Single(candidate => candidate.LogicalZoneId == logicalZoneId);
        LiquidZoneMotionResultV1 localMotion = Require(LiquidZoneOverlayV1.TryAdvance(
            zone,
            motionCommandFraction,
            0.0,
            commandAvailableTime,
            motionTime));

        P6T06TargetKeyV1 target = Require(P6T06TargetKeyV1.TryForLiquidZone(logicalZoneId));
        P6T06QueueStateV1 queue = CreateP6Queue(
            P6T06QueueFamilyV1.LiquidZone,
            Id(0xf703),
            Id(0xf702),
            null,
            0.0,
            new[]
            {
                Require(P6T06AvailableCommandV1.TryCreate(
                    P6T06QueueFamilyV1.LiquidZone,
                    target,
                    initialFillFraction,
                    0.0,
                    1.0,
                    rate))
            });
        P6T06CommandCandidateV1 candidate = Require(P6T06CommandCandidateV1.TryCreate(
            P6T06QueueFamilyV1.LiquidZone,
            target,
            P6T06SourceKindV1.Scheduled,
            EventRankV1.BranchUpdate,
            commandAvailableTime,
            requestedCommandFraction,
            0.0,
            1.0,
            rate));
        P6T06EnqueueResultV1 enqueue = Require(queue.TryEnqueueBatch(
            Binding(0xf712, Digest(0x72)),
            0.0,
            Require(queue.TryCreatePhaseToken(0.0)),
            new[] { candidate }));
        P6T06MotionResultV1 atDue = Require(enqueue.Queue.TryMotionAndConsume(
            commandAvailableTime,
            P6T06MotionModeV1.RateLimited,
            new[]
            {
                Require(P6T06ActuatorStateV1.TryCreate(
                    P6T06QueueFamilyV1.LiquidZone,
                    target,
                    initialFillFraction,
                    rate))
            },
            Array.Empty<double>()));
        P6T06MotionResultV1 finalMotion = Require(atDue.Queue.TryMotionAndConsume(
            motionTime,
            P6T06MotionModeV1.RateLimited,
            atDue.ActuatorStates,
            new[] { motionTime - commandAvailableTime }));
        P6T06SaturationDiagnosticV1 saturation = enqueue.SaturationDiagnostics.Single();

        return new ScenarioTrace(
            EvidenceBytes(
                "zone-saturation",
                ScenarioEvidence(scenario),
                system.ToCanonicalBytes(),
                localMotion.ToCanonicalBytes(),
                enqueue.Transition.ToCanonicalBytes(),
                finalMotion.Transition.ToCanonicalBytes(),
                finalMotion.Queue.ToCanonicalBytes(),
                PackComponents(finalMotion.ActuatorStates.Select(ActuatorStateEvidence))),
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            localMotion.AppliedDeltaFraction,
            saturation.SaturationState,
            finalMotion.ActuatorStates.Single().PhysicalState,
            finalMotion.Queue.AvailableCommands.Single().BoundedCommand,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0);
    }

    private static ScenarioTrace RunAdjusterInsertionWithdrawal()
    {
        JsonElement scenario = LoadScenario("adjuster-insertion-withdrawal");
        StableId bankId = StableId.Parse(scenario.GetProperty("bank_id").GetString()!);
        StableId queueId = StableId.Parse(scenario.GetProperty("queue_id").GetString()!);
        StableId branchId = StableId.Parse(scenario.GetProperty("owner_branch_id").GetString()!);
        double initialFraction = ScenarioDouble(scenario, "initial_fraction");
        double insertionCommand = ScenarioDouble(scenario, "insertion_command_fraction");
        double withdrawalCommand = ScenarioDouble(scenario, "withdrawal_command_fraction");
        double rate = ScenarioDouble(scenario, "rate_limit_s^-1");
        double commandAvailableTime = ScenarioDouble(scenario, "command_available_time_s");
        double motionTime = ScenarioDouble(scenario, "motion_time_s");
        double postDelayDuration = ScenarioDouble(scenario, "post_delay_motion_duration_s");
        Assert.Equal(postDelayDuration, motionTime - commandAvailableTime, 12);
        AdjusterInfluenceMapV1 map = CreateAdjusterMap();
        Assert.Equal(AdjusterBankGroupingV1.ApprovedBankBId, bankId);
        AdjusterSetStateV1 insertionSet = CreateAdjusterSet(
            map,
            branchId,
            queueId,
            insertionCommand,
            initialFraction,
            rate,
            commandAvailableTime);
        AdjusterSetStateV1 withdrawalSet = CreateAdjusterSet(
            map,
            branchId,
            queueId,
            withdrawalCommand,
            initialFraction,
            rate,
            commandAvailableTime);
        P6T06TargetKeyV1 target = Require(P6T06TargetKeyV1.TryForAdjusterBank(bankId));
        P6T06MotionResultV1 insertion = RunAdjusterQueueMotion(
            queueId,
            branchId,
            target,
            insertionCommand,
            initialFraction,
            rate,
            commandAvailableTime,
            motionTime,
            0xf821,
            0x81);
        P6T06MotionResultV1 withdrawal = RunAdjusterQueueMotion(
            queueId,
            branchId,
            target,
            withdrawalCommand,
            initialFraction,
            rate,
            commandAvailableTime,
            motionTime,
            0xf822,
            0x82);
        AdjusterSetStateV1 insertionOverlayState = CreateAdjusterSet(
            map,
            branchId,
            queueId,
            insertionCommand,
            insertion.ActuatorStates.Single().PhysicalState,
            rate,
            commandAvailableTime);
        AdjusterSetStateV1 withdrawalOverlayState = CreateAdjusterSet(
            map,
            branchId,
            queueId,
            withdrawalCommand,
            withdrawal.ActuatorStates.Single().PhysicalState,
            rate,
            commandAvailableTime);
        AdjusterOverlayEvaluationV1 insertionOverlay = Require(AdjusterOverlayV1.TryEvaluate(
            map,
            insertionOverlayState));
        AdjusterOverlayEvaluationV1 withdrawalOverlay = Require(AdjusterOverlayV1.TryEvaluate(
            map,
            withdrawalOverlayState));
        NodeKey bankBNode = new NodeKey(new ChannelId(3), new BundlePosition(0));
        double insertionValue = Require(insertionOverlay.TryGetValue(bankBNode, 0)).DeltaSigmaAMInverse;
        double withdrawalValue = Require(withdrawalOverlay.TryGetValue(bankBNode, 0)).DeltaSigmaAMInverse;

        return new ScenarioTrace(
            EvidenceBytes(
                "adjuster-insertion-withdrawal",
                ScenarioEvidence(scenario),
                insertionSet.ToCanonicalBytes(),
                withdrawalSet.ToCanonicalBytes(),
                insertionOverlayState.ToCanonicalBytes(),
                withdrawalOverlayState.ToCanonicalBytes(),
                insertion.Transition.ToCanonicalBytes(),
                insertion.Queue.ToCanonicalBytes(),
                PackComponents(insertion.ActuatorStates.Select(ActuatorStateEvidence)),
                withdrawal.Transition.ToCanonicalBytes(),
                withdrawal.Queue.ToCanonicalBytes(),
                PackComponents(withdrawal.ActuatorStates.Select(ActuatorStateEvidence)),
                insertionOverlay.ToCanonicalBytes(),
                withdrawalOverlay.ToCanonicalBytes()),
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            P6T06SaturationStateV1.NotApplicable,
            0.0,
            0.0,
            insertion.ActuatorStates.Single().PhysicalState,
            withdrawal.ActuatorStates.Single().PhysicalState,
            insertionValue,
            withdrawalValue,
            0.0,
            0.0,
            0.0);
    }

    private static P6T06MotionResultV1 RunAdjusterQueueMotion(
        StableId queueId,
        StableId ownerId,
        P6T06TargetKeyV1 target,
        double requestedCommand,
        double initialFraction,
        double rate,
        double commandAvailableTime,
        double motionTime,
        int bindingSuffix,
        byte bindingDigest)
    {
        P6T06QueueStateV1 queue = CreateP6Queue(
            P6T06QueueFamilyV1.Adjuster,
            queueId,
            ownerId,
            null,
            0.0,
            new[]
            {
                Require(P6T06AvailableCommandV1.TryCreate(
                    P6T06QueueFamilyV1.Adjuster,
                    target,
                    initialFraction,
                    0.0,
                    1.0,
                    rate))
            });
        P6T06CommandCandidateV1 candidate = Require(P6T06CommandCandidateV1.TryCreate(
            P6T06QueueFamilyV1.Adjuster,
            target,
            P6T06SourceKindV1.Manual,
            EventRankV1.BranchUpdate,
            commandAvailableTime,
            requestedCommand,
            0.0,
            1.0,
            rate));
        P6T06EnqueueResultV1 enqueue = Require(queue.TryEnqueueBatch(
            Binding(bindingSuffix, Digest(bindingDigest)),
            0.0,
            Require(queue.TryCreatePhaseToken(0.0)),
            new[] { candidate }));
        P6T06MotionResultV1 atDue = Require(enqueue.Queue.TryMotionAndConsume(
            commandAvailableTime,
            P6T06MotionModeV1.RateLimited,
            new[]
            {
                Require(P6T06ActuatorStateV1.TryCreate(
                    P6T06QueueFamilyV1.Adjuster,
                    target,
                    initialFraction,
                    rate))
            },
            new[] { commandAvailableTime }));
        return Require(atDue.Queue.TryMotionAndConsume(
            motionTime,
            P6T06MotionModeV1.RateLimited,
            atDue.ActuatorStates,
            new[] { motionTime - commandAvailableTime }));
    }

    private static ScenarioTrace RunPoisonRecovery()
    {
        JsonElement scenario = LoadScenario("poison-recovery");
        double initialMass = ScenarioDouble(scenario, "initial_mass_kg");
        double addDuration = ScenarioDouble(scenario, "add_duration_s");
        double withdrawDuration = ScenarioDouble(scenario, "withdraw_duration_s");
        double recoveryTime = addDuration + withdrawDuration;
        BulkPoisonInfluenceMapV1 map = CreateBulkPoisonMap();
        Assert.Equal(ScenarioDouble(scenario, "moderator_volume_m3"), map.ModeratorVolumeM3, 12);
        Assert.Equal(ScenarioDouble(scenario, "add_rate_kg_s"), map.AddRateKgPerSecond, 12);
        Assert.Equal(ScenarioDouble(scenario, "withdraw_rate_kg_s"), map.WithdrawRateKgPerSecond, 12);
        BulkPoisonStateV1 initial = CreatePoisonState(
            map,
            BulkPoisonModeV1.Add,
            initialMass,
            0.0);
        BulkPoisonActionResultV1 add = Require(BulkPoisonAccountingV1.TryAdvance(initial, addDuration));
        BulkPoisonStateV1 recoveryState = CreatePoisonState(
            map,
            BulkPoisonModeV1.Withdraw,
            add.PoisonMassAfterKg,
            addDuration);
        BulkPoisonActionResultV1 recovery = Require(BulkPoisonAccountingV1.TryAdvance(recoveryState, recoveryTime));
        BulkPoisonOverlayEvaluationV1 addOverlay = Require(BulkPoisonOverlayV1.TryEvaluate(map, recoveryState));
        BulkPoisonStateV1 recoveredState = CreatePoisonState(
            map,
            BulkPoisonModeV1.Withdraw,
            recovery.PoisonMassAfterKg,
            recoveryTime);
        BulkPoisonOverlayEvaluationV1 recoveryOverlay = Require(BulkPoisonOverlayV1.TryEvaluate(map, recoveredState));
        double addValue = Require(addOverlay.TryGetValue(
            new NodeKey(new ChannelId(0), new BundlePosition(0)),
            0)).DeltaSigmaAMInverse;
        double recoveryValue = Require(recoveryOverlay.TryGetValue(
            new NodeKey(new ChannelId(0), new BundlePosition(0)),
            0)).DeltaSigmaAMInverse;

        return new ScenarioTrace(
            EvidenceBytes(
                "poison-recovery",
                ScenarioEvidence(scenario),
                initial.ToCanonicalBytes(),
                add.ToCanonicalBytes(),
                recoveryState.ToCanonicalBytes(),
                recovery.ToCanonicalBytes(),
                addOverlay.ToCanonicalBytes(),
                recoveredState.ToCanonicalBytes(),
                recoveryOverlay.ToCanonicalBytes()),
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            0.0,
            P6T06SaturationStateV1.NotApplicable,
            0.0,
            0.0,
            0.0,
            0.0,
            recoveryValue,
            addValue,
            add.PoisonMassAfterKg,
            recovery.PoisonMassAfterKg,
            recovery.ConcentrationAfterKgPerM3);
    }

    private static P6T06MotionResultV1 RunRrsMotion(IReadOnlyList<double> substeps)
    {
        JsonElement timestep = LoadScenarioPackageProperty("timestep_sensitivity");
        JsonElement centered = LoadScenario("centered-perturbation");
        double targetCommand = timestep.GetProperty("target_command_fraction").GetDouble();
        double initialState = timestep.GetProperty("initial_state_fraction").GetDouble();
        double rate = timestep.GetProperty("rate_limit_s^-1").GetDouble();
        double elapsedTime = timestep.GetProperty("elapsed_time_s").GetDouble();
        P6T06TargetKeyV1 total = Require(P6T06TargetKeyV1.TryForRrsActuator(
            RrsFixtureV1.TotalPowerActuatorId));
        P6T06TargetKeyV1 tilt = Require(P6T06TargetKeyV1.TryForRrsActuator(
            RrsFixtureV1.TiltActuatorId));
        P6T06QueueStateV1 queue = CreateP6Queue(
            P6T06QueueFamilyV1.Rrs,
            Id(0xf901),
            RrsFixtureV1.ControllerId,
            ScenarioDouble(centered, "rrs_cadence_s"),
            0.0,
            new[]
            {
                Require(P6T06AvailableCommandV1.TryCreate(
                    P6T06QueueFamilyV1.Rrs,
                    total,
                    targetCommand,
                    0.0,
                    1.0,
                    rate)),
                Require(P6T06AvailableCommandV1.TryCreate(
                    P6T06QueueFamilyV1.Rrs,
                    tilt,
                    initialState,
                    0.0,
                    1.0,
                    rate))
            });
        return Require(queue.TryMotionAndConsume(
            elapsedTime,
            P6T06MotionModeV1.Automatic,
            new[]
            {
                Require(P6T06ActuatorStateV1.TryCreate(
                    P6T06QueueFamilyV1.Rrs,
                    total,
                    initialState,
                    rate)),
                Require(P6T06ActuatorStateV1.TryCreate(
                    P6T06QueueFamilyV1.Rrs,
                    tilt,
                    initialState,
                    rate))
            },
            substeps));
    }

    private static P6T06QueueStateV1 CreateP6Queue(
        P6T06QueueFamilyV1 family,
        StableId queueId,
        StableId ownerId,
        double? cadence,
        double lastMotionTimeSeconds,
        IEnumerable<P6T06AvailableCommandV1> available)
    {
        return Require(P6T06QueueStateV1.TryCreate(
            family,
            queueId,
            Require(P6T06OwnerKeyV1.TryForFamily(family, ownerId)),
            cadence,
            0,
            0,
            lastMotionTimeSeconds,
            available,
            Array.Empty<P6T06QueueCommandV1>(),
            Array.Empty<StableId>(),
            Array.Empty<StableId>()));
    }

    private static P6T06SourceBindingTokenV1 Binding(int suffix, Digest32 digest)
    {
        return Require(P6T06SourceBindingTokenV1.TryCreate(Id((uint)suffix), digest));
    }

    private static RrsControllerAdmissionFixture CreateCenteredAdmissionFixture(
        double measurementTime,
        double measuredPower,
        double leftMeasuredFraction,
        double rightMeasuredFraction,
        double cadence,
        double rate,
        Digest32 sourceBindingDigest)
    {
        RrsFixture fixture = CreateRrsFixture();
        RrsMeasurementSnapshotV1 measurement = Require(RrsMeasurementSnapshotV1.TryCreate(
            fixture.RegionSet,
            measurementTime,
            measuredPower,
            leftMeasuredFraction * measuredPower,
            rightMeasuredFraction * measuredPower));
        RrsControllerProjectionV1 projection = Require(
            RrsControllerProjectionV1.TryProjectAutomatic(fixture.State, measurement, measurementTime));
        P6T06TargetKeyV1 totalTarget = Require(
            P6T06TargetKeyV1.TryForRrsActuator(RrsFixtureV1.TotalPowerActuatorId));
        P6T06TargetKeyV1 tiltTarget = Require(
            P6T06TargetKeyV1.TryForRrsActuator(RrsFixtureV1.TiltActuatorId));
        P6T06QueueStateV1 queue = CreateP6Queue(
            P6T06QueueFamilyV1.Rrs,
            Id(0xf601),
            fixture.State.ControllerId,
            cadence,
            0.0,
            new[]
            {
                Require(P6T06AvailableCommandV1.TryCreate(
                    P6T06QueueFamilyV1.Rrs,
                    totalTarget,
                    0.5,
                    0.0,
                    1.0,
                    rate)),
                Require(P6T06AvailableCommandV1.TryCreate(
                    P6T06QueueFamilyV1.Rrs,
                    tiltTarget,
                    0.5,
                    0.0,
                    1.0,
                    rate))
            });
        P6T06RrsControllerEventIdentityV1 eventIdentity = Require(
            P6T06RrsControllerEventIdentityV1.TryCreate(
                fixture.State.ControllerId,
                Id(0xf611),
                sourceBindingDigest,
                projection.ProjectionDigest));
        return new RrsControllerAdmissionFixture(
            fixture,
            measurement,
            projection,
            queue,
            eventIdentity);
    }

    private static RrsFixture CreateRrsFixture()
    {
        RrsRegionSetV1 regionSet = CreateRrsRegionSet();
        KeyValuePair<StableId, double>[] references =
        {
            new(RrsFixtureV1.TotalPowerActuatorId, 0.5),
            new(RrsFixtureV1.TiltActuatorId, 0.5)
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
        List<RrsInfluenceMapEntryV1> entries = new();
        foreach (StableId actuatorId in new[]
        {
            RrsFixtureV1.TotalPowerActuatorId,
            RrsFixtureV1.TiltActuatorId
        })
        {
            foreach (NodeKey node in regionSet.TargetNodes)
            {
                for (ushort group = 0; group < RrsFixtureV1.GroupCount; group++)
                {
                    entries.Add(Require(RrsInfluenceMapEntryV1.TryCreate(
                        actuatorId,
                        node,
                        group,
                        RrsInfluenceMapEntryV1.ExpectedWeight(actuatorId, node, group),
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
            RrsModeV1.Automatic,
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
            RrsModeV1.Automatic,
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
        return new RrsFixture(regionSet, map, state);
    }

    private static RrsRegionSetV1 CreateRrsRegionSet()
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

    private static LiquidZoneInfluenceMapV1 CreateLiquidZoneMap()
    {
        LiquidZoneGroupingV1 grouping = CreateLiquidZoneGrouping();
        NodeKey[] targets = Enumerable.Range(0, 6)
            .Select(index => new NodeKey(new ChannelId((uint)index), new BundlePosition(0)))
            .ToArray();
        Digest32 topologyDigest = LiquidZoneInfluenceMapV1.ComputeTopologyDigest(
            LiquidZoneInfluenceMapV1.ApprovedTopologyId,
            targets);
        double[] references = Enumerable.Repeat(
            LiquidZoneInfluenceMapV1.ApprovedReferenceFillFraction,
            14).ToArray();
        Digest32 referenceDigest = LiquidZoneInfluenceMapV1.ComputeReferenceStateDigest(
            LiquidZoneInfluenceMapV1.ApprovedMapId,
            grouping,
            LiquidZoneInfluenceMapV1.ApprovedTopologyId,
            topologyDigest,
            LiquidZoneInfluenceMapV1.ApprovedDataVersion,
            LiquidZoneInfluenceMapV1.ApprovedOwnerId,
            LiquidZoneInfluenceMapV1.ApprovedSignCertificate,
            LiquidZoneInfluenceMapV1.SourceUnitDimensionless,
            LiquidZoneInfluenceMapV1.TargetUnitMInverse,
            references);
        LiquidZoneInfluenceMapEntryV1[] entries = grouping.Mappings
            .SelectMany(mapping => new[]
            {
                Require(LiquidZoneInfluenceMapEntryV1.TryCreate(
                    mapping.LogicalZoneId,
                    targets[(int)mapping.PhysicalAssemblyId],
                    0,
                    LiquidZoneInfluenceMapV1.ApprovedGroup0WeightMInversePerFillFraction,
                    LiquidZoneInfluenceMapV1.SourceUnitDimensionless,
                    LiquidZoneInfluenceMapV1.TargetUnitMInverse)),
                Require(LiquidZoneInfluenceMapEntryV1.TryCreate(
                    mapping.LogicalZoneId,
                    targets[(int)mapping.PhysicalAssemblyId],
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
            targets,
            topologyDigest,
            LiquidZoneInfluenceMapV1.ApprovedDataVersion,
            LiquidZoneInfluenceMapV1.ApprovedOwnerId,
            LiquidZoneInfluenceMapV1.ApprovedSignCertificate,
            LiquidZoneInfluenceMapV1.SourceUnitDimensionless,
            LiquidZoneInfluenceMapV1.TargetUnitMInverse,
            references,
            referenceDigest,
            entries);
        return Require(LiquidZoneInfluenceMapV1.TryCreate(
            1,
            LiquidZoneInfluenceMapV1.ApprovedMapId,
            grouping,
            LiquidZoneInfluenceMapV1.ApprovedTopologyId,
            targets,
            topologyDigest,
            LiquidZoneInfluenceMapV1.ApprovedDataVersion,
            LiquidZoneInfluenceMapV1.ApprovedOwnerId,
            LiquidZoneInfluenceMapV1.ApprovedSignCertificate,
            LiquidZoneInfluenceMapV1.SourceUnitDimensionless,
            LiquidZoneInfluenceMapV1.TargetUnitMInverse,
            references,
            referenceDigest,
            entries,
            mapDigest));
    }

    private static LiquidZoneGroupingV1 CreateLiquidZoneGrouping()
    {
        uint[] physicalByLogical = { 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 0, 1 };
        LiquidZoneAssemblyBindingV1[] mappings = physicalByLogical
            .Select((physical, logical) => Require(
                LiquidZoneAssemblyBindingV1.TryCreate((uint)logical, physical)))
            .ToArray();
        Digest32 digest = LiquidZoneGroupingV1.ComputeDigest(
            1,
            LiquidZoneInfluenceMapV1.ApprovedMappingId,
            LiquidZoneInfluenceMapV1.ApprovedMappingVersion,
            mappings);
        return Require(LiquidZoneGroupingV1.TryCreate(
            1,
            LiquidZoneInfluenceMapV1.ApprovedMappingId,
            LiquidZoneInfluenceMapV1.ApprovedMappingVersion,
            mappings,
            digest));
    }

    private static LiquidZoneSystemStateV1 CreateLiquidZoneSystem(
        LiquidZoneInfluenceMapV1 map,
        StableId branchId,
        double initialFillFraction,
        double rateLimitPerSecond)
    {
        LiquidZoneStateV1[] zones = map.Grouping.Mappings
            .Select(mapping =>
            {
                LiquidZoneModeV1 mode = mapping.LogicalZoneId == 0
                    ? LiquidZoneModeV1.Disabled
                    : LiquidZoneModeV1.RateLimited;
                bool enabled = mode != LiquidZoneModeV1.Disabled;
                LiquidZoneQueueBindingV1 queue = Require(LiquidZoneQueueBindingV1.TryCreate(
                    1,
                    Id(0xf900u + mapping.LogicalZoneId),
                    branchId,
                    Digest((byte)(mapping.LogicalZoneId + 1))));
                return Require(LiquidZoneStateV1.TryCreate(
                    1,
                    mapping.LogicalZoneId,
                    mapping.PhysicalAssemblyId,
                    mapping.LogicalZoneId == 0 ? 0.25 : initialFillFraction,
                    0.50,
                    0.75,
                    enabled,
                    mode,
                    rateLimitPerSecond,
                    0.0,
                    map.MapId,
                    map.DataVersion,
                    map.MapDigest,
                    0.0,
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

    private static AdjusterInfluenceMapV1 CreateAdjusterMap()
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
        NodeKey[] targets = Enumerable.Range(0, 6)
            .Select(index => new NodeKey(new ChannelId((uint)index), new BundlePosition(0)))
            .ToArray();
        Digest32 topologyDigest = AdjusterInfluenceMapV1.ComputeTopologyDigest(
            AdjusterInfluenceMapV1.ApprovedTopologyId,
            targets);
        KeyValuePair<StableId, double>[] references =
        {
            new(AdjusterBankGroupingV1.ApprovedBankAId, 0.5),
            new(AdjusterBankGroupingV1.ApprovedBankBId, 0.5)
        };
        Digest32 referenceDigest = AdjusterInfluenceMapV1.ComputeReferenceStateDigest(
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
            targets,
            topologyDigest,
            AdjusterInfluenceMapV1.ApprovedDataVersion,
            AdjusterInfluenceMapV1.ApprovedOwnerId,
            AdjusterInfluenceMapV1.ApprovedSignCertificate,
            AdjusterInfluenceMapV1.ApprovedNormalization,
            AdjusterInfluenceMapV1.SourceUnitDimensionless,
            AdjusterInfluenceMapV1.TargetUnitMInverse,
            references,
            referenceDigest,
            entries);
        return Require(AdjusterInfluenceMapV1.TryCreate(
            1,
            AdjusterInfluenceMapV1.ApprovedAdjusterSetId,
            AdjusterInfluenceMapV1.ApprovedMapId,
            grouping,
            AdjusterInfluenceMapV1.ApprovedTopologyId,
            targets,
            topologyDigest,
            AdjusterInfluenceMapV1.ApprovedDataVersion,
            AdjusterInfluenceMapV1.ApprovedOwnerId,
            AdjusterInfluenceMapV1.ApprovedSignCertificate,
            AdjusterInfluenceMapV1.ApprovedNormalization,
            AdjusterInfluenceMapV1.SourceUnitDimensionless,
            AdjusterInfluenceMapV1.TargetUnitMInverse,
            references,
            referenceDigest,
            entries,
            mapDigest));
    }

    private static AdjusterSetStateV1 CreateAdjusterSet(
        AdjusterInfluenceMapV1 map,
        StableId branchId,
        StableId queueId,
        double bankBAvailableFraction,
        double bankBStateFraction,
        double rateLimitPerSecond,
        double delaySeconds)
    {
        AdjusterBankStateV1 bankA = Require(AdjusterBankStateV1.TryCreate(
            1,
            AdjusterBankGroupingV1.ApprovedBankAId,
            true,
            AdjusterMotionModeV1.Manual,
            0.5,
            0.5,
            0.5,
            rateLimitPerSecond,
            delaySeconds,
            map.MapId,
            map.DataVersion,
            map.MapDigest,
            0.0,
            null));
        AdjusterAvailableCommandV1 available = Require(AdjusterAvailableCommandV1.TryCreate(
            AdjusterBankGroupingV1.ApprovedBankBId,
            bankBAvailableFraction,
            0.0,
            1.0,
            rateLimitPerSecond));
        AdjusterQueueStateV1 queue = Require(AdjusterQueueStateV1.TryCreate(
            1,
            queueId,
            branchId,
            AdjusterOptionalDoubleV1.NotApplicable,
            0,
            0,
            new[] { available },
            0.0,
            Array.Empty<AdjusterPendingCommandV1>(),
            Array.Empty<StableId>(),
            Array.Empty<StableId>()));
        AdjusterBankStateV1 bankB = Require(AdjusterBankStateV1.TryCreate(
            1,
            AdjusterBankGroupingV1.ApprovedBankBId,
            true,
            AdjusterMotionModeV1.RateLimited,
            0.5,
            0.5,
            bankBStateFraction,
            rateLimitPerSecond,
            delaySeconds,
            map.MapId,
            map.DataVersion,
            map.MapDigest,
            0.0,
            queue));
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

    private static BulkPoisonInfluenceMapV1 CreateBulkPoisonMap()
    {
        NodeKey[] targets = Enumerable.Range(0, 6)
            .Select(index => new NodeKey(new ChannelId((uint)index), new BundlePosition(0)))
            .ToArray();
        Digest32 topologyDigest = BulkPoisonInfluenceMapV1.ComputeTopologyDigest(
            BulkPoisonInfluenceMapV1.ApprovedTopologyId,
            targets);
        Digest32 referenceDigest = BulkPoisonInfluenceMapV1.ComputeReferenceStateDigest(
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
        BulkPoisonInfluenceMapEntryV1[] entries = targets
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
            targets,
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
            referenceDigest,
            entries);
        return Require(BulkPoisonInfluenceMapV1.TryCreate(
            1,
            BulkPoisonInfluenceMapV1.ApprovedPoisonSourceId,
            BulkPoisonInfluenceMapV1.ApprovedMapId,
            BulkPoisonInfluenceMapV1.ApprovedTopologyId,
            targets,
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
            referenceDigest,
            entries,
            mapDigest));
    }

    private static BulkPoisonStateV1 CreatePoisonState(
        BulkPoisonInfluenceMapV1 map,
        BulkPoisonModeV1 mode,
        double massKg,
        double updateTimeSeconds)
    {
        double concentration = massKg / map.ModeratorVolumeM3;
        Digest32 stateDigest = BulkPoisonStateV1.ComputeDigest(
            1,
            map.PoisonSourceId,
            true,
            mode,
            massKg,
            map.ModeratorVolumeM3,
            concentration,
            map.ReferenceConcentrationKgPerM3,
            map.AddRateKgPerSecond,
            map.WithdrawRateKgPerSecond,
            map.MapId,
            map.DataVersion,
            map.MapDigest,
            updateTimeSeconds);
        return Require(BulkPoisonStateV1.TryCreate(
            1,
            map.PoisonSourceId,
            true,
            mode,
            massKg,
            map.ModeratorVolumeM3,
            map.ReferenceConcentrationKgPerM3,
            map.AddRateKgPerSecond,
            map.WithdrawRateKgPerSecond,
            map.MapId,
            map.DataVersion,
            map.MapDigest,
            updateTimeSeconds,
            stateDigest));
    }

    private static VersionLifecycleV1 CreateLifecycle()
    {
        SyntheticCoreFixture fixture = SyntheticFixtures.CreateTwoChannelThreePosition();
        return Require(VersionLifecycleV1.TryCreate(
            fixture.Configuration,
            fixture.Inventory,
            fixture.Inventory.EnumerateOccupied()
                .Select(bundle => new BundleNuclideVersionV1(bundle.BundleId, 0, 0)),
            Digest(0xe1)));
    }

    private static byte[] ProjectionEvidence(RrsControllerProjectionV1 projection)
    {
        return EvidenceBytes(
            "rrs-projection",
            projection.ProjectionDigest.ToArray(),
            PackComponents(projection.Commands.Select(command => command.ToCanonicalBytes())));
    }

    private static byte[] ActuatorStateEvidence(P6T06ActuatorStateV1 state)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        writer.Write((byte)state.Family);
        byte[] targetBytes = state.Target.ToTargetBytes();
        writer.Write(targetBytes.Length);
        writer.Write(targetBytes);
        writer.Write(state.PhysicalState);
        writer.Write(state.RateLimitPerSecond);
        return stream.ToArray();
    }

    private static byte[] PackComponents(IEnumerable<byte[]> components)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        byte[][] values = components.ToArray();
        writer.Write(values.Length);
        foreach (byte[] value in values)
        {
            writer.Write(value.Length);
            writer.Write(value);
        }

        return stream.ToArray();
    }

    private static RrsOverlayValueV1 RequireRrsValue(
        RrsOverlayEvaluationV1 evaluation,
        NodeKey targetNode,
        ushort groupIndex)
    {
        RrsOverlayValueV1? value = evaluation.Find(targetNode, groupIndex);
        Assert.NotNull(value);
        return value!;
    }

    private static byte[] EvidenceBytes(string scenarioId, params byte[][] components)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream, Encoding.UTF8, true);
        writer.Write("P6-T07-EVIDENCE-V1");
        writer.Write(scenarioId);
        writer.Write(components.Length);
        foreach (byte[] component in components)
        {
            writer.Write(component.Length);
            writer.Write(component);
        }

        return stream.ToArray();
    }

    private static byte[] EvidenceBytes(
        string scenarioId,
        IEnumerable<byte[]> components)
    {
        return EvidenceBytes(scenarioId, components.ToArray());
    }

    private void WriteTrace(string scenarioId, ScenarioTrace trace)
    {
        _output.WriteLine(
            "P6_T07_" + scenarioId.ToUpperInvariant().Replace('-', '_') + "_DIGEST=" + Hex(trace.Digest));
    }

    private static Digest32 DigestOf(IEnumerable<byte[]> values)
    {
        return new Digest32(SHA256.HashData(values.SelectMany(value => value).ToArray()));
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

    private static Digest32 Digest(byte value)
    {
        return new Digest32(Enumerable.Repeat(value, 32).ToArray());
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }

    private sealed class RrsFixture
    {
        public RrsFixture(
            RrsRegionSetV1 regionSet,
            RrsInfluenceMapV1 map,
            RrsControllerStateV1 state)
        {
            RegionSet = regionSet;
            Map = map;
            State = state;
        }

        public RrsRegionSetV1 RegionSet { get; }

        public RrsInfluenceMapV1 Map { get; }

        public RrsControllerStateV1 State { get; }
    }

    private sealed class RrsControllerAdmissionFixture
    {
        public RrsControllerAdmissionFixture(
            RrsFixture fixture,
            RrsMeasurementSnapshotV1 measurement,
            RrsControllerProjectionV1 projection,
            P6T06QueueStateV1 queue,
            P6T06RrsControllerEventIdentityV1 eventIdentity)
        {
            Fixture = fixture;
            Measurement = measurement;
            Projection = projection;
            Queue = queue;
            EventIdentity = eventIdentity;
        }

        public RrsFixture Fixture { get; }

        public RrsMeasurementSnapshotV1 Measurement { get; }

        public RrsControllerProjectionV1 Projection { get; }

        public P6T06QueueStateV1 Queue { get; }

        public P6T06RrsControllerEventIdentityV1 EventIdentity { get; }
    }

    private sealed class ScenarioTrace
    {
        public ScenarioTrace(
            byte[] bytes,
            double totalCommand,
            double tiltCommand,
            double physicalStateBeforeDue,
            double physicalStateAfterDue,
            double totalOverlay,
            double leftOverlay,
            double rightOverlay,
            double appliedDeltaFraction,
            P6T06SaturationStateV1 saturationState,
            double finalFillFraction,
            double finalAvailableCommand,
            double insertionFraction,
            double withdrawalFraction,
            double insertionOverlay = 0.0,
            double withdrawalOverlay = 0.0,
            double massAfterAddKg = 0.0,
            double massAfterRecoveryKg = 0.0,
            double concentrationAfterRecoveryKgPerM3 = 0.0)
        {
            Bytes = bytes;
            Digest = new Digest32(SHA256.HashData(bytes));
            TotalCommand = totalCommand;
            TiltCommand = tiltCommand;
            PhysicalStateBeforeDue = physicalStateBeforeDue;
            PhysicalStateAfterDue = physicalStateAfterDue;
            TotalOverlay = totalOverlay;
            LeftOverlay = leftOverlay;
            RightOverlay = rightOverlay;
            AppliedDeltaFraction = appliedDeltaFraction;
            SaturationState = saturationState;
            FinalFillFraction = finalFillFraction;
            FinalAvailableCommand = finalAvailableCommand;
            InsertionFraction = insertionFraction;
            WithdrawalFraction = withdrawalFraction;
            InsertionOverlay = insertionOverlay;
            WithdrawalOverlay = withdrawalOverlay;
            MassAfterAddKg = massAfterAddKg;
            MassAfterRecoveryKg = massAfterRecoveryKg;
            ConcentrationAfterRecoveryKgPerM3 = concentrationAfterRecoveryKgPerM3;
        }

        public byte[] Bytes { get; }

        public Digest32 Digest { get; }

        public double TotalCommand { get; }

        public double TiltCommand { get; }

        public double PhysicalStateBeforeDue { get; }

        public double PhysicalStateAfterDue { get; }

        public double TotalOverlay { get; }

        public double LeftOverlay { get; }

        public double RightOverlay { get; }

        public double AppliedDeltaFraction { get; }

        public P6T06SaturationStateV1 SaturationState { get; }

        public double FinalFillFraction { get; }

        public double FinalAvailableCommand { get; }

        public double InsertionFraction { get; }

        public double WithdrawalFraction { get; }

        public double InsertionOverlay { get; }

        public double WithdrawalOverlay { get; }

        public double RecoveryOverlay
        {
            get { return InsertionOverlay; }
        }

        public double AddOverlay
        {
            get { return WithdrawalOverlay; }
        }

        public double MassAfterAddKg { get; }

        public double MassAfterRecoveryKg { get; }

        public double ConcentrationAfterRecoveryKgPerM3 { get; }
    }
}
