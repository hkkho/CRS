# Repository working instructions

These instructions apply to the whole repository.

## Product priority

- Treat `web/candu-playtest` and its Vercel deployment as the product and
  primary acceptance path.
- Keep the browser client a consumer of the authoritative shared simulation;
  do not duplicate reactor rules in TypeScript or Phaser.
- Keep shutdown, scram, accident progression, operator-training scenarios, and
  full plant operations out of scope.
- Prefer small, testable changes that improve the playable browser loop,
  feedback, performance, reliability, or accessibility.

Read `README.md`, `docs/IMPLEMENTATION_GUIDE.md`, and
`docs/WEB_ROADMAP.md` before substantial work.

## Architecture boundaries

- `src/ReactorSim.Core` owns deterministic simulation state, topology,
  refuelling, inventory, control contracts, and spatial solving.
- `src/ReactorSim.Game` owns the reusable run/session, commands, and immutable
  presentation snapshots.
- `src/ReactorSim.Browser` owns the versioned browser bridge contract.
- `src/ReactorSim.BrowserHost` owns the browser-WASM executable host.
- `web/candu-playtest` owns browser transport and presentation only.
- The active runtime consumes project-authored compact embedded physics packs;
  external analysis programs are never runtime dependencies.

Preserve units, group ordering, deterministic behavior, provenance, and failure
semantics when shared simulation code changes.

## Testing and completion

- Keep `web/candu-playtest` runnable after each implementation slice.
- Add focused tests for changed behavior instead of broad speculative suites.
- Use `tools/Test-DotNet.ps1` for shared Core/Game/Browser checks.
- Use `tools/Test-Browser.ps1` for the browser bridge, Vitest, and production
  frontend build.
- For deployment-sensitive changes, verify the Vercel smoke/reproduction path.
- Inspect the final diff and keep unrelated changes out of the commit.
