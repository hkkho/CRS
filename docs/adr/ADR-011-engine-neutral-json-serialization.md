# ADR-011: Engine-neutral JSON serialization

- Status: Accepted
- Date: 2026-08-07
- Decision owner: P0-T08; historical review evidence is preserved in P0-T08,
  G0, and G0-R1
- Related tasks/gates: P0-T08, G0, G0-R1
- Supersedes: None
- Superseded by: None

## Context

Phase 0 must select and pin a serialization library before later tasks define
data-pack, save, or replay contracts. The Core targets .NET Standard 2.1 and
must remain independent of Unity. Unity consumes a generated Core DLL, so a
Core package dependency must also be supplied to the Unity editor and player;
loading `ReactorSim.Core.dll` alone does not prove that dependency resolves.

Android and iOS use IL2CPP/AOT for the intended delivery path. Unity warns that
reflection-driven serialization can fail when the AOT compiler or managed
linker cannot infer the required types and methods. No approved save, replay,
data-pack, or other public serialization schema exists in Phase 0.

## Decision

- JSON is the serialization syntax for future engine-neutral contracts unless
  an approved later ADR supersedes this decision.
- `ReactorSim.Core` references NuGet package `Newtonsoft.Json` with the exact
  version range `[13.0.2]`.
- Unity supplies that dependency through the official package
  `com.unity.nuget.newtonsoft-json` version `3.2.2`. Unity documents that
  package as corresponding to upstream Newtonsoft.Json `13.0.2`.
- Runtime codecs must read and write named JSON tokens explicitly with
  `JsonReader`/`JsonWriter` or the equivalent manual LINQ-to-JSON token APIs.
  Domain objects must not be mapped through reflection-based `JsonSerializer`
  or `JsonConvert` object serialization, `JToken.FromObject`/`ToObject`,
  contract resolvers, or serializer attributes.
- This ADR selects only the mechanism and dependency boundary. It does not
  define field names, schemas, version numbers, migrations, compatibility
  rules, units, checksums, or canonical byte representations for any future
  contract.

The machine-readable pins live in the Core project, the Unity package manifest
and lock, and `unity/toolchain.json`. `Prepare-UnityCore.ps1` copies only
`ReactorSim.Core.dll`; it verifies the matching pins and rejects a manually
copied `Assets/Plugins/Newtonsoft.Json.dll` so the dependency has one Unity
owner.

## Architectural boundaries and invariants

- Core uses no Unity type, lifecycle call, serialization attribute, or asset.
- The serializer package has no runtime dependency beyond its .NET Standard
  2.0 assembly for the Core target.
- Codec calls are statically reachable and do not require runtime code
  generation or reflection-based domain discovery.
- Unity presentation code may invoke Core codecs but must not own simulation
  state transitions or duplicate contract logic.
- A package or strategy upgrade is a runtime/serialization trigger requiring
  an approved task, T3, T4, and IL2CPP/mobile evidence where applicable.

## Consequences

### Positive

- The same upstream serializer implementation and version serves headless .NET
  and Unity without copying an unmanaged or ad hoc dependency into Plugins.
- Unity's package provides separate editor and AOT player assemblies at file
  version 13.0.2; the package importer selects the AOT asset for players.
- Explicit token codecs make field handling reviewable and keep reachable AOT
  code visible to static analysis.
- Json.NET 13.0.2 targets .NET Standard 2.0 without transitive dependencies,
  reducing Unity dependency-copy and version-conflict risk.

### Negative or limiting

- Manual codecs require more code and focused tests than reflection mapping.
- This decision intentionally pins an older upstream version because it is the
  version wrapped by Unity's official 3.2.2 package. Upgrades must keep the
  NuGet and Unity package implementations aligned.
- The Phase 0 Windows module supports the Mono standalone player only. The
  execution probe establishes editor/player dependency resolution and a
  statically reachable token path, but it is not a substitute for the first
  approved Android/iOS IL2CPP T5 build and device smoke.

## Alternatives considered

### System.Text.Json with manual token codecs

- Reason not selected: its low-level token APIs fit the AOT policy, but Unity
  does not provide a matching project runtime package. Supplying and reconciling
  its .NET Standard dependency graph manually would add more plugin-copy and
  version-conflict surface than the official Unity Json.NET package.

### Newtonsoft.Json reflection object mapping

- Reason not selected: it hides field selection in runtime contract discovery
  and creates the reflection/linker/AOT risk this decision must avoid.

### Unity `JsonUtility`

- Reason not selected: it would couple serialization behavior to Unity's type
  and field-serialization rules and cannot be the engine-neutral Core strategy.

### A custom JSON parser and writer

- Reason not selected: implementing a general JSON grammar would add security,
  correctness, Unicode, and maintenance risk without a project-specific need.

## Validation and evidence

- A Core-owned compatibility probe explicitly writes and reads JSON tokens.
- The focused Core test and full T3 suite execute that probe under .NET.
- The Unity import smoke calls the same Core method after package resolution.
- The desktop player exposes an infrastructure-only, command-line-gated hook;
  it writes a unique marker only after the same Core method succeeds, then
  exits. Normal player startup has no probe behavior or state.
- The clean-source Windows player contained a 13.0.2 serializer whose SHA-256
  exactly matched the resolved package's `Runtime/AOT/Newtonsoft.Json.dll`, and
  that player executed the Core token probe successfully.
- Clean-source T4 rebuilds the ignored Core plugin, resolves the official Unity
  package, imports the project, builds the player, and runs the marker path.

## Follow-up

- Phase 3 must define each serialization contract and its validation behavior
  in an approved specification before implementing codecs.
- The first approved Android/iOS build task must execute representative manual
  codecs under IL2CPP and managed stripping as part of T5.
- G0-R1 must inspect this ADR and rerun the corrective gate evidence.

## References

- [`docs/Implementation_plan.md`](../Implementation_plan.md)
- [`docs/gates/G0.md`](../gates/G0.md)
- [Unity Newtonsoft Json package 3.2.2](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html)
- [NuGet Newtonsoft.Json 13.0.2](https://www.nuget.org/packages/Newtonsoft.Json/13.0.2)
- [NuGet exact version ranges](https://learn.microsoft.com/nuget/concepts/package-versioning)
- [Unity scripting restrictions](https://docs.unity3d.com/Manual/scripting-restrictions.html)
- [Unity managed code stripping](https://docs.unity3d.com/Manual/managed-code-stripping.html)
- [Json.NET manual JSON reading and writing](https://www.newtonsoft.com/json/help/html/readingwritingjson.htm)
