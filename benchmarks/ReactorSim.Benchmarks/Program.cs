using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using ReactorSim.Core;

namespace ReactorSim.Benchmarks;

internal static class Program
{
    private const string ScenarioId = "p4-t05-homogeneous-three-node-static-solve-v1";
    private const string ManifestRelativePath =
        "benchmarks/P4-T08-static-solver-benchmark.json";
    private const string ProfileManifestRelativePath =
        "benchmarks/P9-T03-core-solver-profile-parameters-v1.json";
    private const string ExpectedProfileFormat =
        "reactorsim.p9-core-solver-profile-parameters/v1";
    private const string ExpectedProfileTaskId = "P9-T03";
    private const string ExpectedProfileStatus = "core_solver_observation_only";
    private const string ExpectedProfileId = "p9-t03-core-solver-profile-v1";
    private const string ExpectedBenchmarkManifestSha256 =
        "8a53f7519a3b6597d3382c9e91df356474d7d1e72e8ce5d0055e30ef635a1045";
    private const string ExpectedHotspotStatus =
        "NotMeasured: no external profiler is installed; end-to-end timing does not identify loop-level hotspots.";
    private const string ExpectedDataMovementStatus =
        "Observed descriptors only: topology counts and flux-state bytes; no allocation attribution by solver phase.";
    private const string ExpectedMeasurementBoundary =
        "one complete public SpatialEigenSolve.TrySolve per sample after solver construction";
    private const string ExpectedEvidenceBoundary =
        "Project-authored synthetic P4-T08 Core solve and machine-specific profile observations only; no performance target, numerical tolerance, solver-hotspot claim, mobile result, or release budget.";
    private const int NodeCount = 3;
    private const int GroupCount = 2;
    private const int InteriorEdgeCount = 2;
    private const int BoundaryFaceCount = 14;
    private const double TargetPowerW = 0.9;
    private const int DefaultWarmupIterations = 10;
    private const int DefaultMeasurementIterations = 200;
    private const int DefaultProfileSampleIterations = 100;

    private static readonly double[] InitialGroup1Flux = { 1.0, 1.0, 1.0 };

    private static readonly double[] InitialGroup2Flux = { 1.0, 1.0, 1.0 };

    private static readonly TopologyFace[] CardinalFaces =
    {
        TopologyFace.North,
        TopologyFace.East,
        TopologyFace.South,
        TopologyFace.West
    };

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static int Main(string[] args)
    {
        try
        {
            ValidateScenarioManifest();
            BenchmarkSettings settings = BenchmarkSettings.Parse(args);
            if (settings.ProfileMode)
            {
                if (settings.WarmupIterations != 0 ||
                    settings.MeasurementIterations != 0)
                {
                    throw new ArgumentException(
                        "The P9-T03 profile uses sampling counts from its versioned profile manifest; do not combine --profile with --warmup or --measure.");
                }

                ProfileDefinition profile = LoadProfileDefinition(
                    settings.ProfileManifestPath);
                return RunProfile(profile);
            }

            SpatialEigenSolve solver = CreateSolver();

            for (int warmupIndex = 0; warmupIndex < settings.WarmupIterations; warmupIndex++)
            {
                ExecuteSolve(solver);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            double referenceEigenvalue = 0.0;
            double referenceTotalPowerW = 0.0;
            double[] referenceGroup1Flux = new double[NodeCount];
            double[] referenceGroup2Flux = new double[NodeCount];
            int referenceIterationCount = 0;
            bool hasReference = false;

            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long timestampBefore = Stopwatch.GetTimestamp();
            for (int measurementIndex = 0;
                 measurementIndex < settings.MeasurementIterations;
                 measurementIndex++)
            {
                SpatialSolveResult result = ExecuteSolve(solver);
                SpatialEigenIterationState finalState = result.FinalState!;
                if (!hasReference)
                {
                    referenceEigenvalue = finalState.Eigenvalue;
                    referenceTotalPowerW = finalState.TotalPowerW;
                    referenceIterationCount = result.Diagnostics.IterationCount;
                    for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
                    {
                        referenceGroup1Flux[nodeIndex] = finalState.Group1Flux[nodeIndex];
                        referenceGroup2Flux[nodeIndex] = finalState.Group2Flux[nodeIndex];
                    }

                    hasReference = true;
                }
                else
                {
                    AssertExactRepeat(
                        result,
                        finalState,
                        referenceEigenvalue,
                        referenceTotalPowerW,
                        referenceGroup1Flux,
                        referenceGroup2Flux,
                        referenceIterationCount);
                }
            }

            long timestampAfter = Stopwatch.GetTimestamp();
            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
            long elapsedTicks = timestampAfter - timestampBefore;
            long allocatedBytes = allocatedAfter - allocatedBefore;
            double elapsedMilliseconds = elapsedTicks * 1000.0 / Stopwatch.Frequency;
            double meanMicroseconds = elapsedTicks * 1_000_000.0 /
                Stopwatch.Frequency /
                settings.MeasurementIterations;
            double allocatedBytesPerSolve =
                (double)allocatedBytes / settings.MeasurementIterations;

            BenchmarkReport report = new BenchmarkReport
            {
                ScenarioId = ScenarioId,
                Status = "PASS",
                WarmupIterations = settings.WarmupIterations,
                MeasurementIterations = settings.MeasurementIterations,
                NodeCount = NodeCount,
                DeterministicRepeat = true,
                SolverIterationCount = referenceIterationCount,
                FinalEigenvalue = referenceEigenvalue,
                FinalTotalPowerW = referenceTotalPowerW,
                ElapsedMilliseconds = elapsedMilliseconds,
                MeanMicrosecondsPerSolve = meanMicroseconds,
                AllocatedBytesTotal = allocatedBytes,
                AllocatedBytesPerSolve = allocatedBytesPerSolve,
                StopwatchFrequency = Stopwatch.Frequency,
                Framework = RuntimeInformation.FrameworkDescription,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                OperatingSystem = RuntimeInformation.OSDescription
            };

            Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("P4/P9 benchmark failed: " + exception.Message);
            return 1;
        }
    }

    private static int RunProfile(ProfileDefinition profile)
    {
        SpatialEigenSolve solver = CreateSolver();
        for (int warmupIndex = 0;
             warmupIndex < profile.WarmupIterations;
             warmupIndex++)
        {
            ExecuteSolve(solver);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        ProfileCaseResult profileCase = ProfileSolve(
            solver,
            profile.SampleIterations);
        ProfileReport report = new ProfileReport
        {
            Format = "reactorsim.p9-core-solver-profile-result/v1",
            TaskId = ExpectedProfileTaskId,
            ProfileId = profile.ProfileId,
            Status = "PASS",
            WarmupIterations = profile.WarmupIterations,
            SampleIterations = profile.SampleIterations,
            DeterministicRepeat = profileCase.DeterministicRepeat,
            BenchmarkManifestSha256 = profile.BenchmarkManifestSha256,
            ProfileManifestSha256 = profile.ProfileManifestSha256,
            SolverScenarioId = ScenarioId,
            SolverMethodId = SpatialLinearSolvePolicy.DeterministicJacobiMethodId,
            SolverIterationCount = profileCase.SolverIterationCount,
            FinalEigenvalue = profileCase.FinalEigenvalue,
            FinalTotalPowerW = profileCase.FinalTotalPowerW,
            NodeCount = profile.NodeCount,
            GroupCount = profile.GroupCount,
            InteriorEdgeCount = profile.InteriorEdgeCount,
            BoundaryFaceCount = profile.BoundaryFaceCount,
            FluxStateBytes = profile.FluxStateBytes,
            HotspotStatus = profile.HotspotStatus,
            DataMovementStatus = profile.DataMovementStatus,
            MeasurementBoundary = profile.MeasurementBoundary,
            EvidenceBoundary = profile.EvidenceBoundary,
            Case = profileCase,
            StopwatchFrequency = Stopwatch.Frequency,
            Framework = RuntimeInformation.FrameworkDescription,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            OperatingSystem = RuntimeInformation.OSDescription,
            AndroidBaselineStatus = profile.AndroidBaselineStatus
        };

        Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
        return 0;
    }

    private static ProfileCaseResult ProfileSolve(
        SpatialEigenSolve solver,
        int sampleIterations)
    {
        SpatialSolveResult referenceResult = ExecuteSolve(solver);
        SpatialEigenIterationState referenceState = referenceResult.FinalState!;
        double[] referenceGroup1Flux = referenceState.Group1Flux.ToArray();
        double[] referenceGroup2Flux = referenceState.Group2Flux.ToArray();
        var elapsedTicks = new long[sampleIterations];
        var allocatedBytes = new long[sampleIterations];
        double elapsedMicrosecondsTotal = 0.0;
        double allocatedBytesTotal = 0.0;

        for (int sampleIndex = 0; sampleIndex < sampleIterations; sampleIndex++)
        {
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long timestampBefore = Stopwatch.GetTimestamp();
            SpatialSolveResult result = ExecuteSolve(solver);
            elapsedTicks[sampleIndex] = Stopwatch.GetTimestamp() - timestampBefore;
            allocatedBytes[sampleIndex] =
                GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            elapsedMicrosecondsTotal += TicksToMicroseconds(elapsedTicks[sampleIndex]);
            allocatedBytesTotal += allocatedBytes[sampleIndex];

            AssertExactRepeat(
                result,
                result.FinalState!,
                referenceState.Eigenvalue,
                referenceState.TotalPowerW,
                referenceGroup1Flux,
                referenceGroup2Flux,
                referenceResult.Diagnostics.IterationCount);
        }

        Array.Sort(elapsedTicks);
        Array.Sort(allocatedBytes);
        long p50ElapsedTicks = NearestRank(elapsedTicks, 0.50);
        long p95ElapsedTicks = NearestRank(elapsedTicks, 0.95);
        long p50AllocatedBytes = NearestRank(allocatedBytes, 0.50);
        long p95AllocatedBytes = NearestRank(allocatedBytes, 0.95);

        return new ProfileCaseResult
        {
            CaseId = ScenarioId,
            SampleIterations = sampleIterations,
            DeterministicRepeat = true,
            SolverIterationCount = referenceResult.Diagnostics.IterationCount,
            FinalEigenvalue = referenceState.Eigenvalue,
            FinalTotalPowerW = referenceState.TotalPowerW,
            MeanMicrosecondsPerSolve = elapsedMicrosecondsTotal / sampleIterations,
            MinimumMicrosecondsPerSolve = TicksToMicroseconds(elapsedTicks[0]),
            MedianMicrosecondsPerSolve = TicksToMicroseconds(p50ElapsedTicks),
            P95MicrosecondsPerSolve = TicksToMicroseconds(p95ElapsedTicks),
            MaximumMicrosecondsPerSolve = TicksToMicroseconds(
                elapsedTicks[elapsedTicks.Length - 1]),
            MeanAllocatedBytesPerSolve = allocatedBytesTotal / sampleIterations,
            MinimumAllocatedBytesPerSolve = allocatedBytes[0],
            MedianAllocatedBytesPerSolve = p50AllocatedBytes,
            P95AllocatedBytesPerSolve = p95AllocatedBytes,
            MaximumAllocatedBytesPerSolve = allocatedBytes[allocatedBytes.Length - 1]
        };
    }

    private static double TicksToMicroseconds(long ticks)
    {
        return ticks * 1_000_000.0 / Stopwatch.Frequency;
    }

    private static long NearestRank(long[] sortedValues, double percentile)
    {
        int index = (int)Math.Ceiling(sortedValues.Length * percentile) - 1;
        index = Math.Max(0, Math.Min(index, sortedValues.Length - 1));
        return sortedValues[index];
    }

    private static ProfileDefinition LoadProfileDefinition(string profilePath)
    {
        if (!File.Exists(profilePath))
        {
            throw new InvalidOperationException(
                "The P9-T03 profile must run with " +
                ProfileManifestRelativePath + ".");
        }

        string expectedProfilePath = Path.GetFullPath(
            Path.Combine(
                Directory.GetCurrentDirectory(),
                ProfileManifestRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!string.Equals(
                Path.GetFullPath(profilePath),
                expectedProfilePath,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The P9-T03 profile must use the repository profile manifest at its approved path.");
        }

        string expectedBenchmarkManifestPath = Path.GetFullPath(
            Path.Combine(
                Directory.GetCurrentDirectory(),
                ManifestRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(profilePath));
        JsonElement root = document.RootElement;
        RequireString(root, "format", ExpectedProfileFormat);
        RequireString(root, "task_id", ExpectedProfileTaskId);
        RequireString(root, "status", ExpectedProfileStatus);
        RequireString(root, "profile_id", ExpectedProfileId);

        JsonElement benchmarkManifest = RequireObject(root, "benchmark_manifest");
        RequireString(benchmarkManifest, "path", ManifestRelativePath);
        string benchmarkManifestSha256 = ReadNonEmptyString(
            benchmarkManifest,
            "sha256");
        RequireStringValue(
            benchmarkManifestSha256,
            ExpectedBenchmarkManifestSha256,
            "benchmark_manifest.sha256");
        RequireFileHashAtPath(
            expectedBenchmarkManifestPath,
            benchmarkManifestSha256);

        JsonElement sampling = RequireObject(root, "sampling");
        int warmupIterations = ReadPositiveInt(sampling, "warmup_iterations");
        int sampleIterations = ReadPositiveInt(sampling, "sample_iterations");
        if (warmupIterations != DefaultWarmupIterations ||
            sampleIterations != DefaultProfileSampleIterations)
        {
            throw new InvalidOperationException(
                "The P9-T03 profile sampling counts are not the approved values.");
        }

        RequireString(sampling, "clock", "System.Diagnostics.Stopwatch");
        RequireString(
            sampling,
            "allocation_counter",
            "GC.GetAllocatedBytesForCurrentThread");
        JsonElement percentiles = sampling.GetProperty("percentiles");
        if (percentiles.ValueKind != JsonValueKind.Array ||
            percentiles.GetArrayLength() != 2 ||
            !string.Equals(percentiles[0].GetString(), "p50", StringComparison.Ordinal) ||
            !string.Equals(percentiles[1].GetString(), "p95", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The P9-T03 profile must request the approved p50/p95 percentiles.");
        }

        RequireString(sampling, "percentile_rule", "nearest_rank");
        RequireString(sampling, "measurement_boundary", ExpectedMeasurementBoundary);

        JsonElement workload = RequireObject(root, "workload_descriptors");
        RequirePositiveValue(workload, "node_count", NodeCount);
        RequirePositiveValue(workload, "group_count", GroupCount);
        RequirePositiveValue(workload, "interior_edge_count", InteriorEdgeCount);
        RequirePositiveValue(workload, "boundary_face_count", BoundaryFaceCount);
        RequirePositiveValue(workload, "flux_state_bytes", NodeCount * GroupCount * sizeof(double));
        RequireString(
            workload,
            "flux_state_definition",
            "one double array per group over three nodes");

        JsonElement classification = RequireObject(root, "classification");
        RequireString(classification, "hotspot_status", ExpectedHotspotStatus);
        RequireString(
            classification,
            "data_movement_status",
            ExpectedDataMovementStatus);
        if (classification.GetProperty("performance_target").ValueKind != JsonValueKind.Null)
        {
            throw new InvalidOperationException(
                "The P9-T03 profile must not select a performance target.");
        }

        JsonElement platforms = RequireObject(root, "platforms");
        JsonElement desktop = RequireObject(platforms, "desktop");
        RequireString(desktop, "status", "required_observation");
        RequireString(desktop, "runtime", "Release");
        RequireString(desktop, "architecture", "current_host");
        JsonElement android = RequireObject(platforms, "android");
        string androidStatus = ReadNonEmptyString(android, "status");
        if (!string.Equals(androidStatus, "Deferred/NotAvailable", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The P9-T03 profile may not claim an Android baseline without device evidence.");
        }

        RequireString(
            android,
            "reason",
            "No adb command or representative Android device is available in the execution environment.");
        RequireString(android, "owner_gate", "G9");
        RequireString(root, "evidence_boundary", ExpectedEvidenceBoundary);

        return new ProfileDefinition(
            ExpectedProfileId,
            benchmarkManifestSha256,
            ComputeFileSha256(profilePath),
            warmupIterations,
            sampleIterations,
            NodeCount,
            GroupCount,
            InteriorEdgeCount,
            BoundaryFaceCount,
            NodeCount * GroupCount * sizeof(double),
            ExpectedHotspotStatus,
            ExpectedDataMovementStatus,
            ExpectedMeasurementBoundary,
            ExpectedEvidenceBoundary,
            androidStatus);
    }

    private static void ValidateScenarioManifest()
    {
        string manifestPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            ManifestRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException(
                "The benchmark must run from a repository root containing " +
                ManifestRelativePath + ".");
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = document.RootElement;
        if (root.GetProperty("format").GetString() != "reactorsim.synthetic-benchmark-scenario/v1" ||
            root.GetProperty("task_id").GetString() != "P4-T08" ||
            root.GetProperty("scenario_id").GetString() != ScenarioId)
        {
            throw new InvalidOperationException(
                "The benchmark manifest does not identify the P4-T08 scenario.");
        }

        string methodId = root
            .GetProperty("linear_solve_policy")
            .GetProperty("method_id")
            .GetString()!;
        if (methodId != SpatialLinearSolvePolicy.DeterministicJacobiMethodId)
        {
            throw new InvalidOperationException(
                "The benchmark manifest method ID does not match the Core policy.");
        }
    }

    private static JsonElement RequireObject(JsonElement parent, string propertyName)
    {
        JsonElement value = parent.GetProperty(propertyName);
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                "The P9-T03 profile property must be an object: " + propertyName + ".");
        }

        return value;
    }

    private static string ReadNonEmptyString(JsonElement parent, string propertyName)
    {
        JsonElement value = parent.GetProperty(propertyName);
        if (value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException(
                "The P9-T03 profile property must be a nonempty string: " +
                propertyName + ".");
        }

        return value.GetString()!;
    }

    private static int ReadPositiveInt(JsonElement parent, string propertyName)
    {
        JsonElement value = parent.GetProperty(propertyName);
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out int parsed) ||
            parsed <= 0)
        {
            throw new InvalidOperationException(
                "The P9-T03 profile property must be a positive integer: " +
                propertyName + ".");
        }

        return parsed;
    }

    private static void RequirePositiveValue(
        JsonElement parent,
        string propertyName,
        int expected)
    {
        int actual = ReadPositiveInt(parent, propertyName);
        if (actual != expected)
        {
            throw new InvalidOperationException(
                "The P9-T03 profile property does not match the approved value: " +
                propertyName + ".");
        }
    }

    private static void RequireString(
        JsonElement parent,
        string propertyName,
        string expected)
    {
        string actual = ReadNonEmptyString(parent, propertyName);
        RequireStringValue(actual, expected, propertyName);
    }

    private static void RequireStringValue(
        string actual,
        string expected,
        string propertyName)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The P9-T03 profile property does not match the approved value: " +
                propertyName + ".");
        }
    }

    private static void RequireFileHashAtPath(string path, string expectedHash)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                "The P9-T03 bound artifact is missing: " + path + ".");
        }

        string actualHash = ComputeFileSha256(path);
        if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The P9-T03 bound artifact hash does not match: " + path + ".");
        }
    }

    private static string ComputeFileSha256(string path)
    {
        return Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))
            .ToLowerInvariant();
    }

    private static SpatialSolveResult ExecuteSolve(SpatialEigenSolve solver)
    {
        ContractValidationResult<SpatialSolveResult> result = solver.TrySolve();
        if (!result.IsValid)
        {
            throw new InvalidOperationException(result.FirstDiagnostic.ToString());
        }

        SpatialSolveResult value = result.Value;
        if (value.Status != SpatialSolveStatus.Converged ||
            !value.HasUsableState ||
            value.FinalState == null)
        {
            throw new InvalidOperationException(
                "The benchmark scenario did not produce a converged usable state: " +
                value.Diagnostics.ConvergenceReason);
        }

        return value;
    }

    private static void AssertExactRepeat(
        SpatialSolveResult result,
        SpatialEigenIterationState finalState,
        double referenceEigenvalue,
        double referenceTotalPowerW,
        double[] referenceGroup1Flux,
        double[] referenceGroup2Flux,
        int referenceIterationCount)
    {
        if (result.Diagnostics.IterationCount != referenceIterationCount ||
            finalState.Eigenvalue != referenceEigenvalue ||
            finalState.TotalPowerW != referenceTotalPowerW)
        {
            throw new InvalidOperationException(
                "The benchmark scenario was not deterministic across repeated solves.");
        }

        for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
        {
            if (finalState.Group1Flux[nodeIndex] != referenceGroup1Flux[nodeIndex] ||
                finalState.Group2Flux[nodeIndex] != referenceGroup2Flux[nodeIndex])
            {
                throw new InvalidOperationException(
                    "The benchmark flux result was not deterministic across repeated solves.");
            }
        }
    }

    private static SpatialEigenSolve CreateSolver()
    {
        SpatialStencil stencil = CreateStencil();
        SpatialNodeCoefficients[] nodes = stencil.Nodes
            .Select(node => new SpatialNodeCoefficients(
                node.Node,
                1.0,
                0.3,
                0.2,
                0.1,
                0.1,
                0.1,
                0.15,
                0.25,
                1.0,
                0.0,
                1.0))
            .ToArray();
        SpatialEdgeConductance[] edges =
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

        SpatialCoefficientSet coefficients = Require(
            SpatialCoefficientSet.TryCreate(stencil, nodes, edges, boundaries));
        SpatialLinearSolvePolicy linearPolicy = Require(
            SpatialLinearSolvePolicy.TryCreate(
                SpatialLinearSolvePolicy.DeterministicJacobiMethodId,
                SpatialLinearSolvePolicy.DeterministicJacobiMethodVersion,
                1e-14,
                1e-14,
                512));
        SpatialEigenIteration iteration = Require(
            SpatialEigenIteration.TryCreate(
                stencil,
                coefficients,
                linearPolicy,
                TargetPowerW,
                1.0,
                InitialGroup1Flux,
                InitialGroup2Flux));
        SpatialConvergencePolicy convergencePolicy = Require(
            SpatialConvergencePolicy.TryCreate(
                1e-6,
                1e-6,
                1e-6,
                1e-6,
                1e-6,
                2));
        return Require(SpatialEigenSolve.TryCreate(iteration, convergencePolicy));
    }

    private static SpatialStencil CreateStencil()
    {
        ChannelId channelId = new ChannelId(0);
        var neighbors = new List<NeighborRecord>
        {
            new NeighborRecord(
                channelId,
                new BundlePosition(0),
                channelId,
                new BundlePosition(1),
                NeighborDirection.TowardEndB),
            new NeighborRecord(
                channelId,
                new BundlePosition(1),
                channelId,
                new BundlePosition(0),
                NeighborDirection.TowardEndA),
            new NeighborRecord(
                channelId,
                new BundlePosition(1),
                channelId,
                new BundlePosition(2),
                NeighborDirection.TowardEndB),
            new NeighborRecord(
                channelId,
                new BundlePosition(2),
                channelId,
                new BundlePosition(1),
                NeighborDirection.TowardEndA)
        };
        var boundaries = new List<BoundaryFaceRecord>();
        for (uint position = 0; position < NodeCount; position++)
        {
            foreach (TopologyFace face in CardinalFaces)
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
        CoreTopology topology = Require(CoreTopology.TryCreate(
            1,
            NodeCount,
            new[] { channel }));
        return Require(SpatialStencil.TryCreate(topology));
    }

    private static T Require<T>(ContractValidationResult<T> result)
        where T : class
    {
        if (!result.IsValid)
        {
            throw new InvalidOperationException(result.FirstDiagnostic.ToString());
        }

        return result.Value;
    }

    private readonly struct BenchmarkSettings
    {
        public BenchmarkSettings(
            int warmupIterations,
            int measurementIterations,
            bool profileMode,
            string profileManifestPath)
        {
            WarmupIterations = warmupIterations;
            MeasurementIterations = measurementIterations;
            ProfileMode = profileMode;
            ProfileManifestPath = profileManifestPath;
        }

        public int WarmupIterations { get; }

        public int MeasurementIterations { get; }

        public bool ProfileMode { get; }

        public string ProfileManifestPath { get; }

        public static BenchmarkSettings Parse(string[] args)
        {
            int warmupIterations = DefaultWarmupIterations;
            int measurementIterations = DefaultMeasurementIterations;
            string profileManifestPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                ProfileManifestRelativePath.Replace('/', Path.DirectorySeparatorChar));
            bool profileMode = false;
            int? warmupOverride = null;
            int? measurementOverride = null;
            for (int index = 0; index < args.Length; index++)
            {
                string option = args[index];
                if (option == "--profile")
                {
                    profileMode = true;
                    continue;
                }

                if (option != "--profile-manifest" &&
                    option != "--warmup" &&
                    option != "--measure")
                {
                    throw new ArgumentException("Unknown benchmark option: " + option);
                }

                if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
                {
                    throw new ArgumentException("Benchmark options require a value.");
                }

                string value = args[++index];
                if (option == "--profile-manifest")
                {
                    profileManifestPath = Path.GetFullPath(value);
                    continue;
                }

                if (!int.TryParse(
                        value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int iterationCount) ||
                    iterationCount <= 0)
                {
                    throw new ArgumentException("Benchmark iteration counts must be positive integers.");
                }

                if (option == "--warmup")
                {
                    warmupOverride = iterationCount;
                }
                else
                {
                    measurementOverride = iterationCount;
                }
            }

            if (warmupOverride.HasValue)
            {
                warmupIterations = warmupOverride.Value;
            }

            if (measurementOverride.HasValue)
            {
                measurementIterations = measurementOverride.Value;
            }

            if (profileMode &&
                (warmupOverride.HasValue || measurementOverride.HasValue))
            {
                throw new ArgumentException(
                    "The P9-T03 profile uses sampling counts from its versioned profile manifest; do not combine --profile with --warmup or --measure.");
            }

            return new BenchmarkSettings(
                profileMode ? 0 : warmupIterations,
                profileMode ? 0 : measurementIterations,
                profileMode,
                profileManifestPath);
        }
    }

    private sealed class ProfileDefinition
    {
        public ProfileDefinition(
            string profileId,
            string benchmarkManifestSha256,
            string profileManifestSha256,
            int warmupIterations,
            int sampleIterations,
            int nodeCount,
            int groupCount,
            int interiorEdgeCount,
            int boundaryFaceCount,
            int fluxStateBytes,
            string hotspotStatus,
            string dataMovementStatus,
            string measurementBoundary,
            string evidenceBoundary,
            string androidBaselineStatus)
        {
            ProfileId = profileId;
            BenchmarkManifestSha256 = benchmarkManifestSha256;
            ProfileManifestSha256 = profileManifestSha256;
            WarmupIterations = warmupIterations;
            SampleIterations = sampleIterations;
            NodeCount = nodeCount;
            GroupCount = groupCount;
            InteriorEdgeCount = interiorEdgeCount;
            BoundaryFaceCount = boundaryFaceCount;
            FluxStateBytes = fluxStateBytes;
            HotspotStatus = hotspotStatus;
            DataMovementStatus = dataMovementStatus;
            MeasurementBoundary = measurementBoundary;
            EvidenceBoundary = evidenceBoundary;
            AndroidBaselineStatus = androidBaselineStatus;
        }

        public string ProfileId { get; }

        public string BenchmarkManifestSha256 { get; }

        public string ProfileManifestSha256 { get; }

        public int WarmupIterations { get; }

        public int SampleIterations { get; }

        public int NodeCount { get; }

        public int GroupCount { get; }

        public int InteriorEdgeCount { get; }

        public int BoundaryFaceCount { get; }

        public int FluxStateBytes { get; }

        public string HotspotStatus { get; }

        public string DataMovementStatus { get; }

        public string MeasurementBoundary { get; }

        public string EvidenceBoundary { get; }

        public string AndroidBaselineStatus { get; }
    }

    private sealed class ProfileCaseResult
    {
        public string CaseId { get; set; } = string.Empty;

        public int SampleIterations { get; set; }

        public bool DeterministicRepeat { get; set; }

        public int SolverIterationCount { get; set; }

        public double FinalEigenvalue { get; set; }

        public double FinalTotalPowerW { get; set; }

        public double MeanMicrosecondsPerSolve { get; set; }

        public double MinimumMicrosecondsPerSolve { get; set; }

        public double MedianMicrosecondsPerSolve { get; set; }

        public double P95MicrosecondsPerSolve { get; set; }

        public double MaximumMicrosecondsPerSolve { get; set; }

        public double MeanAllocatedBytesPerSolve { get; set; }

        public long MinimumAllocatedBytesPerSolve { get; set; }

        public long MedianAllocatedBytesPerSolve { get; set; }

        public long P95AllocatedBytesPerSolve { get; set; }

        public long MaximumAllocatedBytesPerSolve { get; set; }
    }

    private sealed class BenchmarkReport
    {
        public string ScenarioId { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public int WarmupIterations { get; set; }

        public int MeasurementIterations { get; set; }

        public int NodeCount { get; set; }

        public bool DeterministicRepeat { get; set; }

        public int SolverIterationCount { get; set; }

        public double FinalEigenvalue { get; set; }

        public double FinalTotalPowerW { get; set; }

        public double ElapsedMilliseconds { get; set; }

        public double MeanMicrosecondsPerSolve { get; set; }

        public long AllocatedBytesTotal { get; set; }

        public double AllocatedBytesPerSolve { get; set; }

        public long StopwatchFrequency { get; set; }

        public string Framework { get; set; } = string.Empty;

        public string ProcessArchitecture { get; set; } = string.Empty;

        public string OperatingSystem { get; set; } = string.Empty;
    }

    private sealed class ProfileReport
    {
        public string Format { get; set; } = string.Empty;

        public string TaskId { get; set; } = string.Empty;

        public string ProfileId { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public int WarmupIterations { get; set; }

        public int SampleIterations { get; set; }

        public bool DeterministicRepeat { get; set; }

        public string BenchmarkManifestSha256 { get; set; } = string.Empty;

        public string ProfileManifestSha256 { get; set; } = string.Empty;

        public string SolverScenarioId { get; set; } = string.Empty;

        public string SolverMethodId { get; set; } = string.Empty;

        public int SolverIterationCount { get; set; }

        public double FinalEigenvalue { get; set; }

        public double FinalTotalPowerW { get; set; }

        public int NodeCount { get; set; }

        public int GroupCount { get; set; }

        public int InteriorEdgeCount { get; set; }

        public int BoundaryFaceCount { get; set; }

        public int FluxStateBytes { get; set; }

        public string HotspotStatus { get; set; } = string.Empty;

        public string DataMovementStatus { get; set; } = string.Empty;

        public string MeasurementBoundary { get; set; } = string.Empty;

        public string EvidenceBoundary { get; set; } = string.Empty;

        public ProfileCaseResult Case { get; set; } = new ProfileCaseResult();

        public long StopwatchFrequency { get; set; }

        public string Framework { get; set; } = string.Empty;

        public string ProcessArchitecture { get; set; } = string.Empty;

        public string OperatingSystem { get; set; } = string.Empty;

        public string AndroidBaselineStatus { get; set; } = string.Empty;
    }
}
