---
name: browser-game-studio
description: Design, prototype and polish browser games using researched play patterns, coherent art direction, responsive feedback and real browser verification. Use for turning an idea into a playable game or improving its core loop and game feel; preserve an existing project's engine and simulation ownership.
---

# Browser Game Studio

Turn the user's idea into a readable, satisfying playable loop, then prove the
implemented actions and outcomes work. A themed dashboard, animation reel or
collection of disconnected features is not a complete game. A sandbox can be
complete without points or a win screen when creation and observation are the
intended reward.

## Choose the work that matters

Inspect the existing project, its instructions, state owner, renderer and release
path before selecting tools. Preserve the user's chosen engine and architecture.
Ask only for constraints that materially change the game; otherwise make a small,
reversible first slice. Do not replace the project with this skill's example game.

For a new concept, read [the design framework](references/design-framework.md).
Write a short playable brief: player role, repeated verb, visible consequence,
scarcity or creative constraint, competing options, progression, session ending
or sandbox return reason, and the first minute. Choose one primary play pattern
and at most one supporting pattern initially. Include an example decision with
specific costs and outcomes; nouns and a genre label alone are insufficient.

When reference research is needed, consult
[the case studies](references/case-studies.md) and
[evidence limits](references/research-method.md). For a different genre, research
relevant examples rather than assuming simulation findings cover it. Separate
creator statements, inspected behavior and your interpretation. Use existing
research selectively; do not repeat a ten-game survey for an ordinary feature.

## Select a practical starting point

Read [starter and skill comparisons](references/toolkit-selection.md) when choosing
a new stack. Verify the actual package version, entry point and license before
borrowing code; a README's engine name can disagree with its dependency file.
Public source or a free-to-play game does not by itself grant asset reuse rights.

- DOM/SVG/Canvas suits interfaces, cards, diagrams and modest simulations.
- Phaser suits new sprite-heavy 2D games needing scenes, input, cameras and effects.
- PixiJS suits custom 2D renderers when simulation and application structure already exist.
- Consider a 3D engine only when depth or spatial navigation serves the main action.

The original [Pattern Lab](assets/pattern-lab/index.html) supplies small examples
of ten mechanics, graphics and feedback. Use one as a disposable vertical slice or
implementation reference. `scripts/create_game.py` can copy a selected example
into a **new** output directory. It is a prototype seed, not a finished production
game or a substitute for the researched commercial games.
See [running and adapting the lab](references/running-the-lab.md) for commands,
controls, scaffold behavior and validation.

## Implement a whole short loop

Keep commands, simulation state, observations and rendering distinct. An existing
authoritative simulation continues to own outcomes. For a new local game, isolate
rules from DOM/canvas code so the same commands can be tested and replayed. Choose
fixed simulation steps, turn boundaries or bounded analytic idle advancement to
fit the rules; do not let animation timing silently determine resource accounting.

Build one complete cycle before expanding content: perceive a need, choose an
action, commit its cost, observe its result, encounter the next changed decision.
Make unavailable actions explain their requirement. Distinguish a rejected action,
a physical loss and a transport failure. Add a restart or undo path appropriate
to the stakes. Test degenerate strategies: always choosing one button, waiting
without deciding, repeating a free reward, and spending below zero.

## Make the world explain the state

Read [motion and graphics recipes](references/motion-and-art.md) for the relevant
pattern. Define a coherent projection, silhouette language, palette, material and
interface hierarchy. Produce a recognizable scene and actors; plain colored
rectangles are useful greyboxes, not evidence of finished art. Use original,
project-owned or appropriately licensed assets and retain their provenance.

Tie feedback to events: show intent or a valid placement before commitment, play
the action, reveal the changed world/resource, then settle. Prioritize feedback
that explains causality over decorative particles. Suggested durations in the
recipes are tuning starting points, not timings measured from the reference games.
Let reduced motion preserve the same information through static emphasis. Gate
audio behind a gesture, include mute and use sound to reinforce visible feedback.

For generated raster assets use an available image-generation workflow when it
improves the result. For editable pixel art use an appropriate sprite workflow;
for procedural diagrams or simple vector objects keep them in code. An unavailable
asset service does not block a useful prototype using original local art.

## Verify the game, then evaluate the design

Use [the evaluation guide](references/evaluation.md). Run appropriate state tests
and the production build. In the browser, perform the primary action through the
real interface and inspect the changed state and visuals. Verify a rejected action,
completion or sandbox creation, restart, keyboard/touch alternatives where relevant,
narrow layout, reduced motion, lifecycle and saved progress when supported. A
successful build or nonblank canvas does not establish gameplay or visual quality.

Record the build/revision, actions, expected and actual observations, screenshots,
console/asset failures and untested devices. Re-test after fixes that affect those
results. If browser automation is restricted to a particular tool, honor that
restriction rather than substituting a different driver from a reference toolkit.

Human play is needed to validate fun. Use the brief's observation questions: can a
new player explain the choice, see its consequence, form a new plan and want another
attempt? Change one design variable after observed friction and repeat. Label an
internal prototype, an externally playtested slice and a release candidate honestly.
Do not invent player studies, retention measurements or visual inspection.

Finish with the playable artifact, controls, what is implemented, verification
evidence and material remaining limitations. Publish only within the user's
authorized destination and scope; this skill adds no publication permission.
