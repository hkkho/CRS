using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReactorSim.Browser;

if (args.Length != 2 || args[0] != "--fixtures")
{
    Console.Error.WriteLine("Usage: BrowserWireFixtures --fixtures <output.json.gz>");
    return 1;
}

var runtime = new PlaytestRuntime("real-time");
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
return 0;
void Dispatch(string name, string payload, int sequence)
{
    string request = "{\"protocol\":\"candu-playtest-v2\",\"responseMode\":\"compact\",\"baseSequence\":" + sequence + ",\"payload\":" + payload + "}";
    Add(name, "dispatch", request, runtime.DispatchJson(request));
}
void Add(string name, string operation, string request, string response)
{
    fixtures.Add(new
    {
        name,
        operation,
        request,
        response,
        resyncSnapshot = name == "resync" ? runtime.GetSnapshotJson() : null,
        sha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(response))).ToLowerInvariant()
    });
}
