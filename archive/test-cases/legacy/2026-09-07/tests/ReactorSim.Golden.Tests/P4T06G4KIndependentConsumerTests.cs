using System.Text.Json;
using System.Security.Cryptography;
using ReactorSim.Core;
using ReactorSim.TestInfrastructure;
using Xunit;

namespace ReactorSim.Golden.Tests;

public sealed class P4T06G4KIndependentConsumerTests
{
    private readonly ITestOutputHelper _output;

    public P4T06G4KIndependentConsumerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private const string TestArtifactDirectory = "P4T06G4KData";
    private const string ArtifactFileName = "independent-authority-v1.json";
    private const string ApprovedArtifactFileName = "approved-independent-authority-v1.json";

    [Fact]
    public void CandidateBindsIndependentAuthorityAndFiveScenarioCoverage()
    {
        using JsonDocument artifact = ReadJson(ArtifactFileName);
        using JsonDocument manifest = ReadJson("independent-authority-v1.manifest.json");
        JsonElement root = artifact.RootElement;
        JsonElement manifestRoot = manifest.RootElement;

        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(ReadArtifactBytes())).ToLowerInvariant(),
            StringValue(manifestRoot, "artifact_sha256"));
        Assert.Equal("reactorsim.g4k-independent-reduced-authority/v1", StringValue(root, "format"));
        Assert.Equal("P4-T06-G4K", StringValue(root, "task_id"));
        Assert.Equal("candidate", StringValue(root, "status"));
        Assert.Equal("Candidate", StringValue(root, "evidence_approval"));
        Assert.Equal("Deferred", StringValue(root, "comparison_status"));
        Assert.Equal("Deferred", StringValue(root, "tolerance_status"));
        Assert.Equal("NoGolden", StringValue(root, "golden_status"));
        Assert.Equal("RepresentativeReducedModel", StringValue(root, "coverage_class"));
        Assert.Equal("ReducedModel", StringValue(root, "validation_domain"));
        Assert.Equal("m^-2 s^-1", StringValue(root.GetProperty("units"), "flux"));
        Assert.False(root.GetProperty("input_authority").GetProperty("expected_outputs_reused").GetBoolean());
        Assert.Equal(
            StringValue(root.GetProperty("input_authority"), "definition_sha256"),
            StringValue(manifestRoot, "definition_sha256"));
        Assert.Equal(
            StringValue(root.GetProperty("input_authority"), "source_pack_sha256"),
            StringValue(manifestRoot, "source_pack_sha256"));
        Assert.Equal(
            StringValue(root.GetProperty("input_authority"), "independent_manifest_sha256"),
            StringValue(manifestRoot, "independent_definition_sha256"));
        Assert.Equal(
            StringValue(root.GetProperty("source_authority"), "source_snapshot_sha256"),
            StringValue(manifestRoot, "source_snapshot_sha256"));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("tolerance").GetProperty("diagnostic_bound").ValueKind);
        Assert.Contains("dense linear algebra", StringValue(root.GetProperty("reference_method"), "implementation"));
        Assert.Contains("no ReactorSim.Core", StringValue(root.GetProperty("source_authority"), "coupling_tool_identity"));
        Assert.Contains("G4J Program.cs", StringValue(root.GetProperty("source_authority"), "coupling_tool_identity"));
        Assert.Equal(48, root.GetProperty("geometry").GetProperty("nodes").GetArrayLength());
        Assert.Equal(92, root.GetProperty("geometry").GetProperty("edges").GetArrayLength());
        Assert.Equal(104, root.GetProperty("geometry").GetProperty("boundaries").GetArrayLength());
        Assert.Equal(5, root.GetProperty("scenarios").GetArrayLength());
        Assert.Equal("NotCovered", StringValue(root.GetProperty("state").GetProperty("kinetics_xenon"), "status"));

        string[] expectedIds =
        [
            "fresh_start",
            "equilibrium_like",
            "refuelled_4_bundle_shift",
            "rrs_tilt_perturbation",
            "bulk_poison_perturbation"
        ];
        for (int index = 0; index < expectedIds.Length; index++)
        {
            JsonElement scenario = root.GetProperty("scenarios")[index];
            Assert.Equal(expectedIds[index], StringValue(scenario, "scenario_id"));
            Assert.Equal("converged", StringValue(scenario.GetProperty("independent_reproduction"), "convergence_reason"));
            Assert.True(scenario.GetProperty("expected_independent").GetProperty("reference_matrix_residual_relative_infinity").GetDouble() < 1e-10);
            Assert.True(scenario.GetProperty("expected_independent").GetProperty("reference_matrix_condition_number_inf").GetDouble() < 10.0);
            Assert.True(scenario.GetProperty("expected_independent").GetProperty("dominant_eigenvalue_gap").GetDouble() > 0.002);
            Assert.Equal(48, scenario.GetProperty("coefficients").GetArrayLength());

            JsonElement fluxRecord = scenario.GetProperty("observable_records")
                .EnumerateArray()
                .Single(record => StringValue(record, "observable_id") == "spatial.flux");
            Assert.Equal("m^-2 s^-1", StringValue(fluxRecord, "unit"));
            JsonElement fluxValues = fluxRecord.GetProperty("values");
            Assert.Equal(96, fluxValues.GetArrayLength());
            for (int fluxIndex = 0; fluxIndex < fluxValues.GetArrayLength(); fluxIndex++)
            {
                JsonElement component = fluxValues[fluxIndex];
                int nodeIndex = fluxIndex / 2;
                Assert.Equal(nodeIndex / 12, component.GetProperty("channel_id").GetInt32());
                Assert.Equal(nodeIndex % 12, component.GetProperty("position").GetInt32());
                Assert.Equal(fluxIndex % 2 + 1, component.GetProperty("group").GetInt32());
            }
        }

        Assert.Equal("TowardEndB", StringValue(root.GetProperty("scenarios")[2].GetProperty("event_history")[2], "shift_direction"));
        Assert.Equal("TowardEndA", StringValue(root.GetProperty("scenarios")[2].GetProperty("event_history")[3], "shift_direction"));
        Assert.Equal("rrs", StringValue(root.GetProperty("scenarios")[3].GetProperty("overlay"), "kind"));
        Assert.Equal("bulk_poison", StringValue(root.GetProperty("scenarios")[4].GetProperty("overlay"), "kind"));

        JsonElement profiles = root.GetProperty("tolerance").GetProperty("profiles");
        Assert.Equal(6, profiles.EnumerateObject().Count());
        Assert.Equal("spatial.flux", root.GetProperty("observable_contract").GetProperty("order")[1].GetString());
        Assert.Equal("spatial.power", root.GetProperty("observable_contract").GetProperty("order")[2].GetString());
        Assert.Equal("m^-2 s^-1", StringValue(profiles.GetProperty("spatial.flux"), "unit"));
        Assert.Equal("P2-T05-rule-P4-T06-G4K-spatial-power-v1", StringValue(profiles.GetProperty("spatial.power"), "comparison_rule_id"));
        foreach (JsonProperty profile in profiles.EnumerateObject())
        {
            string expectedApproval = profile.Name == "spatial.convergence" ? "Deferred" : "Provisional";
            Assert.Equal(expectedApproval, StringValue(profile.Value, "approval_status"));
            Assert.Equal("G4", StringValue(profile.Value, "owner_gate"));
            Assert.Matches("^[0-9a-f]{64}$", StringValue(profile.Value, "data_digest"));
        }
    }

    [Fact]
    public void CoreConsumerComparesAgainstFrozenIndependentExpectedValues()
    {
        using JsonDocument artifact = ReadJson(ArtifactFileName);
        JsonElement root = artifact.RootElement;

        foreach (JsonElement scenario in root.GetProperty("scenarios").EnumerateArray())
        {
            SpatialSolveResult solved = SolveCore(root, scenario);
            Assert.Equal(SpatialSolveStatus.Converged, solved.Status);
            Assert.True(solved.HasUsableState);
            Assert.Equal(48, solved.FinalState!.Group1Flux.Count);
            Assert.Equal(48, solved.FinalState.Group2Flux.Count);

            JsonElement expected = scenario.GetProperty("expected_independent");
            Assert.Equal(
                StringValue(scenario.GetProperty("independent_reproduction"), "convergence_reason"),
                solved.Diagnostics.ConvergenceReason);

            double eigenvalueDifference = Math.Abs(
                solved.FinalState.Eigenvalue - expected.GetProperty("eigenvalue").GetDouble());
            double powerDifference = Math.Abs(
                solved.FinalState.TotalPowerW - expected.GetProperty("total_power_w").GetDouble());
            double[] expectedGroup1 = ReadArray(expected.GetProperty("group1_flux"));
            double[] expectedGroup2 = ReadArray(expected.GetProperty("group2_flux"));
            double[] expectedNodePower = ReadArray(expected.GetProperty("node_power_w"));
            double maximumFluxDifference = 0.0;
            double maximumNodePowerDifference = 0.0;
            for (int index = 0; index < expectedGroup1.Length; index++)
            {
                maximumFluxDifference = Math.Max(
                    maximumFluxDifference,
                    Math.Abs(solved.FinalState.Group1Flux[index] - expectedGroup1[index]));
                maximumFluxDifference = Math.Max(
                    maximumFluxDifference,
                    Math.Abs(solved.FinalState.Group2Flux[index] - expectedGroup2[index]));
                JsonElement coefficient = scenario.GetProperty("coefficients")[index];
                double coreNodePower = coefficient.GetProperty("volume_m3").GetDouble()
                    * coefficient.GetProperty("energy_per_fission_j").GetDouble()
                    * (coefficient.GetProperty("fission_group1_per_m").GetDouble()
                        * solved.FinalState.Group1Flux[index]
                        + coefficient.GetProperty("fission_group2_per_m").GetDouble()
                        * solved.FinalState.Group2Flux[index]);
                double nodePowerDifference = Math.Abs(coreNodePower - expectedNodePower[index]);
                maximumNodePowerDifference = Math.Max(
                    maximumNodePowerDifference,
                    nodePowerDifference);
            }

            double residualDifference = Math.Abs(
                solved.Diagnostics.ResidualRelativeInfinity!.Value
                - expected.GetProperty("residual_relative_infinity").GetDouble());

            _output.WriteLine(
                $"{StringValue(scenario, "scenario_id")}: " +
                $"eigenvalueDifference={eigenvalueDifference:R}; " +
                $"totalPowerDifference={powerDifference:R}; " +
                $"maximumFluxDifference={maximumFluxDifference:R}; " +
                $"maximumNodePowerDifference={maximumNodePowerDifference:R}; " +
                $"residualDifference={residualDifference:R}; " +
                $"coreResidual={solved.Diagnostics.ResidualRelativeInfinity:R}; " +
                $"coreIterations={solved.Diagnostics.IterationCount}");

            Assert.True(double.IsFinite(eigenvalueDifference));
            Assert.True(double.IsFinite(powerDifference));
            Assert.True(double.IsFinite(maximumFluxDifference));
            Assert.True(double.IsFinite(maximumNodePowerDifference));
            Assert.True(double.IsFinite(residualDifference));
            Assert.True(solved.Diagnostics.ResidualRelativeInfinity.HasValue);
            Assert.True(double.IsFinite(solved.Diagnostics.ResidualRelativeInfinity!.Value));
            Assert.True(double.IsFinite(solved.Diagnostics.PowerBalanceRelative!.Value));
        }
    }

    [Fact]
    public void ApprovedConsumerBindsProfilesAndFiveScenarioCoverage()
    {
        using JsonDocument artifact = ReadJson(ApprovedArtifactFileName);
        using JsonDocument manifest = ReadJson("approved-independent-authority-v1.manifest.json");
        JsonElement root = artifact.RootElement;
        JsonElement manifestRoot = manifest.RootElement;

        Assert.Equal("approved_golden", StringValue(root, "status"));
        Assert.Equal("Approved", StringValue(root, "evidence_approval"));
        Assert.Equal("Approved", StringValue(root, "comparison_status"));
        Assert.Equal("Approved", StringValue(root, "tolerance_status"));
        Assert.Equal("ApprovedGolden", StringValue(root, "golden_status"));
        Assert.Equal("RepresentativeReducedModel", StringValue(root, "coverage_class"));
        Assert.Equal("ReducedModel", StringValue(root, "validation_domain"));
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(ReadApprovedArtifactBytes())).ToLowerInvariant(),
            StringValue(manifestRoot, "approved_artifact_sha256"));
        Assert.Equal("approved", StringValue(manifestRoot, "disposition"));
        Assert.Equal("G4-R6", StringValue(root.GetProperty("tolerance"), "owner_gate"));

        JsonElement profiles = root.GetProperty("tolerance").GetProperty("profiles");
        Assert.Equal(6, profiles.EnumerateObject().Count());
        foreach (JsonProperty profile in profiles.EnumerateObject())
        {
            Assert.Equal("Approved", StringValue(profile.Value, "approval_status"));
            Assert.Equal("G4", StringValue(profile.Value, "owner_gate"));
            Assert.Matches("^[0-9a-f]{64}$", StringValue(profile.Value, "data_digest"));
            if (profile.Name == "spatial.convergence")
            {
                Assert.Equal(JsonValueKind.Null, profile.Value.GetProperty("absolute_tolerance").ValueKind);
                Assert.Equal(JsonValueKind.Null, profile.Value.GetProperty("relative_tolerance").ValueKind);
            }
        }

        foreach (JsonElement scenario in root.GetProperty("scenarios").EnumerateArray())
        {
            SpatialSolveResult solved = SolveCore(root, scenario);
            Assert.Equal(SpatialSolveStatus.Converged, solved.Status);
            Assert.True(solved.HasUsableState);
            JsonElement expected = scenario.GetProperty("expected_independent");
            double[] expectedGroup1 = ReadArray(expected.GetProperty("group1_flux"));
            double[] expectedGroup2 = ReadArray(expected.GetProperty("group2_flux"));
            double[] expectedNodePower = ReadArray(expected.GetProperty("node_power_w"));
            double maximumFluxDifference = 0.0;
            double maximumNodePowerDifference = 0.0;
            for (int index = 0; index < expectedGroup1.Length; index++)
            {
                maximumFluxDifference = Math.Max(
                    maximumFluxDifference,
                    Math.Abs(solved.FinalState!.Group1Flux[index] - expectedGroup1[index]));
                maximumFluxDifference = Math.Max(
                    maximumFluxDifference,
                    Math.Abs(solved.FinalState.Group2Flux[index] - expectedGroup2[index]));
                JsonElement coefficient = scenario.GetProperty("coefficients")[index];
                double coreNodePower = coefficient.GetProperty("volume_m3").GetDouble()
                    * coefficient.GetProperty("energy_per_fission_j").GetDouble()
                    * (coefficient.GetProperty("fission_group1_per_m").GetDouble()
                        * solved.FinalState.Group1Flux[index]
                        + coefficient.GetProperty("fission_group2_per_m").GetDouble()
                        * solved.FinalState.Group2Flux[index]);
                double nodePowerDifference = Math.Abs(coreNodePower - expectedNodePower[index]);
                maximumNodePowerDifference = Math.Max(
                    maximumNodePowerDifference,
                    nodePowerDifference);
                Assert.True(
                    WithinProfile(
                        nodePowerDifference,
                        expectedNodePower[index],
                        profiles.GetProperty("spatial.power")),
                    $"{StringValue(scenario, "scenario_id")}: node power index {index} exceeded the approved scalar profile.");
            }

            double kDifference = Math.Abs(
                solved.FinalState!.Eigenvalue - expected.GetProperty("eigenvalue").GetDouble());
            double totalPowerDifference = Math.Abs(
                solved.FinalState.TotalPowerW - expected.GetProperty("total_power_w").GetDouble());
            double residualDifference = Math.Abs(
                solved.Diagnostics.ResidualRelativeInfinity!.Value
                - expected.GetProperty("residual_relative_infinity").GetDouble());

            Assert.True(WithinProfile(kDifference, expected.GetProperty("eigenvalue").GetDouble(), profiles.GetProperty("spatial.k")));
            Assert.True(WithinProfile(maximumFluxDifference, Math.Max(expectedGroup1.Max(), expectedGroup2.Max()), profiles.GetProperty("spatial.flux")));
            Assert.True(WithinProfile(maximumNodePowerDifference, expectedNodePower.Max(), profiles.GetProperty("spatial.power")));
            Assert.True(WithinProfile(totalPowerDifference, expected.GetProperty("total_power_w").GetDouble(), profiles.GetProperty("spatial.total_power")));
            Assert.True(WithinProfile(residualDifference, expected.GetProperty("residual_relative_infinity").GetDouble(), profiles.GetProperty("spatial.residual_relative_inf")));
            Assert.Equal("converged", StringValue(scenario.GetProperty("independent_reproduction"), "convergence_reason"));
            Assert.Equal("converged", solved.Diagnostics.ConvergenceReason);
        }
    }

    private static SpatialSolveResult SolveCore(JsonElement root, JsonElement scenario)
    {
        SpatialStencil stencil = CreateStencil(root.GetProperty("geometry"));
        SpatialCoefficientSet coefficients = CreateCoefficients(
            scenario.GetProperty("coefficients"),
            root.GetProperty("geometry"),
            stencil);
        JsonElement policy = root.GetProperty("policies");
        ContractValidationResult<SpatialLinearSolvePolicy> linearPolicy =
            SpatialLinearSolvePolicy.TryCreate(
                SpatialLinearSolvePolicy.DeterministicJacobiMethodId,
                SpatialLinearSolvePolicy.DeterministicJacobiMethodVersion,
                policy.GetProperty("inner_absolute_residual_tolerance").GetDouble(),
                policy.GetProperty("inner_relative_residual_tolerance").GetDouble(),
                policy.GetProperty("inner_maximum_iterations").GetInt32());
        AssertValid(linearPolicy);

        JsonElement initial = root.GetProperty("state").GetProperty("initial_state");
        double[] initialGroup1 = Enumerable.Repeat(
            initial.GetProperty("initial_group1_flux").GetDouble(),
            stencil.NodeCount).ToArray();
        double[] initialGroup2 = Enumerable.Repeat(
            initial.GetProperty("initial_group2_flux").GetDouble(),
            stencil.NodeCount).ToArray();
        ContractValidationResult<SpatialEigenIteration> iteration =
            SpatialEigenIteration.TryCreate(
                stencil,
                coefficients,
                linearPolicy.Value,
                initial.GetProperty("target_power_w").GetDouble(),
                initial.GetProperty("initial_eigenvalue").GetDouble(),
                initialGroup1,
                initialGroup2);
        AssertValid(iteration);

        ContractValidationResult<SpatialConvergencePolicy> convergence =
            SpatialConvergencePolicy.TryCreate(
                policy.GetProperty("outer_k_absolute_tolerance").GetDouble(),
                policy.GetProperty("outer_k_relative_tolerance").GetDouble(),
                policy.GetProperty("outer_residual_tolerance").GetDouble(),
                policy.GetProperty("outer_source_shape_tolerance").GetDouble(),
                policy.GetProperty("outer_power_balance_tolerance").GetDouble(),
                policy.GetProperty("outer_maximum_iterations").GetInt32());
        AssertValid(convergence);

        ContractValidationResult<SpatialEigenSolve> solve =
            SpatialEigenSolve.TryCreate(iteration.Value, convergence.Value);
        AssertValid(solve);
        ContractValidationResult<SpatialSolveResult> result = solve.Value.TrySolve();
        AssertValid(result);
        return result.Value;
    }

    private static SpatialStencil CreateStencil(JsonElement geometry)
    {
        int channelCount = geometry.GetProperty("channel_count").GetInt32();
        int positionCount = geometry.GetProperty("bundle_position_count").GetInt32();
        var channels = new List<ChannelTopology>();
        foreach (JsonElement channel in geometry.GetProperty("channels").EnumerateArray())
        {
            int channelId = channel.GetProperty("channel_id").GetInt32();
            var neighbors = new List<NeighborRecord>();
            foreach (JsonElement edge in geometry.GetProperty("edges").EnumerateArray())
            {
                JsonElement a = edge.GetProperty("endpoint_a");
                JsonElement b = edge.GetProperty("endpoint_b");
                if (a.GetProperty("channel_id").GetInt32() == channelId)
                {
                    neighbors.Add(new NeighborRecord(
                        NodeChannel(a), NodePosition(a), NodeChannel(b), NodePosition(b),
                        ParseDirection(StringValue(edge, "direction_a_to_b"))));
                }
                if (b.GetProperty("channel_id").GetInt32() == channelId)
                {
                    neighbors.Add(new NeighborRecord(
                        NodeChannel(b), NodePosition(b), NodeChannel(a), NodePosition(a),
                        ParseDirection(StringValue(edge, "direction_b_to_a"))));
                }
            }

            var boundaries = new List<BoundaryFaceRecord>();
            foreach (JsonElement boundary in geometry.GetProperty("boundaries").EnumerateArray())
            {
                JsonElement node = boundary.GetProperty("node");
                if (node.GetProperty("channel_id").GetInt32() == channelId)
                {
                    boundaries.Add(new BoundaryFaceRecord(
                        NodeChannel(node), NodePosition(node),
                        ParseFace(StringValue(boundary, "face")),
                        ParseClassification(StringValue(boundary, "classification"))));
                }
            }

            FlowDirection flow = ParseFlow(StringValue(channel, "flow_direction"));
            uint inlet = flow == FlowDirection.EndAtoEndB ? 0U : (uint)(positionCount - 1);
            uint outlet = flow == FlowDirection.EndAtoEndB ? (uint)(positionCount - 1) : 0U;
            channels.Add(new ChannelTopology(
                new ChannelId((uint)channelId),
                channel.GetProperty("coordinate_x").GetInt32(),
                channel.GetProperty("coordinate_y").GetInt32(),
                flow,
                new BundlePosition(inlet),
                new BundlePosition(outlet),
                neighbors,
                boundaries));
        }

        ContractValidationResult<CoreTopology> topology = CoreTopology.TryCreate(
            (uint)channelCount, (uint)positionCount, channels);
        AssertValid(topology);
        ContractValidationResult<SpatialStencil> stencil = SpatialStencil.TryCreate(topology.Value);
        AssertValid(stencil);
        return stencil.Value;
    }

    private static SpatialCoefficientSet CreateCoefficients(
        JsonElement coefficients,
        JsonElement geometry,
        SpatialStencil stencil)
    {
        var nodes = new List<SpatialNodeCoefficients>();
        foreach (JsonElement coefficient in coefficients.EnumerateArray())
        {
            nodes.Add(new SpatialNodeCoefficients(
                new NodeKey(
                    new ChannelId((uint)coefficient.GetProperty("channel_id").GetInt32()),
                    new BundlePosition((uint)coefficient.GetProperty("position").GetInt32())),
                coefficient.GetProperty("volume_m3").GetDouble(),
                coefficient.GetProperty("absorption_group1_per_m").GetDouble(),
                coefficient.GetProperty("absorption_group2_per_m").GetDouble(),
                coefficient.GetProperty("downscatter_group1_to2_per_m").GetDouble(),
                coefficient.GetProperty("fission_group1_per_m").GetDouble(),
                coefficient.GetProperty("fission_group2_per_m").GetDouble(),
                coefficient.GetProperty("nu_fission_group1_per_m").GetDouble(),
                coefficient.GetProperty("nu_fission_group2_per_m").GetDouble(),
                coefficient.GetProperty("chi_group1").GetDouble(),
                coefficient.GetProperty("chi_group2").GetDouble(),
                coefficient.GetProperty("energy_per_fission_j").GetDouble()));
        }

        var edges = new List<SpatialEdgeConductance>();
        foreach (JsonElement edge in geometry.GetProperty("edges").EnumerateArray())
        {
            edges.Add(new SpatialEdgeConductance(
                NodeKey(edge.GetProperty("endpoint_a")),
                NodeKey(edge.GetProperty("endpoint_b")),
                edge.GetProperty("group1_m2").GetDouble(),
                edge.GetProperty("group2_m2").GetDouble()));
        }

        var boundaries = new List<SpatialBoundaryConductance>();
        foreach (JsonElement boundary in geometry.GetProperty("boundaries").EnumerateArray())
        {
            boundaries.Add(new SpatialBoundaryConductance(
                NodeKey(boundary.GetProperty("node")),
                ParseFace(StringValue(boundary, "face")),
                boundary.GetProperty("group1_m2").GetDouble(),
                boundary.GetProperty("group2_m2").GetDouble()));
        }

        ContractValidationResult<SpatialCoefficientSet> result =
            SpatialCoefficientSet.TryCreate(stencil, nodes, edges, boundaries);
        AssertValid(result);
        return result.Value;
    }

    private static NodeKey NodeKey(JsonElement node)
    {
        return new NodeKey(NodeChannel(node), NodePosition(node));
    }

    private static ChannelId NodeChannel(JsonElement node)
    {
        return new ChannelId((uint)node.GetProperty("channel_id").GetInt32());
    }

    private static BundlePosition NodePosition(JsonElement node)
    {
        return new BundlePosition((uint)node.GetProperty("position").GetInt32());
    }

    private static NeighborDirection ParseDirection(string value)
    {
        return value switch
        {
            "North" => NeighborDirection.North,
            "East" => NeighborDirection.East,
            "South" => NeighborDirection.South,
            "West" => NeighborDirection.West,
            "TowardEndA" => NeighborDirection.TowardEndA,
            "TowardEndB" => NeighborDirection.TowardEndB,
            _ => throw new InvalidOperationException("Unknown neighbor direction " + value)
        };
    }

    private static FlowDirection ParseFlow(string value)
    {
        return value switch
        {
            "EndAtoEndB" => FlowDirection.EndAtoEndB,
            "EndBtoEndA" => FlowDirection.EndBtoEndA,
            _ => throw new InvalidOperationException("Unknown flow direction " + value)
        };
    }

    private static TopologyFace ParseFace(string value)
    {
        return value switch
        {
            "North" => TopologyFace.North,
            "East" => TopologyFace.East,
            "South" => TopologyFace.South,
            "West" => TopologyFace.West,
            "EndA" => TopologyFace.EndA,
            "EndB" => TopologyFace.EndB,
            _ => throw new InvalidOperationException("Unknown topology face " + value)
        };
    }

    private static BoundaryClassification ParseClassification(string value)
    {
        return value switch
        {
            "Reflective" => BoundaryClassification.Reflective,
            "Vacuum" => BoundaryClassification.Vacuum,
            _ => throw new InvalidOperationException("Unknown boundary classification " + value)
        };
    }

    private static JsonDocument ReadJson(string fileName)
    {
        return JsonDocument.Parse(File.ReadAllBytes(
            TestDataLocator.RequireFile(Path.Combine(TestArtifactDirectory, fileName))));
    }

    private static byte[] ReadArtifactBytes()
    {
        return File.ReadAllBytes(TestDataLocator.RequireFile(
            Path.Combine(TestArtifactDirectory, ArtifactFileName)));
    }

    private static byte[] ReadApprovedArtifactBytes()
    {
        return File.ReadAllBytes(TestDataLocator.RequireFile(
            Path.Combine(TestArtifactDirectory, ApprovedArtifactFileName)));
    }

    private static bool WithinProfile(double error, double expected, JsonElement profile)
    {
        double absolute = profile.GetProperty("absolute_tolerance").GetDouble();
        double relative = profile.GetProperty("relative_tolerance").GetDouble();
        double scale = profile.GetProperty("reference_scale").ValueKind == JsonValueKind.Null
            ? Math.Abs(expected)
            : profile.GetProperty("reference_scale").GetDouble();
        double denominator = Math.Max(Math.Abs(expected), scale);
        return error <= absolute || error / denominator <= relative;
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

    private static void AssertValid<T>(ContractValidationResult<T> result)
        where T : class
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
    }
}
