# Twelve browser-game design studies

Research date: **24 October 2026**. Ten primary simulation games and two adjacent
references were selected for established reception and different design problems.
This is a set of useful exemplars, not a definitive ranking. Read
[the evidence method](research-method.md) before treating an observation as a result.
The proposed explanations of engagement are hypotheses; no retention study was run.

The original [Pattern Lab](../assets/pattern-lab/index.html) implements ten small
mechanical and visual studies. It does not bundle the reference games' code or
assets. Suggested timings and tuning belong to our recipes, not measurements of
the originals. The research browser sometimes ran canvas games at a low frame
rate, so no original game's performance or precise animation duration is inferred
from this environment.

## 1. Sort the Court! — give a system a face

**Primary source:** [Graeme Borland's game page](https://graebor.itch.io/sort-the-court).
The creator presents a kingdom-management game built around yes/no decrees. The
page identifies Unity and credits art and music separately. The itch browser
edition is distinct from the updated mobile/localized version linked on Poki.

**Inspected:** launched the official embedded build, followed the advisor's
introduction, answered yes and saw a large positive acknowledgement. Accepted
Skelly's fee request: a negative gold callout appeared and the treasury visibly
counted downward. The room, village, visitor and three icon-led resources remain
visible together. Broad outlines, warm paper, a small cast and drifting clouds
make a modest static composition feel inhabited. Later event chains and endings
were not verified in this short session.

**Design interpretation:** the input is easy; evaluating its consequences supplies
the challenge. Repeated visitors can attach memory and expectation to otherwise
abstract resource changes. A reaction bridges the choice and the number, making
the accounting feel like something happening to someone. The background settlement
can carry a longer-term aspiration without an additional instruction panel.

**Original reproduction:** [Cloud Council](../assets/pattern-lab/index.html?pattern=council)
uses new characters, a floating town and eight authored requests. Its treasury,
trust and population are real model values. Accepting spends the displayed cost;
insufficient funds reject the promise. A viable mixed policy reaches eighteen
residents; declining everything fails that objective. Visitors, a speech card,
small residents and event feedback reproduce the general relationship between
choice, personality and visible consequence, using newly drawn art.

**Next experiment:** hide the numeric preview for half of an explicitly consented
playtest, then ask players what they expected and whether surprises felt fair.
Do not assume a more opaque interface is automatically deeper. For another theme,
replace decrees with museum loans or station maintenance requests, while retaining
the need to trade present resources against a visible community outcome.

## 2. Peggy's Post — make the work physically legible

**Primary source:** [digitarium's game page](https://digitarium.itch.io/peggys-post).
The creator describes sorting, postage, vehicle loading, upgrades and a seven-day
loan objective. Seven-day mode introduces mechanics over time; endless mode starts
with more systems available. The controls include holding, rotating/using objects,
book navigation and a shipping inventory.

**Inspected:** started seven-day mode through a paper log-card save selector and a
loan postcard. Entered the desk, requested a customer and moved the first irregular
parcel. The desk includes a rulebook, stamp pack, scale and bell. Pixel textures,
overlapping paper, muted sea colors and printed labels make its interface part of
the fiction. This session did not complete a shipment, a day or the loan campaign.

**Design interpretation:** an operation gains texture when the player can distinguish
its stages: read, inspect, select a tool, mark, arrange and dispatch. Physical
objects preserve context between those stages. The same tactile density can also
cause friction: an essential number must remain readable when a parcel covers a
tool, and references should not require needless repeated page changes. These are
questions to test, not defects established by this short play session.

**Original reproduction:** [Moonpost Desk](../assets/pattern-lab/index.html?pattern=post)
uses a new two-route postal fiction, exact one-to-three-unit stamps and six parcels.
A parcel can be dragged to a tray; DOM route buttons provide an alternative to
dragging. A rejected route or postage mismatch spends nothing. The model awards
credit only when dispatch is accepted. Wood, kraft paper, ink and a moving shadow
carry the material feel without copying the original desk arrangement or artwork.

**Next experiment:** compare drag-only handling with a select-then-route option on
a phone. Observe wrong drops, successful corrections and whether dispatch still
feels satisfying. A repair shop can reuse the staged work pattern with diagnostic
tools and a finished-item tray; changing only the parcel's name is insufficient
if diagnosis never changes which tool should be used.

## 3. Waterworks! — teach infrastructure through visible dependencies

**Primary source:** [scriptwelder's game page](https://scriptwelder.itch.io/waterworks).
It describes a historical card strategy game about Grudziądz water supply, including
deck management, positioning, technology, pipes and influence. The page identifies
Phaser, an interactive tutorial, four difficulties and achievements.

**Inspected:** selected Easy, read the historical introduction and dragged the first
water-source card onto the town. A construction panel exposed a foreman, two
builders and a permit as separate prerequisites. Assigning cards spent their shown
coins and filled slots with checkmarks and small bursts. Once requirements were
filled, the confirmation gained a conspicuous glow. Confirming completed the
water source, charged its final eight coins and displayed the completion/card
reward explanation. The map uses detailed pixel
buildings, a river, wooded plots and a persistent resource strip. Later city ages,
the full network and the ending were not verified.

**Design interpretation:** external history can become a sequence of decisions
rather than an encyclopedia placed before play. A construction requirement is
easier to understand when the missing pieces have visible slots. Reserving a
strong glow for a newly valid confirmation gives the effect informational value.
The spatial map and card system operate at different levels, so previews and
clear responsibility for each stage are important.

**Original reproduction:** [Water District](../assets/pattern-lab/index.html?pattern=water)
isolates the network consequence rather than copying the deck or historical town.
An orthogonal graph determines connectivity. Pump capacity separately determines
how many connected homes are supplied. Blue pipes, amber capacity warnings and
house acknowledgements distinguish those conditions. End-day reports charge lost
influence for unserved homes; a valid complete network can meet the short objective.

**Next experiment:** ask players to explain a connected-but-unserved house before
pressing End day. If they blame a broken pipe, improve capacity feedback before
adding more technologies. The same pattern can model irrigation or delivery routes,
but a domain-specific flow model must replace this toy connectivity rule where
actual pressure, capacity or timing belongs to an existing simulation.

## 4. 1,000,000 shrimp — remove input fatigue without removing activity

**Primary source:** [Jon Topielski and mafgar's game page](https://jontopielski.itch.io/1000000-shrimp).
The creator explicitly instructs mouse hovering and identifies LÖVE. It presents
a short incremental boating game with a million-shrimp goal; credits distinguish
music, sound and additional art. The page recommends its downloadable build for
best performance, which is not a performance result from this study.

**Inspected:** entered the browser build and its mostly two-tone boat scene.
Holding the pointer on Hand Catch produced repeated catches, incremented the
resource counter, filled a fractional progress indicator and revealed a boat
upgrade. The sea, boat, sky and sparse log make activity visible without a dense
dashboard. Only early collection was tested; later equipment and the million-goal
ending were not reached.

**Design interpretation:** active attention need not mean repeated clicking.
A sustained pointer position can maintain involvement while reducing input effort.
Revealing a purchase after early work makes its purpose concrete. A restrained
palette leaves production effects and the changing equipment more visual room.
The interesting design question is when the player's attention changes from
performing work to choosing how that work is performed.

**Original reproduction:** [Night Harvest](../assets/pattern-lab/index.html?pattern=harvest)
collects fictional sea seeds with a button or sustained hover over an original
vessel. A short recovery prevents a burst of repeated commands from minting
unbounded cargo. Automatic nets then produce resources through simulation time;
vessel upgrades affect both rate and visible equipment. Total harvested and
spendable cargo are separate so buying a tool does not move the goal backward.

**Next experiment:** compare hover, press-and-hold and an explicit timed action for
motor comfort and understandable progress. Keep a keyboard/touch alternative.
If automation leaves only waiting, add a meaningful routing or investment choice
instead of increasing the number of clicks. This pattern can support salvage or
harvesting; it does not justify manufacturing an endless retention treadmill.

## 5. Peckin' Pixels — make production feel like care

**Primary source:** [Waving Walrus Games' page](https://wavingwalrus.itch.io/peckin-pixels).
The creator describes egg sales, special foods, rare chickens, outfits and objectives.
It explicitly states that the browser game uses JavaScript and HTML Canvas without
a game engine; desktop versions use Electron. This matters more than its storefront
“Made with” artwork-tool tag when studying the runtime.

**Inspected:** chose a fresh save, met the tutorial mouse and woke the first chicken.
The hen displayed small need/progress bars, moved in the yard and was introduced
to the accessory-hut interaction. The scene keeps nests, delivery/sell bays, a
scale, ordering and a noticeboard in recognizable places. Bright pixel grass,
plump silhouettes and the character-led instruction establish a friendly tone.
Egg sales, breeding/rarity and completion were not tested in this session.

**Design interpretation:** identity and readable need can change the interpretation
of an economy. Feeding a particular creature feels different from refilling an
anonymous production gauge. Variants and outfits can reward recognition and
attachment alongside economic efficiency. The risk is hiding an important need
in a tiny bar, especially on phones, or making affection depend on tedious upkeep.

**Original reproduction:** [Meadow Flock](../assets/pattern-lab/index.html?pattern=flock)
has original warm-colored hens with walking and pecking cycles, a feed pile and
limited nest space. Feed is consumed by model time; eggs stop when it runs out.
Collecting sells whole eggs, and additional hens raise both output and appetite.
The short objective joins four hens with ten eggs sold. No rare-breed catalog,
cosmetic shop or original character art is reproduced.

**Next experiment:** ask players to predict which resource the next hen will make
scarce. If they understand the tradeoff but enjoy checking the flock, the care
presentation is doing useful work. If they repeatedly miss the need, improve
feedback rather than adding more urgent warnings. The same principles apply to
an alien hatchery or insect garden with appropriate original creature behavior.

## 6. The MachinEGG — show the bottleneck an upgrade removes

**Primary source:** [JC–QuantumGames' page](https://quantumgames-studio.itch.io/the-machinegg).
The creator describes buying and feeding chickens, egg collection, market sales,
automation and retirement, with normal, speedrun, challenge and endless options.
The page identifies PixiJS. A creator [development log](https://quantumgames-studio.itch.io/the-machinegg/devlog)
describes replacing and improving the art and animations; that does not imply the
source or artwork is available for reuse.

**Inspected:** launched the browser build, saw the animated flock/title composition,
opened the mode selection and started Normal (Relax). The first farm view places
animals and feeding areas above a visible underground conveyor/machinery space,
with a purple shop beside it. Buying the free first chicken added one hen, a
named greeting from Lavender and a newly revealed petting action; the next hen
cost two currency.
Only the opening purchase sequence was inspected; this study did not reach its
retirement or independently measure the later production chain's performance.

**Design interpretation:** putting production movement in the world can explain
automation more clearly than adding a percentage to an upgrade description.
The manual stage supplies an experiential baseline. A machine is then a solution
to known work, and a new bottleneck can replace that work with another decision.
Mode selection also lets players choose whether optimization pressure is wanted.

**Original reproduction:** [Copper Works](../assets/pattern-lab/index.html?pattern=factory)
changes the theme to an original copper workshop. Ore becomes parts; output has
an eight-part capacity; sales fund supplies and automatic presses. Missing ore or
full output stops production. A conveyor, press mechanism, gauge and stacked parts
explain the state. It implements a small complete chain, not the original game's
farm, modes, art, pricing or full upgrade structure.

**Next experiment:** after an automation purchase, ask the player what they no
longer need to do and what now limits output. If neither answer changes, the
upgrade is probably an inflated number rather than useful progression. Compare
a calm mode with an optional efficiency challenge only after the basic chain
remains legible at its busiest intended state.

## 7. plant daddy — make time change something worth looking at

**Primary source:** [overfull's page](https://overfull.itch.io/plantdaddy).
The creator describes a relaxed houseplant game with real-time growth, room
decoration and progress while the tab is closed. Its page identifies Unity.
These are the creator's broader feature claims; a brief inspection cannot
establish its offline accounting or long-term balance.

**Inspected:** started browser version 1.2.2 in a sparsely furnished apartment.
The tutorial directs attention to the shop, where a Garden Plant costs the four
starting leaves. Selecting its card first exposes a description; selecting it
again opens a purchase confirmation. After purchase, highlighted windowsill
spots show legal placement. Placed the seedling, then opened its detail view:
light level, growth, pot color, pot size, rotation and naming/moving controls.
The warm wood, angled sunlight and faceted plant make the room itself a reward.
No closed-tab growth or mature-plant sale was tested.

**Design interpretation:** this combines two different reasons to return: a living
thing has changed, and a composition can be improved. Slow growth works when an
intermediate state is attractive and understandable. A bare countdown supplies
less to observe. Showing a legal sunlit position before placement also teaches
the care rule without making the player decipher a formula first. The tradeoff
is that long waits can conceal whether the player's action actually helped.

**Original reproduction:** [Window Garden](../assets/pattern-lab/index.html?pattern=garden)
draws a new window, shelf, pots and growth-dependent leaves. Water supports bounded
growth; a mature cutting earns enough to buy another pot, while the parent grows
again. The implementation intentionally advances only during an active session:
it is **not** a reproduction of the reference's offline simulation. No room
catalog, botanical species, sharing service or original meshes are included.

**Next experiment:** ask a new player to identify the growing plant and explain
why it stopped. Then compare a numerical growth label with a more legible
intermediate silhouette. Judge calmness through observation and interviews,
not by assuming that the absence of a timer guarantees a relaxing game.

## 8. Evolution — turn improvement into an observable experiment

**Primary sources:** [Keiwan's page](https://keiwan.itch.io/evolution),
[source repository](https://github.com/keiwando/evolution), and the creator's
[3.0 development explanation](https://keiwan.itch.io/evolution/devlog/98245/evolution-30-this-is-a-big-update).
The game uses joints, bones and muscles, with neural networks and genetic
algorithms training creatures on tasks. It is a sandbox rather than a campaign
with prizes. The developer discusses physics differences between hardware;
reproducibility should not be assumed from a deterministic-looking animation.

**Inspected:** followed the browser editor's illustrated onboarding: joints,
bones, muscles, optional wing surfaces, then the Evolve action and help control.
The final editor exposes population, seconds per generation, task selection and
structural tools. Red joints, thick dark bones and colored muscles communicate
different roles with very little scenery. The inspection established this
teaching sequence and interface, **not** a successful trained creature or a
measured learning curve. An attempted editor interaction was not confirmed as
placing a joint, so it is not counted as a completed experiment.

**Design interpretation:** the reward is the player's changing hypothesis.
"What if I change this structure?" creates purpose for repeated trials. A useful
experimental game separates editable causes, observed movement and a comparable
result. Without that separation, success can look like unexplained randomness.
Retaining a baseline and showing multiple attempts together helps players
distinguish improvement from a lucky-looking run.

**Original reproduction:** [Creature Trials](../assets/pattern-lab/index.html?pattern=evolution)
uses original jointed walkers in six lanes. A six-second trial compares stride
and rhythm parameters, retains the elite and mutates five candidates using a
seeded generator. The fitness formula is deliberately transparent. It is an
**analytic teaching toy**, not a neural network, articulated physics solver,
creature editor or reproduction of Evolution's training algorithm. The running
poses visualize parameters; they do not determine the score.

**Next experiment:** show two generations without explaining the formula and ask
which candidate should be retained and why. If players cannot connect a visible
parameter to the result, improve the comparison display before adding more
parameters. Test reproducibility separately from whether experimentation is fun.

## 9. Orb of Creation — make the next limit change the plan

**Primary source:** [Marple's page](https://marple.itch.io/orb-of-creation).
The creator describes spell-based incremental world creation, additive and
separately stacking modifiers, keyboard spell shortcuts and experimental save
compatibility. The page contains several version labels; the inspected title
screen specifically reported **v0.5.4.3**, despite the upload path saying v0.5.0.
Neither the path nor the current Steam version is a reliable substitute for
checking the actual browser build.

**Inspected:** started an empty save. The dark violet, pixel-textured interface
begins with a conspicuous circular mana reservoir, a 100-unit cap and +10/second
production. Purchased Infuse Orb for 25 mana: its level changed from 0/5 to 1/5,
the next price became 50 and regeneration changed to +12/second. Its tooltip
explains the +2 base mana/second effect. Later spells, conversion branches and
ending conditions were not reached during this inspection.

**Design interpretation:** a resource is more interesting when its production,
storage and uses create different limits. Faster filling does not solve a
storage requirement, and converting a stock can delay another purchase. The
player can diagnose which limit matters next. The corresponding risk is an
opaque dependency graph: an unaffordable action must explain whether the
problem is current stock, capacity, a prerequisite or an unavailable production
path. More resource names alone do not add meaningful decisions.

**Original reproduction:** [Lumen Workshop](../assets/pattern-lab/index.html?pattern=orb)
has a newly drawn crystal, charge ring and small conversion diagram. Regenerating
charge creates light; light becomes material; both expand light capacity. The
final awakening costs more light than initial capacity, so storage changes the
plan. Costs are explicit and a rejected cast at capacity consumes nothing. This
is a small original conversion puzzle, not the original spell catalog or economy.

**Next experiment:** ask players to explain why waiting alone cannot unlock the
final action. If they say only "I need more resources," show the capacity limit
more clearly. After comprehension is established, vary conversion costs to see
whether more than one order of investment remains attractive.

## 10. Gourdlets — let composition and inhabitants supply the reward

**Primary source:** [AuntyGames' page](https://aunty-games.itch.io/gourdlets).
The creator presents a town-building sandbox without scores or objectives,
where small residents interact with the player's town. The itch build is an
older demonstration; features of the full Steam release should not be silently
attributed to it. The page identifies Godot.

**Inspected:** entered the browser demo, followed the zoom introduction and
opened the construction interface. The opening station sits on a small block of
land beside a railway over bright, animated water. Built a grass tile and opened
the house category. An attempted house placement was followed by a gray world
view, so a successful building placement is **not confirmed**. The palette offers several
building silhouettes, while separate side controls expose terrain, vegetation,
furniture and other object categories. The undo control becomes available after
editing. Residents' later use of a developed town was not tested.

**Design interpretation:** a sandbox still needs a loop: select a material,
compose something, observe the scene, then revise it. A consistent isometric
projection and shared tile scale make individual objects combine into a world.
Ambient movement can make a tiny composition feel inhabited without rewarding
only numerical efficiency. Reversible placement invites experimentation; an
expensive irreversible mistake would work against this particular promise.

**Original reproduction:** [Pocket Hamlet](../assets/pattern-lab/index.html?pattern=hamlet)
uses a new isometric palette of houses, trees and workshops on a small grid.
Placement spends a limited construction budget; undo refunds the actual prior
placement. Decorative residents follow traversable routes around occupied
plots. They have no simulated occupations, needs or production economy. This
isolates composition, path readability and revision; it does not reproduce the
reference's assets, interior system or full resident behavior.

**Next experiment:** observe whether a player voluntarily revises a finished
layout and can explain the improvement. If the only reason to continue is an
external instruction, the sandbox may need richer combinations or clearer
inhabitant reactions. Do not automatically add points to solve a weak creative
vocabulary.

## 11. Planet Life — adjacent reference: progression can reveal a story

**Primary source:** [Crede's page](https://crede.itch.io/planet), which also links
the [official standalone browser edition](https://planetlife.space/).
The creator describes a planet, resource gathering, upgrades, companions and
dungeon exploration across three chapters with an ending. The itch page warns
about browser save persistence and points to the other edition. It is an
adventure/incremental comparator, not one of the ten core simulation studies.
**Evidence limit:** source study only; no gameplay or save transfer was tested.

**Design interpretation:** progression need not mean an endlessly larger income.
A new companion or place can change the meaning and available uses of resources.
Story reveals can therefore supply a short-term question alongside the economic
question. The danger is treating dialogue as a reward disconnected from the
actions that earned it. A new event is strongest when it changes the next plan.

**Transfer recipe:** for a new original game, let the first complete economic
cycle reveal a character with a new use for the product. Keep the first choice
simple; introduce a new verb only after the player has used the old one. Use
one shared visual language for portraits, world objects and resource symbols.
Animate the newly available path in the world, rather than relying only on a
new badge in a distant menu. These are proposed design choices, not animation
observations of Planet Life.

**Next experiment:** compare a larger numerical reward with a small new encounter
that introduces a decision. Ask what the player wants to discover next. Track
comprehension and voluntary continuation before claiming story improves retention.
No Planet Life clone or original assets are included in the lab.

## 12. Tiny Islands — adjacent reference: a small decision can reshape a map

**Primary source:** [David King's 2019 game page](https://dr-d-king.itch.io/tiny-islands).
The creator describes a card-driven adaptation of roll-and-write island drawing,
with spatial scoring and hover explanations. Holding space or the mouse speeds
up the scoring presentation. This is a spatial/card comparator, not a tenth
simulation chosen to inflate the sample. **Evidence limit:** source study only;
no island completion, scoring animation or device behavior was directly tested.

**Design interpretation:** limited options make spatial consequences easier to
read. A placement can be locally attractive but use space another combination
needs later. Showing the scoring rule near the relevant material can let the
player reason before committing. The creator's scoring-speed control also
suggests a useful general distinction: the first result reveal teaches, while
repeat players may prefer a quicker acknowledgement.

**Transfer recipe:** choose two or three original terrain pieces, a small board
and a visible adjacency rule. Preview both legal placement and its immediate
score before commitment; at round end, trace which groups contributed. Keep
score calculation independent from the animation, so skipping the reveal cannot
change a result. Use consistent edge alignment, scale and texture density so
the completed board reads as a landscape rather than unrelated icons. These
are proposed techniques, not claims that all are implemented by Tiny Islands.

**Next experiment:** ask a player to predict a placement's score and identify
what future opportunity it blocks. If prediction is difficult, simplify or
explain the rule before adding more terrain types. No separate Tiny Islands
clone is bundled; Pocket Hamlet supplies an original spatial rendering example
without claiming to implement this card/scoring system.
