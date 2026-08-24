using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class Program
{
    private const string DefinitionFormat = "reactorsim.g4j-representative-reduced-definition/v1";
    private const string ArtifactFormat = "reactorsim.g4j-representative-reduced-authority/v1";
    private const string ManifestFormat = "reactorsim.g4j-representative-reduced-authority-manifest/v1";
    private const string TaskId = "P4-T06-G4J";
    private const string ArtifactId = "p4-t06-g4j-representative-authority-v1";
    private const string SourceCommit = "UNAVAILABLE_NO_GIT_HEAD";
    private const double HeavyMetalMassKg = 0.5;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 6 ||
                (args[0] != "generate" && args[0] != "validate"))
            {
                Console.Error.WriteLine(
                    "Usage: generate|validate <definition> <pack> <artifact> <manifest> <source_sha256>");
                return 1;
            }

            string sourceSnapshotSha256 = ValidateHash(args[5], "source_sha256");
            if (args[0] == "generate")
            {
                Generate(args[1], args[2], args[3], args[4], sourceSnapshotSha256);
            }
            else
            {
                Validate(args[1], args[2], args[3], args[4], sourceSnapshotSha256);
            }

            return 0;
        }
        catch (AuthorityFailure failure)
        {
            Console.Error.WriteLine(
                "P4_T06_G4J_FAILURE code=" + failure.Code +
                " path=" + failure.Path +
                " message=" + failure.Message);
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "P4_T06_G4J_FAILURE code=Unhandled.Exception path=tool message=" +
                exception.Message);
            return 3;
        }
    }

    private static void Generate(
        string definitionPath,
        string packPath,
        string artifactPath,
        string manifestPath,
        string sourceSnapshotSha256)
    {
        byte[] definitionBytes = ReadFile(definitionPath, "definition");
        byte[] packBytes = ReadFile(packPath, "source_pack");
        Definition definition = ParseDefinition(definitionBytes);
        PackRoot pack = ParsePack(packBytes, definition);
        GeneratedCase generated = BuildCase(definition, pack);
        byte[] artifactBytes = JsonSerializer.SerializeToUtf8Bytes(
            BuildArtifact(
                definition,
                generated,
                Hex(Sha256(definitionBytes)),
                Hex(Sha256(packBytes)),
                sourceSnapshotSha256),
            JsonOptions);
        byte[] manifestBytes = JsonSerializer.SerializeToUtf8Bytes(
            BuildManifest(
                definition,
                generated,
                Hex(Sha256(definitionBytes)),
                Hex(Sha256(packBytes)),
                Hex(Sha256(artifactBytes)),
                sourceSnapshotSha256),
            JsonOptions);

        WriteFile(artifactPath, artifactBytes);
        WriteFile(manifestPath, manifestBytes);

        Console.WriteLine(
            "P4_T06_G4J_GENERATE_PASS disposition=candidate" +
            " scenarios=" + generated.Scenarios.Count.ToString(CultureInfo.InvariantCulture) +
            " nodes=" + generated.Topology.Nodes.Count.ToString(CultureInfo.InvariantCulture) +
            " edges=" + generated.Topology.Edges.Count.ToString(CultureInfo.InvariantCulture) +
            " boundaries=" + generated.Topology.Boundaries.Count.ToString(CultureInfo.InvariantCulture) +
            " artifact_sha256=" + Hex(Sha256(artifactBytes)) +
            " manifest_sha256=" + Hex(Sha256(manifestBytes)) +
            " repeat_equal=" + generated.IndependentRepeatEqual);
    }

    private static void Validate(
        string definitionPath,
        string packPath,
        string artifactPath,
        string manifestPath,
        string sourceSnapshotSha256)
    {
        byte[] definitionBytes = ReadFile(definitionPath, "definition");
        byte[] packBytes = ReadFile(packPath, "source_pack");
        Definition definition = ParseDefinition(definitionBytes);
        PackRoot pack = ParsePack(packBytes, definition);
        GeneratedCase generated = BuildCase(definition, pack);
        byte[] expectedArtifactBytes = JsonSerializer.SerializeToUtf8Bytes(
            BuildArtifact(
                definition,
                generated,
                Hex(Sha256(definitionBytes)),
                Hex(Sha256(packBytes)),
                sourceSnapshotSha256),
            JsonOptions);
        byte[] actualArtifactBytes = ReadFile(artifactPath, "artifact");
        if (!CryptographicOperations.FixedTimeEquals(expectedArtifactBytes, actualArtifactBytes))
        {
            throw new AuthorityFailure(
                "Artifact.NonDeterministic",
                "artifact",
                "The stored artifact does not match deterministic regeneration.");
        }

        byte[] expectedManifestBytes = JsonSerializer.SerializeToUtf8Bytes(
            BuildManifest(
                definition,
                generated,
                Hex(Sha256(definitionBytes)),
                Hex(Sha256(packBytes)),
                Hex(Sha256(actualArtifactBytes)),
                sourceSnapshotSha256),
            JsonOptions);
        byte[] actualManifestBytes = ReadFile(manifestPath, "manifest");
        if (!CryptographicOperations.FixedTimeEquals(expectedManifestBytes, actualManifestBytes))
        {
            throw new AuthorityFailure(
                "Manifest.NonDeterministic",
                "manifest",
                "The stored manifest does not match deterministic regeneration.");
        }

        using JsonDocument parsed = JsonDocument.Parse(actualArtifactBytes);
        if (parsed.RootElement.GetProperty("status").GetString() != "candidate" ||
            parsed.RootElement.GetProperty("golden_status").GetString() != "NoGolden")
        {
            throw new AuthorityFailure(
                "Artifact.Status.Invalid",
                "artifact.status",
                "The stored artifact crossed the candidate-only boundary.");
        }

        Console.WriteLine(
            "P4_T06_G4J_VALIDATE_PASS disposition=candidate" +
            " scenarios=" + generated.Scenarios.Count.ToString(CultureInfo.InvariantCulture) +
            " artifact_sha256=" + Hex(Sha256(actualArtifactBytes)) +
            " independent_repeat_equal=" + generated.IndependentRepeatEqual);
    }

    private static Definition ParseDefinition(byte[] bytes)
    {
        Definition? definition = JsonSerializer.Deserialize<Definition>(bytes, JsonOptions);
        if (definition == null || definition.Format != DefinitionFormat ||
            definition.TaskId != TaskId || definition.Topology == null ||
            definition.SourcePack == null || definition.Conductances == null ||
            definition.NodeGeometry == null || definition.MaterialProjection == null ||
            definition.InitialState == null || definition.Policies == null ||
            definition.Scenarios == null)
        {
            throw new AuthorityFailure(
                "Definition.Shape.Invalid",
                "definition",
                "The definition must bind the task, source pack, topology, projection, scenarios, and policies.");
        }

        if (definition.Topology.ChannelCount != 4 ||
            definition.Topology.BundlePositionCount != 12 ||
            definition.Topology.Channels.Length != 4 ||
            definition.Topology.TransverseLinks.Length != 4 ||
            definition.Scenarios.Length != 5)
        {
            throw new AuthorityFailure(
                "Definition.Dimension.Invalid",
                "definition.topology",
                "G4J requires four channels, twelve positions, four transverse links, and five scenarios.");
        }

        ValidateFinitePositive(definition.Conductances.AxialEdgeGroup1M2, "axial_edge_group1_m2");
        ValidateFinitePositive(definition.Conductances.AxialEdgeGroup2M2, "axial_edge_group2_m2");
        ValidateFinitePositive(definition.Conductances.TransverseEdgeGroup1M2, "transverse_edge_group1_m2");
        ValidateFinitePositive(definition.Conductances.TransverseEdgeGroup2M2, "transverse_edge_group2_m2");
        ValidateFinitePositive(definition.Conductances.EndBoundaryGroup1M2, "end_boundary_group1_m2");
        ValidateFinitePositive(definition.Conductances.EndBoundaryGroup2M2, "end_boundary_group2_m2");
        ValidateArray(definition.MaterialProjection.ChannelAbsorptionGroup1Multiplier, 4, "channel_absorption_group1_multiplier");
        ValidateArray(definition.MaterialProjection.ChannelAbsorptionGroup2Multiplier, 4, "channel_absorption_group2_multiplier");
        ValidateArray(definition.MaterialProjection.ChannelFissionMultiplier, 4, "channel_fission_multiplier");
        ValidateArray(definition.MaterialProjection.ChannelDownscatterMultiplier, 4, "channel_downscatter_multiplier");
        ValidateArray(definition.MaterialProjection.PositionAbsorptionGroup1Multiplier, 12, "position_absorption_group1_multiplier");
        ValidateArray(definition.MaterialProjection.PositionAbsorptionGroup2Multiplier, 12, "position_absorption_group2_multiplier");
        ValidateArray(definition.MaterialProjection.PositionFissionMultiplier, 12, "position_fission_multiplier");
        ValidateArray(definition.MaterialProjection.PositionDownscatterMultiplier, 12, "position_downscatter_multiplier");
        ValidateFinitePositive(definition.InitialState.TargetPowerW, "target_power_w");
        ValidateFinitePositive(definition.NodeGeometry.VolumeBaseM3, "volume_base_m3");
        ValidateFinitePositive(definition.InitialState.InitialEigenvalue, "initial_eigenvalue");
        ValidatePolicies(definition.Policies);

        int[] channelIds = definition.Topology.Channels
            .Select(channel => channel.ChannelId)
            .OrderBy(value => value)
            .ToArray();
        if (!channelIds.SequenceEqual(new[] { 0, 1, 2, 3 }))
        {
            throw new AuthorityFailure(
                "Definition.ChannelIds.Invalid",
                "definition.topology.channels",
                "Channel IDs must be the explicit zero-based set 0..3.");
        }

        foreach (ScenarioDefinition scenario in definition.Scenarios)
        {
            if (string.IsNullOrWhiteSpace(scenario.ScenarioId) ||
                scenario.BaseBurnupByPositionJPerKgHm.Length != 12 ||
                scenario.ChannelBurnupOffsetJPerKgHm.Length != 4 ||
                scenario.FreshPositionsByChannel.Length != 4 ||
                scenario.Events.Length == 0 ||
                scenario.Overlay == null)
            {
                throw new AuthorityFailure(
                    "Definition.Scenario.Invalid",
                    "definition.scenarios",
                    "Every scenario must bind twelve-position burnup, four channel offsets, four fresh-position lists, overlay state, and event history.");
            }

            ValidateArray(scenario.BaseBurnupByPositionJPerKgHm, 12, scenario.ScenarioId + ".base_burnup");
            ValidateArray(scenario.ChannelBurnupOffsetJPerKgHm, 4, scenario.ScenarioId + ".channel_burnup_offset");
            foreach (int[] positions in scenario.FreshPositionsByChannel)
            {
                if (positions.Any(position => position < 0 || position >= 12))
                {
                    throw new AuthorityFailure(
                        "Definition.Scenario.FreshPosition.Invalid",
                        "definition.scenarios[" + scenario.ScenarioId + "]",
                        "Fresh positions must be explicit valid bundle positions.");
                }
            }

            ValidateOverlay(scenario.Overlay, scenario.ScenarioId);
        }

        return definition;
    }

    private static PackRoot ParsePack(byte[] bytes, Definition definition)
    {
        string actualHash = Hex(Sha256(bytes));
        if (!actualHash.Equals(definition.SourcePack.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new AuthorityFailure(
                "SourcePack.HashMismatch",
                "definition.source_pack.sha256",
                "The reduced candidate pack does not match the immutable source identity.");
        }

        PackRoot? pack = JsonSerializer.Deserialize<PackRoot>(bytes, JsonOptions);
        if (pack == null || pack.Tables.Length != 1 ||
            pack.Tables[0].TableId != definition.SourcePack.TableId ||
            pack.Tables[0].MaterialVariantId != definition.SourcePack.MaterialVariantId ||
            pack.Tables[0].Rows.Length < 2)
        {
            throw new AuthorityFailure(
                "SourcePack.Shape.Invalid",
                "source_pack",
                "The source pack must contain the named single material table with at least two rows.");
        }

        PackRow[] rows = pack.Tables[0].Rows;
        for (int index = 1; index < rows.Length; index++)
        {
            if (rows[index - 1].BurnupJPerKgHm >= rows[index].BurnupJPerKgHm)
            {
                throw new AuthorityFailure(
                    "SourcePack.BurnupOrder.Invalid",
                    "source_pack.tables[0].rows",
                    "Source pack burnup knots must be strictly increasing.");
            }
        }

        return pack;
    }

    private static GeneratedCase BuildCase(Definition definition, PackRoot pack)
    {
        TopologyModel topology = BuildTopology(definition);
        PackRow[] rows = pack.Tables[0].Rows;
        var scenarios = new List<ScenarioGenerated>(definition.Scenarios.Length);
        bool repeatsEqual = true;

        foreach (ScenarioDefinition scenario in definition.Scenarios)
        {
            var nodes = new NodeModel[topology.Nodes.Count];
            var burnupByNode = new double[topology.Nodes.Count];
            for (int channel = 0; channel < definition.Topology.ChannelCount; channel++)
            {
                for (int position = 0; position < definition.Topology.BundlePositionCount; position++)
                {
                    int index = FlatIndex(channel, position, definition.Topology.BundlePositionCount);
                    double burnup = ComputeScenarioBurnup(scenario, channel, position);

                    if (burnup < 0.0 || burnup > rows[^1].BurnupJPerKgHm)
                    {
                        throw new AuthorityFailure(
                            "Scenario.Burnup.OutOfRange",
                            "scenario[" + scenario.ScenarioId + "].burnup[" + index + "]",
                            "Scenario burnup must remain inside the source pack domain.");
                    }

                    PackCoefficients material = Lookup(rows, burnup);
                    OverlayValues overlay = ComputeOverlay(scenario.Overlay, channel);
                    double baseAbsorption1 = material.AbsorptionGroup1PerM *
                        definition.MaterialProjection.ChannelAbsorptionGroup1Multiplier[channel] *
                        definition.MaterialProjection.PositionAbsorptionGroup1Multiplier[position];
                    double baseAbsorption2 = material.AbsorptionGroup2PerM *
                        definition.MaterialProjection.ChannelAbsorptionGroup2Multiplier[channel] *
                        definition.MaterialProjection.PositionAbsorptionGroup2Multiplier[position];
                    double fissionMultiplier = definition.MaterialProjection.ChannelFissionMultiplier[channel] *
                        definition.MaterialProjection.PositionFissionMultiplier[position];
                    double downscatterMultiplier = definition.MaterialProjection.ChannelDownscatterMultiplier[channel] *
                        definition.MaterialProjection.PositionDownscatterMultiplier[position];
                    double fission1 = material.FissionGroup1PerM * fissionMultiplier;
                    double fission2 = material.FissionGroup2PerM * fissionMultiplier;
                    double nuFission1 = material.NuFissionGroup1PerM * fissionMultiplier;
                    double nuFission2 = material.NuFissionGroup2PerM * fissionMultiplier;
                    double downscatter = material.DownscatterGroup1To2PerM * downscatterMultiplier;
                    double volume = definition.NodeGeometry.VolumeBaseM3 +
                        channel * definition.NodeGeometry.VolumeChannelIncrementM3 +
                        position * definition.NodeGeometry.VolumePositionIncrementM3;

                    if (!double.IsFinite(baseAbsorption1) || !double.IsFinite(baseAbsorption2) ||
                        !double.IsFinite(fission1) || !double.IsFinite(fission2) ||
                        !double.IsFinite(nuFission1) || !double.IsFinite(nuFission2) ||
                        !double.IsFinite(downscatter) || baseAbsorption1 + overlay.Group1 < fission1 ||
                        baseAbsorption2 + overlay.Group2 < fission2 || volume <= 0.0)
                    {
                        throw new AuthorityFailure(
                            "Scenario.Coefficients.Invalid",
                            "scenario[" + scenario.ScenarioId + "].coefficients[" + index + "]",
                            "The projected effective coefficient set violates finite or absorption/fission invariants.");
                    }

                    burnupByNode[index] = burnup;
                    nodes[index] = new NodeModel
                    {
                        FlatIndex = index,
                        ChannelId = channel,
                        Position = position,
                        VolumeM3 = volume,
                        BurnupJPerKgHm = burnup,
                        BaseAbsorptionGroup1PerM = baseAbsorption1,
                        BaseAbsorptionGroup2PerM = baseAbsorption2,
                        OverlayAbsorptionGroup1PerM = overlay.Group1,
                        OverlayAbsorptionGroup2PerM = overlay.Group2,
                        AbsorptionGroup1PerM = baseAbsorption1 + overlay.Group1,
                        AbsorptionGroup2PerM = baseAbsorption2 + overlay.Group2,
                        DownscatterGroup1To2PerM = downscatter,
                        FissionGroup1PerM = fission1,
                        FissionGroup2PerM = fission2,
                        NuFissionGroup1PerM = nuFission1,
                        NuFissionGroup2PerM = nuFission2,
                        ChiGroup1 = material.ChiGroup1,
                        ChiGroup2 = 1.0 - material.ChiGroup1,
                        EnergyPerFissionJ = material.EnergyPerFissionJ,
                        MaterialBracket = FindBracket(rows, burnup)
                    };
                }
            }

            IndependentResult first = Solve(definition, topology, nodes);
            IndependentResult repeat = Solve(definition, topology, nodes);
            byte[] firstBytes = JsonSerializer.SerializeToUtf8Bytes(first, JsonOptions);
            byte[] repeatBytes = JsonSerializer.SerializeToUtf8Bytes(repeat, JsonOptions);
            bool repeatEqual = CryptographicOperations.FixedTimeEquals(firstBytes, repeatBytes);
            repeatsEqual &= repeatEqual;
            if (!repeatEqual)
            {
                throw new AuthorityFailure(
                    "IndependentRepeat.Mismatch",
                    "scenario[" + scenario.ScenarioId + "]",
                    "Repeated standalone solves were not byte-identical.");
            }

            scenarios.Add(new ScenarioGenerated
            {
                Definition = scenario,
                Nodes = nodes,
                BurnupByNode = burnupByNode,
                Independent = first,
                IndependentResultSha256 = Hex(Sha256(firstBytes)),
                IndependentRepeatResultSha256 = Hex(Sha256(repeatBytes)),
                IndependentRepeatEqual = repeatEqual
            });
        }

        return new GeneratedCase
        {
            Topology = topology,
            Scenarios = scenarios,
            IndependentRepeatEqual = repeatsEqual
        };
    }

    private static TopologyModel BuildTopology(Definition definition)
    {
        var nodes = new List<NodeModel>();
        for (int channel = 0; channel < definition.Topology.ChannelCount; channel++)
        {
            for (int position = 0; position < definition.Topology.BundlePositionCount; position++)
            {
                nodes.Add(new NodeModel
                {
                    FlatIndex = FlatIndex(channel, position, definition.Topology.BundlePositionCount),
                    ChannelId = channel,
                    Position = position
                });
            }
        }

        var edges = new List<EdgeModel>();
        for (int channel = 0; channel < definition.Topology.ChannelCount; channel++)
        {
            for (int position = 0; position + 1 < definition.Topology.BundlePositionCount; position++)
            {
                edges.Add(new EdgeModel
                {
                    EndpointA = new NodeRef { ChannelId = channel, Position = position, FlatIndex = FlatIndex(channel, position, 12) },
                    EndpointB = new NodeRef { ChannelId = channel, Position = position + 1, FlatIndex = FlatIndex(channel, position + 1, 12) },
                    DirectionAToB = "TowardEndB",
                    DirectionBToA = "TowardEndA",
                    Group1M2 = definition.Conductances.AxialEdgeGroup1M2,
                    Group2M2 = definition.Conductances.AxialEdgeGroup2M2
                });
            }
        }

        foreach (TransverseLinkDefinition link in definition.Topology.TransverseLinks)
        {
            for (int position = 0; position < definition.Topology.BundlePositionCount; position++)
            {
                edges.Add(new EdgeModel
                {
                    EndpointA = new NodeRef { ChannelId = link.EndpointAChannelId, Position = position, FlatIndex = FlatIndex(link.EndpointAChannelId, position, 12) },
                    EndpointB = new NodeRef { ChannelId = link.EndpointBChannelId, Position = position, FlatIndex = FlatIndex(link.EndpointBChannelId, position, 12) },
                    DirectionAToB = link.DirectionAToB,
                    DirectionBToA = link.DirectionBToA,
                    Group1M2 = definition.Conductances.TransverseEdgeGroup1M2,
                    Group2M2 = definition.Conductances.TransverseEdgeGroup2M2
                });
            }
        }

        var channelCoordinates = definition.Topology.Channels.ToDictionary(
            channel => channel.ChannelId,
            channel => channel);
        var boundaries = new List<BoundaryModel>();
        for (int channel = 0; channel < definition.Topology.ChannelCount; channel++)
        {
            ChannelDefinition channelDefinition = channelCoordinates[channel];
            for (int position = 0; position < definition.Topology.BundlePositionCount; position++)
            {
                int flatIndex = FlatIndex(channel, position, 12);
                if (channelDefinition.CoordinateX == 0)
                {
                    boundaries.Add(ReflectiveBoundary(channel, position, flatIndex, "West"));
                }

                if (channelDefinition.CoordinateX == 1)
                {
                    boundaries.Add(ReflectiveBoundary(channel, position, flatIndex, "East"));
                }

                if (channelDefinition.CoordinateY == 0)
                {
                    boundaries.Add(ReflectiveBoundary(channel, position, flatIndex, "South"));
                }

                if (channelDefinition.CoordinateY == 1)
                {
                    boundaries.Add(ReflectiveBoundary(channel, position, flatIndex, "North"));
                }

                if (position == 0)
                {
                    boundaries.Add(new BoundaryModel
                    {
                        Node = new NodeRef { ChannelId = channel, Position = position, FlatIndex = flatIndex },
                        Face = "EndA",
                        Classification = definition.Topology.EndBoundaryClassification,
                        Group1M2 = definition.Conductances.EndBoundaryGroup1M2,
                        Group2M2 = definition.Conductances.EndBoundaryGroup2M2
                    });
                }

                if (position == 11)
                {
                    boundaries.Add(new BoundaryModel
                    {
                        Node = new NodeRef { ChannelId = channel, Position = position, FlatIndex = flatIndex },
                        Face = "EndB",
                        Classification = definition.Topology.EndBoundaryClassification,
                        Group1M2 = definition.Conductances.EndBoundaryGroup1M2,
                        Group2M2 = definition.Conductances.EndBoundaryGroup2M2
                    });
                }
            }
        }

        return new TopologyModel { Nodes = nodes, Edges = edges, Boundaries = boundaries };
    }

    private static BoundaryModel ReflectiveBoundary(int channel, int position, int flatIndex, string face)
    {
        return new BoundaryModel
        {
            Node = new NodeRef { ChannelId = channel, Position = position, FlatIndex = flatIndex },
            Face = face,
            Classification = "Reflective",
            Group1M2 = 0.0,
            Group2M2 = 0.0
        };
    }

    private static PackCoefficients Lookup(PackRow[] rows, double burnup)
    {
        if (burnup <= rows[0].BurnupJPerKgHm)
        {
            return rows[0].Coefficients;
        }

        for (int index = 1; index < rows.Length; index++)
        {
            if (burnup <= rows[index].BurnupJPerKgHm)
            {
                PackRow left = rows[index - 1];
                PackRow right = rows[index];
                double alpha = (burnup - left.BurnupJPerKgHm) /
                    (right.BurnupJPerKgHm - left.BurnupJPerKgHm);
                return new PackCoefficients
                {
                    AbsorptionGroup1PerM = Lerp(left.Coefficients.AbsorptionGroup1PerM, right.Coefficients.AbsorptionGroup1PerM, alpha),
                    AbsorptionGroup2PerM = Lerp(left.Coefficients.AbsorptionGroup2PerM, right.Coefficients.AbsorptionGroup2PerM, alpha),
                    FissionGroup1PerM = Lerp(left.Coefficients.FissionGroup1PerM, right.Coefficients.FissionGroup1PerM, alpha),
                    FissionGroup2PerM = Lerp(left.Coefficients.FissionGroup2PerM, right.Coefficients.FissionGroup2PerM, alpha),
                    NuFissionGroup1PerM = Lerp(left.Coefficients.NuFissionGroup1PerM, right.Coefficients.NuFissionGroup1PerM, alpha),
                    NuFissionGroup2PerM = Lerp(left.Coefficients.NuFissionGroup2PerM, right.Coefficients.NuFissionGroup2PerM, alpha),
                    DownscatterGroup1To2PerM = Lerp(left.Coefficients.DownscatterGroup1To2PerM, right.Coefficients.DownscatterGroup1To2PerM, alpha),
                    ChiGroup1 = Lerp(left.Coefficients.ChiGroup1, right.Coefficients.ChiGroup1, alpha),
                    EnergyPerFissionJ = Lerp(left.Coefficients.EnergyPerFissionJ, right.Coefficients.EnergyPerFissionJ, alpha)
                };
            }
        }

        return rows[^1].Coefficients;
    }

    private static string FindBracket(PackRow[] rows, double burnup)
    {
        if (burnup <= rows[0].BurnupJPerKgHm)
        {
            return "0:0";
        }

        for (int index = 1; index < rows.Length; index++)
        {
            if (burnup <= rows[index].BurnupJPerKgHm)
            {
                return (index - 1).ToString(CultureInfo.InvariantCulture) + ":" +
                    index.ToString(CultureInfo.InvariantCulture);
            }
        }

        int last = rows.Length - 1;
        return last.ToString(CultureInfo.InvariantCulture) + ":" +
            last.ToString(CultureInfo.InvariantCulture);
    }

    private static double Lerp(double left, double right, double alpha)
    {
        return left + (right - left) * alpha;
    }

    private static OverlayValues ComputeOverlay(OverlayDefinition overlay, int channel)
    {
        if (overlay.Kind == "none")
        {
            return new OverlayValues();
        }

        if (overlay.Kind == "rrs")
        {
            double delta = overlay.StateFraction - overlay.ReferenceFraction;
            return new OverlayValues
            {
                Group1 = overlay.WeightsGroup1PerUnitByChannel[channel] * delta,
                Group2 = overlay.WeightsGroup2PerUnitByChannel[channel] * delta
            };
        }

        if (overlay.Kind == "bulk_poison")
        {
            double concentration = overlay.PoisonMassKg / overlay.ModeratorVolumeM3;
            double delta = concentration - overlay.ReferenceConcentrationKgPerM3;
            return new OverlayValues
            {
                Group1 = overlay.WeightsGroup1PerConcentrationByChannel[channel] * delta,
                Group2 = overlay.WeightsGroup2PerConcentrationByChannel[channel] * delta
            };
        }

        throw new AuthorityFailure(
            "Scenario.Overlay.Kind.Invalid",
            "scenario.overlay.kind",
            "Only none, rrs, and bulk_poison overlays are admitted by G4J.");
    }

    private static IndependentResult Solve(
        Definition definition,
        TopologyModel topology,
        NodeModel[] nodes)
    {
        PolicyDefinition policy = definition.Policies;
        int count = nodes.Length;
        double[] group1 = Enumerable.Repeat(definition.InitialState.InitialGroup1Flux, count).ToArray();
        double[] group2 = Enumerable.Repeat(definition.InitialState.InitialGroup2Flux, count).ToArray();
        double initialPower = ComputePower(nodes, group1, group2);
        Scale(group1, definition.InitialState.TargetPowerW / initialPower);
        Scale(group2, definition.InitialState.TargetPowerW / initialPower);
        double eigenvalue = definition.InitialState.InitialEigenvalue;
        double[] previousSourceShape = new double[count];
        IndependentResult? last = null;

        for (int outer = 0; outer < policy.OuterMaximumIterations; outer++)
        {
            double[] currentSource = ComputeFissionSource(nodes, group1, group2);
            double currentProduction = ComputeProduction(nodes, currentSource);
            if (!double.IsFinite(currentProduction) || currentProduction <= 0.0)
            {
                throw new AuthorityFailure("IndependentSolve.Source.Invalid", "independent_reproduction", "The fission production was not positive and finite.");
            }

            double[] group1Source = new double[count];
            for (int index = 0; index < count; index++)
            {
                group1Source[index] = nodes[index].ChiGroup1 * currentSource[index] / eigenvalue;
            }

            int group1InnerIterations;
            double[] trialGroup1 = InnerSolve(definition, topology, nodes, group1Source, 1, out group1InnerIterations);
            double[] group2Source = new double[count];
            for (int index = 0; index < count; index++)
            {
                group2Source[index] = nodes[index].DownscatterGroup1To2PerM * trialGroup1[index] +
                    nodes[index].ChiGroup2 * currentSource[index] / eigenvalue;
            }

            int group2InnerIterations;
            double[] trialGroup2 = InnerSolve(definition, topology, nodes, group2Source, 2, out group2InnerIterations);
            double trialProduction = ComputeProduction(nodes, ComputeFissionSource(nodes, trialGroup1, trialGroup2));
            double nextEigenvalue = eigenvalue * (trialProduction / currentProduction);
            double trialPower = ComputePower(nodes, trialGroup1, trialGroup2);
            double normalizationScale = definition.InitialState.TargetPowerW / trialPower;
            double[] nextGroup1 = (double[])trialGroup1.Clone();
            double[] nextGroup2 = (double[])trialGroup2.Clone();
            Scale(nextGroup1, normalizationScale);
            Scale(nextGroup2, normalizationScale);
            double[] nextSource = ComputeFissionSource(nodes, nextGroup1, nextGroup2);
            double nextProduction = ComputeProduction(nodes, nextSource);
            double[] nextSourceShape = new double[count];
            for (int index = 0; index < count; index++)
            {
                nextSourceShape[index] = nodes[index].VolumeM3 * nextSource[index] / nextProduction;
            }

            StateMetrics metrics = EvaluateState(definition, topology, nodes, nextGroup1, nextGroup2, nextEigenvalue);
            double deltaEigenvalueAbsolute = Math.Abs(nextEigenvalue - eigenvalue);
            double eigenvalueScale = Math.Max(Math.Abs(nextEigenvalue), Math.Abs(eigenvalue));
            double sourceShapeChange = 0.0;
            for (int index = 0; index < count; index++)
            {
                sourceShapeChange = Math.Max(sourceShapeChange, Math.Abs(nextSourceShape[index] - previousSourceShape[index]));
            }

            last = new IndependentResult
            {
                Converged = false,
                ConvergenceReason = "maximum_iterations_exhausted",
                OuterIterations = outer + 1,
                Group1InnerIterations = group1InnerIterations,
                Group2InnerIterations = group2InnerIterations,
                Eigenvalue = nextEigenvalue,
                NormalizationScale = normalizationScale,
                TotalPowerW = ComputePower(nodes, nextGroup1, nextGroup2),
                Group1Flux = nextGroup1,
                Group2Flux = nextGroup2,
                ResidualAbsoluteInfinity = metrics.ResidualAbsoluteInfinity,
                ResidualRelativeInfinity = metrics.ResidualRelativeInfinity,
                SourceShapeChangeInfinity = sourceShapeChange,
                PowerBalanceRelative = metrics.PowerBalanceRelative,
                FissionSourceByNode = nextSource,
                NodePowerW = ComputeNodePower(nodes, nextGroup1, nextGroup2)
            };

            bool eigenvalueConverged = deltaEigenvalueAbsolute <= policy.OuterKAbsoluteTolerance ||
                deltaEigenvalueAbsolute / eigenvalueScale <= policy.OuterKRelativeTolerance;
            if (eigenvalueConverged &&
                metrics.ResidualRelativeInfinity <= policy.OuterResidualTolerance &&
                sourceShapeChange <= policy.OuterSourceShapeTolerance &&
                metrics.PowerBalanceRelative <= policy.OuterPowerBalanceTolerance)
            {
                last.Converged = true;
                last.ConvergenceReason = "converged";
                return last;
            }

            Array.Copy(nextSourceShape, previousSourceShape, count);
            group1 = nextGroup1;
            group2 = nextGroup2;
            eigenvalue = nextEigenvalue;
        }

        throw new AuthorityFailure(
            "IndependentSolve.Nonconverged",
            "independent_reproduction",
            "The standalone reduced-model solve did not satisfy its declared caller policy; " +
            "outer=" + (last?.OuterIterations ?? 0).ToString(CultureInfo.InvariantCulture) +
            " k=" + (last?.Eigenvalue ?? double.NaN).ToString("R", CultureInfo.InvariantCulture) +
            " residual=" + (last?.ResidualRelativeInfinity ?? double.NaN).ToString("R", CultureInfo.InvariantCulture) +
            " source_shape=" + (last?.SourceShapeChangeInfinity ?? double.NaN).ToString("R", CultureInfo.InvariantCulture) +
            " power=" + (last?.PowerBalanceRelative ?? double.NaN).ToString("R", CultureInfo.InvariantCulture));
    }

    private static double[] InnerSolve(
        Definition definition,
        TopologyModel topology,
        NodeModel[] nodes,
        double[] source,
        int group,
        out int iterations)
    {
        PolicyDefinition policy = definition.Policies;
        int count = nodes.Length;
        double[] diagonal = new double[count];
        for (int index = 0; index < count; index++)
        {
            double removal = group == 1
                ? nodes[index].AbsorptionGroup1PerM + nodes[index].DownscatterGroup1To2PerM
                : nodes[index].AbsorptionGroup2PerM;
            double conductance = 0.0;
            foreach (EdgeModel edge in topology.Edges)
            {
                if (edge.EndpointA.FlatIndex == index || edge.EndpointB.FlatIndex == index)
                {
                    conductance += group == 1 ? edge.Group1M2 : edge.Group2M2;
                }
            }

            foreach (BoundaryModel boundary in topology.Boundaries)
            {
                if (boundary.Node.FlatIndex == index)
                {
                    conductance += group == 1 ? boundary.Group1M2 : boundary.Group2M2;
                }
            }

            diagonal[index] = removal + conductance / nodes[index].VolumeM3;
        }

        double[] solution = new double[count];
        double[] candidate = new double[count];
        double[] applied = new double[count];
        for (int inner = 0; inner < policy.InnerMaximumIterations; inner++)
        {
            ApplyGroup(topology, nodes, solution, applied, group);
            for (int index = 0; index < count; index++)
            {
                candidate[index] = solution[index] + (source[index] - applied[index]) / diagonal[index];
                if (!double.IsFinite(candidate[index]) || candidate[index] < 0.0)
                {
                    throw new AuthorityFailure("IndependentSolve.InnerState.Invalid", "independent_reproduction", "The standalone Jacobi update produced an invalid flux.");
                }
            }

            ApplyGroup(topology, nodes, candidate, applied, group);
            double absoluteResidual = 0.0;
            double scale = 0.0;
            for (int index = 0; index < count; index++)
            {
                absoluteResidual = Math.Max(absoluteResidual, Math.Abs(applied[index] - source[index]));
                scale = Math.Max(scale, Math.Abs(applied[index]) + Math.Abs(source[index]));
            }

            double relativeResidual = scale == 0.0 ? 0.0 : absoluteResidual / scale;
            if (absoluteResidual <= policy.InnerAbsoluteResidualTolerance ||
                relativeResidual <= policy.InnerRelativeResidualTolerance)
            {
                iterations = inner + 1;
                return (double[])candidate.Clone();
            }

            Array.Copy(candidate, solution, count);
        }

        throw new AuthorityFailure("IndependentSolve.InnerNonconverged", "independent_reproduction", "The standalone Jacobi inner solve exhausted its policy.");
    }

    private static StateMetrics EvaluateState(
        Definition definition,
        TopologyModel topology,
        NodeModel[] nodes,
        double[] group1,
        double[] group2,
        double eigenvalue)
    {
        double[] fissionSource = ComputeFissionSource(nodes, group1, group2);
        double absolute = 0.0;
        double scale = 0.0;
        for (int index = 0; index < nodes.Length; index++)
        {
            double left1 = ApplyGroup(topology, nodes, group1, index, 1);
            double left2 = ApplyGroup(topology, nodes, group2, index, 2);
            double right1 = nodes[index].ChiGroup1 * fissionSource[index] / eigenvalue;
            double right2 = nodes[index].DownscatterGroup1To2PerM * group1[index] +
                nodes[index].ChiGroup2 * fissionSource[index] / eigenvalue;
            AddResidual(left1, right1, ref absolute, ref scale);
            AddResidual(left2, right2, ref absolute, ref scale);
        }

        double power = ComputePower(nodes, group1, group2);
        return new StateMetrics
        {
            ResidualAbsoluteInfinity = absolute,
            ResidualRelativeInfinity = scale == 0.0 ? 0.0 : absolute / scale,
            PowerBalanceRelative = Math.Abs(power - definition.InitialState.TargetPowerW) /
                definition.InitialState.TargetPowerW
        };
    }

    private static void ApplyGroup(
        TopologyModel topology,
        NodeModel[] nodes,
        double[] flux,
        double[] destination,
        int group)
    {
        for (int index = 0; index < nodes.Length; index++)
        {
            destination[index] = ApplyGroup(topology, nodes, flux, index, group);
        }
    }

    private static double ApplyGroup(
        TopologyModel topology,
        NodeModel[] nodes,
        double[] flux,
        int nodeIndex,
        int group)
    {
        NodeModel node = nodes[nodeIndex];
        double value = (group == 1
            ? node.AbsorptionGroup1PerM + node.DownscatterGroup1To2PerM
            : node.AbsorptionGroup2PerM) * flux[nodeIndex];
        double leakage = 0.0;
        foreach (EdgeModel edge in topology.Edges)
        {
            int target = -1;
            double conductance = group == 1 ? edge.Group1M2 : edge.Group2M2;
            if (edge.EndpointA.FlatIndex == nodeIndex)
            {
                target = edge.EndpointB.FlatIndex;
            }
            else if (edge.EndpointB.FlatIndex == nodeIndex)
            {
                target = edge.EndpointA.FlatIndex;
            }

            if (target >= 0)
            {
                leakage += conductance * (flux[nodeIndex] - flux[target]);
            }
        }

        foreach (BoundaryModel boundary in topology.Boundaries)
        {
            if (boundary.Node.FlatIndex == nodeIndex)
            {
                leakage += (group == 1 ? boundary.Group1M2 : boundary.Group2M2) * flux[nodeIndex];
            }
        }

        return value + leakage / node.VolumeM3;
    }

    private static double[] ComputeFissionSource(NodeModel[] nodes, double[] group1, double[] group2)
    {
        var result = new double[nodes.Length];
        for (int index = 0; index < nodes.Length; index++)
        {
            result[index] = nodes[index].NuFissionGroup1PerM * group1[index] +
                nodes[index].NuFissionGroup2PerM * group2[index];
        }

        return result;
    }

    private static double ComputeProduction(NodeModel[] nodes, double[] fissionSource)
    {
        double production = 0.0;
        for (int index = 0; index < nodes.Length; index++)
        {
            production += nodes[index].VolumeM3 * fissionSource[index];
        }

        return production;
    }

    private static double ComputePower(NodeModel[] nodes, double[] group1, double[] group2)
    {
        double power = 0.0;
        for (int index = 0; index < nodes.Length; index++)
        {
            double fissionRate = nodes[index].FissionGroup1PerM * group1[index] +
                nodes[index].FissionGroup2PerM * group2[index];
            power += nodes[index].VolumeM3 * nodes[index].EnergyPerFissionJ * fissionRate;
        }

        return power;
    }

    private static double[] ComputeNodePower(NodeModel[] nodes, double[] group1, double[] group2)
    {
        var result = new double[nodes.Length];
        for (int index = 0; index < nodes.Length; index++)
        {
            result[index] = nodes[index].VolumeM3 * nodes[index].EnergyPerFissionJ *
                (nodes[index].FissionGroup1PerM * group1[index] +
                 nodes[index].FissionGroup2PerM * group2[index]);
        }

        return result;
    }

    private static void Scale(double[] values, double scale)
    {
        for (int index = 0; index < values.Length; index++)
        {
            values[index] *= scale;
        }
    }

    private static void AddResidual(double left, double right, ref double absolute, ref double scale)
    {
        absolute = Math.Max(absolute, Math.Abs(left - right));
        scale = Math.Max(scale, Math.Abs(left) + Math.Abs(right));
    }

    private static object BuildArtifact(
        Definition definition,
        GeneratedCase generated,
        string definitionSha256,
        string sourcePackSha256,
        string sourceSnapshotSha256)
    {
        return new
        {
            Format = ArtifactFormat,
            TaskId,
            ArtifactId,
            CaseId = definition.CaseId,
            Status = "candidate",
            EvidenceClass = "reduced_projection",
            CoverageClass = "RepresentativeReducedModel",
            ArtifactAvailability = "RepositoryCandidate",
            EvidenceApproval = "Candidate",
            ValidationDomain = "ReducedModel",
            ComparisonStatus = "Deferred",
            ToleranceStatus = "Deferred",
            GoldenStatus = "NoGolden",
            ApprovalScope = "Representative reduced-model sequence evidence only; not a direct CANDU physics baseline, production golden authority, or release authority.",
            DefinitionSha256 = definitionSha256,
            SourcePack = definition.SourcePack,
            SourceAuthority = new
            {
                SourceProgram = "ReactorSim.RepresentativeReducedAuthority",
                SourceVersion = "P4-T06-G4J-v1",
                BuildIdentity = "dotnet-sdk-10.0.303|net10.0|Release",
                SourceCommit,
                SourceSnapshotSha256 = sourceSnapshotSha256,
                CouplingToolIdentity = "standalone deterministic Jacobi solver; no Core or reference-program runtime coupling",
                GeneratorIdentity = "p4-t06-g4j-representative-reduced-authority"
            },
            NuclearData = new
            {
                Library = "NotApplicable",
                ExternalDataUsed = false,
                Identity = definition.SourcePack.MaterialVariantId,
                Reason = "The source pack is project-authored synthetic reduced data; no external nuclear-data library is claimed or used."
            },
            Geometry = new
            {
                ChannelCount = definition.Topology.ChannelCount,
                BundlePositionCount = definition.Topology.BundlePositionCount,
                NodeCount = generated.Topology.Nodes.Count,
                Mesh = "One explicit P2-T01 node per (ChannelId, BundlePosition); 4-channel 2x2 transverse lattice and 12 axial positions.",
                Homogenization = "Candidate synthetic material projection from the named reduced pack; no external homogenization result is claimed.",
                Channels = definition.Topology.Channels,
                Nodes = generated.Topology.Nodes,
                Edges = generated.Topology.Edges,
                Boundaries = generated.Topology.Boundaries
            },
            Units = new
            {
                Flux = "P2-T02 solver shape units",
                NodeVolume = "m^3",
                MacroscopicCoefficients = "m^-1",
                Conductance = "m^2",
                Burnup = "J/kg_HM",
                Power = "W",
                EnergyPerFission = "J",
                SimulationTime = "s",
                OverlayWeight = "m^-1 per source unit",
                PoisonConcentration = "kg/m^3"
            },
            Normalization = new
            {
                TargetPowerW = definition.InitialState.TargetPowerW,
                InitialEigenvalue = definition.InitialState.InitialEigenvalue,
                FluxNormalization = "The P2-T02 caller target power is applied after each spatial source iteration; no reference power is inferred from source arrays.",
                SignConvention = "Positive fission source and positive power; absorption overlays add to Sigma_a; negative overlay weights reduce absorption when the prescribed state is above its reference."
            },
            State = new
            {
                InitialState = definition.InitialState,
                DataVersion = definition.SourcePack.DataVersion,
                TopologyInstanceId = "G4J-TOPOLOGY-4X12-V1",
                ScenarioCount = generated.Scenarios.Count,
                EventOrder = "(time_s, event_rank, sequence as stored, event_id)",
                KineticsXenon = new
                {
                    Status = "NotCovered",
                    Reason = "This separate case targets spatial/refuelling/RRS/poison authority requirements; P2-T04 I/Xe numerical evidence remains a named future owner scope."
                },
                Scenarios = generated.Scenarios.Select(item => BuildScenarioState(definition, item)).ToArray()
            },
            Policies = definition.Policies,
            Scenarios = generated.Scenarios.Select(item => BuildScenarioArtifact(definition, item)).ToArray(),
            ObservableContract = new
            {
                Schema = "P2-T05 observable identity/value/order contract",
                OrderKey = "(scenario_id, observable_id, flat_index)",
                Quantities = new[] { "spatial.k", "spatial.flux", "spatial.node_power", "spatial.total_power", "spatial.residual_relative_inf", "spatial.convergence" },
                ComparisonRule = "Independent standalone result versus Core consumer result; tolerance status remains Deferred and diagnostic-only bounds are not gate thresholds."
            },
            AuthorityRequirements = new
            {
                SourceModelToolBuild = "Complete for project-authored generator; source commit unavailable because the workspace has no Git HEAD.",
                NuclearData = "CandidateOnly_NoExternalLibrary",
                GeometryTopologyMesh = "Complete for this reduced-model case; not production representative.",
                StateHistoryEventOrder = "Complete for five explicit scenarios, including two S4 shifts and prescribed branch events.",
                UnitsConversions = "Complete for declared SI and burnup units; no unresolved conversion is used.",
                NormalizationSign = "Complete for the frozen P2-T02 caller contract.",
                ObservableSchemaOrder = "Complete for the named P2-T05 comparison records.",
                IndependentReproduction = "Complete for standalone repeat and Core consumer; actual review receipt remains UNVERIFIED.",
                Rights = "Project-authored repository artifact; no external raw artifact or executable is redistributed.",
                ProductionRepresentativeness = "Deferred: no external CANDU numerical authority or full production coverage is claimed."
            },
            Coverage = new
            {
                Included = new[] { "static_spatial_operator", "burnup_indexed_material_projection", "fresh_state", "equilibrium_like_state", "4_bundle_refuelling_both_flow_directions", "prescribed_rrs_overlay", "prescribed_bulk_poison_overlay" },
                NotCovered = new[] { "external_candu_numeric_baseline", "full_380_channel_core", "I135_Xe135_history", "kinetics", "liquid_zone_14_to_6_runtime_state_machine", "adjuster_runtime_sequence", "thermal_hydraulics", "production_tolerance_approval" }
            },
            Tolerance = new
            {
                Status = "Deferred",
                OwnerGate = "G4",
                DiagnosticBound = definition.Comparison.DiagnosticBound,
                Basis = definition.Comparison.Note
            },
            Rights = new
            {
                Status = "ProjectAuthored",
                Redistribution = "Permitted for repository candidate definition, generator, artifact, and manifest.",
                ExternalRights = "NotApplicable; no external raw input or executable is bundled."
            },
            GeneratorChecks = new
            {
                IndependentRepeatEqual = generated.IndependentRepeatEqual,
                ScenarioResultSha256 = generated.Scenarios.ToDictionary(item => item.Definition.ScenarioId, item => item.IndependentResultSha256),
                ScenarioRepeatResultSha256 = generated.Scenarios.ToDictionary(item => item.Definition.ScenarioId, item => item.IndependentRepeatResultSha256),
                GeneratorValidation = "Generate/validate byte equality plus standalone repeat equality for every scenario"
            }
        };
    }

    private static object BuildScenarioArtifact(Definition definition, ScenarioGenerated generated)
    {
        return new
        {
            ScenarioId = generated.Definition.ScenarioId,
            Description = generated.Definition.Description,
            SimulationTimeS = generated.Definition.SimulationTimeS,
            EventHistory = BuildEventHistory(generated.Definition),
            State = BuildScenarioState(definition, generated),
            Overlay = BuildOverlay(definition, generated.Definition.Overlay, generated),
            Coefficients = generated.Nodes.Select(node => BuildCoefficient(node)).ToArray(),
            ExpectedIndependent = generated.Independent,
            ObservableRecords = BuildObservableRecords(generated),
            IndependentReproduction = new
            {
                Solver = "standalone-jacobi-v1",
                Converged = generated.Independent.Converged,
                ConvergenceReason = generated.Independent.ConvergenceReason,
                ResultSha256 = generated.IndependentResultSha256,
                RepeatResultSha256 = generated.IndependentRepeatResultSha256,
                RepeatEqual = generated.IndependentRepeatEqual
            }
        };
    }

    private static object BuildScenarioState(Definition definition, ScenarioGenerated generated)
    {
        ScenarioDefinition scenario = generated.Definition;
        return new
        {
            ScenarioId = scenario.ScenarioId,
            SimulationTimeS = scenario.SimulationTimeS,
            CoreStateVersion = CurrentCoreStateVersion(definition, scenario),
            SpatialStateVersion = definition.InitialState.InitialSpatialStateVersion,
            PowerSnapshotVersion = definition.InitialState.InitialPowerSnapshotVersion,
            SpatialBindingStatus = "InvalidBeforeSolve",
            PowerBindingStatus = "InvalidBeforeSolve",
            BundleStates = generated.Nodes.Select(node => BuildBundleState(definition, generated, node)).ToArray(),
            EventHistory = BuildEventHistory(scenario),
            RefuellingAudits = BuildRefuellingAudits(scenario),
            Rrs = BuildRrsState(scenario.Overlay),
            Poison = BuildPoisonState(scenario.Overlay),
            KineticsXenon = new
            {
                Status = "NotCovered",
                Reason = "The case does not claim I-135/Xe-135 or point-kinetics authority."
            }
        };
    }

    private static object BuildBundleState(Definition definition, ScenarioGenerated generated, NodeModel node)
    {
        bool fresh = IsFreshInserted(generated.Definition, node.ChannelId, node.Position);
        double insertedAt = fresh ? generated.Definition.SimulationTimeS : 0.0;
        double cumulativeEnergy = fresh ? 0.0 : node.BurnupJPerKgHm * HeavyMetalMassKg;
        return new
        {
            BundleId = ResolveBundleId(generated.Definition, node.ChannelId, node.Position),
            ChannelId = node.ChannelId,
            BundlePosition = node.Position,
            MaterialVariantId = "MAT-SYN",
            InitialBurnupJPerKgHm = 0.0,
            CumulativeFissionEnergyJ = cumulativeEnergy,
            CurrentBurnupJPerKgHm = node.BurnupJPerKgHm,
            HeavyMetalMassKg = HeavyMetalMassKg,
            InsertedAtS = insertedAt,
            ResidenceTimeS = generated.Definition.SimulationTimeS - insertedAt,
            Power = (double?)null,
            PowerSnapshotId = (string?)null,
            PowerHistory = Array.Empty<object>(),
            CoefficientTableId = definition.SourcePack.TableId,
            CoefficientBracket = node.MaterialBracket,
            StateVersion = CurrentCoreStateVersion(definition, generated.Definition),
            NuclideStateVersion = 1UL,
            NuclideDataStatus = "NotCovered",
            NodeVolumeM3 = node.VolumeM3
        };
    }

    private static double ComputeScenarioBurnup(ScenarioDefinition scenario, int channel, int position)
    {
        double[] preEventBurnup = scenario.BaseBurnupByPositionJPerKgHm
            .Select(value => value + scenario.ChannelBurnupOffsetJPerKgHm[channel])
            .ToArray();
        EventDefinition? shift = scenario.Events.FirstOrDefault(eventValue =>
            eventValue.EventType == "refuel_shift" && eventValue.ChannelId == channel);
        if (shift == null)
        {
            return scenario.FreshPositionsByChannel[channel].Contains(position)
                ? 0.0
                : preEventBurnup[position];
        }

        if (shift.ShiftDirection == "TowardEndB")
        {
            return position < 4 ? 0.0 : preEventBurnup[position - 4];
        }

        if (shift.ShiftDirection == "TowardEndA")
        {
            return position >= 8 ? 0.0 : preEventBurnup[position + 4];
        }

        throw new AuthorityFailure(
            "Scenario.RefuelDirection.Invalid",
            "scenario.events",
            "A refuelling event has an unsupported shift direction.");
    }

    private static bool IsFreshInserted(ScenarioDefinition scenario, int channel, int position)
    {
        EventDefinition? shift = scenario.Events.FirstOrDefault(eventValue =>
            eventValue.EventType == "refuel_shift" && eventValue.ChannelId == channel);
        if (shift?.ShiftDirection == "TowardEndB")
        {
            return position < 4;
        }

        if (shift?.ShiftDirection == "TowardEndA")
        {
            return position >= 8;
        }

        return scenario.FreshPositionsByChannel[channel].Contains(position);
    }

    private static string ResolveBundleId(ScenarioDefinition scenario, int channel, int position)
    {
        EventDefinition? shift = scenario.Events.FirstOrDefault(eventValue =>
            eventValue.EventType == "refuel_shift" && eventValue.ChannelId == channel);
        if (shift?.ShiftDirection == "TowardEndB")
        {
            return position < 4
                ? "G4J-F-" + shift.EventId + "-" + position.ToString("D2", CultureInfo.InvariantCulture)
                : "G4J-B" + channel.ToString("D2", CultureInfo.InvariantCulture) + "-" +
                  (position - 4).ToString("D2", CultureInfo.InvariantCulture);
        }

        if (shift?.ShiftDirection == "TowardEndA")
        {
            return position >= 8
                ? "G4J-F-" + shift.EventId + "-" + position.ToString("D2", CultureInfo.InvariantCulture)
                : "G4J-B" + channel.ToString("D2", CultureInfo.InvariantCulture) + "-" +
                  (position + 4).ToString("D2", CultureInfo.InvariantCulture);
        }

        return "G4J-B" + channel.ToString("D2", CultureInfo.InvariantCulture) + "-" +
            position.ToString("D2", CultureInfo.InvariantCulture);
    }

    private static object[] BuildRefuellingAudits(ScenarioDefinition scenario)
    {
        return scenario.Events
            .Where(eventValue => eventValue.EventType == "refuel_shift")
            .Select(eventValue =>
            {
                int channel = eventValue.ChannelId ?? throw new AuthorityFailure(
                    "Scenario.RefuelChannel.Missing",
                    "scenario.events",
                    "A refuelling event must bind a channel.");
                bool towardEndB = eventValue.ShiftDirection == "TowardEndB";
                int[] movedSourcePositions = towardEndB
                    ? Enumerable.Range(0, 8).ToArray()
                    : Enumerable.Range(4, 8).ToArray();
                int[] dischargedPositions = towardEndB
                    ? Enumerable.Range(8, 4).ToArray()
                    : Enumerable.Range(0, 4).ToArray();
                var moved = movedSourcePositions.Select(sourcePosition => new
                {
                    BundleId = "G4J-B" + channel.ToString("D2", CultureInfo.InvariantCulture) + "-" + sourcePosition.ToString("D2", CultureInfo.InvariantCulture),
                    SourcePosition = sourcePosition,
                    DestinationPosition = towardEndB ? sourcePosition + 4 : sourcePosition - 4
                }).ToArray();
                var discharged = dischargedPositions.Select(position => new
                {
                    BundleId = "G4J-B" + channel.ToString("D2", CultureInfo.InvariantCulture) + "-" + position.ToString("D2", CultureInfo.InvariantCulture),
                    Position = position,
                    DischargeEnd = towardEndB ? "EndB" : "EndA"
                }).ToArray();
                return new
                {
                    CommandId = eventValue.EventId,
                    EventRank = eventValue.EventRank,
                    EffectiveTimeS = eventValue.TimeS,
                    ChannelId = channel,
                    SchemeId = eventValue.SchemeId,
                    ShiftDirection = eventValue.ShiftDirection,
                    ShiftCount = 4,
                    BeforeBundleIds = Enumerable.Range(0, 12)
                        .Select(position => "G4J-B" + channel.ToString("D2", CultureInfo.InvariantCulture) + "-" + position.ToString("D2", CultureInfo.InvariantCulture))
                        .ToArray(),
                    InsertedBundleIds = Enumerable.Range(0, 4)
                        .Select(position => "G4J-F-" + eventValue.EventId + "-" + (towardEndB ? position : position + 8).ToString("D2", CultureInfo.InvariantCulture))
                        .ToArray(),
                    MovedBundleMap = moved,
                    DischargeRecords = discharged,
                    AfterBundleIds = Enumerable.Range(0, 12)
                        .Select(position => ResolveBundleId(
                            new ScenarioDefinition
                            {
                                Events = new[] { eventValue },
                                FreshPositionsByChannel = new[] { Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>() }
                            },
                            channel,
                            position))
                        .ToArray()
                };
            })
            .ToArray();
    }

    private static object BuildRrsState(OverlayDefinition overlay)
    {
        if (overlay.Kind != "rrs")
        {
            return new { Enabled = false, Mode = "Disabled", SourceId = "NotApplicable", MapId = "NotApplicable" };
        }

        return new
        {
            Enabled = true,
            Mode = "Prescribed",
            SourceId = overlay.SourceId,
            MapId = overlay.MapId,
            DataVersion = overlay.DataVersion,
            StateFraction = overlay.StateFraction,
            ReferenceFraction = overlay.ReferenceFraction,
            PowerSetpointW = 1000.0,
            TiltError = 0.0
        };
    }

    private static object[] BuildEventHistory(ScenarioDefinition scenario)
    {
        return scenario.Events
            .Select((eventValue, index) => new
            {
                Sequence = (ulong)index,
                EventId = eventValue.EventId,
                EventRank = eventValue.EventRank,
                EventType = eventValue.EventType,
                TimeS = eventValue.TimeS,
                OwnerId = eventValue.OwnerId,
                ChannelId = eventValue.ChannelId,
                SchemeId = eventValue.SchemeId,
                ShiftDirection = eventValue.ShiftDirection,
                InsertedPositions = eventValue.InsertedPositions,
                DischargedPositions = eventValue.DischargedPositions
            })
            .ToArray();
    }

    private static ulong CurrentCoreStateVersion(Definition definition, ScenarioDefinition scenario)
    {
        return definition.InitialState.InitialCoreStateVersion +
            (ulong)scenario.Events.Count(eventValue => eventValue.EventType != "scenario_initialization");
    }

    private static object BuildPoisonState(OverlayDefinition overlay)
    {
        if (overlay.Kind != "bulk_poison")
        {
            return new { Enabled = false, Mode = "Disabled", SourceId = "NotApplicable", MapId = "NotApplicable" };
        }

        return new
        {
            Enabled = true,
            Mode = "Prescribed",
            SourceId = overlay.SourceId,
            MapId = overlay.MapId,
            DataVersion = overlay.DataVersion,
            PoisonMassKg = overlay.PoisonMassKg,
            ModeratorVolumeM3 = overlay.ModeratorVolumeM3,
            PoisonMassConcentrationKgPerM3 = overlay.PoisonMassKg / overlay.ModeratorVolumeM3,
            ReferenceConcentrationKgPerM3 = overlay.ReferenceConcentrationKgPerM3,
            AddRateKgPerS = 0.0,
            WithdrawRateKgPerS = 0.0
        };
    }

    private static object BuildOverlay(Definition definition, OverlayDefinition overlay, ScenarioGenerated generated)
    {
        if (overlay.Kind == "none")
        {
            return new
            {
                Kind = "none",
                Enabled = false,
                Mode = "Disabled",
                MapId = "NotApplicable",
                Entries = Array.Empty<object>()
            };
        }

        double stateDelta = overlay.Kind == "rrs"
            ? overlay.StateFraction - overlay.ReferenceFraction
            : overlay.PoisonMassKg / overlay.ModeratorVolumeM3 - overlay.ReferenceConcentrationKgPerM3;
        var entries = new List<object>();
        for (int channel = 0; channel < definition.Topology.ChannelCount; channel++)
        {
            for (int position = 0; position < definition.Topology.BundlePositionCount; position++)
            {
                int flatIndex = FlatIndex(channel, position, 12);
                double weight1 = overlay.Kind == "rrs"
                    ? overlay.WeightsGroup1PerUnitByChannel[channel]
                    : overlay.WeightsGroup1PerConcentrationByChannel[channel];
                double weight2 = overlay.Kind == "rrs"
                    ? overlay.WeightsGroup2PerUnitByChannel[channel]
                    : overlay.WeightsGroup2PerConcentrationByChannel[channel];
                entries.Add(BuildOverlayEntry(
                    overlay,
                    channel,
                    position,
                    flatIndex,
                    1,
                    weight1,
                    stateDelta));
                entries.Add(BuildOverlayEntry(
                    overlay,
                    channel,
                    position,
                    flatIndex,
                    2,
                    weight2,
                    stateDelta));
            }
        }

        return new
        {
            Kind = overlay.Kind,
            Enabled = true,
            Mode = "Prescribed",
            SourceId = overlay.SourceId,
            MapId = overlay.MapId,
            DataVersion = overlay.DataVersion,
            StateDelta = stateDelta,
            Entries = entries
        };
    }

    private static object BuildOverlayEntry(
        OverlayDefinition overlay,
        int channel,
        int position,
        int flatIndex,
        int group,
        double weight,
        double stateDelta)
    {
        return new
        {
            SourceId = overlay.SourceId,
            MapId = overlay.MapId,
            DataVersion = overlay.DataVersion,
            TargetNode = new { ChannelId = channel, Position = position, FlatIndex = flatIndex },
            Group = group,
            WeightMInversePerUnit = weight,
            SourceStateDelta = stateDelta,
            EffectiveDeltaSigmaAPerM = weight * stateDelta,
            SourceUnit = overlay.Kind == "rrs" ? "dimensionless" : "kg/m^3",
            TargetUnit = "m^-1"
        };
    }

    private static object BuildCoefficient(NodeModel node)
    {
        return new
        {
            FlatIndex = node.FlatIndex,
            ChannelId = node.ChannelId,
            Position = node.Position,
            VolumeM3 = node.VolumeM3,
            BurnupJPerKgHm = node.BurnupJPerKgHm,
            BaseAbsorptionGroup1PerM = node.BaseAbsorptionGroup1PerM,
            BaseAbsorptionGroup2PerM = node.BaseAbsorptionGroup2PerM,
            OverlayAbsorptionGroup1PerM = node.OverlayAbsorptionGroup1PerM,
            OverlayAbsorptionGroup2PerM = node.OverlayAbsorptionGroup2PerM,
            AbsorptionGroup1PerM = node.AbsorptionGroup1PerM,
            AbsorptionGroup2PerM = node.AbsorptionGroup2PerM,
            DownscatterGroup1To2PerM = node.DownscatterGroup1To2PerM,
            FissionGroup1PerM = node.FissionGroup1PerM,
            FissionGroup2PerM = node.FissionGroup2PerM,
            NuFissionGroup1PerM = node.NuFissionGroup1PerM,
            NuFissionGroup2PerM = node.NuFissionGroup2PerM,
            ChiGroup1 = node.ChiGroup1,
            ChiGroup2 = node.ChiGroup2,
            EnergyPerFissionJ = node.EnergyPerFissionJ,
            MaterialBracket = node.MaterialBracket
        };
    }

    private static object BuildObservableRecords(ScenarioGenerated generated)
    {
        IndependentResult result = generated.Independent;
        return new
        {
            Scalar = new[]
            {
                new { ObservableId = "spatial.k", QuantityId = "spatial.k", Unit = "1", Value = result.Eigenvalue, OrderKey = "k" },
                new { ObservableId = "spatial.total_power", QuantityId = "spatial.total_power", Unit = "W", Value = result.TotalPowerW, OrderKey = "total_power" },
                new { ObservableId = "spatial.residual_relative_inf", QuantityId = "spatial.residual_relative_inf", Unit = "1", Value = result.ResidualRelativeInfinity, OrderKey = "residual" }
            },
            Vector = new[]
            {
                new { ObservableId = "spatial.flux.group1", QuantityId = "spatial.flux", Unit = "P2-T02 shape units", Values = result.Group1Flux, OrderKey = "group1" },
                new { ObservableId = "spatial.flux.group2", QuantityId = "spatial.flux", Unit = "P2-T02 shape units", Values = result.Group2Flux, OrderKey = "group2" },
                new { ObservableId = "spatial.node_power", QuantityId = "spatial.node_power", Unit = "W", Values = result.NodePowerW, OrderKey = "node_power" }
            },
            Convergence = new
            {
                ObservableId = "spatial.convergence",
                QuantityId = "spatial.convergence",
                Status = result.Converged ? "Converged" : "Nonconverged",
                Reason = result.ConvergenceReason,
                OuterIterations = result.OuterIterations,
                Group1InnerIterations = result.Group1InnerIterations,
                Group2InnerIterations = result.Group2InnerIterations
            }
        };
    }

    private static object BuildCoefficientManifest(ScenarioGenerated generated)
    {
        return new
        {
            ScenarioId = generated.Definition.ScenarioId,
            ResultSha256 = generated.IndependentResultSha256,
            RepeatResultSha256 = generated.IndependentRepeatResultSha256,
            RepeatEqual = generated.IndependentRepeatEqual,
            OuterIterations = generated.Independent.OuterIterations,
            Eigenvalue = generated.Independent.Eigenvalue,
            TotalPowerW = generated.Independent.TotalPowerW,
            ResidualRelativeInfinity = generated.Independent.ResidualRelativeInfinity
        };
    }

    private static object BuildManifest(
        Definition definition,
        GeneratedCase generated,
        string definitionSha256,
        string sourcePackSha256,
        string artifactSha256,
        string sourceSnapshotSha256)
    {
        return new
        {
            Format = ManifestFormat,
            ManifestSchemaVersion = 1,
            TaskId,
            ArtifactId,
            CaseId = definition.CaseId,
            Disposition = "candidate",
            CoverageClass = "RepresentativeReducedModel",
            ValidationDomain = "ReducedModel",
            DefinitionSha256 = definitionSha256,
            SourcePackSha256 = sourcePackSha256,
            ArtifactSha256 = artifactSha256,
            SourceSnapshotSha256 = sourceSnapshotSha256,
            SourceCommit,
            SourcePackPath = definition.SourcePack.RelativePath,
            ScenarioCount = generated.Scenarios.Count,
            NodeCount = generated.Topology.Nodes.Count,
            EdgeCount = generated.Topology.Edges.Count,
            BoundaryCount = generated.Topology.Boundaries.Count,
            ScenarioResults = generated.Scenarios.Select(BuildCoefficientManifest).ToArray()
        };
    }

    private static void ValidateOverlay(OverlayDefinition overlay, string scenarioId)
    {
        if (overlay.Kind == "none")
        {
            return;
        }

        if (overlay.Kind == "rrs")
        {
            ValidateArray(overlay.WeightsGroup1PerUnitByChannel, 4, scenarioId + ".rrs.group1_weights");
            ValidateArray(overlay.WeightsGroup2PerUnitByChannel, 4, scenarioId + ".rrs.group2_weights");
            if (overlay.StateFraction < 0.0 || overlay.StateFraction > 1.0 ||
                overlay.ReferenceFraction < 0.0 || overlay.ReferenceFraction > 1.0)
            {
                throw new AuthorityFailure("Definition.RrsState.Invalid", "scenario.overlay", "RRS state fractions must be in [0,1].");
            }

            return;
        }

        if (overlay.Kind == "bulk_poison")
        {
            ValidateArray(overlay.WeightsGroup1PerConcentrationByChannel, 4, scenarioId + ".poison.group1_weights");
            ValidateArray(overlay.WeightsGroup2PerConcentrationByChannel, 4, scenarioId + ".poison.group2_weights");
            ValidateFinitePositive(overlay.PoisonMassKg, scenarioId + ".poison_mass_kg");
            ValidateFinitePositive(overlay.ModeratorVolumeM3, scenarioId + ".moderator_volume_m3");
            if (overlay.ReferenceConcentrationKgPerM3 < 0.0)
            {
                throw new AuthorityFailure("Definition.PoisonReference.Invalid", "scenario.overlay", "Poison reference concentration may not be negative.");
            }

            return;
        }

        throw new AuthorityFailure("Definition.Overlay.Kind.Invalid", "scenario.overlay.kind", "Unknown overlay kind.");
    }

    private static void ValidatePolicies(PolicyDefinition policies)
    {
        if (policies.InnerMaximumIterations < 1 || policies.OuterMaximumIterations < 1 ||
            policies.InnerAbsoluteResidualTolerance <= 0.0 ||
            policies.InnerRelativeResidualTolerance <= 0.0 ||
            policies.OuterKAbsoluteTolerance <= 0.0 ||
            policies.OuterKRelativeTolerance <= 0.0 ||
            policies.OuterResidualTolerance <= 0.0 ||
            policies.OuterSourceShapeTolerance <= 0.0 ||
            policies.OuterPowerBalanceTolerance <= 0.0)
        {
            throw new AuthorityFailure("Definition.Policies.Invalid", "definition.policies", "All declared solve policies must be finite and positive.");
        }
    }

    private static void ValidateArray(double[] values, int expected, string path)
    {
        if (values == null || values.Length != expected || values.Any(value => !double.IsFinite(value)))
        {
            throw new AuthorityFailure("Definition.Array.Invalid", path, "The declared finite array has the wrong length or contains a non-finite value.");
        }
    }

    private static void ValidateFinitePositive(double value, string path)
    {
        if (!double.IsFinite(value) || value <= 0.0)
        {
            throw new AuthorityFailure("Definition.Value.Invalid", path, "The value must be finite and positive.");
        }
    }

    private static int FlatIndex(int channel, int position, int positionCount)
    {
        return checked(channel * positionCount + position);
    }

    private static string ValidateHash(string value, string path)
    {
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new AuthorityFailure("Hash.Invalid", path, "The hash must be a 64-character hexadecimal SHA-256 identity.");
        }

        return value.ToLowerInvariant();
    }

    private static byte[] ReadFile(string path, string label)
    {
        if (!File.Exists(path))
        {
            throw new AuthorityFailure("Input.Missing", label, "The required input file does not exist.");
        }

        return File.ReadAllBytes(path);
    }

    private static void WriteFile(string path, byte[] bytes)
    {
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, bytes);
    }

    private static byte[] Sha256(byte[] bytes)
    {
        return SHA256.HashData(bytes);
    }

    private static string Hex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

internal sealed class AuthorityFailure : Exception
{
    public AuthorityFailure(string code, string path, string message)
        : base(message)
    {
        Code = code;
        Path = path;
    }

    public string Code { get; }

    public string Path { get; }
}

internal sealed class Definition
{
    public string Format { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string CaseId { get; set; } = string.Empty;
    public SourcePackDefinition SourcePack { get; set; } = new();
    public TopologyDefinition Topology { get; set; } = new();
    public ConductanceDefinition Conductances { get; set; } = new();
    public NodeGeometryDefinition NodeGeometry { get; set; } = new();
    public MaterialProjectionDefinition MaterialProjection { get; set; } = new();
    public InitialStateDefinition InitialState { get; set; } = new();
    public ScenarioDefinition[] Scenarios { get; set; } = Array.Empty<ScenarioDefinition>();
    public PolicyDefinition Policies { get; set; } = new();
    public ComparisonDefinition Comparison { get; set; } = new();
}

internal sealed class SourcePackDefinition
{
    public string RelativePath { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string TableId { get; set; } = string.Empty;
    public string MaterialVariantId { get; set; } = string.Empty;
    public string DataVersion { get; set; } = string.Empty;
    public string TransformId { get; set; } = string.Empty;
}

internal sealed class TopologyDefinition
{
    public int ChannelCount { get; set; }
    public int BundlePositionCount { get; set; }
    public ChannelDefinition[] Channels { get; set; } = Array.Empty<ChannelDefinition>();
    public TransverseLinkDefinition[] TransverseLinks { get; set; } = Array.Empty<TransverseLinkDefinition>();
    public string CardinalBoundaryClassification { get; set; } = string.Empty;
    public string EndBoundaryClassification { get; set; } = string.Empty;
}

internal sealed class ChannelDefinition
{
    public int ChannelId { get; set; }
    public int CoordinateX { get; set; }
    public int CoordinateY { get; set; }
    public string FlowDirection { get; set; } = string.Empty;
}

internal sealed class TransverseLinkDefinition
{
    public int EndpointAChannelId { get; set; }
    public int EndpointBChannelId { get; set; }
    public string DirectionAToB { get; set; } = string.Empty;
    public string DirectionBToA { get; set; } = string.Empty;
}

internal sealed class ConductanceDefinition
{
    public double AxialEdgeGroup1M2 { get; set; }
    public double AxialEdgeGroup2M2 { get; set; }
    public double TransverseEdgeGroup1M2 { get; set; }
    public double TransverseEdgeGroup2M2 { get; set; }
    public double EndBoundaryGroup1M2 { get; set; }
    public double EndBoundaryGroup2M2 { get; set; }
}

internal sealed class NodeGeometryDefinition
{
    public double VolumeBaseM3 { get; set; }
    public double VolumeChannelIncrementM3 { get; set; }
    public double VolumePositionIncrementM3 { get; set; }
}

internal sealed class MaterialProjectionDefinition
{
    public double[] ChannelAbsorptionGroup1Multiplier { get; set; } = Array.Empty<double>();
    public double[] ChannelAbsorptionGroup2Multiplier { get; set; } = Array.Empty<double>();
    public double[] ChannelFissionMultiplier { get; set; } = Array.Empty<double>();
    public double[] ChannelDownscatterMultiplier { get; set; } = Array.Empty<double>();
    public double[] PositionAbsorptionGroup1Multiplier { get; set; } = Array.Empty<double>();
    public double[] PositionAbsorptionGroup2Multiplier { get; set; } = Array.Empty<double>();
    public double[] PositionFissionMultiplier { get; set; } = Array.Empty<double>();
    public double[] PositionDownscatterMultiplier { get; set; } = Array.Empty<double>();
}

internal sealed class InitialStateDefinition
{
    public double InitialEigenvalue { get; set; }
    public double InitialGroup1Flux { get; set; }
    public double InitialGroup2Flux { get; set; }
    public double TargetPowerW { get; set; }
    public ulong InitialCoreStateVersion { get; set; }
    public ulong InitialSpatialStateVersion { get; set; }
    public ulong InitialPowerSnapshotVersion { get; set; }
}

internal sealed class ScenarioDefinition
{
    public string ScenarioId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double SimulationTimeS { get; set; }
    public double[] BaseBurnupByPositionJPerKgHm { get; set; } = Array.Empty<double>();
    public double[] ChannelBurnupOffsetJPerKgHm { get; set; } = Array.Empty<double>();
    public int[][] FreshPositionsByChannel { get; set; } = Array.Empty<int[]>();
    public OverlayDefinition Overlay { get; set; } = new();
    public EventDefinition[] Events { get; set; } = Array.Empty<EventDefinition>();
}

internal sealed class OverlayDefinition
{
    public string Kind { get; set; } = string.Empty;
    public string SourceId { get; set; } = "NotApplicable";
    public string MapId { get; set; } = "NotApplicable";
    public string DataVersion { get; set; } = "NotApplicable";
    public double StateFraction { get; set; }
    public double ReferenceFraction { get; set; }
    public double[] WeightsGroup1PerUnitByChannel { get; set; } = Array.Empty<double>();
    public double[] WeightsGroup2PerUnitByChannel { get; set; } = Array.Empty<double>();
    public double PoisonMassKg { get; set; }
    public double ModeratorVolumeM3 { get; set; }
    public double ReferenceConcentrationKgPerM3 { get; set; }
    public double[] WeightsGroup1PerConcentrationByChannel { get; set; } = Array.Empty<double>();
    public double[] WeightsGroup2PerConcentrationByChannel { get; set; } = Array.Empty<double>();
}

internal sealed class EventDefinition
{
    public string EventId { get; set; } = string.Empty;
    public int EventRank { get; set; }
    public string EventType { get; set; } = string.Empty;
    public double TimeS { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public int? ChannelId { get; set; }
    public string? SchemeId { get; set; }
    public string? ShiftDirection { get; set; }
    public int[]? InsertedPositions { get; set; }
    public int[]? DischargedPositions { get; set; }
}

internal sealed class PolicyDefinition
{
    public double InnerAbsoluteResidualTolerance { get; set; }
    public double InnerRelativeResidualTolerance { get; set; }
    public int InnerMaximumIterations { get; set; }
    public double OuterKAbsoluteTolerance { get; set; }
    public double OuterKRelativeTolerance { get; set; }
    public double OuterResidualTolerance { get; set; }
    public double OuterSourceShapeTolerance { get; set; }
    public double OuterPowerBalanceTolerance { get; set; }
    public int OuterMaximumIterations { get; set; }
}

internal sealed class ComparisonDefinition
{
    public string Status { get; set; } = string.Empty;
    public double DiagnosticBound { get; set; }
    public string OwnerGate { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
}

internal sealed class PackRoot
{
    public PackTable[] Tables { get; set; } = Array.Empty<PackTable>();
}

internal sealed class PackTable
{
    public string TableId { get; set; } = string.Empty;
    public string MaterialVariantId { get; set; } = string.Empty;
    public PackRow[] Rows { get; set; } = Array.Empty<PackRow>();
}

internal sealed class PackRow
{
    public double BurnupJPerKgHm { get; set; }
    public PackCoefficients Coefficients { get; set; } = new();
}

internal sealed class PackCoefficients
{
    public double AbsorptionGroup1PerM { get; set; }
    public double AbsorptionGroup2PerM { get; set; }
    public double FissionGroup1PerM { get; set; }
    public double FissionGroup2PerM { get; set; }
    public double NuFissionGroup1PerM { get; set; }
    public double NuFissionGroup2PerM { get; set; }
    public double DownscatterGroup1To2PerM { get; set; }
    public double ChiGroup1 { get; set; }
    public double EnergyPerFissionJ { get; set; }
}

internal sealed class GeneratedCase
{
    public TopologyModel Topology { get; set; } = new();
    public List<ScenarioGenerated> Scenarios { get; set; } = new();
    public bool IndependentRepeatEqual { get; set; }
}

internal sealed class ScenarioGenerated
{
    public ScenarioDefinition Definition { get; set; } = new();
    public NodeModel[] Nodes { get; set; } = Array.Empty<NodeModel>();
    public double[] BurnupByNode { get; set; } = Array.Empty<double>();
    public IndependentResult Independent { get; set; } = new();
    public string IndependentResultSha256 { get; set; } = string.Empty;
    public string IndependentRepeatResultSha256 { get; set; } = string.Empty;
    public bool IndependentRepeatEqual { get; set; }
}

internal sealed class TopologyModel
{
    public List<NodeModel> Nodes { get; set; } = new();
    public List<EdgeModel> Edges { get; set; } = new();
    public List<BoundaryModel> Boundaries { get; set; } = new();
}

internal sealed class NodeModel
{
    public int FlatIndex { get; set; }
    public int ChannelId { get; set; }
    public int Position { get; set; }
    public double VolumeM3 { get; set; }
    public double BurnupJPerKgHm { get; set; }
    public double BaseAbsorptionGroup1PerM { get; set; }
    public double BaseAbsorptionGroup2PerM { get; set; }
    public double OverlayAbsorptionGroup1PerM { get; set; }
    public double OverlayAbsorptionGroup2PerM { get; set; }
    public double AbsorptionGroup1PerM { get; set; }
    public double AbsorptionGroup2PerM { get; set; }
    public double DownscatterGroup1To2PerM { get; set; }
    public double FissionGroup1PerM { get; set; }
    public double FissionGroup2PerM { get; set; }
    public double NuFissionGroup1PerM { get; set; }
    public double NuFissionGroup2PerM { get; set; }
    public double ChiGroup1 { get; set; }
    public double ChiGroup2 { get; set; }
    public double EnergyPerFissionJ { get; set; }
    public string MaterialBracket { get; set; } = string.Empty;
}

internal sealed class NodeRef
{
    public int ChannelId { get; set; }
    public int Position { get; set; }
    public int FlatIndex { get; set; }
}

internal sealed class EdgeModel
{
    public NodeRef EndpointA { get; set; } = new();
    public NodeRef EndpointB { get; set; } = new();
    public string DirectionAToB { get; set; } = string.Empty;
    public string DirectionBToA { get; set; } = string.Empty;
    public double Group1M2 { get; set; }
    public double Group2M2 { get; set; }
}

internal sealed class BoundaryModel
{
    public NodeRef Node { get; set; } = new();
    public string Face { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public double Group1M2 { get; set; }
    public double Group2M2 { get; set; }
}

internal sealed class OverlayValues
{
    public double Group1 { get; set; }
    public double Group2 { get; set; }
}

internal sealed class StateMetrics
{
    public double ResidualAbsoluteInfinity { get; set; }
    public double ResidualRelativeInfinity { get; set; }
    public double PowerBalanceRelative { get; set; }
}

internal sealed class IndependentResult
{
    public bool Converged { get; set; }
    public string ConvergenceReason { get; set; } = string.Empty;
    public int OuterIterations { get; set; }
    public int Group1InnerIterations { get; set; }
    public int Group2InnerIterations { get; set; }
    public double Eigenvalue { get; set; }
    public double NormalizationScale { get; set; }
    public double TotalPowerW { get; set; }
    public double[] Group1Flux { get; set; } = Array.Empty<double>();
    public double[] Group2Flux { get; set; } = Array.Empty<double>();
    public double[] FissionSourceByNode { get; set; } = Array.Empty<double>();
    public double[] NodePowerW { get; set; } = Array.Empty<double>();
    public double ResidualAbsoluteInfinity { get; set; }
    public double ResidualRelativeInfinity { get; set; }
    public double SourceShapeChangeInfinity { get; set; }
    public double PowerBalanceRelative { get; set; }
}
