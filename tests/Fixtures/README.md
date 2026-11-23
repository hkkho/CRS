# Browser protocol fixtures

`playtest-v2.json.gz` contains exact UTF-8 JSON responses from the C# browser runtime. Compression avoids repeating thousands of channel and bundle fields. Each record contains a request, operation, response bytes and SHA-256 digest, with a full resynchronization snapshot where needed.

The C# fixture tests replay requests on an independent runtime and compare response bytes. TypeScript tests use the production parser and materializer to check replacement, omission, failure and reset behavior. The corpus exercises real-time mode through `PlaytestRuntime("real-time")`; focused bridge tests cover daily mode and full and compact responses.

The fixture records pack and cadence identities, reactivity, scoring, channel-flow refuelling, aged inventory, zone diagnostics, and fuel accounting. Geometry fields are presentation data and do not alter simulation or replay digests. A client receiving a response without optional geometry reports locations as unavailable rather than inventing a layout. Daily fields may be absent from real-time responses.

After reviewing an intentional protocol change, regenerate from the repository root:

```powershell
dotnet run --project tools/BrowserWireFixtures -- --fixtures tests/Fixtures/playtest-v2.json.gz
```

Review the resulting changes and run the browser bridge checks in [release and reproduction](../../docs/maintenance/hosting.md). The fixture is an acceptance baseline, so do not regenerate it merely to hide unexpected drift. See the [protocol specification](../../docs/spec/browser-playtest-protocol-v2.md) for message and replay semantics.
