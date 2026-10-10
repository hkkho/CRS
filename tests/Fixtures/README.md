# Shared browser v2 wire fixtures

The current core snapshots carry inserted-adjuster and LZC tube geometry and cell-overlap
indices projected by Game from Core. The presentation fields alone do not
alter simulation or replay digests. Older hosts can omit the geometry; the client
then reports locations unavailable rather than drawing an assumed layout.

The current fixtures were regenerated for the axial-marshak-v8 physics
pack. The wire schema is unchanged; physical values, pack identity and digests
intentionally change with the calibration.

`playtest-v2.json.gz` contains exact UTF-8 JSON responses authored by the C#
runtime, compressed only to avoid repeating thousands of channel/bundle fields.
Each record includes request, operation, response bytes and SHA-256, plus a full
resync snapshot where required. C# replays these requests on an independent
runtime and compares every response byte; TypeScript uses the strict production
parser/materializer and asserts replacement/omission/failure/reset semantics.

Regenerate intentionally after reviewing protocol changes:

```
dotnet run --project tools/BrowserWireFixtures -- --fixtures tests/Fixtures/playtest-v2.json.gz
```

Do not regenerate to conceal drift. The checked wire fixture is the current
protocol acceptance baseline; historical characterization hashes remain in Git history.

The v3 reactivity pack, three-minute LZC cadence identity and named refuelling
messages are reflected in the exact serialized responses.
The current wire fixtures include the with-flow aged-core orientation, two
eight-bundle plans, automatic channel-flow orders, and LZC average-level
diagnostics. They were deliberately regenerated for these gameplay changes.
The main-game fixtures now include explicit endless and unlimited-fuel flags,
zero numeric sentinels, and fuel-consumption accounting.


The retained v2 corpus runs with an explicit `PlaytestRuntime("real-time")`
constructor default so its original response bytes and digests remain covered.
Daily fields are omitted on legacy responses. The exact response bytes were
intentionally regenerated for the v8 axial Marshak pack and its new reference;
the real-time protocol shape remains covered. Daily default and full/compact
reports have separate focused bridge tests.
