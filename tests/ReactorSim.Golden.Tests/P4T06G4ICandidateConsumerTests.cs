using System.Security.Cryptography;
using System.Text.Json;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Golden.Tests;

public sealed class P4T06G4ICandidateConsumerTests
{
    private const string TestArtifactDirectory = "P4T06G4IData";
    private const string DefinitionFileName = "manufactured-definition-v1.json";
    private const string ArtifactFileName = "manufactured-authority-v1.json";
    private const string ManifestFileName = "manufactured-authority-v1.manifest.json";

    [Fact]
    public void CandidateRetainsExplicitNonApprovalBoundaryAndHashBinding()
    {
        using JsonDocument artifact = ReadJson(ArtifactFileName);
        using JsonDocument manifest = ReadJson(ManifestFileName);
        JsonElement root = artifact.RootElement;
        JsonElement manifestRoot = manifest.RootElement;

        Assert.Equal("reactorsim.g4i-manufactured-spatial-authority/v1", StringValue(root, "format"));
        Assert.Equal("P4-T06-G4I", StringValue(root, "task_id"));
        Assert.Equal("candidate", StringValue(root, "status"));
        Assert.Equal("synthetic", StringValue(root, "evidence_class"));
        Assert.Equal("Synthetic", StringValue(root, "coverage_class"));
        Assert.Equal("Candidate", StringValue(root, "evidence_approval"));
        Assert.Equal("Deferred", StringValue(root, "comparison_status"));
        Assert.Equal("Deferred", StringValue(root, "tolerance_status"));
        Assert.Equal("NoGolden", StringValue(root, "golden_status"));
        Assert.Equal("Synthetic", StringValue(root, "validation_domain"));
        Assert.Equal("NotApplicable", StringValue(root.GetProperty("nuclear_data"), "library"));
        Assert.False(root.GetProperty("nuclear_data").GetProperty("external_data_used").GetBoolean());
        Assert.NotEmpty(StringValue(root.GetProperty("source_authority"), "source_program"));
        Assert.NotEmpty(StringValue(root.GetProperty("source_authority"), "source_version"));
        Assert.NotEmpty(StringValue(root.GetProperty("source_authority"), "build_identity"));
        Assert.NotEmpty(StringValue(root.GetProperty("source_authority"), "source_commit"));
        Assert.NotEmpty(StringValue(root.GetProperty("source_authority"), "coupling_tool_identity"));

        string actualDefinitionHash = HashFor(DefinitionFileName);
        string actualArtifactHash = HashFor(ArtifactFileName);
        Assert.Equal(actualDefinitionHash, StringValue(root, "definition_sha256"));
        Assert.Equal(actualDefinitionHash, StringValue(manifestRoot, "definition_sha256"));
        Assert.Equal(actualArtifactHash, StringValue(manifestRoot, "artifact_sha256"));
        Assert.Equal("candidate", StringValue(manifestRoot, "disposition"));
        Assert.Equal(
            "data/comparisons/p4-t06-g4i-manufactured-authority-v1.json",
            StringValue(root, "evidence_path"));
    }

    [Fact]
    public void CoreConsumerReproducesManufacturedStateAsDiagnosticCandidateEvidence()
    {
        using JsonDocument artifact = ReadJson(ArtifactFileName);
        JsonElement root = artifact.RootElement;
        SpatialSolveResult solved = SolveCore(root);

        Assert.Equal(SpatialSolveStatus.Converged, solved.Status);
        Assert.True(solved.HasUsableState);
        Assert.Equal(4, solved.FinalState!.Group1Flux.Count);
        Assert.Equal(4, solved.FinalState.Group2Flux.Count);
        Assert.Equal(
            StringValue(root.GetProperty("convergence").GetProperty("independent_reproduction"), "convergence_reason"),
            solved.Diagnostics.ConvergenceReason);

        JsonElement manufactured = root.GetProperty("manufactured_solution");
        double eigenvalueDifference = Math.Abs(
            solved.FinalState.Eigenvalue - manufactured.GetProperty("exact_eigenvalue").GetDouble());
        double powerDifference = Math.Abs(
            solved.FinalState.TotalPowerW - root.GetProperty("normalization").GetProperty("target_power_w").GetDouble());
        Assert.True(eigenvalueDifference <= 1e-10, $"k_difference={eigenvalueDifference:R}");
        Assert.True(powerDifference <= 1e-10, $"power_difference={powerDifference:R}");

        double maximumFluxDifference = 0.0;
        double[] expectedGroup1 = ReadArray(manufactured.GetProperty("exact_group1_flux"));
        double[] expectedGroup2 = ReadArray(manufactured.GetProperty("exact_group2_flux"));
        for (int index = 0; index < 4; index++)
        {
            maximumFluxDifference = Math.Max(
                maximumFluxDifference,
                Math.Abs(solved.FinalState.Group1Flux[index] - expectedGroup1[index]));
            maximumFluxDifference = Math.Max(
                maximumFluxDifference,
                Math.Abs(solved.FinalState.Group2Flux[index] - expectedGroup2[index]));
        }

        Assert.InRange(maximumFluxDifference, 0.0, 1e-10);
        Assert.NotNull(solved.Diagnostics.ResidualRelativeInfinity);
        Assert.True(double.IsFinite(solved.Diagnostics.ResidualRelativeInfinity!.Value));
        Assert.True(double.IsFinite(solved.Diagnostics.PowerBalanceRelative!.Value));
    }

    internal static SpatialSolveResult SolveCore(JsonElement root)
    {
        SpatialStencil stencil = CreateStencil(root.GetProperty("geometry"));
        SpatialCoefficientSet coefficients = CreateCoefficients(
            root.GetProperty("geometry"),
            root.GetProperty("coefficients"),
            stencil);
        JsonElement convergence = root.GetProperty("convergence");
        ContractValidationResult<SpatialLinearSolvePolicy> linearPolicy =
            SpatialLinearSolvePolicy.TryCreate(
                SpatialLinearSolvePolicy.DeterministicJacobiMethodId,
                SpatialLinearSolvePolicy.DeterministicJacobiMethodVersion,
                convergence.GetProperty("inner_absolute_residual_tolerance").GetDouble(),
                convergence.GetProperty("inner_relative_residual_tolerance").GetDouble(),
                convergence.GetProperty("inner_maximum_iterations").GetInt32());
        AssertValid(linearPolicy);

        JsonElement state = root.GetProperty("state");
        JsonElement normalization = root.GetProperty("normalization");
        ContractValidationResult<SpatialEigenIteration> iteration =
            SpatialEigenIteration.TryCreate(
                stencil,
                coefficients,
                linearPolicy.Value,
                normalization.GetProperty("target_power_w").GetDouble(),
                state.GetProperty("initial_eigenvalue").GetDouble(),
                ReadArray(state.GetProperty("initial_group1_flux")),
                ReadArray(state.GetProperty("initial_group2_flux")));
        AssertValid(iteration);

        ContractValidationResult<SpatialConvergencePolicy> convergencePolicy =
            SpatialConvergencePolicy.TryCreate(
                convergence.GetProperty("outer_k_absolute_tolerance").GetDouble(),
                convergence.GetProperty("outer_k_relative_tolerance").GetDouble(),
                convergence.GetProperty("outer_residual_tolerance").GetDouble(),
                convergence.GetProperty("outer_source_shape_tolerance").GetDouble(),
                convergence.GetProperty("outer_power_balance_tolerance").GetDouble(),
                convergence.GetProperty("outer_maximum_iterations").GetInt32());
        AssertValid(convergencePolicy);

        ContractValidationResult<SpatialEigenSolve> solve =
            SpatialEigenSolve.TryCreate(iteration.Value, convergencePolicy.Value);
        AssertValid(solve);
        ContractValidationResult<SpatialSolveResult> result = solve.Value.TrySolve();
        AssertValid(result);
        return result.Value;
    }

    private static SpatialStencil CreateStencil(JsonElement geometry)
    {
        var channels = new List<ChannelTopology>();
        for (int channelIndex = 0; channelIndex < 2; channelIndex++)
        {
            var channelId = new ChannelId((uint)channelIndex);
            var neighbors = new List<NeighborRecord>();
            var boundaries = new List<BoundaryFaceRecord>();
            foreach (JsonElement edge in geometry.GetProperty("edges").EnumerateArray())
            {
                JsonElement endpointA = edge.GetProperty("endpoint_a");
                JsonElement endpointB = edge.GetProperty("endpoint_b");
                if (endpointA.GetProperty("channel_id").GetInt32() == channelIndex)
                {
                    neighbors.Add(new NeighborRecord(
                        NodeChannel(endpointA),
                        NodePosition(endpointA),
                        NodeChannel(endpointB),
                        NodePosition(endpointB),
                        ParseDirection(StringValue(edge, "direction_a_to_b"))));
                    if (endpointB.GetProperty("channel_id").GetInt32() == channelIndex)
                    {
                        neighbors.Add(new NeighborRecord(
                            NodeChannel(endpointB),
                            NodePosition(endpointB),
                            NodeChannel(endpointA),
                            NodePosition(endpointA),
                            ParseDirection(StringValue(edge, "direction_b_to_a"))));
                    }
                }
                else if (endpointB.GetProperty("channel_id").GetInt32() == channelIndex)
                {
                    neighbors.Add(new NeighborRecord(
                        NodeChannel(endpointB),
                        NodePosition(endpointB),
                        NodeChannel(endpointA),
                        NodePosition(endpointA),
                        ParseDirection(StringValue(edge, "direction_b_to_a"))));
                }
            }

            foreach (JsonElement boundary in geometry.GetProperty("boundaries").EnumerateArray())
            {
                JsonElement node = boundary.GetProperty("node");
                if (node.GetProperty("channel_id").GetInt32() == channelIndex)
                {
                    boundaries.Add(new BoundaryFaceRecord(
                        NodeChannel(node),
                        NodePosition(node),
                        ParseFace(StringValue(boundary, "face")),
                        ParseClassification(StringValue(boundary, "classification"))));
                }
            }

            channels.Add(new ChannelTopology(
                channelId,
                channelIndex,
                0,
                FlowDirection.EndAtoEndB,
                new BundlePosition(0),
                new BundlePosition(1),
                neighbors,
                boundaries));
        }

        ContractValidationResult<CoreTopology> topology = CoreTopology.TryCreate(2, 2, channels);
        AssertValid(topology);
        ContractValidationResult<SpatialStencil> stencil = SpatialStencil.TryCreate(topology.Value);
        AssertValid(stencil);
        return stencil.Value;
    }

    private static SpatialCoefficientSet CreateCoefficients(
        JsonElement geometry,
        JsonElement coefficientArray,
        SpatialStencil stencil)
    {
        var nodes = new List<SpatialNodeCoefficients>();
        foreach (JsonElement coefficient in coefficientArray.EnumerateArray())
        {
            NodeKey node = new(
                new ChannelId((uint)coefficient.GetProperty("channel_id").GetInt32()),
                new BundlePosition((uint)coefficient.GetProperty("position").GetInt32()));
            nodes.Add(new SpatialNodeCoefficients(
                node,
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

    internal static JsonDocument ReadJson(string fileName)
    {
        return JsonDocument.Parse(File.ReadAllBytes(TestArtifactPath(fileName)));
    }

    internal static string HashFor(string fileName)
    {
        return Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(TestArtifactPath(fileName)))).ToLowerInvariant();
    }

    private static string TestArtifactPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, TestArtifactDirectory, fileName);
    }

    internal static string StringValue(JsonElement parent, string propertyName)
    {
        return parent.GetProperty(propertyName).GetString() ??
            throw new InvalidOperationException("JSON property is null: " + propertyName);
    }

    internal static double[] ReadArray(JsonElement value)
    {
        return value.EnumerateArray().Select(item => item.GetDouble()).ToArray();
    }

    private static void AssertValid<T>(ContractValidationResult<T> result)
        where T : class
    {
        Assert.True(result.IsValid, result.IsValid ? string.Empty : result.FirstDiagnostic.ToString());
    }
}
