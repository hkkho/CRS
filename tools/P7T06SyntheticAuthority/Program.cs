using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class Program
{
    private const string DefinitionFormat = "reactorsim.p7-t06-synthetic-definition/v1";
    private const string ArtifactFormat = "reactorsim.p7-t06-synthetic-authority/v1";
    private const string ManifestFormat = "reactorsim.p7-t06-synthetic-authority-manifest/v1";
    private const string TaskId = "P7-T06";
    private const string ArtifactId = "p7-t06-synthetic-phase7a-v1";
    private const string GeneratorId = "p7-t06-independent-synthetic-authority";
    private const string GeneratorVersion = "v1";

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
                "P7_T06_SYNTHETIC_FAILURE code=" + failure.Code +
                " path=" + failure.Path +
                " message=" + failure.Message);
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "P7_T06_SYNTHETIC_FAILURE code=Unhandled.Exception path=tool message=" +
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
        ArtifactBuild build = BuildArtifact(definition, Hex(Sha256(definitionBytes)), approved);
        byte[] artifactBytes = Serialize(build.Artifact);
        byte[] manifestBytes = Serialize(BuildManifest(
            definition,
            artifactBytes,
            Hex(Sha256(definitionBytes)),
            build.Evaluation));

        WriteFile(artifactPath, artifactBytes);
        WriteFile(manifestPath, manifestBytes);

        Console.WriteLine(
            "P7_T06_SYNTHETIC_GENERATE_PASS disposition=" +
            (approved ? "approved_golden" : "candidate") +
            " artifact_sha256=" + Hex(Sha256(artifactBytes)) +
            " manifest_sha256=" + Hex(Sha256(manifestBytes)) +
            " kinetic_steps=" + definition.Time.StepCount.ToString(CultureInfo.InvariantCulture) +
            " nodes=" + definition.Nodes.Count.ToString(CultureInfo.InvariantCulture) +
            " refinement_ordering_pass=" + build.Refinement.OrderingPass.ToString().ToLowerInvariant());
    }

    private static void Validate(
        string definitionPath,
        string artifactPath,
        string manifestPath,
        bool approved)
    {
        byte[] definitionBytes = ReadFile(definitionPath, "definition");
        Definition definition = ParseDefinition(definitionBytes);
        ArtifactBuild build = BuildArtifact(definition, Hex(Sha256(definitionBytes)), approved);
        byte[] expectedArtifactBytes = Serialize(build.Artifact);
        byte[] actualArtifactBytes = ReadFile(artifactPath, "artifact");
        if (!CryptographicOperations.FixedTimeEquals(expectedArtifactBytes, actualArtifactBytes))
        {
            throw new AuthorityFailure(
                "Artifact.NonDeterministic",
                "artifact",
                "The stored artifact does not match deterministic regeneration.");
        }

        byte[] expectedManifestBytes = Serialize(BuildManifest(
            definition,
            actualArtifactBytes,
            Hex(Sha256(definitionBytes)),
            build.Evaluation));
        byte[] actualManifestBytes = ReadFile(manifestPath, "manifest");
        if (!CryptographicOperations.FixedTimeEquals(expectedManifestBytes, actualManifestBytes))
        {
            throw new AuthorityFailure(
                "Manifest.NonDeterministic",
                "manifest",
                "The stored manifest does not match deterministic regeneration.");
        }

        using JsonDocument artifact = JsonDocument.Parse(actualArtifactBytes);
        string expectedStatus = approved ? "approved_golden" : "candidate";
        if (!string.Equals(
                artifact.RootElement.GetProperty("status").GetString(),
                expectedStatus,
                StringComparison.Ordinal))
        {
            throw new AuthorityFailure(
                "Artifact.Status.Invalid",
                "artifact.status",
                "The stored artifact status does not match the requested disposition.");
        }

        Console.WriteLine(
            "P7_T06_SYNTHETIC_VALIDATE_PASS disposition=" + expectedStatus +
            " artifact_sha256=" + Hex(Sha256(actualArtifactBytes)) +
            " manifest_sha256=" + Hex(Sha256(actualManifestBytes)) +
            " independent_repeat_equal=" + build.Evaluation.IndependentRepeatEqual.ToString().ToLowerInvariant() +
            " max_balance_abs=" + build.Evaluation.MaximumBalanceAbsolute.ToString("R", CultureInfo.InvariantCulture) +
            " refinement_ordering_pass=" + build.Refinement.OrderingPass.ToString().ToLowerInvariant());
    }

    private static Definition ParseDefinition(byte[] bytes)
    {
        Definition? definition = JsonSerializer.Deserialize<Definition>(bytes, JsonOptions);
        if (definition == null || definition.Format != DefinitionFormat)
        {
            throw new AuthorityFailure(
                "Definition.Format.Invalid",
                "definition.format",
                "The P7-T06 synthetic definition format is not recognized.");
        }

        ValidateDefinition(definition);
        return definition;
    }

    private static void ValidateDefinition(Definition definition)
    {
        if (definition.TaskId != TaskId ||
            string.IsNullOrWhiteSpace(definition.CaseId) ||
            definition.SourceAuthority == null ||
            definition.StateBinding == null ||
            definition.Time == null ||
            definition.Kinetics == null ||
            definition.Nuclide == null ||
            definition.Nodes == null)
        {
            throw new AuthorityFailure(
                "Definition.Shape.Invalid",
                "definition",
                "The definition must bind task, source, state, time, kinetics, nuclide, and node data.");
        }

        if (definition.SourceAuthority.CoverageClass != "Synthetic" ||
            definition.SourceAuthority.ExternalCaseStatus != "not_admitted_deferred")
        {
            throw new AuthorityFailure(
                "Definition.SourceBoundary.Invalid",
                "definition.source_authority",
                "The fallback must remain explicitly synthetic and external-reference deferred.");
        }

        if (definition.Time.InitialTimeSeconds != 0.0 ||
            definition.Time.StepSeconds <= 0.0 ||
            definition.Time.StepCount != 20 ||
            !double.IsFinite(definition.Time.StepSeconds))
        {
            throw new AuthorityFailure(
                "Definition.Time.Invalid",
                "definition.time",
                "The bounded case requires a finite 0.1-second, 20-step schedule from time zero.");
        }

        if (definition.Kinetics.Groups.Count != 2 ||
            definition.Kinetics.InitialPrecursor.Length != 2 ||
            definition.Nodes.Count != 2)
        {
            throw new AuthorityFailure(
                "Definition.Dimension.Invalid",
                "definition",
                "The bounded case requires two precursor groups and two canonical nodes.");
        }

        EnsureFiniteNonnegative(definition.Kinetics.InitialAmplitude, "kinetics.initial_amplitude");
        EnsureFiniteNonnegative(definition.Kinetics.ReferencePowerWatts, "kinetics.reference_power_watts");
        EnsureFinite(definition.Kinetics.PromptGenerationTimeSeconds, "kinetics.prompt_generation_time_seconds");
        EnsureFinite(definition.Kinetics.SpatialReactivity, "kinetics.spatial_reactivity");
        EnsureFiniteNonnegativeArray(definition.Kinetics.InitialPrecursor, "kinetics.initial_precursor");
        foreach (DelayedGroupDefinition group in definition.Kinetics.Groups)
        {
            EnsureFiniteNonnegative(group.BetaFraction, "kinetics.groups.beta_fraction");
            EnsureFinite(group.DecayConstantPerSecond, "kinetics.groups.decay_constant_per_second");
            if (group.DecayConstantPerSecond <= 0.0)
            {
                throw new AuthorityFailure(
                    "Definition.Kinetics.Decay.Invalid",
                    "kinetics.groups",
                    "Synthetic delayed-neutron decay constants must be positive.");
            }
        }

        EnsureFiniteNonnegative(definition.Nuclide.GammaI, "nuclide.gamma_i");
        EnsureFiniteNonnegative(definition.Nuclide.GammaXe, "nuclide.gamma_xe");
        EnsureFinite(definition.Nuclide.LambdaIPerSecond, "nuclide.lambda_i_per_second");
        EnsureFinite(definition.Nuclide.LambdaXePerSecond, "nuclide.lambda_xe_per_second");
        EnsureFiniteNonnegative(definition.Nuclide.SigmaXeGroup1M2, "nuclide.sigma_xe_group1_m2");
        EnsureFiniteNonnegative(definition.Nuclide.SigmaXeGroup2M2, "nuclide.sigma_xe_group2_m2");
        if (definition.Nuclide.LambdaIPerSecond <= 0.0 || definition.Nuclide.LambdaXePerSecond <= 0.0)
        {
            throw new AuthorityFailure(
                "Definition.Nuclide.Decay.Invalid",
                "definition.nuclide",
                "Synthetic I/Xe decay constants must be positive.");
        }

        for (int index = 0; index < definition.Nodes.Count; index++)
        {
            NodeDefinition node = definition.Nodes[index];
            if (node == null || node.ChannelId != 0 || node.Position != index ||
                node.FissionRateDensityM3S.Length != definition.Time.StepCount ||
                node.FluxGroup1M2S.Length != definition.Time.StepCount ||
                node.FluxGroup2M2S.Length != definition.Time.StepCount)
            {
                throw new AuthorityFailure(
                    "Definition.NodeOrder.Invalid",
                    "definition.nodes[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                    "Nodes must be in canonical channel/position order with complete forcing arrays.");
            }

            EnsureFinitePositive(node.VolumeM3, "nodes.volume_m3");
            EnsureFiniteNonnegative(node.InitialI135Atoms, "nodes.initial_i135_atoms");
            EnsureFiniteNonnegative(node.InitialXe135Atoms, "nodes.initial_xe135_atoms");
            EnsureFiniteNonnegativeArray(node.FissionRateDensityM3S, "nodes.fission_rate_density_m3_s");
            EnsureFiniteNonnegativeArray(node.FluxGroup1M2S, "nodes.flux_group1_m2_s");
            EnsureFiniteNonnegativeArray(node.FluxGroup2M2S, "nodes.flux_group2_m2_s");
        }
    }

    private static ArtifactBuild BuildArtifact(
        Definition definition,
        string definitionSha256,
        bool approved)
    {
        Evaluation first = Evaluate(definition, 1);
        Evaluation repeat = Evaluate(definition, 1);
        byte[] firstBytes = Serialize(first);
        byte[] repeatBytes = Serialize(repeat);
        bool repeatEqual = CryptographicOperations.FixedTimeEquals(firstBytes, repeatBytes);
        if (!repeatEqual)
        {
            throw new AuthorityFailure(
                "IndependentRepeat.Mismatch",
                "verification.independent_repeat",
                "Two independent runs of the standalone model were not byte-identical.");
        }

        RefinementResult refinement = EvaluateRefinement(definition, first);
        if (!refinement.OrderingPass)
        {
            throw new AuthorityFailure(
                "Refinement.Ordering.Invalid",
                "verification.refinement",
                "The bounded synthetic case did not show the required coarse-to-fine error ordering.");
        }

        string status = approved ? "approved_golden" : "candidate";
        var artifact = new Dictionary<string, object?>
        {
            ["format"] = ArtifactFormat,
            ["task_id"] = TaskId,
            ["artifact_id"] = ArtifactId,
            ["status"] = status,
            ["evidence_approval"] = approved ? "Approved" : "Deferred",
            ["comparison_status"] = approved ? "Approved" : "Deferred",
            ["tolerance_status"] = "NotApplicable",
            ["golden_status"] = approved ? "ApprovedGolden" : "NoGolden",
            ["coverage_class"] = "Synthetic",
            ["validation_domain"] = "Synthetic",
            ["approval_scope"] =
                "Bounded Phase 7A synthetic/test-only consumer scope; not a direct CANDU physics baseline.",
            ["definition_sha256"] = definitionSha256,
            ["generator"] = new Dictionary<string, object?>
            {
                ["id"] = GeneratorId,
                ["version"] = GeneratorVersion,
                ["independent_of_runtime"] = true,
                ["runtime_project_reference"] = false,
                ["equation_authority"] = "P2-T04/P7 frozen contracts"
            },
            ["source_authority"] = definition.SourceAuthority,
            ["state_binding"] = definition.StateBinding,
            ["time"] = definition.Time,
            ["kinetics"] = new Dictionary<string, object?>
            {
                ["inputs"] = definition.Kinetics,
                ["history"] = first.KineticHistory,
                ["final"] = first.KineticHistory[^1]
            },
            ["nuclide_data"] = definition.Nuclide,
            ["nodes"] = first.Nodes.Select(node => new Dictionary<string, object?>
            {
                ["node_id"] = node.NodeId,
                ["bundle_id"] = node.BundleId,
                ["channel_id"] = node.ChannelId,
                ["position"] = node.Position,
                ["volume_m3"] = node.VolumeM3,
                ["inputs"] = definition.Nodes.Single(input => input.NodeId == node.NodeId),
                ["history"] = node.History,
                ["final"] = node.History[^1]
            }).ToArray(),
            ["verification"] = new Dictionary<string, object?>
            {
                ["independent_run_sha256"] = Hex(Sha256(firstBytes)),
                ["independent_repeat_sha256"] = Hex(Sha256(repeatBytes)),
                ["independent_repeat_equal"] = repeatEqual,
                ["maximum_balance_absolute"] = first.MaximumBalanceAbsolute,
                ["minimum_nonnegative_value"] = first.MinimumNonnegativeValue,
                ["maximum_dynamic_overlay_group1_per_m"] = first.MaximumDynamicOverlayGroup1PerM,
                ["maximum_dynamic_overlay_group2_per_m"] = first.MaximumDynamicOverlayGroup2PerM,
                ["refinement"] = refinement
            },
            ["comparison"] = new Dictionary<string, object?>
            {
                ["observable_order"] = new[]
                {
                    "kinetics.amplitude",
                    "kinetics.precursor",
                    "xenon.I135_inventory",
                    "xenon.Xe135_inventory",
                    "xenon.I135_number_density",
                    "xenon.Xe135_number_density",
                    "xenon.absorption_overlay"
                },
                ["state_binding_required"] = true,
                ["numeric_tolerance_authority"] = "NotApplicable; exact deterministic consumer identity only"
            },
            ["limitations"] = new[]
            {
                "Synthetic forcing and values are project-authored regression inputs.",
                "The artifact does not validate a named CANDU design or external reference case.",
                "The artifact does not authorize Phase 7B temperature/purity feedback.",
                "Exact equality is used to detect runtime drift for this generated contract case; no production numerical tolerance is approved."
            }
        };

        Evaluation evaluated = first with
        {
            ArtifactApproved = approved,
            IndependentRepeatEqual = repeatEqual,
            IndependentRepeatSha256 = Hex(Sha256(repeatBytes))
        };
        return new ArtifactBuild(artifact, evaluated, refinement);
    }

    private static Dictionary<string, object?> BuildManifest(
        Definition definition,
        byte[] artifactBytes,
        string definitionSha256,
        Evaluation evaluation)
    {
        return new Dictionary<string, object?>
        {
            ["format"] = ManifestFormat,
            ["task_id"] = TaskId,
            ["artifact_id"] = ArtifactId,
            ["disposition"] = evaluation.ArtifactApproved ? "approved_golden" : "candidate",
            ["definition_sha256"] = definitionSha256,
            ["artifact_sha256"] = Hex(Sha256(artifactBytes)),
            ["artifact_byte_length"] = artifactBytes.Length,
            ["generator_id"] = GeneratorId,
            ["generator_version"] = GeneratorVersion,
            ["research_report"] = definition.SourceAuthority.ResearchReport,
            ["coverage_class"] = "Synthetic",
            ["independent_repeat_equal"] = evaluation.IndependentRepeatEqual,
            ["independent_repeat_sha256"] = evaluation.IndependentRepeatSha256,
            ["maximum_balance_absolute"] = evaluation.MaximumBalanceAbsolute,
            ["minimum_nonnegative_value"] = evaluation.MinimumNonnegativeValue,
            ["manifest_binding"] = "artifact bytes and definition bytes are regenerated and compared exactly"
        };
    }

    private static Evaluation Evaluate(Definition definition, int stride)
    {
        if (definition.Time.StepCount % stride != 0)
        {
            throw new AuthorityFailure(
                "Evaluation.Stride.Invalid",
                "time.step_count",
                "The evaluation stride must divide the bounded step count exactly.");
        }

        double deltaTime = definition.Time.StepSeconds * stride;
        List<KineticRecord> kineticHistory = RunKinetics(definition, stride, deltaTime);
        List<NodeEvaluation> nodes = definition.Nodes
            .Select(node => RunNode(definition, node, stride, deltaTime))
            .ToList();

        double maximumBalance = kineticHistory
            .SelectMany(record => record.PrecursorBalanceAbsolute.Append(record.AmplitudeBalanceAbsolute))
            .Concat(nodes.SelectMany(node => node.History.SelectMany(record =>
                new[] { record.I135BalanceAbsolute, record.Xe135BalanceAbsolute })))
            .Max();
        double minimum = kineticHistory
            .SelectMany(record => record.PrecursorAfter.Append(record.AmplitudeAfter))
            .Concat(nodes.SelectMany(node => node.History.SelectMany(record =>
                new[] { record.I135After, record.Xe135After, record.I135NumberDensityAfter, record.Xe135NumberDensityAfter })))
            .Min();
        double maximumOverlayGroup1 = nodes.SelectMany(node => node.History)
            .Select(record => record.DynamicAbsorptionGroup1PerM)
            .Max();
        double maximumOverlayGroup2 = nodes.SelectMany(node => node.History)
            .Select(record => record.DynamicAbsorptionGroup2PerM)
            .Max();

        return new Evaluation(
            kineticHistory,
            nodes,
            maximumBalance,
            minimum,
            maximumOverlayGroup1,
            maximumOverlayGroup2,
            false,
            "");
    }

    private static List<KineticRecord> RunKinetics(
        Definition definition,
        int stride,
        double deltaTime)
    {
        KineticsDefinition input = definition.Kinetics;
        double amplitude = input.InitialAmplitude;
        double[] precursor = input.InitialPrecursor.ToArray();
        double totalBeta = input.Groups.Sum(group => group.BetaFraction);
        List<KineticRecord> history = new();
        double time = definition.Time.InitialTimeSeconds;

        for (int step = 0; step < definition.Time.StepCount / stride; step++)
        {
            double delayedSource = 0.0;
            for (int index = 0; index < input.Groups.Count; index++)
            {
                delayedSource += input.Groups[index].DecayConstantPerSecond * precursor[index];
            }

            double promptCoefficient =
                (input.SpatialReactivity - totalBeta) / input.PromptGenerationTimeSeconds;
            double promptDerivative = promptCoefficient * amplitude;
            double amplitudeDerivative = promptDerivative + delayedSource;
            double nextAmplitude = amplitude + deltaTime * amplitudeDerivative;
            double[] precursorDerivative = new double[input.Groups.Count];
            double[] nextPrecursor = new double[input.Groups.Count];
            for (int index = 0; index < input.Groups.Count; index++)
            {
                DelayedGroupDefinition group = input.Groups[index];
                double production = (group.BetaFraction / input.PromptGenerationTimeSeconds) * amplitude;
                double decay = group.DecayConstantPerSecond * precursor[index];
                precursorDerivative[index] = production - decay;
                nextPrecursor[index] = precursor[index] + deltaTime * precursorDerivative[index];
            }

            history.Add(new KineticRecord
            {
                StepIndex = step,
                TimeBeforeSeconds = time,
                TimeAfterSeconds = time + deltaTime,
                DeltaTimeSeconds = deltaTime,
                SpatialReactivity = input.SpatialReactivity,
                AmplitudeBefore = amplitude,
                AmplitudeAfter = nextAmplitude,
                PromptDerivative = promptDerivative,
                DelayedSource = delayedSource,
                AmplitudeDerivative = amplitudeDerivative,
                PrecursorBefore = precursor.ToArray(),
                PrecursorAfter = nextPrecursor.ToArray(),
                PrecursorDerivative = precursorDerivative.ToArray(),
                AmplitudeBalanceAbsolute = Math.Abs(
                    nextAmplitude - (amplitude + deltaTime * amplitudeDerivative)),
                PrecursorBalanceAbsolute = precursorDerivative
                    .Select((derivative, index) => Math.Abs(
                        nextPrecursor[index] - (precursor[index] + deltaTime * derivative)))
                    .ToArray(),
                DataId = input.DataId,
                DataVersion = input.DataVersion,
                DataDigest = input.DataDigest,
                SpatialSolveId = definition.StateBinding.SpatialSolveId,
                SpatialStateVersion = definition.StateBinding.SpatialStateVersion,
                FeedbackOverlayDigest = definition.StateBinding.FeedbackOverlayDigest
            });

            amplitude = nextAmplitude;
            precursor = nextPrecursor;
            time += deltaTime;
        }

        if (history.Any(record => !double.IsFinite(record.AmplitudeAfter) ||
                                  record.AmplitudeAfter < 0.0 ||
                                  record.PrecursorAfter.Any(value => !double.IsFinite(value) || value < 0.0)))
        {
            throw new AuthorityFailure(
                "Kinetics.Nonnegative.Invalid",
                "kinetics.history",
                "The independent kinetic history left the finite nonnegative domain.");
        }

        return history;
    }

    private static NodeEvaluation RunNode(
        Definition definition,
        NodeDefinition input,
        int stride,
        double deltaTime)
    {
        double iInventory = input.InitialI135Atoms;
        double xeInventory = input.InitialXe135Atoms;
        List<NuclideRecord> history = new();
        double time = definition.Time.InitialTimeSeconds;
        for (int step = 0; step < definition.Time.StepCount / stride; step++)
        {
            int sourceIndex = step * stride;
            double fissionRateDensity = input.FissionRateDensityM3S[sourceIndex];
            double fluxGroup1 = input.FluxGroup1M2S[sourceIndex];
            double fluxGroup2 = input.FluxGroup2M2S[sourceIndex];
            double iDirect = input.VolumeM3 * definition.Nuclide.GammaI * fissionRateDensity;
            double iDecay = CanonicalNegativeLoss(definition.Nuclide.LambdaIPerSecond * iInventory);
            double xeDirect = input.VolumeM3 * definition.Nuclide.GammaXe * fissionRateDensity;
            double xeFromI = iDecay == 0.0 ? 0.0 : -iDecay;
            double absorptionRate =
                (definition.Nuclide.SigmaXeGroup1M2 * fluxGroup1) +
                (definition.Nuclide.SigmaXeGroup2M2 * fluxGroup2);
            double xeDecay = CanonicalNegativeLoss(definition.Nuclide.LambdaXePerSecond * xeInventory);
            double xeAbsorption = CanonicalNegativeLoss(absorptionRate * xeInventory);
            double nextI = iInventory + deltaTime * (iDirect + iDecay);
            double nextXe = xeInventory + deltaTime *
                (xeDirect + xeFromI + xeDecay + xeAbsorption);
            double iDensityBefore = iInventory / input.VolumeM3;
            double xeDensityBefore = xeInventory / input.VolumeM3;
            double iDensityAfter = nextI / input.VolumeM3;
            double xeDensityAfter = nextXe / input.VolumeM3;
            double overlayGroup1 = definition.Nuclide.SigmaXeGroup1M2 * xeDensityAfter;
            double overlayGroup2 = definition.Nuclide.SigmaXeGroup2M2 * xeDensityAfter;

            history.Add(new NuclideRecord
            {
                StepIndex = step,
                TimeBeforeSeconds = time,
                TimeAfterSeconds = time + deltaTime,
                DeltaTimeSeconds = deltaTime,
                FissionRateDensityM3S = fissionRateDensity,
                FluxGroup1M2S = fluxGroup1,
                FluxGroup2M2S = fluxGroup2,
                I135Before = iInventory,
                I135After = nextI,
                Xe135Before = xeInventory,
                Xe135After = nextXe,
                I135NumberDensityBefore = iDensityBefore,
                I135NumberDensityAfter = iDensityAfter,
                Xe135NumberDensityBefore = xeDensityBefore,
                Xe135NumberDensityAfter = xeDensityAfter,
                I135DirectProductionAtomsPerSecond = iDirect,
                I135DecayLossAtomsPerSecond = iDecay,
                Xe135DirectProductionAtomsPerSecond = xeDirect,
                Xe135FromI135DecayAtomsPerSecond = xeFromI,
                Xe135DecayLossAtomsPerSecond = xeDecay,
                Xe135AbsorptionLossAtomsPerSecond = xeAbsorption,
                AbsorptionRatePerSecond = absorptionRate,
                DynamicAbsorptionGroup1PerM = overlayGroup1,
                DynamicAbsorptionGroup2PerM = overlayGroup2,
                I135BalanceAbsolute = Math.Abs(nextI - (iInventory + deltaTime * (iDirect + iDecay))),
                Xe135BalanceAbsolute = Math.Abs(nextXe -
                    (xeInventory + deltaTime * (xeDirect + xeFromI + xeDecay + xeAbsorption))),
                DataId = definition.Nuclide.DataId,
                DataDigest = definition.Nuclide.DataDigest,
                StateBindingDigest = definition.StateBinding.DataPackDigest
            });

            iInventory = nextI;
            xeInventory = nextXe;
            time += deltaTime;
        }

        if (history.Any(record =>
                !double.IsFinite(record.I135After) || record.I135After < 0.0 ||
                !double.IsFinite(record.Xe135After) || record.Xe135After < 0.0 ||
                !double.IsFinite(record.DynamicAbsorptionGroup1PerM) ||
                !double.IsFinite(record.DynamicAbsorptionGroup2PerM)))
        {
            throw new AuthorityFailure(
                "Nuclide.Nonnegative.Invalid",
                "nodes[" + input.NodeId + "].history",
                "The independent I/Xe history left the finite nonnegative domain.");
        }

        return new NodeEvaluation
        {
            NodeId = input.NodeId,
            BundleId = input.BundleId,
            ChannelId = input.ChannelId,
            Position = input.Position,
            VolumeM3 = input.VolumeM3,
            History = history,
            FinalI135 = history[^1].I135After,
            FinalXe135 = history[^1].Xe135After,
            MaximumBalanceAbsolute = history
                .SelectMany(record => new[] { record.I135BalanceAbsolute, record.Xe135BalanceAbsolute })
                .Max(),
            MinimumNonnegativeValue = history
                .SelectMany(record => new[]
                {
                    record.I135After,
                    record.Xe135After,
                    record.I135NumberDensityAfter,
                    record.Xe135NumberDensityAfter,
                    record.DynamicAbsorptionGroup1PerM,
                    record.DynamicAbsorptionGroup2PerM
                })
                .Min()
        };
    }

    private static RefinementResult EvaluateRefinement(Definition definition, Evaluation fine)
    {
        Evaluation coarse = Evaluate(definition, 4);
        Evaluation medium = Evaluate(definition, 2);
        double coarseError = FinalError(coarse, fine);
        double mediumError = FinalError(medium, fine);
        return new RefinementResult
        {
            CoarseStride = 4,
            MediumStride = 2,
            FineStride = 1,
            CoarseError = coarseError,
            MediumError = mediumError,
            FineError = 0.0,
            OrderingPass = mediumError < coarseError && coarseError > 0.0
        };
    }

    private static double FinalError(Evaluation left, Evaluation right)
    {
        double error = Math.Abs(left.KineticHistory[^1].AmplitudeAfter -
                                right.KineticHistory[^1].AmplitudeAfter);
        for (int index = 0; index < left.Nodes.Count; index++)
        {
            error += Math.Abs(left.Nodes[index].FinalI135 - right.Nodes[index].FinalI135);
            error += Math.Abs(left.Nodes[index].FinalXe135 - right.Nodes[index].FinalXe135);
        }

        return error;
    }

    private static double CanonicalNegativeLoss(double magnitude)
    {
        return magnitude == 0.0 ? 0.0 : -magnitude;
    }

    private static void EnsureFinite(double value, string path)
    {
        if (!double.IsFinite(value))
        {
            throw new AuthorityFailure("Definition.Value.NonFinite", path, "A definition value must be finite.");
        }
    }

    private static void EnsureFinitePositive(double value, string path)
    {
        EnsureFinite(value, path);
        if (value <= 0.0)
        {
            throw new AuthorityFailure("Definition.Value.NotPositive", path, "A definition value must be positive.");
        }
    }

    private static void EnsureFiniteNonnegative(double value, string path)
    {
        EnsureFinite(value, path);
        if (value < 0.0)
        {
            throw new AuthorityFailure("Definition.Value.Negative", path, "A definition value must be nonnegative.");
        }
    }

    private static void EnsureFiniteNonnegativeArray(IEnumerable<double> values, string path)
    {
        foreach (double value in values)
        {
            EnsureFiniteNonnegative(value, path);
        }
    }

    private static byte[] ReadFile(string path, string description)
    {
        if (!File.Exists(path))
        {
            throw new AuthorityFailure("Input.Missing", path, "The " + description + " file was not found.");
        }

        return File.ReadAllBytes(path);
    }

    private static void WriteFile(string path, byte[] bytes)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, bytes);
    }

    private static byte[] Serialize<T>(T value)
    {
        return JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
    }

    private static byte[] Sha256(byte[] bytes)
    {
        return SHA256.HashData(bytes);
    }

    private static string Hex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private sealed class AuthorityFailure : Exception
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

    private sealed class ArtifactBuild
    {
        public ArtifactBuild(
            Dictionary<string, object?> artifact,
            Evaluation evaluation,
            RefinementResult refinement)
        {
            Artifact = artifact;
            Evaluation = evaluation;
            Refinement = refinement;
        }

        public Dictionary<string, object?> Artifact { get; }

        public Evaluation Evaluation { get; }

        public RefinementResult Refinement { get; }
    }

    private sealed record Definition
    {
        public string Format { get; init; } = "";
        public string TaskId { get; init; } = "";
        public string CaseId { get; init; } = "";
        public SourceAuthorityDefinition SourceAuthority { get; init; } = new();
        public StateBindingDefinition StateBinding { get; init; } = new();
        public TimeDefinition Time { get; init; } = new();
        public KineticsDefinition Kinetics { get; init; } = new();
        public NuclideDefinition Nuclide { get; init; } = new();
        public List<NodeDefinition> Nodes { get; init; } = new();
    }

    private sealed record SourceAuthorityDefinition
    {
        public string Kind { get; init; } = "";
        public string ExternalCaseStatus { get; init; } = "";
        public string CoverageClass { get; init; } = "";
        public string ResearchReport { get; init; } = "";
        public List<string> P1T08Rows { get; init; } = new();
        public string License { get; init; } = "";
        public string Restriction { get; init; } = "";
    }

    private sealed record StateBindingDefinition
    {
        public string TopologyId { get; init; } = "";
        public ulong CoreStateVersion { get; init; }
        public string SpatialSolveId { get; init; } = "";
        public ulong SpatialStateVersion { get; init; }
        public string SpatialSnapshotDigest { get; init; } = "";
        public string FeedbackOverlayDigest { get; init; } = "";
        public string DataPackId { get; init; } = "";
        public string DataPackDigest { get; init; } = "";
    }

    private sealed record TimeDefinition
    {
        public string Unit { get; init; } = "";
        public double InitialTimeSeconds { get; init; }
        public double StepSeconds { get; init; }
        public int StepCount { get; init; }
    }

    private sealed record KineticsDefinition
    {
        public string DataId { get; init; } = "";
        public string DataVersion { get; init; } = "";
        public string DataDigest { get; init; } = "";
        public double PromptGenerationTimeSeconds { get; init; }
        public double InitialAmplitude { get; init; }
        public double[] InitialPrecursor { get; init; } = Array.Empty<double>();
        public double ReferencePowerWatts { get; init; }
        public double SpatialReactivity { get; init; }
        public List<DelayedGroupDefinition> Groups { get; init; } = new();
    }

    private sealed record DelayedGroupDefinition
    {
        public int GroupIndex { get; init; }
        public double BetaFraction { get; init; }
        public double DecayConstantPerSecond { get; init; }
    }

    private sealed record NuclideDefinition
    {
        public string MaterialVariantId { get; init; } = "";
        public string DataId { get; init; } = "";
        public string DataDigest { get; init; } = "";
        public double GammaI { get; init; }
        public double GammaXe { get; init; }
        public double LambdaIPerSecond { get; init; }
        public double LambdaXePerSecond { get; init; }
        public double SigmaXeGroup1M2 { get; init; }
        public double SigmaXeGroup2M2 { get; init; }
    }

    private sealed record NodeDefinition
    {
        public string NodeId { get; init; } = "";
        public string BundleId { get; init; } = "";
        public int ChannelId { get; init; }
        public int Position { get; init; }
        public double VolumeM3 { get; init; }
        public double InitialI135Atoms { get; init; }
        public double InitialXe135Atoms { get; init; }
        public double[] FissionRateDensityM3S { get; init; } = Array.Empty<double>();
        public double[] FluxGroup1M2S { get; init; } = Array.Empty<double>();
        public double[] FluxGroup2M2S { get; init; } = Array.Empty<double>();
    }

    private sealed record Evaluation
    {
        public Evaluation(
            List<KineticRecord> kineticHistory,
            List<NodeEvaluation> nodes,
            double maximumBalanceAbsolute,
            double minimumNonnegativeValue,
            double maximumDynamicOverlayGroup1PerM,
            double maximumDynamicOverlayGroup2PerM,
            bool artifactApproved,
            string independentRepeatSha256)
        {
            KineticHistory = kineticHistory;
            Nodes = nodes;
            MaximumBalanceAbsolute = maximumBalanceAbsolute;
            MinimumNonnegativeValue = minimumNonnegativeValue;
            MaximumDynamicOverlayGroup1PerM = maximumDynamicOverlayGroup1PerM;
            MaximumDynamicOverlayGroup2PerM = maximumDynamicOverlayGroup2PerM;
            ArtifactApproved = artifactApproved;
            IndependentRepeatSha256 = independentRepeatSha256;
        }

        public List<KineticRecord> KineticHistory { get; }
        public List<NodeEvaluation> Nodes { get; }
        public double MaximumBalanceAbsolute { get; }
        public double MinimumNonnegativeValue { get; }
        public double MaximumDynamicOverlayGroup1PerM { get; }
        public double MaximumDynamicOverlayGroup2PerM { get; }
        public bool ArtifactApproved { get; init; }
        public bool IndependentRepeatEqual { get; init; }
        public string IndependentRepeatSha256 { get; init; }
    }

    private sealed class KineticRecord
    {
        public int StepIndex { get; init; }
        public double TimeBeforeSeconds { get; init; }
        public double TimeAfterSeconds { get; init; }
        public double DeltaTimeSeconds { get; init; }
        public double SpatialReactivity { get; init; }
        public double AmplitudeBefore { get; init; }
        public double AmplitudeAfter { get; init; }
        public double PromptDerivative { get; init; }
        public double DelayedSource { get; init; }
        public double AmplitudeDerivative { get; init; }
        public double[] PrecursorBefore { get; init; } = Array.Empty<double>();
        public double[] PrecursorAfter { get; init; } = Array.Empty<double>();
        public double[] PrecursorDerivative { get; init; } = Array.Empty<double>();
        public double AmplitudeBalanceAbsolute { get; init; }
        public double[] PrecursorBalanceAbsolute { get; init; } = Array.Empty<double>();
        public string DataId { get; init; } = "";
        public string DataVersion { get; init; } = "";
        public string DataDigest { get; init; } = "";
        public string SpatialSolveId { get; init; } = "";
        public ulong SpatialStateVersion { get; init; }
        public string FeedbackOverlayDigest { get; init; } = "";
    }

    private sealed class NodeEvaluation
    {
        public string NodeId { get; init; } = "";
        public string BundleId { get; init; } = "";
        public int ChannelId { get; init; }
        public int Position { get; init; }
        public double VolumeM3 { get; init; }
        public List<NuclideRecord> History { get; init; } = new();
        public double FinalI135 { get; init; }
        public double FinalXe135 { get; init; }
        public double MaximumBalanceAbsolute { get; init; }
        public double MinimumNonnegativeValue { get; init; }
    }

    private sealed class NuclideRecord
    {
        public int StepIndex { get; init; }
        public double TimeBeforeSeconds { get; init; }
        public double TimeAfterSeconds { get; init; }
        public double DeltaTimeSeconds { get; init; }
        public double FissionRateDensityM3S { get; init; }
        public double FluxGroup1M2S { get; init; }
        public double FluxGroup2M2S { get; init; }
        public double I135Before { get; init; }
        public double I135After { get; init; }
        public double Xe135Before { get; init; }
        public double Xe135After { get; init; }
        public double I135NumberDensityBefore { get; init; }
        public double I135NumberDensityAfter { get; init; }
        public double Xe135NumberDensityBefore { get; init; }
        public double Xe135NumberDensityAfter { get; init; }
        public double I135DirectProductionAtomsPerSecond { get; init; }
        public double I135DecayLossAtomsPerSecond { get; init; }
        public double Xe135DirectProductionAtomsPerSecond { get; init; }
        public double Xe135FromI135DecayAtomsPerSecond { get; init; }
        public double Xe135DecayLossAtomsPerSecond { get; init; }
        public double Xe135AbsorptionLossAtomsPerSecond { get; init; }
        public double AbsorptionRatePerSecond { get; init; }
        public double DynamicAbsorptionGroup1PerM { get; init; }
        public double DynamicAbsorptionGroup2PerM { get; init; }
        public double I135BalanceAbsolute { get; init; }
        public double Xe135BalanceAbsolute { get; init; }
        public string DataId { get; init; } = "";
        public string DataDigest { get; init; } = "";
        public string StateBindingDigest { get; init; } = "";
    }

    private sealed class RefinementResult
    {
        public int CoarseStride { get; init; }
        public int MediumStride { get; init; }
        public int FineStride { get; init; }
        public double CoarseError { get; init; }
        public double MediumError { get; init; }
        public double FineError { get; init; }
        public bool OrderingPass { get; init; }
    }
}
