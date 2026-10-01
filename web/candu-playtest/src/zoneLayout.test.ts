import { describe, expect, it } from "vitest";
import type { CanduSnapshot, ZoneNodeBinding } from "./protocol";
import { moveAbsorberMask } from "./zoneLayout";

const snapshot = { core: { channels: [
  { channelIndex: 0, gridColumn: 10, gridRow: 10 },
  { channelIndex: 1, gridColumn: 11, gridRow: 10 },
] } } as CanduSnapshot;
function nodes(): ZoneNodeBinding[] {
  return [0, 1].flatMap(channelIndex => [0, 1].map(position => ({ channelIndex, position,
    logicalZoneId: channelIndex + 1, absorberZoneId: 0,
    group1AbsorptionPerMPerFillFraction: channelIndex === 0 && position === 0 ? 0.02 : 0,
    group2AbsorptionPerMPerFillFraction: channelIndex === 0 && position === 0 ? 0.008 : 0 })));
}
describe("zone geometry draft editing", () => {
  it("moves absorber slopes and owner while preserving regions and source draft", () => {
    const original = nodes();
    const moved = moveAbsorberMask(original, snapshot, 0, 1, 0, 1);
    expect(moved[0]!.group1AbsorptionPerMPerFillFraction).toBe(0);
    expect(moved[3]!).toMatchObject({ logicalZoneId: 2, absorberZoneId: 0, group1AbsorptionPerMPerFillFraction: 0.02 });
    expect(original[0]!.group1AbsorptionPerMPerFillFraction).toBe(0.02);
  });
  it("rejects off-core and overlapping moves without mutating the draft", () => {
    const original = nodes();
    expect(() => moveAbsorberMask(original, snapshot, 0, -1, 0, 0)).toThrow("outside");
    original[2]!.absorberZoneId = 1; original[2]!.group1AbsorptionPerMPerFillFraction = 0.02;
    expect(() => moveAbsorberMask(original, snapshot, 0, 1, 0, 0)).toThrow("overlaps");
    expect(original[0]!.group1AbsorptionPerMPerFillFraction).toBe(0.02);
  });
});
