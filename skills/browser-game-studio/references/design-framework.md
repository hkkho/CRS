# From an idea to an engaging browser game

This framework is a synthesis of the case studies, not a formula proving that any
idea will be fun. It works by producing a small, falsifiable design: a player can
make a choice, see a consequence and decide what to do next. Its core vocabulary
also applies to action and puzzle games, but those genres need their own reference
study and control/level-design work.

## 1. Translate the fantasy into an action

Write: **I am [role], I repeatedly [verb], because I want [visible change], while
[constraint] prevents every option from being equally good.**

“A space station game” supplies a setting. “A station quartermaster routes scarce
repair kits between a garden, observatory and docking bay before the next supply
ship” supplies a decision. If the fantasy is creative, replace scarcity with
composition: “I arrange a little floating settlement, then watch its residents
make use of what I built.” Gourdlets and Evolution show why every game does not
need debt, combat or a score.

Give the player an immediately understandable object: a parcel, creature, card,
plant or building. Let its appearance expose relevant state. An address label
supports a shipping decision; a drying pot supports a care decision. Abstract
statistics become easier to understand when attached to such objects.

## 2. Select a primary pattern

| Pattern | Interesting decision | Visible consequence | Candidate theme transformations |
| --- | --- | --- | --- |
| Requests and tradeoffs | Help now, conserve resources, or accept a later obligation? | A visitor reacts; a settlement changes | Museum curator, station mayor, village council |
| Tactile work | Which tool, order, destination and arrangement are correct? | An object changes hands, receives a mark and leaves | Repair bench, parcel desk, tea preparation |
| Network management | Where should limited infrastructure go? | Connections deliver a service to named consumers | Irrigation, library deliveries, signal relays |
| Incremental harvesting | When should manual effort fund automation? | More collectors and a visibly stronger tool | Salvage crew, fishing boat, orchard |
| Care and collection | Maintain production or invest in a new living variant? | Fed animals, new eggs, different silhouettes | Bird refuge, alien hatchery, insect garden |
| Production chain | Which bottleneck is worth removing next? | Goods move, queues shrink, a machine activates | Bakery, ceramics studio, recycling line |
| Calm growth | Which object should I nurture, keep, sell or arrange? | New leaves and an increasingly personal space | Houseplants, coral nursery, terrariums |
| Experiment and observe | Which configuration is worth trying next? | A changed trial and a comparison to the previous best | Creature gait, paper glider, mechanical toy |
| Resource conversion | Which spell/tool conversion and capacity upgrade opens a route? | A resource graph becomes usable | Alchemy, cooking research, restoration workshop |
| Creative construction | What composition do I want and what will inhabitants do there? | Persistent buildings and believable visits | Tiny town, orbital garden, floating market |

Initially combine one primary pattern with one support at most. A repair bench
can gain upgrades after a short shift; it does not immediately need an open world,
breeding system, deckbuilder and online economy. Add a second system when it
changes an observed decision, not just because a reference game includes it.

## 3. Construct three connected loops

| Scale | Player experience | Required evidence |
| --- | --- | --- |
| Action, often seconds | Understand an affordance, act, see a clear result | A real action and immediate feedback |
| Session, often minutes | Adapt a plan, face a complication, reach a stopping point | At least one changed choice and a useful report or creation |
| Return, when appropriate | Try a different approach, improve, discover or personalize | A reason other than waiting for a timer |

These are design scales, not fixed duration requirements. A sandbox can offer
ongoing creation rather than a session-ending victory. An incremental can be
pleasantly short instead of requiring daily retention. The MachinEGG's stated
retirement objective and Gourdlets' stated absence of objectives are both valid
directions; copying both indiscriminately would create a confused product.

For each loop, identify what the player **knows**, **can choose**, **spends**,
**changes**, **learns** and **wants next**. If the last three are blank, the loop
is probably a repeated input with a counter rather than a developed design.

## 4. Test whether the choice has substance

Use a small example before coding a large economy. A quartermaster has 8 supplies.
Repairing the observatory costs 5 and earns research next shift. Repairing the
garden costs 4 and restores morale now. Shipping a rush order earns 3 supplies
but occupies the dock for one shift. The player cannot buy both repairs now.
The first question is whether the player can explain the opportunity cost.

Make hypotheses explicit:

- **Agency:** at least two reasonable options exist for a known state.
- **Legibility:** consequences can be predicted at the fidelity the game promises.
- **Variation:** a later state changes which option is attractive.
- **Recovery:** a mistake teaches something and has a proportionate retry path.
- **Expression:** in a sandbox, different choices make recognizably different worlds.

Reject dominant free loops unless they are an intentional short celebration.
Do not hide every consequence to manufacture difficulty. Separate discovery from
unclear controls. A clear preview can coexist with uncertainty about future events.

## 5. Design the first minute as a sequence

One example for a parcel game:

1. Show one waiting parcel, two large destination trays and a readable address.
2. Let the player pick up the parcel or select it by keyboard.
3. On approaching a tray, preview its validity with text and shape as well as color.
4. Confirm delivery with a short snap, a stamped receipt and an updated ledger.
5. Introduce a second parcel with a new postage requirement, rather than ten rules.
6. After several deliveries, offer a tool that removes a task the player understands.

An upgrade is strongest when the player has felt the bottleneck it resolves.
Peggy's Post explicitly uses seven-day mode to introduce mechanics progressively.
Its lack of an explicit tutorial is a design choice to study, not a requirement
for future games. Our version can keep the tactile desk and improve onboarding.

## 6. Make art and feedback serve that sequence

Specify a small visual grammar before making assets:

- Projection and camera: side view, top-down, isometric or deliberate 3D.
- Actor silhouette: recognizable at the actual play size.
- World palette: environment colors, interaction accent, danger and disabled states.
- Material: paper and ink, clean machinery, soft plants, low-resolution pixels.
- Lighting and depth: ground contact, a consistent light direction and clear layers.
- Text: readable essential numbers, concise labels, stable placement.

For each important action, name the visual state before, during and after it.
An animation should have a purpose: targeting, effort, transport, transformation,
reward or recovery. A floating number alone cannot explain where a resource went.
The [motion recipes](motion-and-art.md) give reusable implementations.

## 7. Choose architecture from the experience

Use a pure state transition for a turn, a fixed-step simulation for interacting
agents or physical rules, or bounded elapsed-time calculations for simple idle
growth. Rendering interpolates accepted observations. Input creates commands.
Audio and effects subscribe to outcomes rather than independently awarding them.

Persist the data needed for the actual experience. A replay save needs compatible
rules and deterministic inputs; a creative world needs durable edits; an idle game
needs a declared elapsed-time policy. Do not promise replay determinism across
an uncontrolled physics engine. Evolution's developer specifically documents
cross-device nondeterminism in its Unity physics.

For an existing shared simulation such as CRS, keep authoritative state in its
existing shared layer. Adopt readable controls, previews and report choreography
without creating a second simulation in the browser. Do not expand a refuelling
game into accidents or plant operations merely because another simulator has them.

## 8. Build an experiment, then make a game

Deliver one small vertical slice with original art, a complete short loop, a useful
restart and evidence from real input. Then observe players. Ask them to narrate
what they believe will happen; compare that with the result. Watch where they
hesitate, repeat a mistake, discover a plan or voluntarily replay.

Treat time-to-first-intentional-action, correct prediction of a consequence,
completion of the first loop and voluntary replay as **questions to measure**.
The example thresholds in a project brief are targets, not research findings.
No automated score can establish that the game is enjoyable. External playtests,
performance/device checks and sustained content development distinguish a tested
production game from this initial research-backed prototype library.

## Brief to produce for a new idea

Keep this short enough to guide implementation:

| Field | What to commit to |
| --- | --- |
| Fantasy and audience | Role, intended feeling, session context and device |
| Core action | One concrete verb and its input |
| Decision | State, two alternatives, costs and consequences |
| Constraint | Scarcity, uncertainty, spatial composition or skill |
| Loop | First action → changed state → next meaningful choice |
| Progression | A tool, discovery, relationship or expression that changes play |
| Ending/return | Success, failure, short session result or sandbox satisfaction |
| Visual contract | Camera, silhouette, palette, materials and important object states |
| Feedback contract | What confirms success, rejection, pending work and recovery |
| Technical contract | State owner, timing model, renderer, persistence and deployment |
| Validation | Real inputs, observable outcomes and the next human playtest question |
| Scope | The first slice and explicitly deferred systems |
