using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReactorSim.Browser
{
    [JsonSourceGenerationOptions(
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        GenerationMode = JsonSourceGenerationMode.Metadata,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        WriteIndented = false)]
    [JsonSerializable(typeof(string))]
    [JsonSerializable(typeof(JsonElement))]
    [JsonSerializable(typeof(BridgeCapabilitiesDto))]
    [JsonSerializable(typeof(PlaytestResponseDto))]
    [JsonSerializable(typeof(PlaytestSnapshotPatchDto))]
    [JsonSerializable(typeof(PlaytestSnapshotDto))]
    [JsonSerializable(typeof(PlaytestXenonDto))]
    [JsonSerializable(typeof(PlaytestXenonChannelDto))]
    [JsonSerializable(typeof(PlaytestRrsDto))]
    [JsonSerializable(typeof(PlaytestRrsZoneDto))]
    [JsonSerializable(typeof(LabSnapshotDto))]
    [JsonSerializable(typeof(LabSingleCellSnapshotDto))]
    [JsonSerializable(typeof(LabSingleCellCoefficientsSnapshotDto))]
    [JsonSerializable(typeof(LabSingleCellDiagnosticsSnapshotDto))]
    [JsonSerializable(typeof(LabCoreSnapshotDto))]
    [JsonSerializable(typeof(LabCellSnapshotDto))]
    [JsonSerializable(typeof(LabChannelSnapshotDto))]
    [JsonSerializable(typeof(LabBundleSnapshotDto))]
    [JsonSerializable(typeof(LabSpatialSolveSnapshotDto))]
    [JsonSerializable(typeof(LabSpatialStateSnapshotDto))]
    [JsonSerializable(typeof(LabSpatialDiagnosticsSnapshotDto))]
    internal sealed partial class PlaytestJsonContext : JsonSerializerContext
    {
    }
}
