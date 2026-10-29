# Existing tools and what to start from

Checked **24 October 2026**. Repository revisions and observed package versions
are recorded in `toolkit-sources.json`. A repository's default-branch commit is
a research snapshot, not a recommendation to depend on an unreleased branch.
None of the third-party installers or paid generation pipelines was run for this
study. Recommendations below are engineering judgments from source inspection,
not a benchmark of games produced by every toolkit.

## Recommendation

For this reusable skill, start with a small engine-independent design and
verification workflow, original runnable pattern examples, and a choice of
existing engines. For a **new sprite-heavy 2D game**, the official
[Phaser TypeScript/Vite template](https://github.com/phaserjs/template-vite-ts)
is the strongest small starting point in this comparison. For interface-heavy
simulations or the existing CRS application, keep the current DOM/SVG/Canvas
presentation and authoritative simulation boundaries. A different engine is not
required to add better decisions or feedback.

For a later automated asset-production experiment,
[Tencent Game-Creation-Agents](https://github.com/Tencent/Game-Creation-Agents)
is the closest complete off-the-shelf pipeline found. Pilot it in an isolated
project with an explicit service budget and one modest game before adopting it
as the default. Its setup is substantially larger than a portable Codex skill.

## Agent skills and complete production pipelines

| Candidate | What is provided | Evidence and compatibility | Decision |
| --- | --- | --- | --- |
| [Tencent GCA](https://github.com/Tencent/Game-Creation-Agents) | Design, scene/asset production, integration and verified build gates for Phaser 4 | Its README requires Claude Code, Python, Node, Chromium and media tools; Windows uses WSL2. Its default media stack includes OpenAI and Volcengine ARK services. Main code is Apache-2.0 with separate third-party notices in LICENSE. | Best full pipeline to pilot later; use the design-to-build contracts and independent visible-outcome checks as architectural references now. |
| [OpenGame](https://github.com/leigest519/OpenGame) | An agent runtime with reusable project templates and systematic debugging | Apache-2.0; source installation is documented. Supports OpenAI-compatible model APIs and separately configured media providers. The README still calls the evaluation pipeline forthcoming. | Useful architecture and template research. It is a separate runtime rather than a small drop-in skill; do not equate paper results with independently verified production quality. |
| [LeanEntropy Three.js/Phaser skills](https://github.com/LeanEntropy/threejs-phaser-game-skills) | Portable skills for gameplay, art, interface, debugging and release | MIT notice present; its Phaser suite explicitly targets Phaser 3. Director includes extensive sibling-skill routing and optional external asset services. | Most directly reusable permissively licensed skill pack found, especially for Phaser 3 or Three.js. Audit and trim routing, then adapt API references if adopting Phaser 4. |
| [Yakoub Phaser 4 toolkit](https://github.com/Yakoub-ai/phaser4-gamedev) | Portable engine-specific skills and a real-browser playtest harness, plus Claude-specific integrations | README describes Phaser 4 and portable Codex installation. No repository license was reported by GitHub and a root LICENSE fetch returned 404 at this snapshot. | Study its documented runtime-verification approach. Resolve reuse terms before copying or redistributing its files. This is an observed licensing gap, not a claim about the author's intent. |
| [Historical OpenAI develop-web-game skill](https://github.com/openai/skills/commit/11c643813b4645ca9f25d49ca180697732e0141a) | An earlier browser-game development/testing skill | The official repository removed it in commit `11c6438` on 23 April 2026; its former main-branch path returns 404. Historical source remains in Git history. | Do not recommend a current-main installer command for a removed skill. A historical revision is a separate, explicitly pinned research source. |

The [GCA paper](https://arxiv.org/abs/2610.16776) describes build-linked verification
and a critic judging rendered interfaces. The [OpenGame paper](https://arxiv.org/abs/2604.18394)
describes template and debugging skills. These are useful hypotheses about
reliable production structure. Their authors' comparisons do not demonstrate that
our new skill produces enjoyable games; that needs its own evaluation.

GCA's optional RMBG-2.0 integration has distinct noncommercial model terms;
its README says the default background-removal path uses computational color
keying. This study uses neither and bundles no model weights. The GitHub API's
`NOASSERTION` license label for GCA is not the same as having no license: its
[actual LICENSE](https://github.com/Tencent/Game-Creation-Agents/blob/main/LICENSE)
contains the Apache notice and third-party terms.

## Engines, templates and graphics

| Starting point | Appropriate use | Cost of choosing it | Source |
| --- | --- | --- | --- |
| DOM + SVG/Canvas + ES modules | Cards, tactile workspaces, graphs, small agents and existing simulation clients | Build the needed scene and feedback helpers; keep canvas-only controls accessible through DOM alternatives | Peckin' Pixels' creator explicitly reports a JavaScript/Canvas implementation without an engine; the lab also uses this approach. |
| Phaser + official Vite/TS template | Sprite-driven 2D scenes, cameras, collision, tweens and particles | Respect the installed major version; avoid mixing v3 tutorials with v4 renderer APIs | [Official overview](https://docs.phaser.io/phaser/getting-started/what-is-phaser), [template package](https://github.com/phaserjs/template-vite-ts/blob/main/package.json) |
| PixiJS | Custom 2D rendering and large numbers of sprites with a separately owned simulation | Supply game state, scene coordination, physics if needed and application structure | [PixiJS introduction](https://pixijs.com/8.x/guides/getting-started/intro) |
| LittleJS | Compact code-first arcade prototypes and procedural graphics | Learn a smaller ecosystem; verify the example covers the intended mobile/input requirements | [Source and MIT license](https://github.com/KilledByAPixel/LittleJS) |
| KAPLAY | Quickly assembling small JavaScript games with component-style objects | Verify the current release, plugin status and required behavior rather than relying on older Kaboom examples | [Source and MIT license](https://github.com/kaplayjs/kaplay) |
| GDevelop | A visual event workflow and browser exports for people who prefer an editor | Event/project authoring differs from a conventional TypeScript codebase; evaluate export and collaboration workflow | [Official site](https://gdevelop.io/), [source](https://github.com/4ian/GDevelop) |
| Kenney asset packs | Coherent small worlds, UI, input icons and temporary-to-production art | Choose one compatible family, retain the included license and inspect runtime readability | [Asset catalog](https://kenney.nl/assets), [CC0 statement](https://kenney.nl/support) |

Phaser's official template at this snapshot calls itself a Phaser 3 project in
its README and package description, while its `package.json` pins `phaser` to
**4.0.0**. The npm registry reported **4.2.1** as current during this research.
That discrepancy is why a future task should inspect and pin the chosen template
and dependency, then run the actual build. Do not silently upgrade it merely to
make those labels agree. This study inspected that template; it did not run its
install scripts or certify its compatibility with a newer Phaser release.

The lab is dependency-free and intentionally avoids selecting a universal engine
for every future idea. Engine choice follows the needed interaction and existing
architecture. Waterworks' creator identifies Phaser, The MachinEGG identifies
PixiJS, and Gourdlets identifies Godot; successful examples do not converge on a
single engine.

## Adoption experiment

Use one concrete task such as “build a ten-minute repair-desk game with two valid
workflows, a tool upgrade, a readable failure and a saved shift.” Compare the
small starter plus this skill against a chosen off-the-shelf pipeline under the
same time and service budget. Inspect the delivered browser interface without
reading its code first, then run its build and behavioral checks. Record how much
manual repair was needed, what can be replayed and what players understood.

Adopt only the machinery that improves that outcome. Avoid copying mandatory
credential probes, unrelated settings, deployment permissions or multi-agent
orchestration into every simple game task. A generic promise of “AAA” is not an
acceptance criterion; a readable action, satisfying feedback and a tested complete
loop are reviewable artifacts.
