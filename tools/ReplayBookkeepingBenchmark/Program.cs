using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using ReactorSim.Browser;
using ReactorSim.Game;

// Solver-free measurement of the exact old hashing/storage path and replacement.
// Retained bytes are UTF-8 replay evidence payload, excluding object/list overhead.
// Allocations include transient hashing buffers. Warm both paths before measuring.
const string initialization = "{\"protocol\":\"candu-playtest-v2\",\"mode\":\"play\"}";
const string prefix = PracticeScoring.PolicyId + "|play|" + initialization + "|";
Console.WriteLine("count,path,total_ms,us_per_command,allocated_bytes,retained_payload_bytes");
Measure(100, false, false); Measure(100, true, false);
foreach (int count in new[] { 100, 1000, 10000 })
{
    Measure(count, false, true);
    Measure(count, true, true);
}

static void Measure(int count, bool chained, bool print)
{
    var chain = new ReplayDigestChain("play", initialization);
    var commands = new List<string>();
    var history = new List<BridgeHistoryEntry>();
    long retained = 0;
    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    long before = GC.GetAllocatedBytesForCurrentThread();
    var timer = Stopwatch.StartNew();
    for (int index = 0; index < count; index++)
    {
        // Unique cheap canonical dispatches, no reactor or solver work.
        string command = "{\"requestId\":" + index + ",\"type\":\"pause\"}";
        if (chained) chain.Append(command);
        else
        {
            commands.Add(command);
            history.Add(new BridgeHistoryEntry
            {
                Sequence = (ulong)index + 1,
                Type = "pause",
                CommandJson = command,
                Accepted = true,
                StateDigest = "sha256:" + new string('0', 64)
            });
            _ = PlaytestProtocolV2.ComputeDigest(prefix + string.Join("|", commands));
            // Same command reference in both lists: count its payload only once.
            retained += Encoding.UTF8.GetByteCount(command) + 71;
        }
    }
    timer.Stop();
    long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
    if (chained) retained = Encoding.UTF8.GetByteCount(chain.Digest);
    if (print) Console.WriteLine($"{count},{(chained ? "chain-v2" : "legacy-v1")},{timer.Elapsed.TotalMilliseconds:F3},{timer.Elapsed.TotalMicroseconds / count:F3},{allocated},{retained}");
    GC.KeepAlive(commands); GC.KeepAlive(history); GC.KeepAlive(chain);
}
