using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReactorSim.Core;

internal static class Program
{
    private const string SourceFormat = "reactorsim.reduced-interpolation-source/v1";
    private const string PackFormat = "reactorsim.reduced-interpolation-pack/v1";
    private const string ManifestFormat = "reactorsim.reduced-interpolation-manifest/v1";
    private const string OutputFormat = "reactorsim.candidate-spatial-comparison/v1";
    private const string CandidatePackArtifactId = "p4-t06-r4-reduced-candidate-v1";
    private const string SourceArtifactId = "p4-t06-r4-synthetic-input-v1";
    private const string TransformId = "identity_projection_v1";
    private const string SyntheticEvidence = "synthetic";
    private const string CandidateApproval = "candidate";
    private const string RuntimeUse = "candidate_interpolation_input";
    private const string BenchmarkScenarioId = "p4-t05-homogeneous-three-node-static-solve-v1";
    private const string BenchmarkManifestPath = "benchmarks/P4-T08-static-solver-benchmark.json";
    private const string EvidencePath = "data/comparisons/p4-t06-r5-candidate-snapshots-v1.json";
    private const int ExpectedNodeCount = 3;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly double[] FreshBurnups = { 0.0, 0.0, 0.0 };
    private static readonly double[] EquilibriumLikeBurnups = { 50000.0, 50000.0, 50000.0 };
    private static readonly double[] RefuelledPerturbationBurnups = { 0.0, 50000.0, 100000.0 };
    private static readonly double[] MidcycleInterpolationBurnups = { 25000.0, 25000.0, 25000.0 };

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return 1;
            }

            switch (args[0].ToLowerInvariant())
            {
                case "generate":
                    RequireArgumentCount(args, 5, "generate <source> <pack> <manifest> <output>");
                    Generate(args[1], args[2], args[3], args[4]);
                    return 0;
                case "validate":
                    RequireArgumentCount(args, 5, "validate <source> <pack> <manifest> <output>");
                    Validate(args[1], args[2], args[3], args[4]);
                    return 0;
                default:
                    PrintUsage();
                    return 1;
            }
        }
        catch (ComparisonFailure failure)
        {
            Console.Error.WriteLine(
                "P4_T06_R5_FAILURE code=" + failure.Code +
                " path=" + failure.Path +
                " message=" + failure.Message);
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "P4_T06_R5_FAILURE code=Unhandled.Exception path=tool message=" + exception.Message);
            return 3;
        }
    }

    private static void Generate(
        string sourcePath,
        string packPath,
        string manifestPath,
        string outputPath)
    {
        InputBundle input = ReadInputs(sourcePath, packPath, manifestPath);
        ComparisonDocument document = BuildDocument(input);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        WriteFile(outputPath, bytes);

        int completeCases = document.Cases.Count(@case => @case.Status == "CandidateSnapshot");
        int notCoveredCases = document.Cases.Count(@case => @case.Status == "NotCovered");
        Console.WriteLine(
            "P4_T06_R5_GENERATE_PASS snapshots=" +
            completeCases.ToString(CultureInfo.InvariantCulture) +
            " not_covered=" +
            notCoveredCases.ToString(CultureInfo.InvariantCulture) +
            " records=" +
            document.Records.Count.ToString(CultureInfo.InvariantCulture) +
            " output_sha256=" + ToHex(Sha256(bytes)));
    }

    private static void Validate(
        string sourcePath,
        string packPath,
        string manifestPath,
        string outputPath)
    {
        InputBundle input = ReadInputs(sourcePath, packPath, manifestPath);
        ComparisonDocument expected = BuildDocument(input);
        byte[] expectedBytes = JsonSerializer.SerializeToUtf8Bytes(expected, JsonOptions);
        byte[] actualBytes = ReadFile(outputPath, "comparison output");
        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            throw new ComparisonFailure(
                "Output.NonDeterministic",
                "comparison",
                "The candidate comparison bytes do not match deterministic regeneration.");
        }

        ValidateOutputShape(actualBytes, expected);
        Console.WriteLine(
            "P4_T06_R5_VALIDATE_PASS snapshots=" +
            expected.Cases.Count(@case => @case.Status == "CandidateSnapshot").ToString(CultureInfo.InvariantCulture) +
            " not_covered=" +
            expected.Cases.Count(@case => @case.Status == "NotCovered").ToString(CultureInfo.InvariantCulture) +
            " records=" +
            expected.Records.Count.ToString(CultureInfo.InvariantCulture) +
            " output_sha256=" + ToHex(Sha256(actualBytes)));
    }

    private static InputBundle ReadInputs(
        string sourcePath,
        string packPath,
        string manifestPath)
    {
        byte[] sourceBytes = ReadFile(sourcePath, "source");
        byte[] packBytes = ReadFile(packPath, "pack");
        byte[] manifestBytes = ReadFile(manifestPath, "manifest");
        SourceModel source = ParseSource(sourceBytes);
        PackModel pack = ParsePack(packBytes);
        ManifestModel manifest = ParseManifest(manifestBytes);
        ValidateManifest(sourceBytes, packBytes, source, pack, manifest);
        ValidateSourceAndPack(source, pack);
        if (source.Tables.Count != 1 || pack.Tables.Count != 1)
        {
            throw new ComparisonFailure(
                "Tables.Count.Unsupported",
                "tables",
                "R5 is bounded to the one-table MAT-SYN candidate fixture; extra or missing tables are rejected.");
        }

        string sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? string.Empty;
        string? dataDirectory = Directory.GetParent(sourceDirectory)?.FullName;
        string? repositoryRoot = dataDirectory == null ? null : Directory.GetParent(dataDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            throw new ComparisonFailure(
                "Benchmark.Path.Invalid",
                BenchmarkManifestPath,
                "The fixed synthetic source path does not expose a repository root.");
        }

        string benchmarkPath = Path.Combine(
            repositoryRoot,
            BenchmarkManifestPath.Replace('/', Path.DirectorySeparatorChar));
        byte[] benchmarkBytes = ReadFile(benchmarkPath, "benchmark manifest");
        BenchmarkPolicy benchmark = ParseBenchmarkManifest(benchmarkBytes);
        return new InputBundle(
            source,
            pack,
            manifest,
            benchmark,
            ToHex(Sha256(sourceBytes)),
            ToHex(Sha256(packBytes)),
            ToHex(Sha256(manifestBytes)),
            ToHex(Sha256(benchmarkBytes)));
    }

    private static ComparisonDocument BuildDocument(InputBundle input)
    {
        PackTableModel packTable = input.Pack.Tables[0];
        SourceTableModel sourceTable = input.Source.Tables[0];
        BurnupCoefficientTableV1 packCoreTable = CreateCoreTable(packTable, "pack");
        BurnupCoefficientTableV1 sourceCoreTable = CreateCoreTable(
            new PackTableModel(
                sourceTable.TableId,
                sourceTable.DataVersion,
                sourceTable.MaterialVariantId,
                sourceTable.UnitsProfileId,
                sourceTable.SourceProvenance,
                packTable.Checksum,
                sourceTable.Rows),
            "source");

        List<CaseDefinition> definitions = new List<CaseDefinition>
        {
            new CaseDefinition(
                "fresh_candidate",
                "fresh",
                FreshBurnups,
                "Candidate synthetic fresh-knot material state; no fresh-fuel inference."),
            new CaseDefinition(
                "equilibrium_like_candidate",
                "equilibrium_like",
                EquilibriumLikeBurnups,
                "Representative mid-table knot only; not an inferred equilibrium state."),
            new CaseDefinition(
                "refuelled_perturbation_candidate",
                "refuelled_perturbation",
                RefuelledPerturbationBurnups,
                "Synthetic mixed-burnup coefficient state; not a P2-T03 refuelling transition."),
            new CaseDefinition(
                "midcycle_interpolation_candidate",
                "interpolation_midpoint",
                MidcycleInterpolationBurnups,
                "Synthetic in-domain interpolation point using the existing Core lookup contract.")
        };

        var cases = new List<CaseOutput>();
        var records = new List<ComparisonRecord>();
        foreach (CaseDefinition definition in definitions)
        {
            try
            {
                CaseEvaluation evaluation = EvaluateCase(
                    definition,
                    packTable,
                    packCoreTable,
                    sourceCoreTable,
                    input);
                cases.Add(evaluation.Output);
                records.AddRange(BuildRecords(evaluation, input));
            }
            catch (ComparisonFailure failure) when (failure.Code == "Solve.Nonconverged" || failure.Code == "Solve.Failed")
            {
                string status = failure.Code == "Solve.Nonconverged" ? "Nonconverged" : "Failed";
                cases.Add(new CaseOutput(
                    definition.CaseId,
                    definition.ScenarioClass,
                    status,
                    definition.BurnupJPerKgHmByNode,
                    definition.Description +
                    " No usable state was emitted. " + failure.Message +
                    " R5 does not change the P4-T08 policy or accept a last iterate.",
                    null));
            }
        }

        cases.Add(new CaseOutput(
            "rrs_perturbation_candidate",
            "rrs_perturbation",
            "NotCovered",
            Array.Empty<double>(),
            "No approved RRS influence map, actuator state, or RRS-to-Core overlay is admitted by R5.",
            null));
        cases.Add(new CaseOutput(
            "poison_perturbation_candidate",
            "poison_perturbation",
            "NotCovered",
            Array.Empty<double>(),
            "No approved poison map, reference concentration, or poison-to-Core overlay is admitted by R5.",
            null));

        records.Sort(CompareRecords);
        var observableIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ComparisonRecord record in records)
        {
            if (!observableIds.Add(record.ObservableId))
            {
                throw new ComparisonFailure(
                    "ObservableId.Duplicate",
                    record.ObservableId,
                    "Derived ObservableId values must be unique within the candidate document.");
            }
        }

        return new ComparisonDocument(
            OutputFormat,
            "P4-T06-R5",
            "candidate",
            SyntheticEvidence,
            "CommittedSynthetic",
            "Candidate",
            "Runtime",
            CandidatePackArtifactId,
            SourceArtifactId,
            input.Pack.TransformId,
            input.Pack.DataVersion,
            input.Pack.UnitsProfileId,
            input.Source.SourceProvenance,
            input.SourceSha256,
            input.PackSha256,
            input.ManifestSha256,
            input.BenchmarkManifestSha256,
            BenchmarkScenarioId,
            "P2-T05 spatial quantities are recorded with deferred G4 profiles; exact self-consistency is diagnostic evidence only.",
            cases,
            records);
    }

    private static CaseEvaluation EvaluateCase(
        CaseDefinition definition,
        PackTableModel packTableModel,
        BurnupCoefficientTableV1 packTable,
        BurnupCoefficientTableV1 sourceTable,
        InputBundle input)
    {
        SpatialSnapshot packSnapshot = Solve(definition, packTable, input.Benchmark, "reduced_pack_projection");
        SpatialSnapshot sourceSnapshot = Solve(definition, sourceTable, input.Benchmark, "source_contract_projection");
        bool exact = AreSnapshotsBitwiseEqual(packSnapshot, sourceSnapshot);
        if (!exact)
        {
            throw new ComparisonFailure(
                "Comparison.Mismatch",
                "cases[" + definition.CaseId + "]",
                "The reduced-pack and source-contract projections are not bitwise identical.");
        }

        SpatialSnapshot repeatSnapshot = Solve(definition, packTable, input.Benchmark, "reduced_pack_repeat");
        bool repeatEqual = AreSnapshotsBitwiseEqual(packSnapshot, repeatSnapshot);
        if (!repeatEqual)
        {
            throw new ComparisonFailure(
                "Determinism.RepeatMismatch",
                "cases[" + definition.CaseId + "]",
                "Repeated Core solves did not produce bitwise-identical candidate output.");
        }

        string repeatSnapshotDigest = ComputeSnapshotDigest(repeatSnapshot);
        string inputDigest = ToHex(Sha256(BuildInputDigestBytes(definition, input)));
        List<LookupBindingData> lookupBindings = BuildLookupBindings(
            packTableModel,
            packTable,
            definition.BurnupJPerKgHmByNode);
        string coefficientIdentity = BuildCoefficientIdentity(packTableModel, lookupBindings);
        string snapshotDigest = ComputeSnapshotDigest(packSnapshot);
        string packRunId = DeriveRunId(definition.CaseId, "reduced_pack_projection", input.PackSha256, snapshotDigest);
        string repeatRunId = DeriveRunId(definition.CaseId, "reduced_pack_repeat", input.PackSha256, repeatSnapshotDigest);
        ComparisonOutcome outcome = new ComparisonOutcome(
            "reduced_pack_projection",
            "source_contract_projection",
            exact,
            repeatEqual,
            0.0,
            0.0,
            "Diagnostic exact equality only; numeric acceptance remains deferred to G4.");
        CaseOutput output = new CaseOutput(
            definition.CaseId,
            definition.ScenarioClass,
            "CandidateSnapshot",
            definition.BurnupJPerKgHmByNode,
            definition.Description,
            new SnapshotOutput(
                inputDigest,
                coefficientIdentity,
                snapshotDigest,
                packSnapshot,
                outcome));
        return new CaseEvaluation(
            definition,
            output,
            packSnapshot,
            inputDigest,
            coefficientIdentity,
            snapshotDigest,
            repeatSnapshotDigest,
            lookupBindings,
            packRunId,
            repeatRunId);
    }

    private static List<LookupBindingData> BuildLookupBindings(
        PackTableModel packTable,
        BurnupCoefficientTableV1 coreTable,
        double[] burnups)
    {
        var bindings = new List<LookupBindingData>(ExpectedNodeCount * 2);
        for (int nodeIndex = 0; nodeIndex < burnups.Length; nodeIndex++)
        {
            BurnupCoefficientLookupResultV1 lookup = RequireValid(
                coreTable.TryLookup(burnups[nodeIndex]),
                "coefficient_identity.lookup[" + nodeIndex.ToString(CultureInfo.InvariantCulture) + "]");
            for (int groupIndex = 1; groupIndex <= 2; groupIndex++)
            {
                bindings.Add(new LookupBindingData(
                    nodeIndex,
                    groupIndex,
                    lookup.InputBurnupJPerKgHm,
                    lookup.BracketLowerIndex,
                    lookup.BracketUpperIndex,
                    lookup.InterpolationFraction,
                    packTable.TableId,
                    packTable.MaterialVariantId,
                    packTable.UnitsProfileId,
                    packTable.Checksum,
                    ComputeLookupIdentity(packTable, nodeIndex, groupIndex, lookup)));
            }
        }

        return bindings;
    }

    private static string BuildCoefficientIdentity(
        PackTableModel packTable,
        List<LookupBindingData> lookupBindings)
    {
        var writer = new CanonicalWriter();
        writer.WriteUtf8("P4-T06-R5-coefficient-identity-v1");
        WriteTableIdentity(writer, packTable);
        writer.WriteUInt32((uint)lookupBindings.Count);
        foreach (LookupBindingData lookup in lookupBindings)
        {
            writer.WriteUInt32((uint)lookup.NodeIndex);
            writer.WriteUInt16((ushort)lookup.GroupIndex);
            writer.WriteFloat64(lookup.InputBurnupJPerKgHm);
            writer.WriteUInt32((uint)lookup.BracketLowerIndex);
            writer.WriteUInt32((uint)lookup.BracketUpperIndex);
            writer.WriteFloat64(lookup.InterpolationFraction);
        }

        return ToHex(Sha256(writer.ToArray()));
    }

    private static string ComputeLookupIdentity(
        PackTableModel packTable,
        int nodeIndex,
        int groupIndex,
        BurnupCoefficientLookupResultV1 lookup)
    {
        var writer = new CanonicalWriter();
        writer.WriteUtf8("P4-T06-R5-lookup-identity-v1");
        WriteTableIdentity(writer, packTable);
        writer.WriteUInt32((uint)nodeIndex);
        writer.WriteUInt16((ushort)groupIndex);
        writer.WriteFloat64(lookup.InputBurnupJPerKgHm);
        writer.WriteUInt32((uint)lookup.BracketLowerIndex);
        writer.WriteUInt32((uint)lookup.BracketUpperIndex);
        writer.WriteFloat64(lookup.InterpolationFraction);
        writer.WriteUInt8(0);
        return ToHex(Sha256(writer.ToArray()));
    }

    private static byte[] BuildInputDigestBytes(CaseDefinition definition, InputBundle input)
    {
        var writer = new CanonicalWriter();
        writer.WriteUtf8("P4-T06-R5-input-v1");
        writer.WriteUtf8(definition.CaseId);
        writer.WriteUInt32((uint)definition.BurnupJPerKgHmByNode.Length);
        foreach (double burnup in definition.BurnupJPerKgHmByNode)
        {
            writer.WriteFloat64(burnup);
        }

        writer.WriteDigest(input.PackSha256);
        writer.WriteDigest(input.BenchmarkManifestSha256);
        writer.WriteUtf8(BenchmarkScenarioId);
        return writer.ToArray();
    }

    private static string DeriveRunId(
        string caseId,
        string runLabel,
        string packSha256,
        string snapshotDigest)
    {
        var writer = new CanonicalWriter();
        writer.WriteUtf8("P4-T06-R5-run-v1");
        writer.WriteUtf8(caseId);
        writer.WriteUtf8(runLabel);
        writer.WriteDigest(packSha256);
        writer.WriteDigest(snapshotDigest);
        return DeriveUuidV8(writer.ToArray());
    }

    private static RepeatBinding CreateRepeatBinding(CaseEvaluation evaluation)
    {
        var writer = new CanonicalWriter();
        writer.WriteUtf8("P4-T06-R5-repeat-v1");
        writer.WriteOpaqueId(evaluation.PackRunId);
        writer.WriteOpaqueId(evaluation.RepeatRunId);
        writer.WriteDigest(evaluation.SnapshotDigest);
        writer.WriteDigest(evaluation.RepeatSnapshotDigest);
        writer.WriteBool(true);
        byte[] bytes = writer.ToArray();
        return new RepeatBinding(
            evaluation.PackRunId,
            evaluation.RepeatRunId,
            evaluation.SnapshotDigest,
            evaluation.RepeatSnapshotDigest,
            true,
            ToHex(Sha256(bytes)),
            ToHex(bytes));
    }

    private static string FormatLookupScopeKey(LookupBindingData lookup)
    {
        return "table=" + lookup.TableId +
               ";node=" + lookup.NodeIndex.ToString(CultureInfo.InvariantCulture) +
               ";group=" + lookup.GroupIndex.ToString(CultureInfo.InvariantCulture) +
               ";burnup=" + FormatDouble(lookup.InputBurnupJPerKgHm) +
               ";lower=" + lookup.BracketLowerIndex.ToString(CultureInfo.InvariantCulture) +
               ";upper=" + lookup.BracketUpperIndex.ToString(CultureInfo.InvariantCulture) +
               ";alpha=" + FormatDouble(lookup.InterpolationFraction);
    }

    private static string ComputeSnapshotDigest(SpatialSnapshot snapshot)
    {
        return ToHex(Sha256(JsonSerializer.SerializeToUtf8Bytes(snapshot, JsonOptions)));
    }

    private static List<ComparisonRecord> BuildRecords(CaseEvaluation evaluation, InputBundle input)
    {
        SpatialSnapshot snapshot = evaluation.PackSnapshot;
        string caseId = evaluation.Definition.CaseId;
        string inputDigest = evaluation.InputDigest;
        string coefficientIdentity = evaluation.CoefficientIdentity;
        var records = new List<ComparisonRecord>();

        ComparisonRecord AddRecord(
            string quantityId,
            string scopeKind,
            string scopeKey,
            string componentKey,
            string valueKind,
            object payload,
            string profileId,
            RepeatBinding? repeatBinding = null)
        {
            return CreateRecord(
                caseId,
                quantityId,
                scopeKind,
                scopeKey,
                componentKey,
                valueKind,
                payload,
                profileId,
                inputDigest,
                coefficientIdentity,
                evaluation.SnapshotDigest,
                input.BenchmarkManifestSha256,
                repeatBinding);
        }

        records.Add(AddRecord(
            "spatial.k",
            "Solve",
            "solve",
            "not_applicable",
            "scalar",
            snapshot.Eigenvalue,
            "P2-T05-spatial-k-v1"));
        records.Add(AddRecord(
            "spatial.flux",
            "Vector",
            "NodeGroup",
            "not_applicable",
            "vector",
            CreateFluxComponents(snapshot.FluxByNodeGroup),
            "P2-T05-spatial-flux-v1"));
        records.Add(AddRecord(
            "spatial.total_power",
            "Global",
            "global",
            "not_applicable",
            "scalar",
            snapshot.TotalPowerW,
            "P2-T05-spatial-total-power-v1"));
        records.Add(AddRecord(
            "spatial.normalization_scale",
            "Solve",
            "solve",
            "not_applicable",
            "scalar",
            snapshot.NormalizationScale,
            "P2-T05-spatial-normalization-scale-v1"));
        records.Add(AddRecord(
            "spatial.convergence",
            "Solve",
            "solve",
            "not_applicable",
            "bytes",
            ComputeDiagnosticsIdentity(snapshot.Diagnostics),
            "P2-T05-spatial-convergence-v1"));
        records.Add(AddRecord(
            "spatial.iteration_count",
            "Solve",
            "solve",
            "not_applicable",
            "integer",
            snapshot.Diagnostics.IterationCount,
            "P2-T05-spatial-iteration-count-v1"));
        foreach (LookupBindingData lookup in evaluation.LookupBindings)
        {
            records.Add(AddRecord(
                "spatial.coefficient_identity",
                "Lookup",
                FormatLookupScopeKey(lookup),
                "not_applicable",
                "bytes",
                lookup.IdentityDigest,
                "P2-T05-spatial-coefficient-id-v1"));
        }

        for (int nodeIndex = 0; nodeIndex < snapshot.NodePowerW.Length; nodeIndex++)
        {
            string key = "channel=0;position=" + nodeIndex.ToString(CultureInfo.InvariantCulture);
            records.Add(AddRecord(
                "spatial.power",
                "Entity",
                "Node:" + key,
                "not_applicable",
                "scalar",
                snapshot.NodePowerW[nodeIndex],
                "P2-T05-spatial-power-v1"));
            records.Add(AddRecord(
                "spatial.fission_source",
                "Entity",
                "Node:" + key,
                "not_applicable",
                "scalar",
                snapshot.FissionSourceRateDensity[nodeIndex],
                "P2-T05-spatial-fission-source-v1"));
        }

        RepeatBinding repeatBinding = CreateRepeatBinding(evaluation);
        records.Add(AddRecord(
            "determinism.repeat_equal",
            "RunPair",
            evaluation.PackRunId + "|" + evaluation.RepeatRunId,
            "not_applicable",
            "bytes",
            repeatBinding.EvidenceBytesHex,
            "P2-T05-determinism-repeat-equal-v1",
            repeatBinding));
        return records;
    }

    private static ComparisonRecord CreateRecord(
        string caseId,
        string quantityId,
        string scopeKind,
        string scopeKey,
        string componentKey,
        string valueKind,
        object payload,
        string profileId,
        string inputDigest,
        string coefficientIdentity,
        string snapshotDigest,
        string benchmarkManifestSha256,
        RepeatBinding? repeatBinding)
    {
        StateBinding stateBinding = new StateBinding(
            "synthetic_fixture",
            BenchmarkScenarioId,
            CandidatePackArtifactId,
            inputDigest,
            coefficientIdentity,
            snapshotDigest,
            benchmarkManifestSha256,
            caseId);
        ScopeBinding scope = BuildScopeBinding(quantityId, scopeKind, scopeKey, stateBinding);
        string observableId = DeriveUuidV8(BuildObservableIdentityBytes(
            "Runtime",
            quantityId,
            scope,
            new SimulationTimeBinding(0.0),
            stateBinding,
            componentKey));
        string comparisonRuleId = "P2-T05-rule-" + profileId;
        return new ComparisonRecord(
            observableId,
            quantityId,
            scope,
            new SimulationTimeBinding(0.0),
            stateBinding,
            inputDigest,
            new ValueBinding(
                "available",
                valueKind,
                UnitFor(quantityId),
                PayloadSchemaFor(quantityId),
                ComponentOrderFor(quantityId),
                payload),
            comparisonRuleId,
            profileId,
            SourceArtifactId,
            "Synthetic",
            "CommittedSynthetic",
            "Candidate",
            "Runtime",
            "Deferred",
            EvidencePath,
            repeatBinding,
            new OrderBinding("Runtime", scope, quantityId, componentKey, observableId));
    }

    private static ScopeBinding BuildScopeBinding(
        string quantityId,
        string scopeKind,
        string scopeKey,
        StateBinding stateBinding)
    {
        switch (scopeKind)
        {
            case "Global":
                return new ScopeBinding(
                    scopeKind,
                    new GlobalScopeKeyBinding(CreateNodeKeys()));
            case "Vector":
                return new ScopeBinding(
                    scopeKind,
                    new VectorScopeKeyBinding(CreateNodeGroupKeys()));
            case "Entity":
                return new ScopeBinding(
                    scopeKind,
                    new EntityScopeKeyBinding(ParseNodeKey(scopeKey)));
            case "Solve":
                return new ScopeBinding(
                    scopeKind,
                    new SolveScopeKeyBinding(stateBinding.SpatialSolveId, stateBinding.SpatialStateVersion));
            case "Lookup":
                return new ScopeBinding(
                    scopeKind,
                    ParseLookupScopeKey(scopeKey));
            case "RunPair":
                return new ScopeBinding(
                    scopeKind,
                    ParseRunPairScopeKey(scopeKey));
            default:
                throw new ComparisonFailure(
                    "Scope.Kind.Unsupported",
                    quantityId,
                    "The candidate quantity has no approved typed P2-T05 scope binding.");
        }
    }

    private static int CompareRecords(ComparisonRecord left, ComparisonRecord right)
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
                throw new ComparisonFailure("Scope.Order.Invalid", left.Kind, "Scope key types do not match their closed scope kind.");
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
        if (string.Equals(left, "not_applicable", StringComparison.Ordinal) &&
            string.Equals(right, "not_applicable", StringComparison.Ordinal))
        {
            return 0;
        }

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
            throw new ComparisonFailure("Order.Version.Invalid", "order_key", "A canonical UInt64 state version was required.");
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
            throw new ComparisonFailure("Order.Group.Invalid", "order_key", "A canonical UInt16 group index was required.");
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
        return CompareBytes(HexToBytes(left.Replace("-", string.Empty, StringComparison.Ordinal)), HexToBytes(right.Replace("-", string.Empty, StringComparison.Ordinal)));
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

    private static NodeKeyBinding ParseNodeKey(string value)
    {
        if (!value.StartsWith("Node:", StringComparison.Ordinal))
        {
            throw new ComparisonFailure("Scope.Node.Invalid", value, "A canonical Node scope key was required.");
        }

        Dictionary<string, string> parts = ParseKeyParts(value.Substring("Node:".Length), "scope.node");
        return new NodeKeyBinding(
            ParseUInt32(parts, "channel", "scope.node.channel"),
            ParseUInt16(parts, "position", "scope.node.position"));
    }

    private static LookupScopeKeyBinding ParseLookupScopeKey(string value)
    {
        Dictionary<string, string> parts = ParseKeyParts(value, "scope.lookup");
        foreach (string required in new[] { "table", "node", "group", "burnup", "lower", "upper", "alpha" })
        {
            if (!parts.ContainsKey(required))
            {
                throw new ComparisonFailure("Scope.Lookup.Incomplete", "scope.lookup", "Lookup scope identity is incomplete.");
            }
        }

        ValidateStableId(parts["table"], "scope.lookup.table");
        ParseDouble(parts, "burnup", "scope.lookup.burnup");
        NodeGroupKeyBinding nodeGroup = new NodeGroupKeyBinding(
            0,
            ParseUInt16(parts, "node", "scope.lookup.node"),
            ParseUInt16(parts, "group", "scope.lookup.group"));
        return new LookupScopeKeyBinding(
            nodeGroup,
            parts["table"],
            ParseDouble(parts, "burnup", "scope.lookup.burnup"),
            new LookupBracketBinding(
                ParseUInt32(parts, "lower", "scope.lookup.lower"),
                ParseUInt32(parts, "upper", "scope.lookup.upper"),
                ParseDouble(parts, "alpha", "scope.lookup.alpha")));
    }

    private static RunPairScopeKeyBinding ParseRunPairScopeKey(string value)
    {
        string[] parts = value.Split('|');
        if (parts.Length != 2)
        {
            throw new ComparisonFailure("Scope.RunPair.Invalid", "scope.run_pair", "A pair of canonical run IDs was required.");
        }

        ValidateStableId(parts[0], "scope.run_pair.run_id_a");
        ValidateStableId(parts[1], "scope.run_pair.run_id_b");
        return new RunPairScopeKeyBinding(parts[0], parts[1]);
    }

    private static Dictionary<string, string> ParseKeyParts(string value, string path)
    {
        var parts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string part in value.Split(';'))
        {
            string[] pair = part.Split('=', 2);
            if (pair.Length != 2 || string.IsNullOrWhiteSpace(pair[0]) || !parts.TryAdd(pair[0], pair[1]))
            {
                throw new ComparisonFailure("Scope.Key.Invalid", path, "Scope key fields must be unique key/value pairs.");
            }
        }

        return parts;
    }

    private static ushort ParseUInt16(Dictionary<string, string> parts, string name, string path)
    {
        if (!parts.TryGetValue(name, out string? raw) ||
            !ushort.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out ushort value))
        {
            throw new ComparisonFailure("Scope.Value.Invalid", path, "A canonical UInt16 scope value was required.");
        }

        return value;
    }

    private static uint ParseUInt32(Dictionary<string, string> parts, string name, string path)
    {
        if (!parts.TryGetValue(name, out string? raw) ||
            !uint.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out uint value))
        {
            throw new ComparisonFailure("Scope.Value.Invalid", path, "A canonical UInt32 scope value was required.");
        }

        return value;
    }

    private static double ParseDouble(Dictionary<string, string> parts, string name, string path)
    {
        if (!parts.TryGetValue(name, out string? raw) ||
            !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
            !IsFinite(value))
        {
            throw new ComparisonFailure("Scope.Value.Invalid", path, "A canonical finite Float64 scope value was required.");
        }

        return value;
    }

    private static string UnitFor(string quantityId)
    {
        switch (quantityId)
        {
            case "spatial.k":
            case "spatial.normalization_scale":
                return "1";
            case "spatial.total_power":
            case "spatial.power":
                return "W";
            case "spatial.flux":
                return "m^-2 s^-1";
            case "spatial.fission_source":
                return "m^-3 s^-1";
            case "spatial.iteration_count":
                return "integer";
            case "spatial.coefficient_identity":
            case "determinism.repeat_equal":
            case "spatial.convergence":
                return "bytes";
            default:
                throw new ComparisonFailure(
                    "Quantity.Unknown",
                    quantityId,
                    "The candidate record attempted to emit an unregistered quantity.");
        }
    }

    private static string PayloadSchemaFor(string quantityId)
    {
        switch (quantityId)
        {
            case "spatial.flux":
                return "VectorV1";
            case "spatial.k":
            case "spatial.total_power":
            case "spatial.power":
            case "spatial.fission_source":
            case "spatial.normalization_scale":
                return "ScalarV1";
            case "spatial.iteration_count":
                return "IntegerV1";
            case "spatial.convergence":
            case "spatial.coefficient_identity":
            case "determinism.repeat_equal":
                return "BytesV1";
            default:
                throw new ComparisonFailure(
                    "Quantity.PayloadSchema.Unknown",
                    quantityId,
                    "The candidate quantity has no approved P2-T05 payload schema.");
        }
    }

    private static int ValidationDomainRankFor(string validationDomain)
    {
        switch (validationDomain)
        {
            case "Parser":
                return 0;
            case "Runtime":
                return 1;
            case "Provenance":
                return 2;
            default:
                throw new ComparisonFailure(
                    "Order.ValidationDomain.Unknown",
                    validationDomain,
                    "The validation domain is not in the P2-T05 rank table.");
        }
    }

    private static int ScopeKindRankFor(string scopeKind)
    {
        switch (scopeKind)
        {
            case "Global":
                return 0;
            case "Entity":
                return 1;
            case "Vector":
                return 2;
            case "SourceMap":
                return 3;
            case "Event":
                return 4;
            case "Interval":
                return 5;
            case "Solve":
                return 6;
            case "SolvePair":
                return 7;
            case "Snapshot":
                return 8;
            case "Run":
                return 9;
            case "RunPair":
                return 10;
            case "Fixture":
                return 11;
            case "Failure":
                return 12;
            case "Parser":
                return 13;
            case "Provenance":
                return 14;
            case "Lookup":
                return 15;
            case "TimeSeries":
                return 16;
            default:
                throw new ComparisonFailure(
                    "Order.ScopeKind.Unknown",
                    scopeKind,
                    "The scope kind is not in the P2-T05 rank table.");
        }
    }

    private static object ComponentOrderFor(string quantityId)
    {
        switch (quantityId)
        {
            case "spatial.flux":
                return new ComponentOrderSpecBinding(
                    0,
                    "NodeGroupKeyV1",
                    0,
                    "ChannelPositionGroupV1");
            default:
                return "NotApplicable";
        }
    }

    private static List<VectorComponent> CreateFluxComponents(double[] fluxByNodeGroup)
    {
        var components = new List<VectorComponent>(fluxByNodeGroup.Length);
        for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
        {
            components.Add(new VectorComponent(
                new NodeGroupKeyBinding(0, (ushort)nodeIndex, 1),
                fluxByNodeGroup[nodeIndex * 2]));
            components.Add(new VectorComponent(
                new NodeGroupKeyBinding(0, (ushort)nodeIndex, 2),
                fluxByNodeGroup[nodeIndex * 2 + 1]));
        }

        return components;
    }

    private static string ComputeDiagnosticsIdentity(DiagnosticsOutput diagnostics)
    {
        return ToHex(Sha256(JsonSerializer.SerializeToUtf8Bytes(diagnostics, JsonOptions)));
    }

    private static SpatialSnapshot Solve(
        CaseDefinition definition,
        BurnupCoefficientTableV1 table,
        BenchmarkPolicy benchmark,
        string pathId)
    {
        SpatialStencil stencil = CreateStencil();
        var nodes = new List<SpatialNodeCoefficients>(ExpectedNodeCount);
        for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
        {
            ContractValidationResult<BurnupCoefficientLookupResultV1> lookup = table.TryLookup(
                definition.BurnupJPerKgHmByNode[nodeIndex]);
            RequireValid(lookup, "lookup[" + nodeIndex.ToString(CultureInfo.InvariantCulture) + "]");
            BurnupCoefficientValuesV1 coefficients = lookup.Value.Coefficients;
            nodes.Add(new SpatialNodeCoefficients(
                stencil.Nodes[nodeIndex].Node,
                1.0,
                coefficients.AbsorptionGroup1PerM,
                coefficients.AbsorptionGroup2PerM,
                coefficients.DownscatterGroup1To2PerM,
                coefficients.FissionGroup1PerM,
                coefficients.FissionGroup2PerM,
                coefficients.NuFissionGroup1PerM,
                coefficients.NuFissionGroup2PerM,
                coefficients.ChiGroup1,
                coefficients.ChiGroup2,
                coefficients.EnergyPerFissionJ));
        }

        var edges = new[]
        {
            new SpatialEdgeConductance(
                new NodeKey(new ChannelId(0), new BundlePosition(0)),
                new NodeKey(new ChannelId(0), new BundlePosition(1)),
                0.6,
                0.6),
            new SpatialEdgeConductance(
                new NodeKey(new ChannelId(0), new BundlePosition(1)),
                new NodeKey(new ChannelId(0), new BundlePosition(2)),
                0.6,
                0.6)
        };
        SpatialBoundaryConductance[] boundaries = stencil.Nodes
            .SelectMany(node => node.BoundaryTerms.Select(term => new SpatialBoundaryConductance(
                node.Node,
                term.Face,
                0.0,
                0.0)))
            .ToArray();

        SpatialCoefficientSet coefficientsSet = RequireValid(
            SpatialCoefficientSet.TryCreate(stencil, nodes, edges, boundaries),
            "coefficients");
        SpatialLinearSolvePolicy linearPolicy = RequireValid(
            SpatialLinearSolvePolicy.TryCreate(
                benchmark.LinearMethodId,
                benchmark.LinearMethodVersion,
                benchmark.LinearAbsoluteResidualTolerance,
                benchmark.LinearRelativeResidualTolerance,
                benchmark.LinearMaximumIterations),
            "linear_solve_policy");
        SpatialEigenIteration iteration = RequireValid(
            SpatialEigenIteration.TryCreate(
                stencil,
                coefficientsSet,
                linearPolicy,
                benchmark.TargetPowerW,
                benchmark.InitialEigenvalue,
                benchmark.InitialGroup1Flux,
                benchmark.InitialGroup2Flux),
            "iteration");
        SpatialConvergencePolicy convergencePolicy = RequireValid(
            SpatialConvergencePolicy.TryCreate(
                benchmark.KAbsoluteTolerance,
                benchmark.KRelativeTolerance,
                benchmark.ResidualTolerance,
                benchmark.SourceShapeTolerance,
                benchmark.PowerBalanceTolerance,
                benchmark.MaximumIterations),
            "convergence_policy");
        SpatialEigenSolve solver = RequireValid(
            SpatialEigenSolve.TryCreate(iteration, convergencePolicy),
            "solver");
        ContractValidationResult<SpatialSolveResult> result = solver.TrySolve();
        RequireValid(result, "solve");
        SpatialSolveResult solved = result.Value;
        if (solved.Status != SpatialSolveStatus.Converged ||
            solved.FinalState == null ||
            !solved.HasUsableState)
        {
            string failureCode = solved.Status == SpatialSolveStatus.Nonconverged
                ? "Solve.Nonconverged"
                : "Solve.Failed";
            throw new ComparisonFailure(
                failureCode,
                "cases[" + definition.CaseId + "]",
                "The synthetic candidate case did not produce a usable state via " + pathId +
                ": status=" + solved.Status +
                ", reason=" + solved.Diagnostics.ConvergenceReason +
                ", inner_solve_status=" + solved.Diagnostics.InnerSolveStatus);
        }

        SpatialEigenIterationState finalState = solved.FinalState;
        double[] nodePower = new double[ExpectedNodeCount];
        double[] fissionSource = new double[ExpectedNodeCount];
        for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
        {
            SpatialNodeCoefficients node = coefficientsSet.Nodes[nodeIndex];
            double fissionRate =
                node.FissionGroup1PerM * finalState.Group1Flux[nodeIndex] +
                node.FissionGroup2PerM * finalState.Group2Flux[nodeIndex];
            nodePower[nodeIndex] = node.VolumeM3 * node.EnergyPerFissionJ * fissionRate;
            fissionSource[nodeIndex] =
                node.NuFissionGroup1PerM * finalState.Group1Flux[nodeIndex] +
                node.NuFissionGroup2PerM * finalState.Group2Flux[nodeIndex];
            if (!IsFinite(nodePower[nodeIndex]) || !IsFinite(fissionSource[nodeIndex]))
            {
                throw new ComparisonFailure(
                    "Snapshot.NonFinite",
                    "cases[" + definition.CaseId + "]",
                    "The candidate snapshot contained a non-finite derived observable.");
            }
        }

        return new SpatialSnapshot(
            finalState.Eigenvalue,
            finalState.NormalizationScale,
            finalState.TotalPowerW,
            finalState.FissionProductionRate,
            FlattenFlux(finalState),
            nodePower,
            fissionSource,
            new DiagnosticsOutput(
                solved.Status.ToString(),
                solved.Diagnostics.ConvergenceReason,
                solved.Diagnostics.IterationCount,
                solved.Diagnostics.EigenvalueChangeAbsolute,
                solved.Diagnostics.EigenvalueChangeRelative,
                solved.Diagnostics.ResidualAbsoluteInfinity,
                solved.Diagnostics.ResidualRelativeInfinity,
                solved.Diagnostics.SourceShapeChangeInfinity,
                solved.Diagnostics.PowerBalanceRelative,
                solved.Diagnostics.InnerSolveStatus.ToString(),
                solved.Diagnostics.FailedInnerSolveCount,
                solved.Diagnostics.InvalidCoefficientCount,
                solved.Diagnostics.NegativeFluxCount,
                solved.Diagnostics.NonFiniteValueCount,
                solved.Diagnostics.ClampCount,
                solved.Diagnostics.ForbiddenClampCount));
    }

    private static double[] FlattenFlux(SpatialEigenIterationState state)
    {
        var values = new double[ExpectedNodeCount * 2];
        int index = 0;
        for (int nodeIndex = 0; nodeIndex < ExpectedNodeCount; nodeIndex++)
        {
            values[index++] = state.Group1Flux[nodeIndex];
            values[index++] = state.Group2Flux[nodeIndex];
        }

        return values;
    }

    private static bool AreSnapshotsBitwiseEqual(SpatialSnapshot left, SpatialSnapshot right)
    {
        if (!DoubleBitsEqual(left.Eigenvalue, right.Eigenvalue) ||
            !DoubleBitsEqual(left.NormalizationScale, right.NormalizationScale) ||
            !DoubleBitsEqual(left.TotalPowerW, right.TotalPowerW) ||
            !DoubleBitsEqual(left.FissionProductionRate, right.FissionProductionRate) ||
            left.Diagnostics.Status != right.Diagnostics.Status ||
            left.Diagnostics.ConvergenceReason != right.Diagnostics.ConvergenceReason ||
            left.Diagnostics.IterationCount != right.Diagnostics.IterationCount ||
            left.Diagnostics.FailedInnerSolveCount != right.Diagnostics.FailedInnerSolveCount ||
            left.Diagnostics.InnerSolveStatus != right.Diagnostics.InnerSolveStatus ||
            left.Diagnostics.InvalidCoefficientCount != right.Diagnostics.InvalidCoefficientCount ||
            left.Diagnostics.NegativeFluxCount != right.Diagnostics.NegativeFluxCount ||
            left.Diagnostics.NonFiniteValueCount != right.Diagnostics.NonFiniteValueCount ||
            left.Diagnostics.ClampCount != right.Diagnostics.ClampCount ||
            left.Diagnostics.ForbiddenClampCount != right.Diagnostics.ForbiddenClampCount)
        {
            return false;
        }

        return ArraysBitwiseEqual(left.FluxByNodeGroup, right.FluxByNodeGroup) &&
               ArraysBitwiseEqual(left.NodePowerW, right.NodePowerW) &&
               ArraysBitwiseEqual(left.FissionSourceRateDensity, right.FissionSourceRateDensity) &&
               NullableDoubleBitsEqual(left.Diagnostics.EigenvalueChangeAbsolute, right.Diagnostics.EigenvalueChangeAbsolute) &&
               NullableDoubleBitsEqual(left.Diagnostics.EigenvalueChangeRelative, right.Diagnostics.EigenvalueChangeRelative) &&
               NullableDoubleBitsEqual(left.Diagnostics.ResidualAbsoluteInfinity, right.Diagnostics.ResidualAbsoluteInfinity) &&
               NullableDoubleBitsEqual(left.Diagnostics.ResidualRelativeInfinity, right.Diagnostics.ResidualRelativeInfinity) &&
               NullableDoubleBitsEqual(left.Diagnostics.SourceShapeChangeInfinity, right.Diagnostics.SourceShapeChangeInfinity) &&
               NullableDoubleBitsEqual(left.Diagnostics.PowerBalanceRelative, right.Diagnostics.PowerBalanceRelative);
    }

    private static bool ArraysBitwiseEqual(double[] left, double[] right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int index = 0; index < left.Length; index++)
        {
            if (!DoubleBitsEqual(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool NullableDoubleBitsEqual(double? left, double? right)
    {
        if (left.HasValue != right.HasValue)
        {
            return false;
        }

        return !left.HasValue || DoubleBitsEqual(left.Value, right!.Value);
    }

    private static bool DoubleBitsEqual(double left, double right)
    {
        return BitConverter.DoubleToInt64Bits(left) == BitConverter.DoubleToInt64Bits(right);
    }

    private static SpatialStencil CreateStencil()
    {
        ChannelId channelId = new ChannelId(0);
        var neighbors = new List<NeighborRecord>
        {
            new NeighborRecord(channelId, new BundlePosition(0), channelId, new BundlePosition(1), NeighborDirection.TowardEndB),
            new NeighborRecord(channelId, new BundlePosition(1), channelId, new BundlePosition(0), NeighborDirection.TowardEndA),
            new NeighborRecord(channelId, new BundlePosition(1), channelId, new BundlePosition(2), NeighborDirection.TowardEndB),
            new NeighborRecord(channelId, new BundlePosition(2), channelId, new BundlePosition(1), NeighborDirection.TowardEndA)
        };
        TopologyFace[] cardinalFaces =
        {
            TopologyFace.North,
            TopologyFace.East,
            TopologyFace.South,
            TopologyFace.West
        };
        var boundaries = new List<BoundaryFaceRecord>();
        for (uint position = 0; position < ExpectedNodeCount; position++)
        {
            foreach (TopologyFace face in cardinalFaces)
            {
                boundaries.Add(new BoundaryFaceRecord(
                    channelId,
                    new BundlePosition(position),
                    face,
                    BoundaryClassification.Reflective));
            }
        }

        boundaries.Add(new BoundaryFaceRecord(
            channelId,
            new BundlePosition(0),
            TopologyFace.EndA,
            BoundaryClassification.Reflective));
        boundaries.Add(new BoundaryFaceRecord(
            channelId,
            new BundlePosition(2),
            TopologyFace.EndB,
            BoundaryClassification.Reflective));

        ChannelTopology channel = new ChannelTopology(
            channelId,
            0,
            0,
            FlowDirection.EndAtoEndB,
            new BundlePosition(0),
            new BundlePosition(2),
            neighbors,
            boundaries);
        CoreTopology topology = RequireValid(
            CoreTopology.TryCreate(1, ExpectedNodeCount, new[] { channel }),
            "topology");
        return RequireValid(SpatialStencil.TryCreate(topology), "stencil");
    }

    private static BenchmarkPolicy ParseBenchmarkManifest(byte[] benchmarkBytes)
    {
        JsonDocument document = ParseJson(benchmarkBytes, "benchmark manifest");
        using (document)
        {
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
            RequireString(root, "format", "benchmark.format", "reactorsim.synthetic-benchmark-scenario/v1");
            RequireString(root, "task_id", "benchmark.task_id", "P4-T08");
            RequireString(root, "scenario_id", "benchmark.scenario_id", BenchmarkScenarioId);
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
                "target_power_w",
                "initial_flux_group1",
                "initial_flux_group2",
                "initial_eigenvalue");
            int nodeCount = ReadInt(scenario["node_count"], "benchmark.case.node_count");
            if (nodeCount != ExpectedNodeCount)
            {
                throw new ComparisonFailure(
                    "Benchmark.NodeCount.Unexpected",
                    "benchmark.case.node_count",
                    "R5 requires the existing three-node P4-T08 fixture.");
            }

            Dictionary<string, JsonElement> linear = ReadObject(
                root["linear_solve_policy"],
                "benchmark.linear_solve_policy",
                "method_id",
                "absolute_residual_tolerance",
                "relative_residual_tolerance",
                    "maximum_inner_iterations");
            Dictionary<string, JsonElement> convergence = ReadObject(
                root["convergence_policy"],
                "benchmark.convergence_policy",
                "k_absolute_tolerance",
                "k_relative_tolerance",
                "residual_tolerance",
                "source_shape_tolerance",
                "power_balance_tolerance",
                "maximum_iterations");
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
            RequireString(
                measurement,
                "allocation_counter",
                "benchmark.measurement.allocation_counter",
                "GC.GetAllocatedBytesForCurrentThread");
            RequireNull(measurement["performance_target"], "benchmark.measurement.performance_target");
            RequireString(properties: scenario, name: "topology", path: "benchmark.case.topology", expected: "one channel, three explicit within-channel positions");
            RequireString(properties: scenario, name: "boundary_classification", path: "benchmark.case.boundary_classification", expected: "all reflective");
            RequireDouble(scenario["interior_edge_conductance_group1_m2"], "benchmark.case.interior_edge_conductance_group1_m2", 0.6);
            RequireDouble(scenario["interior_edge_conductance_group2_m2"], "benchmark.case.interior_edge_conductance_group2_m2", 0.6);
            RequireDouble(scenario["node_volume_m3"], "benchmark.case.node_volume_m3", 1.0);
            return new BenchmarkPolicy(
                ReadDouble(scenario["target_power_w"], "benchmark.case.target_power_w"),
                ReadDouble(scenario["initial_eigenvalue"], "benchmark.case.initial_eigenvalue"),
                ReadDoubleArray(scenario["initial_flux_group1"], "benchmark.case.initial_flux_group1", ExpectedNodeCount),
                ReadDoubleArray(scenario["initial_flux_group2"], "benchmark.case.initial_flux_group2", ExpectedNodeCount),
                RequireString(
                    linear,
                    "method_id",
                    "benchmark.linear_solve_policy.method_id",
                    SpatialLinearSolvePolicy.DeterministicJacobiMethodId),
                SpatialLinearSolvePolicy.DeterministicJacobiMethodVersion,
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
    }

    private static SourceModel ParseSource(byte[] bytes)
    {
        JsonDocument document = ParseJson(bytes, "source");
        using (document)
        {
            Dictionary<string, JsonElement> properties = ReadObject(
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
            RequireString(properties, "format", "source.format", SourceFormat);
            RequireString(properties, "evidence_class", "source.evidence_class", SyntheticEvidence);
            RequireString(properties, "source_artifact_id", "source.source_artifact_id", SourceArtifactId);
            string provenance = ReadProvenance(properties["source_provenance"], "source.source_provenance");
            RequireString(properties, "transform_id", "source.transform_id", TransformId);
            string dataVersion = ReadRequiredString(properties["data_version"], "source.data_version");
            string units = ReadRequiredString(properties["units_profile_id"], "source.units_profile_id");
            List<SourceTableModel> tables = new List<SourceTableModel>();
            int index = 0;
            foreach (JsonElement element in ReadArray(properties["tables"], "source.tables"))
            {
                string path = "source.tables[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                Dictionary<string, JsonElement> table = ReadObject(element, path, "table_id", "material_variant_id", "rows");
                string tableId = ReadRequiredString(table["table_id"], path + ".table_id");
                ValidateStableId(tableId, path + ".table_id");
                string material = ReadRequiredString(table["material_variant_id"], path + ".material_variant_id");
                List<RowModel> rows = ReadRows(table["rows"], path + ".rows");
                tables.Add(new SourceTableModel(tableId, dataVersion, material, units, provenance, rows));
                index++;
            }

            if (tables.Count == 0)
            {
                throw new ComparisonFailure("Source.Tables.Empty", "source.tables", "At least one table is required.");
            }

            var identities = new HashSet<string>(StringComparer.Ordinal);
            for (int tableIndex = 0; tableIndex < tables.Count; tableIndex++)
            {
                SourceTableModel table = tables[tableIndex];
                string identity = table.MaterialVariantId + "|" + table.TableId;
                if (!identities.Add(identity))
                {
                    throw new ComparisonFailure(
                        "Source.Tables.Duplicate",
                        "source.tables[" + tableIndex.ToString(CultureInfo.InvariantCulture) + "]",
                        "Source table identities must be unique.");
                }

                if (tableIndex > 0)
                {
                    SourceTableModel previous = tables[tableIndex - 1];
                    int materialOrder = string.CompareOrdinal(previous.MaterialVariantId, table.MaterialVariantId);
                    int tableOrder = string.CompareOrdinal(previous.TableId, table.TableId);
                    if (materialOrder > 0 || (materialOrder == 0 && tableOrder > 0))
                    {
                        throw new ComparisonFailure(
                            "Source.Tables.Order.Invalid",
                            "source.tables",
                            "Source tables must be ordered by material variant and table identity.");
                    }
                }
            }

            return new SourceModel(
                SourceArtifactId,
                provenance,
                TransformId,
                dataVersion,
                units,
                tables);
        }
    }

    private static PackModel ParsePack(byte[] bytes)
    {
        JsonDocument document = ParseJson(bytes, "pack");
        using (document)
        {
            Dictionary<string, JsonElement> properties = ReadObject(
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
            RequireString(properties, "format", "pack.format", PackFormat);
            RequireString(properties, "approval_status", "pack.approval_status", CandidateApproval);
            RequireString(properties, "evidence_class", "pack.evidence_class", SyntheticEvidence);
            RequireString(properties, "runtime_use", "pack.runtime_use", RuntimeUse);
            RequireString(properties, "pack_artifact_id", "pack.pack_artifact_id", CandidatePackArtifactId);
            RequireInt(properties["schema_version"], "pack.schema_version", 1);
            string dataVersion = ReadRequiredString(properties["data_version"], "pack.data_version");
            string units = ReadRequiredString(properties["units_profile_id"], "pack.units_profile_id");
            RequireString(properties, "source_artifact_id", "pack.source_artifact_id", SourceArtifactId);
            string provenance = ReadProvenance(properties["source_provenance"], "pack.source_provenance");
            RequireString(properties, "transform_id", "pack.transform_id", TransformId);
            List<PackTableModel> tables = new List<PackTableModel>();
            int index = 0;
            foreach (JsonElement element in ReadArray(properties["tables"], "pack.tables"))
            {
                string path = "pack.tables[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                Dictionary<string, JsonElement> table = ReadObject(
                    element,
                    path,
                    "table_id",
                    "schema_version",
                    "data_version",
                    "material_variant_id",
                    "units_profile_id",
                    "source_provenance",
                    "checksum",
                    "rows");
                string tableId = ReadRequiredString(table["table_id"], path + ".table_id");
                ValidateStableId(tableId, path + ".table_id");
                RequireInt(table["schema_version"], path + ".schema_version", 1);
                string tableDataVersion = ReadRequiredString(table["data_version"], path + ".data_version");
                string material = ReadRequiredString(table["material_variant_id"], path + ".material_variant_id");
                string tableUnits = ReadRequiredString(table["units_profile_id"], path + ".units_profile_id");
                string tableProvenance = ReadProvenance(table["source_provenance"], path + ".source_provenance");
                string checksum = ReadDigest(table["checksum"], path + ".checksum");
                List<RowModel> rows = ReadRows(table["rows"], path + ".rows");
                PackTableModel parsedTable = new PackTableModel(
                    tableId,
                    tableDataVersion,
                    material,
                    tableUnits,
                    tableProvenance,
                    checksum,
                    rows);
                string expectedChecksum = ToHex(Sha256(SerializeTableIdentity(parsedTable)));
                if (!string.Equals(expectedChecksum, checksum, StringComparison.Ordinal))
                {
                    throw new ComparisonFailure(
                        "Pack.TableChecksum.Mismatch",
                        path + ".checksum",
                        "The pack table checksum does not match its canonical identity bytes.");
                }

                tables.Add(parsedTable);
                index++;
            }

            if (tables.Count == 0)
            {
                throw new ComparisonFailure("Pack.Tables.Empty", "pack.tables", "At least one table is required.");
            }

            var identities = new HashSet<string>(StringComparer.Ordinal);
            for (int tableIndex = 0; tableIndex < tables.Count; tableIndex++)
            {
                PackTableModel table = tables[tableIndex];
                string identity = table.MaterialVariantId + "|" + table.TableId;
                if (!identities.Add(identity))
                {
                    throw new ComparisonFailure(
                        "Pack.Tables.Duplicate",
                        "pack.tables[" + tableIndex.ToString(CultureInfo.InvariantCulture) + "]",
                        "Pack table identities must be unique.");
                }

                if (tableIndex > 0)
                {
                    PackTableModel previous = tables[tableIndex - 1];
                    int materialOrder = string.CompareOrdinal(previous.MaterialVariantId, table.MaterialVariantId);
                    int tableOrder = string.CompareOrdinal(previous.TableId, table.TableId);
                    if (materialOrder > 0 || (materialOrder == 0 && tableOrder > 0))
                    {
                        throw new ComparisonFailure(
                            "Pack.Tables.Order.Invalid",
                            "pack.tables",
                            "Pack tables must be ordered by material variant and table identity.");
                    }
                }
            }

            return new PackModel(
                CandidatePackArtifactId,
                SourceArtifactId,
                provenance,
                TransformId,
                dataVersion,
                units,
                tables);
        }
    }

    private static ManifestModel ParseManifest(byte[] bytes)
    {
        JsonDocument document = ParseJson(bytes, "manifest");
        using (document)
        {
            Dictionary<string, JsonElement> properties = ReadObject(
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
            RequireString(properties, "format", "manifest.format", ManifestFormat);
            RequireInt(properties["manifest_schema_version"], "manifest.manifest_schema_version", 1);
            RequireString(properties, "approval_status", "manifest.approval_status", CandidateApproval);
            RequireString(properties, "evidence_class", "manifest.evidence_class", SyntheticEvidence);
            RequireString(properties, "pack_artifact_id", "manifest.pack_artifact_id", CandidatePackArtifactId);
            RequireString(properties, "pack_format", "manifest.pack_format", PackFormat);
            string packSha = ReadDigest(properties["pack_sha256"], "manifest.pack_sha256");
            RequireString(properties, "source_artifact_id", "manifest.source_artifact_id", SourceArtifactId);
            string sourceSha = ReadDigest(properties["source_sha256"], "manifest.source_sha256");
            string provenance = ReadProvenance(properties["source_provenance"], "manifest.source_provenance");
            RequireString(properties, "transform_id", "manifest.transform_id", TransformId);
            string dataVersion = ReadRequiredString(properties["data_version"], "manifest.data_version");
            string units = ReadRequiredString(properties["units_profile_id"], "manifest.units_profile_id");
            var tables = new List<ManifestTableModel>();
            int index = 0;
            foreach (JsonElement element in ReadArray(properties["tables"], "manifest.tables"))
            {
                string path = "manifest.tables[" + index.ToString(CultureInfo.InvariantCulture) + "]";
                Dictionary<string, JsonElement> table = ReadObject(
                    element,
                    path,
                    "table_id",
                    "material_variant_id",
                    "checksum",
                    "row_count",
                    "burnup_min_j_per_kg_hm",
                    "burnup_max_j_per_kg_hm");
                string tableId = ReadRequiredString(table["table_id"], path + ".table_id");
                ValidateStableId(tableId, path + ".table_id");
                string material = ReadRequiredString(table["material_variant_id"], path + ".material_variant_id");
                string checksum = ReadDigest(table["checksum"], path + ".checksum");
                int rowCount = ReadInt(table["row_count"], path + ".row_count");
                if (rowCount <= 0)
                {
                    throw new ComparisonFailure("Manifest.RowCount.Invalid", path + ".row_count", "A positive row count is required.");
                }

                tables.Add(new ManifestTableModel(
                    tableId,
                    material,
                    checksum,
                    rowCount,
                    ReadDouble(table["burnup_min_j_per_kg_hm"], path + ".burnup_min_j_per_kg_hm"),
                    ReadDouble(table["burnup_max_j_per_kg_hm"], path + ".burnup_max_j_per_kg_hm")));
                index++;
            }

            if (tables.Count == 0)
            {
                throw new ComparisonFailure("Manifest.Tables.Empty", "manifest.tables", "At least one table is required.");
            }

            return new ManifestModel(packSha, sourceSha, provenance, dataVersion, units, tables);
        }
    }

    private static void ValidateManifest(
        byte[] sourceBytes,
        byte[] packBytes,
        SourceModel source,
        PackModel pack,
        ManifestModel manifest)
    {
        if (!string.Equals(ToHex(Sha256(sourceBytes)), manifest.SourceSha256, StringComparison.Ordinal) ||
            !string.Equals(ToHex(Sha256(packBytes)), manifest.PackSha256, StringComparison.Ordinal))
        {
            throw new ComparisonFailure(
                "Manifest.DigestMismatch",
                "manifest",
                "The R4 manifest does not bind the supplied source and pack bytes.");
        }

        if (!string.Equals(source.SourceProvenance, manifest.SourceProvenance, StringComparison.Ordinal) ||
            !string.Equals(pack.SourceProvenance, manifest.SourceProvenance, StringComparison.Ordinal) ||
            !string.Equals(source.DataVersion, manifest.DataVersion, StringComparison.Ordinal) ||
            !string.Equals(pack.DataVersion, manifest.DataVersion, StringComparison.Ordinal) ||
            !string.Equals(source.UnitsProfileId, manifest.UnitsProfileId, StringComparison.Ordinal) ||
            !string.Equals(pack.UnitsProfileId, manifest.UnitsProfileId, StringComparison.Ordinal))
        {
            throw new ComparisonFailure(
                "Manifest.MetadataMismatch",
                "manifest",
                "Source, pack, and manifest metadata are not consistently bound.");
        }

        if (manifest.Tables.Count != pack.Tables.Count)
        {
            throw new ComparisonFailure(
                "Manifest.TableCountMismatch",
                "manifest.tables",
                "The manifest table count must equal the pack table count.");
        }

        for (int index = 0; index < pack.Tables.Count; index++)
        {
            PackTableModel packTable = pack.Tables[index];
            ManifestTableModel manifestTable = manifest.Tables[index];
            if (!string.Equals(packTable.TableId, manifestTable.TableId, StringComparison.Ordinal) ||
                !string.Equals(packTable.MaterialVariantId, manifestTable.MaterialVariantId, StringComparison.Ordinal) ||
                !string.Equals(packTable.Checksum, manifestTable.Checksum, StringComparison.Ordinal) ||
                packTable.Rows.Count != manifestTable.RowCount ||
                !DoubleBitsEqual(packTable.Rows[0].BurnupJPerKgHm, manifestTable.BurnupMinJPerKgHm) ||
                !DoubleBitsEqual(packTable.Rows[^1].BurnupJPerKgHm, manifestTable.BurnupMaxJPerKgHm))
            {
                throw new ComparisonFailure(
                    "Manifest.TableBindingMismatch",
                    "manifest.tables[" + index.ToString(CultureInfo.InvariantCulture) + "]",
                    "Manifest table identity, checksum, row count, or burnup bounds do not match the pack.");
            }
        }
    }

    private static void ValidateSourceAndPack(SourceModel source, PackModel pack)
    {
        if (source.Tables.Count != pack.Tables.Count)
        {
            throw new ComparisonFailure(
                "SourcePack.TableCountMismatch",
                "tables",
                "The source and pack table counts differ.");
        }

        for (int tableIndex = 0; tableIndex < source.Tables.Count; tableIndex++)
        {
            SourceTableModel sourceTable = source.Tables[tableIndex];
            PackTableModel packTable = pack.Tables[tableIndex];
            if (!string.Equals(sourceTable.TableId, packTable.TableId, StringComparison.Ordinal) ||
                !string.Equals(sourceTable.MaterialVariantId, packTable.MaterialVariantId, StringComparison.Ordinal))
            {
                throw new ComparisonFailure(
                    "SourcePack.TableIdentityMismatch",
                    "tables[" + tableIndex.ToString(CultureInfo.InvariantCulture) + "]",
                    "The source and pack table identities or ordering differ.");
            }

            if (!string.Equals(packTable.DataVersion, source.DataVersion, StringComparison.Ordinal) ||
                !string.Equals(packTable.UnitsProfileId, source.UnitsProfileId, StringComparison.Ordinal) ||
                !string.Equals(packTable.SourceProvenance, source.SourceProvenance, StringComparison.Ordinal))
            {
                throw new ComparisonFailure(
                    "SourcePack.TableMetadataMismatch",
                    "tables[" + tableIndex.ToString(CultureInfo.InvariantCulture) + "]",
                    "The pack table metadata does not equal the bound source metadata.");
            }

            if (sourceTable.Rows.Count != packTable.Rows.Count)
            {
                throw new ComparisonFailure(
                    "SourcePack.RowCountMismatch",
                    sourceTable.MaterialVariantId,
                    "The source and pack row counts differ.");
            }

            for (int rowIndex = 0; rowIndex < sourceTable.Rows.Count; rowIndex++)
            {
                if (!RowBitwiseEqual(sourceTable.Rows[rowIndex], packTable.Rows[rowIndex]))
                {
                    throw new ComparisonFailure(
                        "SourcePack.RowMismatch",
                        sourceTable.MaterialVariantId + ".rows[" + rowIndex.ToString(CultureInfo.InvariantCulture) + "]",
                        "The reduced pack row does not equal the admitted synthetic source row.");
                }
            }
        }
    }

    private static bool RowBitwiseEqual(RowModel left, RowModel right)
    {
        return DoubleBitsEqual(left.BurnupJPerKgHm, right.BurnupJPerKgHm) &&
               DoubleBitsEqual(left.Coefficients.AbsorptionGroup1PerM, right.Coefficients.AbsorptionGroup1PerM) &&
               DoubleBitsEqual(left.Coefficients.AbsorptionGroup2PerM, right.Coefficients.AbsorptionGroup2PerM) &&
               DoubleBitsEqual(left.Coefficients.FissionGroup1PerM, right.Coefficients.FissionGroup1PerM) &&
               DoubleBitsEqual(left.Coefficients.FissionGroup2PerM, right.Coefficients.FissionGroup2PerM) &&
               DoubleBitsEqual(left.Coefficients.NuFissionGroup1PerM, right.Coefficients.NuFissionGroup1PerM) &&
               DoubleBitsEqual(left.Coefficients.NuFissionGroup2PerM, right.Coefficients.NuFissionGroup2PerM) &&
               DoubleBitsEqual(left.Coefficients.DownscatterGroup1To2PerM, right.Coefficients.DownscatterGroup1To2PerM) &&
               DoubleBitsEqual(left.Coefficients.ChiGroup1, right.Coefficients.ChiGroup1) &&
               DoubleBitsEqual(left.Coefficients.EnergyPerFissionJ, right.Coefficients.EnergyPerFissionJ);
    }

    private static BurnupCoefficientTableV1 CreateCoreTable(PackTableModel table, string pathId)
    {
        StableId tableId = StableId.Parse(table.TableId);
        var rows = new List<BurnupCoefficientRowV1>();
        foreach (RowModel row in table.Rows)
        {
            BurnupCoefficientValuesV1 values = RequireValid(
                BurnupCoefficientValuesV1.TryCreate(
                    row.Coefficients.AbsorptionGroup1PerM,
                    row.Coefficients.AbsorptionGroup2PerM,
                    row.Coefficients.FissionGroup1PerM,
                    row.Coefficients.FissionGroup2PerM,
                    row.Coefficients.NuFissionGroup1PerM,
                    row.Coefficients.NuFissionGroup2PerM,
                    row.Coefficients.DownscatterGroup1To2PerM,
                    row.Coefficients.ChiGroup1,
                    row.Coefficients.EnergyPerFissionJ),
                pathId + ".coefficients");
            rows.Add(RequireValid(
                BurnupCoefficientRowV1.TryCreate(row.BurnupJPerKgHm, values),
                pathId + ".row"));
        }

        return RequireValid(
            BurnupCoefficientTableV1.TryCreate(
                tableId,
                1,
                table.DataVersion,
                new MaterialVariantId(table.MaterialVariantId),
                table.UnitsProfileId,
                table.SourceProvenance,
                new Digest32(HexToBytes(table.Checksum)),
                rows),
            pathId + ".table");
    }

    private static byte[] SerializeTableIdentity(PackTableModel table)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
                   stream,
                   new JsonWriterOptions
                   {
                       Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default,
                       Indented = true
                   }))
        {
            writer.WriteStartObject();
            writer.WriteString("table_id", table.TableId);
            writer.WriteNumber("schema_version", 1);
            writer.WriteString("data_version", table.DataVersion);
            writer.WriteString("material_variant_id", table.MaterialVariantId);
            writer.WriteString("units_profile_id", table.UnitsProfileId);
            writer.WriteString("source_provenance", table.SourceProvenance);
            writer.WritePropertyName("rows");
            writer.WriteStartArray();
            foreach (RowModel row in table.Rows)
            {
                writer.WriteStartObject();
                writer.WriteNumber("burnup_j_per_kg_hm", row.BurnupJPerKgHm);
                writer.WritePropertyName("coefficients");
                writer.WriteStartObject();
                writer.WriteNumber("absorption_group1_per_m", row.Coefficients.AbsorptionGroup1PerM);
                writer.WriteNumber("absorption_group2_per_m", row.Coefficients.AbsorptionGroup2PerM);
                writer.WriteNumber("fission_group1_per_m", row.Coefficients.FissionGroup1PerM);
                writer.WriteNumber("fission_group2_per_m", row.Coefficients.FissionGroup2PerM);
                writer.WriteNumber("nu_fission_group1_per_m", row.Coefficients.NuFissionGroup1PerM);
                writer.WriteNumber("nu_fission_group2_per_m", row.Coefficients.NuFissionGroup2PerM);
                writer.WriteNumber("downscatter_group1_to2_per_m", row.Coefficients.DownscatterGroup1To2PerM);
                writer.WriteNumber("chi_group1", row.Coefficients.ChiGroup1);
                writer.WriteNumber("energy_per_fission_j", row.Coefficients.EnergyPerFissionJ);
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        string text = Encoding.UTF8.GetString(stream.ToArray())
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .TrimEnd('\n') + "\n";
        return Encoding.UTF8.GetBytes(text);
    }

    private static void ValidateOutputShape(byte[] bytes, ComparisonDocument expected)
    {
        JsonDocument document = ParseJson(bytes, "comparison output");
        using (document)
        {
            Dictionary<string, JsonElement> properties = ReadObject(
                document.RootElement,
                "comparison",
                "format",
                "task_id",
                "status",
                "evidence_class",
                "artifact_availability",
                "evidence_approval",
                "validation_domain",
                "pack_artifact_id",
                "source_artifact_id",
                "transform_id",
                "data_version",
                "units_profile_id",
                "source_provenance",
                "source_sha256",
                "pack_sha256",
                "manifest_sha256",
                "benchmark_manifest_sha256",
                "topology_fixture_id",
                "gate_status",
                "cases",
                "records");
            RequireString(properties, "format", "comparison.format", OutputFormat);
            RequireString(properties, "task_id", "comparison.task_id", "P4-T06-R5");
            RequireString(properties, "status", "comparison.status", "candidate");
            RequireString(properties, "evidence_class", "comparison.evidence_class", SyntheticEvidence);
            RequireString(properties, "artifact_availability", "comparison.artifact_availability", "CommittedSynthetic");
            RequireString(properties, "evidence_approval", "comparison.evidence_approval", "Candidate");
            RequireString(properties, "validation_domain", "comparison.validation_domain", "Runtime");
            RequireString(properties, "pack_artifact_id", "comparison.pack_artifact_id", CandidatePackArtifactId);
            RequireString(properties, "source_artifact_id", "comparison.source_artifact_id", SourceArtifactId);
            RequireString(properties, "benchmark_manifest_sha256", "comparison.benchmark_manifest_sha256", expected.BenchmarkManifestSha256);
            if (ReadArray(properties["cases"], "comparison.cases").Count() != expected.Cases.Count ||
                ReadArray(properties["records"], "comparison.records").Count() != expected.Records.Count)
            {
                throw new ComparisonFailure(
                    "Output.ShapeMismatch",
                    "comparison",
                    "The comparison output case or record count is not deterministic.");
            }
        }
    }

    private static List<RowModel> ReadRows(JsonElement element, string path)
    {
        var rows = new List<RowModel>();
        double previousBurnup = -1.0;
        int index = 0;
        foreach (JsonElement rowElement in ReadArray(element, path))
        {
            string rowPath = path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            Dictionary<string, JsonElement> row = ReadObject(rowElement, rowPath, "burnup_j_per_kg_hm", "coefficients");
            double burnup = ReadDouble(row["burnup_j_per_kg_hm"], rowPath + ".burnup_j_per_kg_hm");
            if (burnup < 0 || (index > 0 && burnup <= previousBurnup))
            {
                throw new ComparisonFailure(
                    "Rows.Order.Invalid",
                    rowPath + ".burnup_j_per_kg_hm",
                    "Burnup knots must be finite, nonnegative, and strictly increasing.");
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
            RowModel parsed = new RowModel(
                burnup,
                new CoefficientsModel(
                    ReadDouble(coefficients["absorption_group1_per_m"], rowPath + ".coefficients.absorption_group1_per_m"),
                    ReadDouble(coefficients["absorption_group2_per_m"], rowPath + ".coefficients.absorption_group2_per_m"),
                    ReadDouble(coefficients["fission_group1_per_m"], rowPath + ".coefficients.fission_group1_per_m"),
                    ReadDouble(coefficients["fission_group2_per_m"], rowPath + ".coefficients.fission_group2_per_m"),
                    ReadDouble(coefficients["nu_fission_group1_per_m"], rowPath + ".coefficients.nu_fission_group1_per_m"),
                    ReadDouble(coefficients["nu_fission_group2_per_m"], rowPath + ".coefficients.nu_fission_group2_per_m"),
                    ReadDouble(coefficients["downscatter_group1_to2_per_m"], rowPath + ".coefficients.downscatter_group1_to2_per_m"),
                    ReadDouble(coefficients["chi_group1"], rowPath + ".coefficients.chi_group1"),
                    ReadDouble(coefficients["energy_per_fission_j"], rowPath + ".coefficients.energy_per_fission_j")));
            rows.Add(parsed);
            previousBurnup = burnup;
            index++;
        }

        if (rows.Count == 0)
        {
            throw new ComparisonFailure("Rows.Empty", path, "At least one burnup row is required.");
        }

        return rows;
    }

    private static JsonDocument ParseJson(byte[] bytes, string kind)
    {
        try
        {
            return JsonDocument.Parse(bytes, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 32
            });
        }
        catch (JsonException exception)
        {
            throw new ComparisonFailure("Json.Invalid", kind, exception.Message);
        }
    }

    private static Dictionary<string, JsonElement> ReadObject(
        JsonElement element,
        string path,
        params string[] allowedNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ComparisonFailure("Json.Object.Required", path, "An object was required.");
        }

        var allowed = new HashSet<string>(allowedNames, StringComparer.Ordinal);
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
            {
                throw new ComparisonFailure("Json.Field.Unknown", path + "." + property.Name, "Unknown fields are rejected.");
            }

            if (!result.TryAdd(property.Name, property.Value))
            {
                throw new ComparisonFailure("Json.Field.Duplicate", path + "." + property.Name, "Duplicate fields are rejected.");
            }
        }

        foreach (string name in allowedNames)
        {
            if (!result.ContainsKey(name))
            {
                throw new ComparisonFailure("Json.Field.Missing", path + "." + name, "A required field is missing.");
            }
        }

        return result;
    }

    private static JsonElement.ArrayEnumerator ReadArray(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new ComparisonFailure("Json.Array.Required", path, "An array was required.");
        }

        return element.EnumerateArray();
    }

    private static string RequireString(
        Dictionary<string, JsonElement> properties,
        string name,
        string path,
        string expected)
    {
        string actual = ReadRequiredString(properties[name], path);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new ComparisonFailure("Value.Unexpected", path, "Expected '" + expected + "'.");
        }

        return actual;
    }

    private static string ReadRequiredString(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new ComparisonFailure("Value.String.Required", path, "A string was required.");
        }

        string value = element.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ComparisonFailure("Value.String.Empty", path, "A non-empty string was required.");
        }

        return value;
    }

    private static string ReadProvenance(JsonElement element, string path)
    {
        string value = ReadRequiredString(element, path);
        bool driveQualified = value.Length >= 2 &&
                              char.IsLetter(value[0]) &&
                              value[1] == ':';
        bool rooted = value.Length > 0 &&
                      (value[0] == '/' || value[0] == '\\' || value[0] == '~');
        if (driveQualified ||
            rooted ||
            value.Contains('/') ||
            value.Contains('\\') ||
            value.Contains("://", StringComparison.Ordinal) ||
            value.Contains("..", StringComparison.Ordinal))
        {
            throw new ComparisonFailure(
                "Provenance.Path.Invalid",
                path,
                "Provenance must be a path-free stable label, not a host path or URL.");
        }

        return value;
    }

    private static void RequireNull(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Null)
        {
            throw new ComparisonFailure("Value.Null.Required", path, "A JSON null was required.");
        }
    }

    private static double ReadDouble(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Number ||
            !element.TryGetDouble(out double value) ||
            !IsFinite(value) ||
            IsNegativeZero(value))
        {
            throw new ComparisonFailure("Value.Double.Invalid", path, "A finite JSON number was required.");
        }

        return value;
    }

    private static int ReadInt(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int value))
        {
            throw new ComparisonFailure("Value.Integer.Invalid", path, "A JSON Int32 was required.");
        }

        return value;
    }

    private static void RequireDouble(JsonElement element, string path, double expected)
    {
        double actual = ReadDouble(element, path);
        if (!DoubleBitsEqual(actual, expected))
        {
            throw new ComparisonFailure(
                "Value.Unexpected",
                path,
                "The existing synthetic benchmark fixture value changed unexpectedly.");
        }
    }

    private static double[] ReadDoubleArray(JsonElement element, string path, int expectedLength)
    {
        double[] values = ReadArray(element, path).Select((value, index) =>
            ReadDouble(value, path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]")).ToArray();
        if (values.Length != expectedLength)
        {
            throw new ComparisonFailure("Value.Array.Length", path, "The array length does not match the fixture node count.");
        }

        return values;
    }

    private static void RequireInt(JsonElement element, string path, int expected)
    {
        int actual = ReadInt(element, path);
        if (actual != expected)
        {
            throw new ComparisonFailure("Value.Unexpected", path, "Expected integer " + expected.ToString(CultureInfo.InvariantCulture) + ".");
        }
    }

    private static string ReadDigest(JsonElement element, string path)
    {
        string value = ReadRequiredString(element, path);
        if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new ComparisonFailure("Digest.Invalid", path, "A lowercase 32-byte hexadecimal digest was required.");
        }

        return value.ToLowerInvariant();
    }

    private static void ValidateStableId(string value, string path)
    {
        if (!StableId.TryParse(value, out StableId parsed) || parsed.IsEmpty)
        {
            throw new ComparisonFailure("StableId.Invalid", path, "A non-empty canonical UUID was required.");
        }
    }

    private static byte[] HexToBytes(string value)
    {
        var bytes = new byte[value.Length / 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = Convert.ToByte(value.Substring(index * 2, 2), 16);
        }

        return bytes;
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
                throw new ComparisonFailure(
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
        SimulationTimeBinding simulationTime,
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
            throw new ComparisonFailure(
                "Component.Key.Unsupported",
                componentKey,
                "R5 admits only aggregate records with the explicit P2-T05 NotApplicable component key.");
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

    private static string DeriveStateId(
        string stateKind,
        string caseId,
        string inputDigest,
        string snapshotDigest)
    {
        var writer = new CanonicalWriter();
        writer.WriteUtf8("CANDU-STATE-ID-V1");
        writer.WriteUtf8(stateKind);
        writer.WriteUtf8(caseId);
        writer.WriteDigest(inputDigest);
        writer.WriteDigest(snapshotDigest);
        return DeriveUuidV8(writer.ToArray());
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
            throw new ComparisonFailure("State.Version.Invalid", value, "A canonical UInt64 or NotApplicable value was required.");
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
            throw new ComparisonFailure("State.Version.Invalid", value, "A canonical UInt16 or NotApplicable value was required.");
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

    private static byte[] ReadFile(string path, string kind)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new ComparisonFailure("File.Missing", kind, "The required " + kind + " file does not exist.");
        }

        return File.ReadAllBytes(fullPath);
    }

    private static void WriteFile(string path, byte[] bytes)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ComparisonFailure("File.Path.Invalid", path, "The output directory is not valid.");
        }

        Directory.CreateDirectory(directory);
        File.WriteAllBytes(fullPath, bytes);
    }

    private static string DeriveUuidV8(byte[] identityBytes)
    {
        byte[] bytes = Sha256(identityBytes);
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return string.Concat(
            ToHex(bytes.AsSpan(0, 4)), "-",
            ToHex(bytes.AsSpan(4, 2)), "-",
            ToHex(bytes.AsSpan(6, 2)), "-",
            ToHex(bytes.AsSpan(8, 2)), "-",
            ToHex(bytes.AsSpan(10, 6)));
    }

    private static byte[] Sha256(byte[] bytes)
    {
        return SHA256.HashData(bytes);
    }

    private static string ToHex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string FormatDouble(double value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static bool IsNegativeZero(double value)
    {
        return value == 0.0 && BitConverter.DoubleToInt64Bits(value) < 0;
    }

    private static T RequireValid<T>(ContractValidationResult<T> result, string path)
        where T : class
    {
        if (!result.IsValid)
        {
            throw new ComparisonFailure("Core.Invalid", path, result.FirstDiagnostic.ToString());
        }

        return result.Value;
    }

    private static void RequireArgumentCount(string[] args, int expected, string usage)
    {
        if (args.Length != expected)
        {
            throw new ComparisonFailure("Arguments.Invalid", "arguments", "Usage: " + usage);
        }
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: generate <source> <pack> <manifest> <output>");
        Console.Error.WriteLine("       validate <source> <pack> <manifest> <output>");
    }

    private sealed class InputBundle
    {
        public InputBundle(
            SourceModel source,
            PackModel pack,
            ManifestModel manifest,
            BenchmarkPolicy benchmark,
            string sourceSha256,
            string packSha256,
            string manifestSha256,
            string benchmarkManifestSha256)
        {
            Source = source;
            Pack = pack;
            Manifest = manifest;
            Benchmark = benchmark;
            SourceSha256 = sourceSha256;
            PackSha256 = packSha256;
            ManifestSha256 = manifestSha256;
            BenchmarkManifestSha256 = benchmarkManifestSha256;
        }

        public SourceModel Source { get; }
        public PackModel Pack { get; }
        public ManifestModel Manifest { get; }
        public BenchmarkPolicy Benchmark { get; }
        public string SourceSha256 { get; }
        public string PackSha256 { get; }
        public string ManifestSha256 { get; }
        public string BenchmarkManifestSha256 { get; }
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
        public SourceTableModel(string tableId, string dataVersion, string materialVariantId, string unitsProfileId, string sourceProvenance, List<RowModel> rows)
        {
            TableId = tableId;
            DataVersion = dataVersion;
            MaterialVariantId = materialVariantId;
            UnitsProfileId = unitsProfileId;
            SourceProvenance = sourceProvenance;
            Rows = rows;
        }

        public string TableId { get; }
        public string DataVersion { get; }
        public string MaterialVariantId { get; }
        public string UnitsProfileId { get; }
        public string SourceProvenance { get; }
        public List<RowModel> Rows { get; }
    }

    private sealed class PackModel
    {
        public PackModel(string packArtifactId, string sourceArtifactId, string sourceProvenance, string transformId, string dataVersion, string unitsProfileId, List<PackTableModel> tables)
        {
            PackArtifactId = packArtifactId;
            SourceArtifactId = sourceArtifactId;
            SourceProvenance = sourceProvenance;
            TransformId = transformId;
            DataVersion = dataVersion;
            UnitsProfileId = unitsProfileId;
            Tables = tables;
        }

        public string PackArtifactId { get; }
        public string SourceArtifactId { get; }
        public string SourceProvenance { get; }
        public string TransformId { get; }
        public string DataVersion { get; }
        public string UnitsProfileId { get; }
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

    private sealed class ManifestModel
    {
        public ManifestModel(string packSha256, string sourceSha256, string sourceProvenance, string dataVersion, string unitsProfileId, List<ManifestTableModel> tables)
        {
            PackSha256 = packSha256;
            SourceSha256 = sourceSha256;
            SourceProvenance = sourceProvenance;
            DataVersion = dataVersion;
            UnitsProfileId = unitsProfileId;
            Tables = tables;
        }

        public string PackSha256 { get; }
        public string SourceSha256 { get; }
        public string SourceProvenance { get; }
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

    private sealed class BenchmarkPolicy
    {
        public BenchmarkPolicy(double targetPowerW, double initialEigenvalue, double[] initialGroup1Flux, double[] initialGroup2Flux, string linearMethodId, string linearMethodVersion, double linearAbsoluteResidualTolerance, double linearRelativeResidualTolerance, int linearMaximumIterations, double kAbsoluteTolerance, double kRelativeTolerance, double residualTolerance, double sourceShapeTolerance, double powerBalanceTolerance, int maximumIterations)
        {
            TargetPowerW = targetPowerW;
            InitialEigenvalue = initialEigenvalue;
            InitialGroup1Flux = initialGroup1Flux;
            InitialGroup2Flux = initialGroup2Flux;
            LinearMethodId = linearMethodId;
            LinearMethodVersion = linearMethodVersion;
            LinearAbsoluteResidualTolerance = linearAbsoluteResidualTolerance;
            LinearRelativeResidualTolerance = linearRelativeResidualTolerance;
            LinearMaximumIterations = linearMaximumIterations;
            KAbsoluteTolerance = kAbsoluteTolerance;
            KRelativeTolerance = kRelativeTolerance;
            ResidualTolerance = residualTolerance;
            SourceShapeTolerance = sourceShapeTolerance;
            PowerBalanceTolerance = powerBalanceTolerance;
            MaximumIterations = maximumIterations;
        }

        public double TargetPowerW { get; }
        public double InitialEigenvalue { get; }
        public double[] InitialGroup1Flux { get; }
        public double[] InitialGroup2Flux { get; }
        public string LinearMethodId { get; }
        public string LinearMethodVersion { get; }
        public double LinearAbsoluteResidualTolerance { get; }
        public double LinearRelativeResidualTolerance { get; }
        public int LinearMaximumIterations { get; }
        public double KAbsoluteTolerance { get; }
        public double KRelativeTolerance { get; }
        public double ResidualTolerance { get; }
        public double SourceShapeTolerance { get; }
        public double PowerBalanceTolerance { get; }
        public int MaximumIterations { get; }
    }

    private sealed class CaseDefinition
    {
        public CaseDefinition(string caseId, string scenarioClass, double[] burnupJPerKgHmByNode, string description)
        {
            CaseId = caseId;
            ScenarioClass = scenarioClass;
            BurnupJPerKgHmByNode = burnupJPerKgHmByNode;
            Description = description;
        }

        public string CaseId { get; }
        public string ScenarioClass { get; }
        public double[] BurnupJPerKgHmByNode { get; }
        public string Description { get; }
    }

    private sealed class CaseEvaluation
    {
        public CaseEvaluation(
            CaseDefinition definition,
            CaseOutput output,
            SpatialSnapshot packSnapshot,
            string inputDigest,
            string coefficientIdentity,
            string snapshotDigest,
            string repeatSnapshotDigest,
            List<LookupBindingData> lookupBindings,
            string packRunId,
            string repeatRunId)
        {
            Definition = definition;
            Output = output;
            PackSnapshot = packSnapshot;
            InputDigest = inputDigest;
            CoefficientIdentity = coefficientIdentity;
            SnapshotDigest = snapshotDigest;
            RepeatSnapshotDigest = repeatSnapshotDigest;
            LookupBindings = lookupBindings;
            PackRunId = packRunId;
            RepeatRunId = repeatRunId;
        }

        public CaseDefinition Definition { get; }
        public CaseOutput Output { get; }
        public SpatialSnapshot PackSnapshot { get; }
        public string InputDigest { get; }
        public string CoefficientIdentity { get; }
        public string SnapshotDigest { get; }
        public string RepeatSnapshotDigest { get; }
        public List<LookupBindingData> LookupBindings { get; }
        public string PackRunId { get; }
        public string RepeatRunId { get; }
    }

    private sealed class LookupBindingData
    {
        public LookupBindingData(
            int nodeIndex,
            int groupIndex,
            double inputBurnupJPerKgHm,
            int bracketLowerIndex,
            int bracketUpperIndex,
            double interpolationFraction,
            string tableId,
            string materialVariantId,
            string unitsProfileId,
            string checksum,
            string identityDigest)
        {
            NodeIndex = nodeIndex;
            GroupIndex = groupIndex;
            InputBurnupJPerKgHm = inputBurnupJPerKgHm;
            BracketLowerIndex = bracketLowerIndex;
            BracketUpperIndex = bracketUpperIndex;
            InterpolationFraction = interpolationFraction;
            TableId = tableId;
            MaterialVariantId = materialVariantId;
            UnitsProfileId = unitsProfileId;
            Checksum = checksum;
            IdentityDigest = identityDigest;
        }

        public int NodeIndex { get; }
        public int GroupIndex { get; }
        public double InputBurnupJPerKgHm { get; }
        public int BracketLowerIndex { get; }
        public int BracketUpperIndex { get; }
        public double InterpolationFraction { get; }
        public string TableId { get; }
        public string MaterialVariantId { get; }
        public string UnitsProfileId { get; }
        public string Checksum { get; }
        public string IdentityDigest { get; }
    }

    private sealed class ComparisonDocument
    {
        public ComparisonDocument(string format, string taskId, string status, string evidenceClass, string artifactAvailability, string evidenceApproval, string validationDomain, string packArtifactId, string sourceArtifactId, string transformId, string dataVersion, string unitsProfileId, string sourceProvenance, string sourceSha256, string packSha256, string manifestSha256, string benchmarkManifestSha256, string topologyFixtureId, string gateStatus, List<CaseOutput> cases, List<ComparisonRecord> records)
        {
            Format = format;
            TaskId = taskId;
            Status = status;
            EvidenceClass = evidenceClass;
            ArtifactAvailability = artifactAvailability;
            EvidenceApproval = evidenceApproval;
            ValidationDomain = validationDomain;
            PackArtifactId = packArtifactId;
            SourceArtifactId = sourceArtifactId;
            TransformId = transformId;
            DataVersion = dataVersion;
            UnitsProfileId = unitsProfileId;
            SourceProvenance = sourceProvenance;
            SourceSha256 = sourceSha256;
            PackSha256 = packSha256;
            ManifestSha256 = manifestSha256;
            BenchmarkManifestSha256 = benchmarkManifestSha256;
            TopologyFixtureId = topologyFixtureId;
            GateStatus = gateStatus;
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
        public string PackArtifactId { get; }
        public string SourceArtifactId { get; }
        public string TransformId { get; }
        public string DataVersion { get; }
        public string UnitsProfileId { get; }
        public string SourceProvenance { get; }
        public string SourceSha256 { get; }
        public string PackSha256 { get; }
        public string ManifestSha256 { get; }
        public string BenchmarkManifestSha256 { get; }
        public string TopologyFixtureId { get; }
        public string GateStatus { get; }
        public List<CaseOutput> Cases { get; }
        public List<ComparisonRecord> Records { get; }
    }

    private sealed class CaseOutput
    {
        public CaseOutput(string caseId, string scenarioClass, string status, double[] burnupJPerKgHmByNode, string description, SnapshotOutput? snapshot)
        {
            CaseId = caseId;
            ScenarioClass = scenarioClass;
            Status = status;
            BurnupJPerKgHmByNode = burnupJPerKgHmByNode;
            Description = description;
            Snapshot = snapshot;
        }

        public string CaseId { get; }
        public string ScenarioClass { get; }
        public string Status { get; }
        public double[] BurnupJPerKgHmByNode { get; }
        public string Description { get; }
        public SnapshotOutput? Snapshot { get; }
    }

    private sealed class SnapshotOutput
    {
        public SnapshotOutput(string inputDigest, string coefficientIdentity, string snapshotDigest, SpatialSnapshot reducedPackSnapshot, ComparisonOutcome comparison)
        {
            InputDigest = inputDigest;
            CoefficientIdentity = coefficientIdentity;
            SnapshotDigest = snapshotDigest;
            ReducedPackSnapshot = reducedPackSnapshot;
            Comparison = comparison;
        }

        public string InputDigest { get; }
        public string CoefficientIdentity { get; }
        public string SnapshotDigest { get; }
        public SpatialSnapshot ReducedPackSnapshot { get; }
        public ComparisonOutcome Comparison { get; }
    }

    private sealed class ComparisonOutcome
    {
        public ComparisonOutcome(string leftPath, string rightPath, bool exactBitwiseEqual, bool repeatEqual, double maximumAbsoluteDifference, double maximumRelativeDifference, string acceptanceStatus)
        {
            LeftPath = leftPath;
            RightPath = rightPath;
            ExactBitwiseEqual = exactBitwiseEqual;
            RepeatEqual = repeatEqual;
            MaximumAbsoluteDifference = maximumAbsoluteDifference;
            MaximumRelativeDifference = maximumRelativeDifference;
            AcceptanceStatus = acceptanceStatus;
        }

        public string LeftPath { get; }
        public string RightPath { get; }
        public bool ExactBitwiseEqual { get; }
        public bool RepeatEqual { get; }
        public double MaximumAbsoluteDifference { get; }
        public double MaximumRelativeDifference { get; }
        public string AcceptanceStatus { get; }
    }

    private sealed class SpatialSnapshot
    {
        public SpatialSnapshot(double eigenvalue, double normalizationScale, double totalPowerW, double fissionProductionRate, double[] fluxByNodeGroup, double[] nodePowerW, double[] fissionSourceRateDensity, DiagnosticsOutput diagnostics)
        {
            Eigenvalue = eigenvalue;
            NormalizationScale = normalizationScale;
            TotalPowerW = totalPowerW;
            FissionProductionRate = fissionProductionRate;
            FluxByNodeGroup = fluxByNodeGroup;
            NodePowerW = nodePowerW;
            FissionSourceRateDensity = fissionSourceRateDensity;
            Diagnostics = diagnostics;
        }

        public double Eigenvalue { get; }
        public double NormalizationScale { get; }
        public double TotalPowerW { get; }
        public double FissionProductionRate { get; }
        public double[] FluxByNodeGroup { get; }
        public double[] NodePowerW { get; }
        public double[] FissionSourceRateDensity { get; }
        public DiagnosticsOutput Diagnostics { get; }
    }

    private sealed class DiagnosticsOutput
    {
        public DiagnosticsOutput(string status, string convergenceReason, int iterationCount, double? eigenvalueChangeAbsolute, double? eigenvalueChangeRelative, double? residualAbsoluteInfinity, double? residualRelativeInfinity, double? sourceShapeChangeInfinity, double? powerBalanceRelative, string innerSolveStatus, int failedInnerSolveCount, int invalidCoefficientCount, int negativeFluxCount, int nonFiniteValueCount, int clampCount, int forbiddenClampCount)
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
        public double? EigenvalueChangeAbsolute { get; }
        public double? EigenvalueChangeRelative { get; }
        public double? ResidualAbsoluteInfinity { get; }
        public double? ResidualRelativeInfinity { get; }
        public double? SourceShapeChangeInfinity { get; }
        public double? PowerBalanceRelative { get; }
        public string InnerSolveStatus { get; }
        public int FailedInnerSolveCount { get; }
        public int InvalidCoefficientCount { get; }
        public int NegativeFluxCount { get; }
        public int NonFiniteValueCount { get; }
        public int ClampCount { get; }
        public int ForbiddenClampCount { get; }
    }

    private sealed class ComparisonRecord
    {
        public ComparisonRecord(
            string observableId,
            string quantityId,
            ScopeBinding scope,
            SimulationTimeBinding simulationTime,
            StateBinding stateBinding,
            string inputDigest,
            ValueBinding value,
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
        public SimulationTimeBinding SimulationTime { get; }
        public StateBinding StateBinding { get; }
        public string InputDigest { get; }
        public ValueBinding Value { get; }
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

    private sealed class RepeatBinding
    {
        public RepeatBinding(
            string leftRunId,
            string rightRunId,
            string leftSnapshotDigest,
            string rightSnapshotDigest,
            bool exactBitwiseEqual,
            string evidenceDigest,
            string evidenceBytesHex)
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

    private sealed class SimulationTimeBinding
    {
        public SimulationTimeBinding(double seconds)
        {
            Status = "Available";
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

        public string IdentityDigest()
        {
            return ToHex(Sha256(BuildStateCanonicalBytes(this)));
        }
    }

    private sealed class ValueBinding
    {
        public ValueBinding(
            string status,
            string kind,
            string unit,
            string payloadSchemaId,
            object componentOrderSpec,
            object payload)
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
        public ComponentOrderSpecBinding(
            int orderKindOrdinal,
            string componentKeySchemaId,
            int comparatorOrdinal,
            string tieBreakSchemaId)
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

    private sealed class CanonicalWriter
    {
        private readonly List<byte> buffer = new List<byte>();

        public void WriteUInt8(byte value)
        {
            WriteTyped(0x01, new[] { value });
        }

        public void WriteUInt16(ushort value)
        {
            WriteTyped(0x02, GetBytes(value));
        }

        public void WriteUInt32(uint value)
        {
            WriteTyped(0x03, GetBytes(value));
        }

        public void WriteUInt64(ulong value)
        {
            WriteTyped(0x04, GetBytes(value));
        }

        public void WriteFloat64(double value)
        {
            if (!IsFinite(value) || IsNegativeZero(value))
            {
                throw new ComparisonFailure("Canonical.Float.Invalid", "canonical", "Canonical Float64 values must be finite.");
            }

            WriteTyped(0x07, GetBytes(value));
        }

        public void WriteBool(bool value)
        {
            WriteTyped(0x08, new[] { value ? (byte)1 : (byte)0 });
        }

        public void WriteOpaqueId(string value)
        {
            string compact = value.Replace("-", string.Empty, StringComparison.Ordinal);
            if (compact.Length != 32 || compact.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new ComparisonFailure("Canonical.OpaqueId.Invalid", value, "Canonical opaque identifier bytes were required.");
            }

            WriteBytes(HexToBytes(compact));
        }

        public void WriteUtf8(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ComparisonFailure("Canonical.String.Invalid", "canonical", "Canonical UTF-8 values must be non-empty.");
            }

            WriteTyped(0x0A, Encoding.UTF8.GetBytes(value));
        }

        public void WriteBytes(byte[] value)
        {
            WriteTyped(0x0B, value);
        }

        public void WriteDigest(string value)
        {
            if (value.Length != 64 || value.Any(character => !Uri.IsHexDigit(character)))
            {
                throw new ComparisonFailure("Canonical.Digest.Invalid", "canonical", "Canonical SHA-256 bytes were required.");
            }

            WriteBytes(HexToBytes(value));
        }

        public void WriteNotApplicable()
        {
            WriteTyped(0x10, Array.Empty<byte>());
        }

        public byte[] ToArray()
        {
            return buffer.ToArray();
        }

        private void WriteTyped(byte typeTag, byte[] payload)
        {
            buffer.Add(typeTag);
            byte[] length = GetBytes((uint)payload.Length);
            buffer.AddRange(length);
            buffer.AddRange(payload);
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

    private sealed class ComparisonFailure : Exception
    {
        public ComparisonFailure(string code, string path, string message)
            : base(message)
        {
            Code = code;
            Path = path;
            MessageText = message;
        }

        public string Code { get; }
        public string Path { get; }
        public string MessageText { get; }
        public new string Message { get { return MessageText; } }
    }
}
