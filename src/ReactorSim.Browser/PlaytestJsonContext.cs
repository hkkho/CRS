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
    [JsonSerializable(typeof(PlaytestSnapshotDto))]
    internal sealed partial class PlaytestJsonContext : JsonSerializerContext
    {
    }
}
