import { describe, expect, it } from "vitest";
import { makePhysicsProfile, readPhysicsProfile } from "./physicsProfile";

const overrides = {
  "practice.thermal_power_multiplier": 0.98,
  "geometry.axial_boundary.cell_length_m": 0.4953000000000001,
  "coefficient_tables[0].rows[1].burnup_j_per_kg_hm": 1.4e10,
  "xenon_reference.rows[1].xe135_number_density_m3": 2e20,
};
const keys = Object.keys(overrides);
const read = (source: string) => readPhysicsProfile(source, keys);

describe("physics profiles", () => {
  it("round-trips the editable sections and the full precision of a value", () => {
    const profile = makePhysicsProfile(overrides);
    expect(profile.parameters.fuel_rows).toEqual({ "1": { burnup_j_per_kg_hm: 1.4e10 } });
    expect(read(JSON.stringify(profile))).toEqual(overrides);
    expect(read(JSON.stringify(makePhysicsProfile({})))).toEqual({});
  });

  it("rejects an incompatible base or malformed shape", () => {
    const profile = makePhysicsProfile({});
    expect(() => read(JSON.stringify({ ...profile, basePack: "unknown" }))).toThrow(/requires base pack/);
    expect(() => read(JSON.stringify({ ...profile, version: 2 }))).toThrow(/version/);
    expect(() => read(JSON.stringify({ ...profile, parameters: { mesh: {} } }))).toThrow(/Unsupported parameter/);
    expect(() => read(JSON.stringify({ ...profile, parameters: { practice: { thermal_power_multiplier: "1" } } }))).toThrow(/finite/);
  });

  it("rejects duplicate JSON keys including escaped names", () => {
    const source = JSON.stringify(makePhysicsProfile({ "practice.thermal_power_multiplier": 0.98 }));
    expect(() => read(source.replace('"thermal_power_multiplier":0.98',
      '"thermal_power_multiplier":0.98,"thermal_power_multiplie\\u0072":1'))).toThrow(/Duplicate/);
  });
});
