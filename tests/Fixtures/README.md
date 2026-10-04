# Shared browser v2 wire fixtures

`playtest-v2.json.gz` contains exact UTF-8 JSON responses authored by the C#
runtime, compressed only to avoid repeating thousands of channel/bundle fields.
Each record includes request, operation, response bytes and SHA-256, plus a full
resync snapshot where required. C# replays these requests on an independent
runtime and compares every response byte; TypeScript uses the strict production
parser/materializer and asserts replacement/omission/failure/reset semantics.

Regenerate intentionally after reviewing protocol changes:

```
dotnet run --project tools/Phase4ContractCorpus -- --fixtures tests/Fixtures/playtest-v2.json.gz
```

Do not regenerate to conceal drift. `tools/Phase4ContractCorpus` also records the
two-seed characterization corpus (24 responses) to an output path. The original
pre-refactor hashes in `benchmarks/phase4-contract-baseline.json` remain the
acceptance baseline for Phase 4.

The current wire fixtures include the with-flow aged-core orientation, two
eight-bundle plans, automatic channel-flow orders, and LZC average-level
diagnostics. They were deliberately regenerated for these gameplay changes.
The original Phase 4 hashes remain a historical refactor baseline.
