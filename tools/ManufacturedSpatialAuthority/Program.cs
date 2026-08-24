using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class Program
{
    private const string DefinitionFormat = "reactorsim.g4i-manufactured-definition/v1";
    private const string ArtifactFormat = "reactorsim.g4i-manufactured-spatial-authority/v1";
    private const string ManifestFormat = "reactorsim.g4i-manufactured-spatial-authority-manifest/v1";
    private const string TaskId = "P4-T06-G4I";
    private const string ArtifactId = "p4-t06-g4i-manufactured-authority-v1";
    private const string GeneratorId = "p4-t06-g4i-manufactured-spatial-authority";
    private const string GeneratorVersion = "v1";
    private const string CandidateRelativePath =
        "data/comparisons/p4-t06-g4i-manufactured-authority-v1.json";
    private const string ApprovedRelativePath =
        "data/golden/p4-t06-g4i-manufactured-authority-v1.json";
    private const string DefinitionRelativePath =
        "data/comparisons/p4-t06-g4i-manufactured-definition-v1.json";

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
            if (args.Length != 5 ||
                (args[0] != "generate" && args[0] != "validate") ||
                (args[4] != "candidate" && args[4] != "approved"))
            {
                Console.Error.WriteLine(
                    "Usage: generate|validate <definition> <artifact> <manifest> candidate|approved");
                return 1;
            }

            bool approved = args[4] == "approved";
            if (args[0] == "generate")
            {
                Generate(args[1], args[2], args[3], approved);
            }
            else
            {
                Validate(args[1], args[2], args[3], approved);
            }

            return 0;
        }
        catch (AuthorityFailure failure)
        {
            Console.Error.WriteLine(
                "P4_T06_G4I_FAILURE code=" + failure.Code +
                " path=" + failure.Path +
                " message=" + failure.Message);
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "P4_T06_G4I_FAILURE code=Unhandled.Exception path=tool message=" +
                exception.Message);
            return 3;
        }
    }

    private static void Generate(
        string definitionPath,
        string artifactPath,
        string manifestPath,
        bool approved)
    {
        byte[] definitionBytes = ReadFile(definitionPath, "definition");
        Definition definition = ParseDefinition(definitionBytes);
        GeneratedCase generated = BuildCase(definition);
        AuthorityArtifact artifact = BuildArtifact(
            definition,
            generated,
            Hex(Sha256(definitionBytes)),
            approved);
        byte[] artifactBytes = JsonSerializer.SerializeToUtf8Bytes(artifact, JsonOptions);
        AuthorityManifest manifest = BuildManifest(
            definition,
            artifactBytes,
            Hex(Sha256(definitionBytes)),
            approved);
        byte[] manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);

        WriteFile(artifactPath, artifactBytes);
        WriteFile(manifestPath, manifestBytes);

        Console.WriteLine(
            "P4_T06_G4I_GENERATE_PASS disposition=" +
            (approved ? "approved" : "candidate") +
            " artifact_sha256=" + Hex(Sha256(artifactBytes)) +
            " manifest_sha256=" + Hex(Sha256(manifestBytes)) +
            " independent_outer_iterations=" +
            generated.Independent.OuterIterations.ToString(CultureInfo.InvariantCulture) +
            " exact_residual=" +
            generated.ExactBalance.RelativeInfinity.ToString("R", CultureInfo.InvariantCulture));
    }

    private static void Validate(
        string definitionPath,
        string artifactPath,
        string manifestPath,
        bool approved)
    {
        byte[] definitionBytes = ReadFile(definitionPath, "definition");
        Definition definition = ParseDefinition(definitionBytes);
        GeneratedCase generated = BuildCase(definition);
        AuthorityArtifact expectedArtifact = BuildArtifact(
            definition,
            generated,
            Hex(Sha256(definitionBytes)),
            approved);
        byte[] expectedArtifactBytes = JsonSerializer.SerializeToUtf8Bytes(
            expectedArtifact,
            JsonOptions);
        byte[] actualArtifactBytes = ReadFile(artifactPath, "artifact");
        if (!CryptographicOperations.FixedTimeEquals(expectedArtifactBytes, actualArtifactBytes))
        {
            throw new AuthorityFailure(
                "Artifact.NonDeterministic",
                "artifact",
                "The stored artifact does not match deterministic regeneration.");
        }

        AuthorityManifest expectedManifest = BuildManifest(
            definition,
            actualArtifactBytes,
            Hex(Sha256(definitionBytes)),
            approved);
        byte[] expectedManifestBytes = JsonSerializer.SerializeToUtf8Bytes(
            expectedManifest,
            JsonOptions);
        byte[] actualManifestBytes = ReadFile(manifestPath, "manifest");
        if (!CryptographicOperations.FixedTimeEquals(expectedManifestBytes, actualManifestBytes))
        {
            throw new AuthorityFailure(
                "Manifest.NonDeterministic",
                "manifest",
                "The stored manifest does not match deterministic regeneration.");
        }

        AuthorityArtifact? parsed = JsonSerializer.Deserialize<AuthorityArtifact>(
            actualArtifactBytes,
            JsonOptions);
        if (parsed == null || parsed.Status != (approved ? "approved_golden" : "candidate"))
        {
            throw new AuthorityFailure(
                "Artifact.Status.Invalid",
                "artifact.status",
                "The stored artifact status does not match the requested disposition.");
        }

        Console.WriteLine(
            "P4_T06_G4I_VALIDATE_PASS disposition=" +
            (approved ? "approved" : "candidate") +
            " artifact_sha256=" + Hex(Sha256(actualArtifactBytes)) +
            " manifest_sha256=" + Hex(Sha256(actualManifestBytes)) +
            " independent_outer_iterations=" +
            generated.Independent.OuterIterations.ToString(CultureInfo.InvariantCulture) +
            " max_core_reference_error_pending=true");
    }

    private static Definition ParseDefinition(byte[] bytes)
    {
        Definition? definition = JsonSerializer.Deserialize<Definition>(bytes, JsonOptions);
        if (definition == null || definition.Format != DefinitionFormat)
        {
            throw new AuthorityFailure(
                "Definition.Format.Invalid",
                "definition.format",
                "The manufactured-solution definition format is not recognized.");
        }

        if (definition.TaskId != TaskId || definition.Topology == null ||
            definition.ManufacturedParameters == null || definition.Policies == null)
        {
            throw new AuthorityFailure(
                "Definition.Shape.Invalid",
                "definition",
                "The definition must bind the task, topology, manufactured parameters, and policies.");
        }

        if (definition.Topology.Nodes.Count != 4 ||
            definition.Topology.Edges.Count != 4 ||
            definition.Topology.Boundaries.Count != 16)
        {
            throw new AuthorityFailure(
                "Definition.Topology.Invalid",
                "definition.topology",
                "The bounded G4I case must contain four nodes, four reciprocal edges, and sixteen boundary faces.");
        }

        ValidateLength(definition.ManufacturedParameters.VolumeM3ByNode, 4, "volume_m3_by_node");
        ValidateLength(
            definition.ManufacturedParameters.AbsorptionGroup1PerMByNode,
            4,
            "absorption_group1_per_m_by_node");
        ValidateLength(
            definition.ManufacturedParameters.AbsorptionGroup2PerMByNode,
            4,
            "absorption_group2_per_m_by_node");
        ValidateLength(
            definition.ManufacturedParameters.ExactGroup1FluxShape,
            4,
            "exact_group1_flux_shape");
        ValidateLength(
            definition.ManufacturedParameters.ExactGroup2FluxShape,
            4,
            "exact_group2_flux_shape");
        ValidateLength(
            definition.ManufacturedParameters.InitialGroup1Flux,
            4,
            "initial_group1_flux");
        ValidateLength(
            definition.ManufacturedParameters.InitialGroup2Flux,
            4,
            "initial_group2_flux");

        ValidateLength(definition.Policies, 1, "policies");
        ValidateCanonicalTopology(definition.Topology);
        return definition;
    }

    private static void ValidateLength(double[]? values, int expected, string path)
    {
        if (values == null || values.Length != expected ||
            values.Any(value => !double.IsFinite(value)))
        {
            throw new AuthorityFailure(
                "Definition.Array.Invalid",
                "definition." + path,
                "The definition array must contain the expected finite values.");
        }
    }

    private static void ValidateLength(PolicyDefinition policies, int expected, string path)
    {
        if (policies.InnerMaximumIterations < 1 ||
            policies.OuterMaximumIterations < 1 ||
            !double.IsFinite(policies.InnerAbsoluteResidualTolerance) ||
            !double.IsFinite(policies.InnerRelativeResidualTolerance) ||
            !double.IsFinite(policies.KAbsoluteTolerance) ||
            !double.IsFinite(policies.KRelativeTolerance) ||
            !double.IsFinite(policies.ResidualTolerance) ||
            !double.IsFinite(policies.SourceShapeTolerance) ||
            !double.IsFinite(policies.PowerBalanceTolerance))
        {
            throw new AuthorityFailure(
                "Definition.Policy.Invalid",
                "definition." + path,
                "The definition policies must be finite and positively bounded.");
        }
    }

    private static void ValidateCanonicalTopology(TopologyDefinition topology)
    {
        for (int index = 0; index < topology.Nodes.Count; index++)
        {
            NodeDefinition node = topology.Nodes[index];
            if (node.FlatIndex != index || node.ChannelId < 0 || node.ChannelId > 1 ||
                node.Position < 0 || node.Position > 1)
            {
                throw new AuthorityFailure(
                    "Definition.NodeOrder.Invalid",
                    "definition.topology.nodes[" + index + "]",
                    "The case requires canonical channel/position/flat-index order.");
            }
        }

        if (topology.Nodes.Select(node => node.ChannelId + ":" + node.Position)
            .Distinct(StringComparer.Ordinal).Count() != 4)
        {
            throw new AuthorityFailure(
                "Definition.NodeOrder.Duplicate",
                "definition.topology.nodes",
                "Every channel/position pair must occur exactly once.");
        }
    }

    private static GeneratedCase BuildCase(Definition definition)
    {
        ManufacturedParameters parameters = definition.ManufacturedParameters!;
        var exactGroup1 = parameters.ExactGroup1FluxShape!;
        var exactGroup2 = parameters.ExactGroup2FluxShape!;
        var downscatter = new double[4];
        var fissionSource = new double[4];
        var nodes = new DerivedNode[4];

        for (int nodeIndex = 0; nodeIndex < 4; nodeIndex++)
        {
            double group2Left = ApplyGroup(
                definition,
                nodeIndex,
                exactGroup2,
                parameters.AbsorptionGroup2PerMByNode![nodeIndex],
                removalExtra: 0.0,
                group: 2);
            downscatter[nodeIndex] = group2Left / exactGroup1[nodeIndex];
            if (!double.IsFinite(downscatter[nodeIndex]) || downscatter[nodeIndex] <= 0)
            {
                throw new AuthorityFailure(
                    "Manufactured.Downscatter.Invalid",
                    "derived_nodes[" + nodeIndex + "].downscatter_group1_to_2_per_m",
                    "The manufactured group-2 balance must produce a positive finite downscatter coefficient.");
            }

            double group1Left = ApplyGroup(
                definition,
                nodeIndex,
                exactGroup1,
                parameters.AbsorptionGroup1PerMByNode![nodeIndex],
                downscatter[nodeIndex],
                group: 1);
            fissionSource[nodeIndex] = parameters.TargetEigenvalue * group1Left;
            if (!double.IsFinite(fissionSource[nodeIndex]) || fissionSource[nodeIndex] <= 0)
            {
                throw new AuthorityFailure(
                    "Manufactured.FissionSource.Invalid",
                    "derived_nodes[" + nodeIndex + "].fission_source",
                    "The manufactured fission source must be positive and finite.");
            }

            double nuFission1 = parameters.Group1FissionShare *
                fissionSource[nodeIndex] / exactGroup1[nodeIndex];
            double nuFission2 = parameters.Group2FissionShare *
                fissionSource[nodeIndex] / exactGroup2[nodeIndex];
            double fission1 = nuFission1 / parameters.NuFissionToFissionRatio;
            double fission2 = nuFission2 / parameters.NuFissionToFissionRatio;
            if (fission1 > parameters.AbsorptionGroup1PerMByNode[nodeIndex] ||
                fission2 > parameters.AbsorptionGroup2PerMByNode[nodeIndex])
            {
                throw new AuthorityFailure(
                    "Manufactured.FissionAbsorption.Invalid",
                    "derived_nodes[" + nodeIndex + "]",
                    "The manufactured fission absorption must not exceed total absorption.");
            }

            nodes[nodeIndex] = new DerivedNode
            {
                FlatIndex = nodeIndex,
                ChannelId = definition.Topology!.Nodes[nodeIndex].ChannelId,
                Position = definition.Topology!.Nodes[nodeIndex].Position,
                VolumeM3 = parameters.VolumeM3ByNode![nodeIndex],
                AbsorptionGroup1PerM = parameters.AbsorptionGroup1PerMByNode![nodeIndex],
                AbsorptionGroup2PerM = parameters.AbsorptionGroup2PerMByNode![nodeIndex],
                DownscatterGroup1To2PerM = downscatter[nodeIndex],
                FissionGroup1PerM = fission1,
                FissionGroup2PerM = fission2,
                NuFissionGroup1PerM = nuFission1,
                NuFissionGroup2PerM = nuFission2,
                ChiGroup1 = parameters.ChiGroup1,
                ChiGroup2 = parameters.ChiGroup2,
                EnergyPerFissionJ = parameters.EnergyPerFissionJ
            };
        }

        double targetPower = ComputePower(nodes, exactGroup1, exactGroup2);
        if (!double.IsFinite(targetPower) || targetPower <= 0)
        {
            throw new AuthorityFailure(
                "Manufactured.TargetPower.Invalid",
                "normalization.target_power_w",
                "The exact manufactured state must have a positive finite target power.");
        }

        ExactBalance balance = EvaluateExactBalance(
            definition,
            nodes,
            exactGroup1,
            exactGroup2,
            fissionSource,
            parameters.TargetEigenvalue);
        IndependentSolveResult first = Solve(
            definition,
            nodes,
            targetPower,
            parameters.InitialEigenvalue,
            parameters.InitialGroup1Flux!,
            parameters.InitialGroup2Flux!);
        IndependentSolveResult repeat = Solve(
            definition,
            nodes,
            targetPower,
            parameters.InitialEigenvalue,
            parameters.InitialGroup1Flux!,
            parameters.InitialGroup2Flux!);
        byte[] firstBytes = JsonSerializer.SerializeToUtf8Bytes(first, JsonOptions);
        byte[] repeatBytes = JsonSerializer.SerializeToUtf8Bytes(repeat, JsonOptions);
        if (!CryptographicOperations.FixedTimeEquals(firstBytes, repeatBytes))
        {
            throw new AuthorityFailure(
                "IndependentRepeat.Mismatch",
                "independent_reproduction",
                "Two standalone reproductions of the case were not byte-identical.");
        }

        double[] independentGroup1 = first.Group1Flux!;
        double[] independentGroup2 = first.Group2Flux!;
        double maxFluxError = 0.0;
        for (int index = 0; index < 4; index++)
        {
            maxFluxError = Math.Max(
                maxFluxError,
                Math.Abs(independentGroup1[index] - exactGroup1[index]));
            maxFluxError = Math.Max(
                maxFluxError,
                Math.Abs(independentGroup2[index] - exactGroup2[index]));
        }

        return new GeneratedCase
        {
            Nodes = nodes,
            TargetPowerW = targetPower,
            ExactBalance = balance,
            Independent = first,
            IndependentResultSha256 = Hex(Sha256(firstBytes)),
            IndependentRepeatResultSha256 = Hex(Sha256(repeatBytes)),
            IndependentRepeatEqual = true,
            IndependentMaximumFluxAbsoluteError = maxFluxError,
            IndependentEigenvalueAbsoluteError = Math.Abs(
                first.Eigenvalue - parameters.TargetEigenvalue),
            IndependentTotalPowerAbsoluteError = Math.Abs(first.TotalPowerW - targetPower)
        };
    }

    private static ExactBalance EvaluateExactBalance(
        Definition definition,
        DerivedNode[] nodes,
        double[] group1,
        double[] group2,
        double[] fissionSource,
        double eigenvalue)
    {
        double maximumAbsolute = 0.0;
        double maximumScale = 0.0;
        double maximumGroup1Absolute = 0.0;
        double maximumGroup2Absolute = 0.0;
        for (int index = 0; index < 4; index++)
        {
            double left1 = ApplyGroup(
                definition,
                index,
                group1,
                nodes[index].AbsorptionGroup1PerM,
                nodes[index].DownscatterGroup1To2PerM,
                group: 1);
            double left2 = ApplyGroup(
                definition,
                index,
                group2,
                nodes[index].AbsorptionGroup2PerM,
                removalExtra: 0.0,
                group: 2);
            double right1 = nodes[index].ChiGroup1 * fissionSource[index] / eigenvalue;
            double right2 = nodes[index].DownscatterGroup1To2PerM * group1[index] +
                nodes[index].ChiGroup2 * fissionSource[index] / eigenvalue;
            double group1Difference = Math.Abs(left1 - right1);
            double group2Difference = Math.Abs(left2 - right2);
            maximumGroup1Absolute = Math.Max(maximumGroup1Absolute, group1Difference);
            maximumGroup2Absolute = Math.Max(maximumGroup2Absolute, group2Difference);
            maximumAbsolute = Math.Max(maximumAbsolute, group1Difference);
            maximumAbsolute = Math.Max(maximumAbsolute, group2Difference);
            maximumScale = Math.Max(maximumScale, Math.Abs(left1) + Math.Abs(right1));
            maximumScale = Math.Max(maximumScale, Math.Abs(left2) + Math.Abs(right2));
        }

        return new ExactBalance
        {
            MaximumAbsolute = maximumAbsolute,
            RelativeInfinity = maximumScale == 0.0 ? 0.0 : maximumAbsolute / maximumScale,
            Group1MaximumAbsolute = maximumGroup1Absolute,
            Group2MaximumAbsolute = maximumGroup2Absolute
        };
    }

    private static IndependentSolveResult Solve(
        Definition definition,
        DerivedNode[] nodes,
        double targetPower,
        double initialEigenvalue,
        double[] initialGroup1,
        double[] initialGroup2)
    {
        PolicyDefinition policy = definition.Policies!;
        double[] group1 = (double[])initialGroup1.Clone();
        double[] group2 = (double[])initialGroup2.Clone();
        double initialPower = ComputePower(nodes, group1, group2);
        double initialScale = targetPower / initialPower;
        Scale(group1, initialScale);
        Scale(group2, initialScale);
        double eigenvalue = initialEigenvalue;
        double[] previousSourceShape = new double[4];
        IndependentSolveResult? last = null;

        for (int outer = 0; outer < policy.OuterMaximumIterations; outer++)
        {
            double[] currentFissionSource = ComputeFissionSource(nodes, group1, group2);
            double currentProduction = ComputeProduction(nodes, currentFissionSource);
            double[] group1Source = new double[4];
            for (int index = 0; index < 4; index++)
            {
                group1Source[index] = nodes[index].ChiGroup1 * currentFissionSource[index] / eigenvalue;
            }

            int group1InnerIterations;
            double[] trialGroup1 = InnerSolve(
                definition,
                nodes,
                group1Source,
                group: 1,
                out group1InnerIterations);
            double[] group2Source = new double[4];
            for (int index = 0; index < 4; index++)
            {
                group2Source[index] = nodes[index].DownscatterGroup1To2PerM * trialGroup1[index] +
                    nodes[index].ChiGroup2 * currentFissionSource[index] / eigenvalue;
            }

            int group2InnerIterations;
            double[] trialGroup2 = InnerSolve(
                definition,
                nodes,
                group2Source,
                group: 2,
                out group2InnerIterations);
            double trialProduction = ComputeProduction(
                nodes,
                ComputeFissionSource(nodes, trialGroup1, trialGroup2));
            double nextEigenvalue = eigenvalue * (trialProduction / currentProduction);
            double trialPower = ComputePower(nodes, trialGroup1, trialGroup2);
            double normalizationScale = targetPower / trialPower;
            double[] nextGroup1 = (double[])trialGroup1.Clone();
            double[] nextGroup2 = (double[])trialGroup2.Clone();
            Scale(nextGroup1, normalizationScale);
            Scale(nextGroup2, normalizationScale);

            double[] nextFissionSource = ComputeFissionSource(nodes, nextGroup1, nextGroup2);
            double[] nextSourceShape = new double[4];
            double nextProduction = ComputeProduction(nodes, nextFissionSource);
            for (int index = 0; index < 4; index++)
            {
                nextSourceShape[index] = nodes[index].VolumeM3 *
                    nextFissionSource[index] / nextProduction;
            }

            StateMetrics metrics = EvaluateState(
                definition,
                nodes,
                nextGroup1,
                nextGroup2,
                nextEigenvalue,
                targetPower,
                nextSourceShape);
            double deltaEigenvalueAbsolute = Math.Abs(nextEigenvalue - eigenvalue);
            double eigenvalueScale = Math.Max(
                Math.Abs(nextEigenvalue),
                Math.Abs(eigenvalue));
            double deltaEigenvalueRelative = deltaEigenvalueAbsolute / eigenvalueScale;
            double sourceShapeChange = 0.0;
            for (int index = 0; index < 4; index++)
            {
                sourceShapeChange = Math.Max(
                    sourceShapeChange,
                    Math.Abs(nextSourceShape[index] - previousSourceShape[index]));
            }

            last = new IndependentSolveResult
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
                FissionSourceByNode = nextFissionSource
            };

            bool eigenvalueConverged =
                deltaEigenvalueAbsolute <= policy.KAbsoluteTolerance ||
                deltaEigenvalueRelative <= policy.KRelativeTolerance;
            if (eigenvalueConverged &&
                metrics.ResidualRelativeInfinity <= policy.ResidualTolerance &&
                sourceShapeChange <= policy.SourceShapeTolerance &&
                metrics.PowerBalanceRelative <= policy.PowerBalanceTolerance)
            {
                last.Converged = true;
                last.ConvergenceReason = "converged";
                return last;
            }

            Array.Copy(nextSourceShape, previousSourceShape, 4);
            group1 = nextGroup1;
            group2 = nextGroup2;
            eigenvalue = nextEigenvalue;
        }

        throw new AuthorityFailure(
            "IndependentSolve.Nonconverged",
            "independent_reproduction",
            "The standalone solve did not satisfy all configured convergence conditions.");
    }

    private static double[] InnerSolve(
        Definition definition,
        DerivedNode[] nodes,
        double[] source,
        int group,
        out int iterations)
    {
        PolicyDefinition policy = definition.Policies!;
        double[] diagonal = new double[4];
        for (int index = 0; index < 4; index++)
        {
            double value = group == 1
                ? nodes[index].AbsorptionGroup1PerM + nodes[index].DownscatterGroup1To2PerM
                : nodes[index].AbsorptionGroup2PerM;
            foreach (EdgeDefinition edge in definition.Topology!.Edges)
            {
                if (NodeMatches(edge.EndpointA, nodes[index]) || NodeMatches(edge.EndpointB, nodes[index]))
                {
                    value += group == 1 ? edge.Group1M2 : edge.Group2M2;
                }
            }

            foreach (BoundaryDefinition boundary in definition.Topology.Boundaries)
            {
                if (boundary.Node.ChannelId == nodes[index].ChannelId &&
                    boundary.Node.Position == nodes[index].Position)
                {
                    value += group == 1 ? boundary.Group1M2 : boundary.Group2M2;
                }
            }

            diagonal[index] = value;
        }

        double[] solution = new double[4];
        double[] candidate = new double[4];
        double[] applied = new double[4];
        for (int inner = 0; inner < policy.InnerMaximumIterations; inner++)
        {
            ApplyGroup(definition, nodes, solution, applied, group);
            for (int index = 0; index < 4; index++)
            {
                candidate[index] = solution[index] + (source[index] - applied[index]) / diagonal[index];
                if (!double.IsFinite(candidate[index]) || candidate[index] < 0)
                {
                    throw new AuthorityFailure(
                        "IndependentSolve.InnerState.Invalid",
                        "independent_reproduction",
                        "The standalone Jacobi update produced an invalid flux.");
                }
            }

            ApplyGroup(definition, nodes, candidate, applied, group);
            double absoluteResidual = 0.0;
            double scale = 0.0;
            for (int index = 0; index < 4; index++)
            {
                absoluteResidual = Math.Max(
                    absoluteResidual,
                    Math.Abs(applied[index] - source[index]));
                scale = Math.Max(
                    scale,
                    Math.Abs(applied[index]) + Math.Abs(source[index]));
            }

            double relativeResidual = scale == 0.0 ? 0.0 : absoluteResidual / scale;
            if (absoluteResidual <= policy.InnerAbsoluteResidualTolerance ||
                relativeResidual <= policy.InnerRelativeResidualTolerance)
            {
                iterations = inner + 1;
                return (double[])candidate.Clone();
            }

            Array.Copy(candidate, solution, 4);
        }

        throw new AuthorityFailure(
            "IndependentSolve.InnerNonconverged",
            "independent_reproduction",
            "The standalone Jacobi inner solve exhausted its configured iteration limit.");
    }

    private static StateMetrics EvaluateState(
        Definition definition,
        DerivedNode[] nodes,
        double[] group1,
        double[] group2,
        double eigenvalue,
        double targetPower,
        double[] sourceShape)
    {
        double[] fissionSource = ComputeFissionSource(nodes, group1, group2);
        double residualAbsolute = 0.0;
        double residualScale = 0.0;
        for (int index = 0; index < 4; index++)
        {
            double left1 = ApplyGroup(
                definition,
                index,
                group1,
                nodes[index].AbsorptionGroup1PerM,
                nodes[index].DownscatterGroup1To2PerM,
                group: 1);
            double left2 = ApplyGroup(
                definition,
                index,
                group2,
                nodes[index].AbsorptionGroup2PerM,
                removalExtra: 0.0,
                group: 2);
            double right1 = nodes[index].ChiGroup1 * fissionSource[index] / eigenvalue;
            double right2 = nodes[index].DownscatterGroup1To2PerM * group1[index] +
                nodes[index].ChiGroup2 * fissionSource[index] / eigenvalue;
            AddResidual(left1, right1, ref residualAbsolute, ref residualScale);
            AddResidual(left2, right2, ref residualAbsolute, ref residualScale);
        }

        double power = ComputePower(nodes, group1, group2);
        return new StateMetrics
        {
            ResidualAbsoluteInfinity = residualAbsolute,
            ResidualRelativeInfinity = residualScale == 0.0 ? 0.0 : residualAbsolute / residualScale,
            PowerBalanceRelative = Math.Abs(power - targetPower) / targetPower
        };
    }

    private static void AddResidual(
        double left,
        double right,
        ref double maximumAbsolute,
        ref double maximumScale)
    {
        maximumAbsolute = Math.Max(maximumAbsolute, Math.Abs(left - right));
        maximumScale = Math.Max(maximumScale, Math.Abs(left) + Math.Abs(right));
    }

    private static double ApplyGroup(
        Definition definition,
        int nodeIndex,
        double[] flux,
        double absorption,
        double removalExtra,
        int group)
    {
        return ApplyGroup(
            definition,
            null,
            flux,
            nodeIndex,
            absorption,
            removalExtra,
            group);
    }

    private static double ApplyGroup(
        Definition definition,
        DerivedNode[]? nodes,
        double[] flux,
        int nodeIndex,
        double absorption,
        double removalExtra,
        int group)
    {
        NodeDefinition node = definition.Topology!.Nodes[nodeIndex];
        double value = (absorption + removalExtra) * flux[nodeIndex];
        double leakage = 0.0;
        foreach (EdgeDefinition edge in definition.Topology.Edges)
        {
            if (NodeMatches(edge.EndpointA, node))
            {
                int target = FindNodeIndex(definition.Topology, edge.EndpointB);
                leakage += (group == 1 ? edge.Group1M2 : edge.Group2M2) *
                    (flux[nodeIndex] - flux[target]);
            }
            else if (NodeMatches(edge.EndpointB, node))
            {
                int target = FindNodeIndex(definition.Topology, edge.EndpointA);
                leakage += (group == 1 ? edge.Group1M2 : edge.Group2M2) *
                    (flux[nodeIndex] - flux[target]);
            }
        }

        foreach (BoundaryDefinition boundary in definition.Topology.Boundaries)
        {
            if (NodeMatches(boundary.Node, node))
            {
                leakage += (group == 1 ? boundary.Group1M2 : boundary.Group2M2) * flux[nodeIndex];
            }
        }

        double volume = nodes == null
            ? definition.ManufacturedParameters!.VolumeM3ByNode![nodeIndex]
            : nodes[nodeIndex].VolumeM3;
        return value + leakage / volume;
    }

    private static void ApplyGroup(
        Definition definition,
        DerivedNode[] nodes,
        double[] flux,
        double[] destination,
        int group)
    {
        for (int index = 0; index < 4; index++)
        {
            destination[index] = ApplyGroup(
                definition,
                nodes,
                flux,
                index,
                group == 1 ? nodes[index].AbsorptionGroup1PerM : nodes[index].AbsorptionGroup2PerM,
                group == 1 ? nodes[index].DownscatterGroup1To2PerM : 0.0,
                group);
        }
    }

    private static int FindNodeIndex(TopologyDefinition topology, NodeDefinition node)
    {
        for (int index = 0; index < topology.Nodes.Count; index++)
        {
            if (NodeMatches(topology.Nodes[index], node))
            {
                return index;
            }
        }

        throw new AuthorityFailure(
            "Definition.Node.Missing",
            "definition.topology",
            "An edge or boundary references a node outside the canonical node order.");
    }

    private static bool NodeMatches(NodeDefinition left, NodeDefinition right)
    {
        return left.ChannelId == right.ChannelId && left.Position == right.Position;
    }

    private static bool NodeMatches(NodeDefinition left, DerivedNode right)
    {
        return left.ChannelId == right.ChannelId && left.Position == right.Position;
    }

    private static double[] ComputeFissionSource(
        DerivedNode[] nodes,
        double[] group1,
        double[] group2)
    {
        var result = new double[4];
        for (int index = 0; index < 4; index++)
        {
            result[index] = nodes[index].NuFissionGroup1PerM * group1[index] +
                nodes[index].NuFissionGroup2PerM * group2[index];
        }

        return result;
    }

    private static double ComputeProduction(DerivedNode[] nodes, double[] fissionSource)
    {
        double production = 0.0;
        for (int index = 0; index < 4; index++)
        {
            production += nodes[index].VolumeM3 * fissionSource[index];
        }

        return production;
    }

    private static double ComputePower(
        DerivedNode[] nodes,
        double[] group1,
        double[] group2)
    {
        double power = 0.0;
        for (int index = 0; index < 4; index++)
        {
            double fissionRate = nodes[index].FissionGroup1PerM * group1[index] +
                nodes[index].FissionGroup2PerM * group2[index];
            power += nodes[index].VolumeM3 * nodes[index].EnergyPerFissionJ * fissionRate;
        }

        return power;
    }

    private static void Scale(double[] values, double scale)
    {
        for (int index = 0; index < values.Length; index++)
        {
            values[index] *= scale;
        }
    }

    private static AuthorityArtifact BuildArtifact(
        Definition definition,
        GeneratedCase generated,
        string definitionSha256,
        bool approved)
    {
        PolicyDefinition policy = definition.Policies!;
        ManufacturedParameters parameters = definition.ManufacturedParameters!;
        string disposition = approved ? "approved_golden" : "candidate";
        string evidenceApproval = approved ? "Approved" : "Candidate";
        string comparisonStatus = approved ? "Approved" : "Deferred";
        string toleranceStatus = approved ? "Approved" : "Deferred";
        string goldenStatus = approved ? "ApprovedGolden" : "NoGolden";
        string artifactRelativePath = approved ? ApprovedRelativePath : CandidateRelativePath;
        return new AuthorityArtifact
        {
            Format = ArtifactFormat,
            TaskId = TaskId,
            ArtifactId = ArtifactId,
            CaseId = definition.CaseId,
            Status = disposition,
            EvidenceClass = "synthetic",
            CoverageClass = "Synthetic",
            ArtifactAvailability = "CommittedSynthetic",
            EvidenceApproval = evidenceApproval,
            ValidationDomain = "Synthetic",
            ComparisonStatus = comparisonStatus,
            ToleranceStatus = toleranceStatus,
            GoldenStatus = goldenStatus,
            ApprovalScope = approved
                ? "Synthetic manufactured spatial-solver authority only; not a direct CANDU physics baseline."
                : "Candidate synthetic manufactured spatial-solver evidence; no production or golden approval.",
            EvidencePath = artifactRelativePath,
            DefinitionPath = DefinitionRelativePath,
            DefinitionSha256 = definitionSha256,
            SourceAuthority = new SourceAuthority
            {
                SourceProgram = "Project-authored deterministic manufactured-solution generator",
                SourceVersion = GeneratorVersion,
                BuildIdentity = "ReactorSim.ManufacturedSpatialAuthority net10.0; Release build; dotnet SDK recorded by task validation",
                SourceCommit = "UNAVAILABLE: workspace has no committed HEAD; definition, artifact, manifest, and source paths are hash-bound in the task report",
                CouplingToolIdentity = "None; standalone algebraic reference with no external solver or runtime coupling",
                GeneratorId = GeneratorId,
                GeneratorVersion = GeneratorVersion
            },
            NuclearData = new NuclearDataAuthority
            {
                Library = "NotApplicable",
                Version = "NotApplicable",
                Processing = "NotApplicable",
                Format = "NotApplicable",
                Checksum = "NotApplicable",
                Applicability = "NotApplicable: all coefficients are project-authored synthetic values derived from the manufactured balance",
                ExternalDataUsed = false
            },
            Geometry = new GeometryAuthority
            {
                ChannelCount = definition.Topology!.ChannelCount,
                BundlePositionCount = definition.Topology.BundlePositionCount,
                FlowDirection = definition.Topology.FlowDirection,
                NodeOrder = definition.Topology.Nodes,
                Edges = definition.Topology.Edges,
                Boundaries = definition.Topology.Boundaries,
                HomogenizationMapping = "NotApplicable: each synthetic node coefficient is bound directly to one explicit node"
            },
            State = new StateAuthority
            {
                InitialEigenvalue = parameters.InitialEigenvalue,
                InitialGroup1Flux = parameters.InitialGroup1Flux,
                InitialGroup2Flux = parameters.InitialGroup2Flux,
                StateIdentity = "g4i_manufactured_static_state_v1",
                DepletionHistory = "NotApplicable: static manufactured state; no burnup or refuelling history",
                EventOrdering = "NotApplicable: no state-transition events"
            },
            Units = new UnitsAuthority
            {
                ProfileId = "SI-v1",
                Flux = "m^-2 s^-1",
                Volume = "m^3",
                CrossSections = "m^-1",
                Conductances = "m^2",
                EnergyPerFission = "J",
                Power = "W",
                Eigenvalue = "1",
                ConversionRules = "No implicit conversions; all values are serialized in the listed SI units"
            },
            Normalization = new NormalizationAuthority
            {
                TargetPowerW = generated.TargetPowerW,
                ReferenceState = "Exact manufactured flux shape before any iterative normalization scale",
                SignConvention = "Positive removal, positive leakage, positive fission source; operator is A(phi)=F(phi)/k",
                FluxScale = "The exact manufactured shape is already normalized to target_power_w; standalone and Core consumers must preserve this power"
            },
            Coefficients = generated.Nodes,
            ManufacturedSolution = new ManufacturedSolution
            {
                ExactEigenvalue = parameters.TargetEigenvalue,
                ExactGroup1Flux = parameters.ExactGroup1FluxShape,
                ExactGroup2Flux = parameters.ExactGroup2FluxShape,
                ExactFissionSourceByNode = generated.Nodes
                    .Select(node => node.NuFissionGroup1PerM * parameters.ExactGroup1FluxShape![node.FlatIndex] +
                        node.NuFissionGroup2PerM * parameters.ExactGroup2FluxShape![node.FlatIndex])
                    .ToArray(),
                EquationResidualAbsoluteInfinity = generated.ExactBalance.MaximumAbsolute,
                EquationResidualRelativeInfinity = generated.ExactBalance.RelativeInfinity,
                Group1EquationResidualAbsoluteInfinity = generated.ExactBalance.Group1MaximumAbsolute,
                Group2EquationResidualAbsoluteInfinity = generated.ExactBalance.Group2MaximumAbsolute
            },
            Observables = new ObservableAuthority
            {
                QuantityIds = new[]
                {
                    "spatial.k",
                    "spatial.flux",
                    "spatial.node_power",
                    "spatial.total_power",
                    "spatial.equation_residual",
                    "spatial.convergence"
                },
                NodeOrdering = "flat_index ascending; channel_id ascending then position ascending; group order group1 then group2",
                ComparisonRule = "Exact quantity identity and state binding; numeric profiles are explicit and no hidden epsilon is permitted",
                SchemaVersion = "P2-T05-observables-v1"
            },
            Convergence = new ConvergenceAuthority
            {
                InnerMethod = "deterministic scalar Jacobi",
                InnerMethodVersion = "P2-T02-jacobi-v1",
                InnerAbsoluteResidualTolerance = policy.InnerAbsoluteResidualTolerance,
                InnerRelativeResidualTolerance = policy.InnerRelativeResidualTolerance,
                InnerMaximumIterations = policy.InnerMaximumIterations,
                OuterKAbsoluteTolerance = policy.KAbsoluteTolerance,
                OuterKRelativeTolerance = policy.KRelativeTolerance,
                OuterResidualTolerance = policy.ResidualTolerance,
                OuterSourceShapeTolerance = policy.SourceShapeTolerance,
                OuterPowerBalanceTolerance = policy.PowerBalanceTolerance,
                OuterMaximumIterations = policy.OuterMaximumIterations,
                IndependentReproduction = generated.Independent
            },
            Tolerance = BuildToleranceDisposition(approved, generated),
            Rights = new RightsAuthority
            {
                License = "Project-authored synthetic fixture; no third-party input or nuclear-data payload",
                Redistribution = "Permitted for repository review and downstream test use within this project",
                ExternalRestrictions = "None identified; this artifact is not a redistribution of DRAGON5, DONJON5, or nuclear-data files"
            },
            GeneratorChecks = new GeneratorChecks
            {
                IndependentRepeatEqual = generated.IndependentRepeatEqual,
                IndependentResultSha256 = generated.IndependentResultSha256,
                IndependentRepeatResultSha256 = generated.IndependentRepeatResultSha256,
                IndependentMaximumFluxAbsoluteError = generated.IndependentMaximumFluxAbsoluteError,
                IndependentEigenvalueAbsoluteError = generated.IndependentEigenvalueAbsoluteError,
                IndependentTotalPowerAbsoluteError = generated.IndependentTotalPowerAbsoluteError,
                GeneratorValidation = "Generate/validate byte equality plus standalone repeat equality"
            }
        };
    }

    private static ToleranceDisposition BuildToleranceDisposition(
        bool approved,
        GeneratedCase generated)
    {
        if (!approved)
        {
            return new ToleranceDisposition
            {
                Status = "Deferred",
                Authority = "G4",
                Basis = "No tolerance is selected by the candidate-generation task; observed differences are diagnostic only",
                Profiles = null
            };
        }

        return new ToleranceDisposition
        {
            Status = "Approved",
            Authority = "G4-R4 synthetic-only disposition",
            Basis = "Explicit G4 profile selected after exact manufactured balance, standalone repeat, Core consumer, and T3/T6 validation",
            Profiles = new Dictionary<string, ApprovedToleranceProfile>(StringComparer.Ordinal)
            {
                ["spatial.k"] = new("absolute", 1e-10, 1e-10),
                ["spatial.flux"] = new("absolute_and_relative", 1e-10, 1e-10),
                ["spatial.node_power"] = new("absolute_and_relative", 1e-10, 1e-10),
                ["spatial.total_power"] = new("absolute", 1e-10, 1e-10),
                ["spatial.equation_residual"] = new("relative", 1e-10, 1e-10),
                ["spatial.convergence"] = new("exact_discrete_and_profile", 0.0, 0.0)
            }
        };
    }

    private static AuthorityManifest BuildManifest(
        Definition definition,
        byte[] artifactBytes,
        string definitionSha256,
        bool approved)
    {
        return new AuthorityManifest
        {
            Format = ManifestFormat,
            TaskId = TaskId,
            ArtifactId = ArtifactId,
            CaseId = definition.CaseId,
            ArtifactPath = approved ? ApprovedRelativePath : CandidateRelativePath,
            DefinitionPath = DefinitionRelativePath,
            DefinitionSha256 = definitionSha256,
            ArtifactSha256 = Hex(Sha256(artifactBytes)),
            Disposition = approved ? "approved_golden" : "candidate",
            GeneratorId = GeneratorId,
            GeneratorVersion = GeneratorVersion,
            SourceCommit = "UNAVAILABLE: workspace has no committed HEAD; content hashes are the reproducibility binding"
        };
    }

    private static byte[] ReadFile(string path, string label)
    {
        if (!File.Exists(path))
        {
            throw new AuthorityFailure(
                "Input.Missing",
                label,
                "The required input file does not exist.");
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

    public string Description { get; set; } = string.Empty;

    public TopologyDefinition? Topology { get; set; }

    public ManufacturedParameters? ManufacturedParameters { get; set; }

    public PolicyDefinition? Policies { get; set; }
}

internal sealed class TopologyDefinition
{
    public int ChannelCount { get; set; }

    public int BundlePositionCount { get; set; }

    public string FlowDirection { get; set; } = string.Empty;

    public List<NodeDefinition> Nodes { get; set; } = new();

    public List<EdgeDefinition> Edges { get; set; } = new();

    public List<BoundaryDefinition> Boundaries { get; set; } = new();
}

internal sealed class NodeDefinition
{
    public int ChannelId { get; set; }

    public int Position { get; set; }

    public int FlatIndex { get; set; }
}

internal sealed class EdgeDefinition
{
    public NodeDefinition EndpointA { get; set; } = new();

    public NodeDefinition EndpointB { get; set; } = new();

    public string DirectionAToB { get; set; } = string.Empty;

    public string DirectionBToA { get; set; } = string.Empty;

    public double Group1M2 { get; set; }

    public double Group2M2 { get; set; }
}

internal sealed class BoundaryDefinition
{
    public NodeDefinition Node { get; set; } = new();

    public string Face { get; set; } = string.Empty;

    public string Classification { get; set; } = string.Empty;

    public double Group1M2 { get; set; }

    public double Group2M2 { get; set; }
}

internal sealed class ManufacturedParameters
{
    public double TargetEigenvalue { get; set; }

    public double[]? VolumeM3ByNode { get; set; }

    public double[]? AbsorptionGroup1PerMByNode { get; set; }

    public double[]? AbsorptionGroup2PerMByNode { get; set; }

    public double[]? ExactGroup1FluxShape { get; set; }

    public double[]? ExactGroup2FluxShape { get; set; }

    public double Group1FissionShare { get; set; }

    public double Group2FissionShare { get; set; }

    public double NuFissionToFissionRatio { get; set; }

    public double ChiGroup1 { get; set; }

    public double ChiGroup2 { get; set; }

    public double EnergyPerFissionJ { get; set; }

    public double InitialEigenvalue { get; set; }

    public double[]? InitialGroup1Flux { get; set; }

    public double[]? InitialGroup2Flux { get; set; }
}

internal sealed class PolicyDefinition
{
    public double InnerAbsoluteResidualTolerance { get; set; }

    public double InnerRelativeResidualTolerance { get; set; }

    public int InnerMaximumIterations { get; set; }

    public double KAbsoluteTolerance { get; set; }

    public double KRelativeTolerance { get; set; }

    public double ResidualTolerance { get; set; }

    public double SourceShapeTolerance { get; set; }

    public double PowerBalanceTolerance { get; set; }

    public int OuterMaximumIterations { get; set; }
}

internal sealed class GeneratedCase
{
    public DerivedNode[] Nodes { get; set; } = Array.Empty<DerivedNode>();

    public double TargetPowerW { get; set; }

    public ExactBalance ExactBalance { get; set; } = new();

    public IndependentSolveResult Independent { get; set; } = new();

    public string IndependentResultSha256 { get; set; } = string.Empty;

    public string IndependentRepeatResultSha256 { get; set; } = string.Empty;

    public bool IndependentRepeatEqual { get; set; }

    public double IndependentMaximumFluxAbsoluteError { get; set; }

    public double IndependentEigenvalueAbsoluteError { get; set; }

    public double IndependentTotalPowerAbsoluteError { get; set; }
}

internal sealed class DerivedNode
{
    public int FlatIndex { get; set; }

    public int ChannelId { get; set; }

    public int Position { get; set; }

    public double VolumeM3 { get; set; }

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
}

internal sealed class ExactBalance
{
    public double MaximumAbsolute { get; set; }

    public double RelativeInfinity { get; set; }

    public double Group1MaximumAbsolute { get; set; }

    public double Group2MaximumAbsolute { get; set; }
}

internal sealed class StateMetrics
{
    public double ResidualAbsoluteInfinity { get; set; }

    public double ResidualRelativeInfinity { get; set; }

    public double PowerBalanceRelative { get; set; }
}

internal sealed class IndependentSolveResult
{
    public bool Converged { get; set; }

    public string ConvergenceReason { get; set; } = string.Empty;

    public int OuterIterations { get; set; }

    public int Group1InnerIterations { get; set; }

    public int Group2InnerIterations { get; set; }

    public double Eigenvalue { get; set; }

    public double NormalizationScale { get; set; }

    public double TotalPowerW { get; set; }

    public double[]? Group1Flux { get; set; }

    public double[]? Group2Flux { get; set; }

    public double[]? FissionSourceByNode { get; set; }

    public double ResidualAbsoluteInfinity { get; set; }

    public double ResidualRelativeInfinity { get; set; }

    public double SourceShapeChangeInfinity { get; set; }

    public double PowerBalanceRelative { get; set; }
}

internal sealed class AuthorityArtifact
{
    public string Format { get; set; } = string.Empty;

    public string TaskId { get; set; } = string.Empty;

    public string ArtifactId { get; set; } = string.Empty;

    public string CaseId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string EvidenceClass { get; set; } = string.Empty;

    public string CoverageClass { get; set; } = string.Empty;

    public string ArtifactAvailability { get; set; } = string.Empty;

    public string EvidenceApproval { get; set; } = string.Empty;

    public string ValidationDomain { get; set; } = string.Empty;

    public string ComparisonStatus { get; set; } = string.Empty;

    public string ToleranceStatus { get; set; } = string.Empty;

    public string GoldenStatus { get; set; } = string.Empty;

    public string ApprovalScope { get; set; } = string.Empty;

    public string EvidencePath { get; set; } = string.Empty;

    public string DefinitionPath { get; set; } = string.Empty;

    public string DefinitionSha256 { get; set; } = string.Empty;

    public SourceAuthority SourceAuthority { get; set; } = new();

    public NuclearDataAuthority NuclearData { get; set; } = new();

    public GeometryAuthority Geometry { get; set; } = new();

    public StateAuthority State { get; set; } = new();

    public UnitsAuthority Units { get; set; } = new();

    public NormalizationAuthority Normalization { get; set; } = new();

    public DerivedNode[] Coefficients { get; set; } = Array.Empty<DerivedNode>();

    public ManufacturedSolution ManufacturedSolution { get; set; } = new();

    public ObservableAuthority Observables { get; set; } = new();

    public ConvergenceAuthority Convergence { get; set; } = new();

    public ToleranceDisposition Tolerance { get; set; } = new();

    public RightsAuthority Rights { get; set; } = new();

    public GeneratorChecks GeneratorChecks { get; set; } = new();
}

internal sealed class SourceAuthority
{
    public string SourceProgram { get; set; } = string.Empty;

    public string SourceVersion { get; set; } = string.Empty;

    public string BuildIdentity { get; set; } = string.Empty;

    public string SourceCommit { get; set; } = string.Empty;

    public string CouplingToolIdentity { get; set; } = string.Empty;

    public string GeneratorId { get; set; } = string.Empty;

    public string GeneratorVersion { get; set; } = string.Empty;
}

internal sealed class NuclearDataAuthority
{
    public string Library { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public string Processing { get; set; } = string.Empty;

    public string Format { get; set; } = string.Empty;

    public string Checksum { get; set; } = string.Empty;

    public string Applicability { get; set; } = string.Empty;

    public bool ExternalDataUsed { get; set; }
}

internal sealed class GeometryAuthority
{
    public int ChannelCount { get; set; }

    public int BundlePositionCount { get; set; }

    public string FlowDirection { get; set; } = string.Empty;

    public List<NodeDefinition> NodeOrder { get; set; } = new();

    public List<EdgeDefinition> Edges { get; set; } = new();

    public List<BoundaryDefinition> Boundaries { get; set; } = new();

    public string HomogenizationMapping { get; set; } = string.Empty;
}

internal sealed class StateAuthority
{
    public double InitialEigenvalue { get; set; }

    public double[]? InitialGroup1Flux { get; set; }

    public double[]? InitialGroup2Flux { get; set; }

    public string StateIdentity { get; set; } = string.Empty;

    public string DepletionHistory { get; set; } = string.Empty;

    public string EventOrdering { get; set; } = string.Empty;
}

internal sealed class UnitsAuthority
{
    public string ProfileId { get; set; } = string.Empty;

    public string Flux { get; set; } = string.Empty;

    public string Volume { get; set; } = string.Empty;

    public string CrossSections { get; set; } = string.Empty;

    public string Conductances { get; set; } = string.Empty;

    public string EnergyPerFission { get; set; } = string.Empty;

    public string Power { get; set; } = string.Empty;

    public string Eigenvalue { get; set; } = string.Empty;

    public string ConversionRules { get; set; } = string.Empty;
}

internal sealed class NormalizationAuthority
{
    public double TargetPowerW { get; set; }

    public string ReferenceState { get; set; } = string.Empty;

    public string SignConvention { get; set; } = string.Empty;

    public string FluxScale { get; set; } = string.Empty;
}

internal sealed class ManufacturedSolution
{
    public double ExactEigenvalue { get; set; }

    public double[]? ExactGroup1Flux { get; set; }

    public double[]? ExactGroup2Flux { get; set; }

    public double[]? ExactFissionSourceByNode { get; set; }

    public double EquationResidualAbsoluteInfinity { get; set; }

    public double EquationResidualRelativeInfinity { get; set; }

    public double Group1EquationResidualAbsoluteInfinity { get; set; }

    public double Group2EquationResidualAbsoluteInfinity { get; set; }
}

internal sealed class ObservableAuthority
{
    public string[] QuantityIds { get; set; } = Array.Empty<string>();

    public string NodeOrdering { get; set; } = string.Empty;

    public string ComparisonRule { get; set; } = string.Empty;

    public string SchemaVersion { get; set; } = string.Empty;
}

internal sealed class ConvergenceAuthority
{
    public string InnerMethod { get; set; } = string.Empty;

    public string InnerMethodVersion { get; set; } = string.Empty;

    public double InnerAbsoluteResidualTolerance { get; set; }

    public double InnerRelativeResidualTolerance { get; set; }

    public int InnerMaximumIterations { get; set; }

    public double OuterKAbsoluteTolerance { get; set; }

    public double OuterKRelativeTolerance { get; set; }

    public double OuterResidualTolerance { get; set; }

    public double OuterSourceShapeTolerance { get; set; }

    public double OuterPowerBalanceTolerance { get; set; }

    public int OuterMaximumIterations { get; set; }

    public IndependentSolveResult IndependentReproduction { get; set; } = new();
}

internal sealed class ToleranceDisposition
{
    public string Status { get; set; } = string.Empty;

    public string Authority { get; set; } = string.Empty;

    public string Basis { get; set; } = string.Empty;

    public Dictionary<string, ApprovedToleranceProfile>? Profiles { get; set; }
}

internal sealed record ApprovedToleranceProfile(
    string Rule,
    double Absolute,
    double Relative);

internal sealed class RightsAuthority
{
    public string License { get; set; } = string.Empty;

    public string Redistribution { get; set; } = string.Empty;

    public string ExternalRestrictions { get; set; } = string.Empty;
}

internal sealed class GeneratorChecks
{
    public bool IndependentRepeatEqual { get; set; }

    public string IndependentResultSha256 { get; set; } = string.Empty;

    public string IndependentRepeatResultSha256 { get; set; } = string.Empty;

    public double IndependentMaximumFluxAbsoluteError { get; set; }

    public double IndependentEigenvalueAbsoluteError { get; set; }

    public double IndependentTotalPowerAbsoluteError { get; set; }

    public string GeneratorValidation { get; set; } = string.Empty;
}

internal sealed class AuthorityManifest
{
    public string Format { get; set; } = string.Empty;

    public string TaskId { get; set; } = string.Empty;

    public string ArtifactId { get; set; } = string.Empty;

    public string CaseId { get; set; } = string.Empty;

    public string ArtifactPath { get; set; } = string.Empty;

    public string DefinitionPath { get; set; } = string.Empty;

    public string DefinitionSha256 { get; set; } = string.Empty;

    public string ArtifactSha256 { get; set; } = string.Empty;

    public string Disposition { get; set; } = string.Empty;

    public string GeneratorId { get; set; } = string.Empty;

    public string GeneratorVersion { get; set; } = string.Empty;

    public string SourceCommit { get; set; } = string.Empty;
}
