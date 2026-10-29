# Run the original studies or create a prototype

The package has no JavaScript runtime dependencies. Use Python 3.9 or newer for
the preview/scaffold helpers and Node 20 or newer for the model tests. Browser
modules must be served over HTTP rather than opened with a file URL.

From this skill's directory, run:

```powershell
python scripts/serve.py --directory . --port 8010
```

This serves only the selected local directory on the loopback interface. Open
`http://127.0.0.1:8010/assets/pattern-lab/`. The explicit directory matters:
without it the helper serves its own scripts folder. Stop the preview with
Ctrl+C. The helper is a local development server, not a production hosting stack.

Each numbered item opens a fresh original experiment. Read its instruction beside
the scene. Buttons work without dragging; the postal scene also supports dragging
a parcel into a route tray. Spatial scenes accept arrows to select and Enter to
build. Harvesting accepts Space or the gather button as alternatives to hovering.
Pause stops timed production while allowing planning commands. Save/restore uses
local browser storage per pattern; closing a tab grants no offline production.
Reduced motion freezes decorative movement while preserving rule progression.
Sound is optional and requires a gesture. Download scene exports original canvas
art; the brief form exports a Markdown design hypothesis.

## Start a separate game

```powershell
python scripts/create_game.py C:/work/my-new-game --pattern post --title "Moon Parcel Office" --seed 42
```

The positional path must name a **new** output folder. The command copies the
original template and a preview server, then writes a game configuration selecting
the postal example, title and repeatable seed. It refuses to replace an existing
folder. Available patterns are `council`, `post`, `water`, `harvest`, `flock`,
`factory`, `garden`, `evolution`, `orb` and `hamlet`. A query string cannot override
the selected pattern in standalone mode. The other examples remain in the source
for development, while gallery navigation and research panels are hidden.

```powershell
python C:/work/my-new-game/serve.py --port 8011
```

This serves the generated directory at `http://127.0.0.1:8011/`. Develop its rules
in `model.mjs`, scene in `graphics.mjs`, and interface in `app.mjs`/`style.css`.
`project.json` controls the selected pattern, displayed title and seed. This is
a runnable seed, not an automatic conversion of an arbitrary idea into a complete
game. Rewrite its choices, theme and progression using the design framework.
Read [the source walkthrough](source-guide.md) to trace the entry point, rules,
controls, renderer, time loop and persistence before adapting them.

## Verify changes

```powershell
node --test assets/pattern-lab/model.test.mjs
python scripts/test_packaging.py
```

The first command checks the examples' rule invariants, including rejected
spending, capacity, network topology, deterministic trials and save validation.
The second checks safe scaffold creation and module-serving headers. Neither
command evaluates graphics or proves fun: perform real interface actions and
human playtests as described in [evaluation](evaluation.md).

The lab is static HTML/CSS/ES modules with relative application asset URLs.
A static host can serve the lab directory; a standalone export can use its output
directory as the site root. Verify MIME types, the chosen host's path behavior,
iframe sizing and saved-state origin before release. Publication is a separate
action requiring an authorized destination.
