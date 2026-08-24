using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class Program
{
    private const string SourceFormat = "reactorsim.reduced-interpolation-source/v1";
    private const string PackFormat = "reactorsim.reduced-interpolation-pack/v1";
    private const string ReducedManifestFormat = "reactorsim.reduced-interpolation-manifest/v1";
    private const string BenchmarkFormat = "reactorsim.synthetic-benchmark-scenario/v1";
    private const string CandidateFormat = "reactorsim.independent-spatial-reproduction/v1";
    private const string CandidateManifestFormat = "reactorsim.independent-spatial-reproduction-manifest/v1";
    private const string OutputFormat = "reactorsim.analytical-spatial-reference/v1";
    private const string OutputManifestFormat = "reactorsim.analytical-spatial-reference-manifest/v1";
    private const string TaskId = "P4-T06-G4F";
    private const string SourceArtifactId = "p4-t06-r4-synthetic-input-v1";
    private const string PackArtifactId = "p4-t06-r4-reduced-candidate-v1";
    private const string TopologyFixtureId = "p4-t05-homogeneous-three-node-static-solve-v1";
    private const string BenchmarkScenarioId = "p4-t05-homogeneous-three-node-static-solve-v1";
    private const string TableId = "00000000-0000-0000-0000-0000000005a1";
    private const string MaterialVariantId = "MAT-SYN";
    private const string CandidateArtifactId = "p4-t06-g4d-independent-reproduction-v1";
    private const string CandidateManifestArtifactId = "p4-t06-g4d-independent-reproduction-manifest-v1";
    private const string UnitsProfileId = "SI-v1";
    private const string SyntheticEvidence = "synthetic";
    private const string CandidateApproval = "Candidate";
    private const string CandidateStatus = "candidate";
    private const string ComparisonStatus = "Deferred";
    private const int ExpectedNodeCount = 3;

    private const string ExpectedSourceSha256 =
        "1680a075cd34867b603e1e786002815b19be360d4e8557cd5485978dc32c1694";
    private const string ExpectedPackSha256 =
        "0de429b27f78b5e2c5fe724916b14ac8ce45ba05560ab73f3fa4805befd11a0e";
    private const string ExpectedReducedManifestSha256 =
        "6e3c19d4f596dfd930b339f983a54b46a6a27b9e83b6652f8e40a058c19e2a8b";
    private const string ExpectedBenchmarkSha256 =
        "8a53f7519a3b6597d3382c9e91df356474d7d1e72e8ce5d0055e30ef635a1045";
    private const string ExpectedCandidateSha256 =
        "ab456ca38fbd434d2c42af2ffb2f115af964273115e04c09f14945b74e2f5be3";
    private const string ExpectedCandidateManifestSha256 =
        "c9c5466f25cc8a0690fcf9f4307eb1948257d5efabde2580fd6402f765d0749b";
    private const string ExpectedTableChecksum =
        "4b1e27cb954d6bcbd6f25d12128a9bb1225ce8ae751ad10c3097ff470b881d40";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly CaseDefinition[] Definitions =
    {
        new("fresh_candidate", "fresh", 0.0, "Fresh-knot material state; no fresh-fuel inference."),
        new("equilibrium_like_candidate", "equilibrium_like", 50000.0,
            "Representative 50000 J/kg HM knot; not an inferred equilibrium state."),
        new("midcycle_interpolation_candidate", "interpolation_midpoint", 25000.0,
            "In-domain 0-to-50000 J/kg HM interpolation midpoint.")
    };

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 9)
            {
                PrintUsage();
                return 1;
            }

            return args[0].ToLowerInvariant() switch
            {
                "generate" => RunGenerate(args[1..]),
                "validate" => RunValidate(args[1..]),
                _ => UsageFailure()
            };
        }
        catch (AnalyticalFailure failure)
        {
            Console.Error.WriteLine(
                "P4_T06_G4F_FAILURE code=" + failure.Code +
                " input=" + failure.Input +
                " message=" + failure.Message);
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                "P4_T06_G4F_FAILURE code=Unhandled.Exception input=tool message=" +
                exception.Message);
            return 3;
        }
    }

    private static int RunGenerate(string[] paths)
    {
        InputBundle input = ReadInputs(paths);
        AnalyticalDocument document = BuildDocument(input);
        byte[] artifactBytes = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        AnalyticalManifest manifest = BuildManifest(input, artifactBytes, document);
        byte[] manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
        WriteFile(paths[6], artifactBytes);
        WriteFile(paths[7], manifestBytes);
        Console.WriteLine(
            "P4_T06_G4F_GENERATE_PASS cases=" + document.Cases.Count.ToString(CultureInfo.InvariantCulture) +
            " artifact_sha256=" + Sha256(artifactBytes) +
            " manifest_sha256=" + Sha256(manifestBytes));
        return 0;
    }

    private static int RunValidate(string[] paths)
    {
        InputBundle input = ReadInputs(paths);
        AnalyticalDocument expected = BuildDocument(input);
        byte[] expectedBytes = JsonSerializer.SerializeToUtf8Bytes(expected, JsonOptions);
        byte[] actualBytes = ReadFile(paths[6], "analytical artifact");
        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            throw new AnalyticalFailure(
                "Output.NonDeterministic", "artifact",
                "The committed analytical artifact does not match deterministic regeneration.");
        }

        AnalyticalManifest expectedManifest = BuildManifest(input, actualBytes, expected);
        byte[] expectedManifestBytes = JsonSerializer.SerializeToUtf8Bytes(expectedManifest, JsonOptions);
        byte[] actualManifestBytes = ReadFile(paths[7], "analytical manifest");
        if (!CryptographicOperations.FixedTimeEquals(expectedManifestBytes, actualManifestBytes))
        {
            throw new AnalyticalFailure(
                "Manifest.NonDeterministic", "manifest",
                "The committed analytical manifest does not match deterministic regeneration.");
        }

        ValidateDocument(expected, actualBytes, actualManifestBytes);
        Console.WriteLine(
            "P4_T06_G4F_VALIDATE_PASS cases=" + expected.Cases.Count.ToString(CultureInfo.InvariantCulture) +
            " artifact_sha256=" + Sha256(actualBytes) +
            " manifest_sha256=" + Sha256(actualManifestBytes));
        return 0;
    }

    private static int UsageFailure()
    {
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine(
            "Usage: <generate|validate> <source> <pack> <pack-manifest> <benchmark> " +
            "<candidate> <candidate-manifest> <output> <output-manifest>");
    }

    private static InputBundle ReadInputs(string[] paths)
    {
        if (paths.Length != 8)
        {
            throw new AnalyticalFailure("Arguments.Invalid", "tool", "Eight input/output paths are required.");
        }

        EnsureDistinctPaths(paths);
        byte[] sourceBytes = ReadFile(paths[0], "source");
        byte[] packBytes = ReadFile(paths[1], "pack");
        byte[] packManifestBytes = ReadFile(paths[2], "pack manifest");
        byte[] benchmarkBytes = ReadFile(paths[3], "benchmark");
        byte[] candidateBytes = ReadFile(paths[4], "candidate artifact");
        byte[] candidateManifestBytes = ReadFile(paths[5], "candidate manifest");

        RequireHash(sourceBytes, ExpectedSourceSha256, "source");
        RequireHash(packBytes, ExpectedPackSha256, "pack");
        RequireHash(packManifestBytes, ExpectedReducedManifestSha256, "pack manifest");
        RequireHash(benchmarkBytes, ExpectedBenchmarkSha256, "benchmark");
        RequireHash(candidateBytes, ExpectedCandidateSha256, "candidate artifact");
        RequireHash(candidateManifestBytes, ExpectedCandidateManifestSha256, "candidate manifest");

        JsonDocument source = ParseJson(sourceBytes, "source");
        JsonDocument pack = ParseJson(packBytes, "pack");
        JsonDocument packManifest = ParseJson(packManifestBytes, "pack manifest");
        JsonDocument benchmark = ParseJson(benchmarkBytes, "benchmark");
        JsonDocument candidate = ParseJson(candidateBytes, "candidate artifact");
        JsonDocument candidateManifest = ParseJson(candidateManifestBytes, "candidate manifest");

        ValidateSource(source.RootElement);
        PackModel packModel = ParsePack(pack.RootElement);
        ValidatePackManifest(packManifest.RootElement);
        BenchmarkModel benchmarkModel = ParseBenchmark(benchmark.RootElement);
        CandidateModel candidateModel = ParseCandidate(candidate.RootElement);
        ValidateCandidateManifest(candidateManifest.RootElement);

        if (packModel.SourceArtifactId != SourceArtifactId ||
            packModel.PackArtifactId != PackArtifactId ||
            packModel.TableId != TableId ||
            packModel.MaterialVariantId != MaterialVariantId)
        {
            throw new AnalyticalFailure("Input.Identity.Invalid", "pack", "The frozen pack identity changed.");
        }

        if (packModel.SourceSha256 != ExpectedSourceSha256 ||
            packModel.PackManifestSha256 != ExpectedReducedManifestSha256)
        {
            throw new AnalyticalFailure("Input.Identity.Invalid", "pack", "The pack provenance hashes changed.");
        }

        if (benchmarkModel.ScenarioId != BenchmarkScenarioId ||
            benchmarkModel.NodeCount != ExpectedNodeCount ||
            benchmarkModel.BoundaryClassification != "all reflective" ||
            benchmarkModel.InteriorConductanceGroup1 != 0.6 ||
            benchmarkModel.InteriorConductanceGroup2 != 0.6 ||
            benchmarkModel.NodeVolume != 1.0 ||
            benchmarkModel.TargetPowerW != 0.9)
        {
            throw new AnalyticalFailure(
                "Input.Benchmark.Invalid", "benchmark", "The admitted homogeneous reflective fixture changed.");
        }

        ValidateCandidateBinding(candidateModel);
        return new InputBundle(
            packModel,
            benchmarkModel,
            candidateModel,
            Sha256(sourceBytes),
            Sha256(packBytes),
            Sha256(packManifestBytes),
            Sha256(benchmarkBytes),
            Sha256(candidateBytes),
            Sha256(candidateManifestBytes));
    }

    private static AnalyticalDocument BuildDocument(InputBundle input)
    {
        var outputs = new List<AnalyticalCase>(Definitions.Length);
        foreach (CaseDefinition definition in Definitions)
        {
            PackCoefficients coefficients = input.Pack.Lookup(definition.Burnup);
            CandidateCase candidate = input.Candidate.Get(definition.CaseId);
            outputs.Add(Evaluate(definition, coefficients, candidate, input));
        }

        return new AnalyticalDocument
        {
            Format = OutputFormat,
            TaskId = TaskId,
            Status = CandidateStatus,
            EvidenceClass = SyntheticEvidence,
            EvidenceApproval = CandidateApproval,
            ComparisonStatus = ComparisonStatus,
            ReferenceRole = "candidate_mathematical_property_oracle",
            DerivationId = "p2-t02-homogeneous-reflective-two-group-generalized-eigen-v1",
            DerivationSummary =
                "For the admitted uniform reflective mode, the frozen P2-T02 equations are " +
                "represented as L phi = (1/k) B phi and evaluated as B phi = k L phi.",
            SourceArtifactId = SourceArtifactId,
            PackArtifactId = PackArtifactId,
            CandidateArtifactId = CandidateArtifactId,
            CandidateManifestArtifactId = CandidateManifestArtifactId,
            TopologyFixtureId = TopologyFixtureId,
            BenchmarkScenarioId = BenchmarkScenarioId,
            UnitsProfileId = UnitsProfileId,
            SourceSha256 = input.SourceSha256,
            PackSha256 = input.PackSha256,
            PackManifestSha256 = input.PackManifestSha256,
            BenchmarkSha256 = input.BenchmarkSha256,
            CandidateSha256 = input.CandidateSha256,
            CandidateManifestSha256 = input.CandidateManifestSha256,
            NodeCount = ExpectedNodeCount,
            TargetPowerW = input.Benchmark.TargetPowerW,
            ToleranceStatus = "Deferred",
            GoldenStatus = "NoGolden",
            Cases = outputs,
            CoverageLimitations = new List<string>
            {
                "Candidate-only mathematical evidence; no numeric tolerance or golden value is selected.",
                "The reduction applies only to the three homogeneous all-reflective admitted cases.",
                "Refuelled, RRS, and poison cases remain outside this artifact and retain their existing dispositions.",
                "Agreement with G4D is diagnostic self-consistency, not external solver validation."
            }
        };
    }

    private static AnalyticalCase Evaluate(
        CaseDefinition definition,
        PackCoefficients coefficients,
        CandidateCase candidate,
        InputBundle input)
    {
        double removalGroup1 = coefficients.AbsorptionGroup1PerM + coefficients.DownscatterGroup1To2PerM;
        double removalGroup2 = coefficients.AbsorptionGroup2PerM;
        RequirePositiveFinite(removalGroup1, definition.CaseId + ".removal_group1_per_m");
        RequirePositiveFinite(removalGroup2, definition.CaseId + ".removal_group2_per_m");

        double lowerFactor =
            (coefficients.DownscatterGroup1To2PerM * coefficients.ChiGroup1 / removalGroup1) +
            coefficients.ChiGroup2;
        double matrix11 = coefficients.ChiGroup1 * coefficients.NuFissionGroup1PerM / removalGroup1;
        double matrix12 = coefficients.ChiGroup1 * coefficients.NuFissionGroup2PerM / removalGroup1;
        double matrix21 = lowerFactor * coefficients.NuFissionGroup1PerM / removalGroup2;
        double matrix22 = lowerFactor * coefficients.NuFissionGroup2PerM / removalGroup2;

        double trace = matrix11 + matrix22;
        double determinant = (matrix11 * matrix22) - (matrix12 * matrix21);
        double discriminant = (trace * trace) - (4.0 * determinant);
        RequireFinite(discriminant, definition.CaseId + ".discriminant");
        if (discriminant < 0.0)
        {
            throw new AnalyticalFailure(
                "Reference.Eigenproblem.Invalid", definition.CaseId,
                "The frozen uniform-mode two-group matrix has no real eigenvalues.");
        }

        double sqrtDiscriminant = Math.Sqrt(discriminant);
        double eigenvalue1 = (trace + sqrtDiscriminant) / 2.0;
        double eigenvalue2 = (trace - sqrtDiscriminant) / 2.0;
        double eigenvalue = Math.Max(eigenvalue1, eigenvalue2);
        RequirePositiveFinite(eigenvalue, definition.CaseId + ".eigenvalue");

        double ratioGroup2ToGroup1;
        if (matrix12 != 0.0)
        {
            ratioGroup2ToGroup1 = (eigenvalue - matrix11) / matrix12;
        }
        else if (matrix21 != 0.0)
        {
            ratioGroup2ToGroup1 = matrix21 / (eigenvalue - matrix22);
        }
        else
        {
            throw new AnalyticalFailure(
                "Reference.Eigenvector.Invalid", definition.CaseId,
                "The frozen uniform-mode matrix has no usable two-group eigenvector ratio.");
        }

        RequirePositiveFinite(ratioGroup2ToGroup1, definition.CaseId + ".group2_to_group1_ratio");
        double rawGroup1 = 1.0;
        double rawGroup2 = ratioGroup2ToGroup1;
        double rawNodePower = input.Benchmark.NodeVolume * coefficients.EnergyPerFissionJ *
            ((coefficients.FissionGroup1PerM * rawGroup1) +
             (coefficients.FissionGroup2PerM * rawGroup2));
        double rawTotalPower = ExpectedNodeCount * rawNodePower;
        RequirePositiveFinite(rawTotalPower, definition.CaseId + ".raw_total_power_w");

        double normalizationScale = input.Benchmark.TargetPowerW / rawTotalPower;
        RequirePositiveFinite(normalizationScale, definition.CaseId + ".normalization_scale");
        double normalizedGroup1 = normalizationScale * rawGroup1;
        double normalizedGroup2 = normalizationScale * rawGroup2;
        double normalizedNodePower = input.Benchmark.NodeVolume * coefficients.EnergyPerFissionJ *
            ((coefficients.FissionGroup1PerM * normalizedGroup1) +
             (coefficients.FissionGroup2PerM * normalizedGroup2));
        double normalizedTotalPower = ExpectedNodeCount * normalizedNodePower;

        double fissionRate = (coefficients.FissionGroup1PerM * normalizedGroup1) +
            (coefficients.FissionGroup2PerM * normalizedGroup2);
        double fissionProductionRate = (coefficients.NuFissionGroup1PerM * normalizedGroup1) +
            (coefficients.NuFissionGroup2PerM * normalizedGroup2);
        double sourceRateDensity = fissionProductionRate;
        double group1Left = removalGroup1 * normalizedGroup1;
        double group1Right = coefficients.ChiGroup1 * fissionProductionRate / eigenvalue;
        double group2Left = removalGroup2 * normalizedGroup2;
        double group2Right = (coefficients.DownscatterGroup1To2PerM * normalizedGroup1) +
            (coefficients.ChiGroup2 * fissionProductionRate / eigenvalue);

        double[] analyticalFlux =
        {
            normalizedGroup1, normalizedGroup2,
            normalizedGroup1, normalizedGroup2,
            normalizedGroup1, normalizedGroup2
        };
        double[] candidateFlux = candidate.FluxByNodeGroup;
        double maxFluxAbsoluteDifference = MaxAbsoluteDifference(analyticalFlux, candidateFlux);
        double maxFluxRelativeDifference = MaxRelativeDifference(analyticalFlux, candidateFlux);
        double eigenvalueAbsoluteDifference = Math.Abs(eigenvalue - candidate.Eigenvalue);
        double eigenvalueRelativeDifference = RelativeDifference(eigenvalue, candidate.Eigenvalue);
        double totalPowerAbsoluteDifference = Math.Abs(normalizedTotalPower - candidate.TotalPowerW);
        double nodePowerAbsoluteDifference = MaxAbsoluteDifference(
            new[] { normalizedNodePower, normalizedNodePower, normalizedNodePower }, candidate.NodePowerW);

        return new AnalyticalCase
        {
            CaseId = definition.CaseId,
            ScenarioClass = definition.ScenarioClass,
            Description = definition.Description,
            BurnupJPerKgHmByNode = new[] { definition.Burnup, definition.Burnup, definition.Burnup },
            CandidateInputDigest = candidate.InputDigest,
            CandidateCoefficientIdentity = candidate.CoefficientIdentity,
            TableId = TableId,
            MaterialVariantId = MaterialVariantId,
            InterpolationLowerIndex = input.Pack.BracketFor(definition.Burnup).LowerIndex,
            InterpolationUpperIndex = input.Pack.BracketFor(definition.Burnup).UpperIndex,
            InterpolationFraction = input.Pack.BracketFor(definition.Burnup).Alpha,
            Coefficients = coefficients,
            Reduction = new Reduction
            {
                UniformFluxMode = true,
                LeakageContributionGroup1PerM = 0.0,
                LeakageContributionGroup2PerM = 0.0,
                RemovalGroup1PerM = removalGroup1,
                RemovalGroup2PerM = removalGroup2,
                LowerSystemFactor = lowerFactor,
                Matrix = new Matrix2x2 { M11 = matrix11, M12 = matrix12, M21 = matrix21, M22 = matrix22 },
                Trace = trace,
                Determinant = determinant,
                Discriminant = discriminant,
                EigenvalueOther = eigenvalue == eigenvalue1 ? eigenvalue2 : eigenvalue1,
                Eigenvalue = eigenvalue,
                RawFluxGroup1 = rawGroup1,
                RawFluxGroup2 = rawGroup2,
                Group2ToGroup1Ratio = ratioGroup2ToGroup1,
                RawNodePowerW = rawNodePower,
                RawTotalPowerW = rawTotalPower,
                NormalizationScale = normalizationScale,
                NormalizedFluxByNodeGroup = analyticalFlux,
                NormalizedNodePowerW = normalizedNodePower,
                NormalizedTotalPowerW = normalizedTotalPower,
                FissionRateDensity = fissionRate,
                FissionProductionRate = fissionProductionRate
            },
            EquationResiduals = new EquationResiduals
            {
                Group1Absolute = group1Left - group1Right,
                Group2Absolute = group2Left - group2Right,
                Group1Left = group1Left,
                Group1Right = group1Right,
                Group2Left = group2Left,
                Group2Right = group2Right,
                SourceRateDensity = sourceRateDensity
            },
            CandidateComparison = new CandidateComparison
            {
                CandidateArtifactId = CandidateArtifactId,
                ComparisonDisposition = "DiagnosticOnly",
                EigenvalueAbsoluteDifference = eigenvalueAbsoluteDifference,
                EigenvalueRelativeDifference = eigenvalueRelativeDifference,
                FluxMaxAbsoluteDifference = maxFluxAbsoluteDifference,
                FluxMaxRelativeDifference = maxFluxRelativeDifference,
                TotalPowerAbsoluteDifference = totalPowerAbsoluteDifference,
                NodePowerMaxAbsoluteDifference = nodePowerAbsoluteDifference,
                BitwiseEqualForEigenvalue = BitwiseEqual(eigenvalue, candidate.Eigenvalue),
                BitwiseEqualForFlux = BitwiseEqual(analyticalFlux, candidateFlux),
                CandidateStatus = candidate.Status,
                CandidateEvidenceApproval = candidate.EvidenceApproval
            }
        };
    }

    private static AnalyticalManifest BuildManifest(
        InputBundle input,
        byte[] artifactBytes,
        AnalyticalDocument document)
    {
        return new AnalyticalManifest
        {
            Format = OutputManifestFormat,
            ManifestSchemaVersion = 1,
            ManifestArtifactId = "p4-t06-g4f-analytical-reference-manifest-v1",
            ArtifactId = "p4-t06-g4f-analytical-reference-v1",
            ArtifactSha256 = Sha256(artifactBytes),
            Status = CandidateStatus,
            EvidenceClass = SyntheticEvidence,
            EvidenceApproval = CandidateApproval,
            ComparisonStatus = ComparisonStatus,
            ReferenceRole = document.ReferenceRole,
            SourceSha256 = input.SourceSha256,
            PackSha256 = input.PackSha256,
            PackManifestSha256 = input.PackManifestSha256,
            BenchmarkSha256 = input.BenchmarkSha256,
            CandidateSha256 = input.CandidateSha256,
            CandidateManifestSha256 = input.CandidateManifestSha256,
            SourceArtifactId = SourceArtifactId,
            PackArtifactId = PackArtifactId,
            CandidateArtifactId = CandidateArtifactId,
            TopologyFixtureId = TopologyFixtureId,
            BenchmarkScenarioId = BenchmarkScenarioId,
            CaseCount = document.Cases.Count,
            PathBoundary = "No host-local paths; candidate comparison remains diagnostic only."
        };
    }

    private static void ValidateDocument(AnalyticalDocument document, byte[] artifactBytes, byte[] manifestBytes)
    {
        if (document.Cases.Count != 3 || document.Cases.Any(@case => @case.CandidateComparison.ComparisonDisposition != "DiagnosticOnly"))
        {
            throw new AnalyticalFailure("Output.Shape.Invalid", "artifact", "The candidate analytical output shape changed.");
        }

        if (document.ToleranceStatus != "Deferred" || document.GoldenStatus != "NoGolden" ||
            document.EvidenceApproval != CandidateApproval || document.ComparisonStatus != ComparisonStatus)
        {
            throw new AnalyticalFailure(
                "Output.Status.Invalid", "artifact", "Candidate evidence was promoted or a deferred status changed.");
        }

        if (document.Cases.Any(@case => !IsFinite(@case.Reduction.Eigenvalue) ||
            !IsFinite(@case.Reduction.NormalizedTotalPowerW) ||
            !IsFinite(@case.EquationResiduals.Group1Absolute) ||
            !IsFinite(@case.EquationResiduals.Group2Absolute)))
        {
            throw new AnalyticalFailure("Output.Numeric.Invalid", "artifact", "The analytical output contains a non-finite value.");
        }

        string artifactText = System.Text.Encoding.UTF8.GetString(artifactBytes);
        string manifestText = System.Text.Encoding.UTF8.GetString(manifestBytes);
        if (ContainsHostLocalPath(artifactText) || ContainsHostLocalPath(manifestText))
        {
            throw new AnalyticalFailure("Output.PathLeak", "artifact", "The candidate artifact contains a host-local path.");
        }
    }

    private static bool ContainsHostLocalPath(string text)
    {
        for (int index = 0; index < text.Length; index++)
        {
            if (char.IsLetter(text[index]) && index + 2 < text.Length &&
                text[index + 1] == ':' && (text[index + 2] == '\\' || text[index + 2] == '/'))
            {
                return true;
            }

            if (text[index] == '\\' && index + 1 < text.Length && text[index + 1] == '\\' &&
                ContainsUncServerShare(text, index))
            {
                return true;
            }

            if (text[index] == '/' && index + 1 < text.Length &&
                IsPathDelimiter(index == 0 ? '\0' : text[index - 1]) &&
                !char.IsWhiteSpace(text[index + 1]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsUncServerShare(string text, int start)
    {
        int cursor = start;
        while (cursor < text.Length && text[cursor] == '\\')
        {
            cursor++;
        }

        if (cursor - start < 2 || cursor >= text.Length || IsPathBreak(text[cursor]))
        {
            return false;
        }

        int serverStart = cursor;
        while (cursor < text.Length && !IsPathSeparator(text[cursor]) && !IsPathBreak(text[cursor]))
        {
            cursor++;
        }

        if (cursor == serverStart || cursor >= text.Length || !IsPathSeparator(text[cursor]))
        {
            return false;
        }

        while (cursor < text.Length && IsPathSeparator(text[cursor]))
        {
            cursor++;
        }

        return cursor < text.Length && !IsPathBreak(text[cursor]);
    }

    private static bool IsPathSeparator(char value) => value == '\\' || value == '/';

    private static bool IsPathBreak(char value) =>
        char.IsWhiteSpace(value) || value == '"' || value == '\'' || value == ',' ||
        value == '=' || value == ':' || value == ')' || value == ']' || value == '}';

    private static bool IsPathDelimiter(char value) =>
        value == '\0' || value == '"' || value == '\'' || value == '=' || value == ':' ||
        value == ',' || value == '(' || value == '[' || value == '{' || value == '\r' ||
        value == '\n' || value == '\t' || value == ' ';

    private static void ValidateSource(JsonElement root)
    {
        RequireString(root, "format", "source.format", SourceFormat);
        RequireString(root, "evidence_class", "source.evidence_class", SyntheticEvidence);
        RequireString(root, "source_artifact_id", "source.source_artifact_id", SourceArtifactId);
        RequireString(root, "units_profile_id", "source.units_profile_id", UnitsProfileId);
        RequireString(root, "data_version", "source.data_version", "synthetic-v1");
    }

    private static PackModel ParsePack(JsonElement root)
    {
        RequireString(root, "format", "pack.format", PackFormat);
        RequireString(root, "approval_status", "pack.approval_status", "candidate");
        RequireString(root, "evidence_class", "pack.evidence_class", SyntheticEvidence);
        string sourceArtifactId = ReadString(root, "source_artifact_id", "pack.source_artifact_id");
        string packArtifactId = ReadString(root, "pack_artifact_id", "pack.pack_artifact_id");
        string sourceSha256 = ReadString(root, "source_sha256", "pack.source_sha256", optional: true) ?? ExpectedSourceSha256;
        string packManifestSha256 = ReadString(root, "pack_manifest_sha256", "pack.pack_manifest_sha256", optional: true) ?? ExpectedReducedManifestSha256;
        JsonElement tables = RequireProperty(root, "tables", "pack");
        if (tables.ValueKind != JsonValueKind.Array || tables.GetArrayLength() != 1)
        {
            throw new AnalyticalFailure("Input.Pack.Invalid", "pack.tables", "Exactly one material table is required.");
        }

        JsonElement table = tables[0];
        string tableId = ReadString(table, "table_id", "pack.tables[0].table_id");
        string materialVariantId = ReadString(table, "material_variant_id", "pack.tables[0].material_variant_id");
        string units = ReadString(table, "units_profile_id", "pack.tables[0].units_profile_id");
        string checksum = ReadString(table, "checksum", "pack.tables[0].checksum");
        if (units != UnitsProfileId || checksum != ExpectedTableChecksum)
        {
            throw new AnalyticalFailure("Input.Pack.Invalid", "pack.tables[0]", "The frozen table identity changed.");
        }

        JsonElement rows = RequireProperty(table, "rows", "pack.tables[0]");
        if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() != 3)
        {
            throw new AnalyticalFailure("Input.Pack.Invalid", "pack.tables[0].rows", "The frozen three-row table changed.");
        }

        var parsedRows = new List<PackRow>(3);
        foreach (JsonElement row in rows.EnumerateArray())
        {
            double burnup = ReadDouble(row, "burnup_j_per_kg_hm", "pack.row.burnup");
            JsonElement coefficients = RequireProperty(row, "coefficients", "pack.row");
            parsedRows.Add(new PackRow(burnup, ParseCoefficients(coefficients)));
        }

        parsedRows.Sort((left, right) => left.Burnup.CompareTo(right.Burnup));
        if (parsedRows[0].Burnup != 0.0 || parsedRows[1].Burnup != 50000.0 || parsedRows[2].Burnup != 100000.0)
        {
            throw new AnalyticalFailure("Input.Pack.Invalid", "pack.rows", "The frozen burnup knots changed.");
        }

        return new PackModel(sourceArtifactId, packArtifactId, sourceSha256, packManifestSha256,
            tableId, materialVariantId, checksum, parsedRows);
    }

    private static PackCoefficients ParseCoefficients(JsonElement element)
    {
        return new PackCoefficients(
            ReadDouble(element, "absorption_group1_per_m", "pack.coefficients.absorption_group1_per_m"),
            ReadDouble(element, "absorption_group2_per_m", "pack.coefficients.absorption_group2_per_m"),
            ReadDouble(element, "fission_group1_per_m", "pack.coefficients.fission_group1_per_m"),
            ReadDouble(element, "fission_group2_per_m", "pack.coefficients.fission_group2_per_m"),
            ReadDouble(element, "nu_fission_group1_per_m", "pack.coefficients.nu_fission_group1_per_m"),
            ReadDouble(element, "nu_fission_group2_per_m", "pack.coefficients.nu_fission_group2_per_m"),
            ReadDouble(element, "downscatter_group1_to2_per_m", "pack.coefficients.downscatter_group1_to2_per_m"),
            ReadDouble(element, "chi_group1", "pack.coefficients.chi_group1"),
            ReadDouble(element, "energy_per_fission_j", "pack.coefficients.energy_per_fission_j"));
    }

    private static BenchmarkModel ParseBenchmark(JsonElement root)
    {
        RequireString(root, "format", "benchmark.format", BenchmarkFormat);
        JsonElement @case = RequireProperty(root, "case", "benchmark");
        JsonElement policy = RequireProperty(root, "convergence_policy", "benchmark");
        return new BenchmarkModel(
            ReadString(root, "scenario_id", "benchmark.scenario_id"),
            ReadInt(@case, "node_count", "benchmark.case.node_count"),
            ReadString(@case, "boundary_classification", "benchmark.case.boundary_classification"),
            ReadDouble(@case, "interior_edge_conductance_group1_m2", "benchmark.case.edge_g1"),
            ReadDouble(@case, "interior_edge_conductance_group2_m2", "benchmark.case.edge_g2"),
            ReadDouble(@case, "node_volume_m3", "benchmark.case.node_volume"),
            ReadDouble(@case, "target_power_w", "benchmark.case.target_power_w"),
            ReadDouble(policy, "maximum_iterations", "benchmark.convergence_policy.maximum_iterations"));
    }

    private static CandidateModel ParseCandidate(JsonElement root)
    {
        RequireString(root, "format", "candidate.format", CandidateFormat);
        RequireString(root, "status", "candidate.status", CandidateStatus);
        RequireString(root, "evidence_class", "candidate.evidence_class", SyntheticEvidence);
        RequireString(root, "evidence_approval", "candidate.evidence_approval", CandidateApproval);
        RequireString(root, "comparison_status", "candidate.comparison_status", ComparisonStatus);
        RequireString(root, "source_sha256", "candidate.source_sha256", ExpectedSourceSha256);
        RequireString(root, "pack_sha256", "candidate.pack_sha256", ExpectedPackSha256);
        RequireString(root, "reduced_manifest_sha256", "candidate.reduced_manifest_sha256", ExpectedReducedManifestSha256);
        RequireString(root, "benchmark_manifest_sha256", "candidate.benchmark_manifest_sha256", ExpectedBenchmarkSha256);
        JsonElement cases = RequireProperty(root, "cases", "candidate");
        if (cases.ValueKind != JsonValueKind.Array || cases.GetArrayLength() != 3)
        {
            throw new AnalyticalFailure("Input.Candidate.Invalid", "candidate.cases", "The candidate must contain three cases.");
        }

        var parsed = new List<CandidateCase>(3);
        foreach (JsonElement @case in cases.EnumerateArray())
        {
            string caseId = ReadString(@case, "case_id", "candidate.case_id");
            JsonElement snapshot = RequireProperty(@case, "snapshot", "candidate." + caseId);
            parsed.Add(new CandidateCase(
                caseId,
                ReadString(@case, "input_digest", "candidate." + caseId + ".input_digest"),
                ReadString(@case, "coefficient_identity", "candidate." + caseId + ".coefficient_identity"),
                ReadString(@case, "status", "candidate." + caseId + ".status"),
                ReadString(root, "evidence_approval", "candidate.evidence_approval"),
                ReadDouble(snapshot, "eigenvalue", "candidate." + caseId + ".snapshot.eigenvalue"),
                ReadDouble(snapshot, "total_power_w", "candidate." + caseId + ".snapshot.total_power_w"),
                ReadDoubleArray(snapshot, "flux_by_node_group", "candidate." + caseId + ".snapshot.flux_by_node_group", 6),
                ReadDoubleArray(snapshot, "node_power_w", "candidate." + caseId + ".snapshot.node_power_w", 3)));
        }

        return new CandidateModel(parsed);
    }

    private static void ValidateCandidateBinding(CandidateModel candidate)
    {
        foreach (CaseDefinition definition in Definitions)
        {
            CandidateCase value = candidate.Get(definition.CaseId);
            if (value.Status != "CandidateSnapshot" || value.EvidenceApproval != CandidateApproval)
            {
                throw new AnalyticalFailure("Input.Candidate.Invalid", definition.CaseId, "Candidate case status changed.");
            }
        }
    }

    private static void ValidatePackManifest(JsonElement root)
    {
        RequireString(root, "format", "pack-manifest.format", ReducedManifestFormat);
        RequireString(root, "approval_status", "pack-manifest.approval_status", "candidate");
        RequireString(root, "evidence_class", "pack-manifest.evidence_class", SyntheticEvidence);
        RequireString(root, "pack_artifact_id", "pack-manifest.pack_artifact_id", PackArtifactId);
        RequireString(root, "pack_sha256", "pack-manifest.pack_sha256", ExpectedPackSha256);
        RequireString(root, "source_sha256", "pack-manifest.source_sha256", ExpectedSourceSha256);
    }

    private static void ValidateCandidateManifest(JsonElement root)
    {
        RequireString(root, "format", "candidate-manifest.format", CandidateManifestFormat);
        RequireString(root, "artifact_sha256", "candidate-manifest.artifact_sha256", ExpectedCandidateSha256);
        RequireString(root, "approval_status", "candidate-manifest.approval_status", "candidate");
        RequireString(root, "evidence_approval", "candidate-manifest.evidence_approval", CandidateApproval);
        RequireString(root, "comparison_status", "candidate-manifest.comparison_status", ComparisonStatus);
        if (ReadInt(root, "case_count", "candidate-manifest.case_count") != 3)
        {
            throw new AnalyticalFailure("Input.Candidate.Invalid", "candidate-manifest.case_count", "Candidate case count changed.");
        }
    }

    private static JsonDocument ParseJson(byte[] bytes, string input)
    {
        try
        {
            return JsonDocument.Parse(bytes);
        }
        catch (JsonException exception)
        {
            throw new AnalyticalFailure("Input.Json.Invalid", input, exception.Message);
        }
    }

    private static void EnsureDistinctPaths(string[] paths)
    {
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in paths)
        {
            string fullPath = Path.GetFullPath(path);
            if (!resolved.Add(fullPath))
            {
                throw new AnalyticalFailure("Arguments.PathOverlap", "tool", "Input and output paths must be distinct.");
            }
        }
    }

    private static byte[] ReadFile(string path, string input)
    {
        if (!File.Exists(path))
        {
            throw new AnalyticalFailure("Input.Missing", input, "Required file does not exist.");
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

    private static void RequireHash(byte[] bytes, string expected, string input)
    {
        string actual = Sha256(bytes);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new AnalyticalFailure("Input.HashMismatch", input, "Expected " + expected + " but found " + actual + ".");
        }
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static JsonElement RequireProperty(JsonElement element, string name, string input)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
        {
            throw new AnalyticalFailure("Input.Property.Missing", input + "." + name, "Required property is missing.");
        }

        return value;
    }

    private static string ReadString(JsonElement element, string name, string input, bool optional = false)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
        {
            if (optional) return null!;
            throw new AnalyticalFailure("Input.Property.Missing", input + "." + name, "Required string is missing.");
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new AnalyticalFailure("Input.Type.Invalid", input + "." + name, "Expected a JSON string.");
        }

        return value.GetString() ?? throw new AnalyticalFailure("Input.Value.Invalid", input + "." + name, "String was null.");
    }

    private static void RequireString(JsonElement element, string name, string input, string expected)
    {
        string actual = ReadString(element, name, input);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new AnalyticalFailure("Input.Value.Invalid", input + "." + name, "Expected " + expected + " but found " + actual + ".");
        }
    }

    private static double ReadDouble(JsonElement element, string name, string input)
    {
        JsonElement value = RequireProperty(element, name, input[..Math.Max(0, input.LastIndexOf('.'))]);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double parsed) || !IsFinite(parsed))
        {
            throw new AnalyticalFailure("Input.Number.Invalid", input, "Expected a finite JSON number.");
        }

        return parsed;
    }

    private static int ReadInt(JsonElement element, string name, string input)
    {
        JsonElement value = RequireProperty(element, name, input[..Math.Max(0, input.LastIndexOf('.'))]);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int parsed))
        {
            throw new AnalyticalFailure("Input.Number.Invalid", input, "Expected a JSON integer.");
        }

        return parsed;
    }

    private static double[] ReadDoubleArray(JsonElement element, string name, string input, int expectedLength)
    {
        JsonElement value = RequireProperty(element, name, input[..Math.Max(0, input.LastIndexOf('.'))]);
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != expectedLength)
        {
            throw new AnalyticalFailure("Input.Array.Invalid", input, "Unexpected array length.");
        }

        var result = new double[expectedLength];
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number || !item.TryGetDouble(out result[index]) || !IsFinite(result[index]))
            {
                throw new AnalyticalFailure("Input.Number.Invalid", input, "Array contains a non-finite number.");
            }

            index++;
        }

        return result;
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static void RequireFinite(double value, string input)
    {
        if (!IsFinite(value))
        {
            throw new AnalyticalFailure("Reference.Number.Invalid", input, "The calculated value is not finite.");
        }
    }

    private static void RequirePositiveFinite(double value, string input)
    {
        RequireFinite(value, input);
        if (value <= 0.0)
        {
            throw new AnalyticalFailure("Reference.Number.Invalid", input, "The calculated value is not positive.");
        }
    }

    private static double MaxAbsoluteDifference(double[] expected, double[] actual)
    {
        if (expected.Length != actual.Length)
        {
            throw new AnalyticalFailure("Comparison.Shape.Invalid", "candidate", "Comparison arrays have different lengths.");
        }

        double maximum = 0.0;
        for (int index = 0; index < expected.Length; index++)
        {
            maximum = Math.Max(maximum, Math.Abs(expected[index] - actual[index]));
        }

        return maximum;
    }

    private static double MaxRelativeDifference(double[] expected, double[] actual)
    {
        if (expected.Length != actual.Length)
        {
            throw new AnalyticalFailure("Comparison.Shape.Invalid", "candidate", "Comparison arrays have different lengths.");
        }

        double maximum = 0.0;
        for (int index = 0; index < expected.Length; index++)
        {
            maximum = Math.Max(maximum, RelativeDifference(expected[index], actual[index]));
        }

        return maximum;
    }

    private static double RelativeDifference(double expected, double actual)
    {
        double scale = Math.Max(Math.Abs(expected), Math.Abs(actual));
        return scale == 0.0 ? 0.0 : Math.Abs(expected - actual) / scale;
    }

    private static bool BitwiseEqual(double expected, double actual) =>
        BitConverter.DoubleToInt64Bits(expected) == BitConverter.DoubleToInt64Bits(actual);

    private static bool BitwiseEqual(double[] expected, double[] actual)
    {
        if (expected.Length != actual.Length) return false;
        for (int index = 0; index < expected.Length; index++)
        {
            if (!BitwiseEqual(expected[index], actual[index])) return false;
        }

        return true;
    }

    private sealed class AnalyticalFailure : Exception
    {
        public AnalyticalFailure(string code, string input, string message) : base(message)
        {
            Code = code;
            Input = input;
        }

        public string Code { get; }
        public string Input { get; }
    }

    private sealed record CaseDefinition(string CaseId, string ScenarioClass, double Burnup, string Description);

    private sealed record InputBundle(
        PackModel Pack,
        BenchmarkModel Benchmark,
        CandidateModel Candidate,
        string SourceSha256,
        string PackSha256,
        string PackManifestSha256,
        string BenchmarkSha256,
        string CandidateSha256,
        string CandidateManifestSha256);

    private sealed class PackModel
    {
        public PackModel(string sourceArtifactId, string packArtifactId, string sourceSha256, string packManifestSha256,
            string tableId, string materialVariantId, string checksum, List<PackRow> rows)
        {
            SourceArtifactId = sourceArtifactId;
            PackArtifactId = packArtifactId;
            SourceSha256 = sourceSha256;
            PackManifestSha256 = packManifestSha256;
            TableId = tableId;
            MaterialVariantId = materialVariantId;
            Checksum = checksum;
            Rows = rows;
        }

        public string SourceArtifactId { get; }
        public string PackArtifactId { get; }
        public string SourceSha256 { get; }
        public string PackManifestSha256 { get; }
        public string TableId { get; }
        public string MaterialVariantId { get; }
        public string Checksum { get; }
        public List<PackRow> Rows { get; }

        public PackCoefficients Lookup(double burnup)
        {
            Bracket bracket = BracketFor(burnup);
            if (bracket.LowerIndex == bracket.UpperIndex)
            {
                return Rows[bracket.LowerIndex].Coefficients;
            }

            PackCoefficients lower = Rows[bracket.LowerIndex].Coefficients;
            PackCoefficients upper = Rows[bracket.UpperIndex].Coefficients;
            return PackCoefficients.Interpolate(lower, upper, bracket.Alpha);
        }

        public Bracket BracketFor(double burnup)
        {
            for (int index = 0; index < Rows.Count; index++)
            {
                if (Rows[index].Burnup == burnup) return new Bracket(index, index, 0.0);
            }

            for (int index = 1; index < Rows.Count; index++)
            {
                if (Rows[index].Burnup > burnup)
                {
                    double alpha = (burnup - Rows[index - 1].Burnup) /
                        (Rows[index].Burnup - Rows[index - 1].Burnup);
                    return new Bracket(index - 1, index, alpha);
                }
            }

            throw new AnalyticalFailure("Lookup.OutOfRange", "pack", "The admitted burnup is outside the frozen pack domain.");
        }
    }

    private sealed record PackRow(double Burnup, PackCoefficients Coefficients);
    private sealed record Bracket(int LowerIndex, int UpperIndex, double Alpha);

    private sealed record PackCoefficients(
        double AbsorptionGroup1PerM,
        double AbsorptionGroup2PerM,
        double FissionGroup1PerM,
        double FissionGroup2PerM,
        double NuFissionGroup1PerM,
        double NuFissionGroup2PerM,
        double DownscatterGroup1To2PerM,
        double ChiGroup1,
        double EnergyPerFissionJ)
    {
        public double ChiGroup2 => 1.0 - ChiGroup1;

        public static PackCoefficients Interpolate(PackCoefficients lower, PackCoefficients upper, double alpha)
        {
            static double Blend(double left, double right, double fraction) =>
                ((1.0 - fraction) * left) + (fraction * right);

            return new PackCoefficients(
                Blend(lower.AbsorptionGroup1PerM, upper.AbsorptionGroup1PerM, alpha),
                Blend(lower.AbsorptionGroup2PerM, upper.AbsorptionGroup2PerM, alpha),
                Blend(lower.FissionGroup1PerM, upper.FissionGroup1PerM, alpha),
                Blend(lower.FissionGroup2PerM, upper.FissionGroup2PerM, alpha),
                Blend(lower.NuFissionGroup1PerM, upper.NuFissionGroup1PerM, alpha),
                Blend(lower.NuFissionGroup2PerM, upper.NuFissionGroup2PerM, alpha),
                Blend(lower.DownscatterGroup1To2PerM, upper.DownscatterGroup1To2PerM, alpha),
                Blend(lower.ChiGroup1, upper.ChiGroup1, alpha),
                Blend(lower.EnergyPerFissionJ, upper.EnergyPerFissionJ, alpha));
        }
    }

    private sealed record BenchmarkModel(
        string ScenarioId,
        int NodeCount,
        string BoundaryClassification,
        double InteriorConductanceGroup1,
        double InteriorConductanceGroup2,
        double NodeVolume,
        double TargetPowerW,
        double MaximumOuterIterations);

    private sealed class CandidateModel
    {
        public CandidateModel(List<CandidateCase> cases) => Cases = cases;
        public List<CandidateCase> Cases { get; }

        public CandidateCase Get(string caseId) =>
            Cases.SingleOrDefault(value => value.CaseId == caseId) ??
            throw new AnalyticalFailure("Input.Candidate.MissingCase", caseId, "Required candidate case is missing.");
    }

    private sealed record CandidateCase(
        string CaseId,
        string InputDigest,
        string CoefficientIdentity,
        string Status,
        string EvidenceApproval,
        double Eigenvalue,
        double TotalPowerW,
        double[] FluxByNodeGroup,
        double[] NodePowerW);

    private sealed class AnalyticalDocument
    {
        public string Format { get; set; } = "";
        public string TaskId { get; set; } = "";
        public string Status { get; set; } = "";
        public string EvidenceClass { get; set; } = "";
        public string EvidenceApproval { get; set; } = "";
        public string ComparisonStatus { get; set; } = "";
        public string ReferenceRole { get; set; } = "";
        public string DerivationId { get; set; } = "";
        public string DerivationSummary { get; set; } = "";
        public string SourceArtifactId { get; set; } = "";
        public string PackArtifactId { get; set; } = "";
        public string CandidateArtifactId { get; set; } = "";
        public string CandidateManifestArtifactId { get; set; } = "";
        public string TopologyFixtureId { get; set; } = "";
        public string BenchmarkScenarioId { get; set; } = "";
        public string UnitsProfileId { get; set; } = "";
        public string SourceSha256 { get; set; } = "";
        public string PackSha256 { get; set; } = "";
        public string PackManifestSha256 { get; set; } = "";
        public string BenchmarkSha256 { get; set; } = "";
        public string CandidateSha256 { get; set; } = "";
        public string CandidateManifestSha256 { get; set; } = "";
        public int NodeCount { get; set; }
        public double TargetPowerW { get; set; }
        public string ToleranceStatus { get; set; } = "";
        public string GoldenStatus { get; set; } = "";
        public List<AnalyticalCase> Cases { get; set; } = new();
        public List<string> CoverageLimitations { get; set; } = new();
    }

    private sealed class AnalyticalCase
    {
        public string CaseId { get; set; } = "";
        public string ScenarioClass { get; set; } = "";
        public string Description { get; set; } = "";
        public double[] BurnupJPerKgHmByNode { get; set; } = Array.Empty<double>();
        public string CandidateInputDigest { get; set; } = "";
        public string CandidateCoefficientIdentity { get; set; } = "";
        public string TableId { get; set; } = "";
        public string MaterialVariantId { get; set; } = "";
        public int InterpolationLowerIndex { get; set; }
        public int InterpolationUpperIndex { get; set; }
        public double InterpolationFraction { get; set; }
        public PackCoefficients Coefficients { get; set; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
        public Reduction Reduction { get; set; } = new();
        public EquationResiduals EquationResiduals { get; set; } = new();
        public CandidateComparison CandidateComparison { get; set; } = new();
    }

    private sealed class Reduction
    {
        public bool UniformFluxMode { get; set; }
        public double LeakageContributionGroup1PerM { get; set; }
        public double LeakageContributionGroup2PerM { get; set; }
        public double RemovalGroup1PerM { get; set; }
        public double RemovalGroup2PerM { get; set; }
        public double LowerSystemFactor { get; set; }
        public Matrix2x2 Matrix { get; set; } = new();
        public double Trace { get; set; }
        public double Determinant { get; set; }
        public double Discriminant { get; set; }
        public double EigenvalueOther { get; set; }
        public double Eigenvalue { get; set; }
        public double RawFluxGroup1 { get; set; }
        public double RawFluxGroup2 { get; set; }
        public double Group2ToGroup1Ratio { get; set; }
        public double RawNodePowerW { get; set; }
        public double RawTotalPowerW { get; set; }
        public double NormalizationScale { get; set; }
        public double[] NormalizedFluxByNodeGroup { get; set; } = Array.Empty<double>();
        public double NormalizedNodePowerW { get; set; }
        public double NormalizedTotalPowerW { get; set; }
        public double FissionRateDensity { get; set; }
        public double FissionProductionRate { get; set; }
    }

    private sealed class Matrix2x2
    {
        public double M11 { get; set; }
        public double M12 { get; set; }
        public double M21 { get; set; }
        public double M22 { get; set; }
    }

    private sealed class EquationResiduals
    {
        public double Group1Absolute { get; set; }
        public double Group2Absolute { get; set; }
        public double Group1Left { get; set; }
        public double Group1Right { get; set; }
        public double Group2Left { get; set; }
        public double Group2Right { get; set; }
        public double SourceRateDensity { get; set; }
    }

    private sealed class CandidateComparison
    {
        public string CandidateArtifactId { get; set; } = "";
        public string ComparisonDisposition { get; set; } = "";
        public double EigenvalueAbsoluteDifference { get; set; }
        public double EigenvalueRelativeDifference { get; set; }
        public double FluxMaxAbsoluteDifference { get; set; }
        public double FluxMaxRelativeDifference { get; set; }
        public double TotalPowerAbsoluteDifference { get; set; }
        public double NodePowerMaxAbsoluteDifference { get; set; }
        public bool BitwiseEqualForEigenvalue { get; set; }
        public bool BitwiseEqualForFlux { get; set; }
        public string CandidateStatus { get; set; } = "";
        public string CandidateEvidenceApproval { get; set; } = "";
    }

    private sealed class AnalyticalManifest
    {
        public string Format { get; set; } = "";
        public int ManifestSchemaVersion { get; set; }
        public string ManifestArtifactId { get; set; } = "";
        public string ArtifactId { get; set; } = "";
        public string ArtifactSha256 { get; set; } = "";
        public string Status { get; set; } = "";
        public string EvidenceClass { get; set; } = "";
        public string EvidenceApproval { get; set; } = "";
        public string ComparisonStatus { get; set; } = "";
        public string ReferenceRole { get; set; } = "";
        public string SourceSha256 { get; set; } = "";
        public string PackSha256 { get; set; } = "";
        public string PackManifestSha256 { get; set; } = "";
        public string BenchmarkSha256 { get; set; } = "";
        public string CandidateSha256 { get; set; } = "";
        public string CandidateManifestSha256 { get; set; } = "";
        public string SourceArtifactId { get; set; } = "";
        public string PackArtifactId { get; set; } = "";
        public string CandidateArtifactId { get; set; } = "";
        public string TopologyFixtureId { get; set; } = "";
        public string BenchmarkScenarioId { get; set; } = "";
        public int CaseCount { get; set; }
        public string PathBoundary { get; set; } = "";
    }
}
