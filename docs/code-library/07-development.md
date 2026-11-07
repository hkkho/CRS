# 7. Build, tests and documentation

Use the [release guide](../maintenance/hosting.md) for local setup, verification and the Pages path. Use the [implementation guide](../IMPLEMENTATION_GUIDE.md#architecture-and-ownership) to find the code that owns a behavior.

## Trace a failure

| Symptom | Start here |
| --- | --- |
| Core never becomes ready | [Worker loading and startup](01-startup.md), then the browser console and staged WASM assets |
| A command is rejected | [Wire diagnostics](02-browser-transport.md) and the shared session |
| A step gives unexpected power, burnup or score | [Transaction boundaries](03-daily-turn.md) and [Core physics](04-core-physics.md) |
| A reading appears at the wrong time | [Presentation](05-presentation.md), comparing displayed and solved endpoints |
| An update fails to restore a run | [Checkpoint validation](06-saves-lifecycle.md) and the recorded pack, cadence and scoring IDs |

## Maintain this library

`mkdocs.yml` defines the published navigation. The retained F1 dialog bundles the same four manual chapters through [manualContent.ts](../../web/candu-playtest/src/manualContent.ts).

An `annotated-source` fence declares a file, exact start and end anchors and consecutive explanation groups. [The build hook](../../tools/docs/annotated_source.py) resolves those anchors against the current source and rejects missing anchors or uncovered lines. [The link hook](../../tools/docs/repository_links.py) validates checkout-relative links and rewrites links outside this site to the matching GitHub revision. Use `CRS_DOCS_REF` to identify that revision in a local build.
