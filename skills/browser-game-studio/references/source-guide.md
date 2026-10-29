# Read the starter from its entry point

The starter is deliberately small enough to trace without a framework. Its rule
model is a teaching implementation for these examples. An existing game's
authoritative simulation should remain in charge when applying the skill there.

| Read in this order | Responsibility and connection |
| --- | --- |
| [index.html](../assets/pattern-lab/index.html) | Declares the semantic page, canvas, readings, controls and design form; loads `app.mjs` as its module entry. |
| [project.json](../assets/pattern-lab/project.json) | Chooses lab/game mode, title, pattern and seed. `app.mjs` fetches it before creating state. |
| [catalog.mjs](../assets/pattern-lab/catalog.mjs) | Supplies names, design notes and reference links. It is presentation metadata, not the economic rules. |
| [model.mjs](../assets/pattern-lab/model.mjs) | Creates state, processes commands, advances time and validates saved snapshots. It imports no DOM or canvas code. |
| [app.mjs](../assets/pattern-lab/app.mjs) | Connects actual controls to model commands, schedules time, renders readings and requests feedback. It also handles local saves and exports. |
| [graphics.mjs](../assets/pattern-lab/graphics.mjs) | Draws original scenes from state and temporary presentation effects. It never grants resources or decides a shipment result. |
| [style.css](../assets/pattern-lab/style.css) | Defines the shared visual hierarchy, desktop side panel, stacked narrow layout, visible focus and motion preference rules. |
| [model.test.mjs](../assets/pattern-lab/model.test.mjs) | Exercises meaningful accepted/rejected outcomes, boundary conditions and deterministic behavior without a browser. |

## Startup

The HTML supplies stable element IDs. The module loads configuration, validates
its seed and chooses a pattern. In lab mode, a recognized `pattern` query selects
a study; in standalone game mode the saved configuration wins. `createState`
constructs that pattern's initial resources and seed. The application creates
navigation and action buttons, fills the design notes, mounts readable state
values and starts the animation loop. A resize observer scales the canvas to
its displayed width with a capped device-pixel ratio.

## One click through the system

An action button stores its action name and optional value. Its click listener
calls `act`, which passes both to `command`. The model validates the operation
before changing costs or benefits. A rejection returns the original state and
an explanation. Acceptance returns replacement state plus a feedback kind.

`act` adopts accepted state, writes the status message and adds a short-lived
visual effect for work, delivery or an upgrade. It then refreshes the DOM readings.
`drawScene` sees the new state on the next frame. There is one accepted purchase,
even when several frames depict its response. Buttons also let keyboard and
touch users perform actions that have a pointer-based canvas alternative.

## Time and animation

The frame callback computes visible elapsed time with a one-second ceiling,
accumulates it, and calls `advance` in 1/30-second steps. This bounds work after
a delayed frame. Each timed pattern owns its accounting: hens consume feed,
presses consume ore and buffer space, or plants consume moisture while growing.
Turn-based patterns simply keep their state until a command arrives.

Decorative time controls waves, gait poses, scene movement and brief effects.
Reduced motion freezes decorative movement while the same model steps still
produce valid outcomes. Pause stops those steps; commands remain available for
planning. A visibility change clears accumulated time and pointer activity,
preventing hidden time from unexpectedly producing resources. Production games
that promise offline progress need an explicit, separately tested policy.

## Persistence and extension

Saving serializes a versioned snapshot under a configuration/pattern-specific
local-storage key. Restoring calls `decode`, which checks shape, ranges and
pattern-specific consistency before the application adopts it. No saved data
is transmitted. An absent or rejected snapshot produces visible feedback.

To adapt a study, first change its player decision and success/sandbox promise.
Then change its command and time rules, add a focused invariant test, update the
DOM explanation and redraw the relevant world object. Finally perform the action
in the browser. Copying a theme into the catalog without changing the decision
creates a reskin; adding an animation callback that grants resources creates a
second accounting path. Neither is the intended extension model.
