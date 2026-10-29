# Graphics and animation that explain play

The reference games use different engines and production methods. Reuse a
relationship between action and feedback; create an art system that belongs to
the new game. The descriptions below are original implementation recipes. Their
durations are proposed tuning ranges, not timings measured in the studied games.

## A useful feedback sequence

An action usually needs **intent → commitment → consequence → acknowledgement →
settle**. Not every event needs five separate animations. A pipe preview, a pipe
appearing and a newly supplied house changing state may communicate the sequence
more clearly than a large celebration.

| Stage | Information to communicate | Starting duration for a modest 2D interface |
| --- | --- | --- |
| Intent | What is targeted, what will be spent, whether it is valid | Immediate highlight; about 80–120 ms emphasis if motion helps |
| Commitment | The input was accepted and its work is underway | About 90–180 ms press, lift or cast anticipation |
| Consequence | What moved, transformed, connected or grew | About 150–350 ms for a small local action; actual simulation decides completion |
| Acknowledgement | Success, rejection, new resource or next goal | About 250–600 ms emphasis, with persistent text for the important result |
| Settle | The next action is available and the object has a stable state | About 120–250 ms local recovery; avoid blocking input for decorative motion |

When a real calculation takes longer, show its genuine pending status. Use an
indeterminate animation when no reliable fraction is available. Do not pretend
that a progress bar measures a solve merely because the renderer can animate it.
Numerical failure, physical loss and unknown transport outcome need different
messages. Applying that distinction matters particularly for CRS's daily worker.

## Ten reusable recipes

| Pattern | Mechanics and visual implementation | Motion to prototype | Reduced-motion equivalent |
| --- | --- | --- | --- |
| Visitor decision | Keep a readable cast silhouette, a short request and a persistent resource ledger in one composition | Visitor arrival; a local reaction; cost/reward emphasis; next visitor | Instant arrival, static reaction mark and a textual delta |
| Tactile desk | Layer paper, tools and trays with a consistent light direction; lift the held object's shadow; show a legal drop target | Pointer-following object; gentle tilt; short route snap; receipt/stamp | Direct tracking without lag/tilt; highlighted destination and static receipt |
| Infrastructure | Draw the accepted connectivity graph, distinguish missing route from insufficient capacity, label consumers | Placement confirmation; a pulse along accepted edges; consumer acknowledgement | Solid highlighted connection and an explicit service/capacity label |
| Harvesting | Put a changing tool in the world and keep total progress distinct from spendable stock | Net arc; sparse catches; a resource pulse; an upgraded tool silhouette | Static active-net state, count delta and progress bar |
| Flock care | Give actors distinct silhouettes/colors and attach needs to the relevant actor or shared feed source | Walk/peck cycle; grain scatter; nest change; collection | Still actors, readable feed state and a clear nest/egg change |
| Factory | Use consistent travel direction, real queue/buffer observations and identifiable transformation stages | Press stroke; material transfer; conveyor motion only while processing | Static stage/queue changes and a persistent running/blocked label |
| Plant care | Distinguish seedling, growing, mature and dry states with stem/leaf geometry and soil state | Water droplets; slow leaf emergence; a mature marker | Immediate new growth geometry and soil/maturity labels |
| Experiments | Preserve a shared distance or performance scale and show the previous best | Articulated trial; markers; best highlight; next-generation transition | A static comparable result table plus selected-creature poses |
| Conversions | Draw resource roles and connections; make storage caps and required inputs visible | Cast into a transfer path; receiving-node pulse; newly available action | Static highlighted conversion and before/after resource values |
| Creative building | Use coherent tile scale, ground contact and a small actor vocabulary | Placement ghost; settling object; credible paths and pauses at destinations | Static placed world and actor positions; edits remain visible |

The [Pattern Lab renderer](../assets/pattern-lab/graphics.mjs) implements original
variations of those motifs. Some are intentionally small: the factory uses a
bounded buffer rather than a large production network; creature trials optimize
a transparent toy fitness function rather than simulating muscles and a neural
controller. Read each demo's design note before treating it as an implementation
of the original game's full system.

## Separate state from appearance

The [model](../assets/pattern-lab/model.mjs) accepts commands, validates costs and
returns replacement state. The [application](../assets/pattern-lab/app.mjs) then
creates visual/audio feedback. The renderer receives observations. A moving
object is evidence of an already accepted outcome or an explicit preview, not a
second resource-accounting system.

For example, a purchase follows this simplified form:

```javascript
if (coins < cost) return rejected;
coins -= cost;
machines += 1;
return accepted;
```

The first line prevents an unaffordable purchase. The next two change related
values together: paying the cost and receiving the machine are one accepted
transition. The last line reports that outcome. A full implementation also
validates inputs and creates its observation; the example expresses the rule's
shape rather than claiming to be a complete function. Animation runs afterward
and cannot add a machine a second time when its tween completes.

For a visual transition, normalized progress is often sufficient:

```javascript
u = clamp(elapsed / duration, 0, 1);
eased = 1 - (1 - u) ** 3;
visibleX = startX + (endX - startX) * eased;
```

These are explanatory assignments for an ease-out movement. Dividing elapsed time
by duration produces dimensionless progress; clamping keeps it between zero and
one. The cubic expression moves quickly at first and slows near the destination.
The final line interpolates between two display coordinates. A zero or invalid
duration needs an immediate-final-state branch in real code. `visibleX` belongs
to the view, not the simulation position used to decide a delivery or collision.

Animation has its own lifecycle. Cap temporary particles; remove finished effects;
stop listeners and loops when a view is disposed. Freeze decoration and discard
unexpected wall-time gaps when a hidden tab returns unless the design explicitly
supports offline progress. Plant daddy's creator reports real-time growth across
visits; the lab deliberately pauses hidden time and explains that different policy.

## Establish a coherent art direction

Specify the projection, scale and material before collecting assets. A side-view
workshop, top-down district and isometric town can each be coherent; mixing their
sprites without a scale plan usually cannot. Choose one primary environment
palette, one interaction accent, distinct warning states and a consistent light
direction. Small contact shadows do useful work: feet and objects appear to rest
on a surface instead of floating over it.

For a character, test its silhouette at its actual play size before adding tiny
details. For a production object, test whether idle, processing, blocked and ready
can be recognized without opening a tooltip. For essential numbers and addresses,
use readable text outside a low-resolution canvas when needed. A deliberately
pixelated world does not require pixelating the only readable postage value.

The lab uses newly authored Canvas vector scenes, not extracted sprite sheets.
Its shared drawing helpers establish ground contact and consistent shapes, while
each scene changes palette, camera and material. A later production game can
replace these with authored pixel sprites, licensed packs or generated art after
establishing dimensions, pivots, animation states and provenance.

## Make progression visible

An upgrade should alter at least one experienced property: effort, available
choice, capacity, reach, route, transformation or appearance. Prefer a new visible
net to an invisible multiplier; prefer a new usable plot to a decorative badge
when the game's promise is construction. Cosmetic expression is itself valid
when the player wants to make a personal space, but label it honestly.

Keep the feedback hierarchy proportional. Routine work can use a small movement
and a persistent number. A newly usable system can add a larger reveal. Reserve
the largest acknowledgement for a meaningful session result. Repeated large
effects can obscure the model and make the game feel less responsive.

## Sound is reinforcement, not the only explanation

Use a short pickup/press/cast cue, a distinct rejection cue and an occasional
milestone cue. Unlock Web Audio only after a user gesture and provide mute.
Stacked production loops need a voice/rate limit so growing output does not
produce an exhausting wall of identical sounds. Verify audible quality with a
human listening test; a running audio context alone does not establish it.

Every important event also needs visible information. Reduced motion should keep
the accepted state change, a stable emphasis and concise feedback. In a fast action
game, motion may carry essential gameplay information; research a suitable
accessibility mode rather than simply freezing the entire game.
