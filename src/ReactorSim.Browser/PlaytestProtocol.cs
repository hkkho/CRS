using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReactorSim.Core;
using ReactorSim.Game;

namespace ReactorSim.Browser
{
    public static class PlaytestProtocolV2
    {
        public const string ProtocolId = "candu-playtest-v2";
        public const uint SchemaVersion = 2;
        public const ulong PracticeSeed = 1001;
        public const string StateDigestAlgorithm = "sha256-canonical-state-v1";
        public const string CompactStateDigestAlgorithm = "sha256-canonical-compact-state-v1";
        public const string ReplayDigestAlgorithm = "sha256-canonical-replay-v1";

        internal static string Serialize(object value)
        {
            return value switch
            {
                BridgeCapabilitiesDto capabilities =>
                    JsonSerializer.Serialize(capabilities, PlaytestJsonContext.Default.BridgeCapabilitiesDto),
                PlaytestResponseDto response =>
                    JsonSerializer.Serialize(response, PlaytestJsonContext.Default.PlaytestResponseDto),
                PlaytestSnapshotPatchDto patch =>
                    JsonSerializer.Serialize(patch, PlaytestJsonContext.Default.PlaytestSnapshotPatchDto),
                PlaytestSnapshotDto snapshot =>
                    JsonSerializer.Serialize(snapshot, PlaytestJsonContext.Default.PlaytestSnapshotDto),
                JsonElement element =>
                    JsonSerializer.Serialize(element, PlaytestJsonContext.Default.JsonElement),
                _ => throw new InvalidOperationException(
                    "The browser protocol received an unsupported serialization type.")
            };
        }

        internal static string ComputeDigest(string canonicalText)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalText));
            StringBuilder result = new StringBuilder(bytes.Length * 2 + 7);
            result.Append("sha256:");
            foreach (byte value in bytes)
            {
                result.Append(value.ToString("x2", CultureInfo.InvariantCulture));
            }

            return result.ToString();
        }

        internal static string CanonicalizeJson(JsonElement value)
        {
            StringBuilder builder = new StringBuilder();
            AppendCanonicalJson(builder, value);
            return builder.ToString();
        }

        internal static BridgeMetadataDto CreateMetadata()
        {
            return new BridgeMetadataDto
            {
                Units = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["time"] = "s",
                    ["wallTime"] = "ms",
                    ["power"] = "W",
                    ["normalizedPower"] = "1",
                    ["tilt"] = "1",
                    ["burnup"] = "MWd/kg_HM",
                    ["burnupInternal"] = "J/kg_HM",
                    ["energy"] = "J",
                    ["heavyMetalMass"] = "kg_HM",
                    ["nodeVolume"] = "m^3",
                    ["numberDensity"] = "m^-3",
                    ["conductance"] = "m^2",
                    ["macroscopicCrossSection"] = "m^-1",
                    ["dynamicAbsorption"] = "m^-1",
                    ["flux"] = "normalized-arbitrary"
                },
                EnergyGroups = new List<BridgeEnergyGroupDto>
                {
                    new BridgeEnergyGroupDto
                    {
                        Id = "group-1",
                        Ordinal = 1,
                        Label = "Group 1 (fast)",
                        Ordering = "fast-to-thermal"
                    },
                    new BridgeEnergyGroupDto
                    {
                        Id = "group-2",
                        Ordinal = 2,
                        Label = "Group 2 (thermal)",
                        Ordering = "fast-to-thermal"
                    }
                },
                FormulationId = EquilibriumCoreSolverIdentityV1.FormulationId,
                ShapeMethodId = EquilibriumCoreSolverIdentityV1.ShapeMethodId,
                AmplitudeMethodId = EquilibriumCoreSolverIdentityV1.AmplitudeMethodId,
                ReactivityMethodId = EquilibriumCoreSolverIdentityV1.ReactivityMethodId,
                StateDigestAlgorithm = StateDigestAlgorithm,
                CompactStateDigestAlgorithm = CompactStateDigestAlgorithm,
                ReplayDigestAlgorithm = ReplayDigestAlgorithm
            };
        }

        internal static BridgeDiagnosticDto Diagnostic(
            string code,
            string path,
            string message)
        {
            return new BridgeDiagnosticDto
            {
                Code = code,
                Path = path,
                Message = message
            };
        }

        internal static BridgeDiagnosticDto Diagnostic(ContractDiagnostic diagnostic)
        {
            return Diagnostic(diagnostic.Code, diagnostic.Path, diagnostic.Message);
        }

        internal static string EnumId(Enum value)
        {
            return value.ToString().ToLowerInvariant();
        }

        private static void AppendCanonicalJson(StringBuilder builder, JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    builder.Append('{');
                    bool firstProperty = true;
                    foreach (JsonProperty property in value.EnumerateObject()
                                 .OrderBy(item => item.Name, StringComparer.Ordinal))
                    {
                        if (!firstProperty)
                        {
                            builder.Append(',');
                        }

                        firstProperty = false;
                        builder.Append(JsonSerializer.Serialize(property.Name, PlaytestJsonContext.Default.String));
                        builder.Append(':');
                        AppendCanonicalJson(builder, property.Value);
                    }

                    builder.Append('}');
                    return;

                case JsonValueKind.Array:
                    builder.Append('[');
                    bool firstElement = true;
                    foreach (JsonElement element in value.EnumerateArray())
                    {
                        if (!firstElement)
                        {
                            builder.Append(',');
                        }

                        firstElement = false;
                        AppendCanonicalJson(builder, element);
                    }

                    builder.Append(']');
                    return;

                case JsonValueKind.String:
                    builder.Append(JsonSerializer.Serialize(value.GetString(), PlaytestJsonContext.Default.String));
                    return;

                case JsonValueKind.Number:
                    if (value.TryGetDouble(out double number) &&
                        double.IsFinite(number))
                    {
                        builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(value.GetRawText());
                    }

                    return;

                case JsonValueKind.True:
                    builder.Append("true");
                    return;

                case JsonValueKind.False:
                    builder.Append("false");
                    return;

                case JsonValueKind.Null:
                    builder.Append("null");
                    return;

                default:
                    throw new InvalidOperationException(
                        "The browser protocol received an unsupported JSON value kind.");
            }
        }
    }

    internal static class PlaytestInput
    {
        public static bool TryGetProperty(
            JsonElement objectValue,
            out JsonElement value,
            params string[] names)
        {
            if (objectValue.ValueKind == JsonValueKind.Object)
            {
                foreach (string name in names)
                {
                    if (objectValue.TryGetProperty(name, out value))
                    {
                        return true;
                    }

                    foreach (JsonProperty property in objectValue.EnumerateObject())
                    {
                        if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            value = property.Value;
                            return true;
                        }
                    }
                }
            }

            value = default;
            return false;
        }

        public static string NormalizeType(string value)
        {
            return value.Trim().Replace('_', '-').Replace(' ', '-').ToLowerInvariant();
        }

        public static string NormalizeMode(string value)
        {
            return NormalizeType(value);
        }
    }

    public sealed class BridgeDiagnosticDto
    {
        public string Code { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }

    public sealed class BridgeEnergyGroupDto
    {
        public string Id { get; set; } = string.Empty;

        public int Ordinal { get; set; }

        public string Label { get; set; } = string.Empty;

        public string Ordering { get; set; } = string.Empty;
    }

    public sealed class BridgeMetadataDto
    {
        public Dictionary<string, string> Units { get; set; } = new Dictionary<string, string>();

        public List<BridgeEnergyGroupDto> EnergyGroups { get; set; } = new List<BridgeEnergyGroupDto>();

        public string FormulationId { get; set; } = string.Empty;

        public string ShapeMethodId { get; set; } = string.Empty;

        public string AmplitudeMethodId { get; set; } = string.Empty;

        public string ReactivityMethodId { get; set; } = string.Empty;

        public string StateDigestAlgorithm { get; set; } = string.Empty;

        public string CompactStateDigestAlgorithm { get; set; } = string.Empty;

        public string ReplayDigestAlgorithm { get; set; } = string.Empty;
    }

    public sealed class BridgeModeCapabilityDto
    {
        public string Id { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string AuthoritativeModel { get; set; } = string.Empty;

        public string FixtureId { get; set; } = string.Empty;

        public List<string> Commands { get; set; } = new List<string>();
    }

    public sealed class BridgeCapabilitiesDto
    {
        public string Protocol { get; set; } = PlaytestProtocolV2.ProtocolId;

        public uint SchemaVersion { get; set; } = PlaytestProtocolV2.SchemaVersion;

        public List<string> Operations { get; set; } = new List<string>();

        public List<BridgeModeCapabilityDto> Modes { get; set; } = new List<BridgeModeCapabilityDto>();

        public BridgeMetadataDto Metadata { get; set; } = PlaytestProtocolV2.CreateMetadata();
    }

    public sealed class BridgeCommandHistoryDto
    {
        public ulong Sequence { get; set; }

        public string Type { get; set; } = string.Empty;

        public string CommandJson { get; set; } = string.Empty;

        public bool Accepted { get; set; }

        public string? DiagnosticCode { get; set; }

        public string StateDigest { get; set; } = string.Empty;
    }

    public sealed class BridgeReplayDto
    {
        public string Protocol { get; set; } = PlaytestProtocolV2.ProtocolId;

        public uint SchemaVersion { get; set; } = PlaytestProtocolV2.SchemaVersion;

        public string Mode { get; set; } = string.Empty;

        public string InitializationJson { get; set; } = string.Empty;

        public List<string> Commands { get; set; } = new List<string>();

        public string FinalStateDigest { get; set; } = string.Empty;

        public string ReplayDigest { get; set; } = string.Empty;
    }

    public sealed class BridgeStateDto
    {
        public string Mode { get; set; } = string.Empty;

        public string FixtureId { get; set; } = string.Empty;

        public string StateDigest { get; set; } = string.Empty;

        public string ReplayDigest { get; set; } = string.Empty;

        public BridgeMetadataDto Metadata { get; set; } = PlaytestProtocolV2.CreateMetadata();

        public object? Snapshot { get; set; }

        public List<BridgeCommandHistoryDto> CommandHistory { get; set; } =
            new List<BridgeCommandHistoryDto>();

        public BridgeReplayDto Replay { get; set; } = new BridgeReplayDto();
    }

    public sealed class BridgeCommandResultDto
    {
        public bool Accepted { get; set; }

        public string Message { get; set; } = string.Empty;

        public string? DiagnosticCode { get; set; }

        public string? DiagnosticMessage { get; set; }

        public object? SpatialSolve { get; set; }
    }

    public sealed class BridgeResponseDto
    {
        public string Protocol { get; set; } = PlaytestProtocolV2.ProtocolId;

        public uint SchemaVersion { get; set; } = PlaytestProtocolV2.SchemaVersion;

        public string Operation { get; set; } = string.Empty;

        public bool Ok { get; set; }

        public string? Mode { get; set; }

        public string? Message { get; set; }

        public List<BridgeDiagnosticDto> Diagnostics { get; set; } = new List<BridgeDiagnosticDto>();

        public string? StateDigest { get; set; }

        public string? ReplayDigest { get; set; }

        public BridgeCapabilitiesDto? Capabilities { get; set; }

        public BridgeCommandResultDto? Result { get; set; }

        public BridgeStateDto? State { get; set; }

        public BridgeReplayDto? Replay { get; set; }
    }

    internal sealed class BridgeCommandExecution
    {
        public bool Accepted { get; init; }

        public string Message { get; init; } = string.Empty;

        public List<BridgeDiagnosticDto> Diagnostics { get; init; } = new List<BridgeDiagnosticDto>();

        public object? SpatialSolve { get; init; }

        public GameSessionSnapshot? Snapshot { get; init; }

        public static BridgeCommandExecution Success(
            string message = "",
            object? spatialSolve = null,
            GameSessionSnapshot? snapshot = null)
        {
            return new BridgeCommandExecution
            {
                Accepted = true,
                Message = message,
                SpatialSolve = spatialSolve,
                Snapshot = snapshot
            };
        }

        public static BridgeCommandExecution Failure(
            BridgeDiagnosticDto diagnostic,
            GameSessionSnapshot? snapshot = null)
        {
            return new BridgeCommandExecution
            {
                Accepted = false,
                Message = diagnostic.Message,
                Diagnostics = new List<BridgeDiagnosticDto> { diagnostic },
                Snapshot = snapshot
            };
        }
    }

    internal sealed class BridgeHistoryEntry
    {
        public ulong Sequence { get; init; }

        public string Type { get; init; } = string.Empty;

        public string CommandJson { get; init; } = string.Empty;

        public bool Accepted { get; init; }

        public string? DiagnosticCode { get; init; }

        public string StateDigest { get; init; } = string.Empty;

        public BridgeCommandHistoryDto ToDto()
        {
            return new BridgeCommandHistoryDto
            {
                Sequence = Sequence,
                Type = Type,
                CommandJson = CommandJson,
                Accepted = Accepted,
                DiagnosticCode = DiagnosticCode,
                StateDigest = StateDigest
            };
        }
    }
}
