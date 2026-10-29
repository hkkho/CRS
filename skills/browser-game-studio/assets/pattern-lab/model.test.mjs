import test from "node:test";
import assert from "node:assert/strict";
import {
  IDS,
  createState,
  command,
  advance,
  encode,
  decode,
  connected,
  served,
} from "./model.mjs";

const act = (s, a, v) => {
  const r = command(s, a, v);
  assert.ok(r.accepted, r.message);
  return r.state;
};
const tick = (s, seconds) => {
  for (let n = 0; n < seconds; n++) s = advance(s, Math.min(1, seconds - n));
  return s;
};

test("every pattern starts with a valid reproducible local snapshot", () => {
  for (const id of IDS) {
    const s = createState(id, 987);
    assert.deepEqual(decode(encode(s)), s);
    assert.deepEqual(createState(id, 987), s);
  }
});
test("rejected commands preserve identity and all live values", () => {
  for (const [id, action] of [
    ["council", "unknown"],
    ["post", "ship"],
    ["water", "pump"],
    ["flock", "hen"],
    ["factory", "machine"],
  ]) {
    const s = createState(id);
    if ("coins" in s) s.coins = 0;
    const before = encode(s),
      r = command(s, action, "wrong");
    assert.equal(r.accepted, false);
    assert.equal(r.state, s);
    assert.equal(encode(s), before);
  }
});
test("council requires a growth plan; a viable mixed policy can finish", () => {
  let s = createState("council");
  for (let i = 0; i < 8; i++) s = act(s, "no");
  assert.equal(s.status, "lost");
  s = createState("council");
  for (let i = 0; i < 8; i++) s = act(s, i === 2 ? "no" : "yes");
  assert.equal(s.status, "won");
  assert.ok(s.people >= 18);
  assert.ok(s.coins >= 0);
  assert.deepEqual(decode(encode(s)), s);
});
test("a promise cannot overdraw the treasury", () => {
  const s = createState("council");
  s.coins = 1;
  const r = command(s, "yes");
  assert.equal(r.accepted, false);
  assert.equal(r.state.turn, 0);
});
test("postage and destination both matter; a rejected shipment consumes nothing", () => {
  let s = createState("post");
  const route = s.parcel.route,
    weight = s.parcel.weight;
  assert.equal(command(s, "ship", route).accepted, false);
  s = act(s, "stamp", weight);
  const before = encode(s);
  assert.equal(
    command(s, "ship", route === "harbor" ? "forest" : "harbor").accepted,
    false,
  );
  assert.equal(encode(s), before);
  s = act(s, "ship", route);
  assert.equal(s.coins, 12 + 4 - weight);
  assert.equal(s.delivered, 1);
  while (s.status === "playing") {
    s = act(s, "stamp", s.parcel.weight);
    s = act(s, "ship", s.parcel.route);
  }
  assert.equal(s.delivered, 6);
});
test("water connectivity never wraps between grid rows", () => {
  const s = createState("water");
  s.pipes = [6, 5];
  assert.equal(connected(s).has(5), false);
});
test("water needs both a connected route and adequate capacity", () => {
  let s = createState("water");
  for (const n of [7, 8, 9, 10, 11, 5, 17, 23]) {
    s = act(s, "select", n);
    s = act(s, "pipe");
  }
  assert.equal(served(s), 1);
  s = act(s, "pump");
  s = act(s, "pump");
  assert.equal(served(s), 3);
  s = act(s, "day");
  s = act(s, "day");
  assert.equal(s.status, "won");
  assert.ok(s.coins >= 0);
});
test("unserved days can end a water run", () => {
  let s = createState("water");
  s = act(s, "day");
  s = act(s, "day");
  assert.equal(s.status, "lost");
});
test("manual harvest has a recovery and automation changes production", () => {
  let s = createState("harvest");
  s = act(s, "gather");
  assert.equal(command(s, "gather").accepted, false);
  s = tick(s, 1);
  s = act(s, "gather");
  s = tick(s, 1);
  s = act(s, "gather");
  s = act(s, "net");
  const n = s.cargo;
  s = tick(s, 2);
  assert.equal(s.cargo, n + 2);
  s = tick(s, 60);
  assert.equal(s.status, "won");
});
test("hens consume feed and stop producing when it is gone", () => {
  let s = createState("flock");
  s.feed = 0;
  const dry = tick(s, 3);
  assert.equal(dry.eggs, 0);
  s = act(s, "feed");
  s = tick(s, 10);
  assert.ok(s.feed < 5);
  assert.ok(s.eggs > 0);
  assert.ok(s.eggs <= s.hens * 3);
});
test("factory output is bounded and invalid work does not consume ore", () => {
  let s = createState("factory");
  for (let i = 0; i < 8; i++) s = act(s, "crank");
  const before = encode(s);
  assert.equal(command(s, "crank").accepted, false);
  assert.equal(encode(s), before);
  s = act(s, "sell");
  assert.equal(s.parts, 0);
  assert.equal(s.sold, 8);
  s = act(s, "ore");
  s = act(s, "machine");
  s = tick(s, 20);
  assert.ok(s.parts <= 8);
  assert.ok(s.ore >= 0);
});
test("garden growth is bounded, dry plants wait and cuttings retain the parent", () => {
  let s = createState("garden");
  s.water = 0;
  const before = [...s.plants];
  s = tick(s, 10);
  assert.deepEqual(s.plants, before);
  s = act(s, "water");
  s = tick(s, 30);
  assert.ok(s.plants.some((g) => g === 1));
  s = act(s, "sell");
  assert.equal(s.plants.length, 2);
  assert.ok(s.plants.every((g) => g <= 1));
  assert.equal(s.sold, 1);
});
test("seeded trials replay exactly and elite selection retains the previous best", () => {
  let a = act(createState("evolution", 123), "trial"),
    b = act(createState("evolution", 123), "trial");
  a = tick(a, 6);
  b = tick(b, 6);
  assert.deepEqual(a, b);
  const best = a.best;
  a = act(a, "trial");
  a = tick(a, 6);
  assert.ok(a.best >= best);
  assert.equal(a.generation, 2);
  assert.equal(a.creatures.length, 6);
  assert.deepEqual(decode(encode(a)), a);
});
test("resource conversion requires expanded capacity for the final action", () => {
  let s = createState("orb");
  assert.equal(command(s, "awaken").accepted, false);
  s = act(s, "cast");
  s = act(s, "cast");
  s = act(s, "transmute");
  s = tick(s, 5);
  s = act(s, "cast");
  s = act(s, "cast");
  s = act(s, "expand");
  assert.equal(s.capacity, 8);
  for (let i = 0; i < 3; i++) {
    while (s.light < 3) {
      s = tick(s, 2);
      s = act(s, "cast");
    }
    s = act(s, "transmute");
  }
  while (s.light < 8) {
    s = tick(s, 2);
    s = act(s, "cast");
  }
  s = act(s, "awaken");
  assert.equal(s.status, "won");
});
test("sandbox placement changes the world and undo returns the exact cost", () => {
  const original = createState("hamlet");
  let s = act(original, "place");
  assert.equal(s.tiles.length, 1);
  assert.equal(s.coins, 21);
  assert.equal(command(s, "place").accepted, false);
  s = act(s, "undo");
  assert.deepEqual(s.tiles, []);
  assert.equal(s.coins, original.coins);
});
test("save validation rejects bad units, out-of-range plots and inconsistent undo", () => {
  for (const id of IDS) {
    const s = createState(id);
    s.time = NaN;
    assert.throws(() => decode(encode(s)));
  }
  const h = createState("hamlet");
  h.tiles = [{ cell: 17, type: "house" }];
  h.undo = [{ cell: 17, cost: 5 }];
  assert.throws(() => decode(encode(h)));
  const w = createState("water");
  w.pipes = [6, 24];
  assert.throws(() => decode(encode(w)));
  const e = createState("evolution");
  e.creatures[0].stride = 2;
  assert.throws(() => decode(encode(e)));
});
test("invalid time and seed input is explicit", () => {
  assert.throws(() => advance(createState("garden"), -1));
  assert.throws(() => advance(createState("garden"), Infinity));
  assert.throws(() => createState("post", -1));
  assert.throws(() => createState("unknown"));
});
