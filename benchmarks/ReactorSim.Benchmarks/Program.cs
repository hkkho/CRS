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
    private const int NodeCount = 3;
    private const double TargetPowerW = 0.9;
    private const int DefaultWarmupIterations = 10;
    private const int DefaultMeasurementIterations = 200;

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
        ValidateScenarioManifest();
        BenchmarkSettings settings = BenchmarkSettings.Parse(args);
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
        public BenchmarkSettings(int warmupIterations, int measurementIterations)
        {
            WarmupIterations = warmupIterations;
            MeasurementIterations = measurementIterations;
        }

        public int WarmupIterations { get; }

        public int MeasurementIterations { get; }

        public static BenchmarkSettings Parse(string[] args)
        {
            int warmupIterations = DefaultWarmupIterations;
            int measurementIterations = DefaultMeasurementIterations;
            for (int index = 0; index < args.Length; index++)
            {
                string option = args[index];
                if (option != "--warmup" && option != "--measure")
                {
                    throw new ArgumentException("Unknown benchmark option: " + option);
                }

                if (index + 1 >= args.Length ||
                    !int.TryParse(
                        args[++index],
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int value) ||
                    value <= 0)
                {
                    throw new ArgumentException("Benchmark iteration counts must be positive integers.");
                }

                if (option == "--warmup")
                {
                    warmupIterations = value;
                }
                else
                {
                    measurementIterations = value;
                }
            }

            return new BenchmarkSettings(warmupIterations, measurementIterations);
        }
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
}
