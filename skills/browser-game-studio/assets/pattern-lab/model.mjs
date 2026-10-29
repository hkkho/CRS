// Rules are independent of rendering. Commands validate on a copy and return a
// replacement state only when accepted; animation never awards resources.
export const VERSION = 1;
export const IDS = [
  "council",
  "post",
  "water",
  "harvest",
  "flock",
  "factory",
  "garden",
  "evolution",
  "orb",
  "hamlet",
];
const clamp = (n, lo, hi) => Math.max(lo, Math.min(hi, n));
function random(s) {
  s.rng = (Math.imul(1664525, s.rng) + 1013904223) >>> 0;
  return s.rng / 4294967296;
}
const clone = (value) => structuredClone(value);

export const requests = [
  {
    who: "Mira, the gardener",
    text: "A rooftop orchard would feed our new arrivals. Will you fund the seedlings?",
    yes: [-5, 3, 3],
    no: [0, -2, 0],
  },
  {
    who: "Oren, the courier",
    text: "The ferry needs new ropes. A repair now will bring two more families.",
    yes: [-4, 1, 2],
    no: [1, -1, 0],
  },
  {
    who: "Pip, the musician",
    text: "May I play at the market? A quiet evening could lift everyone’s spirits.",
    yes: [-2, 4, 0],
    no: [2, -2, 0],
  },
  {
    who: "Nell, the trader",
    text: "I can rent the central square. There will be noise, but the town earns six coins.",
    yes: [6, -3, 1],
    no: [0, 1, 0],
  },
  {
    who: "Mira, the gardener",
    text: "The orchard is thirsty. A water cart costs four coins and keeps our neighbours happy.",
    yes: [-4, 3, 1],
    no: [0, -3, -1],
  },
  {
    who: "Oren, the courier",
    text: "A new route will pay back tomorrow. Can the town lend five coins today?",
    yes: [-5, 2, 2],
    no: [1, -1, 0],
  },
  {
    who: "Nell, the trader",
    text: "My stall has done well. Accept a donation in exchange for a busier market?",
    yes: [7, -2, 1],
    no: [0, 2, 0],
  },
  {
    who: "Pip, the musician",
    text: "One last lantern festival? Four coins would give the whole town a lovely night.",
    yes: [-4, 4, 1],
    no: [0, -2, 0],
  },
];

function parcel(s) {
  return {
    route: random(s) < 0.5 ? "harbor" : "forest",
    weight: 1 + Math.floor(random(s) * 3),
    stamp: 0,
  };
}
function genome(s, parent) {
  return {
    stride: clamp(
      (parent?.stride ?? 0.25 + random(s) * 0.6) +
        (parent ? (random(s) - 0.5) * 0.25 : 0),
      0.1,
      1,
    ),
    rhythm: clamp(
      (parent?.rhythm ?? 0.2 + random(s) * 0.7) +
        (parent ? (random(s) - 0.5) * 0.3 : 0),
      0.1,
      1,
    ),
    distance: 0,
  };
}
export function speed(g) {
  return (
    0.2 +
    Math.max(0, 1 - ((g.stride - 0.73) ** 2 * 3 + (g.rhythm - 0.59) ** 2 * 4))
  );
}

export function createState(id, seed = 42) {
  if (
    !IDS.includes(id) ||
    !Number.isInteger(seed) ||
    seed < 0 ||
    seed > 0xffffffff
  )
    throw new Error("Choose a known pattern and a uint32 seed.");
  const s = {
    version: VERSION,
    id,
    seed,
    rng: seed >>> 0,
    time: 0,
    turn: 0,
    status: "playing",
    events: 0,
  };
  const initial = {
    council: { coins: 12, trust: 10, people: 8 },
    post: { coins: 12, delivered: 0 },
    water: {
      coins: 18,
      influence: 6,
      capacity: 1,
      pipes: [6],
      selected: 7,
      days: 0,
    },
    harvest: { cargo: 0, total: 0, nets: 0, vessel: 1, cooldown: 0 },
    flock: { coins: 10, feed: 4, eggs: 0, sold: 0, hens: 2 },
    factory: { coins: 10, ore: 8, parts: 0, machines: 0, sold: 0, work: 0 },
    garden: { coins: 6, water: 0.65, sold: 0, plants: [0.25, 0.6] },
    evolution: {
      generation: 1,
      trial: 0,
      running: false,
      best: 0,
      previousBest: 0,
    },
    orb: { charge: 4, light: 0, wood: 0, capacity: 5, power: 1 },
    hamlet: { coins: 24, selected: 17, tool: "house", tiles: [], undo: [] },
  };
  Object.assign(s, clone(initial[id]));
  if (id === "post") s.parcel = parcel(s);
  if (id === "evolution")
    s.creatures = Array.from({ length: 6 }, () => genome(s));
  return s;
}

// Orthogonal connectivity is a rule. The moving blue dots in the view only
// explain the result of this traversal; they never decide which home is served.
export function connected(s) {
  const visited = new Set([6]),
    pending = [6],
    pipes = new Set(s.pipes);
  while (pending.length) {
    const n = pending.shift();
    for (const m of [
      n - 6,
      n + 6,
      n % 6 ? n - 1 : -1,
      n % 6 < 5 ? n + 1 : -1,
    ]) {
      if (m >= 0 && m < 24 && pipes.has(m) && !visited.has(m)) {
        visited.add(m);
        pending.push(m);
      }
    }
  }
  return visited;
}
export function served(s) {
  const network = connected(s);
  return Math.min(s.capacity, [5, 17, 23].filter((n) => network.has(n)).length);
}

export function command(state, action, value) {
  if (state.status !== "playing")
    return {
      state,
      accepted: false,
      message: "This run has ended. Restart to try another plan.",
    };
  const s = clone(state);
  let message = "",
    kind = "work";
  const fail = (text) => ({ state, accepted: false, message: text });
  switch (s.id) {
    case "council": {
      if (!["yes", "no"].includes(action))
        return fail("Choose an answer to the current request.");
      const r = requests[s.turn],
        delta = r[action];
      if (s.coins + delta[0] < 0)
        return fail(
          "The treasury cannot afford this promise. Decline or restart with a different plan.",
        );
      s.coins += delta[0];
      s.trust = Math.max(0, s.trust + delta[1]);
      s.people = Math.max(0, s.people + delta[2]);
      s.turn++;
      message = `${action === "yes" ? "Accepted" : "Declined"} · ${delta[0] >= 0 ? "+" : ""}${delta[0]} coins · ${delta[1] >= 0 ? "+" : ""}${delta[1]} trust`;
      if (s.trust <= 0) {
        s.status = "lost";
        message = "The town lost trust in its council.";
      } else if (s.turn === requests.length) {
        s.status = s.people >= 18 ? "won" : "lost";
        message =
          s.status === "won"
            ? `Eight requests resolved. Your town has ${s.people} residents and ${s.trust} trust.`
            : `The town kept ${s.people} residents, short of the eighteen-resident goal. Try balancing growth with treasury needs.`;
      }
      kind = "decision";
      break;
    }
    case "post": {
      if (action === "stamp") {
        if (![1, 2, 3].includes(value))
          return fail("Postage must be one, two or three.");
        s.parcel.stamp = value;
        message = `Postage selected: ${value}. It will be paid when the parcel ships.`;
        kind = "stamp";
      } else if (action === "ship") {
        if (value !== s.parcel.route)
          return fail(
            `The address belongs on the ${s.parcel.route} route. No postage was spent.`,
          );
        if (s.parcel.stamp !== s.parcel.weight)
          return fail(
            `This parcel needs exactly ${s.parcel.weight} postage. Select the matching stamp.`,
          );
        if (s.coins < s.parcel.stamp) return fail("Not enough postage credit.");
        s.coins += 4 - s.parcel.stamp;
        s.delivered++;
        s.turn++;
        s.parcel = parcel(s);
        message = "Parcel dispatched. A receipt records the delivery.";
        kind = "delivery";
        if (s.delivered >= 6) {
          s.status = "won";
          message =
            "Six parcels delivered correctly. The desk is clear for the next shift.";
        }
      } else return fail("Select postage, then choose a shipping route.");
      break;
    }
    case "water": {
      if (action === "select") {
        if (!Number.isInteger(value) || value < 0 || value >= 24)
          return fail("Choose a cell inside the district.");
        s.selected = value;
        message = `Cell ${value + 1} selected.`;
        kind = "select";
      } else if (action === "pipe") {
        if (s.pipes.includes(s.selected))
          return fail("There is already a connection here.");
        if (s.coins < 1) return fail("A pipe costs one coin.");
        s.pipes.push(s.selected);
        s.coins--;
        message = `Pipe installed. ${served(s)} homes currently supplied.`;
        kind = "build";
      } else if (action === "pump") {
        if (s.capacity >= 3)
          return fail("The pump already supports all three homes.");
        if (s.coins < 4) return fail("Pump expansion costs four coins.");
        s.coins -= 4;
        s.capacity++;
        message = `Pump capacity is now ${s.capacity} homes.`;
        kind = "upgrade";
      } else if (action === "day") {
        const n = served(s);
        s.days++;
        s.turn++;
        s.coins += n * 2;
        s.influence -= 3 - n;
        message = `Day report: ${n}/3 homes supplied; ${3 - n} influence lost.`;
        if (s.influence <= 0) {
          s.status = "lost";
          message =
            "Unserved homes lost confidence. Try connecting a shorter route before ending the day.";
        } else if (n === 3 && s.days >= 2) {
          s.status = "won";
          message =
            "The whole district is supplied. Two days of service completed.";
        }
      } else return fail("Choose a pipe, pump expansion or next day.");
      break;
    }
    case "harvest": {
      if (action === "gather") {
        if (s.cooldown > 0)
          return fail("The net is returning. Wait for its short recovery.");
        const n = 2 * s.vessel;
        s.cargo += n;
        s.total += n;
        s.cooldown = 0.22;
        message = `${n} sea seeds brought aboard.`;
        kind = "harvest";
      } else if (action === "net") {
        const cost = 5 + s.nets * 4;
        if (s.cargo < cost)
          return fail(`An automatic net costs ${cost} cargo.`);
        s.cargo -= cost;
        s.nets++;
        message =
          "An automatic net is working. The next catch no longer needs a click.";
        kind = "upgrade";
      } else if (action === "vessel") {
        const cost = 12 * s.vessel;
        if (s.cargo < cost)
          return fail(`The vessel upgrade costs ${cost} cargo.`);
        s.cargo -= cost;
        s.vessel++;
        message =
          "A stronger vessel doubles the first net’s capacity and improves every catch.";
        kind = "upgrade";
      } else return fail("Gather or improve the equipment.");
      if (s.total >= 60) {
        s.status = "won";
        message = "Sixty sea seeds harvested. The short voyage is complete.";
      }
      break;
    }
    case "flock": {
      if (action === "feed") {
        if (s.coins < 2) return fail("A feed sack costs two coins.");
        s.coins -= 2;
        s.feed += 5;
        message = "Feed scattered. The flock can keep producing.";
        kind = "care";
      } else if (action === "collect") {
        const n = Math.floor(s.eggs);
        if (!n)
          return fail(
            "The nests are not ready yet. Fed hens produce eggs over time.",
          );
        s.eggs -= n;
        s.coins += n * 2;
        s.sold += n;
        message = `${n} eggs collected and sold. The nests are clear.`;
        kind = "harvest";
      } else if (action === "hen") {
        if (s.coins < 8) return fail("A new hen costs eight coins.");
        if (s.hens >= 8)
          return fail("This little coop has room for eight hens.");
        s.coins -= 8;
        s.hens++;
        message = "A new hen joins the flock. More hens also use more feed.";
        kind = "upgrade";
      } else return fail("Feed, collect or add a hen.");
      if (s.hens >= 4 && s.sold >= 10) {
        s.status = "won";
        message = "A flourishing coop: four hens and ten eggs sold.";
      }
      break;
    }
    case "factory": {
      if (action === "crank") {
        if (s.ore < 1) return fail("Order more ore first.");
        if (s.parts >= 8)
          return fail(
            "The output buffer is full. Sell parts before pressing more.",
          );
        s.ore--;
        s.parts++;
        message = "One copper part pressed into the output buffer.";
        kind = "work";
      } else if (action === "sell") {
        if (s.parts < 1) return fail("There are no parts to ship.");
        const n = s.parts;
        s.coins += n * 2;
        s.sold += n;
        s.parts = 0;
        message = `${n} parts shipped; the buffer is free.`;
        kind = "delivery";
      } else if (action === "ore") {
        if (s.coins < 3) return fail("A supply crate costs three coins.");
        s.coins -= 3;
        s.ore += 6;
        message = "Six ore units arrived.";
        kind = "delivery";
      } else if (action === "machine") {
        const cost = 8 + s.machines * 4;
        if (s.coins < cost)
          return fail(`Another automatic press costs ${cost} coins.`);
        s.coins -= cost;
        s.machines++;
        message =
          "A press now works automatically when ore and buffer space are available.";
        kind = "upgrade";
      } else return fail("Use the press, supply ore, ship parts or automate.");
      if (s.machines >= 2 && s.sold >= 16) {
        s.status = "won";
        message =
          "Two presses and sixteen parts shipped. A complete little production line.";
      }
      break;
    }
    case "garden": {
      if (action === "water") {
        s.water = clamp(s.water + 0.4, 0, 1);
        message = "Soil watered. Growth continues while moisture remains.";
        kind = "care";
      } else if (action === "sell") {
        const i = s.plants.findIndex((g) => g >= 1);
        if (i < 0)
          return fail("Let a plant mature before taking a cutting to market.");
        s.plants[i] = 0.1;
        s.sold++;
        s.coins += 6;
        message =
          "A mature cutting sold; its parent begins a fresh growth cycle.";
        kind = "harvest";
      } else if (action === "pot") {
        if (s.coins < 4) return fail("A pot costs four coins.");
        if (s.plants.length >= 7)
          return fail("The sunny shelf has room for seven pots.");
        s.coins -= 4;
        s.plants.push(0.1);
        message = "Another pot joins the shelf.";
        kind = "build";
      } else return fail("Water, sell a mature cutting or add a pot.");
      break;
    }
    case "evolution": {
      if (action !== "trial") return fail("Run the next generation.");
      if (s.running) return fail("The current trial is still running.");
      if (s.trial > 0) {
        const best = s.creatures.reduce((a, b) =>
          a.distance > b.distance ? a : b,
        );
        s.previousBest = s.best;
        s.creatures = [
          { ...best, distance: 0 },
          ...Array.from({ length: 5 }, () => genome(s, best)),
        ];
        s.generation++;
      }
      s.running = true;
      s.trial = 0;
      message = `Generation ${s.generation}: comparing six gait parameters.`;
      kind = "trial";
      break;
    }
    case "orb": {
      if (action === "cast") {
        if (s.charge < 1)
          return fail("Casting needs one charge. It regenerates over time.");
        if (s.light >= s.capacity)
          return fail("Light storage is full. Transmute or expand first.");
        s.charge--;
        s.light = Math.min(s.capacity, s.light + 2 * s.power);
        message = "Charge converted into stored light.";
        kind = "spell";
      } else if (action === "transmute") {
        if (s.light < 3) return fail("Transmutation needs three light.");
        s.light -= 3;
        s.wood++;
        message = "Three light became one piece of living material.";
        kind = "spell";
      } else if (action === "expand") {
        if (s.light < 4 || s.wood < 1)
          return fail("Expansion needs four light and one material.");
        s.light -= 4;
        s.wood--;
        s.capacity += 3;
        message = `The light vessel now holds ${s.capacity}.`;
        kind = "upgrade";
      } else if (action === "awaken") {
        if (s.light < 8 || s.wood < 3)
          return fail(
            "Awakening needs eight light and three material. Increase storage first.",
          );
        s.light -= 8;
        s.wood -= 3;
        s.status = "won";
        message = "A little world awakens inside the apparatus.";
        kind = "spell";
      } else return fail("Choose a conversion or capacity upgrade.");
      break;
    }
    case "hamlet": {
      if (action === "select") {
        if (!Number.isInteger(value) || value < 0 || value >= 35)
          return fail("Choose an island cell.");
        s.selected = value;
        message = `Plot ${value + 1} selected.`;
        kind = "select";
      } else if (action === "tool") {
        if (!["house", "tree", "workshop"].includes(value))
          return fail("Choose a building or tree.");
        s.tool = value;
        message = `Placing: ${value}.`;
        kind = "select";
      } else if (action === "place") {
        if (s.tiles.some((t) => t.cell === s.selected))
          return fail(
            "This plot is already occupied. Undo the last placement or choose another.",
          );
        const cost = { house: 3, tree: 1, workshop: 5 }[s.tool];
        if (s.coins < cost)
          return fail(
            `This placement needs ${cost} coins. Undo returns its full cost.`,
          );
        s.undo.push({ cell: s.selected, cost });
        s.tiles.push({ cell: s.selected, type: s.tool });
        s.coins -= cost;
        message = "Placed. Watch residents stroll through your arrangement.";
        kind = "build";
      } else if (action === "undo") {
        const last = s.undo.pop();
        if (!last) return fail("There is no placement to undo.");
        s.tiles = s.tiles.filter((t) => t.cell !== last.cell);
        s.coins += last.cost;
        message = "The last placement was undone and its coins returned.";
        kind = "build";
      } else return fail("Choose a plot and place an object.");
      break;
    }
  }
  s.events++;
  return { state: s, accepted: true, message, kind };
}

export function advance(state, seconds) {
  if (!Number.isFinite(seconds) || seconds < 0 || seconds > 1)
    throw new Error(
      "Advance by a finite interval from zero through one second.",
    );
  if (
    !seconds ||
    state.status !== "playing" ||
    ["council", "post", "water", "hamlet"].includes(state.id)
  )
    return state;
  const s = clone(state);
  s.time += seconds;
  switch (s.id) {
    case "harvest": {
      s.cooldown = Math.max(0, s.cooldown - seconds);
      const n = s.nets * s.vessel * seconds;
      s.cargo += n;
      s.total += n;
      if (s.total >= 60) s.status = "won";
      break;
    }
    case "flock": {
      const used = Math.min(s.feed, s.hens * 0.06 * seconds);
      s.feed -= used;
      s.eggs = Math.min(s.hens * 3, s.eggs + (used / 0.06) * 0.16);
      break;
    }
    case "factory": {
      if (s.machines && s.ore && s.parts < 8) {
        s.work += s.machines * seconds;
        const n = Math.min(Math.floor(s.work), s.ore, 8 - s.parts);
        s.work -= n;
        s.ore -= n;
        s.parts += n;
        if (s.parts === 8 || s.ore === 0) s.work = 0;
      }
      break;
    }
    case "garden": {
      const wet = Math.min(seconds, s.water / 0.012);
      s.water = Math.max(0, s.water - seconds * 0.012);
      s.plants = s.plants.map((g) => Math.min(1, g + wet * 0.035));
      break;
    }
    case "evolution": {
      if (s.running) {
        const dt = Math.min(seconds, 6 - s.trial);
        s.trial += dt;
        s.creatures.forEach((g) => (g.distance += speed(g) * dt));
        if (s.trial >= 6) {
          s.running = false;
          s.best = Math.max(s.best, ...s.creatures.map((g) => g.distance));
        }
      }
      break;
    }
    case "orb":
      s.charge = Math.min(5, s.charge + seconds * 0.7);
      break;
  }
  return s;
}

// Saves are local snapshots of this toy model. They are not CRS saves or replay
// proofs. A declared version, shape and bounded values prevent broken files
// from replacing a live prototype; no network or credentials are involved.
export function encode(state) {
  return JSON.stringify(state);
}
export function decode(text) {
  const s = JSON.parse(text),
    base = createState(s?.id, s?.seed);
  if (s.version !== VERSION || !["playing", "won", "lost"].includes(s.status))
    throw new Error("Unsupported prototype save.");
  function shape(value, example) {
    if (typeof example === "number")
      return (
        typeof value === "number" &&
        Number.isFinite(value) &&
        value >= 0 &&
        value <= Number.MAX_SAFE_INTEGER
      );
    if (typeof example === "boolean") return typeof value === "boolean";
    if (typeof example === "string") return typeof value === "string";
    if (Array.isArray(example))
      return (
        Array.isArray(value) &&
        value.length <= 1000 &&
        value.every((v) =>
          example.length ? shape(v, example[0]) : v && typeof v === "object",
        )
      );
    return (
      value &&
      typeof value === "object" &&
      Object.keys(example).every((k) => shape(value[k], example[k]))
    );
  }
  if (!shape(s, base))
    throw new Error("The prototype save has invalid fields.");
  if (s.rng > 0xffffffff) throw new Error("Invalid random generator state.");
  for (const field of [
    "seed",
    "rng",
    "turn",
    "events",
    "coins",
    "people",
    "trust",
    "delivered",
    "days",
    "nets",
    "vessel",
    "hens",
    "sold",
    "machines",
    "parts",
    "ore",
    "generation",
    "wood",
    "capacity",
    "power",
  ]) {
    if (field in s && !Number.isInteger(s[field]))
      throw new Error(`Invalid integer field: ${field}.`);
  }
  if (
    s.id === "council" &&
    (!Number.isInteger(s.turn) ||
      s.turn > requests.length ||
      (s.status === "playing" && s.turn === requests.length))
  )
    throw new Error("Invalid council turn.");
  if (
    s.id === "post" &&
    (!["harbor", "forest"].includes(s.parcel.route) ||
      ![1, 2, 3].includes(s.parcel.weight) ||
      ![0, 1, 2, 3].includes(s.parcel.stamp))
  )
    throw new Error("Invalid parcel.");
  if (
    s.id === "water" &&
    (!s.pipes.every((n) => Number.isInteger(n) && n >= 0 && n < 24) ||
      !s.pipes.includes(6) ||
      !Number.isInteger(s.selected) ||
      s.selected < 0 ||
      s.selected >= 24 ||
      ![1, 2, 3].includes(s.capacity))
  )
    throw new Error("Invalid water network.");
  if (
    s.id === "hamlet" &&
    (!["house", "tree", "workshop"].includes(s.tool) ||
      !s.tiles.every(
        (t) =>
          t &&
          Number.isInteger(t.cell) &&
          t.cell >= 0 &&
          t.cell < 35 &&
          ["house", "tree", "workshop"].includes(t.type),
      ) ||
      new Set(s.tiles.map((t) => t.cell)).size !== s.tiles.length ||
      s.undo.length !== s.tiles.length ||
      !s.undo.every(
        (t, i) =>
          t &&
          t.cell === s.tiles[i].cell &&
          t.cost === { house: 3, tree: 1, workshop: 5 }[s.tiles[i].type],
      ) ||
      !Number.isInteger(s.selected) ||
      s.selected >= 35)
  )
    throw new Error("Invalid hamlet.");
  if (
    s.id === "evolution" &&
    (s.creatures.length !== 6 ||
      s.trial > 6 ||
      s.creatures.some(
        (g) => g.stride < 0.1 || g.stride > 1 || g.rhythm < 0.1 || g.rhythm > 1,
      ))
  )
    throw new Error("Invalid trial.");
  if (
    s.id === "garden" &&
    (s.water > 1 ||
      s.plants.length < 1 ||
      s.plants.length > 7 ||
      s.plants.some((g) => g > 1))
  )
    throw new Error("Invalid garden.");
  if (
    s.id === "flock" &&
    (!Number.isInteger(s.hens) || s.hens < 1 || s.hens > 8)
  )
    throw new Error("Invalid flock.");
  if (
    s.id === "harvest" &&
    (s.nets > 16 || s.vessel < 1 || s.vessel > 16 || s.cooldown > 1)
  )
    throw new Error("Invalid harvesting equipment.");
  if (s.id === "factory" && (s.parts > 8 || s.machines > 16))
    throw new Error("Invalid factory buffer.");
  return s;
}
