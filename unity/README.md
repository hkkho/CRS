# Unity bootstrap

This is an adapter setup guide, not scope or task authorization. Follow
[`AGENTS.md`](../AGENTS.md) for execution rules,
[`docs/Implementation_plan.md`](../docs/Implementation_plan.md) for the
architecture boundary, and
[`docs/PROJECT_SCOPE.md`](../docs/PROJECT_SCOPE.md) for current status.

The ReactorGame project is pinned to Unity 6.3 LTS `6000.3.21f1` revision `c02631ffc030`. The standalone Unity CLI is pinned to experimental version `1.0.0-beta.3`; it is an optional editor-management surface, not a runtime dependency.

After installing the official beta-channel Unity CLI, select the repository pin before using it:

```powershell
unity upgrade --target 1.0.0-beta.3
unity --version
unity install 6000.3.21f1 -c c02631ffc030 --accept-eula --non-interactive
```

Review and accept Unity's license terms yourself before running the install
command. This bootstrap guide does not require mobile modules.

The editor also requires an active Unity license. Check the machine state before running the smoke:

```powershell
unity license status
```

For a Unity Personal license, sign in and personally review and accept the applicable terms before activation:

```powershell
unity auth login
unity license activate --personal --accept-eula
```

Use the serial, license-file, or floating-license activation supported by your organization instead when applicable. Do not commit credentials or license files.

## Core reference

Unity consumes the engine-neutral Core as a generated `netstandard2.1` plugin. The source remains under `src/ReactorSim.Core`; it is not copied into the Unity project.

```powershell
.\tools\Prepare-UnityCore.ps1
```

The generated `Assets/Plugins/ReactorSim.Core.dll` is ignored. Its committed `.meta` file marks it as explicitly referenced, and only the `ReactorGame.Unity` assembly definition names it.

Core's JSON dependency is not copied into `Assets/Plugins`. The project pins
Unity's official `com.unity.nuget.newtonsoft-json` package `3.2.2`, which
corresponds to the Core NuGet pin Newtonsoft.Json `13.0.2`. Runtime codecs use
explicit JSON token reads/writes as specified by
[`ADR-011`](../docs/adr/ADR-011-engine-neutral-json-serialization.md).

## Import and compile smoke

The supported fallback invokes the pinned editor directly in batch mode,
imports the project, compiles both adapter assemblies, executes the Core JSON
token probe through the pinned Unity package, and opens the empty bootstrap
scene:

```powershell
.\tools\Test-UnityImport.ps1
```

Pass `-UnityEditorPath` when the editor is installed outside the standard
Unity Hub directory. The experimental CLI may open the project with
`unity open ./unity/ReactorGame`, but the direct editor wrapper remains the
reproducible smoke path; the CLI is optional.

P0-T08 also includes a built-player execution hook for gate evidence. It is a
no-op during normal startup and activates only when both
`-canduSerializationSmokeMarkerPath` and
`-canduSerializationSmokeMarkerToken` are present. The hook writes its marker
only after the Core token probe succeeds, then exits the player with code zero.
It is infrastructure, not a game command or serialization contract.

## Offline graphical demo

Use `tools/Build-UnityDemo.ps1` to build the visible Bootstrap scene for a
manual graphical smoke, then launch it with `tools/Start-UnityDemo.ps1`. The
launcher first removes only confirmed Candu Unity Test Runner players under
the temporary `candu-*` artifact roots, preventing a stale `PlayerWithTests`
process from owning a Windows network prompt. This path uses a normal
`StandaloneWindows64`
`BuildPipeline` player and deliberately does not pass Unity Test Runner
arguments (`-runTests`, `-testPlatform`, or `-testResults`). The resulting
player is therefore an offline demo and does not open the Test Runner result
channel that can trigger a Windows network-permission prompt. Keep the
headless EditMode/PlayMode/desktop test commands separate from the player used
for visual inspection. The launcher does not change Windows Firewall rules or
any other security setting.

The repository-local `.codex/config.toml` already marks this project as trusted
for autonomous repository work. Host-level Computer Use access is controlled
by Codex and Windows, not by Unity project files; the project does not attempt
to weaken or bypass that platform permission boundary.

## Phase 10 runtime seam

`Assets/ReactorGame.Unity/Phase8UnityRuntimeAdapter.cs` is the first bounded
Phase 10 presentation/input slice. `Phase8UnityRuntimeAdapter` forwards
sequence-numbered power, tilt, playback, pause/resume, and explicit wall-time
commands to an injected `IPhase8RuntimePort`; the port remains the sole owner
of the engine-neutral CLI/Core runtime and state transitions. The adapter
publishes immutable `Phase8UnityPresentationSnapshotV1` values for UI binding
and rejects duplicate command sequences before they reach the port.

The adapter intentionally has no `Update` loop and never reads Unity frame
time. The host supplies integer wall milliseconds explicitly, preserving the
approved Phase 8 100 ms control tick, default 10x acceleration, and one
simulation-second presentation cap. Parameter-pack loading, playback-mode
validation, simulation advancement, save/replay, and mobile-device behavior
remain outside this first seam and require their own bounded tasks.
