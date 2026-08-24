using System.Text.Json;
using ReactorSim.Core;
using Xunit;

namespace ReactorSim.Golden.Tests;

public sealed class P4T06G4JRepresentativeConsumerTests
{
    private const string TestArtifactDirectory = "P4T06G4JData";
    private const string ArtifactFileName = "representative-authority-v1.json";

    [Fact]
    public void CandidateBindsRepresentativeCoverageWithoutProductionApproval()
    {
        using JsonDocument artifact = ReadJson(ArtifactFileName);
        JsonElement root = artifact.RootElement;

        Assert.Equal("reactorsim.g4j-representative-reduced-authority/v1", StringValue(root, "format"));
        Assert.Equal("P4-T06-G4J", StringValue(root, "task_id"));
        Assert.Equal("candidate", StringValue(root, "status"));
        Assert.Equal("reduced_projection", StringValue(root, "evidence_class"));
        Assert.Equal("RepresentativeReducedModel", StringValue(root, "coverage_class"));
        Assert.Equal("ReducedModel", StringValue(root, "validation_domain"));
        Assert.Equal("Candidate", StringValue(root, "evidence_approval"));
        Assert.Equal("Deferred", StringValue(root, "comparison_status"));
        Assert.Equal("Deferred", StringValue(root, "tolerance_status"));
        Assert.Equal("NoGolden", StringValue(root, "golden_status"));
        Assert.Equal(48, root.GetProperty("geometry").GetProperty("nodes").GetArrayLength());
        Assert.Equal(92, root.GetProperty("geometry").GetProperty("edges").GetArrayLength());
        Assert.Equal(104, root.GetProperty("geometry").GetProperty("boundaries").GetArrayLength());
        Assert.Equal(5, root.GetProperty("scenarios").GetArrayLength());
        Assert.Equal("NotCovered", StringValue(root.GetProperty("state").GetProperty("kinetics_xenon"), "status"));
        Assert.Equal("Complete for five explicit scenarios, including two S4 shifts and prescribed branch events.",
            StringValue(root.GetProperty("authority_requirements"), "state_history_event_order"));

        JsonElement channels = root.GetProperty("geometry").GetProperty("channels");
        Assert.Equal("EndAtoEndB", StringValue(channels[0], "flow_direction"));
        Assert.Equal("EndBtoEndA", StringValue(channels[1], "flow_direction"));
        Assert.Equal("EndAtoEndB", StringValue(channels[2], "flow_direction"));
        Assert.Equal("EndBtoEndA", StringValue(channels[3], "flow_direction"));

        JsonElement refuel = root.GetProperty("scenarios")[2];
        Assert.Equal("refuelled_4_bundle_shift", StringValue(refuel, "scenario_id"));
        Assert.Equal(4, refuel.GetProperty("event_history")[2].GetProperty("inserted_positions").GetArrayLength());
        Assert.Equal("TowardEndB", StringValue(refuel.GetProperty("event_history")[2], "shift_direction"));
        Assert.Equal("TowardEndA", StringValue(refuel.GetProperty("event_history")[3], "shift_direction"));
        Assert.Equal(2, refuel.GetProperty("state").GetProperty("refuelling_audits").GetArrayLength());
        Assert.Equal(8, refuel.GetProperty("state").GetProperty("refuelling_audits")[0].GetProperty("moved_bundle_map").GetArrayLength());
        Assert.Equal(4, refuel.GetProperty("state").GetProperty("refuelling_audits")[0].GetProperty("discharge_records").GetArrayLength());

        Assert.Equal("rrs", StringValue(root.GetProperty("scenarios")[3].GetProperty("overlay"), "kind"));
        Assert.Equal("bulk_poison", StringValue(root.GetProperty("scenarios")[4].GetProperty("overlay"), "kind"));
        Assert.Equal(96, root.GetProperty("scenarios")[3].GetProperty("overlay").GetProperty("entries").GetArrayLength());
        Assert.Equal(96, root.GetProperty("scenarios")[4].GetProperty("overlay").GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public void CoreConsumerReproducesEveryReducedSequenceScenarioAsDiagnosticEvidence()
    {
        using JsonDocument artifact = ReadJson(ArtifactFileName);
        JsonElement root = artifact.RootElement;
        double diagnosticBound = root.GetProperty("tolerance").GetProperty("diagnostic_bound").GetDouble();
        Assert.Equal(1e-8, diagnosticBound);

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
            Assert.True(eigenvalueDifference <= diagnosticBound,
                $"{StringValue(scenario, "scenario_id")} k_difference={eigenvalueDifference:R}");
            Assert.True(powerDifference <= diagnosticBound,
                $"{StringValue(scenario, "scenario_id")} power_difference={powerDifference:R}");

            double[] expectedGroup1 = ReadArray(expected.GetProperty("group1_flux"));
            double[] expectedGroup2 = ReadArray(expected.GetProperty("group2_flux"));
            double maximumFluxDifference = 0.0;
            for (int index = 0; index < expectedGroup1.Length; index++)
            {
                maximumFluxDifference = Math.Max(
                    maximumFluxDifference,
                    Math.Abs(solved.FinalState.Group1Flux[index] - expectedGroup1[index]));
                maximumFluxDifference = Math.Max(
                    maximumFluxDifference,
                    Math.Abs(solved.FinalState.Group2Flux[index] - expectedGroup2[index]));
            }

            Assert.True(maximumFluxDifference <= diagnosticBound,
                $"{StringValue(scenario, "scenario_id")} flux_difference={maximumFluxDifference:R}");
            Assert.NotNull(solved.Diagnostics.ResidualRelativeInfinity);
            Assert.True(double.IsFinite(solved.Diagnostics.ResidualRelativeInfinity!.Value));
            Assert.True(double.IsFinite(solved.Diagnostics.PowerBalanceRelative!.Value));
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
            Path.Combine(AppContext.BaseDirectory, TestArtifactDirectory, fileName)));
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
