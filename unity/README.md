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
