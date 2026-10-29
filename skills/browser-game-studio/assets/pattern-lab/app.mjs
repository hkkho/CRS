import { patterns, byId } from "./catalog.mjs";
import {
  createState,
  command,
  advance,
  encode,
  decode,
  requests,
  served,
} from "./model.mjs";
import { drawScene, waterCell, hamletCell } from "./graphics.mjs";

const $ = (id) => document.getElementById(id);
const config = await fetch(new URL("./project.json", import.meta.url)).then(
  (r) => {
    if (!r.ok)
      throw new Error(
        "project.json could not be loaded. Serve this folder over HTTP.",
      );
    return r.json();
  },
);
const configuredSeed =
  Number.isInteger(config.seed) && config.seed >= 0 && config.seed <= 0xffffffff
    ? config.seed
    : 42;
const query = new URLSearchParams(location.search);
let pattern =
  (config.mode === "game"
    ? byId[config.pattern]
    : (byId[query.get("pattern")] ?? byId[config.pattern])) ?? patterns[0];
let state = createState(pattern.id, configuredSeed),
  paused = false,
  audio = null,
  sound = false;
let reducedMotion = matchMedia("(prefers-reduced-motion: reduce)").matches;
let visualTime = 0,
  lastTime = performance.now(),
  accumulator = 0,
  lastPaintedReadings = "",
  brief = "";
const ui = { effects: [], drag: null, reducedMotion };
const canvas = $("scene"),
  context = canvas.getContext("2d");
const keyFor = (id) =>
  `browser-game-studio:v1:${config.mode}:${config.title}:${id}`;
const format = (n) => (Number.isInteger(n) ? String(n) : n.toFixed(1));
if (config.mode === "game") document.body.classList.add("game-only");
document.title =
  config.mode === "game" ? config.title : "Browser Game Studio · Pattern Lab";

for (const [i, p] of patterns.entries()) {
  const b = document.createElement("button");
  b.type = "button";
  b.className = "pattern";
  b.dataset.pattern = p.id;
  b.setAttribute("aria-current", String(p.id === pattern.id));
  const n = document.createElement("span");
  n.className = "pattern-number";
  n.textContent = String(i + 1).padStart(2, "0");
  const label = document.createElement("span"),
    strong = document.createElement("strong"),
    small = document.createElement("small");
  strong.textContent = p.title;
  small.textContent = p.genre;
  label.append(strong, small);
  b.append(n, label);
  b.addEventListener("click", () => select(p));
  $("patterns").append(b);
}

function select(p) {
  pattern = p;
  state = createState(p.id, configuredSeed);
  ui.effects = [];
  ui.drag = null;
  ui.hover = null;
  paused = false;
  accumulator = 0;
  for (const b of $("patterns").children)
    b.setAttribute("aria-current", String(b.dataset.pattern === p.id));
  history.replaceState(null, "", `?pattern=${p.id}`);
  mount();
  say("A fresh experiment. Read the goal, then try an action.");
}
function say(message, rejected = false) {
  $("feedback").textContent = message;
  $("feedback").classList.toggle("rejected", rejected);
}
function beep(kind) {
  if (!sound || !audio) return;
  const o = audio.createOscillator(),
    g = audio.createGain(),
    now = audio.currentTime;
  o.type = "sine";
  o.frequency.setValueAtTime(
    kind === "upgrade" ? 740 : kind === "rejected" ? 190 : 490,
    now,
  );
  o.frequency.exponentialRampToValueAtTime(
    kind === "rejected" ? 150 : 660,
    now + 0.11,
  );
  g.gain.setValueAtTime(0.0001, now);
  g.gain.exponentialRampToValueAtTime(0.035, now + 0.012);
  g.gain.exponentialRampToValueAtTime(0.0001, now + 0.15);
  o.connect(g).connect(audio.destination);
  o.start(now);
  o.stop(now + 0.16);
}
function act(action, value) {
  const result = command(state, action, value);
  say(result.message, !result.accepted);
  if (result.accepted) {
    state = result.state;
    if (result.kind !== "select")
      ui.effects.push({
        at: visualTime,
        kind: result.kind,
        x: ui.hover?.x ?? 480,
        y: ui.hover?.y ?? 315,
        color: pattern.accent,
      });
    beep(result.kind);
  } else beep("rejected");
  paintInterface();
  return result.accepted;
}
function button(label, action, value, detail) {
  const b = document.createElement("button");
  b.type = "button";
  const name = document.createElement("span");
  name.textContent = label;
  b.append(name);
  if (detail) {
    const small = document.createElement("small");
    small.textContent = detail;
    b.append(small);
  }
  b.dataset.action = action;
  if (value !== undefined) b.dataset.value = String(value);
  b.addEventListener("click", () => act(action, value));
  $("actions").append(b);
}
function mount() {
  const i = patterns.indexOf(pattern) + 1;
  $("pattern-number").textContent =
    `PATTERN ${String(i).padStart(2, "0")} / FIELD EXPERIMENT`;
  $("note-number").textContent = String(i).padStart(2, "0");
  $("genre").textContent = pattern.genre;
  $("title").textContent =
    config.mode === "game" ? config.title : pattern.title;
  $("premise").textContent = pattern.premise;
  $("lesson").textContent = pattern.lesson;
  $("art-note").textContent = pattern.art;
  $("motion-note").textContent = pattern.motion;
  $("test-note").textContent = pattern.test;
  $("source").textContent = pattern.source;
  $("source").href = pattern.url;
  $("seed-label").textContent = state.seed;
  document.documentElement.style.setProperty("--accent", pattern.accent);
  $("actions").replaceChildren();
  switch (pattern.id) {
    case "council":
      button("Accept request", "yes", undefined, "Commit the shown tradeoff");
      button(
        "Decline request",
        "no",
        undefined,
        "Keep or gain resources; accept the other consequence",
      );
      break;
    case "post":
      for (let n = 1; n <= 3; n++)
        button(`Postage ${n}`, "stamp", n, "Select exact postage");
      button(
        "Ship: harbor",
        "ship",
        "harbor",
        "Or drag to the blue harbor tray",
      );
      button("Ship: forest", "ship", "forest", "Or drag to the forest tray");
      break;
    case "water":
      button("Install pipe", "pipe", undefined, "One coin · selected plot");
      button("Expand pump", "pump", undefined, "Four coins · +1 served home");
      button("End day", "day", undefined, "Unserved homes cost influence");
      break;
    case "harvest":
      button(
        "Gather sea seeds",
        "gather",
        undefined,
        "Or hover over the vessel",
      );
      button(
        "Buy automatic net",
        "net",
        undefined,
        "Costs 5 + 4 per existing net",
      );
      button(
        "Improve vessel",
        "vessel",
        undefined,
        "Costs 12 × current vessel level",
      );
      break;
    case "flock":
      button("Feed the flock", "feed", undefined, "Two coins · five feed");
      button(
        "Collect & sell eggs",
        "collect",
        undefined,
        "Two coins per whole egg",
      );
      button(
        "Add a hen",
        "hen",
        undefined,
        "Eight coins · more production and appetite",
      );
      break;
    case "factory":
      button("Use hand press", "crank", undefined, "One ore → one part");
      button("Ship the parts", "sell", undefined, "Two coins per part");
      button("Order ore", "ore", undefined, "Three coins → six ore");
      button(
        "Add automatic press",
        "machine",
        undefined,
        "Costs 8 + 4 per existing press",
      );
      break;
    case "garden":
      button(
        "Water the soil",
        "water",
        undefined,
        "Free care · refill moisture",
      );
      button(
        "Sell a mature cutting",
        "sell",
        undefined,
        "Six coins · the parent grows again",
      );
      button(
        "Add a pot",
        "pot",
        undefined,
        "Four coins · seven pots fit on the shelf",
      );
      break;
    case "evolution":
      button(
        "Run a generation",
        "trial",
        undefined,
        "Six seconds · retain the best and mutate five",
      );
      break;
    case "orb":
      button("Cast light", "cast", undefined, "One charge → two light");
      button("Transmute", "transmute", undefined, "Three light → one material");
      button(
        "Expand capacity",
        "expand",
        undefined,
        "Four light + one material → +3 storage",
      );
      button(
        "Awaken the world",
        "awaken",
        undefined,
        "Eight light + three material",
      );
      break;
    case "hamlet":
      button("Choose house", "tool", "house", "Three coins");
      button("Choose tree", "tool", "tree", "One coin");
      button("Choose workshop", "tool", "workshop", "Five coins");
      button(
        "Place on selected plot",
        "place",
        undefined,
        "Click a plot, or use arrows and Enter",
      );
      button("Undo placement", "undo", undefined, "Return the full cost");
      break;
  }
  lastPaintedReadings = "";
  paintInterface();
  resize();
}

function describe(s) {
  switch (s.id) {
    case "council": {
      const r = requests[Math.min(s.turn, 7)],
        d = r.yes,
        n = r.no;
      return {
        stats: [
          ["Request", `${Math.min(s.turn + 1, 8)} / 8`],
          ["Coins", s.coins],
          ["Trust", s.trust],
          ["Residents", s.people],
        ],
        instruction: `${r.who}: ${r.text} Accept: ${d[0]} coins, ${d[1]} trust, ${d[2]} residents. Decline: ${n[0]} coins, ${n[1]} trust, ${n[2]} residents. Goal: at least 18 residents after eight requests, with trust remaining.`,
      };
    }
    case "post":
      return {
        stats: [
          ["Delivered", `${s.delivered} / 6`],
          ["Credit", s.coins],
          ["Postage", s.parcel.stamp],
          ["Required", s.parcel.weight],
        ],
        instruction: `Address: ${s.parcel.route.toUpperCase()}. Required postage: ${s.parcel.weight}. Select the exact stamp, then drag the parcel into its route tray or use a route button. Correct shipment earns four credit before postage. Mistakes are rejected without spending.`,
      };
    case "water":
      return {
        stats: [
          ["Coins", s.coins],
          ["Influence", s.influence],
          ["Supplied", `${served(s)} / 3`],
          ["Pump capacity", s.capacity],
        ],
        instruction: `Plot ${s.selected + 1} selected. Click a grid cell, then Install pipe. Arrow keys move the selection; Enter installs. Blue means connected; amber ! means connected but beyond pump capacity. Supply three homes and complete two day reports.`,
      };
    case "harvest":
      return {
        stats: [
          ["Cargo", Math.floor(s.cargo)],
          ["Harvested", `${Math.floor(s.total)} / 60`],
          ["Automatic nets", s.nets],
          ["Vessel", s.vessel],
        ],
        instruction:
          "Gather with the button, or keep the pointer over the vessel. Buy automation after experiencing manual work. Upgrade costs consume cargo, while the voyage goal counts total harvested. No clicking marathon is required.",
      };
    case "flock":
      return {
        stats: [
          ["Coins", s.coins],
          ["Feed", s.feed],
          ["Hens", s.hens],
          ["Eggs", s.eggs],
          ["Sold", `${s.sold} / 10`],
        ],
        instruction:
          "Fed hens lay over time. Nests have limited space, so collect and sell eggs. More hens consume feed faster. Reach four hens and ten eggs sold. The clock can be paused while you read.",
      };
    case "factory":
      return {
        stats: [
          ["Coins", s.coins],
          ["Ore", s.ore],
          ["Output", `${s.parts} / 8`],
          ["Auto presses", s.machines],
          ["Shipped", `${s.sold} / 16`],
        ],
        instruction:
          "Make a part, ship it, buy supplies and decide when to automate. Full output or missing ore stops automatic production. Build two automatic presses and ship sixteen parts.",
      };
    case "garden":
      return {
        stats: [
          ["Coins", s.coins],
          ["Moisture", `${Math.round(s.water * 100)}%`],
          ["Pots", `${s.plants.length} / 7`],
          ["Mature", s.plants.filter((g) => g >= 1).length],
          ["Cuttings sold", s.sold],
        ],
        instruction:
          "Water when soil dries. Mature plants show a gold bud and can supply a cutting. Plants wait without dying when dry. This is a calm sandbox; it has no forced victory or failure. Growth pauses in hidden tabs; there is no offline catch-up in this prototype.",
      };
    case "evolution":
      return {
        stats: [
          ["Generation", s.generation],
          ["Trial", `${s.trial.toFixed(1)} / 6 s`],
          ["Best", `${s.best.toFixed(2)} m`],
          ["Previous best", `${s.previousBest.toFixed(2)} m`],
        ],
        instruction:
          "Compare six parameterized gaits, retain the best and mutate the rest. Run another generation after the trial finishes. This is an explicit deterministic toy fitness model, not rigid-body physics, a neural network or a recreation of Evolution’s learning system.",
      };
    case "orb":
      return {
        stats: [
          ["Charge", s.charge],
          ["Light", `${Math.floor(s.light)} / ${s.capacity}`],
          ["Material", s.wood],
        ],
        instruction:
          "Charge regenerates. Cast it into light, transmute some light into material, and expand storage before trying the final eight-light action. The world needs eight light and three material to awaken.",
      };
    case "hamlet":
      return {
        stats: [
          ["Coins", s.coins],
          ["Placed", s.tiles.length],
          ["Selected plot", s.selected + 1],
          ["Tool", s.tool],
        ],
        instruction:
          "Choose an object, select a diamond plot and place. Arrows move the selected plot; Enter places. Undo returns its cost. Residents follow decorative grid routes to the arrangement; they do not simulate jobs or an economy. No score or forced ending.",
      };
  }
}

function paintInterface() {
  const d = describe(state),
    fingerprint = JSON.stringify(d.stats);
  if (fingerprint !== lastPaintedReadings) {
    const rows = d.stats.map(([name, value]) => {
      const div = document.createElement("div"),
        dt = document.createElement("dt"),
        dd = document.createElement("dd");
      dt.textContent = name;
      dd.textContent = typeof value === "number" ? format(value) : value;
      div.append(dt, dd);
      return div;
    });
    $("readings").replaceChildren(...rows);
    lastPaintedReadings = fingerprint;
  }
  if ($("instruction").textContent !== d.instruction)
    $("instruction").textContent = d.instruction;
  $("seed-label").textContent = state.seed;
  $("pause").textContent = paused ? "Resume" : "Pause";
  $("pause").setAttribute("aria-pressed", String(paused));
  $("motion").textContent = reducedMotion ? "Motion reduced" : "Reduce motion";
  $("motion").setAttribute("aria-pressed", String(reducedMotion));
  $("sound").textContent = sound ? "Sound on" : "Sound off";
  $("sound").setAttribute("aria-pressed", String(sound));
  for (const b of $("actions").children)
    b.disabled = state.status !== "playing";
  const ended = state.status !== "playing";
  $("outcome").hidden = !ended;
  if (ended)
    $("outcome").textContent =
      state.status === "won"
        ? "Experiment complete. Inspect the result, then restart to try a different approach."
        : "The plan did not hold together. Restart and change one decision.";
  canvas.dataset.pattern = state.id;
  canvas.dataset.status = state.status;
}

function point(event) {
  const r = canvas.getBoundingClientRect();
  return {
    x: ((event.clientX - r.left) * 960) / r.width,
    y: ((event.clientY - r.top) * 540) / r.height,
  };
}
canvas.addEventListener("pointerdown", (event) => {
  const p = point(event);
  ui.hover = p;
  canvas.focus({ preventScroll: true });
  if (state.id === "post" && p.x > 320 && p.x < 620 && p.y > 200 && p.y < 440) {
    ui.drag = p;
    canvas.setPointerCapture(event.pointerId);
  } else if (state.id === "water") {
    const n = waterCell(p.x, p.y);
    if (n !== null) act("select", n);
  } else if (state.id === "hamlet") {
    const n = hamletCell(p.x, p.y);
    if (n !== null) act("select", n);
  }
});
canvas.addEventListener("pointermove", (event) => {
  ui.hover = point(event);
  if (ui.drag) ui.drag = ui.hover;
});
canvas.addEventListener("pointerleave", () => {
  if (!ui.drag) ui.hover = null;
});
canvas.addEventListener("pointerup", (event) => {
  if (ui.drag) {
    const p = point(event);
    if (p.x >= 708 && p.x <= 911 && p.y >= 155 && p.y <= 452)
      act("ship", p.y < 300 ? "harbor" : "forest");
    else say("Parcel returned to the desk. Drop inside a labelled route tray.");
    ui.drag = null;
    if (canvas.hasPointerCapture(event.pointerId))
      canvas.releasePointerCapture(event.pointerId);
  }
});
canvas.addEventListener("pointercancel", (event) => {
  ui.drag = null;
  ui.hover = null;
  if (canvas.hasPointerCapture(event.pointerId))
    canvas.releasePointerCapture(event.pointerId);
});
canvas.addEventListener("keydown", (event) => {
  const width = state.id === "water" ? 6 : 7,
    count = state.id === "water" ? 24 : 35;
  if (["water", "hamlet"].includes(state.id) && event.key.startsWith("Arrow")) {
    event.preventDefault();
    const offsets = {
      ArrowLeft: -1,
      ArrowRight: 1,
      ArrowUp: -width,
      ArrowDown: width,
    };
    const next = Math.max(
      0,
      Math.min(count - 1, state.selected + offsets[event.key]),
    );
    act("select", next);
  } else if (event.key === "Enter") {
    event.preventDefault();
    if (state.id === "water") act("pipe");
    else if (state.id === "hamlet") act("place");
  } else if (event.code === "Space" && state.id === "harvest") {
    event.preventDefault();
    act("gather");
  }
});

$("restart").addEventListener("click", () => {
  state = createState(pattern.id, configuredSeed);
  ui.effects = [];
  ui.drag = null;
  accumulator = 0;
  paused = false;
  paintInterface();
  say("Restarted with the same seed. Try a different plan.");
});
$("pause").addEventListener("click", () => {
  paused = !paused;
  accumulator = 0;
  paintInterface();
  say(
    paused
      ? "Simulation paused. Actions remain available for planning and inspection."
      : "Simulation resumed.",
  );
});
$("motion").addEventListener("click", () => {
  reducedMotion = !reducedMotion;
  ui.reducedMotion = reducedMotion;
  ui.effects = [];
  paintInterface();
});
$("sound").addEventListener("click", async () => {
  try {
    if (!audio)
      audio = new (window.AudioContext ?? window.webkitAudioContext)();
    await audio.resume();
    sound = !sound;
    paintInterface();
    beep("work");
  } catch {
    sound = false;
    say("Audio is unavailable in this browser. All feedback remains visible.");
  }
});
$("save").addEventListener("click", () => {
  try {
    localStorage.setItem(keyFor(state.id), encode(state));
    say(
      "Saved locally for this pattern. Saved prototypes do not advance while closed.",
    );
  } catch {
    say(
      "Local storage is unavailable. This session can still be played.",
      true,
    );
  }
});
$("restore").addEventListener("click", () => {
  try {
    const raw = localStorage.getItem(keyFor(pattern.id));
    if (!raw) throw new Error("No local save exists for this pattern.");
    const candidate = decode(raw);
    if (candidate.id !== pattern.id)
      throw new Error("This save belongs to another pattern.");
    state = candidate;
    ui.effects = [];
    ui.drag = null;
    accumulator = 0;
    paintInterface();
    say("Verified local snapshot restored. Hidden time was not added.");
  } catch (error) {
    say(`Restore rejected: ${error.message}`, true);
  }
});
$("export-scene").addEventListener("click", () =>
  canvas.toBlob((blob) => {
    if (!blob) {
      say("Scene export is unavailable.", true);
      return;
    }
    const url = URL.createObjectURL(blob),
      a = document.createElement("a");
    a.href = url;
    a.download = `${pattern.id}-scene.png`;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }, "image/png"),
);
document.addEventListener("visibilitychange", () => {
  lastTime = performance.now();
  accumulator = 0;
  ui.hover = null;
  ui.drag = null;
});
function resize() {
  const r = canvas.getBoundingClientRect(),
    ratio = Math.min(devicePixelRatio || 1, 2);
  canvas.width = Math.max(1, Math.round(r.width * ratio));
  canvas.height = Math.max(1, Math.round(((r.width * 9) / 16) * ratio));
  context.setTransform(canvas.width / 960, 0, 0, canvas.height / 540, 0, 0);
}
new ResizeObserver(resize).observe(canvas);

$("brief-form").addEventListener("submit", (event) => {
  event.preventDefault();
  const idea = $("idea").value.trim(),
    role = $("role").value.trim(),
    verb = $("verb").value.trim(),
    constraint = $("constraint").value.trim();
  if (!idea || !role || !verb || !constraint) return;
  brief = `# ${idea}\n\n## Playable promise\nI am a ${role}. I repeatedly ${verb}, while ${constraint}. Each action must visibly change the next decision.\n\n## Primary pattern\n${pattern.genre}. Study: ${pattern.source} (${pattern.url}).\n\n## First slice\n- One readable scene and one main action: ${verb}.\n- Two reasonable choices with explicit costs or compositional differences.\n- One complete loop: inspect → choose → commit → see consequence → plan again.\n- One progression change after the player understands the initial action.\n- A restart or undo path proportionate to the stakes.\n\n## Visual and motion direction\nChoose original silhouettes, projection and materials that express this setting. Pattern reference: ${pattern.art}\nFeedback sequence to adapt: ${pattern.motion}\n\n## Technical contract\nKeep rules independent of the view. Decide turn/fixed-step/idle timing and persistence explicitly. Preserve any existing authoritative simulation. Verify the installed engine version before using starter APIs.\n\n## Evidence to collect\n${pattern.test}\nTest primary input, rejection, a changed next decision, reset, narrow layout and reduced motion in the real browser. Ask a new player to predict a consequence and explain their next plan.\n\n## Open design decisions\nChoose actual costs, progression, ending or sandbox return reason, target devices and asset provenance. This brief is a starting hypothesis, not a validated design.\n`;
  $("brief-text").textContent = brief;
  $("brief-result").hidden = false;
});
$("download-brief").addEventListener("click", () => {
  if (!brief) return;
  const url = URL.createObjectURL(
      new Blob([brief], { type: "text/markdown;charset=utf-8" }),
    ),
    a = document.createElement("a");
  a.href = url;
  a.download = "game-brief.md";
  a.click();
  URL.revokeObjectURL(url);
});

function frame(now) {
  const delta = Math.max(0, Math.min(1, (now - lastTime) / 1000));
  lastTime = now;
  if (!paused && !document.hidden) {
    visualTime += delta;
    accumulator += delta;
    // Bounded fixed steps prevent a resumed tab from applying a large surprise interval.
    while (accumulator >= 1 / 30) {
      const previous = state;
      state = advance(state, 1 / 30);
      accumulator -= 1 / 30;
      if (
        state.id === "harvest" &&
        ui.hover &&
        ui.hover.x >= 290 &&
        ui.hover.x <= 660 &&
        ui.hover.y >= 185 &&
        ui.hover.y <= 370 &&
        state.cooldown === 0 &&
        state.status === "playing"
      )
        act("gather");
      if (previous.status === "playing" && state.status !== "playing") {
        say("Voyage completed: sixty sea seeds harvested.");
        beep("upgrade");
      }
    }
  }
  ui.effects = ui.effects.filter((e) => visualTime - e.at < 0.75);
  drawScene(context, state, visualTime, ui);
  paintInterface();
  requestAnimationFrame(frame);
}
mount();
requestAnimationFrame(frame);
