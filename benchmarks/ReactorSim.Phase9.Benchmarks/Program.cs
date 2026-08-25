using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using ReactorSim.Cli;

namespace ReactorSim.Phase9.Benchmarks;

internal static class Program
{
    private const string ManifestRelativePath =
        "benchmarks/P9-T01-cli-gameplay-benchmark-v1.json";
    private const string ExpectedFormat = "reactorsim.p9-cli-gameplay-benchmark/v1";
    private const string ExpectedTaskId = "P9-T01";
    private const string ExpectedStatus = "synthetic_observation_only";
    private const string ExpectedBenchmarkId = "p9-t01-cli-gameplay-v1";
    private const string ExpectedScenarioParameterSha256 =
        "80981452f4808fae9e2c8341fc32e88640b446d386f9dbb7726c1550a2ff51a2";
    private const string ExpectedScoringParameterSha256 =
        "4b0f6d0aa3336b0560ca763bfdbf5012151289bbe9122cb1126c13f86086b91c";
    private const string ExpectedPolicyParameterSha256 =
        "d0e6dec7199751ca0f5d5418892fe0210831e46153ff445ec13e4e84ddf49d39";
    private const int MaximumCaseCount = 8;
    private const int MaximumCommandCountPerCase = 32;

    private static readonly string[] ExpectedCaseIds =
    {
        "steady-survival-10x",
        "controlled-response-10x",
        "boundary-loss-10x"
    };

    private static readonly string[] ExpectedCasePurposes =
    {
        "Complete the approved tutorial survival horizon without player actions.",
        "Queue power and tilt targets and observe scored response before the horizon.",
        "Reach the approved operating-envelope loss and expose its explanation."
    };

    private static readonly string[][] ExpectedCommandStreams =
    {
        new[]
        {
            "new run tutorial-equilibrium play-accelerated-10x",
            "advance wall 60000",
            "inspect score",
            "quit"
        },
        new[]
        {
            "new run centered-power-perturbation play-accelerated-10x",
            "set power target 0.95",
            "advance wall 100",
            "set tilt target 0.1",
            "advance wall 100",
            "inspect score",
            "quit"
        },
        new[]
        {
            "new run operating-envelope-boundary play-accelerated-10x",
            "advance wall 1000",
            "inspect score",
            "quit"
        }
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
            BenchmarkSettings commandLine = BenchmarkSettings.Parse(args);
            BenchmarkManifest manifest = LoadManifest(commandLine.ManifestPath);
            BenchmarkSettings settings = commandLine.WithManifestDefaults(
                manifest.WarmupIterations,
                manifest.MeasurementIterations);
            var cases = new List<BenchmarkCaseResult>();

            foreach (BenchmarkCase benchmarkCase in manifest.Cases)
            {
                AssertDeterministic(benchmarkCase);
            }

            for (int warmupIndex = 0; warmupIndex < settings.WarmupIterations; warmupIndex++)
            {
                foreach (BenchmarkCase benchmarkCase in manifest.Cases)
                {
                    ExecuteWithoutCapture(benchmarkCase);
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            foreach (BenchmarkCase benchmarkCase in manifest.Cases)
            {
                cases.Add(Measure(benchmarkCase, settings.MeasurementIterations));
            }

            var report = new BenchmarkReport
            {
                Format = "reactorsim.p9-cli-gameplay-benchmark-result/v1",
                TaskId = ExpectedTaskId,
                BenchmarkId = manifest.BenchmarkId,
                Status = "PASS",
                WarmupIterations = settings.WarmupIterations,
                MeasurementIterations = settings.MeasurementIterations,
                DeterministicRepeat = true,
                ScenarioParameterSha256 = manifest.ScenarioParameterSha256,
                ScoringParameterSha256 = manifest.ScoringParameterSha256,
                PolicyParameterSha256 = manifest.PolicyParameterSha256,
                Cases = cases,
                StopwatchFrequency = Stopwatch.Frequency,
                Framework = RuntimeInformation.FrameworkDescription,
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                OperatingSystem = RuntimeInformation.OSDescription,
                AndroidBaselineStatus = manifest.AndroidBaselineStatus
            };

            Console.WriteLine(JsonSerializer.Serialize(report, JsonOptions));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("P9-T01 benchmark failed: " + exception.Message);
            return 1;
        }
    }

    private static BenchmarkManifest LoadManifest(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            throw new InvalidOperationException(
                "The benchmark must run from a repository root containing " +
                ManifestRelativePath + ".");
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = document.RootElement;
        RequireString(root, "format", ExpectedFormat);
        RequireString(root, "task_id", ExpectedTaskId);
        RequireString(root, "status", ExpectedStatus);
        string benchmarkId = ReadNonEmptyString(root, "benchmark_id");
        RequireStringValue(benchmarkId, ExpectedBenchmarkId, "benchmark_id");
        string scenarioParameterSha256 = ReadNonEmptyString(root, "scenario_parameter_sha256");
        string scoringParameterSha256 = ReadNonEmptyString(root, "scoring_parameter_sha256");
        string policyParameterSha256 = ReadNonEmptyString(root, "policy_parameter_sha256");
        RequireStringValue(
            scenarioParameterSha256,
            ExpectedScenarioParameterSha256,
            "scenario_parameter_sha256");
        RequireStringValue(
            scoringParameterSha256,
            ExpectedScoringParameterSha256,
            "scoring_parameter_sha256");
        RequireStringValue(
            policyParameterSha256,
            ExpectedPolicyParameterSha256,
            "policy_parameter_sha256");
        RequireFileHash(
            "data/scenarios/p8-t02-scenario-difficulty-parameters-v1.json",
            scenarioParameterSha256);
        RequireFileHash(
            "data/scenarios/p8-t03-scoring-parameters-v1.json",
            scoringParameterSha256);
        RequireFileHash(
            "data/scenarios/p8-t05-scripted-policy-parameters-v1.json",
            policyParameterSha256);

        JsonElement measurement = RequireObject(root, "measurement");
        int warmupIterations = ReadPositiveInt(measurement, "warmup_iterations");
        int measurementIterations = ReadPositiveInt(measurement, "measured_iterations");
        RequireString(measurement, "clock", "System.Diagnostics.Stopwatch");
        RequireString(
            measurement,
            "allocation_counter",
            "GC.GetAllocatedBytesForCurrentThread");
        if (measurement.GetProperty("performance_target").ValueKind != JsonValueKind.Null)
        {
            throw new InvalidOperationException(
                "The P9-T01 manifest must not select a performance target.");
        }
        RequireString(
            measurement,
            "result_boundary",
            "machine_specific_observation_only");

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
                "The P9-T01 manifest may not claim an Android baseline without device evidence.");
        }
        RequireString(
            android,
            "reason",
            "No adb command or representative Android device is available in the execution environment.");
        RequireString(android, "owner_gate", "G9");

        JsonElement caseArray = root.GetProperty("cases");
        if (caseArray.ValueKind != JsonValueKind.Array ||
            caseArray.GetArrayLength() == 0 ||
            caseArray.GetArrayLength() > MaximumCaseCount)
        {
            throw new InvalidOperationException(
                "The P9-T01 manifest must contain a bounded nonempty case array.");
        }

        var cases = new List<BenchmarkCase>();
        var caseIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement caseElement in caseArray.EnumerateArray())
        {
            if (caseElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("Every P9-T01 case must be an object.");
            }

            string caseId = ReadNonEmptyString(caseElement, "case_id");
            if (!caseIds.Add(caseId))
            {
                throw new InvalidOperationException(
                    "The P9-T01 manifest contains duplicate case_id " + caseId + ".");
            }

            string purpose = ReadNonEmptyString(caseElement, "purpose");
            JsonElement commandsElement = caseElement.GetProperty("command_stream");
            if (commandsElement.ValueKind != JsonValueKind.Array ||
                commandsElement.GetArrayLength() == 0 ||
                commandsElement.GetArrayLength() > MaximumCommandCountPerCase)
            {
                throw new InvalidOperationException(
                    "The P9-T01 command stream is missing or exceeds its capacity.");
            }

            var commands = new List<string>();
            foreach (JsonElement commandElement in commandsElement.EnumerateArray())
            {
                if (commandElement.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidOperationException(
                        "Every P9-T01 command stream entry must be a string.");
                }

                string command = commandElement.GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(command) || command.Contains('\r') || command.Contains('\n'))
                {
                    throw new InvalidOperationException(
                        "A P9-T01 command must be a nonempty single line.");
                }

                commands.Add(command);
            }

            if (!string.Equals(commands[0].Split(' ')[0], "new", StringComparison.Ordinal) ||
                !string.Equals(commands[commands.Count - 1], "quit", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Every P9-T01 command stream must start with new and end with quit.");
            }

            cases.Add(new BenchmarkCase(caseId, purpose, commands));
        }

        ValidateApprovedCases(cases);

        return new BenchmarkManifest(
            benchmarkId,
            scenarioParameterSha256,
            scoringParameterSha256,
            policyParameterSha256,
            androidStatus,
            cases,
            warmupIterations,
            measurementIterations);
    }

    private static void ValidateApprovedCases(IReadOnlyList<BenchmarkCase> cases)
    {
        if (cases.Count != ExpectedCaseIds.Length)
        {
            throw new InvalidOperationException(
                "The P9-T01 manifest must contain exactly the approved case set.");
        }

        for (int caseIndex = 0; caseIndex < ExpectedCaseIds.Length; caseIndex++)
        {
            BenchmarkCase actual = cases[caseIndex];
            if (!string.Equals(
                    actual.CaseId,
                    ExpectedCaseIds[caseIndex],
                    StringComparison.Ordinal) ||
                !string.Equals(
                    actual.Purpose,
                    ExpectedCasePurposes[caseIndex],
                    StringComparison.Ordinal) ||
                actual.Commands.Count != ExpectedCommandStreams[caseIndex].Length)
            {
                throw new InvalidOperationException(
                    "The P9-T01 case identity or purpose is not the approved value: " +
                    ExpectedCaseIds[caseIndex] + ".");
            }

            for (int commandIndex = 0; commandIndex < actual.Commands.Count; commandIndex++)
            {
                if (!string.Equals(
                        actual.Commands[commandIndex],
                        ExpectedCommandStreams[caseIndex][commandIndex],
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The P9-T01 command stream is not the approved value: " +
                        ExpectedCaseIds[caseIndex] + ".");
                }
            }
        }
    }

    private static void AssertDeterministic(BenchmarkCase benchmarkCase)
    {
        CapturedRun first = ExecuteWithCapture(benchmarkCase);
        CapturedRun second = ExecuteWithCapture(benchmarkCase);
        if (first.ExitCode != 0 || second.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "The benchmark case did not complete successfully: " + benchmarkCase.CaseId + ".");
        }

        if (!string.Equals(first.Output, second.Output, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The benchmark case output was not exactly deterministic: " +
                benchmarkCase.CaseId + ".");
        }
    }

    private static BenchmarkCaseResult Measure(BenchmarkCase benchmarkCase, int iterations)
    {
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long timestampBefore = Stopwatch.GetTimestamp();
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            ExecuteWithoutCapture(benchmarkCase);
        }

        long elapsedTicks = Stopwatch.GetTimestamp() - timestampBefore;
        long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        double elapsedMilliseconds = elapsedTicks * 1000.0 / Stopwatch.Frequency;
        double meanMicroseconds = elapsedTicks * 1_000_000.0 /
            Stopwatch.Frequency /
            iterations;
        return new BenchmarkCaseResult
        {
            CaseId = benchmarkCase.CaseId,
            Purpose = benchmarkCase.Purpose,
            ExitCode = 0,
            DeterministicRepeat = true,
            MeasurementIterations = iterations,
            ElapsedMilliseconds = elapsedMilliseconds,
            MeanMicrosecondsPerExecution = meanMicroseconds,
            AllocatedBytesTotal = allocatedBytes,
            AllocatedBytesPerExecution = (double)allocatedBytes / iterations
        };
    }

    private static void ExecuteWithoutCapture(BenchmarkCase benchmarkCase)
    {
        CapturedRun run = Execute(
            benchmarkCase,
            TextWriter.Null,
            TextWriter.Null);
        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "The benchmark case returned exit code " +
                run.ExitCode.ToString(CultureInfo.InvariantCulture) + ": " +
                benchmarkCase.CaseId + ".");
        }
    }

    private static CapturedRun ExecuteWithCapture(BenchmarkCase benchmarkCase)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        return Execute(benchmarkCase, output, error);
    }

    private static CapturedRun Execute(
        BenchmarkCase benchmarkCase,
        TextWriter output,
        TextWriter error)
    {
        string commandText = string.Join("\n", benchmarkCase.Commands) + "\n";
        int exitCode = CliApplication.Run(
            new StringReader(commandText),
            output,
            error);
        return new CapturedRun(exitCode, output is StringWriter stringWriter
            ? stringWriter.ToString()
            : string.Empty);
    }

    private static JsonElement RequireObject(JsonElement parent, string propertyName)
    {
        JsonElement value = parent.GetProperty(propertyName);
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException(
                "The P9-T01 property must be an object: " + propertyName + ".");
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
                "The P9-T01 property must be a nonempty string: " + propertyName + ".");
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
                "The P9-T01 property must be a positive integer: " + propertyName + ".");
        }

        return parsed;
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
                "The P9-T01 property " + propertyName + " does not match the approved value.");
        }
    }

    private static void RequireFileHash(string relativePath, string expectedHash)
    {
        string path = Path.Combine(
            Directory.GetCurrentDirectory(),
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                "The P9-T01 bound artifact is missing: " + relativePath + ".");
        }

        string actualHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(path)))
            .ToLowerInvariant();
        if (!string.Equals(actualHash, expectedHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The P9-T01 bound artifact hash does not match: " + relativePath + ".");
        }
    }

    private sealed class BenchmarkManifest
    {
        public BenchmarkManifest(
            string benchmarkId,
            string scenarioParameterSha256,
            string scoringParameterSha256,
            string policyParameterSha256,
            string androidBaselineStatus,
            IReadOnlyList<BenchmarkCase> cases,
            int warmupIterations,
            int measurementIterations)
        {
            BenchmarkId = benchmarkId;
            ScenarioParameterSha256 = scenarioParameterSha256;
            ScoringParameterSha256 = scoringParameterSha256;
            PolicyParameterSha256 = policyParameterSha256;
            AndroidBaselineStatus = androidBaselineStatus;
            Cases = cases;
            WarmupIterations = warmupIterations;
            MeasurementIterations = measurementIterations;
        }

        public string BenchmarkId { get; }

        public string ScenarioParameterSha256 { get; }

        public string ScoringParameterSha256 { get; }

        public string PolicyParameterSha256 { get; }

        public string AndroidBaselineStatus { get; }

        public IReadOnlyList<BenchmarkCase> Cases { get; }

        public int WarmupIterations { get; }

        public int MeasurementIterations { get; }
    }

    private sealed class BenchmarkCase
    {
        public BenchmarkCase(string caseId, string purpose, IReadOnlyList<string> commands)
        {
            CaseId = caseId;
            Purpose = purpose;
            Commands = commands;
        }

        public string CaseId { get; }

        public string Purpose { get; }

        public IReadOnlyList<string> Commands { get; }
    }

    private readonly struct CapturedRun
    {
        public CapturedRun(int exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output;
        }

        public int ExitCode { get; }

        public string Output { get; }
    }

    private readonly struct BenchmarkSettings
    {
        public BenchmarkSettings(
            string manifestPath,
            int warmupIterations,
            int measurementIterations)
        {
            ManifestPath = manifestPath;
            WarmupIterations = warmupIterations;
            MeasurementIterations = measurementIterations;
        }

        public string ManifestPath { get; }

        public int WarmupIterations { get; }

        public int MeasurementIterations { get; }

        public static BenchmarkSettings Parse(
            string[] args)
        {
            string manifestPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                ManifestRelativePath.Replace('/', Path.DirectorySeparatorChar));
            int? warmupIterations = null;
            int? measurementIterations = null;
            for (int index = 0; index < args.Length; index++)
            {
                string option = args[index];
                if (option != "--manifest" && option != "--warmup" && option != "--measure")
                {
                    throw new ArgumentException("Unknown benchmark option: " + option);
                }

                if (index + 1 >= args.Length ||
                    string.IsNullOrWhiteSpace(args[index + 1]))
                {
                    throw new ArgumentException("Benchmark options require a value.");
                }

                string value = args[++index];
                if (option == "--manifest")
                {
                    manifestPath = Path.GetFullPath(value);
                }
                else if (!int.TryParse(
                             value,
                             NumberStyles.Integer,
                             CultureInfo.InvariantCulture,
                             out int iterationCount) ||
                         iterationCount <= 0)
                {
                    throw new ArgumentException("Benchmark iteration counts must be positive integers.");
                }
                else if (option == "--warmup")
                {
                    warmupIterations = iterationCount;
                }
                else
                {
                    measurementIterations = iterationCount;
                }
            }

            return new BenchmarkSettings(
                manifestPath,
                warmupIterations ?? 0,
                measurementIterations ?? 0);
        }

        public BenchmarkSettings WithManifestDefaults(
            int defaultWarmupIterations,
            int defaultMeasurementIterations)
        {
            return new BenchmarkSettings(
                ManifestPath,
                WarmupIterations == 0 ? defaultWarmupIterations : WarmupIterations,
                MeasurementIterations == 0 ? defaultMeasurementIterations : MeasurementIterations);
        }
    }

    private sealed class BenchmarkCaseResult
    {
        public string CaseId { get; set; } = string.Empty;

        public string Purpose { get; set; } = string.Empty;

        public int ExitCode { get; set; }

        public bool DeterministicRepeat { get; set; }

        public int MeasurementIterations { get; set; }

        public double ElapsedMilliseconds { get; set; }

        public double MeanMicrosecondsPerExecution { get; set; }

        public long AllocatedBytesTotal { get; set; }

        public double AllocatedBytesPerExecution { get; set; }
    }

    private sealed class BenchmarkReport
    {
        public string Format { get; set; } = string.Empty;

        public string TaskId { get; set; } = string.Empty;

        public string BenchmarkId { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public int WarmupIterations { get; set; }

        public int MeasurementIterations { get; set; }

        public bool DeterministicRepeat { get; set; }

        public string ScenarioParameterSha256 { get; set; } = string.Empty;

        public string ScoringParameterSha256 { get; set; } = string.Empty;

        public string PolicyParameterSha256 { get; set; } = string.Empty;

        public List<BenchmarkCaseResult> Cases { get; set; } = new List<BenchmarkCaseResult>();

        public long StopwatchFrequency { get; set; }

        public string Framework { get; set; } = string.Empty;

        public string ProcessArchitecture { get; set; } = string.Empty;

        public string OperatingSystem { get; set; } = string.Empty;

        public string AndroidBaselineStatus { get; set; } = string.Empty;
    }
}
