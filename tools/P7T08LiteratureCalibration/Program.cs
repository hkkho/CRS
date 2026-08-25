using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

const string DefinitionFormat = "reactorsim.p7-t08-literature-calibrated-definition/v1";
const string ArtifactFormat = "reactorsim.p7-t08-literature-calibrated-synthetic/v1";
const string ManifestFormat = "reactorsim.p7-t08-literature-calibrated-synthetic-manifest/v1";
const string TaskId = "P7-T08";
const string SourceManifestSha = "df9b06987dc831b61c2f75daf368628d697b5ab1b0605466134ee0c73bbd25a1";
const string ArtifactId = "p7-t08-literature-calibrated-synthetic-v1";

if (args.Length != 5 || !IsCommand(args[0]))
{
    Console.Error.WriteLine("Usage: generate|validate <definition.json> <artifact.json> <manifest.json> candidate|approved");
    return 2;
}

string command = args[0].ToLowerInvariant();
string definitionPath = Path.GetFullPath(args[1]);
string artifactPath = Path.GetFullPath(args[2]);
string manifestPath = Path.GetFullPath(args[3]);
string disposition = args[4].ToLowerInvariant();
if (disposition is not ("candidate" or "approved"))
{
    Console.Error.WriteLine("P7_T08_LITERATURE_FAIL disposition must be candidate or approved");
    return 2;
}

try
{
    return command == "generate"
        ? Generate(definitionPath, artifactPath, manifestPath, disposition)
        : Validate(definitionPath, artifactPath, manifestPath, disposition);
}
catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or FormatException)
{
    Console.Error.WriteLine($"P7_T08_LITERATURE_FAIL {exception.Message}");
    return 1;
}

static bool IsCommand(string value) => string.Equals(value, "generate", StringComparison.OrdinalIgnoreCase) || string.Equals(value, "validate", StringComparison.OrdinalIgnoreCase);

static int Generate(string definitionPath, string artifactPath, string manifestPath, string disposition)
{
    EnsureFiles(definitionPath);
    byte[] definitionBytes = File.ReadAllBytes(definitionPath);
    using JsonDocument definition = JsonDocument.Parse(definitionBytes);
    VerifySourceManifest(definition.RootElement, definitionPath);
    string definitionSha = Sha256(definitionBytes);
    byte[] artifactBytes = BuildArtifact(definition.RootElement, disposition, definitionSha);
    string artifactSha = Sha256(artifactBytes);
    string repeatSha = Sha256(BuildArtifact(definition.RootElement, disposition, definitionSha));
    using JsonDocument artifact = JsonDocument.Parse(artifactBytes);
    (double maximumScheduleBalance, double minimumNonnegative) = MeasureArtifact(artifact.RootElement);

    Directory.CreateDirectory(Path.GetDirectoryName(artifactPath)!);
    Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
    File.WriteAllBytes(artifactPath, artifactBytes);
    File.WriteAllBytes(manifestPath, Serialize(Manifest(disposition, definitionSha, artifactSha, artifactBytes.Length, repeatSha, maximumScheduleBalance, minimumNonnegative)));

    Console.WriteLine($"P7_T08_LITERATURE_GENERATE_PASS cases=4 artifact_bytes={artifactBytes.Length} artifact_sha256={artifactSha} status={StatusLabel(disposition)}");
    return 0;
}

static int Validate(string definitionPath, string artifactPath, string manifestPath, string disposition)
{
    EnsureFiles(definitionPath, artifactPath, manifestPath);
    byte[] definitionBytes = File.ReadAllBytes(definitionPath);
    byte[] artifactBytes = File.ReadAllBytes(artifactPath);
    string definitionSha = Sha256(definitionBytes);
    byte[] expectedArtifactBytes;
    using (JsonDocument definition = JsonDocument.Parse(definitionBytes))
    {
        VerifySourceManifest(definition.RootElement, definitionPath);
        expectedArtifactBytes = BuildArtifact(definition.RootElement, disposition, definitionSha);
    }

    using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
    using JsonDocument artifact = JsonDocument.Parse(artifactBytes);
    List<string> errors = [];
    string artifactSha = Sha256(artifactBytes);
    string repeatSha = Sha256(expectedArtifactBytes);
    (double maximumScheduleBalance, double minimumNonnegative) = MeasureArtifact(artifact.RootElement);

    RequireBytes(artifactBytes, expectedArtifactBytes, errors, "artifact bytes differ from independent regeneration");
    RequireString(artifact.RootElement, "format", ArtifactFormat, errors);
    RequireString(artifact.RootElement, "task_id", TaskId, errors);
    RequireString(artifact.RootElement, "artifact_id", ArtifactId, errors);
    RequireString(artifact.RootElement, "source_manifest_sha256", SourceManifestSha, errors);
    RequireString(artifact.RootElement, "artifact_status", ArtifactStatus(disposition), errors);
    RequireString(artifact.RootElement, "evidence_approval", EvidenceApproval(disposition), errors);
    RequireString(artifact.RootElement, "coverage_class", "SyntheticLiteratureCalibrated", errors);
    RequireString(artifact.RootElement, "golden_status", GoldenStatus(disposition), errors);
    RequireString(artifact.RootElement, "comparison_status", ComparisonStatus(disposition), errors);
    RequireString(artifact.RootElement, "runtime_use", "GoldenDataAuditOnly;CoreMappingProhibited", errors);
    if (!artifact.RootElement.TryGetProperty("licensing_decision", out JsonElement licensing) || licensing.GetProperty("source_artifacts_redistributed").GetBoolean() || licensing.GetProperty("full_source_outputs_or_figures_redistributed").GetBoolean() || licensing.GetProperty("derived_source_run_claimed").GetBoolean())
    {
        errors.Add("licensing decision must remain facts-only with no source artifact/output redistribution or derived-run claim");
    }
    ValidateCases(artifact.RootElement, errors);
    ValidateManifest(manifest.RootElement, disposition, definitionSha, artifactSha, artifactBytes.Length, repeatSha, maximumScheduleBalance, minimumNonnegative, errors);

    if (errors.Count > 0)
    {
        Console.Error.WriteLine("P7_T08_LITERATURE_VALIDATE_FAIL");
        foreach (string error in errors)
        {
            Console.Error.WriteLine($"- {error}");
        }

        return 1;
    }

    Console.WriteLine($"P7_T08_LITERATURE_VALIDATE_PASS cases=4 artifact_bytes={artifactBytes.Length} artifact_sha256={artifactSha} status={StatusLabel(disposition)}");
    return 0;
}

static Dictionary<string, object?> Manifest(string disposition, string definitionSha, string artifactSha, int artifactBytes, string repeatSha, double maximumScheduleBalance, double minimumNonnegative)
{
    return Object(
        ("format", ManifestFormat),
        ("task_id", TaskId),
        ("artifact_id", ArtifactId),
        ("disposition", disposition == "approved" ? "approved_golden_synthetic_only" : "candidate_deferred"),
        ("artifact_status", ArtifactStatus(disposition)),
        ("definition_sha256", definitionSha),
        ("artifact_sha256", artifactSha),
        ("artifact_byte_length", artifactBytes),
        ("generator_id", "p7-t08-independent-literature-calibration"),
        ("generator_version", "v1"),
        ("research_report", "docs/research/P7-T08-literature-calibration.md"),
        ("coverage_class", "SyntheticLiteratureCalibrated"),
        ("source_manifest_sha256", SourceManifestSha),
        ("case_count", 4),
        ("independent_repeat_equal", string.Equals(artifactSha, repeatSha, StringComparison.Ordinal)),
        ("independent_repeat_sha256", repeatSha),
        ("maximum_schedule_balance_absolute", maximumScheduleBalance),
        ("minimum_nonnegative_value", minimumNonnegative),
        ("manifest_binding", "definition bytes, generated artifact bytes, and independent repeat are compared exactly"));
}

static byte[] BuildArtifact(JsonElement definition, string disposition, string definitionSha)
{
    RequireDefinition(definition);
    List<object?> cases = [];
    foreach (JsonElement definitionCase in definition.GetProperty("cases").EnumerateArray())
    {
        string kind = String(definitionCase, "case_kind");
        cases.Add(kind switch
        {
            "lattice_k_effective" => BuildLatticeCase(definitionCase),
            "lattice_depletion" => BuildDepletionCase(definitionCase),
            "full_core_schedule_and_summary" => BuildAtfCase(definitionCase),
            "full_core_refueling_summary" => BuildRefuelingCase(definitionCase),
            _ => throw new InvalidOperationException($"unsupported case kind: {kind}")
        });
    }

    return Serialize(Object(
        ("format", ArtifactFormat),
        ("task_id", TaskId),
        ("artifact_id", ArtifactId),
        ("artifact_status", ArtifactStatus(disposition)),
        ("evidence_approval", EvidenceApproval(disposition)),
        ("comparison_status", ComparisonStatus(disposition)),
        ("golden_status", GoldenStatus(disposition)),
        ("coverage_class", "SyntheticLiteratureCalibrated"),
        ("runtime_use", "GoldenDataAuditOnly;CoreMappingProhibited"),
        ("approval_scope", "Synthetic/test-only reported-target and theory-calibration consumer; not a direct CANDU physics baseline or production validation authority."),
        ("definition_id", String(definition, "definition_id")),
        ("definition_sha256", definitionSha),
        ("source_manifest", String(definition, "source_manifest")),
        ("source_manifest_sha256", SourceManifestSha),
        ("source_authority", Object(
            ("kind", "project_authored_synthetic_calibration"),
            ("external_case_status", "not_admitted_deferred"),
            ("source_values", "ReportedTargetOrComputedTheoryOnly"),
            ("license", "External source artifacts remain outside the repository; only concise attributed facts are represented."),
            ("restriction", "Do not use as a direct CANDU physics baseline, production tolerance, or external solver reproduction."))),
        ("licensing_decision", Clone(definition.GetProperty("licensing_decision"))),
        ("calibration_policy", Object(
            ("k_effective", "minimax midpoint of the printed S4 DRAGON/SERPENT pair"),
            ("burnup", "independent constant-power energy conversion compared with the rounded S5 report"),
            ("full_core", "deterministic schedule plus reported aggregate target replay"),
            ("physical_tolerances", "NotSelected"))),
        ("cases", cases)));
}

static Dictionary<string, object?> BuildLatticeCase(JsonElement definitionCase)
{
    JsonElement inputs = definitionCase.GetProperty("inputs");
    double dragon = Number(inputs, "reported_dragon_k_effective");
    double serpent = Number(inputs, "reported_serpent_k_effective");
    double synthetic = (dragon + serpent) / 2.0;
    double deltaDragon = synthetic - dragon;
    double deltaSerpent = synthetic - serpent;
    return CaseHeader(definitionCase, Object(
        ("input_snapshot", Clone(inputs)),
        ("outputs", Object(
            ("synthetic_k_effective", synthetic),
            ("calibration_method", "minimax midpoint of the two printed k-effective values"),
            ("reported_pair_span_mk", (serpent - dragon) * 1000.0),
            ("reported_serpent_sigma_mk", Number(inputs, "reported_serpent_statistical_sigma") * 1000.0))),
        ("comparisons", new List<object?>
        {
            Comparison("S4-R06/DRAGON4", dragon, synthetic, "1", "reported printed value", deltaDragon),
            Comparison("S4-R06/SERPENT", serpent, synthetic, "1", "reported printed value with published statistical sigma", deltaSerpent)
        }),
        ("interpretation", "The midpoint minimizes the maximum absolute distance to the two published method outputs. It does not reproduce either solver and no physical tolerance is selected.")));
}

static Dictionary<string, object?> BuildDepletionCase(JsonElement definitionCase)
{
    JsonElement inputs = definitionCase.GetProperty("inputs");
    int days = Integer(inputs, "duration_days");
    double power = Number(inputs, "constant_power_density_kw_per_kg");
    double hoursPerDay = Number(inputs, "hours_per_day");
    double kwhPerGwd = Number(inputs, "kwh_per_kg_per_gwd_per_t");
    double syntheticExit = power * days * hoursPerDay / kwhPerGwd;
    List<object?> history = [];
    for (int day = 0; day <= days; day++)
    {
        history.Add(Object(
            ("day", day),
            ("power_density_kw_per_kg", power),
            ("burnup_gwd_per_t", power * day * hoursPerDay / kwhPerGwd)));
    }

    double reportedExit = Number(inputs, "reported_exit_burnup_gwd_per_t");
    return CaseHeader(definitionCase, Object(
        ("input_snapshot", Clone(inputs)),
        ("outputs", Object(
            ("history", history),
            ("synthetic_exit_burnup_gwd_per_t", syntheticExit),
            ("energy_balance_identity", "power_density_kw_per_kg * duration_days * hours_per_day / 24000 kWh_per_kg_per_GWd_per_t"),
            ("reported_k_difference_upper_bound_mk", Number(inputs, "reported_k_difference_upper_bound_mk")))),
        ("comparisons", new List<object?>
        {
            Comparison("S5-R05/reported_exit_burnup", reportedExit, syntheticExit, "GWd/t", "reported rounded target", syntheticExit - reportedExit)
        }),
        ("interpretation", "The independently computed energy balance is 9.59139 GWd/t versus the reported rounded 9.6 GWd/t. The difference is reported, not converted into a tolerance or used to change runtime physics.")));
}

static Dictionary<string, object?> BuildAtfCase(JsonElement definitionCase)
{
    JsonElement inputs = definitionCase.GetProperty("inputs");
    int days = Integer(inputs, "duration_full_power_days");
    int dailyChannels = Integer(inputs, "refueling_rate_channels_per_day");
    int shiftBundles = Integer(inputs, "refueling_shift_bundles");
    double boronMidpoint = (Number(inputs, "reported_enriched_boron_delta_min_ppm") + Number(inputs, "reported_enriched_boron_delta_max_ppm")) / 2.0;
    List<object?> history = [];
    for (int day = 0; day <= days; day++)
    {
        int cumulativeChannels = day * dailyChannels;
        history.Add(Object(
            ("full_power_day", day),
            ("refueled_channels_today", day == 0 ? 0 : dailyChannels),
            ("cumulative_refueled_channels", cumulativeChannels),
            ("bundle_shift_events_today", day == 0 ? 0 : dailyChannels),
            ("cumulative_shifted_bundles", cumulativeChannels * shiftBundles),
            ("synthetic_reactivity_delta_mk", Number(inputs, "reported_sic_reactivity_delta_mk")),
            ("synthetic_enriched_boron_delta_ppm", boronMidpoint),
            ("synthetic_enriched_channel_power_delta_kw", -Number(inputs, "reported_enriched_channel_power_reduction_kw"))));
    }

    double reactivity = Number(inputs, "reported_sic_reactivity_delta_mk");
    double minBoron = Number(inputs, "reported_enriched_boron_delta_min_ppm");
    double maxBoron = Number(inputs, "reported_enriched_boron_delta_max_ppm");
    double powerReduction = -Number(inputs, "reported_enriched_channel_power_reduction_kw");
    return CaseHeader(definitionCase, Object(
        ("input_snapshot", Clone(inputs)),
        ("outputs", Object(
            ("history", history),
            ("synthetic_reactivity_delta_mk", reactivity),
            ("synthetic_enriched_boron_delta_ppm", boronMidpoint),
            ("synthetic_enriched_channel_power_delta_kw", powerReduction),
            ("total_refueled_channels", days * dailyChannels),
            ("total_shifted_bundles", days * dailyChannels * shiftBundles))),
        ("comparisons", new List<object?>
        {
            IntervalComparison("S1-abstract/reactivity", reactivity, reactivity, reactivity, "mk", "reported summary replay"),
            IntervalComparison("S1-abstract/enriched_boron", boronMidpoint, minBoron, maxBoron, "ppm", "midpoint of reported interval"),
            Comparison("S1-abstract/enriched_channel_power", powerReduction, powerReduction, "kW", "reported summary replay", 0)
        }),
        ("interpretation", "Schedule quantities are reproduced as a deterministic synthetic history. Summary values are attributed reported targets, not a source solver output or a direct mapping to the repository Core.")));
}

static Dictionary<string, object?> BuildRefuelingCase(JsonElement definitionCase)
{
    JsonElement inputs = definitionCase.GetProperty("inputs");
    JsonElement reference = inputs.GetProperty("aggregate_reference");
    JsonElement reported = inputs.GetProperty("aggregate_reported_simulation");
    string[] metricNames =
    [
        "maximum_channel_power_kw",
        "maximum_bundle_power_kw",
        "rippled_channel_power_over_reference",
        "average_zone_level",
        "refueling_rate_channels_per_day",
        "exit_burnup_zone_1_mwd_per_t",
        "exit_burnup_zone_2_mwd_per_t"
    ];
    List<object?> metrics = [];
    List<object?> comparisons = [];
    foreach (string metric in metricNames)
    {
        double referenceValue = Number(reference, metric);
        double reportedValue = Number(reported, metric);
        metrics.Add(Object(
            ("metric", metric),
            ("reference_value", referenceValue),
            ("reported_simulation_value", reportedValue),
            ("synthetic_value", reportedValue)));
        comparisons.Add(Object(
            ("observable", metric),
            ("unit", UnitFor(metric)),
            ("reference_value", referenceValue),
            ("reported_simulation_value", reportedValue),
            ("synthetic_value", reportedValue),
            ("difference_to_reported_simulation", 0),
            ("difference_to_time_average_reference", reportedValue - referenceValue),
            ("relative_difference_to_time_average_percent", RelativePercent(referenceValue, reportedValue)),
            ("comparison_status", "reported aggregate replay; not independent reproduction")));
    }

    int days = Integer(inputs, "duration_days");
    int totalEvents = 0;
    List<object?> history = [];
    for (int day = 0; day <= days; day++)
    {
        int events = day == 0 ? 0 : ((day - 1) % 25 < 2 ? 5 : 4);
        totalEvents += events;
        history.Add(Object(
            ("day", day),
            ("synthetic_refueling_events_today", events),
            ("cumulative_refueling_events", totalEvents),
            ("bundle_shift", Integer(inputs, "bundle_shift")),
            ("cumulative_shifted_bundles", totalEvents * Integer(inputs, "bundle_shift"))));
    }

    return CaseHeader(definitionCase, Object(
        ("input_snapshot", Clone(inputs)),
        ("outputs", Object(
            ("aggregate_metrics", metrics),
            ("history", history),
            ("synthetic_total_refueling_events", totalEvents),
            ("synthetic_average_refueling_rate_channels_per_day", totalEvents / (double)days))),
        ("comparisons", comparisons),
        ("interpretation", "The seven small aggregate values are replayed as a deterministic synthetic data-contract fixture. The schedule is constructed to reproduce the reported 4.08 channels/day average, not to claim the unavailable DONJON4/CANFUEL run.")));
}

static Dictionary<string, object?> CaseHeader(JsonElement definitionCase, Dictionary<string, object?> body)
{
    Dictionary<string, object?> result = Object(
        ("case_id", String(definitionCase, "case_id")),
        ("title", String(definitionCase, "title")),
        ("case_kind", String(definitionCase, "case_kind")),
        ("case_status", "SyntheticCalibrated"),
        ("source_rows", Strings(definitionCase.GetProperty("source_rows"))),
        ("source_locators", Strings(definitionCase.GetProperty("source_locators"))),
        ("source_evidence_class", String(definitionCase, "source_evidence_class")),
        ("coverage_status", "SyntheticOnlyReportedTargetComparison"));
    foreach ((string key, object? value) in body)
    {
        result.Add(key, value);
    }

    return result;
}

static Dictionary<string, object?> Comparison(string referenceId, double referenceValue, double syntheticValue, string unit, string evidence, double difference)
{
    return Object(
        ("observable", referenceId),
        ("unit", unit),
        ("reference_value", referenceValue),
        ("synthetic_value", syntheticValue),
        ("absolute_difference", Math.Abs(difference)),
        ("signed_difference", difference),
        ("relative_difference_percent", RelativePercent(referenceValue, syntheticValue)),
        ("evidence", evidence),
        ("comparison_status", "ReportedTargetOnly"));
}

static Dictionary<string, object?> IntervalComparison(string observable, double synthetic, double minimum, double maximum, string unit, string evidence)
{
    double distance = synthetic < minimum ? minimum - synthetic : synthetic > maximum ? synthetic - maximum : 0;
    return Object(
        ("observable", observable),
        ("unit", unit),
        ("reference_minimum", minimum),
        ("reference_maximum", maximum),
        ("synthetic_value", synthetic),
        ("distance_to_reported_interval", distance),
        ("relative_distance_to_interval_percent", distance == 0 ? 0 : distance / Math.Max(Math.Abs(minimum), Math.Abs(maximum)) * 100.0),
        ("evidence", evidence),
        ("comparison_status", "ReportedTargetOnly"));
}

static void VerifySourceManifest(JsonElement definition, string definitionPath)
{
    string declaredSha = String(definition, "source_manifest_sha256");
    string relativePath = String(definition, "source_manifest").Replace('/', Path.DirectorySeparatorChar);
    string definitionDirectory = Path.GetDirectoryName(definitionPath) ?? throw new InvalidOperationException("definition directory is unavailable");
    string repositoryRoot = Directory.GetParent(Directory.GetParent(definitionDirectory)?.FullName ?? string.Empty)?.FullName
        ?? throw new InvalidOperationException("repository root is unavailable");
    string sourcePath = Path.GetFullPath(Path.Combine(repositoryRoot, relativePath));
    string repositoryPrefix = repositoryRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!sourcePath.StartsWith(repositoryPrefix, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("source manifest path escapes the repository");
    }

    EnsureFiles(sourcePath);
    string actualSha = Sha256(File.ReadAllBytes(sourcePath));
    if (!string.Equals(actualSha, declaredSha, StringComparison.Ordinal) || !string.Equals(actualSha, SourceManifestSha, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"source manifest SHA-256 mismatch: expected {SourceManifestSha}, actual {actualSha}");
    }
}

static (double MaximumScheduleBalance, double MinimumNonnegative) MeasureArtifact(JsonElement root)
{
    EnsureFiniteNumbers(root, "$artifact");
    Dictionary<string, JsonElement> cases = root.GetProperty("cases")
        .EnumerateArray()
        .ToDictionary(item => String(item, "case_id"), StringComparer.Ordinal);
    double maximumBalance = 0;
    double minimumNonnegative = double.PositiveInfinity;

    void AddBalance(double actual, double expected, string label)
    {
        EnsureFinite(actual, label);
        EnsureFinite(expected, label);
        maximumBalance = Math.Max(maximumBalance, Math.Abs(actual - expected));
    }

    void AddNonnegative(double value, string label)
    {
        EnsureFinite(value, label);
        if (value < 0)
        {
            throw new InvalidOperationException($"negative value in {label}");
        }

        minimumNonnegative = Math.Min(minimumNonnegative, value);
    }

    JsonElement depletion = cases["p7-t08-s5-lattice-depletion-300d-v1"];
    JsonElement depletionInput = depletion.GetProperty("input_snapshot");
    JsonElement depletionHistory = depletion.GetProperty("outputs").GetProperty("history");
    RequireSequentialHistory(depletionHistory, "day", Integer(depletionInput, "duration_days"), "S5 depletion history");
    foreach (JsonElement row in depletionHistory.EnumerateArray())
    {
        int day = Integer(row, "day");
        double burnup = Number(row, "burnup_gwd_per_t");
        AddNonnegative(burnup, "S5 burnup");
        AddBalance(burnup, Number(depletionInput, "constant_power_density_kw_per_kg") * day * Number(depletionInput, "hours_per_day") / Number(depletionInput, "kwh_per_kg_per_gwd_per_t"), "S5 burnup identity");
    }

    JsonElement atf = cases["p7-t08-s1-fullcore-atf-summary-v1"];
    JsonElement atfInput = atf.GetProperty("input_snapshot");
    int atfRate = Integer(atfInput, "refueling_rate_channels_per_day");
    int atfShift = Integer(atfInput, "refueling_shift_bundles");
    JsonElement atfHistory = atf.GetProperty("outputs").GetProperty("history");
    RequireSequentialHistory(atfHistory, "full_power_day", Integer(atfInput, "duration_full_power_days"), "S1 full-core history");
    foreach (JsonElement row in atfHistory.EnumerateArray())
    {
        int day = Integer(row, "full_power_day");
        int expectedToday = day == 0 ? 0 : atfRate;
        int cumulative = Integer(row, "cumulative_refueled_channels");
        AddBalance(Integer(row, "refueled_channels_today"), expectedToday, "S1 daily channel schedule");
        AddBalance(cumulative, day * atfRate, "S1 cumulative channel schedule");
        AddBalance(Integer(row, "cumulative_shifted_bundles"), cumulative * atfShift, "S1 cumulative bundle schedule");
        AddNonnegative(Integer(row, "refueled_channels_today"), "S1 daily channels");
        AddNonnegative(cumulative, "S1 cumulative channels");
        AddNonnegative(Integer(row, "cumulative_shifted_bundles"), "S1 cumulative bundles");
    }

    JsonElement refueling = cases["p7-t08-s4-fullcore-refueling-summary-v1"];
    JsonElement refuelingInput = refueling.GetProperty("input_snapshot");
    int runningEvents = 0;
    int refuelingShift = Integer(refuelingInput, "bundle_shift");
    JsonElement refuelingHistory = refueling.GetProperty("outputs").GetProperty("history");
    RequireSequentialHistory(refuelingHistory, "day", Integer(refuelingInput, "duration_days"), "S4 refueling history");
    foreach (JsonElement row in refuelingHistory.EnumerateArray())
    {
        int today = Integer(row, "synthetic_refueling_events_today");
        runningEvents += today;
        AddBalance(Integer(row, "cumulative_refueling_events"), runningEvents, "S4 cumulative refueling schedule");
        AddBalance(Integer(row, "cumulative_shifted_bundles"), runningEvents * refuelingShift, "S4 cumulative bundle schedule");
        AddNonnegative(today, "S4 daily events");
        AddNonnegative(runningEvents, "S4 cumulative events");
        AddNonnegative(Integer(row, "cumulative_shifted_bundles"), "S4 cumulative bundles");
    }

    return (maximumBalance, double.IsPositiveInfinity(minimumNonnegative) ? 0 : minimumNonnegative);
}

static void EnsureFinite(double value, string label)
{
    if (!double.IsFinite(value))
    {
        throw new InvalidOperationException($"non-finite value in {label}");
    }
}

static void EnsureFiniteNumbers(JsonElement element, string path)
{
    switch (element.ValueKind)
    {
        case JsonValueKind.Number:
            EnsureFinite(element.GetDouble(), path);
            break;
        case JsonValueKind.Array:
            int index = 0;
            foreach (JsonElement child in element.EnumerateArray())
            {
                EnsureFiniteNumbers(child, $"{path}[{index}]");
                index++;
            }

            break;
        case JsonValueKind.Object:
            foreach (JsonProperty property in element.EnumerateObject())
            {
                EnsureFiniteNumbers(property.Value, $"{path}.{property.Name}");
            }

            break;
    }
}

static void RequireSequentialHistory(JsonElement history, string indexProperty, int expectedLastIndex, string label)
{
    if (history.ValueKind != JsonValueKind.Array || history.GetArrayLength() != expectedLastIndex + 1)
    {
        throw new InvalidOperationException($"{label} must contain indices 0 through {expectedLastIndex}");
    }

    int expectedIndex = 0;
    foreach (JsonElement row in history.EnumerateArray())
    {
        if (!row.TryGetProperty(indexProperty, out JsonElement indexElement) || !indexElement.TryGetInt32(out int actualIndex) || actualIndex != expectedIndex || actualIndex < 0 || actualIndex > expectedLastIndex)
        {
            throw new InvalidOperationException($"{label} has a non-sequential {indexProperty} at position {expectedIndex}");
        }

        expectedIndex++;
    }
}

static void RequireDefinition(JsonElement definition)
{
    if (String(definition, "format") != DefinitionFormat || String(definition, "task_id") != TaskId || String(definition, "source_manifest_sha256") != SourceManifestSha)
    {
        throw new InvalidOperationException("definition identity does not match P7-T08 authority");
    }
    if (definition.GetProperty("cases").GetArrayLength() != 4)
    {
        throw new InvalidOperationException("definition must contain four cases");
    }
}

static void ValidateCases(JsonElement root, List<string> errors)
{
    try
    {
        EnsureFiniteNumbers(root, "$artifact");
    }
    catch (InvalidOperationException exception)
    {
        errors.Add(exception.Message);
    }

    JsonElement cases = root.GetProperty("cases");
    if (cases.ValueKind != JsonValueKind.Array || cases.GetArrayLength() != 4)
    {
        errors.Add("expected exactly four generated cases");
        return;
    }

    string[] requiredIds =
    [
        "p7-t08-s4-lattice-37-bundle-kcross-v1",
        "p7-t08-s5-lattice-depletion-300d-v1",
        "p7-t08-s1-fullcore-atf-summary-v1",
        "p7-t08-s4-fullcore-refueling-summary-v1"
    ];
    Dictionary<string, JsonElement> byId = cases.EnumerateArray().ToDictionary(item => String(item, "case_id"), StringComparer.Ordinal);
    foreach (string id in requiredIds)
    {
        if (!byId.ContainsKey(id))
        {
            errors.Add($"missing case: {id}");
        }
    }

    foreach (JsonElement caseElement in cases.EnumerateArray())
    {
        if (caseElement.GetProperty("source_locators").GetArrayLength() == 0 || String(caseElement, "source_evidence_class") != "PrimarySourceReportedNumeric")
        {
            errors.Add($"case {String(caseElement, "case_id")} must retain a reported-numeric evidence class and source locator");
        }
    }

    if (byId.TryGetValue(requiredIds[0], out JsonElement lattice))
    {
        double synthetic = Number(lattice.GetProperty("outputs"), "synthetic_k_effective");
        double dragon = Number(lattice.GetProperty("input_snapshot"), "reported_dragon_k_effective");
        double serpent = Number(lattice.GetProperty("input_snapshot"), "reported_serpent_k_effective");
        if (synthetic != (dragon + serpent) / 2.0)
        {
            errors.Add("lattice synthetic k-effective is not the minimax midpoint");
        }
    }

    if (byId.TryGetValue(requiredIds[1], out JsonElement depletion))
    {
        JsonElement input = depletion.GetProperty("input_snapshot");
        double expected = Number(input, "constant_power_density_kw_per_kg") * Integer(input, "duration_days") * Number(input, "hours_per_day") / Number(input, "kwh_per_kg_per_gwd_per_t");
        double actual = Number(depletion.GetProperty("outputs"), "synthetic_exit_burnup_gwd_per_t");
        JsonElement history = depletion.GetProperty("outputs").GetProperty("history");
        ValidateSequentialHistory(history, "day", Integer(input, "duration_days"), "S5 depletion history", errors);
        if (actual != expected)
        {
            errors.Add("depletion history or energy-balance output is invalid");
        }
    }

    if (byId.TryGetValue(requiredIds[2], out JsonElement atf))
    {
        JsonElement input = atf.GetProperty("input_snapshot");
        JsonElement output = atf.GetProperty("outputs");
        int days = Integer(input, "duration_full_power_days");
        ValidateSequentialHistory(output.GetProperty("history"), "full_power_day", days, "S1 full-core history", errors);
        if (Integer(output, "total_refueled_channels") != days * Integer(input, "refueling_rate_channels_per_day"))
        {
            errors.Add("full-core ATF schedule does not cover the declared history");
        }
    }

    if (byId.TryGetValue(requiredIds[3], out JsonElement refueling))
    {
        JsonElement output = refueling.GetProperty("outputs");
        JsonElement input = refueling.GetProperty("input_snapshot");
        ValidateSequentialHistory(output.GetProperty("history"), "day", Integer(input, "duration_days"), "S4 refueling history", errors);
        int events = Integer(output, "synthetic_total_refueling_events");
        double rate = Number(output, "synthetic_average_refueling_rate_channels_per_day");
        if (events != 408 || rate != 4.08 || output.GetProperty("aggregate_metrics").GetArrayLength() != 7)
        {
            errors.Add("full-core refueling schedule or aggregate output is invalid");
        }
    }
}

static void ValidateSequentialHistory(JsonElement history, string indexProperty, int expectedLastIndex, string label, List<string> errors)
{
    if (history.ValueKind != JsonValueKind.Array || history.GetArrayLength() != expectedLastIndex + 1)
    {
        errors.Add($"{label} must contain indices 0 through {expectedLastIndex}");
        return;
    }

    int expectedIndex = 0;
    foreach (JsonElement row in history.EnumerateArray())
    {
        if (!row.TryGetProperty(indexProperty, out JsonElement indexElement) || !indexElement.TryGetInt32(out int actualIndex) || actualIndex != expectedIndex || actualIndex < 0 || actualIndex > expectedLastIndex)
        {
            errors.Add($"{label} has a non-sequential {indexProperty} at position {expectedIndex}");
            return;
        }

        expectedIndex++;
    }
}

static void ValidateManifest(JsonElement manifest, string disposition, string definitionSha, string artifactSha, int artifactBytes, string repeatSha, double maximumScheduleBalance, double minimumNonnegative, List<string> errors)
{
    RequireString(manifest, "format", ManifestFormat, errors);
    RequireString(manifest, "task_id", TaskId, errors);
    RequireString(manifest, "artifact_id", ArtifactId, errors);
    RequireString(manifest, "definition_sha256", definitionSha, errors);
    RequireString(manifest, "artifact_sha256", artifactSha, errors);
    RequireInt(manifest, "artifact_byte_length", artifactBytes, errors);
    RequireString(manifest, "source_manifest_sha256", SourceManifestSha, errors);
    RequireString(manifest, "artifact_status", ArtifactStatus(disposition), errors);
    RequireInt(manifest, "case_count", 4, errors);
    RequireDouble(manifest, "maximum_schedule_balance_absolute", maximumScheduleBalance, errors);
    RequireDouble(manifest, "minimum_nonnegative_value", minimumNonnegative, errors);
    if (!manifest.TryGetProperty("independent_repeat_equal", out JsonElement equal) || equal.ValueKind != JsonValueKind.True)
    {
        errors.Add("independent_repeat_equal must be true");
    }

    RequireString(manifest, "independent_repeat_sha256", repeatSha, errors);
}

static void EnsureFiles(params string[] paths)
{
    foreach (string path in paths)
    {
        if (!File.Exists(path))
        {
            throw new IOException($"missing file: {path}");
        }
    }
}

static Dictionary<string, object?> Object(params (string Key, object? Value)[] properties)
{
    Dictionary<string, object?> result = new(StringComparer.Ordinal);
    foreach ((string key, object? value) in properties)
    {
        result.Add(key, value);
    }

    return result;
}

static byte[] Serialize(object value)
{
    JsonSerializerOptions options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = null
    };
    return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, options) + "\n");
}

static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

static string ArtifactStatus(string disposition) => disposition == "approved" ? "Approved/ApprovedGolden" : "Candidate/Deferred/NoGolden";
static string EvidenceApproval(string disposition) => disposition == "approved" ? "ApprovedSyntheticOnly" : "Candidate";
static string ComparisonStatus(string disposition) => disposition == "approved" ? "ApprovedSyntheticOnly" : "DeferredSyntheticOnly";
static string GoldenStatus(string disposition) => disposition == "approved" ? "ApprovedGolden" : "NoGolden";
static string StatusLabel(string disposition) => disposition == "approved" ? "Approved/ApprovedGolden" : "Candidate/Deferred/NoGolden";

static string String(JsonElement parent, string property)
{
    JsonElement value = parent.GetProperty(property);
    return value.GetString() ?? throw new InvalidOperationException($"{property} must be a string");
}

static double Number(JsonElement parent, string property)
{
    JsonElement value = parent.GetProperty(property);
    return value.GetDouble();
}

static int Integer(JsonElement parent, string property)
{
    JsonElement value = parent.GetProperty(property);
    return value.GetInt32();
}

static List<object?> Strings(JsonElement array)
{
    return array.EnumerateArray().Select(item => (object?)(item.GetString() ?? string.Empty)).ToList();
}

static JsonElement Clone(JsonElement element) => element.Clone();

static double RelativePercent(double reference, double value) => reference == 0 ? 0 : Math.Abs(value - reference) / Math.Abs(reference) * 100.0;

static string UnitFor(string metric) => metric switch
{
    "maximum_channel_power_kw" => "kW",
    "maximum_bundle_power_kw" => "kW",
    "rippled_channel_power_over_reference" => "1",
    "average_zone_level" => "fraction",
    "refueling_rate_channels_per_day" => "channel/day",
    "exit_burnup_zone_1_mwd_per_t" => "MWd/t",
    "exit_burnup_zone_2_mwd_per_t" => "MWd/t",
    _ => "source-defined"
};

static void RequireString(JsonElement parent, string property, string expected, List<string> errors)
{
    if (!parent.TryGetProperty(property, out JsonElement actual) || actual.ValueKind != JsonValueKind.String || !string.Equals(actual.GetString(), expected, StringComparison.Ordinal))
    {
        errors.Add($"{property} must equal {expected}");
    }
}

static void RequireInt(JsonElement parent, string property, int expected, List<string> errors)
{
    if (!parent.TryGetProperty(property, out JsonElement actual) || actual.ValueKind != JsonValueKind.Number || actual.GetInt32() != expected)
    {
        errors.Add($"{property} must equal {expected}");
    }
}

static void RequireDouble(JsonElement parent, string property, double expected, List<string> errors)
{
    if (!parent.TryGetProperty(property, out JsonElement actual) || actual.ValueKind != JsonValueKind.Number || actual.GetDouble() != expected)
    {
        errors.Add($"{property} must equal {expected.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
    }
}

static void RequireBytes(byte[] actual, byte[] expected, List<string> errors, string message)
{
    if (!actual.AsSpan().SequenceEqual(expected))
    {
        errors.Add(message);
    }
}
