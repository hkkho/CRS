# Evaluate the delivered game and the skill that produced it

Use two separate questions: **does it work?** and **is it worth playing?** The first
has many objective checks. The second needs observation and judgment, particularly
from people who did not build it. None of the checklists below is a validated
retention predictor.

## Evidence for a playable slice

Record the source revision or file digest, start command, URL, browser/device,
viewport, tested inputs and actual outcomes. Keep expected outcomes explicit.
Retain a scene screenshot and a result/rejection screenshot when possible. Do not
call a rendered title screen a completed playtest.

| Check | Observe through the actual interface |
| --- | --- |
| Entry and loading | A readable first state, successful assets and usable input |
| Main action | A real input changes the intended authoritative state |
| Consequence | The player can identify what changed and why |
| Competing choice | A different action has a distinct meaningful consequence |
| Rejection | Missing resources or an invalid target do not partially commit work |
| Progression | A new tool, configuration or composition changes play |
| Completion or sandbox creation | A complete short loop reaches a useful result |
| Recovery | Restart, retry or undo produces the promised state |
| Lifecycle | Pause, hidden tabs and reopening follow the stated time policy |
| Persistence, if present | Save/restore preserves intended data; a bad candidate cannot replace the live state |
| Input alternatives | Relevant keyboard and touch paths work, not just desktop hover |
| Accessibility | Focus is visible and stable; critical distinctions use more than color; reduced motion retains information |
| Packaging | The production/static output runs at its actual base path and iframe conditions |

Use pure state tests for accounting, ordering, limits, deterministic replay and
meaningful failure semantics. Use browser interaction to verify controls, view
updates and navigation. Inspect the image as well as DOM assertions. A button can
update a counter while the scene remains empty, and a beautiful scene can hide a
button that never changes state.

The prototype tests live in
[model.test.mjs](../assets/pattern-lab/model.test.mjs). They cover a viable council
policy and a losing one; postage/route validation; connectivity versus capacity;
harvest recovery and automation; feed consumption; bounded production; dry growth;
repeatable trials with elite retention; conversion capacity; sandbox undo; and
invalid saves/time. These tests establish selected rule invariants. They do not
establish enjoyment, actual phone performance or that all browser controls work.

## A first human playtest

Give a new player the delivered build and a short objective, not a walkthrough.
Ask them to say what they expect before their first important choice. Observe
whether they locate input, predict a consequence, understand the result and form
a second plan. When they hesitate, distinguish an unclear affordance from an
interesting decision. Ask what they would do differently on another attempt.

For a sandbox, ask them to create a particular kind of place or creature, then
let them change their mind. Look for expression and curiosity rather than forcing
a win condition into a design that does not need one. Voluntarily inspecting the
result can be a meaningful sign, but one short session does not establish retention.

Choose project-specific targets before testing. Example targets could be a first
intentional action within a minute, a correct explanation of one consequence and
completion of the short loop without developer intervention. Those are proposed
acceptance targets, not results of this research. Record failures as useful data.

## A design review rubric

Use 0–3 as an editorial aid: 0 absent, 1 confusing or fragile, 2 clear and working,
3 notably effective in observed play. Keep the evidence beside the judgment and
do not combine the values into a scientific “fun score.”

- **Decision quality:** alternatives, opportunity cost and changing priorities.
- **Feedback:** legible intent, consequence, rejection and recovery.
- **Progression:** a change in actions or understanding rather than only larger numbers.
- **World coherence:** readable objects, consistent scale/material and useful motion.
- **Onboarding:** the first loop teaches the action through doing.
- **Comfort:** readable interface, bounded effects, controllable sound and suitable input.
- **Technical reliability:** state integrity, runtime stability and packaging evidence.
- **Return reason:** another plan, creative expression, discovery or a satisfying short ending.

Make the next revision address an observed issue. Examples: keep the puzzle but
make legal destinations visible; keep the tool but shorten its decorative lockout;
keep the economy but change a dominant strategy. Avoid changing art, costs,
onboarding and difficulty simultaneously, since the next test would not explain
which change helped.

## Forward-test the skill

Choose several different briefs: a tactile repair desk, a calm garden and a spatial
network game, for example. Use the same resource budget and evaluation protocol.
The consumer should receive the skill and the brief, not the author's intended
solution. Where an authorized independent evaluator is available, let it review
the delivered game from its interface before reading implementation notes.

Record which references were needed, whether setup worked, whether the agent kept
the user's architecture, whether the loop had substance and how much repair was
needed. Change the skill only when an observed failure suggests a reusable rule
or a missing resource. Avoid turning one prototype's exact dimensions, costs or
mistake into mandatory rules for every genre.

This package's first validation includes its original lab, model tests, scaffold
checks and browser inspection. A generated seed that runs demonstrates packaging,
not independent creative generalization. External human playtests and independent
consumer tasks are still needed before claiming a mature or reliably fun
game-making skill.

## A discovered portability failure

In this Windows environment, the standard Python static server returned
`text/plain` for `.mjs` modules because of MIME associations. The page rendered
its HTML but the application did not start. Syntax checks alone passed.
[serve.py](../scripts/serve.py) explicitly serves JavaScript with a valid module
MIME type and avoids stale development cache responses. Its headers and actual
browser startup were checked after the fix. This is an example of a demonstrated
runtime repair worth retaining in a starter, not a reason to force Python hosting
on every production game.
