using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReactorSim.Browser;

if (args[0] == "--fixtures")
{
    var runtime = new PlaytestRuntime();
    var fixtures = new List<object>();
    string initialize = "{\"protocol\":\"candu-playtest-v2\",\"mode\":\"play\",\"seed\":1001}";
    Add("full", "initialize", initialize, runtime.Initialize(initialize));
    Dispatch("compact", "{\"type\":\"pause\"}", 0);
    Dispatch("replacement", "{\"type\":\"commit-refuel\",\"request\":{\"channelIndex\":210,\"directionId\":\"toward-end-b\",\"shiftCount\":8,\"fuelTypeId\":\"NAT-U-SYNTHETIC\"}}", 1);
    Dispatch("failure", "{\"type\":\"queue-power-target\",\"targetFraction\":0.95}", 2);
    Dispatch("resync", "{\"type\":\"resume\"}", 0);
    Dispatch("reset", "{\"type\":\"reset\"}", 3);
    using var file = File.Create(args[1]);
    using var gzip = new GZipStream(file, CompressionLevel.SmallestSize);
    JsonSerializer.Serialize(gzip, fixtures);
    Console.WriteLine($"Recorded {fixtures.Count} shared wire fixtures to {args[1]}");
    return;
    void Dispatch(string name, string payload, int sequence)
    {
        string request = "{\"protocol\":\"candu-playtest-v2\",\"responseMode\":\"compact\",\"baseSequence\":" + sequence + ",\"payload\":" + payload + "}";
        Add(name, "dispatch", request, runtime.DispatchJson(request));
    }
    void Add(string name, string operation, string request, string response)
    {
        fixtures.Add(new
        {
            name, operation, request, response,
            resyncSnapshot = name == "resync" ? runtime.GetSnapshotJson() : null,
            sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(response))).ToLowerInvariant()
        });
    }
}

var rows = new List<object>();
foreach (int seed in new[] { 1001, 1002 })
{
    Capture("initialize", PlaytestBridgeV2.Initialize($"{{\"protocol\":\"candu-playtest-v2\",\"mode\":\"play\",\"seed\":{seed}}}"));
    foreach (var payload in new[] {
        "{\"type\":\"pause\"}",
        "{\"type\":\"queue-power-target\",\"targetFraction\":0.95}",
        "{\"type\":\"resume\"}",
        "{\"type\":\"queue-power-target\",\"targetFraction\":0.95}",
        "{\"type\":\"advance\",\"wallMilliseconds\":100}",
        "{\"type\":\"commit-refuel\",\"request\":{\"channelIndex\":210,\"directionId\":\"toward-end-b\",\"shiftCount\":8,\"fuelTypeId\":\"NAT-U-SYNTHETIC\"}}",
        "{\"type\":\"commit-refuel\",\"request\":{\"channelIndex\":999,\"directionId\":\"toward-end-b\",\"shiftCount\":8,\"fuelTypeId\":\"NAT-U-SYNTHETIC\"}}",
        "{\"type\":\"set-playback-mode\",\"modeId\":\"10x\"}",
        "{\"type\":\"advance\",\"wallMilliseconds\":100}",
        "{\"type\":\"solve\"}",
        "{\"type\":\"reset\"}" })
        Capture(payload, PlaytestBridgeV2.DispatchJson("{\"protocol\":\"candu-playtest-v2\",\"type\":\"command\",\"payload\":" + payload + "}"));
    void Capture(string command, string response)
    {
        rows.Add(new { seed, command, sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(response))).ToLowerInvariant()
        });
    }
}
var output = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(args[0], output);
Console.WriteLine($"Recorded {rows.Count} response byte hashes to {args[0]}");
