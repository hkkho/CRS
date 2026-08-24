# Mobile build prerequisites

This is a platform setup reference, not scope or task authorization. The
repository rules, implementation plan, current delivery register, and pinned
`unity/toolchain.json` take precedence.

## Scope and pinned Unity context

This repository is pinned to Unity 6.3 LTS Editor `6000.3.21f1` (revision
`c02631ffc030`) in [`unity/toolchain.json`](../../unity/toolchain.json). These
prerequisites apply to that pinned editor; do not substitute a different editor
or Android toolchain version without an approved task.

P0-T06 records prerequisites and inert examples only. It does not install a
mobile module, change Unity Player Settings, create a signing identity, or build
an Android or iOS player.

The files in [`unity/build-config/`](../../unity/build-config/) are JSON
examples, not active Unity configuration. Nothing in this repository reads or
applies them automatically. A later approved platform-build task must explicitly
map reviewed values into the Unity and, for iOS, Xcode build process.

## Shared configuration and secret boundary

Both examples use `version: 1` as the example-format version. Their application
identifiers, application versions, build numbers, and output paths are all
placeholders. Output paths are repository-relative; no absolute host path is
stored here.

The `signing` objects contain environment-variable *names* only. They never
contain a keystore, certificate, provisioning profile, password, serial, token,
or a resolved filesystem path. Supply real signing material only at build time
from a developer's secure local store or an approved CI secret store. Do not
commit it, print it in logs, or copy it into a task report.

The examples deliberately set `developmentBuild` to `true`. They are not a
release policy and must not be treated as a production-build command.

## Android prerequisites

### Editor and host setup

For the pinned Unity editor, use Unity Hub to add the following modules to the
same editor installation:

- Android Build Support
- Android SDK & NDK Tools
- OpenJDK

Unity's Unity 6.3 Android documentation lists all four dependencies and directs
developers to the editor-specific supported dependency versions. Prefer the
SDK, NDK, and OpenJDK supplied with the matching Unity module; a custom external
toolchain is a later, explicit configuration decision, not a value to record in
these examples. See [Android environment
setup](https://docs.unity3d.com/6000.3/Documentation/Manual/android-sdksetup.html)
and [supported dependency
versions](https://docs.unity3d.com/6000.3/Documentation/Manual/android-supported-dependency-versions.html).

Use a host that meets the pinned editor's current system requirements. The
platform build runner will also need a usable Android SDK Platform-Tools
installation so `adb` can communicate with a test device; do not hard-code the
SDK or `adb` location in source control.

### Device and ADB readiness

Before a later Android device smoke test, provide a physical Android device,
connect it over USB or an approved wireless ADB workflow, enable Developer
options and USB debugging where required, and accept the device's debugging
authorization prompt. Verify the connection with `adb devices`; the device must
appear as an authorized `device`, not `unauthorized` or `offline`.

Host integration can require device-specific setup: Windows can need the OEM USB
driver, and Linux can need the appropriate USB permissions or `udev` rules. The
official [Android physical-device guide](https://developer.android.com/studio/run/device)
and [Android Debug Bridge reference](https://developer.android.com/tools/adb)
cover these prerequisites and verification steps.

### Android signing boundary

Android release signing needs a signing key/keystore, a key alias, and their
associated secret values. The Android example refers only to these environment
variable names:

- `REACTORGAME_ANDROID_KEYSTORE_PATH`
- `REACTORGAME_ANDROID_KEYSTORE_PASSWORD`
- `REACTORGAME_ANDROID_KEY_ALIAS`
- `REACTORGAME_ANDROID_KEY_ALIAS_PASSWORD`

No signing value is configured in Unity Project Settings by this task. A later
approved build task must obtain the material from a secure local or CI secret
store and follow Android's [app-signing guidance](https://developer.android.com/studio/publish/app-signing).

## iOS prerequisites

### macOS, Xcode, and Unity setup

Add **iOS Build Support** to the pinned `6000.3.21f1` Unity editor through Unity
Hub. Unity generates an Xcode project, then Xcode builds that project into the
application. Consequently, final local iOS builds require **macOS and Xcode**;
they cannot be completed on Windows or Linux. See Unity's [iOS environment
setup](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-environment-setup.html)
and [iOS build process](https://docs.unity3d.com/6000.3/Documentation/Manual/iphone-BuildProcess.html).

### Apple account, signing, and provisioning

For development on a physical device, sign in to Xcode with an Apple Account,
select the appropriate team, and use managed signing or create the required App
ID, development certificate, registered device, and provisioning profile. An
Apple Developer Program membership is required for distribution and advanced
capabilities; Apple documents the distinction in its [developer account
overview](https://developer.apple.com/help/account/basics/about-your-developer-account)
and [development provisioning-profile guidance](https://developer.apple.com/help/account/provisioning-profiles/create-a-development-provisioning-profile/).

The iOS example refers only to these environment-variable names:

- `REACTORGAME_IOS_TEAM_ID`
- `REACTORGAME_IOS_SIGNING_CERTIFICATE`
- `REACTORGAME_IOS_PROVISIONING_PROFILE_SPECIFIER`

Actual certificates, private keys, provisioning profiles, team identifiers, and
Apple account credentials remain in Xcode/Keychain or an approved CI secret
store. Do not put any resolved value, certificate file, profile UUID, or host
path into a tracked configuration file.

### Physical-device validation is mandatory for final iOS builds

Simulators are useful during development, but they do not reproduce all physical
device behavior. Every final iOS build must be signed on macOS/Xcode and
installed and tested on one or more physical iPhone or iPad devices. Apple
documents device selection, signing, and testing in [Running your app on
simulated or physical devices](https://developer.apple.com/documentation/Xcode/running-your-app-on-simulated-or-physical-devices).

## Example-format reference

The examples are deliberately small and parseable so a future approved build
task can validate them before using them. They are not a public runtime schema.

| File | Required top-level values | Platform-specific values |
| --- | --- | --- |
| [`android.example.json`](../../unity/build-config/android.example.json) | `version` integer `1`; `platform` string `android`; `developmentBuild` Boolean | `application.identifier`, `application.versionName`, `application.versionCode`, `output.path`, and four Android signing environment-variable-name fields |
| [`ios.example.json`](../../unity/build-config/ios.example.json) | `version` integer `1`; `platform` string `ios`; `developmentBuild` Boolean | `application.identifier`, `application.bundleVersion`, `application.buildNumber`, `output.xcodeProjectPath`, `output.archivePath`, and three iOS signing environment-variable-name fields |

P0-T06 defers actual platform builds, Android installs, iOS archives, signing,
and physical-device smoke testing to the applicable later mobile milestone and
T5. No T5/platform build has been run for this documentation-only task.

## Official sources consulted

- [Unity 6.3 Android environment setup](https://docs.unity3d.com/6000.3/Documentation/Manual/android-sdksetup.html)
- [Unity 6.3 iOS environment setup](https://docs.unity3d.com/6000.3/Documentation/Manual/ios-environment-setup.html)
- [Unity 6.3 iOS build process](https://docs.unity3d.com/6000.3/Documentation/Manual/iphone-BuildProcess.html)
- [Android: run apps on a hardware device](https://developer.android.com/studio/run/device)
- [Android: app signing](https://developer.android.com/studio/publish/app-signing)
- [Apple: running on simulated or physical devices](https://developer.apple.com/documentation/Xcode/running-your-app-on-simulated-or-physical-devices)
- [Apple: developer account overview](https://developer.apple.com/help/account/basics/about-your-developer-account)
