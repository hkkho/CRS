using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ReactorSim.Browser;
using Xunit;

namespace ReactorSim.Browser.Tests;

public sealed class InstanceRuntimeContractTests
{
    [Fact]
    public void SharedWireFixturesReplayWithIdenticalBytes()
    {
        using var file = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "playtest-v2.json.gz"));
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var fixtures = JsonDocument.Parse(gzip);
        var runtime = new PlaytestRuntime();
        Assert.Equal(6, fixtures.RootElement.GetArrayLength());
        foreach (var row in fixtures.RootElement.EnumerateArray())
        {
            string request = row.GetProperty("request").GetString()!;
            string actual = row.GetProperty("operation").GetString() == "initialize"
                ? runtime.Initialize(request) : runtime.DispatchJson(request);
            Assert.Equal(row.GetProperty("response").GetString(), actual);
            Assert.Equal(row.GetProperty("sha256").GetString(),
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(actual))).ToLowerInvariant());
            if (row.GetProperty("name").GetString() == "resync")
                Assert.Equal(row.GetProperty("resyncSnapshot").GetString(), runtime.GetSnapshotJson());
        }
    }

    [Fact]
    public void IndependentRuntimesKeepSessionReplayCacheAndCountersIsolated()
    {
        var first = new PlaytestRuntime();
        var second = new PlaytestRuntime();
        first.Initialize("{\"protocol\":\"candu-playtest-v2\",\"seed\":1001}");
        second.Initialize("{\"protocol\":\"candu-playtest-v2\",\"seed\":1002}");
        string before = second.GetSnapshotJson();
        int count = second.CoreSnapshotMaterializationCount;
        first.ResetCoreSnapshotMaterializationCount();
        first.DispatchJson("{\"protocol\":\"candu-playtest-v2\",\"type\":\"pause\"}");
        first.DispatchJson("{\"protocol\":\"candu-playtest-v2\",\"type\":\"reset\",\"seed\":1003}");
        Assert.Equal(before, second.GetSnapshotJson());
        Assert.Equal(count + 1, second.CoreSnapshotMaterializationCount);
        Assert.NotEqual(first.GetSnapshotJson(), before);
    }
}
