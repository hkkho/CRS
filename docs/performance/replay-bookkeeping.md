# Replay bookkeeping measurement

Task 09, 2026-10-02. Release .NET 10 on the development machine. Run:

```powershell
dotnet run --project tools/ReplayBookkeepingBenchmark/ReplayBookkeepingBenchmark.csproj -c Release
```

The benchmark sends unique canonical pause records through the exact old
list/join/hash bookkeeping and the new chain. It excludes initialization,
simulation, solver, projection, transport, and JSON serialization. Both paths
are warmed before recording. Timings are local observations, not browser
latency guarantees. Allocations are thread allocation counts during the loop.
Retained payload counts UTF-8 command evidence and stored state digests, counting
the shared command string once; it excludes object/list overhead and the fixed
initialization string. Thus the old retained number is a lower bound. The new
object retains only one 71-character digest string (including `sha256:`), with
zero retained command strings or diagnostic records.

| Commands | Algorithm | Total ms | µs / command | Allocated bytes | Retained payload bytes |
| ---: | --- | ---: | ---: | ---: | ---: |
| 100 | Previous canonical v1 | 0.571 | 5.708 | 1,032,568 | 10,190 |
| 100 | Chained v2 | 0.160 | 1.600 | 191,240 | 71 |
| 1,000 | Previous canonical v1 | 23.236 | 23.236 | 84,376,192 | 102,890 |
| 1,000 | Chained v2 | 1.450 | 1.450 | 1,934,440 | 71 |
| 10,000 | Previous canonical v1 | 2,054.809 | 205.481 | 8,472,754,232 | 1,038,890 |
| 10,000 | Chained v2 | 11.672 | 1.167 | 19,502,440 | 71 |

The replacement's per-command cost stays approximately constant at these sizes;
work still scales with the current command's byte length. The old path accumulates
quadratic hashing allocations. No numerical tolerance or simulation behavior
changed. The bridge no longer claims retained replay evidence: callers record
the initialization and sequence-advancing responses as a stream, as specified in
[protocol v2](../spec/browser-playtest-protocol-v2.md#replay-recording-and-identity).
The digest identifies that evidence but cannot replace it.
