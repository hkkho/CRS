using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 3 || !string.Equals(args[0], "validate", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Usage: validate <candidate-cases.json> <manifest.json>");
    return 2;
}

string artifactPath = Path.GetFullPath(args[1]);
string manifestPath = Path.GetFullPath(args[2]);
if (!File.Exists(artifactPath) || !File.Exists(manifestPath))
{
    Console.Error.WriteLine("P7_T07_VALIDATE_FAIL missing artifact or manifest");
    return 1;
}

byte[] artifactBytes = File.ReadAllBytes(artifactPath);
string artifactSha = Convert.ToHexString(SHA256.HashData(artifactBytes)).ToLowerInvariant();
using JsonDocument artifact = JsonDocument.Parse(artifactBytes);
using JsonDocument manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
JsonElement root = artifact.RootElement;
JsonElement manifestRoot = manifest.RootElement;
List<string> errors = [];

RequireString(root, "format", "reactorsim.p7-t07-literature-candidate-cases/v1", errors);
RequireString(root, "task_id", "P7-T07", errors);
RequireString(root, "digest_id", "candu-literature-digest-v1", errors);
RequireString(root, "artifact_status", "Candidate/Deferred/NoGolden", errors);
RequireString(root, "coverage_class", "LiteratureCandidate", errors);
RequireString(root, "evidence_approval", "Candidate", errors);
RequireString(root, "comparison_status", "Deferred", errors);
RequireString(root, "golden_status", "NoGolden", errors);
RequirePredicate(root, "runtime_use", value => value.Contains("Prohibited", StringComparison.Ordinal), errors, "runtime_use must prohibit unapproved execution");

string sourceManifestSha = root.TryGetProperty("source_manifest_sha256", out JsonElement sourceManifestElement) && sourceManifestElement.ValueKind == JsonValueKind.String
    ? sourceManifestElement.GetString() ?? string.Empty
    : string.Empty;
if (sourceManifestSha.Length != 64 ||
    !string.Equals(sourceManifestSha, "df9b06987dc831b61c2f75daf368628d697b5ab1b0605466134ee0c73bbd25a1", StringComparison.Ordinal))
{
    errors.Add("source_manifest_sha256 does not bind P1-T08 manifest identity");
}

JsonElement cases = root.GetProperty("cases");
if (cases.ValueKind != JsonValueKind.Array || cases.GetArrayLength() != 3)
{
    errors.Add("expected exactly three candidate cases");
}
else
{
    HashSet<string> ids = new(StringComparer.Ordinal);
    foreach (JsonElement candidate in cases.EnumerateArray())
    {
        string id = StringValue(candidate, "case_id", errors);
        if (!ids.Add(id))
        {
            errors.Add($"duplicate case_id: {id}");
        }

        RequireString(candidate, "case_status", "CandidateCaseDesign", errors);
        RequireBool(candidate.GetProperty("source"), "artifact_committed", false, errors);
        RequireString(candidate, "execution_status", "BlockedUntilAdmissionProof", errors);
        JsonElement gaps = candidate.GetProperty("admission_gaps");
        if (gaps.ValueKind != JsonValueKind.Object || gaps.EnumerateObject().Any(property => property.Value.ValueKind != JsonValueKind.String || property.Value.GetString() is not { Length: > 0 }))
        {
            errors.Add($"{id} admission_gaps must be explicit non-empty states");
        }

        foreach (JsonElement observable in candidate.GetProperty("comparison_observables").EnumerateArray())
        {
            RequireString(observable, "mapping_status", "NotProven", errors);
            RequireString(observable, "comparison_status", "Deferred", errors);
        }
    }

    string[] requiredIds =
    [
        "p7-t07-s5-lattice-37-bundle-crosscheck-v1",
        "p7-t07-s5-lattice-depletion-300d-v1",
        "p7-t07-s1-fullcore-380-channel-300fpd-v1"
    ];
    if (!requiredIds.All(ids.Contains))
    {
        errors.Add("required S5 lattice, S5 depletion, and S1 full-core case IDs are incomplete");
    }
}

foreach (string forbidden in new[] { "ApprovedGolden", "approved_golden", "EvidenceApproval=Approved", "CoverageClass=Direct" })
{
    if (ContainsString(root, forbidden))
    {
        errors.Add($"forbidden approval/direct-coverage token present: {forbidden}");
    }
}

if (ContainsPrivatePath(root))
{
    errors.Add("private or host-local path token present");
}

RequireString(manifestRoot, "format", "reactorsim.p7-t07-literature-candidate-manifest/v1", errors);
RequireString(manifestRoot, "task_id", "P7-T07", errors);
RequireString(manifestRoot, "artifact_sha256", artifactSha, errors);
RequireInt(manifestRoot, "artifact_byte_length", artifactBytes.Length, errors);
RequireInt(manifestRoot, "case_count", 3, errors);
RequireString(manifestRoot, "artifact_status", "Candidate/Deferred/NoGolden", errors);
RequireString(manifestRoot, "coverage_class", "LiteratureCandidate", errors);
RequireString(manifestRoot, "source_manifest_sha256", sourceManifestSha, errors);

if (errors.Count > 0)
{
    Console.Error.WriteLine("P7_T07_VALIDATE_FAIL");
    foreach (string error in errors)
    {
        Console.Error.WriteLine($"- {error}");
    }

    return 1;
}

Console.WriteLine($"P7_T07_LITERATURE_VALIDATE_PASS cases=3 artifact_bytes={artifactBytes.Length} artifact_sha256={artifactSha} source_manifest_sha256={sourceManifestSha} status=Candidate/Deferred/NoGolden");
return 0;

static void RequireString(JsonElement parent, string property, string expected, List<string> errors)
{
    if (!parent.TryGetProperty(property, out JsonElement actual) || actual.ValueKind != JsonValueKind.String || !string.Equals(actual.GetString(), expected, StringComparison.Ordinal))
    {
        errors.Add($"{property} must equal {expected}");
    }
}

static void RequireBool(JsonElement parent, string property, bool expected, List<string> errors)
{
    if (!parent.TryGetProperty(property, out JsonElement actual) || actual.ValueKind != JsonValueKind.False && actual.ValueKind != JsonValueKind.True || actual.GetBoolean() != expected)
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

static void RequirePredicate(JsonElement parent, string property, Func<string, bool> predicate, List<string> errors, string message)
{
    if (!parent.TryGetProperty(property, out JsonElement actual) || actual.ValueKind != JsonValueKind.String || actual.GetString() is not { } value || !predicate(value))
    {
        errors.Add(message);
    }
}

static string StringValue(JsonElement parent, string property, List<string> errors)
{
    if (!parent.TryGetProperty(property, out JsonElement actual) || actual.ValueKind != JsonValueKind.String)
    {
        errors.Add($"missing string property: {property}");
        return string.Empty;
    }

    return actual.GetString() ?? string.Empty;
}

static bool ContainsString(JsonElement element, string value)
{
    if (element.ValueKind == JsonValueKind.String)
    {
        return string.Equals(element.GetString(), value, StringComparison.Ordinal) || (element.GetString()?.Contains(value, StringComparison.Ordinal) ?? false);
    }

    if (element.ValueKind == JsonValueKind.Object)
    {
        return element.EnumerateObject().Any(property => ContainsString(property.Value, value));
    }

    return element.ValueKind == JsonValueKind.Array && element.EnumerateArray().Any(item => ContainsString(item, value));
}

static bool ContainsPrivatePath(JsonElement element)
{
    if (element.ValueKind == JsonValueKind.String)
    {
        string value = element.GetString() ?? string.Empty;
        return value.Contains("C:\\", StringComparison.OrdinalIgnoreCase) || value.Contains("C:/", StringComparison.OrdinalIgnoreCase) || value.Contains("/Users/", StringComparison.OrdinalIgnoreCase) || value.Contains("/home/", StringComparison.OrdinalIgnoreCase) || value.Contains("\\tmp\\", StringComparison.OrdinalIgnoreCase);
    }

    if (element.ValueKind == JsonValueKind.Object)
    {
        return element.EnumerateObject().Any(property => ContainsPrivatePath(property.Value));
    }

    return element.ValueKind == JsonValueKind.Array && element.EnumerateArray().Any(ContainsPrivatePath);
}
