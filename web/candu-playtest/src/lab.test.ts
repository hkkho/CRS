import { describe, expect, it } from "vitest";
import {
  defaultReflectiveFaces,
  effectiveReflectiveFaces,
  fixedReflectiveFaces,
  isFixedExteriorFace,
  labCellAt,
  labFluxAt,
  sortReflectiveFaces,
  toggleReflectiveFace,
} from "./lab";
import type { CanduLabSnapshot } from "./protocol";

function createLab(): CanduLabSnapshot {
  return {
    fixtureId: "lab-2x8-synthetic-v1",
    simulationTimeSeconds: 0,
    freshBundlesAvailable: 32,
    refuellingOperationCount: 0,
    lastRefuelledChannel: -1,
    lastRefuellingDirectionId: null,
    lastRefuellingShiftCount: 0,
    core: {
      fixtureId: "lab-2x8-synthetic-v1",
      channelCount: 2,
      bundlePositionCount: 8,
      channels: [
        { channelIndex: 0, coordinateX: 0, coordinateY: 0, flowDirection: "EndAtoEndB", bundles: [] },
        { channelIndex: 1, coordinateX: 1, coordinateY: 0, flowDirection: "EndBtoEndA", bundles: [] },
      ],
      cells: [
        { channelIndex: 0, position: 0, hasFuel: true, materialId: "LAB-FUEL-SYNTHETIC", reflectiveFaces: ["south", "north"] },
        { channelIndex: 0, position: 1, hasFuel: false, materialId: "moderator", reflectiveFaces: ["west"] },
      ],
    },
    spatialSolve: {
      status: "converged",
      isConverged: true,
      hasUsableState: true,
      finalState: {
        iteration: 3,
        eigenvalue: 1.0123,
        totalPowerW: 0.4,
        group1Flux: Array.from({ length: 16 }, (_, index) => index + 1),
        group2Flux: Array.from({ length: 16 }, (_, index) => (index + 1) * 2),
      },
      diagnostics: {
        iterationCount: 3,
        residualRelativeInfinity: 1e-13,
        sourceShapeChangeInfinity: 1e-13,
        powerBalanceRelative: 1e-13,
        convergenceReason: "converged",
        innerSolveStatus: "converged",
      },
    },
  };
}

describe("Lab topology helpers", () => {
  it("prefers the canonical flat cell projection and maps flux in channel-major order", () => {
    const lab = createLab();
    expect(labCellAt(lab, 0, 1)).toMatchObject({
      channelIndex: 0,
      position: 1,
      hasFuel: false,
      reflectiveFaces: ["west"],
    });
    expect(labFluxAt(lab, 1, 0)).toMatchObject({
      group1: 9,
      group2: 18,
      total: 27,
    });
  });

  it("uses the bundle projection for older hosts without cells", () => {
    const lab = createLab();
    lab.core.cells = undefined;
    lab.core.channels[0].bundles = [{
      position: 0,
      bundleId: "bundle",
      fuelTypeId: "LAB-FUEL-SYNTHETIC",
      currentBurnupMwDayPerKg: 0,
      currentBurnupJPerKgHm: 0,
      insertedAtSeconds: 0,
      stateVersion: 0,
    }];
    expect(labCellAt(lab, 0, 0).hasFuel).toBe(true);
    expect(labCellAt(lab, 0, 1).hasFuel).toBe(false);
  });

  it("keeps reflective face controls deterministic", () => {
    expect(defaultReflectiveFaces(0, 0)).toEqual(["east", "end-b"]);
    expect(fixedReflectiveFaces(0, 0)).toEqual(["north", "south", "west", "end-a"]);
    expect(isFixedExteriorFace(0, 0, "north")).toBe(true);
    expect(isFixedExteriorFace(0, 0, "east")).toBe(false);
    expect(effectiveReflectiveFaces({ channelIndex: 0, position: 0, hasFuel: false, reflectiveFaces: ["east"] }))
      .toEqual(["north", "east", "south", "west", "end-a"]);
    expect(sortReflectiveFaces(["end-b", "north", "north", "east"])).toEqual(["north", "east", "end-b"]);
    expect(toggleReflectiveFace(["north"], "east")).toEqual(["north", "east"]);
    expect(toggleReflectiveFace(["north", "east"], "north")).toEqual(["east"]);
  });
});
