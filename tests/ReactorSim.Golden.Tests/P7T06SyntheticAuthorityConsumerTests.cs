using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using ReactorSim.Core;
using ReactorSim.TestInfrastructure;
using Xunit;

namespace ReactorSim.Golden.Tests;

public sealed class P7T06SyntheticAuthorityConsumerTests
{
    private const string TestArtifactDirectory = "P7T06Data";

    [Fact]
    public void CandidateRetainsDeferredExternalBoundaryAndExactManifestBinding()
    {
        using JsonDocument definition = ReadJson("synthetic-definition-v1.json");
        using JsonDocument artifact = ReadJson("candidate-authority-v1.json");
        using JsonDocument manifest = ReadJson("candidate-authority-v1.manifest.json");

        JsonElement root = artifact.RootElement;
        JsonElement manifestRoot = manifest.RootElement;
        Assert.Equal("reactorsim.p7-t06-synthetic-authority/v1", StringValue(root, "format"));
        Assert.Equal("P7-T06", StringValue(root, "task_id"));
        Assert.Equal("candidate", StringValue(root, "status"));
        Assert.Equal("Deferred", StringValue(root, "evidence_approval"));
        Assert.Equal("Deferred", StringValue(root, "comparison_status"));
        Assert.Equal("NotApplicable", StringValue(root, "tolerance_status"));
        Assert.Equal("NoGolden", StringValue(root, "golden_status"));
        Assert.Equal("Synthetic", StringValue(root, "coverage_class"));
        Assert.Equal("not_admitted_deferred", StringValue(root.GetProperty("source_authority"), "external_case_status"));
        Assert.Equal(
            HashFor("synthetic-definition-v1.json"),
            StringValue(root, "definition_sha256"));
        Assert.Equal(
            HashFor("synthetic-definition-v1.json"),
            StringValue(manifestRoot, "definition_sha256"));
        Assert.Equal(
            HashFor("candidate-authority-v1.json"),
            StringValue(manifestRoot, "artifact_sha256"));
        Assert.Equal(
            File.ReadAllBytes(TestArtifactPath("candidate-authority-v1.json")).Length,
            manifestRoot.GetProperty("artifact_byte_length").GetInt32());
        Assert.Equal(
            StringValue(definition.RootElement.GetProperty("source_authority"), "research_report"),
            StringValue(manifestRoot, "research_report"));
    }

    [Fact]
    public void ApprovedGoldenBindsSyntheticOnlyScopeAndApprovedManifest()
    {
        using JsonDocument artifact = ReadJson("approved-authority-v1.json");
        using JsonDocument manifest = ReadJson("approved-authority-v1.manifest.json");
        JsonElement root = artifact.RootElement;

        Assert.Equal("approved_golden", StringValue(root, "status"));
        Assert.Equal("Approved", StringValue(root, "evidence_approval"));
        Assert.Equal("Approved", StringValue(root, "comparison_status"));
        Assert.Equal("ApprovedGolden", StringValue(root, "golden_status"));
        Assert.Equal("Synthetic", StringValue(root, "coverage_class"));
        Assert.Equal("Synthetic", StringValue(root, "validation_domain"));
        Assert.Contains("not a direct CANDU physics baseline", StringValue(root, "approval_scope"));
        Assert.Equal("approved_golden", StringValue(manifest.RootElement, "disposition"));
        Assert.Equal(
            HashFor("approved-authority-v1.json"),
            StringValue(manifest.RootElement, "artifact_sha256"));
        Assert.True(manifest.RootElement.GetProperty("independent_repeat_equal").GetBoolean());
        Assert.Equal(0.0, manifest.RootElement.GetProperty("maximum_balance_absolute").GetDouble());
        Assert.True(manifest.RootElement.GetProperty("minimum_nonnegative_value").GetDouble() >= 0.0);
    }

    [Fact]
    public void ApprovedGoldenReplaysKineticsAndIxeHistoriesThroughCoreContracts()
    {
        using JsonDocument artifact = ReadJson("approved-authority-v1.json");
        JsonElement root = artifact.RootElement;
        JsonElement binding = root.GetProperty("state_binding");
        JsonElement kineticInputs = root.GetProperty("kinetics").GetProperty("inputs");

        DelayedNeutronDataV1 kineticData = Require(DelayedNeutronDataV1.TryCreate(
            StableId.Parse("76000000-0000-4000-8000-000000000002"),
            StringValue(kineticInputs, "data_version"),
            ParseDigest(StringValue(kineticInputs, "data_digest")),
            kineticInputs.GetProperty("prompt_generation_time_seconds").GetDouble(),
            kineticInputs.GetProperty("groups").EnumerateArray().Select(group =>
                Require(DelayedNeutronGroupV1.TryCreate(
                    group.GetProperty("group_index").GetInt32(),
                    group.GetProperty("beta_fraction").GetDouble(),
                    group.GetProperty("decay_constant_per_second").GetDouble())))));

        double[] initialPrecursor = ReadArray(kineticInputs.GetProperty("initial_precursor"));
        KineticStateV1 kineticState = Require(KineticStateV1.TryCreate(
            0.0,
            kineticInputs.GetProperty("initial_amplitude").GetDouble(),
            initialPrecursor,
            kineticInputs.GetProperty("initial_amplitude").GetDouble(),
            initialPrecursor,
            kineticInputs.GetProperty("reference_power_watts").GetDouble(),
            kineticData,
            OptionalStableId.Applicable(StableId.Parse(StringValue(binding, "spatial_solve_id"))),
            OptionalUInt64.Applicable(binding.GetProperty("spatial_state_version").GetUInt64()),
            kineticInputs.GetProperty("spatial_reactivity").GetDouble(),
            OptionalDigest32.Applicable(ParseDigest(StringValue(binding, "feedback_overlay_digest"))),
            0));

        foreach (JsonElement expected in root.GetProperty("kinetics").GetProperty("history").EnumerateArray())
        {
            KineticIntegrationResultV1 result = Require(
                KineticIntegrationTransitionV1.TryApply(
                    kineticState,
                    kineticData,
                    expected.GetProperty("delta_time_seconds").GetDouble()));

            Assert.Equal(expected.GetProperty("time_before_seconds").GetDouble(), result.Step.SimulationTimeSeconds);
            Assert.Equal(expected.GetProperty("time_after_seconds").GetDouble(), result.Step.SimulationTimeAfterSeconds);
            Assert.Equal(expected.GetProperty("amplitude_before").GetDouble(), result.Step.AmplitudeBefore);
            Assert.Equal(expected.GetProperty("amplitude_after").GetDouble(), result.Step.AmplitudeAfter);
            Assert.Equal(expected.GetProperty("prompt_derivative").GetDouble(), result.Step.PromptDerivative);
            Assert.Equal(expected.GetProperty("delayed_source").GetDouble(), result.Step.DelayedSource);
            Assert.Equal(expected.GetProperty("amplitude_derivative").GetDouble(), result.Step.AmplitudeDerivative);
            AssertSequenceEqual(ReadArray(expected.GetProperty("precursor_before")), result.Step.PrecursorBefore);
            AssertSequenceEqual(ReadArray(expected.GetProperty("precursor_after")), result.Step.PrecursorAfter);
            AssertSequenceEqual(ReadArray(expected.GetProperty("precursor_derivative")), result.Step.PrecursorDerivative);
            Assert.Equal(StringValue(expected, "data_id"), result.Step.DataId.ToString());
            Assert.Equal(StringValue(expected, "data_version"), result.Step.DataVersion);
            Assert.Equal(ParseDigest(StringValue(expected, "data_digest")), result.Step.DataDigest);
            Assert.Equal(StableId.Parse(StringValue(expected, "spatial_solve_id")), result.Step.SpatialSolveId.Value);
            Assert.Equal(expected.GetProperty("spatial_state_version").GetUInt64(), result.Step.SpatialStateVersion.Value);
            Assert.Equal(ParseDigest(StringValue(expected, "feedback_overlay_digest")), result.Step.FeedbackOverlayDigest.Value);

            kineticState = result.ResultingState;
        }

        JsonElement expectedFinalKinetics = root.GetProperty("kinetics").GetProperty("final");
        Assert.Equal(expectedFinalKinetics.GetProperty("amplitude_after").GetDouble(), kineticState.Amplitude);
        AssertSequenceEqual(ReadArray(expectedFinalKinetics.GetProperty("precursor_after")), kineticState.Precursor);

        JsonElement nuclideDataJson = root.GetProperty("nuclide_data");
        NuclideDataV1 nuclideData = Require(NuclideDataV1.TryCreate(
            new MaterialVariantId(StringValue(nuclideDataJson, "material_variant_id")),
            StringValue(nuclideDataJson, "data_id"),
            ParseDigest(StringValue(nuclideDataJson, "data_digest")),
            nuclideDataJson.GetProperty("gamma_i").GetDouble(),
            nuclideDataJson.GetProperty("gamma_xe").GetDouble(),
            nuclideDataJson.GetProperty("lambda_i_per_second").GetDouble(),
            nuclideDataJson.GetProperty("lambda_xe_per_second").GetDouble(),
            nuclideDataJson.GetProperty("sigma_xe_group1_m2").GetDouble(),
            nuclideDataJson.GetProperty("sigma_xe_group2_m2").GetDouble()));

        int nodeIndex = 0;
        foreach (JsonElement node in root.GetProperty("nodes").EnumerateArray())
        {
            JsonElement nodeInputs = node.GetProperty("inputs");
            NuclideStateEnvelopeV1 state = Require(NuclideStateEnvelopeV1.TryCreateFresh(
                StableId.Parse(StringValue(node, "bundle_id")),
                nodeInputs.GetProperty("initial_i135_atoms").GetDouble(),
                nodeInputs.GetProperty("initial_xe135_atoms").GetDouble(),
                node.GetProperty("volume_m3").GetDouble(),
                0,
                nuclideData));

            foreach (JsonElement expected in node.GetProperty("history").EnumerateArray())
            {
                int stepIndex = expected.GetProperty("step_index").GetInt32();
                NuclideIntegrationInputV1 input = Require(NuclideIntegrationInputV1.TryCreate(
                    StepId(nodeIndex * 100 + stepIndex),
                    (ulong)stepIndex,
                    EventRankV1.KineticNuclideStep,
                    expected.GetProperty("time_before_seconds").GetDouble(),
                    expected.GetProperty("delta_time_seconds").GetDouble(),
                    binding.GetProperty("core_state_version").GetUInt64(),
                    expected.GetProperty("fission_rate_density_m3_s").GetDouble(),
                    expected.GetProperty("flux_group1_m2_s").GetDouble(),
                    expected.GetProperty("flux_group2_m2_s").GetDouble(),
                    nuclideData,
                    ParseDigest(StringValue(binding, "data_pack_digest"))));

                NuclideIntegrationResultV1 result = Require(
                    NuclideIntegrationTransitionV1.TryApply(state, input));
                NuclideTransitionRecordV1 transition = result.Transition;
                NuclideStateEnvelopeV1 next = result.ResultingState;

                Assert.Equal(expected.GetProperty("i135_before").GetDouble(), transition.I135AtomInventoryBefore);
                Assert.Equal(expected.GetProperty("i135_after").GetDouble(), transition.I135AtomInventoryAfter);
                Assert.Equal(expected.GetProperty("xe135_before").GetDouble(), transition.Xe135AtomInventoryBefore);
                Assert.Equal(expected.GetProperty("xe135_after").GetDouble(), transition.Xe135AtomInventoryAfter);
                Assert.Equal(expected.GetProperty("i135_number_density_before").GetDouble(), transition.I135NumberDensityBefore);
                Assert.Equal(expected.GetProperty("i135_number_density_after").GetDouble(), transition.I135NumberDensityAfter);
                Assert.Equal(expected.GetProperty("xe135_number_density_before").GetDouble(), transition.Xe135NumberDensityBefore);
                Assert.Equal(expected.GetProperty("xe135_number_density_after").GetDouble(), transition.Xe135NumberDensityAfter);
                Assert.Equal(expected.GetProperty("i135_direct_production_atoms_per_second").GetDouble(), transition.I135DirectProductionAtomsPerSecond);
                Assert.Equal(expected.GetProperty("i135_decay_loss_atoms_per_second").GetDouble(), transition.I135DecayLossAtomsPerSecond);
                Assert.Equal(expected.GetProperty("xe135_direct_production_atoms_per_second").GetDouble(), transition.Xe135DirectProductionAtomsPerSecond);
                Assert.Equal(expected.GetProperty("xe135_from_i135_decay_atoms_per_second").GetDouble(), transition.Xe135FromI135DecayAtomsPerSecond);
                Assert.Equal(expected.GetProperty("xe135_decay_loss_atoms_per_second").GetDouble(), transition.Xe135DecayLossAtomsPerSecond);
                Assert.Equal(expected.GetProperty("xe135_absorption_loss_atoms_per_second").GetDouble(), transition.Xe135AbsorptionLossAtomsPerSecond);
                Assert.Equal(ParseDigest(StringValue(expected, "data_digest")), transition.NuclideDataDigest);
                Assert.Equal(ParseDigest(StringValue(expected, "state_binding_digest")), transition.StateBindingDigest);

                Assert.Equal(
                    expected.GetProperty("dynamic_absorption_group1_per_m").GetDouble(),
                    nuclideData.SigmaXeGroup1M2 * next.Xe135NumberDensity);
                Assert.Equal(
                    expected.GetProperty("dynamic_absorption_group2_per_m").GetDouble(),
                    nuclideData.SigmaXeGroup2M2 * next.Xe135NumberDensity);
                Assert.True(next.I135AtomInventory >= 0.0);
                Assert.True(next.Xe135AtomInventory >= 0.0);
                state = next;
            }

            JsonElement expectedFinal = node.GetProperty("final");
            Assert.Equal(expectedFinal.GetProperty("i135_after").GetDouble(), state.I135AtomInventory);
            Assert.Equal(expectedFinal.GetProperty("xe135_after").GetDouble(), state.Xe135AtomInventory);
            Assert.Equal(nodeIndex, node.GetProperty("position").GetInt32());
            nodeIndex++;
        }
    }

    private static JsonDocument ReadJson(string fileName)
    {
        return JsonDocument.Parse(File.ReadAllBytes(TestArtifactPath(fileName)));
    }

    private static string TestArtifactPath(string fileName)
    {
        return TestDataLocator.RequireFile(Path.Combine(TestArtifactDirectory, fileName));
    }

    private static string HashFor(string fileName)
    {
        return Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(TestArtifactPath(fileName)))).ToLowerInvariant();
    }

    private static string StringValue(JsonElement parent, string propertyName)
    {
        return parent.GetProperty(propertyName).GetString() ??
            throw new InvalidOperationException("JSON property is null: " + propertyName);
    }

    private static double[] ReadArray(JsonElement value)
    {
        return value.EnumerateArray().Select(item => item.GetDouble()).ToArray();
    }

    private static void AssertSequenceEqual(double[] expected, IReadOnlyList<double> actual)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index], actual[index]);
        }
    }

    private static Digest32 ParseDigest(string value)
    {
        return new Digest32(Convert.FromHexString(value));
    }

    private static StableId StepId(int index)
    {
        return StableId.Parse(
            "76000000-0000-4000-8000-" + index.ToString("x12", CultureInfo.InvariantCulture));
    }

    private static T Require<T>(ContractValidationResult<T> result)
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
        return result.Value;
    }
}
