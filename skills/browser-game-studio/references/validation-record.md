# Initial validation record

Date: **24 October 2026**. Environment: Windows, Node 24, Python 3.13 and the
in-app Chromium browser. This is an author-tested prototype library and skill,
not an external playtest or evidence of general creative ability.

## Automated results

- **17 model tests passed.** Coverage includes mixed-policy council completion,
  a losing policy, atomic rejected commands, postage plus destination validation,
  grid topology without row wrapping, water capacity and completion, harvest
  recovery/automation, feed consumption, bounded production, plant growth,
  seeded elite trials, conversion capacity, exact undo refunds and save validation.
- **3 packaging tests passed.** A new selected/title/seed configuration is created;
  invalid input creates nothing; an existing folder and its work are preserved;
  conditional module requests return valid JavaScript MIME and current content.
- The skill frontmatter/scaffold validator passed. JavaScript files were formatted
  with Prettier 3.9.10; this adds no runtime dependency.

## Actual browser actions

| Study or utility | Observed result |
| --- | --- |
| Cloud Council | First acceptance changed coins 12→7, trust 10→13, residents 8→11. A mixed policy completed; eight declines ended with seven residents and a loss. Restart returned the initial state. |
| Moonpost Desk | A forest dispatch for a harbor address was rejected without a charge. Postage 1 and harbor dispatch advanced deliveries to 1/6 and credit to 15. |
| Water District | Arrow/Enter placed connected pipes; two pump purchases and two day reports completed service to all three homes. |
| Night Harvest | Manual gathering raised cargo; a five-cargo automatic net purchase changed net count to one. Cooldown prevented rapid repeated clicks from granting unlimited catches. |
| Meadow Flock | Feeding plus another hen changed feed to nine and hens to three. Later collection sold four eggs and granted eight coins. |
| Copper Works | A hand press consumed ore, shipping granted coins, and an automatic press purchase changed the machine count. After resuming, eight ore became a full eight-part buffer; pausing preserved those values. No console errors were reported for the lab. |
| Window Garden | Watering filled moisture to 100%; buying a pot changed count to three and coins to two. Different growth silhouettes were visible. |
| Creature Trials | A trial advanced to 6/6 simulated seconds, reporting best 7.14 m for seed 42. Starting again advanced to generation two. This is the analytic toy described in the case study. |
| Lumen Workshop | Casts filled light storage to 5/5; a further cast explained the full-storage rejection. |
| Pocket Hamlet | Keyboard placement spent three coins and placed a house. Undo returned coins to 24 and placed count to zero. |
| Save/restore | Saved the council after its first acceptance, restarted, then restored the same request and resource values. |
| Reduced motion | Control toggled; the scene remained usable with the changed state. The implementation also reads the operating-system preference at startup; an OS preference change was not separately emulated. |
| Brief/export | Form produced a Markdown brief using the selected pattern and entered fields. Downloaded `game-brief.md`; canvas export downloaded `hamlet-scene.png`. |
| Standalone scaffold | Generated “Copper Moon Workshop”, factory, seed 73. Its page kept the factory despite `?pattern=council`, hid library navigation and changed output after a real hand-press click. |
| Layout | Desktop scenes for all ten patterns were visually inspected. At viewport widths 320 and 640, document widths were 305 and 625: no document-wide horizontal overflow. The narrow library uses intentional horizontal navigation. |

The browser inspection exposed an invalid `.mjs` MIME association in the standard
Windows static server. The package now supplies explicit module MIME types and
no-store responses; startup succeeded after that repair. The factory drawing was
also changed so an unpurchased automatic press does not continuously stroke:
manual work triggers feedback and automatic work requires an active machine,
ore and free output space.

The in-app browser sometimes delivered frames slowly. The updated loop permits
up to one second of visible elapsed time, advanced in bounded 1/30-second steps,
and discards hidden-tab elapsed time. No cross-device frame-rate benchmark or
low-end performance claim was established.

## What remains unverified

Human fun, first-time comprehension and voluntary replay; independent skill
consumer tasks; real phone/touch behavior; audible quality; assistive-technology
use beyond semantic DOM controls; long-session save migration and storage-denied
behavior; cross-browser and production hosting. Not every goal was completed
through UI: the model suite covers additional end states. Postal dragging was
implemented but the verified delivery used its accessible button alternative.
Hidden-tab catch-up rejection was reviewed in source, not measured in a timed
browser lifecycle experiment. Do not promote these limits into completed tests.

Reference-game observations are recorded separately in the case studies. In
particular, Evolution's inspected onboarding did not establish a trained creature;
Gourdlets' terrain placement succeeded but its house-placement attempt ended in
a gray world view, so building placement was not confirmed. These are inspection
limits, not independently diagnosed defects in those games.
