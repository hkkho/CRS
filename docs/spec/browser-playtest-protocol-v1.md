# Browser playtest protocol v1

The browser pivot uses `candu-playtest-v1` as a deliberately small boundary
between the web presentation and the engine-neutral C# model. It is a local,
single-session protocol for synthetic data; it is not an API contract for a
production reactor service.

## Operations

The browser host may expose these JSON operations:

- `GetCapabilities()` returns protocol/schema versions, available `play` and
  `lab` modes, supported commands, model/data identities, units, and the
  two-group ordering.
- `Initialize(requestJson)` creates one deterministic session and returns the
  initial state. The request selects the mode and may opt into a detailed
  fixture/full-synthetic diagnostic view.
- `Dispatch(commandJson)` validates and applies one command, returning a
  post-command snapshot, diagnostics, and any preview/solver result.

The minimal command envelope is:

```json
{
  "protocol": "candu-playtest-v1",
  "type": "command",
  "payload": {
    "type": "advance",
    "wallMilliseconds": 100
  }
}
```

Commands are deterministic and include `advance`, `step`, playback/pause and
resume, power/tilt target queues, refuel preview/commit, and reset. A rejected
command returns the unchanged authoritative state with an explicit diagnostic;
it never applies a partial mutation.

## State and reproducibility

Responses include a monotonically increasing sequence, state digest, model/data
identities, the 380-channel/12-position presentation projection, and the
command history/replay digest. Replay archives contain initialization JSON and
canonical command JSON. Digests use canonical UTF-8 JSON and SHA-256; unsigned
64-bit values are serialized as strings where JavaScript's safe integer range
would otherwise be exceeded.

Values are finite only. Missing or inapplicable measurements are represented as
`null`, never `NaN` or infinity. Units are explicit: simulation time is SI
seconds, wall time is milliseconds, burnup is `MWd/kg_HM` in the presentation
projection (`J/kg_HM` in Core), energy is joules, mass is kilograms, volume is
`m^3`, and the two energy groups are ordered fast-to-thermal as group 1 then
group 2.

## Modes and authority

Play mode calls `PracticeGameSessionFactory`/`GameSession` and is the only owner
of the reduced game response. Lab mode invokes the existing Core spatial
contracts on an explicit synthetic fixture and reports convergence diagnostics.
Refuelling is atomic: the candidate inventory, coefficient bindings, and solve
result must all validate before the lab state is replaced. Nonconverged or
invalid results fail closed and leave the prior state active.

When browser WASM is not loaded, the web app may show a stable compatibility
fixture for layout and interaction feedback. It must be labelled
`synthetic-fixture`, must not be described as solver output, and must not be
used as evidence that the Core algorithm matches Unity.

## Hosting boundary

The bridge is compiled to browser WASM and copied into
`web/candu-playtest/public/wasm`. The Vite app and bridge assets are static and
can be deployed from that directory to Vercel. No backend, authentication,
telemetry, or DRAGON5/DONJON5 executable is part of this milestone.
