using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class Program
{
    private const string SourceFormat = "reactorsim.reduced-interpolation-source/v1";
    private const string PackFormat = "reactorsim.reduced-interpolation-pack/v1";
    private const string ReducedManifestFormat = "reactorsim.reduced-interpolation-manifest/v1";
    private const string BenchmarkFormat = "reactorsim.synthetic-benchmark-scenario/v1";
    private const string OutputFormat = "reactorsim.independent-spatial-reproduction/v1";
    private const string OutputManifestFormat = "reactorsim.independent-spatial-reproduction-manifest/v1";
    private const string TaskId = "P4-T06-G4D";
    private const string SourceArtifactId = "p4-t06-r4-synthetic-input-v1";
    private const string PackArtifactId = "p4-t06-r4-reduced-candidate-v1";
    private const string TopologyFixtureId = "p4-t05-homogeneous-three-node-static-solve-v1";
    private const string BenchmarkScenarioId = "p4-t05-homogeneous-three-node-static-solve-v1";
    private const string TableId = "00000000-0000-0000-0000-0000000005a1";
    private const string MaterialVariantId = "MAT-SYN";
    private const string SyntheticEvidence = "synthetic";
    private const string CandidateApproval = "Candidate";
    private const string CandidateStatus = "candidate";
    private const string UnitsProfileId = "SI-v1";
    private const string TransformId = "identity_projection_v1";
    private const string DataVersion = "synthetic-v1";
    private const string SourceProvenance = "synthetic:P5-T05";
    private const string GeneratorId = "p4-t06-g4d-independent-spatial-reproduction";
    private const string GeneratorVersion = "v1";
    private const string EvidencePath = "data/comparisons/p4-t06-g4d-independent-reproduction-v1.json";
    private const string ManifestArtifactId = "p4-t06-g4d-independent-reproduction-manifest-v1";
    private const int ExpectedNodeCount = 3;

    private const string ExpectedSourceSha256 =
        "1680a075cd34867b603e1e786002815b19be360d4e8557cd5485978dc32c1694";
    private const string ExpectedPackSha256 =
        "0de429b27f78b5e2c5fe724916b14ac8ce45ba05560ab73f3fa4805befd11a0e";
    private const string ExpectedReducedManifestSha256 =
        "6e3c19d4f596dfd930b339f983a54b46a6a27b9e83b6652f8e40a058c19e2a8b";
    private const string ExpectedBenchmarkSha256 =
        "8a53f7519a3b6597d3382c9e91df356474d7d1e72e8ce5d0055e30ef635a1045";
    private const string ExpectedTableChecksum =
        "4b1e27cb954d6bcbd6f25d12128a9bb1225ce8ae751ad10c3097ff470b881d40";

    private static readonly double[] FreshBurnups = { 0.0, 0.0, 0.0 };
    private static readonly double[] EquilibriumLikeBurnups = { 50000.0, 50000.0, 50000.0 };
    private static readonly double[] MidcycleBurnups = { 25000.0, 25000.0, 25000.0 };
    private static readonly double[] ResidualUnitFluxGroup1 = { 1.0, 1.0, 1.0 };
    private static readonly double[] ResidualUnitFluxGroup2 = { 1.0, 1.0, 1.0 };

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 7)
            {
                PrintUsage();
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "generate":
                    Generate(args[1], args[2], args[3], args[4], args[5], args[6]);
                    return 0;
                case "validate":
                    Validate(args[1], args[2], args[3], args[4], args[5], args[6]);
                    return 0;
                default:
                    PrintUsage();
                    return 1;
            }
        }
        catch (ReproductionFailure failure)
        {
            Console.Error.WriteLine(
                "P4_T06_G4D_FAILURE code=" + failure.Code +
                " path=" + failure.Path +
                " message=" + failure.Message);
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "P4_T06_G4D_FAILURE code=Unhandled.Exception path=tool message=" + exception.Message);
            return 3;
        }
    }

    private static void Generate(
        string sourcePath,
        string packPath,
        string reducedManifestPath,
        string benchmarkPath,
        string outputPath,
        string outputManifestPath)
    {
        InputBundle input = ReadInputs(
            sourcePath,
            packPath,
            reducedManifestPath,
            benchmarkPath,
            outputPath,
            outputManifestPath);
        ReproductionDocument document = BuildDocument(input);
        byte[] artifactBytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        ReproductionManifest manifest = BuildManifest(input, artifactBytes, document);
        byte[] manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);

        WriteFile(outputPath, artifactBytes);
        WriteFile(outputManifestPath, manifestBytes);

        Console.WriteLine(
            "P4_T06_G4D_GENERATE_PASS cases=" +
            document.Cases.Count.ToString(CultureInfo.InvariantCulture) +
            " records=" + document.Records.Count.ToString(CultureInfo.InvariantCulture) +
            " artifact_sha256=" + Hex(Sha256(artifactBytes)) +
            " manifest_sha256=" + Hex(Sha256(manifestBytes)));
    }

    private static void Validate(
        string sourcePath,
        string packPath,
        string reducedManifestPath,
        string benchmarkPath,
        string outputPath,
        string outputManifestPath)
    {
        InputBundle input = ReadInputs(
            sourcePath,
            packPath,
            reducedManifestPath,
            benchmarkPath,
            outputPath,
            outputManifestPath);
        ReproductionDocument expectedDocument = BuildDocument(input);
        byte[] expectedArtifactBytes = JsonSerializer.SerializeToUtf8Bytes(expectedDocument, JsonOptions);
        byte[] actualArtifactBytes = ReadFile(outputPath, "reproduction artifact");
        if (!CryptographicOperations.FixedTimeEquals(expectedArtifactBytes, actualArtifactBytes))
        {
            throw new ReproductionFailure(
                "Output.NonDeterministic",
                "artifact",
                "The committed reproduction artifact does not match deterministic regeneration.");
        }

        ReproductionManifest expectedManifest = BuildManifest(input, actualArtifactBytes, expectedDocument);
        byte[] expectedManifestBytes = JsonSerializer.SerializeToUtf8Bytes(expectedManifest, JsonOptions);
        byte[] actualManifestBytes = ReadFile(outputManifestPath, "reproduction manifest");
        if (!CryptographicOperations.FixedTimeEquals(expectedManifestBytes, actualManifestBytes))
        {
            throw new ReproductionFailure(
                "Manifest.NonDeterministic",
                "manifest",
                "The committed reproduction manifest does not match deterministic regeneration.");
        }

        ValidateOutputDocument(actualArtifactBytes, expectedDocument);
        Console.WriteLine(
            "P4_T06_G4D_VALIDATE_PASS cases=" +
            expectedDocument.Cases.Count.ToString(CultureInfo.InvariantCulture) +
            " records=" + expectedDocument.Records.Count.ToString(CultureInfo.InvariantCulture) +
            " artifact_sha256=" + Hex(Sha256(actualArtifactBytes)) +
            " manifest_sha256=" + Hex(Sha256(actualManifestBytes)));
    }

    private static InputBundle ReadInputs(
        string sourcePath,
        string packPath,
        string reducedManifestPath,
        string benchmarkPath,
        string outputPath,
        string outputManifestPath)
    {
        EnsureOutputDoesNotOverwriteInput(
            sourcePath,
            packPath,
            reducedManifestPath,
            benchmarkPath,
            outputPath,
            outputManifestPath);

        byte[] sourceBytes = ReadFile(sourcePath, "source");
        byte[] packBytes = ReadFile(packPath, "pack");
        byte[] reducedManifestBytes = ReadFile(reducedManifestPath, "reduced manifest");
        byte[] benchmarkBytes = ReadFile(benchmarkPath, "benchmark");
        string sourceSha256 = RequireExpectedHash(sourceBytes, ExpectedSourceSha256, "source");
        string packSha256 = RequireExpectedHash(packBytes, ExpectedPackSha256, "pack");
        string reducedManifestSha256 = RequireExpectedHash(
            reducedManifestBytes,
            ExpectedReducedManifestSha256,
            "reduced manifest");
        string benchmarkSha256 = RequireExpectedHash(benchmarkBytes, ExpectedBenchmarkSha256, "benchmark");

        SourceModel source = ParseSource(sourceBytes);
        PackModel pack = ParsePack(packBytes);
        ReducedManifestModel reducedManifest = ParseReducedManifest(reducedManifestBytes);
        BenchmarkModel benchmark = ParseBenchmark(benchmarkBytes);
        ValidateInputBundle(
            source,
            pack,
            reducedManifest,
            benchmark,
            sourceSha256,
            packSha256,
            reducedManifestSha256,
            benchmarkSha256);
        SpatialFixture fixture = CreateFixture(benchmark);

        return new InputBundle(
            source,
            pack,
            reducedManifest,
            benchmark,
            fixture,
            sourceSha256,
            packSha256,
            reducedManifestSha256,
            benchmarkSha256);
    }

    private static ReproductionDocument BuildDocument(InputBundle input)
    {
        var definitions = new List<CaseDefinition>
        {
            new CaseDefinition(
                "fresh_candidate",
                "fresh",
                FreshBurnups,
                "Fresh-knot material state; no fresh-fuel inference."),
            new CaseDefinition(
                "equilibrium_like_candidate",
                "equilibrium_like",
                EquilibriumLikeBurnups,
                "Representative 50000 J/kg HM knot; not an inferred equilibrium state."),
            new CaseDefinition(
                "midcycle_interpolation_candidate",
                "interpolation_midpoint",
                MidcycleBurnups,
                "In-domain 0-to-50000 J/kg HM interpolation midpoint."),
        };

        var cases = new List<CaseOutput>(definitions.Count);
        var records = new List<EvidenceRecord>(57);
        foreach (CaseDefinition definition in definitions)
        {
            CaseEvaluation evaluation = EvaluateCase(definition, input);
            cases.Add(evaluation.Output);
            records.AddRange(BuildRecords(evaluation, input));
        }

        records.Sort(CompareRecords);

        if (cases.Count != 3 || records.Count != 57)
        {
            throw new ReproductionFailure(
                "Output.Shape.Invalid",
                "document",
                "The bounded G4D artifact must contain exactly three cases and 57 records.");
        }

        return new ReproductionDocument(
            OutputFormat,
            TaskId,
            "candidate",
            SyntheticEvidence,
            "CommittedSynthetic",
            CandidateApproval,
            "Runtime",
            GeneratorId,
            GeneratorVersion,
            "standalone_scalar_p2_t02_reproduction",
            "independent_candidate_reproduction",
            "Deferred",
            "P2-T05 spatial profiles remain Deferred until G4 approval; exact self-repeat is diagnostic only.",
            SourceArtifactId,
            PackArtifactId,
            TransformId,
            DataVersion,
            UnitsProfileId,
            SourceProvenance,
            input.SourceSha256,
            input.PackSha256,
            input.ReducedManifestSha256,
            input.BenchmarkSha256,
            TopologyFixtureId,
            BenchmarkScenarioId,
            "SI units; explicit node/channel/position order; P2-T02 equations and caller-owned target power.",
            "Offline artifact only; no runtime schema, dependency, or data/golden payload is added.",
            cases,
            records);
    }

    private static CaseEvaluation EvaluateCase(CaseDefinition definition, InputBundle input)
    {
        SourceTableModel sourceTable = input.Source.Tables[0];
        var lookups = new List<LookupModel>(ExpectedNodeCount);
        var materials = new List<Material>(ExpectedNodeCount);
        for (int nodeIndex = 0; nodeIndex < definition.Burnups.Length; nodeIndex++)
        {
            LookupModel lookup = Lookup(
                sourceTable,
                input.Source.UnitsProfileId,
                input.Pack.Tables[0].Checksum,
                definition.Burnups[nodeIndex],
                nodeIndex);
            lookups.Add(lookup);
            materials.Add(new Material(input.Benchmark.NodeVolumeM3, lookup.Coefficients));
        }

        string inputDigest = ComputeInputDigest(definition, input);
        string coefficientIdentity = ComputeCoefficientIdentity(input.Pack.Tables[0], lookups);
        SolveResult first = Solve(materials, input.Benchmark, input.Fixture, definition.CaseId);
        SolveResult repeat = Solve(materials, input.Benchmark, input.Fixture, definition.CaseId);
        byte[] firstBytes = JsonSerializer.SerializeToUtf8Bytes(first.Snapshot, JsonOptions);
        byte[] repeatBytes = JsonSerializer.SerializeToUtf8Bytes(repeat.Snapshot, JsonOptions);
        bool repeatEqual = CryptographicOperations.FixedTimeEquals(firstBytes, repeatBytes);
        if (!repeatEqual)
        {
            throw new ReproductionFailure(
                "Determinism.RepeatMismatch",
                "cases[" + definition.CaseId + "]",
                "Two standalone reproductions of the same case were not byte-identical.");
        }

        string snapshotDigest = Hex(Sha256(firstBytes));
        var lookupOutputs = new List<LookupOutput>(lookups.Count);
        foreach (LookupModel lookup in lookups)
        {
            lookupOutputs.Add(
                new LookupOutput(
                    lookup.NodeIndex,
                    lookup.InputBurnupJPerKgHm,
                    lookup.BracketLowerIndex,
                    lookup.BracketUpperIndex,
                    lookup.InterpolationFraction,
                    lookup.TableId,
                    lookup.MaterialVariantId,
                    lookup.UnitsProfileId,
                    lookup.TableChecksum,
                    new CoefficientOutput(lookup.Coefficients)));
        }

        var output = new CaseOutput(
            definition.CaseId,
            definition.ScenarioClass,
            "CandidateSnapshot",
            definition.Burnups,
            definition.Description,
            inputDigest,
            coefficientIdentity,
            snapshotDigest,
            repeatEqual,
            lookupOutputs,
            first.Snapshot);

        return new CaseEvaluation(
            definition,
            output,
            lookups,
            inputDigest,
            coefficientIdentity,
            snapshotDigest,
            DeriveRunId(definition.CaseId, "independent_standalone", input.PackSha256, snapshotDigest),
            DeriveRunId(definition.CaseId, "independent_repeat", input.PackSha256, snapshotDigest),
            Hex(Sha256(repeatBytes)));
    }

    private static List<EvidenceRecord> BuildRecords(CaseEvaluation evaluation, InputBundle input)
    {
        Snapshot snapshot = evaluation.Output.Snapshot;
        string caseId = evaluation.Definition.CaseId;
        var records = new List<EvidenceRecord>(19);
        StateBinding stateBinding = CreateStateBinding(evaluation, input);

        records.Add(CreateRecord(
            evaluation,
            stateBinding,
            "spatial.k",
            new ScopeBinding("Solve", new SolveScopeKeyBinding(stateBinding.SpatialSolveId, stateBinding.SpatialStateVersion)),
            "scalar",
            "1",
            "ScalarV1",
            "NotApplicable",
            snapshot.Eigenvalue,
            "P2-T05-spatial-k-v1"));
        records.Add(CreateRecord(
            evaluation,
            stateBinding,
            "spatial.flux",
            new ScopeBinding("Vector", new VectorScopeKeyBinding(CreateNodeGroupKeys())),
            "vector",
            "m^-2 s^-1",
            "VectorV1",
            new ComponentOrderSpecBinding(0, "NodeGroupKeyV1", 0, "ChannelPositionGroupV1"),
            CreateFluxComponents(snapshot.FluxByNodeGroup),
            "P2-T05-spatial-flux-v1"));
        records.Add(CreateRecord(
            evaluation,
            stateBinding,
            "spatial.total_power",
            new ScopeBinding("Global", new GlobalScopeKeyBinding(CreateNodeKeys())),
            "scalar",
            "W",
            "ScalarV1",
            "NotApplicable",
            snapshot.TotalPowerW,
            "P2-T05-spatial-total-power-v1"));
        records.Add(CreateRecord(
            evaluation,
            stateBinding,
            "spatial.normalization_scale",
            new ScopeBinding("Solve", new SolveScopeKeyBinding(stateBinding.SpatialSolveId, stateBinding.SpatialStateVersion)),
            "scalar",
            "1",
            "ScalarV1",
            "NotApplicable",
            snapshot.NormalizationScale,
            "P2-T05-spatial-normalization-scale-v1"));
        records.Add(CreateRecord(
            evaluation,
            stateBinding,
            "spatial.convergence",
            new ScopeBinding("Solve", new SolveScopeKeyBinding(stateBinding.SpatialSolveId, stateBinding.SpatialStateVersion)),
            "bytes",
            "bytes",
            "BytesV1",
            "NotApplicable",
            Hex(Sha256(JsonSerializer.SerializeToUtf8Bytes(snapshot.Diagnostics, JsonOptions))),
            "P2-T05-spatial-convergence-v1"));
        records.Add(CreateRecord(
            evaluation,
            stateBinding,
            "spatial.iteration_count",
            new ScopeBinding("Solve", new SolveScopeKeyBinding(stateBinding.SpatialSolveId, stateBinding.SpatialStateVersion)),
            "integer",
            "integer",
            "IntegerV1",
            "NotApplicable",
            snapshot.Diagnostics.IterationCount,
            "P2-T05-spatial-iteration-count-v1"));

        for (int nodeIndex = 0; nodeIndex < evaluation.Lookups.Count; nodeIndex++)
        {
            LookupModel lookup = evaluation.Lookups[nodeIndex];
            for (int groupIndex = 1; groupIndex <= 2; groupIndex++)
            {
                string coefficientIdentity = ComputeLookupIdentity(input.Pack.Tables[0], lookup, groupIndex);
                records.Add(CreateRecord(
                    evaluation,
                    stateBinding,
                    "spatial.coefficient_identity",
                    new ScopeBinding(
                        "Lookup",
                        new LookupScopeKeyBinding(
                            new NodeGroupKeyBinding(0, (ushort)nodeIndex, (ushort)groupIndex),
                            lookup.TableId,
                            lookup.InputBurnupJPerKgHm,
                            new LookupBracketBinding(
                                (uint)lookup.BracketLowerIndex,
                                (uint)lookup.BracketUpperIndex,
                                lookup.InterpolationFraction))),
                    "bytes",
                    "bytes",
                    "BytesV1",
                    "NotApplicable",
                    coefficientIdentity,
                    "P2-T05-spatial-coefficient-id-v1"));
            }
        }

        for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
        {
            ScopeBinding scope = new ScopeBinding(
                "Entity",
                new EntityScopeKeyBinding(new NodeKeyBinding(0, (ushort)nodeIndex)));
            records.Add(CreateRecord(
                evaluation,
                stateBinding,
                "spatial.power",
                scope,
                "scalar",
                "W",
                "ScalarV1",
                "NotApplicable",
                snapshot.NodePowerW[nodeIndex],
                "P2-T05-spatial-power-v1"));
            records.Add(CreateRecord(
                evaluation,
                stateBinding,
                "spatial.fission_source",
                scope,
                "scalar",
                "m^-3 s^-1",
                "ScalarV1",
                "NotApplicable",
                snapshot.FissionSourceRateDensity[nodeIndex],
                "P2-T05-spatial-fission-source-v1"));
        }

        RepeatBinding repeatBinding = CreateRepeatBinding(evaluation);
        records.Add(CreateRecord(
            evaluation,
            stateBinding,
            "determinism.repeat_equal",
            new ScopeBinding(
                "RunPair",
                new RunPairScopeKeyBinding(evaluation.RunId, evaluation.RepeatRunId)),
            "bytes",
            "bytes",
            "BytesV1",
            "NotApplicable",
            repeatBinding.EvidenceBytesHex,
            "P2-T05-determinism-repeat-equal-v1",
            repeatBinding));

        if (records.Count != 19)
        {
            throw new ReproductionFailure(
                "Records.CaseCount.Invalid",
                "cases[" + caseId + "]",
                "Each admitted case must emit exactly 19 observable records.");
        }

        return records;
    }

    private static EvidenceRecord CreateRecord(
        CaseEvaluation evaluation,
        StateBinding stateBinding,
        string quantityId,
        ScopeBinding scope,
        string valueKind,
        string unit,
        string payloadSchemaId,
        object componentOrder,
        object payload,
        string profileId,
        RepeatBinding? repeatBinding = null)
    {
        string observableId = DeriveUuidV8(BuildObservableIdentityBytes(
            "Runtime",
            quantityId,
            scope,
            new SimulationTime("Available", 0.0),
            stateBinding,
            "not_applicable"));
        return new EvidenceRecord(
            observableId,
            quantityId,
            scope,
            new SimulationTime("Available", 0.0),
            stateBinding,
            evaluation.InputDigest,
            new RecordValue("available", valueKind, unit, payloadSchemaId, componentOrder, payload),
            "P2-T05-rule-" + profileId,
            profileId,
            SourceArtifactId,
            "Synthetic",
            "CommittedSynthetic",
            CandidateApproval,
            "Runtime",
            "Deferred",
            EvidencePath,
            repeatBinding,
            new OrderBinding("Runtime", scope, quantityId, "not_applicable", observableId));
    }

    private static StateBinding CreateStateBinding(CaseEvaluation evaluation, InputBundle input)
    {
        return new StateBinding(
            "synthetic_fixture",
            TopologyFixtureId,
            PackArtifactId,
            evaluation.InputDigest,
            evaluation.CoefficientIdentity,
            evaluation.SnapshotDigest,
            input.BenchmarkSha256,
            evaluation.Definition.CaseId);
    }

    private static int CompareRecords(EvidenceRecord left, EvidenceRecord right)
    {
        int comparison = left.OrderKey.ValidationDomainRank.CompareTo(right.OrderKey.ValidationDomainRank);
        if (comparison != 0) return comparison;
        comparison = left.OrderKey.SimulationTimeSeconds.CompareTo(right.OrderKey.SimulationTimeSeconds);
        if (comparison != 0) return comparison;
        comparison = left.OrderKey.CoreStateVersion.CompareTo(right.OrderKey.CoreStateVersion);
        if (comparison != 0) return comparison;
        comparison = left.OrderKey.EventRankSort.CompareTo(right.OrderKey.EventRankSort);
        if (comparison != 0) return comparison;
        comparison = left.OrderKey.SequenceSort.CompareTo(right.OrderKey.SequenceSort);
        if (comparison != 0) return comparison;
        comparison = CompareOptionalUuid(left.OrderKey.EventIdOrNotApplicable, right.OrderKey.EventIdOrNotApplicable);
        if (comparison != 0) return comparison;
        comparison = CompareScopeKeys(left.Scope, right.Scope);
        if (comparison != 0) return comparison;
        comparison = StringComparer.Ordinal.Compare(left.QuantityId, right.QuantityId);
        if (comparison != 0) return comparison;
        comparison = CompareComponentKeys(left.OrderKey.ComponentKey, right.OrderKey.ComponentKey);
        if (comparison != 0) return comparison;
        return CompareUuid(left.ObservableId, right.ObservableId);
    }

    private static int CompareScopeKeys(ScopeBinding left, ScopeBinding right)
    {
        int comparison = ScopeKindRankFor(left.Kind).CompareTo(ScopeKindRankFor(right.Kind));
        if (comparison != 0) return comparison;
        switch (left.Key, right.Key)
        {
            case (GlobalScopeKeyBinding leftGlobal, GlobalScopeKeyBinding rightGlobal):
                comparison = leftGlobal.NodeSet.Count.CompareTo(rightGlobal.NodeSet.Count);
                if (comparison != 0) return comparison;
                for (int index = 0; index < leftGlobal.NodeSet.Count; index++)
                {
                    comparison = CompareNodeKey(leftGlobal.NodeSet[index], rightGlobal.NodeSet[index]);
                    if (comparison != 0) return comparison;
                }

                return 0;
            case (EntityScopeKeyBinding leftEntity, EntityScopeKeyBinding rightEntity):
                comparison = StringComparer.Ordinal.Compare(leftEntity.EntityKind, rightEntity.EntityKind);
                return comparison != 0 ? comparison : CompareNodeKey(leftEntity.Node, rightEntity.Node);
            case (VectorScopeKeyBinding leftVector, VectorScopeKeyBinding rightVector):
                comparison = leftVector.NodeGroups.Count.CompareTo(rightVector.NodeGroups.Count);
                if (comparison != 0) return comparison;
                for (int index = 0; index < leftVector.NodeGroups.Count; index++)
                {
                    comparison = CompareNodeGroupKey(leftVector.NodeGroups[index], rightVector.NodeGroups[index]);
                    if (comparison != 0) return comparison;
                }

                return 0;
            case (SolveScopeKeyBinding leftSolve, SolveScopeKeyBinding rightSolve):
                comparison = CompareUuid(leftSolve.SolveId, rightSolve.SolveId);
                if (comparison != 0) return comparison;
                comparison = CompareOptionalUInt64(leftSolve.SpatialStateVersion, rightSolve.SpatialStateVersion);
                if (comparison != 0) return comparison;
                return CompareOptionalUInt16(leftSolve.GroupIndex, rightSolve.GroupIndex);
            case (LookupScopeKeyBinding leftLookup, LookupScopeKeyBinding rightLookup):
                comparison = StringComparer.Ordinal.Compare(leftLookup.OwnerKind, rightLookup.OwnerKind);
                if (comparison != 0) return comparison;
                comparison = CompareNodeGroupKey(leftLookup.NodeGroup, rightLookup.NodeGroup);
                if (comparison != 0) return comparison;
                comparison = StringComparer.Ordinal.Compare(leftLookup.TableId, rightLookup.TableId);
                if (comparison != 0) return comparison;
                comparison = leftLookup.Bracket.LowerIndex.CompareTo(rightLookup.Bracket.LowerIndex);
                if (comparison != 0) return comparison;
                comparison = leftLookup.Bracket.UpperIndex.CompareTo(rightLookup.Bracket.UpperIndex);
                if (comparison != 0) return comparison;
                comparison = leftLookup.Bracket.Alpha.CompareTo(rightLookup.Bracket.Alpha);
                if (comparison != 0) return comparison;
                return StringComparer.Ordinal.Compare(leftLookup.Bracket.ResultStatus, rightLookup.Bracket.ResultStatus);
            case (RunPairScopeKeyBinding leftRunPair, RunPairScopeKeyBinding rightRunPair):
                comparison = CompareUuid(leftRunPair.RunIdA, rightRunPair.RunIdA);
                return comparison != 0 ? comparison : CompareUuid(leftRunPair.RunIdB, rightRunPair.RunIdB);
            default:
                throw new ReproductionFailure("Scope.Order.Invalid", left.Kind, "Scope key types do not match their closed scope kind.");
        }
    }

    private static int CompareNodeKey(NodeKeyBinding left, NodeKeyBinding right)
    {
        int comparison = left.ChannelId.CompareTo(right.ChannelId);
        return comparison != 0 ? comparison : left.BundlePosition.CompareTo(right.BundlePosition);
    }

    private static int CompareNodeGroupKey(NodeGroupKeyBinding left, NodeGroupKeyBinding right)
    {
        int comparison = left.ChannelId.CompareTo(right.ChannelId);
        if (comparison != 0) return comparison;
        comparison = left.BundlePosition.CompareTo(right.BundlePosition);
        return comparison != 0 ? comparison : left.GroupIndex.CompareTo(right.GroupIndex);
    }

    private static int CompareComponentKeys(string left, string right)
    {
        return StringComparer.Ordinal.Compare(left, right);
    }

    private static int CompareOptionalUInt64(string left, string right)
    {
        bool leftApplicable = !string.Equals(left, "NotApplicable", StringComparison.Ordinal);
        bool rightApplicable = !string.Equals(right, "NotApplicable", StringComparison.Ordinal);
        if (leftApplicable != rightApplicable) return leftApplicable ? 1 : -1;
        if (!leftApplicable) return 0;
        if (!ulong.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out ulong leftValue) ||
            !ulong.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out ulong rightValue))
        {
            throw new ReproductionFailure("Order.Version.Invalid", "order_key", "A canonical UInt64 state version was required.");
        }

        return leftValue.CompareTo(rightValue);
    }

    private static int CompareOptionalUInt16(string left, string right)
    {
        bool leftApplicable = !string.Equals(left, "NotApplicable", StringComparison.Ordinal);
        bool rightApplicable = !string.Equals(right, "NotApplicable", StringComparison.Ordinal);
        if (leftApplicable != rightApplicable) return leftApplicable ? 1 : -1;
        if (!leftApplicable) return 0;
        if (!ushort.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out ushort leftValue) ||
            !ushort.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out ushort rightValue))
        {
            throw new ReproductionFailure("Order.Group.Invalid", "order_key", "A canonical UInt16 group index was required.");
        }

        return leftValue.CompareTo(rightValue);
    }

    private static int CompareOptionalUuid(string left, string right)
    {
        bool leftApplicable = !string.Equals(left, "NotApplicable", StringComparison.Ordinal);
        bool rightApplicable = !string.Equals(right, "NotApplicable", StringComparison.Ordinal);
        if (leftApplicable != rightApplicable) return leftApplicable ? 1 : -1;
        if (!leftApplicable) return 0;
        return CompareUuid(left, right);
    }

    private static int CompareUuid(string left, string right)
    {
        return CompareBytes(HexToBytes(left), HexToBytes(right));
    }

    private static int CompareBytes(byte[] left, byte[] right)
    {
        int length = Math.Min(left.Length, right.Length);
        for (int index = 0; index < length; index++)
        {
            int comparison = left[index].CompareTo(right[index]);
            if (comparison != 0) return comparison;
        }

        return left.Length.CompareTo(right.Length);
    }

    private static List<VectorComponent> CreateFluxComponents(double[] fluxByNodeGroup)
    {
        var components = new List<VectorComponent>(ExpectedNodeCount * 2);
        for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
        {
            components.Add(new VectorComponent(new NodeGroupKeyBinding(0, (ushort)nodeIndex, 1), fluxByNodeGroup[nodeIndex * 2]));
            components.Add(new VectorComponent(new NodeGroupKeyBinding(0, (ushort)nodeIndex, 2), fluxByNodeGroup[nodeIndex * 2 + 1]));
        }

        return components;
    }

    private static ReproductionManifest BuildManifest(
        InputBundle input,
        byte[] artifactBytes,
        ReproductionDocument document)
    {
        return new ReproductionManifest(
            OutputManifestFormat,
            1,
            ManifestArtifactId,
            "p4-t06-g4d-independent-reproduction-v1",
            Hex(Sha256(artifactBytes)),
            CandidateStatus,
            SyntheticEvidence,
            CandidateApproval,
            "Runtime",
            GeneratorId,
            GeneratorVersion,
            "standalone_scalar_p2_t02_reproduction",
            input.SourceSha256,
            input.PackSha256,
            input.ReducedManifestSha256,
            input.BenchmarkSha256,
            SourceArtifactId,
            PackArtifactId,
            TopologyFixtureId,
            BenchmarkScenarioId,
            UnitsProfileId,
            document.Cases.Count,
            document.Records.Count,
            "Deferred",
            "No host-local paths; exact self-repeat is candidate diagnostic evidence only.");
    }

    private static SolveResult Solve(List<Material> materials, BenchmarkModel benchmark, SpatialFixture fixture, string caseId)
    {
        double[] initialGroup1 = benchmark.InitialGroup1.ToArray();
        double[] initialGroup2 = benchmark.InitialGroup2.ToArray();
        double initialPower = ComputePower(materials, initialGroup1, initialGroup2);
        double initialNormalizationScale = benchmark.TargetPowerW / initialPower;
        RequirePositiveFinite(initialNormalizationScale, "cases[" + caseId + "].initial_normalization_scale");

        var currentGroup1 = new double[ExpectedNodeCount];
        var currentGroup2 = new double[ExpectedNodeCount];
        for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
        {
            currentGroup1[nodeIndex] = initialGroup1[nodeIndex] * initialNormalizationScale;
            currentGroup2[nodeIndex] = initialGroup2[nodeIndex] * initialNormalizationScale;
            RequireFlux(currentGroup1[nodeIndex], "initial_group1", nodeIndex);
            RequireFlux(currentGroup2[nodeIndex], "initial_group2", nodeIndex);
        }

        double currentEigenvalue = benchmark.InitialEigenvalue;
        double currentProduction = ComputeProduction(materials, currentGroup1, currentGroup2, null);
        double[] previousSourceShape = ComputeSourceShape(materials, currentGroup1, currentGroup2, currentProduction);
        double[] group1Diagonal = BuildDiagonal(materials, 1, fixture);
        double[] group2Diagonal = BuildDiagonal(materials, 2, fixture);
        SolveStepDiagnostics? lastDiagnostics = null;
        double finalTrialPower = 0.0;
        double finalNormalizationScale = initialNormalizationScale;

        for (int outerIndex = 0; outerIndex < benchmark.MaximumOuterIterations; outerIndex++)
        {
            double[] fissionSource = new double[ExpectedNodeCount];
            currentProduction = ComputeProduction(materials, currentGroup1, currentGroup2, fissionSource);
            double[] group1Source = new double[ExpectedNodeCount];
            for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
            {
                double fissionOverK = fissionSource[nodeIndex] / currentEigenvalue;
                group1Source[nodeIndex] = materials[nodeIndex].Coefficients.ChiGroup1 * fissionOverK;
                RequireNonnegativeFinite(group1Source[nodeIndex], "group1_source", nodeIndex);
            }

            LinearSolveResult group1 = SolveLinear(
                materials,
                1,
                group1Source,
                group1Diagonal,
                fixture,
                benchmark);
            double[] group2Source = new double[ExpectedNodeCount];
            for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
            {
                double fissionOverK = fissionSource[nodeIndex] / currentEigenvalue;
                group2Source[nodeIndex] =
                    materials[nodeIndex].Coefficients.DownscatterGroup1To2PerM * group1.Solution[nodeIndex] +
                    materials[nodeIndex].Coefficients.ChiGroup2 * fissionOverK;
                RequireNonnegativeFinite(group2Source[nodeIndex], "group2_source", nodeIndex);
            }

            LinearSolveResult group2 = SolveLinear(
                materials,
                2,
                group2Source,
                group2Diagonal,
                fixture,
                benchmark);
            double trialProduction = ComputeProduction(materials, group1.Solution, group2.Solution, null);
            double nextEigenvalue = currentEigenvalue * (trialProduction / currentProduction);
            RequirePositiveFinite(nextEigenvalue, "cases[" + caseId + "].trial_eigenvalue");
            finalTrialPower = ComputePower(materials, group1.Solution, group2.Solution);
            finalNormalizationScale = benchmark.TargetPowerW / finalTrialPower;
            RequirePositiveFinite(finalNormalizationScale, "cases[" + caseId + "].normalization_scale");

            var nextGroup1 = new double[ExpectedNodeCount];
            var nextGroup2 = new double[ExpectedNodeCount];
            for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
            {
                nextGroup1[nodeIndex] = group1.Solution[nodeIndex] * finalNormalizationScale;
                nextGroup2[nodeIndex] = group2.Solution[nodeIndex] * finalNormalizationScale;
                RequireFlux(nextGroup1[nodeIndex], "normalized_group1", nodeIndex);
                RequireFlux(nextGroup2[nodeIndex], "normalized_group2", nodeIndex);
            }

            double normalizedProduction = ComputeProduction(materials, nextGroup1, nextGroup2, null);
            double normalizedPower = ComputePower(materials, nextGroup1, nextGroup2);
            double[] nextSourceShape = ComputeSourceShape(materials, nextGroup1, nextGroup2, normalizedProduction);
            ResidualMetrics residual = ComputeResidual(
                materials,
                fixture,
                nextGroup1,
                nextGroup2,
                nextEigenvalue);
            double deltaKAbsolute = Math.Abs(nextEigenvalue - currentEigenvalue);
            double kScale = Math.Max(Math.Abs(nextEigenvalue), Math.Abs(currentEigenvalue));
            double deltaKRelative = kScale == 0.0 ? 0.0 : deltaKAbsolute / kScale;
            double sourceShapeChange = ComputeSourceShapeChange(previousSourceShape, nextSourceShape);
            double powerBalance = Math.Abs(normalizedPower - benchmark.TargetPowerW) / benchmark.TargetPowerW;
            RequireFinite(deltaKAbsolute, "delta_k_absolute");
            RequireFinite(deltaKRelative, "delta_k_relative");
            RequireFinite(sourceShapeChange, "source_shape_change");
            RequireFinite(powerBalance, "power_balance");

            lastDiagnostics = new SolveStepDiagnostics(
                "Converged",
                "converged",
                outerIndex + 1,
                deltaKAbsolute,
                deltaKRelative,
                residual.AbsoluteInfinity,
                residual.RelativeInfinity,
                sourceShapeChange,
                powerBalance,
                "Succeeded",
                group1.IterationCount,
                group2.IterationCount,
                group1.IterationCount + group2.IterationCount,
                0,
                0,
                0,
                0,
                0,
                0);

            bool eigenvalueConverged =
                deltaKAbsolute <= benchmark.KAbsoluteTolerance ||
                deltaKRelative <= benchmark.KRelativeTolerance;
            bool converged =
                eigenvalueConverged &&
                residual.RelativeInfinity <= benchmark.ResidualTolerance &&
                sourceShapeChange <= benchmark.SourceShapeTolerance &&
                powerBalance <= benchmark.PowerBalanceTolerance;
            if (converged)
            {
                var snapshot = CreateSnapshot(
                    materials,
                    benchmark,
                    currentEigenvalue: nextEigenvalue,
                    normalizationScale: finalNormalizationScale,
                    totalPower: normalizedPower,
                    production: normalizedProduction,
                    group1: nextGroup1,
                    group2: nextGroup2,
                    initialPower: initialPower,
                    initialNormalizationScale: initialNormalizationScale,
                    finalTrialPower: finalTrialPower,
                    diagnostics: lastDiagnostics);
                return new SolveResult(snapshot);
            }

            Array.Copy(nextSourceShape, previousSourceShape, ExpectedNodeCount);
            currentGroup1 = nextGroup1;
            currentGroup2 = nextGroup2;
            currentEigenvalue = nextEigenvalue;
            currentProduction = normalizedProduction;
        }

        throw new ReproductionFailure(
            "Solve.Nonconverged",
            "cases[" + caseId + "]",
            "The standalone solver exhausted the frozen caller-supplied outer iteration limit without convergence; no last iterate was emitted.");
    }

    private static Snapshot CreateSnapshot(
        List<Material> materials,
        BenchmarkModel benchmark,
        double currentEigenvalue,
        double normalizationScale,
        double totalPower,
        double production,
        double[] group1,
        double[] group2,
        double initialPower,
        double initialNormalizationScale,
        double finalTrialPower,
        SolveStepDiagnostics diagnostics)
    {
        var flux = new double[ExpectedNodeCount * 2];
        var nodePower = new double[ExpectedNodeCount];
        var fissionSource = new double[ExpectedNodeCount];
        for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
        {
            flux[nodeIndex * 2] = group1[nodeIndex];
            flux[nodeIndex * 2 + 1] = group2[nodeIndex];
            double fissionRate =
                materials[nodeIndex].Coefficients.FissionGroup1PerM * group1[nodeIndex] +
                materials[nodeIndex].Coefficients.FissionGroup2PerM * group2[nodeIndex];
            nodePower[nodeIndex] = materials[nodeIndex].VolumeM3 *
                                   materials[nodeIndex].Coefficients.EnergyPerFissionJ *
                                   fissionRate;
            fissionSource[nodeIndex] =
                materials[nodeIndex].Coefficients.NuFissionGroup1PerM * group1[nodeIndex] +
                materials[nodeIndex].Coefficients.NuFissionGroup2PerM * group2[nodeIndex];
            RequireFinite(nodePower[nodeIndex], "node_power");
            RequireNonnegativeFinite(fissionSource[nodeIndex], "fission_source", nodeIndex);
        }

        return new Snapshot(
            benchmark.TargetPowerW,
            initialPower,
            initialNormalizationScale,
            finalTrialPower,
            currentEigenvalue,
            normalizationScale,
            totalPower,
            production,
            flux,
            nodePower,
            fissionSource,
            diagnostics);
    }

    private static SpatialFixture CreateFixture(BenchmarkModel benchmark)
    {
        var edges = new List<FixtureEdge>
        {
            new FixtureEdge(0, 1, "TowardEndB", benchmark.InteriorEdgeConductanceGroup1M2, benchmark.InteriorEdgeConductanceGroup2M2),
            new FixtureEdge(1, 0, "TowardEndA", benchmark.InteriorEdgeConductanceGroup1M2, benchmark.InteriorEdgeConductanceGroup2M2),
            new FixtureEdge(1, 2, "TowardEndB", benchmark.InteriorEdgeConductanceGroup1M2, benchmark.InteriorEdgeConductanceGroup2M2),
            new FixtureEdge(2, 1, "TowardEndA", benchmark.InteriorEdgeConductanceGroup1M2, benchmark.InteriorEdgeConductanceGroup2M2)
        };
        var boundaries = new List<FixtureBoundary>
        {
            new FixtureBoundary(0, "North", "Reflective", 0.0, 0.0),
            new FixtureBoundary(0, "East", "Reflective", 0.0, 0.0),
            new FixtureBoundary(0, "South", "Reflective", 0.0, 0.0),
            new FixtureBoundary(0, "West", "Reflective", 0.0, 0.0),
            new FixtureBoundary(0, "EndA", "Reflective", 0.0, 0.0),
            new FixtureBoundary(1, "North", "Reflective", 0.0, 0.0),
            new FixtureBoundary(1, "East", "Reflective", 0.0, 0.0),
            new FixtureBoundary(1, "South", "Reflective", 0.0, 0.0),
            new FixtureBoundary(1, "West", "Reflective", 0.0, 0.0),
            new FixtureBoundary(2, "North", "Reflective", 0.0, 0.0),
            new FixtureBoundary(2, "East", "Reflective", 0.0, 0.0),
            new FixtureBoundary(2, "South", "Reflective", 0.0, 0.0),
            new FixtureBoundary(2, "West", "Reflective", 0.0, 0.0),
            new FixtureBoundary(2, "EndB", "Reflective", 0.0, 0.0)
        };
        var nodes = new List<FixtureNode>
        {
            new FixtureNode(0, 0, edges.Where(edge => edge.SourceNodeIndex == 0).ToArray(), boundaries.Where(boundary => boundary.NodeIndex == 0).ToArray()),
            new FixtureNode(0, 1, edges.Where(edge => edge.SourceNodeIndex == 1).ToArray(), boundaries.Where(boundary => boundary.NodeIndex == 1).ToArray()),
            new FixtureNode(0, 2, edges.Where(edge => edge.SourceNodeIndex == 2).ToArray(), boundaries.Where(boundary => boundary.NodeIndex == 2).ToArray())
        };

        ValidateFixture(nodes, edges, boundaries, benchmark);
        return new SpatialFixture(nodes, edges, boundaries);
    }

    private static void ValidateFixture(
        List<FixtureNode> nodes,
        List<FixtureEdge> edges,
        List<FixtureBoundary> boundaries,
        BenchmarkModel benchmark)
    {
        if (nodes.Count != ExpectedNodeCount || edges.Count != 4 || boundaries.Count != 14)
        {
            throw new ReproductionFailure(
                "Fixture.Shape.Invalid",
                "topology",
                "The explicit spatial fixture does not contain the frozen node, edge, and boundary records.");
        }

        var edgeKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (FixtureEdge edge in edges)
        {
            if (edge.SourceNodeIndex < 0 || edge.SourceNodeIndex >= nodes.Count ||
                edge.TargetNodeIndex < 0 || edge.TargetNodeIndex >= nodes.Count ||
                edge.SourceNodeIndex == edge.TargetNodeIndex ||
                !DoubleBitsEqual(edge.Group1ConductanceM2, benchmark.InteriorEdgeConductanceGroup1M2) ||
                !DoubleBitsEqual(edge.Group2ConductanceM2, benchmark.InteriorEdgeConductanceGroup2M2) ||
                (edge.Direction != "TowardEndA" && edge.Direction != "TowardEndB") ||
                !edgeKeys.Add(
                    edge.SourceNodeIndex.ToString(CultureInfo.InvariantCulture) + "|" + edge.Direction))
            {
                throw new ReproductionFailure(
                    "Fixture.Edge.Invalid",
                    "topology.edges",
                    "Every explicit edge must bind valid distinct nodes, a unique direction, and the benchmark conductances.");
            }

            int expectedTarget = edge.Direction == "TowardEndA"
                ? edge.SourceNodeIndex - 1
                : edge.SourceNodeIndex + 1;
            if (edge.TargetNodeIndex != expectedTarget)
            {
                throw new ReproductionFailure(
                    "Fixture.Edge.Direction.Invalid",
                    "topology.edges",
                    "Explicit edge direction and target position are inconsistent.");
            }

            string reciprocalDirection = edge.Direction == "TowardEndA" ? "TowardEndB" : "TowardEndA";
            if (!edges.Any(candidate =>
                    candidate.SourceNodeIndex == edge.TargetNodeIndex &&
                    candidate.TargetNodeIndex == edge.SourceNodeIndex &&
                    candidate.Direction == reciprocalDirection &&
                    DoubleBitsEqual(candidate.Group1ConductanceM2, edge.Group1ConductanceM2) &&
                    DoubleBitsEqual(candidate.Group2ConductanceM2, edge.Group2ConductanceM2)))
            {
                throw new ReproductionFailure(
                    "Fixture.Edge.ReciprocalMissing",
                    "topology.edges",
                    "Every explicit edge must have a reciprocal record with the opposite direction.");
            }
        }

        var boundaryKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (FixtureBoundary boundary in boundaries)
        {
            if (boundary.NodeIndex < 0 || boundary.NodeIndex >= nodes.Count ||
                boundary.Classification != "Reflective" ||
                !DoubleBitsEqual(boundary.Group1ConductanceM2, 0.0) ||
                !DoubleBitsEqual(boundary.Group2ConductanceM2, 0.0) ||
                (boundary.Face != "North" && boundary.Face != "East" && boundary.Face != "South" &&
                 boundary.Face != "West" && boundary.Face != "EndA" && boundary.Face != "EndB") ||
                !boundaryKeys.Add(
                    boundary.NodeIndex.ToString(CultureInfo.InvariantCulture) + "|" + boundary.Face))
            {
                throw new ReproductionFailure(
                    "Fixture.Boundary.Invalid",
                    "topology.boundaries",
                    "Every explicit boundary must have a unique valid face and be reflective with zero conductance.");
            }
        }

        for (int nodeIndex = 0; nodeIndex < nodes.Count; nodeIndex++)
        {
            FixtureNode node = nodes[nodeIndex];
            if (node.NodeIndex != nodeIndex || node.BundlePosition != nodeIndex)
            {
                throw new ReproductionFailure(
                    "Fixture.Node.Order.Invalid",
                    "topology.nodes",
                    "Fixture node records must be in canonical channel/position order.");
            }

            string[] expectedFaces = nodeIndex switch
            {
                0 => new[] { "North", "East", "South", "West", "EndA" },
                1 => new[] { "North", "East", "South", "West" },
                2 => new[] { "North", "East", "South", "West", "EndB" },
                _ => Array.Empty<string>()
            };
            if (node.Boundaries.Length != expectedFaces.Length ||
                expectedFaces.Any(face => !node.Boundaries.Any(boundary => boundary.Face == face)))
            {
                throw new ReproductionFailure(
                    "Fixture.Boundary.Complete.Invalid",
                    "topology.nodes",
                    "Every fixture node must carry the complete explicit boundary-face outcome set.");
            }

            foreach (FixtureEdge edge in node.Neighbors)
            {
                if (edge.SourceNodeIndex != nodeIndex)
                {
                    throw new ReproductionFailure(
                        "Fixture.Edge.Binding.Invalid",
                        "topology.nodes",
                        "A node neighbor record is not bound to its explicit source node.");
                }
            }
        }
    }

    private static double[] BuildDiagonal(List<Material> materials, int group, SpatialFixture fixture)
    {
        var diagonal = new double[ExpectedNodeCount];
        for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
        {
            Coefficients coefficients = materials[nodeIndex].Coefficients;
            double removal = group == 1
                ? coefficients.AbsorptionGroup1PerM + coefficients.DownscatterGroup1To2PerM
                : coefficients.AbsorptionGroup2PerM;
            FixtureNode node = fixture.Nodes[nodeIndex];
            double conductanceSum = 0.0;
            foreach (FixtureEdge edge in node.Neighbors)
            {
                conductanceSum += group == 1 ? edge.Group1ConductanceM2 : edge.Group2ConductanceM2;
            }

            foreach (FixtureBoundary boundary in node.Boundaries)
            {
                conductanceSum += group == 1 ? boundary.Group1ConductanceM2 : boundary.Group2ConductanceM2;
            }

            double diagonalValue = removal + conductanceSum / materials[nodeIndex].VolumeM3;
            RequirePositiveFinite(diagonalValue, "diagonal[" + nodeIndex.ToString(CultureInfo.InvariantCulture) + "]");
            diagonal[nodeIndex] = diagonalValue;
        }

        return diagonal;
    }

    private static LinearSolveResult SolveLinear(
        List<Material> materials,
        int group,
        double[] source,
        double[] diagonal,
        SpatialFixture fixture,
        BenchmarkModel benchmark)
    {
        var solution = new double[ExpectedNodeCount];
        var candidate = new double[ExpectedNodeCount];
        var applied = new double[ExpectedNodeCount];
        var candidateApplied = new double[ExpectedNodeCount];
        for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
        {
            RequireNonnegativeFinite(source[nodeIndex], "linear_source", nodeIndex);
        }

        for (int innerIndex = 0; innerIndex < benchmark.MaximumInnerIterations; innerIndex++)
        {
            Apply(materials, group, solution, applied, fixture);
            for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
            {
                double correction = (source[nodeIndex] - applied[nodeIndex]) / diagonal[nodeIndex];
                double next = solution[nodeIndex] + correction;
                RequireNonnegativeFinite(next, "linear_candidate", nodeIndex);
                candidate[nodeIndex] = next;
            }

            Apply(materials, group, candidate, candidateApplied, fixture);
            double absoluteResidual = 0.0;
            double scale = 0.0;
            for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
            {
                double difference = candidateApplied[nodeIndex] - source[nodeIndex];
                double absoluteDifference = Math.Abs(difference);
                double rowScale = Math.Abs(candidateApplied[nodeIndex]) + Math.Abs(source[nodeIndex]);
                RequireFinite(absoluteDifference, "inner_absolute_residual");
                RequireFinite(rowScale, "inner_residual_scale");
                absoluteResidual = Math.Max(absoluteResidual, absoluteDifference);
                scale = Math.Max(scale, rowScale);
            }

            double relativeResidual = scale == 0.0 ? 0.0 : absoluteResidual / scale;
            RequireFinite(relativeResidual, "inner_relative_residual");
            Array.Copy(candidate, solution, ExpectedNodeCount);
            if (absoluteResidual <= benchmark.InnerAbsoluteTolerance ||
                relativeResidual <= benchmark.InnerRelativeTolerance)
            {
                return new LinearSolveResult(solution, innerIndex + 1, absoluteResidual, relativeResidual);
            }
        }

        throw new ReproductionFailure(
            "Solve.InnerNonconverged",
            "linear_solve",
            "The standalone Jacobi solve exhausted the frozen inner iteration limit; no last iterate was accepted.");
    }

    private static void Apply(
        List<Material> materials,
        int group,
        double[] flux,
        double[] destination,
        SpatialFixture fixture)
    {
        for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
        {
            RequireFlux(flux[nodeIndex], "operator_flux", nodeIndex);
            Coefficients coefficients = materials[nodeIndex].Coefficients;
            double removal = group == 1
                ? coefficients.AbsorptionGroup1PerM + coefficients.DownscatterGroup1To2PerM
                : coefficients.AbsorptionGroup2PerM;
            double result = removal * flux[nodeIndex];
            double leakage = 0.0;
            FixtureNode node = fixture.Nodes[nodeIndex];
            foreach (FixtureEdge edge in node.Neighbors)
            {
                double difference = flux[nodeIndex] - flux[edge.TargetNodeIndex];
                double conductance = group == 1 ? edge.Group1ConductanceM2 : edge.Group2ConductanceM2;
                leakage += conductance * difference;
            }

            foreach (FixtureBoundary boundary in node.Boundaries)
            {
                double conductance = group == 1 ? boundary.Group1ConductanceM2 : boundary.Group2ConductanceM2;
                leakage += conductance * flux[nodeIndex];
            }

            double leakageContribution = leakage / materials[nodeIndex].VolumeM3;
            result += leakageContribution;
            RequireFinite(result, "operator_result");
            destination[nodeIndex] = result;
        }
    }

    private static ResidualMetrics ComputeResidual(
        List<Material> materials,
        SpatialFixture fixture,
        double[] group1,
        double[] group2,
        double eigenvalue)
    {
        var group1Applied = new double[ExpectedNodeCount];
        var group2Applied = new double[ExpectedNodeCount];
        Apply(materials, 1, group1, group1Applied, fixture);
        Apply(materials, 2, group2, group2Applied, fixture);
        var fissionSource = new double[ExpectedNodeCount];
        double production = ComputeProduction(materials, group1, group2, fissionSource);
        double maximumAbsolute = 0.0;
        double maximumScale = 0.0;
        for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
        {
            double fissionOverK = fissionSource[nodeIndex] / eigenvalue;
            double group1Right = materials[nodeIndex].Coefficients.ChiGroup1 * fissionOverK;
            double group2Right =
                materials[nodeIndex].Coefficients.DownscatterGroup1To2PerM * group1[nodeIndex] +
                materials[nodeIndex].Coefficients.ChiGroup2 * fissionOverK;
            AccumulateResidual(group1Applied[nodeIndex], group1Right, ref maximumAbsolute, ref maximumScale);
            AccumulateResidual(group2Applied[nodeIndex], group2Right, ref maximumAbsolute, ref maximumScale);
        }

        double relative = maximumScale == 0.0 ? 0.0 : maximumAbsolute / maximumScale;
        RequireFinite(relative, "residual_relative");
        RequireFinite(production, "residual_production");
        return new ResidualMetrics(maximumAbsolute, relative);
    }

    private static double ComputeProduction(
        List<Material> materials,
        double[] group1,
        double[] group2,
        double[]? destination)
    {
        double production = 0.0;
        for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
        {
            double value =
                materials[nodeIndex].Coefficients.NuFissionGroup1PerM * group1[nodeIndex] +
                materials[nodeIndex].Coefficients.NuFissionGroup2PerM * group2[nodeIndex];
            RequireNonnegativeFinite(value, "fission_production", nodeIndex);
            if (destination != null)
            {
                destination[nodeIndex] = value;
            }

            double volumeContribution = materials[nodeIndex].VolumeM3 * value;
            RequireFinite(volumeContribution, "fission_production_volume");
            production += volumeContribution;
            RequireFinite(production, "fission_production_total");
        }

        RequirePositiveFinite(production, "fission_production_total");
        return production;
    }

    private static double ComputePower(List<Material> materials, double[] group1, double[] group2)
    {
        double power = 0.0;
        for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
        {
            double fissionRate =
                materials[nodeIndex].Coefficients.FissionGroup1PerM * group1[nodeIndex] +
                materials[nodeIndex].Coefficients.FissionGroup2PerM * group2[nodeIndex];
            RequireNonnegativeFinite(fissionRate, "fission_rate", nodeIndex);
            double localPower = materials[nodeIndex].VolumeM3 *
                                materials[nodeIndex].Coefficients.EnergyPerFissionJ *
                                fissionRate;
            RequireNonnegativeFinite(localPower, "local_power", nodeIndex);
            power += localPower;
            RequireFinite(power, "power_total");
        }

        RequirePositiveFinite(power, "power_total");
        return power;
    }

    private static double[] ComputeSourceShape(
        List<Material> materials,
        double[] group1,
        double[] group2,
        double production)
    {
        var shape = new double[ExpectedNodeCount];
        for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
        {
            double source =
                materials[nodeIndex].Coefficients.NuFissionGroup1PerM * group1[nodeIndex] +
                materials[nodeIndex].Coefficients.NuFissionGroup2PerM * group2[nodeIndex];
            double shapeValue = materials[nodeIndex].VolumeM3 * source / production;
            RequireNonnegativeFinite(shapeValue, "source_shape", nodeIndex);
            shape[nodeIndex] = shapeValue;
        }

        return shape;
    }

    private static double ComputeSourceShapeChange(double[] previous, double[] next)
    {
        double maximum = 0.0;
        for (int nodeIndex = 0; nodeIndex < previous.Length; nodeIndex++)
        {
            double difference = Math.Abs(next[nodeIndex] - previous[nodeIndex]);
            RequireFinite(difference, "source_shape_change");
            maximum = Math.Max(maximum, difference);
        }

        return maximum;
    }

    private static void AccumulateResidual(
        double left,
        double right,
        ref double maximumAbsolute,
        ref double maximumScale)
    {
        double difference = left - right;
        double absoluteDifference = Math.Abs(difference);
        double scale = Math.Abs(left) + Math.Abs(right);
        RequireFinite(absoluteDifference, "residual_absolute");
        RequireFinite(scale, "residual_scale");
        maximumAbsolute = Math.Max(maximumAbsolute, absoluteDifference);
        maximumScale = Math.Max(maximumScale, scale);
    }

    private static LookupModel Lookup(
        SourceTableModel table,
        string unitsProfileId,
        string tableChecksum,
        double burnup,
        int nodeIndex)
    {
        RequireNonnegativeFinite(burnup, "burnup[" + nodeIndex.ToString(CultureInfo.InvariantCulture) + "]", nodeIndex);
        if (burnup < table.Rows[0].BurnupJPerKgHm || burnup > table.Rows[^1].BurnupJPerKgHm)
        {
            throw new ReproductionFailure(
                "Lookup.OutOfRange",
                "burnup[" + nodeIndex.ToString(CultureInfo.InvariantCulture) + "]",
                "Burnup outside the frozen table domain is rejected without extrapolation or clamping.");
        }

        int exactIndex = -1;
        for (int rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            if (table.Rows[rowIndex].BurnupJPerKgHm == burnup)
            {
                exactIndex = rowIndex;
                break;
            }
        }

        if (exactIndex >= 0)
        {
            double exactFraction = exactIndex == table.Rows.Count - 1 && table.Rows.Count > 1 ? 1.0 : 0.0;
            return new LookupModel(
                nodeIndex,
                burnup,
                exactIndex,
                exactIndex,
                exactFraction,
                table.TableId,
                table.MaterialVariantId,
                unitsProfileId,
                tableChecksum,
                ToCoefficients(table.Rows[exactIndex].Coefficients));
        }

        int upperIndex = 1;
        while (upperIndex < table.Rows.Count && table.Rows[upperIndex].BurnupJPerKgHm < burnup)
        {
            upperIndex++;
        }

        int lowerIndex = upperIndex - 1;
        double lowerBurnup = table.Rows[lowerIndex].BurnupJPerKgHm;
        double upperBurnup = table.Rows[upperIndex].BurnupJPerKgHm;
        double denominator = upperBurnup - lowerBurnup;
        double alpha = (burnup - lowerBurnup) / denominator;
        RequireFinite(alpha, "interpolation_alpha");
        if (alpha <= 0.0 || alpha >= 1.0)
        {
            throw new ReproductionFailure(
                "Lookup.Bracket.Invalid",
                "burnup[" + nodeIndex.ToString(CultureInfo.InvariantCulture) + "]",
                "The selected interpolation fraction must be strictly between zero and one.");
        }

        Coefficients lower = ToCoefficients(table.Rows[lowerIndex].Coefficients);
        Coefficients upper = ToCoefficients(table.Rows[upperIndex].Coefficients);
        return new LookupModel(
            nodeIndex,
            burnup,
            lowerIndex,
            upperIndex,
            alpha,
            table.TableId,
            table.MaterialVariantId,
            unitsProfileId,
            tableChecksum,
            Interpolate(lower, upper, alpha));
    }

    private static Coefficients Interpolate(Coefficients lower, Coefficients upper, double alpha)
    {
        return new Coefficients(
            InterpolateValue(lower.AbsorptionGroup1PerM, upper.AbsorptionGroup1PerM, alpha),
            InterpolateValue(lower.AbsorptionGroup2PerM, upper.AbsorptionGroup2PerM, alpha),
            InterpolateValue(lower.FissionGroup1PerM, upper.FissionGroup1PerM, alpha),
            InterpolateValue(lower.FissionGroup2PerM, upper.FissionGroup2PerM, alpha),
            InterpolateValue(lower.NuFissionGroup1PerM, upper.NuFissionGroup1PerM, alpha),
            InterpolateValue(lower.NuFissionGroup2PerM, upper.NuFissionGroup2PerM, alpha),
            InterpolateValue(lower.DownscatterGroup1To2PerM, upper.DownscatterGroup1To2PerM, alpha),
            InterpolateValue(lower.ChiGroup1, upper.ChiGroup1, alpha),
            InterpolateValue(lower.EnergyPerFissionJ, upper.EnergyPerFissionJ, alpha));
    }

    private static double InterpolateValue(double lower, double upper, double alpha)
    {
        return ((1.0 - alpha) * lower) + (alpha * upper);
    }

    private static Coefficients ToCoefficients(CoefficientsModel model)
    {
        var coefficients = new Coefficients(
            model.AbsorptionGroup1PerM,
            model.AbsorptionGroup2PerM,
            model.FissionGroup1PerM,
            model.FissionGroup2PerM,
            model.NuFissionGroup1PerM,
            model.NuFissionGroup2PerM,
            model.DownscatterGroup1To2PerM,
            model.ChiGroup1,
            model.EnergyPerFissionJ);
        ValidateCoefficients(coefficients, "source_table");
        return coefficients;
    }

    private static void ValidateInputBundle(
        SourceModel source,
        PackModel pack,
        ReducedManifestModel reducedManifest,
        BenchmarkModel benchmark,
        string sourceSha256,
        string packSha256,
        string reducedManifestSha256,
        string benchmarkSha256)
    {
        if (source.Tables.Count != 1 || pack.Tables.Count != 1 || reducedManifest.Tables.Count != 1)
        {
            throw new ReproductionFailure(
                "Input.Tables.Unsupported",
                "tables",
                "The bounded checker requires exactly one MAT-SYN table in each frozen input artifact.");
        }

        SourceTableModel sourceTable = source.Tables[0];
        PackTableModel packTable = pack.Tables[0];
        ManifestTableModel manifestTable = reducedManifest.Tables[0];
        RequireEqual(source.SourceArtifactId, SourceArtifactId, "source.source_artifact_id");
        RequireEqual(pack.PackArtifactId, PackArtifactId, "pack.pack_artifact_id");
        RequireEqual(pack.SourceArtifactId, SourceArtifactId, "pack.source_artifact_id");
        RequireEqual(source.SourceProvenance, SourceProvenance, "source.source_provenance");
        RequireEqual(pack.SourceProvenance, SourceProvenance, "pack.source_provenance");
        RequireEqual(source.TransformId, TransformId, "source.transform_id");
        RequireEqual(pack.TransformId, TransformId, "pack.transform_id");
        RequireEqual(source.DataVersion, DataVersion, "source.data_version");
        RequireEqual(pack.DataVersion, DataVersion, "pack.data_version");
        RequireEqual(source.UnitsProfileId, UnitsProfileId, "source.units_profile_id");
        RequireEqual(pack.UnitsProfileId, UnitsProfileId, "pack.units_profile_id");
        RequireEqual(sourceTable.TableId, TableId, "source.tables[0].table_id");
        RequireEqual(packTable.TableId, TableId, "pack.tables[0].table_id");
        RequireEqual(sourceTable.MaterialVariantId, MaterialVariantId, "source.tables[0].material_variant_id");
        RequireEqual(packTable.MaterialVariantId, MaterialVariantId, "pack.tables[0].material_variant_id");
        RequireEqual(packTable.Checksum, ExpectedTableChecksum, "pack.tables[0].checksum");
        RequireEqual(manifestTable.TableId, TableId, "manifest.tables[0].table_id");
        RequireEqual(manifestTable.MaterialVariantId, MaterialVariantId, "manifest.tables[0].material_variant_id");
        RequireEqual(manifestTable.Checksum, ExpectedTableChecksum, "manifest.tables[0].checksum");
        RequireEqual(manifestTable.RowCount.ToString(CultureInfo.InvariantCulture), "3", "manifest.tables[0].row_count");
        RequireEqual(manifestTable.BurnupMinJPerKgHm.ToString("R", CultureInfo.InvariantCulture), "0", "manifest.tables[0].burnup_min");
        RequireEqual(manifestTable.BurnupMaxJPerKgHm.ToString("R", CultureInfo.InvariantCulture), "100000", "manifest.tables[0].burnup_max");
        RequireEqual(reducedManifest.PackArtifactId, PackArtifactId, "manifest.pack_artifact_id");
        RequireEqual(reducedManifest.PackSha256, packSha256, "manifest.pack_sha256");
        RequireEqual(reducedManifest.SourceArtifactId, SourceArtifactId, "manifest.source_artifact_id");
        RequireEqual(reducedManifest.SourceSha256, sourceSha256, "manifest.source_sha256");
        RequireEqual(reducedManifest.SourceProvenance, SourceProvenance, "manifest.source_provenance");
        RequireEqual(reducedManifest.DataVersion, DataVersion, "manifest.data_version");
        RequireEqual(reducedManifest.UnitsProfileId, UnitsProfileId, "manifest.units_profile_id");
        RequireEqual(reducedManifest.ApprovalStatus, "candidate", "manifest.approval_status");
        RequireEqual(reducedManifest.EvidenceClass, SyntheticEvidence, "manifest.evidence_class");
        RequireEqual(benchmark.ScenarioId, BenchmarkScenarioId, "benchmark.scenario_id");
        RequireEqual(sourceSha256, ExpectedSourceSha256, "source_sha256");
        RequireEqual(packSha256, ExpectedPackSha256, "pack_sha256");
        RequireEqual(reducedManifestSha256, ExpectedReducedManifestSha256, "reduced_manifest_sha256");
        RequireEqual(benchmarkSha256, ExpectedBenchmarkSha256, "benchmark_sha256");

        if (sourceTable.Rows.Count != 3 || packTable.Rows.Count != 3)
        {
            throw new ReproductionFailure("Input.Rows.Unsupported", "tables", "The MAT-SYN fixture must contain three ordered rows.");
        }

        for (int rowIndex = 0; rowIndex < 3; rowIndex++)
        {
            RowModel sourceRow = sourceTable.Rows[rowIndex];
            RowModel packRow = packTable.Rows[rowIndex];
            if (!DoubleBitsEqual(sourceRow.BurnupJPerKgHm, packRow.BurnupJPerKgHm) ||
                !CoefficientsEqual(sourceRow.Coefficients, packRow.Coefficients))
            {
                throw new ReproductionFailure(
                    "Input.SourcePackMismatch",
                    "tables[0].rows[" + rowIndex.ToString(CultureInfo.InvariantCulture) + "]",
                    "The source and reduced pack rows are not bitwise identical.");
            }
        }

        _ = benchmark;
    }

    private static bool CoefficientsEqual(CoefficientsModel left, CoefficientsModel right)
    {
        return DoubleBitsEqual(left.AbsorptionGroup1PerM, right.AbsorptionGroup1PerM) &&
               DoubleBitsEqual(left.AbsorptionGroup2PerM, right.AbsorptionGroup2PerM) &&
               DoubleBitsEqual(left.FissionGroup1PerM, right.FissionGroup1PerM) &&
               DoubleBitsEqual(left.FissionGroup2PerM, right.FissionGroup2PerM) &&
               DoubleBitsEqual(left.NuFissionGroup1PerM, right.NuFissionGroup1PerM) &&
               DoubleBitsEqual(left.NuFissionGroup2PerM, right.NuFissionGroup2PerM) &&
               DoubleBitsEqual(left.DownscatterGroup1To2PerM, right.DownscatterGroup1To2PerM) &&
               DoubleBitsEqual(left.ChiGroup1, right.ChiGroup1) &&
               DoubleBitsEqual(left.EnergyPerFissionJ, right.EnergyPerFissionJ);
    }

    private static SourceModel ParseSource(byte[] bytes)
    {
        using JsonDocument document = ParseJson(bytes, "source");
        Dictionary<string, JsonElement> root = ReadObject(
            document.RootElement,
            "source",
            "format",
            "evidence_class",
            "source_artifact_id",
            "source_provenance",
            "transform_id",
            "data_version",
            "units_profile_id",
            "tables");
        RequireString(root, "format", "source.format", SourceFormat);
        RequireString(root, "evidence_class", "source.evidence_class", SyntheticEvidence);
        string sourceArtifactId = ReadString(root["source_artifact_id"], "source.source_artifact_id");
        string provenance = ReadPathFreeString(root["source_provenance"], "source.source_provenance");
        string transformId = ReadString(root["transform_id"], "source.transform_id");
        string dataVersion = ReadString(root["data_version"], "source.data_version");
        string units = ReadString(root["units_profile_id"], "source.units_profile_id");
        var tables = new List<SourceTableModel>();
        int tableIndex = 0;
        foreach (JsonElement tableElement in ReadArray(root["tables"], "source.tables"))
        {
            string path = "source.tables[" + tableIndex.ToString(CultureInfo.InvariantCulture) + "]";
            Dictionary<string, JsonElement> table = ReadObject(tableElement, path, "table_id", "material_variant_id", "rows");
            string tableId = ReadString(table["table_id"], path + ".table_id");
            RequireGuid(tableId, path + ".table_id");
            string material = ReadString(table["material_variant_id"], path + ".material_variant_id");
            List<RowModel> rows = ReadRows(table["rows"], path + ".rows");
            tables.Add(new SourceTableModel(tableId, material, rows));
            tableIndex++;
        }

        if (tables.Count == 0)
        {
            throw new ReproductionFailure("Source.Tables.Empty", "source.tables", "At least one source table is required.");
        }

        return new SourceModel(sourceArtifactId, provenance, transformId, dataVersion, units, tables);
    }

    private static PackModel ParsePack(byte[] bytes)
    {
        using JsonDocument document = ParseJson(bytes, "pack");
        Dictionary<string, JsonElement> root = ReadObject(
            document.RootElement,
            "pack",
            "format",
            "approval_status",
            "evidence_class",
            "runtime_use",
            "pack_artifact_id",
            "schema_version",
            "data_version",
            "units_profile_id",
            "source_artifact_id",
            "source_provenance",
            "transform_id",
            "tables");
        RequireString(root, "format", "pack.format", PackFormat);
        RequireString(root, "approval_status", "pack.approval_status", "candidate");
        RequireString(root, "evidence_class", "pack.evidence_class", SyntheticEvidence);
        RequireString(root, "runtime_use", "pack.runtime_use", "candidate_interpolation_input");
        string packArtifactId = ReadString(root["pack_artifact_id"], "pack.pack_artifact_id");
        RequireInt(root["schema_version"], "pack.schema_version", 1);
        string dataVersion = ReadString(root["data_version"], "pack.data_version");
        string units = ReadString(root["units_profile_id"], "pack.units_profile_id");
        string sourceArtifactId = ReadString(root["source_artifact_id"], "pack.source_artifact_id");
        string provenance = ReadPathFreeString(root["source_provenance"], "pack.source_provenance");
        string transformId = ReadString(root["transform_id"], "pack.transform_id");
        var tables = new List<PackTableModel>();
        int tableIndex = 0;
        foreach (JsonElement tableElement in ReadArray(root["tables"], "pack.tables"))
        {
            string path = "pack.tables[" + tableIndex.ToString(CultureInfo.InvariantCulture) + "]";
            Dictionary<string, JsonElement> table = ReadObject(
                tableElement,
                path,
                "table_id",
                "schema_version",
                "data_version",
                "material_variant_id",
                "units_profile_id",
                "source_provenance",
                "checksum",
                "rows");
            string tableId = ReadString(table["table_id"], path + ".table_id");
            RequireGuid(tableId, path + ".table_id");
            RequireInt(table["schema_version"], path + ".schema_version", 1);
            string tableDataVersion = ReadString(table["data_version"], path + ".data_version");
            string material = ReadString(table["material_variant_id"], path + ".material_variant_id");
            string tableUnits = ReadString(table["units_profile_id"], path + ".units_profile_id");
            string tableProvenance = ReadPathFreeString(table["source_provenance"], path + ".source_provenance");
            string checksum = ReadDigest(table["checksum"], path + ".checksum");
            List<RowModel> rows = ReadRows(table["rows"], path + ".rows");
            tables.Add(new PackTableModel(
                tableId,
                tableDataVersion,
                material,
                tableUnits,
                tableProvenance,
                checksum,
                rows));
            tableIndex++;
        }

        return new PackModel(
            packArtifactId,
            dataVersion,
            units,
            sourceArtifactId,
            provenance,
            transformId,
            tables);
    }

    private static ReducedManifestModel ParseReducedManifest(byte[] bytes)
    {
        using JsonDocument document = ParseJson(bytes, "reduced manifest");
        Dictionary<string, JsonElement> root = ReadObject(
            document.RootElement,
            "manifest",
            "format",
            "manifest_schema_version",
            "approval_status",
            "evidence_class",
            "pack_artifact_id",
            "pack_format",
            "pack_sha256",
            "source_artifact_id",
            "source_sha256",
            "source_provenance",
            "transform_id",
            "data_version",
            "units_profile_id",
            "tables");
        RequireString(root, "format", "manifest.format", ReducedManifestFormat);
        RequireInt(root["manifest_schema_version"], "manifest.manifest_schema_version", 1);
        string approval = ReadString(root["approval_status"], "manifest.approval_status");
        string evidence = ReadString(root["evidence_class"], "manifest.evidence_class");
        string packArtifactId = ReadString(root["pack_artifact_id"], "manifest.pack_artifact_id");
        RequireString(root, "pack_format", "manifest.pack_format", PackFormat);
        string packSha = ReadDigest(root["pack_sha256"], "manifest.pack_sha256");
        string sourceArtifactId = ReadString(root["source_artifact_id"], "manifest.source_artifact_id");
        string sourceSha = ReadDigest(root["source_sha256"], "manifest.source_sha256");
        string provenance = ReadPathFreeString(root["source_provenance"], "manifest.source_provenance");
        string transformId = ReadString(root["transform_id"], "manifest.transform_id");
        string dataVersion = ReadString(root["data_version"], "manifest.data_version");
        string units = ReadString(root["units_profile_id"], "manifest.units_profile_id");
        var tables = new List<ManifestTableModel>();
        int tableIndex = 0;
        foreach (JsonElement tableElement in ReadArray(root["tables"], "manifest.tables"))
        {
            string path = "manifest.tables[" + tableIndex.ToString(CultureInfo.InvariantCulture) + "]";
            Dictionary<string, JsonElement> table = ReadObject(
                tableElement,
                path,
                "table_id",
                "material_variant_id",
                "checksum",
                "row_count",
                "burnup_min_j_per_kg_hm",
                "burnup_max_j_per_kg_hm");
            tables.Add(new ManifestTableModel(
                ReadString(table["table_id"], path + ".table_id"),
                ReadString(table["material_variant_id"], path + ".material_variant_id"),
                ReadDigest(table["checksum"], path + ".checksum"),
                ReadInt(table["row_count"], path + ".row_count"),
                ReadDouble(table["burnup_min_j_per_kg_hm"], path + ".burnup_min_j_per_kg_hm"),
                ReadDouble(table["burnup_max_j_per_kg_hm"], path + ".burnup_max_j_per_kg_hm")));
            tableIndex++;
        }

        return new ReducedManifestModel(
            approval,
            evidence,
            packArtifactId,
            packSha,
            sourceArtifactId,
            sourceSha,
            provenance,
            transformId,
            dataVersion,
            units,
            tables);
    }

    private static BenchmarkModel ParseBenchmark(byte[] bytes)
    {
        using JsonDocument document = ParseJson(bytes, "benchmark");
        Dictionary<string, JsonElement> root = ReadObject(
            document.RootElement,
            "benchmark",
            "format",
            "task_id",
            "scenario_id",
            "status",
            "purpose",
            "case",
            "linear_solve_policy",
            "convergence_policy",
            "measurement",
            "evidence_boundary");
        RequireString(root, "format", "benchmark.format", BenchmarkFormat);
        RequireString(root, "task_id", "benchmark.task_id", "P4-T08");
        string scenarioId = ReadString(root["scenario_id"], "benchmark.scenario_id");
        RequireString(root, "status", "benchmark.status", "synthetic_not_golden");
        RequireString(
            root,
            "purpose",
            "benchmark.purpose",
            "Measure the current deterministic scalar Core spatial solve without selecting a performance target or changing runtime code.");
        RequireString(
            root,
            "evidence_boundary",
            "benchmark.evidence_boundary",
            "Synthetic scenario and machine-specific observation only; no DONJON5 comparison, golden value, tolerance approval, or release budget.");

        Dictionary<string, JsonElement> scenario = ReadObject(
            root["case"],
            "benchmark.case",
            "node_count",
            "topology",
            "boundary_classification",
            "interior_edge_conductance_group1_m2",
            "interior_edge_conductance_group2_m2",
            "node_volume_m3",
            "absorption_group1_per_m",
            "absorption_group2_per_m",
            "downscatter_group1_to_2_per_m",
            "fission_group1_per_m",
            "fission_group2_per_m",
            "nu_fission_group1_per_m",
            "nu_fission_group2_per_m",
            "chi_group1",
            "chi_group2",
            "energy_per_fission_j",
            "initial_flux_group1",
            "initial_flux_group2",
            "target_power_w",
            "initial_eigenvalue");
        RequireInt(scenario["node_count"], "benchmark.case.node_count", ExpectedNodeCount);
        RequireString(scenario, "topology", "benchmark.case.topology", "one channel, three explicit within-channel positions");
        RequireString(scenario, "boundary_classification", "benchmark.case.boundary_classification", "all reflective");
        RequireDouble(scenario["interior_edge_conductance_group1_m2"], "benchmark.case.edge_group1", 0.6);
        RequireDouble(scenario["interior_edge_conductance_group2_m2"], "benchmark.case.edge_group2", 0.6);
        RequireDouble(scenario["node_volume_m3"], "benchmark.case.node_volume_m3", 1.0);
        _ = ReadDouble(scenario["absorption_group1_per_m"], "benchmark.case.absorption_group1_per_m");
        _ = ReadDouble(scenario["absorption_group2_per_m"], "benchmark.case.absorption_group2_per_m");
        _ = ReadDouble(scenario["downscatter_group1_to_2_per_m"], "benchmark.case.downscatter_group1_to_2_per_m");
        _ = ReadDouble(scenario["fission_group1_per_m"], "benchmark.case.fission_group1_per_m");
        _ = ReadDouble(scenario["fission_group2_per_m"], "benchmark.case.fission_group2_per_m");
        _ = ReadDouble(scenario["nu_fission_group1_per_m"], "benchmark.case.nu_fission_group1_per_m");
        _ = ReadDouble(scenario["nu_fission_group2_per_m"], "benchmark.case.nu_fission_group2_per_m");
        _ = ReadDouble(scenario["chi_group1"], "benchmark.case.chi_group1");
        _ = ReadDouble(scenario["chi_group2"], "benchmark.case.chi_group2");
        _ = ReadDouble(scenario["energy_per_fission_j"], "benchmark.case.energy_per_fission_j");

        Dictionary<string, JsonElement> linear = ReadObject(
            root["linear_solve_policy"],
            "benchmark.linear_solve_policy",
            "method_id",
            "absolute_residual_tolerance",
            "relative_residual_tolerance",
            "maximum_inner_iterations");
        RequireString(linear, "method_id", "benchmark.linear_solve_policy.method_id", "jacobi-v1");
        RequireDouble(linear["absolute_residual_tolerance"], "benchmark.linear_solve_policy.absolute_residual_tolerance", 1e-14);
        RequireDouble(linear["relative_residual_tolerance"], "benchmark.linear_solve_policy.relative_residual_tolerance", 1e-14);
        RequireInt(linear["maximum_inner_iterations"], "benchmark.linear_solve_policy.maximum_inner_iterations", 512);

        Dictionary<string, JsonElement> convergence = ReadObject(
            root["convergence_policy"],
            "benchmark.convergence_policy",
            "k_absolute_tolerance",
            "k_relative_tolerance",
            "residual_tolerance",
            "source_shape_tolerance",
            "power_balance_tolerance",
            "maximum_iterations");
        RequireDouble(convergence["k_absolute_tolerance"], "benchmark.convergence_policy.k_absolute_tolerance", 1e-6);
        RequireDouble(convergence["k_relative_tolerance"], "benchmark.convergence_policy.k_relative_tolerance", 1e-6);
        RequireDouble(convergence["residual_tolerance"], "benchmark.convergence_policy.residual_tolerance", 1e-6);
        RequireDouble(convergence["source_shape_tolerance"], "benchmark.convergence_policy.source_shape_tolerance", 1e-6);
        RequireDouble(convergence["power_balance_tolerance"], "benchmark.convergence_policy.power_balance_tolerance", 1e-6);
        RequireInt(convergence["maximum_iterations"], "benchmark.convergence_policy.maximum_iterations", 2);

        Dictionary<string, JsonElement> measurement = ReadObject(
            root["measurement"],
            "benchmark.measurement",
            "warmup_iterations",
            "measured_iterations",
            "clock",
            "allocation_counter",
            "performance_target");
        RequireInt(measurement["warmup_iterations"], "benchmark.measurement.warmup_iterations", 10);
        RequireInt(measurement["measured_iterations"], "benchmark.measurement.measured_iterations", 200);
        RequireString(measurement, "clock", "benchmark.measurement.clock", "System.Diagnostics.Stopwatch");
        RequireString(measurement, "allocation_counter", "benchmark.measurement.allocation_counter", "GC.GetAllocatedBytesForCurrentThread");
        RequireNull(measurement["performance_target"], "benchmark.measurement.performance_target");

        return new BenchmarkModel(
            scenarioId,
            ReadDouble(scenario["interior_edge_conductance_group1_m2"], "benchmark.case.edge_group1"),
            ReadDouble(scenario["interior_edge_conductance_group2_m2"], "benchmark.case.edge_group2"),
            ReadDouble(scenario["node_volume_m3"], "benchmark.case.node_volume_m3"),
            ReadDoubleArray(scenario["initial_flux_group1"], "benchmark.case.initial_flux_group1", ExpectedNodeCount),
            ReadDoubleArray(scenario["initial_flux_group2"], "benchmark.case.initial_flux_group2", ExpectedNodeCount),
            ReadDouble(scenario["target_power_w"], "benchmark.case.target_power_w"),
            ReadDouble(scenario["initial_eigenvalue"], "benchmark.case.initial_eigenvalue"),
            ReadDouble(linear["absolute_residual_tolerance"], "benchmark.linear_solve_policy.absolute_residual_tolerance"),
            ReadDouble(linear["relative_residual_tolerance"], "benchmark.linear_solve_policy.relative_residual_tolerance"),
            ReadInt(linear["maximum_inner_iterations"], "benchmark.linear_solve_policy.maximum_inner_iterations"),
            ReadDouble(convergence["k_absolute_tolerance"], "benchmark.convergence_policy.k_absolute_tolerance"),
            ReadDouble(convergence["k_relative_tolerance"], "benchmark.convergence_policy.k_relative_tolerance"),
            ReadDouble(convergence["residual_tolerance"], "benchmark.convergence_policy.residual_tolerance"),
            ReadDouble(convergence["source_shape_tolerance"], "benchmark.convergence_policy.source_shape_tolerance"),
            ReadDouble(convergence["power_balance_tolerance"], "benchmark.convergence_policy.power_balance_tolerance"),
            ReadInt(convergence["maximum_iterations"], "benchmark.convergence_policy.maximum_iterations"));
    }

    private static List<RowModel> ReadRows(JsonElement element, string path)
    {
        var rows = new List<RowModel>();
        double previousBurnup = -1.0;
        int rowIndex = 0;
        foreach (JsonElement rowElement in ReadArray(element, path))
        {
            string rowPath = path + "[" + rowIndex.ToString(CultureInfo.InvariantCulture) + "]";
            Dictionary<string, JsonElement> row = ReadObject(rowElement, rowPath, "burnup_j_per_kg_hm", "coefficients");
            double burnup = ReadDouble(row["burnup_j_per_kg_hm"], rowPath + ".burnup_j_per_kg_hm");
            if (burnup < 0.0 || (rowIndex > 0 && burnup <= previousBurnup))
            {
                throw new ReproductionFailure("Input.Rows.Order.Invalid", rowPath, "Burnup knots must be strictly increasing and nonnegative.");
            }

            Dictionary<string, JsonElement> coefficients = ReadObject(
                row["coefficients"],
                rowPath + ".coefficients",
                "absorption_group1_per_m",
                "absorption_group2_per_m",
                "fission_group1_per_m",
                "fission_group2_per_m",
                "nu_fission_group1_per_m",
                "nu_fission_group2_per_m",
                "downscatter_group1_to2_per_m",
                "chi_group1",
                "energy_per_fission_j");
            var coefficientModel = new CoefficientsModel(
                ReadDouble(coefficients["absorption_group1_per_m"], rowPath + ".coefficients.absorption_group1_per_m"),
                ReadDouble(coefficients["absorption_group2_per_m"], rowPath + ".coefficients.absorption_group2_per_m"),
                ReadDouble(coefficients["fission_group1_per_m"], rowPath + ".coefficients.fission_group1_per_m"),
                ReadDouble(coefficients["fission_group2_per_m"], rowPath + ".coefficients.fission_group2_per_m"),
                ReadDouble(coefficients["nu_fission_group1_per_m"], rowPath + ".coefficients.nu_fission_group1_per_m"),
                ReadDouble(coefficients["nu_fission_group2_per_m"], rowPath + ".coefficients.nu_fission_group2_per_m"),
                ReadDouble(coefficients["downscatter_group1_to2_per_m"], rowPath + ".coefficients.downscatter_group1_to2_per_m"),
                ReadDouble(coefficients["chi_group1"], rowPath + ".coefficients.chi_group1"),
                ReadDouble(coefficients["energy_per_fission_j"], rowPath + ".coefficients.energy_per_fission_j"));
            ValidateCoefficients(ToCoefficients(coefficientModel), rowPath + ".coefficients");
            rows.Add(new RowModel(burnup, coefficientModel));
            previousBurnup = burnup;
            rowIndex++;
        }

        if (rows.Count == 0)
        {
            throw new ReproductionFailure("Input.Rows.Empty", path, "At least one coefficient row is required.");
        }

        return rows;
    }

    private static void ValidateCoefficients(Coefficients coefficients, string path)
    {
        double[] values =
        {
            coefficients.AbsorptionGroup1PerM,
            coefficients.AbsorptionGroup2PerM,
            coefficients.FissionGroup1PerM,
            coefficients.FissionGroup2PerM,
            coefficients.NuFissionGroup1PerM,
            coefficients.NuFissionGroup2PerM,
            coefficients.DownscatterGroup1To2PerM,
            coefficients.ChiGroup1,
            coefficients.ChiGroup2,
            coefficients.EnergyPerFissionJ
        };
        if (values.Any(value => !IsFinite(value)))
        {
            throw new ReproductionFailure("Input.Coefficients.NonFinite", path, "All coefficient values must be finite.");
        }

        if (coefficients.AbsorptionGroup1PerM < 0.0 ||
            coefficients.AbsorptionGroup2PerM < 0.0 ||
            coefficients.FissionGroup1PerM < 0.0 ||
            coefficients.FissionGroup2PerM < 0.0 ||
            coefficients.NuFissionGroup1PerM < 0.0 ||
            coefficients.NuFissionGroup2PerM < 0.0 ||
            coefficients.DownscatterGroup1To2PerM < 0.0 ||
            coefficients.ChiGroup1 < 0.0 ||
            coefficients.ChiGroup1 > 1.0 ||
            coefficients.ChiGroup2 < 0.0 ||
            coefficients.ChiGroup2 > 1.0 ||
            coefficients.EnergyPerFissionJ <= 0.0 ||
            coefficients.AbsorptionGroup1PerM < coefficients.FissionGroup1PerM ||
            coefficients.AbsorptionGroup2PerM < coefficients.FissionGroup2PerM)
        {
            throw new ReproductionFailure("Input.Coefficients.Invalid", path, "Coefficient signs or support do not satisfy the frozen contract.");
        }

        if ((coefficients.FissionGroup1PerM == 0.0) != (coefficients.NuFissionGroup1PerM == 0.0) ||
            (coefficients.FissionGroup2PerM == 0.0) != (coefficients.NuFissionGroup2PerM == 0.0))
        {
            throw new ReproductionFailure("Input.Coefficients.FissionSupportMismatch", path, "Fission and nu-fission support must match.");
        }
    }

    private static void ValidateOutputDocument(byte[] bytes, ReproductionDocument expected)
    {
        using JsonDocument document = ParseJson(bytes, "reproduction artifact");
        Dictionary<string, JsonElement> root = ReadObject(
            document.RootElement,
            "reproduction",
            "format",
            "task_id",
            "status",
            "evidence_class",
            "artifact_availability",
            "evidence_approval",
            "validation_domain",
            "generator_id",
            "generator_version",
            "model_contract",
            "independence_claim",
            "comparison_status",
            "gate_status",
            "source_artifact_id",
            "pack_artifact_id",
            "transform_id",
            "data_version",
            "units_profile_id",
            "source_provenance",
            "source_sha256",
            "pack_sha256",
            "reduced_manifest_sha256",
            "benchmark_manifest_sha256",
            "topology_fixture_id",
            "benchmark_scenario_id",
            "unit_and_ordering_boundary",
            "runtime_boundary",
            "cases",
            "records");
        RequireString(root, "format", "reproduction.format", OutputFormat);
        RequireString(root, "task_id", "reproduction.task_id", TaskId);
        RequireString(root, "status", "reproduction.status", CandidateStatus);
        RequireString(root, "evidence_class", "reproduction.evidence_class", SyntheticEvidence);
        RequireString(root, "evidence_approval", "reproduction.evidence_approval", CandidateApproval);
        RequireString(root, "comparison_status", "reproduction.comparison_status", "Deferred");
        RequireIntCount(root["cases"], "reproduction.cases", expected.Cases.Count);
        RequireIntCount(root["records"], "reproduction.records", expected.Records.Count);
    }

    private static string ComputeInputDigest(CaseDefinition definition, InputBundle input)
    {
        var writer = new CanonicalWriter();
        writer.WriteUtf8("P4-T06-G4D-input-v1");
        writer.WriteUtf8(definition.CaseId);
        writer.WriteUInt32((uint)definition.Burnups.Length);
        foreach (double burnup in definition.Burnups)
        {
            writer.WriteFloat64(burnup);
        }

        writer.WriteDigest(input.PackSha256);
        writer.WriteDigest(input.BenchmarkSha256);
        writer.WriteUtf8(BenchmarkScenarioId);
        return Hex(Sha256(writer.ToArray()));
    }

    private static string ComputeCoefficientIdentity(PackTableModel table, List<LookupModel> lookups)
    {
        var writer = new CanonicalWriter();
        writer.WriteUtf8("P4-T06-G4D-coefficient-identity-v1");
        WriteTableIdentity(writer, table);
        writer.WriteUInt32((uint)(lookups.Count * 2));
        foreach (LookupModel lookup in lookups)
        {
            for (int groupIndex = 1; groupIndex <= 2; groupIndex++)
            {
                writer.WriteUInt32((uint)lookup.NodeIndex);
                writer.WriteUInt16((ushort)groupIndex);
                writer.WriteFloat64(lookup.InputBurnupJPerKgHm);
                writer.WriteUInt32((uint)lookup.BracketLowerIndex);
                writer.WriteUInt32((uint)lookup.BracketUpperIndex);
                writer.WriteFloat64(lookup.InterpolationFraction);
            }
        }

        return Hex(Sha256(writer.ToArray()));
    }

    private static string ComputeLookupIdentity(PackTableModel table, LookupModel lookup, int groupIndex)
    {
        var writer = new CanonicalWriter();
        writer.WriteUtf8("P4-T06-G4D-lookup-identity-v1");
        WriteTableIdentity(writer, table);
        writer.WriteUInt32((uint)lookup.NodeIndex);
        writer.WriteUInt16((ushort)groupIndex);
        writer.WriteFloat64(lookup.InputBurnupJPerKgHm);
        writer.WriteUInt32((uint)lookup.BracketLowerIndex);
        writer.WriteUInt32((uint)lookup.BracketUpperIndex);
        writer.WriteFloat64(lookup.InterpolationFraction);
        writer.WriteUInt8(0);
        return Hex(Sha256(writer.ToArray()));
    }

    private static void WriteTableIdentity(CanonicalWriter writer, PackTableModel table)
    {
        writer.WriteUtf8(table.TableId);
        writer.WriteUtf8(table.MaterialVariantId);
        writer.WriteUtf8(table.UnitsProfileId);
        writer.WriteUtf8(table.DataVersion);
        writer.WriteUtf8(table.SourceProvenance);
        writer.WriteDigest(table.Checksum);
    }

    private static string DeriveRunId(
        string caseId,
        string runLabel,
        string packSha256,
        string snapshotDigest)
    {
        var writer = new CanonicalWriter();
        writer.WriteUtf8("P4-T06-G4D-run-v1");
        writer.WriteUtf8(caseId);
        writer.WriteUtf8(runLabel);
        writer.WriteDigest(packSha256);
        writer.WriteDigest(snapshotDigest);
        return DeriveUuidV8(writer.ToArray());
    }

    private static RepeatBinding CreateRepeatBinding(CaseEvaluation evaluation)
    {
        var writer = new CanonicalWriter();
        writer.WriteUtf8("P4-T06-G4D-repeat-v1");
        writer.WriteOpaqueId(evaluation.RunId);
        writer.WriteOpaqueId(evaluation.RepeatRunId);
        writer.WriteDigest(evaluation.SnapshotDigest);
        writer.WriteDigest(evaluation.RepeatSnapshotDigest);
        writer.WriteBool(true);
        byte[] evidenceBytes = writer.ToArray();
        return new RepeatBinding(
            evaluation.RunId,
            evaluation.RepeatRunId,
            evaluation.SnapshotDigest,
            evaluation.RepeatSnapshotDigest,
            true,
            Hex(Sha256(evidenceBytes)),
            Hex(evidenceBytes));
    }

    private static JsonDocument ParseJson(byte[] bytes, string kind)
    {
        try
        {
            return JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64
            });
        }
        catch (JsonException exception)
        {
            throw new ReproductionFailure("Json.Invalid", kind, exception.Message);
        }
    }

    private static Dictionary<string, JsonElement> ReadObject(
        JsonElement element,
        string path,
        params string[] allowedNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ReproductionFailure("Json.Object.Required", path, "A JSON object was required.");
        }

        var allowed = new HashSet<string>(allowedNames, StringComparer.Ordinal);
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                throw new ReproductionFailure("Json.Field.Unknown", path + "." + property.Name, "Unknown fields are rejected.");
            }

            if (!values.TryAdd(property.Name, property.Value))
            {
                throw new ReproductionFailure("Json.Field.Duplicate", path + "." + property.Name, "Duplicate fields are rejected.");
            }
        }

        foreach (string name in allowedNames)
        {
            if (!values.ContainsKey(name))
            {
                throw new ReproductionFailure("Json.Field.Missing", path + "." + name, "A required field is missing.");
            }
        }

        return values;
    }

    private static JsonElement.ArrayEnumerator ReadArray(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new ReproductionFailure("Json.Array.Required", path, "A JSON array was required.");
        }

        return element.EnumerateArray();
    }

    private static string ReadString(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new ReproductionFailure("Value.String.Required", path, "A JSON string was required.");
        }

        string value = element.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ReproductionFailure("Value.String.Empty", path, "A non-empty string was required.");
        }

        return value;
    }

    private static string ReadPathFreeString(JsonElement element, string path)
    {
        string value = ReadString(element, path);
        if (value.Contains('/') || value.Contains('\\') || value.Contains("://", StringComparison.Ordinal) || value.Contains("..", StringComparison.Ordinal))
        {
            throw new ReproductionFailure("Provenance.Path.Invalid", path, "Stable provenance may not contain a host path or URL.");
        }

        return value;
    }

    private static double ReadDouble(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetDouble(out double value) || !IsFinite(value) || IsNegativeZero(value))
        {
            throw new ReproductionFailure("Value.Double.Invalid", path, "A finite JSON double was required.");
        }

        return value;
    }

    private static int ReadInt(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int value))
        {
            throw new ReproductionFailure("Value.Integer.Invalid", path, "A JSON Int32 was required.");
        }

        return value;
    }

    private static double[] ReadDoubleArray(JsonElement element, string path, int expectedLength)
    {
        double[] values = ReadArray(element, path).Select((value, index) => ReadDouble(value, path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]")).ToArray();
        if (values.Length != expectedLength)
        {
            throw new ReproductionFailure("Value.Array.Length", path, "The array length does not match the frozen fixture.");
        }

        return values;
    }

    private static string ReadDigest(JsonElement element, string path)
    {
        string value = ReadString(element, path).ToLowerInvariant();
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ReproductionFailure("Digest.Invalid", path, "A 32-byte hexadecimal digest was required.");
        }

        return value;
    }

    private static void RequireString(Dictionary<string, JsonElement> values, string name, string path, string expected)
    {
        RequireEqual(ReadString(values[name], path), expected, path);
    }

    private static void RequireDouble(JsonElement element, string path, double expected)
    {
        double actual = ReadDouble(element, path);
        if (!DoubleBitsEqual(actual, expected))
        {
            throw new ReproductionFailure("Value.Unexpected", path, "The frozen fixture value changed.");
        }
    }

    private static void RequireInt(JsonElement element, string path, int expected)
    {
        int actual = ReadInt(element, path);
        if (actual != expected)
        {
            throw new ReproductionFailure("Value.Unexpected", path, "The frozen fixture integer changed.");
        }
    }

    private static void RequireNull(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Null)
        {
            throw new ReproductionFailure("Value.Null.Required", path, "The frozen fixture requires null.");
        }
    }

    private static void RequireGuid(string value, string path)
    {
        if (!Guid.TryParse(value, out Guid parsed) || parsed == Guid.Empty)
        {
            throw new ReproductionFailure("StableId.Invalid", path, "A non-empty canonical stable identifier was required.");
        }
    }

    private static void RequireIntCount(JsonElement element, string path, int expected)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() != expected)
        {
            throw new ReproductionFailure("Output.Count.Invalid", path, "The output count does not match the bounded contract.");
        }
    }

    private static void RequireEqual(string actual, string expected, string path)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new ReproductionFailure("Value.Unexpected", path, "Expected '" + expected + "'.");
        }
    }

    private static string RequireExpectedHash(byte[] bytes, string expected, string path)
    {
        string actual = Hex(Sha256(bytes));
        RequireEqual(actual, expected, path + ".sha256");
        return actual;
    }

    private static void EnsureOutputDoesNotOverwriteInput(
        string sourcePath,
        string packPath,
        string reducedManifestPath,
        string benchmarkPath,
        string outputPath,
        string outputManifestPath)
    {
        string[] inputs = { sourcePath, packPath, reducedManifestPath, benchmarkPath };
        string[] outputs = { outputPath, outputManifestPath };
        foreach (string output in outputs)
        {
            string outputFullPath = Path.GetFullPath(output);
            foreach (string input in inputs)
            {
                if (string.Equals(outputFullPath, Path.GetFullPath(input), StringComparison.OrdinalIgnoreCase))
                {
                    throw new ReproductionFailure("Output.OverwritesInput", output, "Output may not overwrite an input artifact.");
                }
            }
        }
    }

    private static byte[] ReadFile(string path, string label)
    {
        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception exception)
        {
            throw new ReproductionFailure("Input.ReadFailed", label, exception.Message);
        }
    }

    private static void WriteFile(string path, byte[] bytes)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ReproductionFailure("Output.Path.Invalid", path, "Output must have a valid directory.");
        }

        Directory.CreateDirectory(directory);
        File.WriteAllBytes(fullPath, bytes);
    }

    private static void RequireFlux(double value, string label, int nodeIndex)
    {
        if (!IsFinite(value) || value < 0.0)
        {
            throw new ReproductionFailure("State.Flux.Invalid", label + "[" + nodeIndex.ToString(CultureInfo.InvariantCulture) + "]", "Flux must be finite and nonnegative.");
        }
    }

    private static void RequireNonnegativeFinite(double value, string label, int nodeIndex)
    {
        if (!IsFinite(value) || value < 0.0)
        {
            throw new ReproductionFailure("State.NonnegativeFinite.Invalid", label + "[" + nodeIndex.ToString(CultureInfo.InvariantCulture) + "]", "The value must be finite and nonnegative.");
        }
    }

    private static void RequirePositiveFinite(double value, string path)
    {
        if (!IsFinite(value) || value <= 0.0)
        {
            throw new ReproductionFailure("State.PositiveFinite.Invalid", path, "The value must be finite and strictly positive.");
        }
    }

    private static void RequireFinite(double value, string path)
    {
        if (!IsFinite(value))
        {
            throw new ReproductionFailure("State.NonFinite", path, "The value became non-finite.");
        }
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static bool IsNegativeZero(double value)
    {
        return BitConverter.DoubleToInt64Bits(value) == long.MinValue;
    }

    private static bool DoubleBitsEqual(double left, double right)
    {
        return BitConverter.DoubleToInt64Bits(left) == BitConverter.DoubleToInt64Bits(right);
    }

    private static string FormatDouble(double value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static byte[] Sha256(byte[] bytes)
    {
        return SHA256.HashData(bytes);
    }

    private static string Hex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string Hex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static List<NodeKeyBinding> CreateNodeKeys()
    {
        var keys = new List<NodeKeyBinding>(ExpectedNodeCount);
        for (ushort position = 0; position < ExpectedNodeCount; position++)
        {
            keys.Add(new NodeKeyBinding(0, position));
        }

        return keys;
    }

    private static List<NodeGroupKeyBinding> CreateNodeGroupKeys()
    {
        var keys = new List<NodeGroupKeyBinding>(ExpectedNodeCount * 2);
        for (ushort position = 0; position < ExpectedNodeCount; position++)
        {
            keys.Add(new NodeGroupKeyBinding(0, position, 1));
            keys.Add(new NodeGroupKeyBinding(0, position, 2));
        }

        return keys;
    }

    private static byte[] BuildScopeCanonicalBytes(ScopeBinding scope)
    {
        var writer = new CanonicalWriter();
        writer.WriteUInt8((byte)ScopeKindRankFor(scope.Kind));
        switch (scope.Key)
        {
            case GlobalScopeKeyBinding global:
                writer.WriteUInt32((uint)global.NodeSet.Count);
                foreach (NodeKeyBinding node in global.NodeSet)
                {
                    WriteNodeKey(writer, node);
                }

                break;
            case EntityScopeKeyBinding entity:
                writer.WriteUInt8(3);
                WriteNodeKey(writer, entity.Node);
                break;
            case VectorScopeKeyBinding vector:
                writer.WriteUInt8(1);
                writer.WriteUInt32((uint)vector.NodeGroups.Count);
                foreach (NodeGroupKeyBinding nodeGroup in vector.NodeGroups)
                {
                    WriteNodeGroupKey(writer, nodeGroup);
                }

                break;
            case SolveScopeKeyBinding solve:
                writer.WriteOpaqueId(solve.SolveId);
                WriteOptionalUInt64(writer, solve.SpatialStateVersion);
                WriteOptionalUInt16(writer, solve.GroupIndex);
                break;
            case LookupScopeKeyBinding lookup:
                writer.WriteUInt8(1);
                WriteNodeGroupKey(writer, lookup.NodeGroup);
                writer.WriteUtf8(lookup.TableId);
                writer.WriteUInt32(lookup.Bracket.LowerIndex);
                writer.WriteUInt32(lookup.Bracket.UpperIndex);
                writer.WriteFloat64(lookup.Bracket.Alpha);
                writer.WriteUInt8(0);
                break;
            case RunPairScopeKeyBinding runPair:
                writer.WriteOpaqueId(runPair.RunIdA);
                writer.WriteOpaqueId(runPair.RunIdB);
                break;
            default:
                throw new ReproductionFailure(
                    "Scope.Key.Unsupported",
                    scope.Kind,
                    "The scope key is not a closed P2-T05 typed key.");
        }

        return writer.ToArray();
    }

    private static void WriteNodeKey(CanonicalWriter writer, NodeKeyBinding node)
    {
        writer.WriteUInt32(node.ChannelId);
        writer.WriteUInt16(node.BundlePosition);
    }

    private static void WriteNodeGroupKey(CanonicalWriter writer, NodeGroupKeyBinding nodeGroup)
    {
        writer.WriteUInt32(nodeGroup.ChannelId);
        writer.WriteUInt16(nodeGroup.BundlePosition);
        writer.WriteUInt16(nodeGroup.GroupIndex);
    }

    private static byte[] BuildObservableIdentityBytes(
        string validationDomain,
        string quantityId,
        ScopeBinding scope,
        SimulationTime simulationTime,
        StateBinding stateBinding,
        string componentKey)
    {
        var writer = new CanonicalWriter();
        writer.WriteUtf8("CANDU-OBSERVABLE-V1");
        writer.WriteUInt32(1);
        writer.WriteUInt8((byte)ValidationDomainRankFor(validationDomain));
        writer.WriteUtf8(quantityId);
        writer.WriteBytes(BuildScopeCanonicalBytes(scope));
        writer.WriteFloat64(simulationTime.Seconds);
        writer.WriteBytes(BuildStateCanonicalBytes(stateBinding));
        if (!string.Equals(componentKey, "not_applicable", StringComparison.Ordinal))
        {
            throw new ReproductionFailure(
                "Component.Key.Unsupported",
                componentKey,
                "G4D emits only the explicit P2-T05 NotApplicable component key.");
        }

        writer.WriteNotApplicable();
        writer.WriteNotApplicable();
        return writer.ToArray();
    }

    private static byte[] BuildStateCanonicalBytes(StateBinding stateBinding)
    {
        var writer = new CanonicalWriter();
        writer.WriteUtf8("CANDU-STATE-BINDING-V1");
        WriteOptionalUInt64(writer, stateBinding.CoreStateVersion);
        WriteOptionalUInt64(writer, stateBinding.SpatialStateVersion);
        WriteOptionalOpaqueId(writer, stateBinding.SpatialSolveId);
        WriteOptionalOpaqueId(writer, stateBinding.PowerSnapshotId);
        WriteOptionalUInt64(writer, stateBinding.PowerSnapshotVersion);
        WriteOptionalUInt64(writer, stateBinding.KineticStepIndex);
        WriteOptionalUInt64(writer, stateBinding.NuclideStateVersion);
        WriteOptionalUtf8(writer, stateBinding.TopologyVersion);
        WriteOptionalUtf8(writer, stateBinding.DataPackVersion);
        WriteOptionalDigest(writer, stateBinding.CoefficientDigest);
        WriteOptionalDigest(writer, stateBinding.SnapshotDigest);
        return writer.ToArray();
    }

    private static void WriteOptionalUInt64(CanonicalWriter writer, string value)
    {
        if (string.Equals(value, "NotApplicable", StringComparison.Ordinal))
        {
            writer.WriteNotApplicable();
            return;
        }

        if (!ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ulong parsed))
        {
            throw new ReproductionFailure("State.Version.Invalid", value, "A canonical UInt64 or NotApplicable value was required.");
        }

        writer.WriteUInt64(parsed);
    }

    private static void WriteOptionalUInt16(CanonicalWriter writer, string value)
    {
        if (string.Equals(value, "NotApplicable", StringComparison.Ordinal))
        {
            writer.WriteNotApplicable();
            return;
        }

        if (!ushort.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out ushort parsed))
        {
            throw new ReproductionFailure("State.Group.Invalid", value, "A canonical UInt16 or NotApplicable value was required.");
        }

        writer.WriteUInt16(parsed);
    }

    private static void WriteOptionalOpaqueId(CanonicalWriter writer, string value)
    {
        if (string.Equals(value, "NotApplicable", StringComparison.Ordinal))
        {
            writer.WriteNotApplicable();
            return;
        }

        writer.WriteOpaqueId(value);
    }

    private static void WriteOptionalUtf8(CanonicalWriter writer, string value)
    {
        if (string.Equals(value, "NotApplicable", StringComparison.Ordinal))
        {
            writer.WriteNotApplicable();
            return;
        }

        writer.WriteUtf8(value);
    }

    private static void WriteOptionalDigest(CanonicalWriter writer, string value)
    {
        if (string.Equals(value, "NotApplicable", StringComparison.Ordinal))
        {
            writer.WriteNotApplicable();
            return;
        }

        writer.WriteDigest(value);
    }

    private static int ValidationDomainRankFor(string validationDomain)
    {
        return validationDomain switch
        {
            "Parser" => 0,
            "Runtime" => 1,
            "Provenance" => 2,
            _ => throw new ReproductionFailure("Order.ValidationDomain.Unknown", validationDomain, "The validation domain is not in the P2-T05 rank table.")
        };
    }

    private static int ScopeKindRankFor(string scopeKind)
    {
        return scopeKind switch
        {
            "Global" => 0,
            "Entity" => 1,
            "Vector" => 2,
            "SourceMap" => 3,
            "Event" => 4,
            "Interval" => 5,
            "Solve" => 6,
            "SolvePair" => 7,
            "Snapshot" => 8,
            "Run" => 9,
            "RunPair" => 10,
            "Fixture" => 11,
            "Failure" => 12,
            "Parser" => 13,
            "Provenance" => 14,
            "Lookup" => 15,
            "TimeSeries" => 16,
            _ => throw new ReproductionFailure("Order.ScopeKind.Unknown", scopeKind, "The scope kind is not in the P2-T05 rank table.")
        };
    }

    private static string DeriveUuidV8(byte[] identityBytes)
    {
        byte[] bytes = Sha256(identityBytes);
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return string.Concat(
            Hex(bytes.AsSpan(0, 4)), "-",
            Hex(bytes.AsSpan(4, 2)), "-",
            Hex(bytes.AsSpan(6, 2)), "-",
            Hex(bytes.AsSpan(8, 2)), "-",
            Hex(bytes.AsSpan(10, 6)));
    }

    private static byte[] HexToBytes(string value)
    {
        string compact = value.Replace("-", string.Empty, StringComparison.Ordinal);
        var bytes = new byte[compact.Length / 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = Convert.ToByte(compact.Substring(index * 2, 2), 16);
        }

        return bytes;
    }

    private sealed class CanonicalWriter
    {
        private readonly List<byte> _buffer = new List<byte>();

        public void WriteUInt8(byte value) => WriteTyped(0x01, new[] { value });
        public void WriteUInt16(ushort value) => WriteTyped(0x02, GetBytes(value));
        public void WriteUInt32(uint value) => WriteTyped(0x03, GetBytes(value));
        public void WriteUInt64(ulong value) => WriteTyped(0x04, GetBytes(value));

        public void WriteFloat64(double value)
        {
            if (!IsFinite(value) || IsNegativeZero(value))
            {
                throw new ReproductionFailure("Canonical.Float.Invalid", "canonical", "Canonical Float64 values must be finite and non-negative-zero.");
            }

            WriteTyped(0x07, GetBytes(value));
        }

        public void WriteBool(bool value) => WriteTyped(0x08, new[] { value ? (byte)1 : (byte)0 });

        public void WriteOpaqueId(string value)
        {
            string compact = value.Replace("-", string.Empty, StringComparison.Ordinal);
            if (compact.Length != 32 || compact.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new ReproductionFailure("Canonical.OpaqueId.Invalid", value, "Canonical opaque identifier bytes were required.");
            }

            WriteBytes(HexToBytes(compact));
        }

        public void WriteUtf8(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ReproductionFailure("Canonical.String.Invalid", "canonical", "Canonical UTF-8 values must be non-empty.");
            }

            WriteTyped(0x0A, Encoding.UTF8.GetBytes(value));
        }

        public void WriteBytes(byte[] value) => WriteTyped(0x0B, value);

        public void WriteDigest(string value)
        {
            if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new ReproductionFailure("Canonical.Digest.Invalid", value, "Canonical SHA-256 bytes were required.");
            }

            WriteBytes(HexToBytes(value));
        }

        public void WriteNotApplicable() => WriteTyped(0x10, Array.Empty<byte>());
        public byte[] ToArray() => _buffer.ToArray();

        private void WriteTyped(byte typeTag, byte[] payload)
        {
            _buffer.Add(typeTag);
            _buffer.AddRange(GetBytes((uint)payload.Length));
            _buffer.AddRange(payload);
        }

        private static byte[] GetBytes(ushort value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            return BitConverter.IsLittleEndian ? bytes : bytes.Reverse().ToArray();
        }

        private static byte[] GetBytes(uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            return BitConverter.IsLittleEndian ? bytes : bytes.Reverse().ToArray();
        }

        private static byte[] GetBytes(ulong value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            return BitConverter.IsLittleEndian ? bytes : bytes.Reverse().ToArray();
        }

        private static byte[] GetBytes(double value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            return BitConverter.IsLittleEndian ? bytes : bytes.Reverse().ToArray();
        }
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: generate|validate <source> <pack> <reduced-manifest> <benchmark> <output> <output-manifest>");
    }

    private sealed class ReproductionFailure : Exception
    {
        public ReproductionFailure(string code, string path, string message)
            : base(message)
        {
            Code = code;
            Path = path;
        }

        public string Code { get; }

        public string Path { get; }
    }

    private sealed class InputBundle
    {
        public InputBundle(
            SourceModel source,
            PackModel pack,
            ReducedManifestModel reducedManifest,
            BenchmarkModel benchmark,
            SpatialFixture fixture,
            string sourceSha256,
            string packSha256,
            string reducedManifestSha256,
            string benchmarkSha256)
        {
            Source = source;
            Pack = pack;
            ReducedManifest = reducedManifest;
            Benchmark = benchmark;
            Fixture = fixture;
            SourceSha256 = sourceSha256;
            PackSha256 = packSha256;
            ReducedManifestSha256 = reducedManifestSha256;
            BenchmarkSha256 = benchmarkSha256;
        }

        public SourceModel Source { get; }
        public PackModel Pack { get; }
        public ReducedManifestModel ReducedManifest { get; }
        public BenchmarkModel Benchmark { get; }
        public SpatialFixture Fixture { get; }
        public string SourceSha256 { get; }
        public string PackSha256 { get; }
        public string ReducedManifestSha256 { get; }
        public string BenchmarkSha256 { get; }
    }

    private sealed class SpatialFixture
    {
        public SpatialFixture(List<FixtureNode> nodes, List<FixtureEdge> edges, List<FixtureBoundary> boundaries)
        {
            Nodes = nodes;
            Edges = edges;
            Boundaries = boundaries;
        }

        public List<FixtureNode> Nodes { get; }
        public List<FixtureEdge> Edges { get; }
        public List<FixtureBoundary> Boundaries { get; }
    }

    private sealed class FixtureNode
    {
        public FixtureNode(int channelId, int bundlePosition, FixtureEdge[] neighbors, FixtureBoundary[] boundaries)
        {
            ChannelId = channelId;
            BundlePosition = bundlePosition;
            NodeIndex = bundlePosition;
            Neighbors = neighbors;
            Boundaries = boundaries;
        }

        public int ChannelId { get; }
        public int BundlePosition { get; }
        public int NodeIndex { get; }
        public FixtureEdge[] Neighbors { get; }
        public FixtureBoundary[] Boundaries { get; }
    }

    private sealed class FixtureEdge
    {
        public FixtureEdge(int sourceNodeIndex, int targetNodeIndex, string direction, double group1ConductanceM2, double group2ConductanceM2)
        {
            SourceNodeIndex = sourceNodeIndex;
            TargetNodeIndex = targetNodeIndex;
            Direction = direction;
            Group1ConductanceM2 = group1ConductanceM2;
            Group2ConductanceM2 = group2ConductanceM2;
        }

        public int SourceNodeIndex { get; }
        public int TargetNodeIndex { get; }
        public string Direction { get; }
        public double Group1ConductanceM2 { get; }
        public double Group2ConductanceM2 { get; }
    }

    private sealed class FixtureBoundary
    {
        public FixtureBoundary(int nodeIndex, string face, string classification, double group1ConductanceM2, double group2ConductanceM2)
        {
            NodeIndex = nodeIndex;
            Face = face;
            Classification = classification;
            Group1ConductanceM2 = group1ConductanceM2;
            Group2ConductanceM2 = group2ConductanceM2;
        }

        public int NodeIndex { get; }
        public string Face { get; }
        public string Classification { get; }
        public double Group1ConductanceM2 { get; }
        public double Group2ConductanceM2 { get; }
    }

    private sealed class SourceModel
    {
        public SourceModel(string sourceArtifactId, string sourceProvenance, string transformId, string dataVersion, string unitsProfileId, List<SourceTableModel> tables)
        {
            SourceArtifactId = sourceArtifactId;
            SourceProvenance = sourceProvenance;
            TransformId = transformId;
            DataVersion = dataVersion;
            UnitsProfileId = unitsProfileId;
            Tables = tables;
        }

        public string SourceArtifactId { get; }
        public string SourceProvenance { get; }
        public string TransformId { get; }
        public string DataVersion { get; }
        public string UnitsProfileId { get; }
        public List<SourceTableModel> Tables { get; }
    }

    private sealed class SourceTableModel
    {
        public SourceTableModel(string tableId, string materialVariantId, List<RowModel> rows)
        {
            TableId = tableId;
            MaterialVariantId = materialVariantId;
            Rows = rows;
        }

        public string TableId { get; }
        public string MaterialVariantId { get; }
        public List<RowModel> Rows { get; }
    }

    private sealed class PackModel
    {
        public PackModel(string packArtifactId, string dataVersion, string unitsProfileId, string sourceArtifactId, string sourceProvenance, string transformId, List<PackTableModel> tables)
        {
            PackArtifactId = packArtifactId;
            DataVersion = dataVersion;
            UnitsProfileId = unitsProfileId;
            SourceArtifactId = sourceArtifactId;
            SourceProvenance = sourceProvenance;
            TransformId = transformId;
            Tables = tables;
        }

        public string PackArtifactId { get; }
        public string DataVersion { get; }
        public string UnitsProfileId { get; }
        public string SourceArtifactId { get; }
        public string SourceProvenance { get; }
        public string TransformId { get; }
        public List<PackTableModel> Tables { get; }
    }

    private sealed class PackTableModel
    {
        public PackTableModel(string tableId, string dataVersion, string materialVariantId, string unitsProfileId, string sourceProvenance, string checksum, List<RowModel> rows)
        {
            TableId = tableId;
            DataVersion = dataVersion;
            MaterialVariantId = materialVariantId;
            UnitsProfileId = unitsProfileId;
            SourceProvenance = sourceProvenance;
            Checksum = checksum;
            Rows = rows;
        }

        public string TableId { get; }
        public string DataVersion { get; }
        public string MaterialVariantId { get; }
        public string UnitsProfileId { get; }
        public string SourceProvenance { get; }
        public string Checksum { get; }
        public List<RowModel> Rows { get; }
    }

    private sealed class ReducedManifestModel
    {
        public ReducedManifestModel(string approvalStatus, string evidenceClass, string packArtifactId, string packSha256, string sourceArtifactId, string sourceSha256, string sourceProvenance, string transformId, string dataVersion, string unitsProfileId, List<ManifestTableModel> tables)
        {
            ApprovalStatus = approvalStatus;
            EvidenceClass = evidenceClass;
            PackArtifactId = packArtifactId;
            PackSha256 = packSha256;
            SourceArtifactId = sourceArtifactId;
            SourceSha256 = sourceSha256;
            SourceProvenance = sourceProvenance;
            TransformId = transformId;
            DataVersion = dataVersion;
            UnitsProfileId = unitsProfileId;
            Tables = tables;
        }

        public string ApprovalStatus { get; }
        public string EvidenceClass { get; }
        public string PackArtifactId { get; }
        public string PackSha256 { get; }
        public string SourceArtifactId { get; }
        public string SourceSha256 { get; }
        public string SourceProvenance { get; }
        public string TransformId { get; }
        public string DataVersion { get; }
        public string UnitsProfileId { get; }
        public List<ManifestTableModel> Tables { get; }
    }

    private sealed class ManifestTableModel
    {
        public ManifestTableModel(string tableId, string materialVariantId, string checksum, int rowCount, double burnupMinJPerKgHm, double burnupMaxJPerKgHm)
        {
            TableId = tableId;
            MaterialVariantId = materialVariantId;
            Checksum = checksum;
            RowCount = rowCount;
            BurnupMinJPerKgHm = burnupMinJPerKgHm;
            BurnupMaxJPerKgHm = burnupMaxJPerKgHm;
        }

        public string TableId { get; }
        public string MaterialVariantId { get; }
        public string Checksum { get; }
        public int RowCount { get; }
        public double BurnupMinJPerKgHm { get; }
        public double BurnupMaxJPerKgHm { get; }
    }

    private sealed class RowModel
    {
        public RowModel(double burnupJPerKgHm, CoefficientsModel coefficients)
        {
            BurnupJPerKgHm = burnupJPerKgHm;
            Coefficients = coefficients;
        }

        public double BurnupJPerKgHm { get; }
        public CoefficientsModel Coefficients { get; }
    }

    private sealed class CoefficientsModel
    {
        public CoefficientsModel(double absorptionGroup1PerM, double absorptionGroup2PerM, double fissionGroup1PerM, double fissionGroup2PerM, double nuFissionGroup1PerM, double nuFissionGroup2PerM, double downscatterGroup1To2PerM, double chiGroup1, double energyPerFissionJ)
        {
            AbsorptionGroup1PerM = absorptionGroup1PerM;
            AbsorptionGroup2PerM = absorptionGroup2PerM;
            FissionGroup1PerM = fissionGroup1PerM;
            FissionGroup2PerM = fissionGroup2PerM;
            NuFissionGroup1PerM = nuFissionGroup1PerM;
            NuFissionGroup2PerM = nuFissionGroup2PerM;
            DownscatterGroup1To2PerM = downscatterGroup1To2PerM;
            ChiGroup1 = chiGroup1;
            EnergyPerFissionJ = energyPerFissionJ;
        }

        public double AbsorptionGroup1PerM { get; }
        public double AbsorptionGroup2PerM { get; }
        public double FissionGroup1PerM { get; }
        public double FissionGroup2PerM { get; }
        public double NuFissionGroup1PerM { get; }
        public double NuFissionGroup2PerM { get; }
        public double DownscatterGroup1To2PerM { get; }
        public double ChiGroup1 { get; }
        public double EnergyPerFissionJ { get; }
    }

    private sealed class BenchmarkModel
    {
        public BenchmarkModel(string scenarioId, double interiorEdgeConductanceGroup1M2, double interiorEdgeConductanceGroup2M2, double nodeVolumeM3, double[] initialGroup1, double[] initialGroup2, double targetPowerW, double initialEigenvalue, double innerAbsoluteTolerance, double innerRelativeTolerance, int maximumInnerIterations, double kAbsoluteTolerance, double kRelativeTolerance, double residualTolerance, double sourceShapeTolerance, double powerBalanceTolerance, int maximumOuterIterations)
        {
            ScenarioId = scenarioId;
            InteriorEdgeConductanceGroup1M2 = interiorEdgeConductanceGroup1M2;
            InteriorEdgeConductanceGroup2M2 = interiorEdgeConductanceGroup2M2;
            NodeVolumeM3 = nodeVolumeM3;
            InitialGroup1 = initialGroup1;
            InitialGroup2 = initialGroup2;
            TargetPowerW = targetPowerW;
            InitialEigenvalue = initialEigenvalue;
            InnerAbsoluteTolerance = innerAbsoluteTolerance;
            InnerRelativeTolerance = innerRelativeTolerance;
            MaximumInnerIterations = maximumInnerIterations;
            KAbsoluteTolerance = kAbsoluteTolerance;
            KRelativeTolerance = kRelativeTolerance;
            ResidualTolerance = residualTolerance;
            SourceShapeTolerance = sourceShapeTolerance;
            PowerBalanceTolerance = powerBalanceTolerance;
            MaximumOuterIterations = maximumOuterIterations;
        }

        public string ScenarioId { get; }
        public double InteriorEdgeConductanceGroup1M2 { get; }
        public double InteriorEdgeConductanceGroup2M2 { get; }
        public double NodeVolumeM3 { get; }
        public double[] InitialGroup1 { get; }
        public double[] InitialGroup2 { get; }
        public double TargetPowerW { get; }
        public double InitialEigenvalue { get; }
        public double InnerAbsoluteTolerance { get; }
        public double InnerRelativeTolerance { get; }
        public int MaximumInnerIterations { get; }
        public double KAbsoluteTolerance { get; }
        public double KRelativeTolerance { get; }
        public double ResidualTolerance { get; }
        public double SourceShapeTolerance { get; }
        public double PowerBalanceTolerance { get; }
        public int MaximumOuterIterations { get; }

        public static BenchmarkModel ForResidual(double group1Conductance, double group2Conductance)
        {
            return new BenchmarkModel(
                BenchmarkScenarioId,
                group1Conductance,
                group2Conductance,
                1.0,
                ResidualUnitFluxGroup1,
                ResidualUnitFluxGroup2,
                1.0,
                1.0,
                1e-14,
                1e-14,
                512,
                1e-6,
                1e-6,
                1e-6,
                1e-6,
                1e-6,
                2);
        }
    }

    private sealed class CaseDefinition
    {
        public CaseDefinition(string caseId, string scenarioClass, double[] burnups, string description)
        {
            CaseId = caseId;
            ScenarioClass = scenarioClass;
            Burnups = burnups;
            Description = description;
        }

        public string CaseId { get; }
        public string ScenarioClass { get; }
        public double[] Burnups { get; }
        public string Description { get; }
    }

    private sealed class Coefficients
    {
        public Coefficients(double absorptionGroup1PerM, double absorptionGroup2PerM, double fissionGroup1PerM, double fissionGroup2PerM, double nuFissionGroup1PerM, double nuFissionGroup2PerM, double downscatterGroup1To2PerM, double chiGroup1, double energyPerFissionJ)
        {
            AbsorptionGroup1PerM = absorptionGroup1PerM;
            AbsorptionGroup2PerM = absorptionGroup2PerM;
            FissionGroup1PerM = fissionGroup1PerM;
            FissionGroup2PerM = fissionGroup2PerM;
            NuFissionGroup1PerM = nuFissionGroup1PerM;
            NuFissionGroup2PerM = nuFissionGroup2PerM;
            DownscatterGroup1To2PerM = downscatterGroup1To2PerM;
            ChiGroup1 = chiGroup1;
            EnergyPerFissionJ = energyPerFissionJ;
        }

        public double AbsorptionGroup1PerM { get; }
        public double AbsorptionGroup2PerM { get; }
        public double FissionGroup1PerM { get; }
        public double FissionGroup2PerM { get; }
        public double NuFissionGroup1PerM { get; }
        public double NuFissionGroup2PerM { get; }
        public double DownscatterGroup1To2PerM { get; }
        public double ChiGroup1 { get; }
        public double ChiGroup2 { get { return 1.0 - ChiGroup1; } }
        public double EnergyPerFissionJ { get; }
    }

    private sealed class Material
    {
        public Material(double volumeM3, Coefficients coefficients)
        {
            VolumeM3 = volumeM3;
            Coefficients = coefficients;
        }

        public double VolumeM3 { get; }
        public Coefficients Coefficients { get; }
    }

    private sealed class LookupModel
    {
        public LookupModel(int nodeIndex, double inputBurnupJPerKgHm, int bracketLowerIndex, int bracketUpperIndex, double interpolationFraction, string tableId, string materialVariantId, string unitsProfileId, string tableChecksum, Coefficients coefficients)
        {
            NodeIndex = nodeIndex;
            InputBurnupJPerKgHm = inputBurnupJPerKgHm;
            BracketLowerIndex = bracketLowerIndex;
            BracketUpperIndex = bracketUpperIndex;
            InterpolationFraction = interpolationFraction;
            TableId = tableId;
            MaterialVariantId = materialVariantId;
            UnitsProfileId = unitsProfileId;
            TableChecksum = tableChecksum;
            Coefficients = coefficients;
        }

        public int NodeIndex { get; }
        public double InputBurnupJPerKgHm { get; }
        public int BracketLowerIndex { get; }
        public int BracketUpperIndex { get; }
        public double InterpolationFraction { get; }
        public string TableId { get; }
        public string MaterialVariantId { get; }
        public string UnitsProfileId { get; }
        public string TableChecksum { get; }
        public Coefficients Coefficients { get; }
    }

    private sealed class LinearSolveResult
    {
        public LinearSolveResult(double[] solution, int iterationCount, double absoluteResidual, double relativeResidual)
        {
            Solution = solution;
            IterationCount = iterationCount;
            AbsoluteResidual = absoluteResidual;
            RelativeResidual = relativeResidual;
        }

        public double[] Solution { get; }
        public int IterationCount { get; }
        public double AbsoluteResidual { get; }
        public double RelativeResidual { get; }
    }

    private sealed class ResidualMetrics
    {
        public ResidualMetrics(double absoluteInfinity, double relativeInfinity)
        {
            AbsoluteInfinity = absoluteInfinity;
            RelativeInfinity = relativeInfinity;
        }

        public double AbsoluteInfinity { get; }
        public double RelativeInfinity { get; }
    }

    private sealed class SolveStepDiagnostics
    {
        public SolveStepDiagnostics(string status, string convergenceReason, int iterationCount, double eigenvalueChangeAbsolute, double eigenvalueChangeRelative, double residualAbsoluteInfinity, double residualRelativeInfinity, double sourceShapeChangeInfinity, double powerBalanceRelative, string innerSolveStatus, int innerIterationCountGroup1, int innerIterationCountGroup2, int innerIterationCountTotal, int failedInnerSolveCount, int invalidCoefficientCount, int negativeFluxCount, int nonFiniteValueCount, int clampCount, int forbiddenClampCount)
        {
            Status = status;
            ConvergenceReason = convergenceReason;
            IterationCount = iterationCount;
            EigenvalueChangeAbsolute = eigenvalueChangeAbsolute;
            EigenvalueChangeRelative = eigenvalueChangeRelative;
            ResidualAbsoluteInfinity = residualAbsoluteInfinity;
            ResidualRelativeInfinity = residualRelativeInfinity;
            SourceShapeChangeInfinity = sourceShapeChangeInfinity;
            PowerBalanceRelative = powerBalanceRelative;
            InnerSolveStatus = innerSolveStatus;
            InnerIterationCountGroup1 = innerIterationCountGroup1;
            InnerIterationCountGroup2 = innerIterationCountGroup2;
            InnerIterationCountTotal = innerIterationCountTotal;
            FailedInnerSolveCount = failedInnerSolveCount;
            InvalidCoefficientCount = invalidCoefficientCount;
            NegativeFluxCount = negativeFluxCount;
            NonFiniteValueCount = nonFiniteValueCount;
            ClampCount = clampCount;
            ForbiddenClampCount = forbiddenClampCount;
        }

        public string Status { get; }
        public string ConvergenceReason { get; }
        public int IterationCount { get; }
        public double EigenvalueChangeAbsolute { get; }
        public double EigenvalueChangeRelative { get; }
        public double ResidualAbsoluteInfinity { get; }
        public double ResidualRelativeInfinity { get; }
        public double SourceShapeChangeInfinity { get; }
        public double PowerBalanceRelative { get; }
        public string InnerSolveStatus { get; }
        public int InnerIterationCountGroup1 { get; }
        public int InnerIterationCountGroup2 { get; }
        public int InnerIterationCountTotal { get; }
        public int FailedInnerSolveCount { get; }
        public int InvalidCoefficientCount { get; }
        public int NegativeFluxCount { get; }
        public int NonFiniteValueCount { get; }
        public int ClampCount { get; }
        public int ForbiddenClampCount { get; }
    }

    private sealed class SolveResult
    {
        public SolveResult(Snapshot snapshot)
        {
            Snapshot = snapshot;
        }

        public Snapshot Snapshot { get; }
    }

    private sealed class Snapshot
    {
        public Snapshot(double targetPowerW, double initialPowerBeforeNormalizationW, double initialNormalizationScale, double finalTrialPowerBeforeNormalizationW, double eigenvalue, double normalizationScale, double totalPowerW, double fissionProductionRate, double[] fluxByNodeGroup, double[] nodePowerW, double[] fissionSourceRateDensity, SolveStepDiagnostics diagnostics)
        {
            TargetPowerW = targetPowerW;
            InitialPowerBeforeNormalizationW = initialPowerBeforeNormalizationW;
            InitialNormalizationScale = initialNormalizationScale;
            FinalTrialPowerBeforeNormalizationW = finalTrialPowerBeforeNormalizationW;
            Eigenvalue = eigenvalue;
            NormalizationScale = normalizationScale;
            TotalPowerW = totalPowerW;
            FissionProductionRate = fissionProductionRate;
            FluxByNodeGroup = fluxByNodeGroup;
            NodePowerW = nodePowerW;
            FissionSourceRateDensity = fissionSourceRateDensity;
            Diagnostics = diagnostics;
        }

        public double TargetPowerW { get; }
        public double InitialPowerBeforeNormalizationW { get; }
        public double InitialNormalizationScale { get; }
        public double FinalTrialPowerBeforeNormalizationW { get; }
        public double Eigenvalue { get; }
        public double NormalizationScale { get; }
        public double TotalPowerW { get; }
        public double FissionProductionRate { get; }
        public double[] FluxByNodeGroup { get; }
        public double[] NodePowerW { get; }
        public double[] FissionSourceRateDensity { get; }
        public SolveStepDiagnostics Diagnostics { get; }
    }

    private sealed class CaseEvaluation
    {
        public CaseEvaluation(CaseDefinition definition, CaseOutput output, List<LookupModel> lookups, string inputDigest, string coefficientIdentity, string snapshotDigest, string runId, string repeatRunId, string repeatSnapshotDigest)
        {
            Definition = definition;
            Output = output;
            Lookups = lookups;
            InputDigest = inputDigest;
            CoefficientIdentity = coefficientIdentity;
            SnapshotDigest = snapshotDigest;
            RunId = runId;
            RepeatRunId = repeatRunId;
            RepeatSnapshotDigest = repeatSnapshotDigest;
        }

        public CaseDefinition Definition { get; }
        public CaseOutput Output { get; }
        public List<LookupModel> Lookups { get; }
        public string InputDigest { get; }
        public string CoefficientIdentity { get; }
        public string SnapshotDigest { get; }
        public string RunId { get; }
        public string RepeatRunId { get; }
        public string RepeatSnapshotDigest { get; }
    }

    private sealed class ReproductionDocument
    {
        public ReproductionDocument(string format, string taskId, string status, string evidenceClass, string artifactAvailability, string evidenceApproval, string validationDomain, string generatorId, string generatorVersion, string modelContract, string independenceClaim, string comparisonStatus, string gateStatus, string sourceArtifactId, string packArtifactId, string transformId, string dataVersion, string unitsProfileId, string sourceProvenance, string sourceSha256, string packSha256, string reducedManifestSha256, string benchmarkManifestSha256, string topologyFixtureId, string benchmarkScenarioId, string unitAndOrderingBoundary, string runtimeBoundary, List<CaseOutput> cases, List<EvidenceRecord> records)
        {
            Format = format;
            TaskId = taskId;
            Status = status;
            EvidenceClass = evidenceClass;
            ArtifactAvailability = artifactAvailability;
            EvidenceApproval = evidenceApproval;
            ValidationDomain = validationDomain;
            GeneratorId = generatorId;
            GeneratorVersion = generatorVersion;
            ModelContract = modelContract;
            IndependenceClaim = independenceClaim;
            ComparisonStatus = comparisonStatus;
            GateStatus = gateStatus;
            SourceArtifactId = sourceArtifactId;
            PackArtifactId = packArtifactId;
            TransformId = transformId;
            DataVersion = dataVersion;
            UnitsProfileId = unitsProfileId;
            SourceProvenance = sourceProvenance;
            SourceSha256 = sourceSha256;
            PackSha256 = packSha256;
            ReducedManifestSha256 = reducedManifestSha256;
            BenchmarkManifestSha256 = benchmarkManifestSha256;
            TopologyFixtureId = topologyFixtureId;
            BenchmarkScenarioId = benchmarkScenarioId;
            UnitAndOrderingBoundary = unitAndOrderingBoundary;
            RuntimeBoundary = runtimeBoundary;
            Cases = cases;
            Records = records;
        }

        public string Format { get; }
        public string TaskId { get; }
        public string Status { get; }
        public string EvidenceClass { get; }
        public string ArtifactAvailability { get; }
        public string EvidenceApproval { get; }
        public string ValidationDomain { get; }
        public string GeneratorId { get; }
        public string GeneratorVersion { get; }
        public string ModelContract { get; }
        public string IndependenceClaim { get; }
        public string ComparisonStatus { get; }
        public string GateStatus { get; }
        public string SourceArtifactId { get; }
        public string PackArtifactId { get; }
        public string TransformId { get; }
        public string DataVersion { get; }
        public string UnitsProfileId { get; }
        public string SourceProvenance { get; }
        public string SourceSha256 { get; }
        public string PackSha256 { get; }
        public string ReducedManifestSha256 { get; }
        public string BenchmarkManifestSha256 { get; }
        public string TopologyFixtureId { get; }
        public string BenchmarkScenarioId { get; }
        public string UnitAndOrderingBoundary { get; }
        public string RuntimeBoundary { get; }
        public List<CaseOutput> Cases { get; }
        public List<EvidenceRecord> Records { get; }
    }

    private sealed class CaseOutput
    {
        public CaseOutput(string caseId, string scenarioClass, string status, double[] burnupJPerKgHmByNode, string description, string inputDigest, string coefficientIdentity, string snapshotDigest, bool repeatEqual, List<LookupOutput> lookups, Snapshot snapshot)
        {
            CaseId = caseId;
            ScenarioClass = scenarioClass;
            Status = status;
            BurnupJPerKgHmByNode = burnupJPerKgHmByNode;
            Description = description;
            InputDigest = inputDigest;
            CoefficientIdentity = coefficientIdentity;
            SnapshotDigest = snapshotDigest;
            RepeatEqual = repeatEqual;
            Lookups = lookups;
            Snapshot = snapshot;
        }

        public string CaseId { get; }
        public string ScenarioClass { get; }
        public string Status { get; }
        public double[] BurnupJPerKgHmByNode { get; }
        public string Description { get; }
        public string InputDigest { get; }
        public string CoefficientIdentity { get; }
        public string SnapshotDigest { get; }
        public bool RepeatEqual { get; }
        public List<LookupOutput> Lookups { get; }
        public Snapshot Snapshot { get; }
    }

    private sealed class LookupOutput
    {
        public LookupOutput(int nodeIndex, double inputBurnupJPerKgHm, int bracketLowerIndex, int bracketUpperIndex, double interpolationFraction, string tableId, string materialVariantId, string unitsProfileId, string tableChecksum, CoefficientOutput coefficients)
        {
            NodeIndex = nodeIndex;
            InputBurnupJPerKgHm = inputBurnupJPerKgHm;
            BracketLowerIndex = bracketLowerIndex;
            BracketUpperIndex = bracketUpperIndex;
            InterpolationFraction = interpolationFraction;
            TableId = tableId;
            MaterialVariantId = materialVariantId;
            UnitsProfileId = unitsProfileId;
            TableChecksum = tableChecksum;
            Coefficients = coefficients;
        }

        public int NodeIndex { get; }
        public double InputBurnupJPerKgHm { get; }
        public int BracketLowerIndex { get; }
        public int BracketUpperIndex { get; }
        public double InterpolationFraction { get; }
        public string TableId { get; }
        public string MaterialVariantId { get; }
        public string UnitsProfileId { get; }
        public string TableChecksum { get; }
        public CoefficientOutput Coefficients { get; }
    }

    private sealed class CoefficientOutput
    {
        public CoefficientOutput(Coefficients coefficients)
        {
            AbsorptionGroup1PerM = coefficients.AbsorptionGroup1PerM;
            AbsorptionGroup2PerM = coefficients.AbsorptionGroup2PerM;
            FissionGroup1PerM = coefficients.FissionGroup1PerM;
            FissionGroup2PerM = coefficients.FissionGroup2PerM;
            NuFissionGroup1PerM = coefficients.NuFissionGroup1PerM;
            NuFissionGroup2PerM = coefficients.NuFissionGroup2PerM;
            DownscatterGroup1To2PerM = coefficients.DownscatterGroup1To2PerM;
            ChiGroup1 = coefficients.ChiGroup1;
            ChiGroup2 = coefficients.ChiGroup2;
            EnergyPerFissionJ = coefficients.EnergyPerFissionJ;
        }

        public double AbsorptionGroup1PerM { get; }
        public double AbsorptionGroup2PerM { get; }
        public double FissionGroup1PerM { get; }
        public double FissionGroup2PerM { get; }
        public double NuFissionGroup1PerM { get; }
        public double NuFissionGroup2PerM { get; }
        public double DownscatterGroup1To2PerM { get; }
        public double ChiGroup1 { get; }
        public double ChiGroup2 { get; }
        public double EnergyPerFissionJ { get; }
    }

    private sealed class ReproductionManifest
    {
        public ReproductionManifest(string format, int manifestSchemaVersion, string manifestArtifactId, string artifactId, string artifactSha256, string approvalStatus, string evidenceClass, string evidenceApproval, string validationDomain, string generatorId, string generatorVersion, string modelContract, string sourceSha256, string packSha256, string reducedManifestSha256, string benchmarkManifestSha256, string sourceArtifactId, string packArtifactId, string topologyFixtureId, string benchmarkScenarioId, string unitsProfileId, int caseCount, int recordCount, string comparisonStatus, string pathBoundary)
        {
            Format = format;
            ManifestSchemaVersion = manifestSchemaVersion;
            ManifestArtifactId = manifestArtifactId;
            ArtifactId = artifactId;
            ArtifactSha256 = artifactSha256;
            ApprovalStatus = approvalStatus;
            EvidenceClass = evidenceClass;
            EvidenceApproval = evidenceApproval;
            ValidationDomain = validationDomain;
            GeneratorId = generatorId;
            GeneratorVersion = generatorVersion;
            ModelContract = modelContract;
            SourceSha256 = sourceSha256;
            PackSha256 = packSha256;
            ReducedManifestSha256 = reducedManifestSha256;
            BenchmarkManifestSha256 = benchmarkManifestSha256;
            SourceArtifactId = sourceArtifactId;
            PackArtifactId = packArtifactId;
            TopologyFixtureId = topologyFixtureId;
            BenchmarkScenarioId = benchmarkScenarioId;
            UnitsProfileId = unitsProfileId;
            CaseCount = caseCount;
            RecordCount = recordCount;
            ComparisonStatus = comparisonStatus;
            PathBoundary = pathBoundary;
        }

        public string Format { get; }
        public int ManifestSchemaVersion { get; }
        public string ManifestArtifactId { get; }
        public string ArtifactId { get; }
        public string ArtifactSha256 { get; }
        public string ApprovalStatus { get; }
        public string EvidenceClass { get; }
        public string EvidenceApproval { get; }
        public string ValidationDomain { get; }
        public string GeneratorId { get; }
        public string GeneratorVersion { get; }
        public string ModelContract { get; }
        public string SourceSha256 { get; }
        public string PackSha256 { get; }
        public string ReducedManifestSha256 { get; }
        public string BenchmarkManifestSha256 { get; }
        public string SourceArtifactId { get; }
        public string PackArtifactId { get; }
        public string TopologyFixtureId { get; }
        public string BenchmarkScenarioId { get; }
        public string UnitsProfileId { get; }
        public int CaseCount { get; }
        public int RecordCount { get; }
        public string ComparisonStatus { get; }
        public string PathBoundary { get; }
    }

    private sealed class EvidenceRecord
    {
        public EvidenceRecord(
            string observableId,
            string quantityId,
            ScopeBinding scope,
            SimulationTime simulationTime,
            StateBinding stateBinding,
            string inputDigest,
            RecordValue value,
            string comparisonRuleId,
            string toleranceProfileId,
            string referenceId,
            string coverageClass,
            string artifactAvailability,
            string evidenceApproval,
            string validationDomain,
            string status,
            string evidencePath,
            RepeatBinding? repeatBinding,
            OrderBinding orderKey)
        {
            ObservableId = observableId;
            QuantityId = quantityId;
            Scope = scope;
            SimulationTime = simulationTime;
            StateBinding = stateBinding;
            InputDigest = inputDigest;
            Value = value;
            ComparisonRuleId = comparisonRuleId;
            ToleranceProfileId = toleranceProfileId;
            ReferenceId = referenceId;
            CoverageClass = coverageClass;
            ArtifactAvailability = artifactAvailability;
            EvidenceApproval = evidenceApproval;
            ValidationDomain = validationDomain;
            Status = status;
            EvidencePath = evidencePath;
            RepeatBinding = repeatBinding;
            OrderKey = orderKey;
        }

        public string ObservableId { get; }
        public string QuantityId { get; }
        public ScopeBinding Scope { get; }
        public SimulationTime SimulationTime { get; }
        public StateBinding StateBinding { get; }
        public string InputDigest { get; }
        public RecordValue Value { get; }
        public string ComparisonRuleId { get; }
        public string ToleranceProfileId { get; }
        public string ReferenceId { get; }
        public string CoverageClass { get; }
        public string ArtifactAvailability { get; }
        public string EvidenceApproval { get; }
        public string ValidationDomain { get; }
        public string Status { get; }
        public string EvidencePath { get; }
        public RepeatBinding? RepeatBinding { get; }
        public OrderBinding OrderKey { get; }
    }

    private sealed class ScopeBinding
    {
        public ScopeBinding(string kind, object key)
        {
            Kind = kind;
            Key = key;
        }

        public string Kind { get; }
        public object Key { get; }
    }

    private sealed class NodeKeyBinding
    {
        public NodeKeyBinding(uint channelId, ushort bundlePosition)
        {
            ChannelId = channelId;
            BundlePosition = bundlePosition;
        }

        public uint ChannelId { get; }
        public ushort BundlePosition { get; }
    }

    private sealed class GlobalScopeKeyBinding
    {
        public GlobalScopeKeyBinding(List<NodeKeyBinding> nodeSet)
        {
            KeyKind = "NodeSet";
            NodeSet = nodeSet;
        }

        public string KeyKind { get; }
        public List<NodeKeyBinding> NodeSet { get; }
    }

    private sealed class EntityScopeKeyBinding
    {
        public EntityScopeKeyBinding(NodeKeyBinding node)
        {
            EntityKind = "Node";
            Node = node;
        }

        public string EntityKind { get; }
        public NodeKeyBinding Node { get; }
    }

    private sealed class VectorScopeKeyBinding
    {
        public VectorScopeKeyBinding(List<NodeGroupKeyBinding> nodeGroups)
        {
            VectorKind = "NodeGroup";
            NodeGroups = nodeGroups;
        }

        public string VectorKind { get; }
        public List<NodeGroupKeyBinding> NodeGroups { get; }
    }

    private sealed class SolveScopeKeyBinding
    {
        public SolveScopeKeyBinding(string solveId, string spatialStateVersion)
        {
            SolveId = solveId;
            SpatialStateVersion = spatialStateVersion;
            GroupIndex = "NotApplicable";
        }

        public string SolveId { get; }
        public string SpatialStateVersion { get; }
        public string GroupIndex { get; }
    }

    private sealed class LookupScopeKeyBinding
    {
        public LookupScopeKeyBinding(NodeGroupKeyBinding nodeGroup, string tableId, double inputBurnupJPerKgHm, LookupBracketBinding bracket)
        {
            OwnerKind = "NodeGroup";
            NodeGroup = nodeGroup;
            TableId = tableId;
            InputBurnupJPerKgHm = inputBurnupJPerKgHm;
            Bracket = bracket;
        }

        public string OwnerKind { get; }
        public NodeGroupKeyBinding NodeGroup { get; }
        public string TableId { get; }
        public double InputBurnupJPerKgHm { get; }
        public LookupBracketBinding Bracket { get; }
    }

    private sealed class NodeGroupKeyBinding
    {
        public NodeGroupKeyBinding(uint channelId, ushort bundlePosition, ushort groupIndex)
        {
            ChannelId = channelId;
            BundlePosition = bundlePosition;
            GroupIndex = groupIndex;
        }

        public uint ChannelId { get; }
        public ushort BundlePosition { get; }
        public ushort GroupIndex { get; }
    }

    private sealed class LookupBracketBinding
    {
        public LookupBracketBinding(uint lowerIndex, uint upperIndex, double alpha)
        {
            LowerIndex = lowerIndex;
            UpperIndex = upperIndex;
            Alpha = alpha;
            ResultStatus = "InRange";
        }

        public uint LowerIndex { get; }
        public uint UpperIndex { get; }
        public double Alpha { get; }
        public string ResultStatus { get; }
    }

    private sealed class RunPairScopeKeyBinding
    {
        public RunPairScopeKeyBinding(string runIdA, string runIdB)
        {
            RunIdA = runIdA;
            RunIdB = runIdB;
        }

        public string RunIdA { get; }
        public string RunIdB { get; }
    }

    private sealed class SimulationTime
    {
        public SimulationTime(string status, double seconds)
        {
            Status = status;
            Seconds = seconds;
        }

        public string Status { get; }
        public double Seconds { get; }
    }

    private sealed class StateBinding
    {
        public StateBinding(
            string bindingKind,
            string topologyFixtureId,
            string dataPackArtifactId,
            string inputDigest,
            string coefficientIdentity,
            string snapshotDigest,
            string benchmarkManifestSha256,
            string caseId)
        {
            BindingKind = bindingKind;
            TopologyFixtureId = topologyFixtureId;
            DataPackArtifactId = dataPackArtifactId;
            InputDigest = inputDigest;
            BenchmarkManifestSha256 = benchmarkManifestSha256;
            CoreStateVersion = "0";
            SpatialStateVersion = "0";
            SpatialSolveId = DeriveStateId("SpatialSolve", caseId, inputDigest, snapshotDigest);
            PowerSnapshotId = DeriveStateId("PowerSnapshot", caseId, inputDigest, snapshotDigest);
            PowerSnapshotVersion = "0";
            KineticStepIndex = "NotApplicable";
            NuclideStateVersion = "NotApplicable";
            TopologyVersion = topologyFixtureId;
            DataPackVersion = dataPackArtifactId;
            CoefficientDigest = coefficientIdentity;
            SnapshotDigest = snapshotDigest;
        }

        public string BindingKind { get; }
        public string TopologyFixtureId { get; }
        public string DataPackArtifactId { get; }
        public string InputDigest { get; }
        public string BenchmarkManifestSha256 { get; }
        public string CoreStateVersion { get; }
        public string SpatialStateVersion { get; }
        public string SpatialSolveId { get; }
        public string PowerSnapshotId { get; }
        public string PowerSnapshotVersion { get; }
        public string KineticStepIndex { get; }
        public string NuclideStateVersion { get; }
        public string TopologyVersion { get; }
        public string DataPackVersion { get; }
        public string CoefficientDigest { get; }
        public string SnapshotDigest { get; }
    }

    private static string DeriveStateId(string stateKind, string caseId, string inputDigest, string snapshotDigest)
    {
        var writer = new CanonicalWriter();
        writer.WriteUtf8("CANDU-STATE-ID-V1");
        writer.WriteUtf8(stateKind);
        writer.WriteUtf8(caseId);
        writer.WriteDigest(inputDigest);
        writer.WriteDigest(snapshotDigest);
        return DeriveUuidV8(writer.ToArray());
    }

    private sealed class RecordValue
    {
        public RecordValue(string status, string kind, string unit, string payloadSchemaId, object componentOrderSpec, object payload)
        {
            Status = status;
            Kind = kind;
            Unit = unit;
            PayloadSchemaId = payloadSchemaId;
            ComponentOrderSpec = componentOrderSpec;
            Payload = payload;
        }

        public string Status { get; }
        public string Kind { get; }
        public string Unit { get; }
        public string PayloadSchemaId { get; }
        public object ComponentOrderSpec { get; }
        public object Payload { get; }
    }

    private sealed class ComponentOrderSpecBinding
    {
        public ComponentOrderSpecBinding(int orderKindOrdinal, string componentKeySchemaId, int comparatorOrdinal, string tieBreakSchemaId)
        {
            OrderKindOrdinal = orderKindOrdinal;
            ComponentKeySchemaId = componentKeySchemaId;
            ComparatorOrdinal = comparatorOrdinal;
            TieBreakSchemaId = tieBreakSchemaId;
        }

        public int OrderKindOrdinal { get; }
        public string ComponentKeySchemaId { get; }
        public int ComparatorOrdinal { get; }
        public string TieBreakSchemaId { get; }
    }

    private sealed class VectorComponent
    {
        public VectorComponent(NodeGroupKeyBinding componentKey, double value)
        {
            ComponentKey = componentKey;
            Value = value;
        }

        public NodeGroupKeyBinding ComponentKey { get; }
        public double Value { get; }
    }

    private sealed class OrderBinding
    {
        public OrderBinding(string validationDomain, ScopeBinding scope, string quantityId, string componentKey, string observableId)
        {
            ValidationDomain = validationDomain;
            ValidationDomainRank = ValidationDomainRankFor(validationDomain);
            SimulationTimeSeconds = 0.0;
            CoreStateVersion = 0UL;
            EventRankOrNotApplicable = "NotApplicable";
            EventRankSort = 0;
            SequenceOrNotApplicable = "NotApplicable";
            SequenceSort = 0UL;
            EventIdOrNotApplicable = "NotApplicable";
            ScopeKind = scope.Kind;
            ScopeKindRank = ScopeKindRankFor(scope.Kind);
            ScopeKey = scope.Key;
            QuantityId = quantityId;
            ComponentKey = componentKey;
            ObservableId = observableId;
        }

        public string ValidationDomain { get; }
        public int ValidationDomainRank { get; }
        public double SimulationTimeSeconds { get; }
        public ulong CoreStateVersion { get; }
        public string EventRankOrNotApplicable { get; }
        public int EventRankSort { get; }
        public string SequenceOrNotApplicable { get; }
        public ulong SequenceSort { get; }
        public string EventIdOrNotApplicable { get; }
        public string ScopeKind { get; }
        public int ScopeKindRank { get; }
        public object ScopeKey { get; }
        public string QuantityId { get; }
        public string ComponentKey { get; }
        public string ObservableId { get; }
    }

    private sealed class RepeatBinding
    {
        public RepeatBinding(string leftRunId, string rightRunId, string leftSnapshotDigest, string rightSnapshotDigest, bool exactBitwiseEqual, string evidenceDigest, string evidenceBytesHex)
        {
            LeftRunId = leftRunId;
            RightRunId = rightRunId;
            LeftSnapshotDigest = leftSnapshotDigest;
            RightSnapshotDigest = rightSnapshotDigest;
            ExactBitwiseEqual = exactBitwiseEqual;
            EvidenceDigest = evidenceDigest;
            EvidenceBytesHex = evidenceBytesHex;
        }

        public string LeftRunId { get; }
        public string RightRunId { get; }
        public string LeftSnapshotDigest { get; }
        public string RightSnapshotDigest { get; }
        public bool ExactBitwiseEqual { get; }
        public string EvidenceDigest { get; }
        public string EvidenceBytesHex { get; }
    }
}
