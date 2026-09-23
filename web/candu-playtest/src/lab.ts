import type {
  CanduLabCellSnapshot,
  CanduLabSnapshot,
  LabBoundaryFace,
} from "./protocol";

export const LAB_CHANNEL_COUNT = 2 as const;
export const LAB_POSITION_COUNT = 8 as const;

export interface LabCellState {
  channelIndex: number;
  position: number;
  hasFuel: boolean;
  materialId?: string;
  reflectiveFaces: LabBoundaryFace[];
}

export interface LabFluxFeedback {
  group1: number;
  group2: number;
  total: number;
  normalized: number;
}

const FACE_ORDER: readonly LabBoundaryFace[] = [
  "north",
  "east",
  "south",
  "west",
  "end-a",
  "end-b",
];

export function labCellKey(channelIndex: number, position: number): string {
  return `${channelIndex}:${position}`;
}

export function labCells(lab: CanduLabSnapshot): LabCellState[] {
  const cells: LabCellState[] = [];
  for (let channelIndex = 0; channelIndex < lab.core.channelCount; channelIndex += 1) {
    for (let position = 0; position < lab.core.bundlePositionCount; position += 1) {
      cells.push(labCellAt(lab, channelIndex, position));
    }
  }
  return cells;
}

export function labCellAt(
  lab: CanduLabSnapshot,
  channelIndex: number,
  position: number,
): LabCellState {
  const flat = lab.core.cells?.find((candidate) =>
    candidate.channelIndex === channelIndex && candidate.position === position);
  const channel = lab.core.channels.find((candidate) => candidate.channelIndex === channelIndex);
  const nested = channel?.cells?.find((candidate) => candidate.position === position);
  const candidate = flat ?? nested;
  if (candidate !== undefined) {
    return {
      channelIndex,
      position,
      hasFuel: candidate.hasFuel,
      ...(candidate.materialId === undefined ? {} : { materialId: candidate.materialId }),
      reflectiveFaces: sortReflectiveFaces(candidate.reflectiveFaces),
    };
  }

  // Older hosts did not expose cells. Bundles are a display-only compatibility
  // projection; authoritative Lab hosts always provide the flat cell list.
  const bundle = channel?.bundles.find((candidateBundle) => candidateBundle.position === position);
  return {
    channelIndex,
    position,
    hasFuel: bundle !== undefined,
    ...(bundle === undefined ? {} : { materialId: bundle.fuelTypeId }),
    reflectiveFaces: [],
  };
}

export function labFluxAt(
  lab: CanduLabSnapshot,
  channelIndex: number,
  position: number,
): LabFluxFeedback | null {
  const state = lab.spatialSolve.finalState;
  if (state === null) {
    return null;
  }
  const index = channelIndex * lab.core.bundlePositionCount + position;
  const group1 = state.group1Flux[index] ?? 0;
  const group2 = state.group2Flux[index] ?? 0;
  const total = group1 + group2;
  const allFlux = [...state.group1Flux, ...state.group2Flux];
  const maximum = Math.max(1e-12, ...allFlux.filter((value) => Number.isFinite(value)));
  return {
    group1,
    group2,
    total,
    normalized: total / maximum,
  };
}

export function labMaximumFlux(lab: CanduLabSnapshot): number {
  const state = lab.spatialSolve.finalState;
  if (state === null) {
    return 0;
  }
  return Math.max(0, ...state.group1Flux, ...state.group2Flux);
}

export function sortReflectiveFaces(faces: readonly LabBoundaryFace[]): LabBoundaryFace[] {
  return [...new Set(faces)].sort((left, right) => FACE_ORDER.indexOf(left) - FACE_ORDER.indexOf(right));
}

export function toggleReflectiveFace(
  faces: readonly LabBoundaryFace[],
  face: LabBoundaryFace,
): LabBoundaryFace[] {
  return sortReflectiveFaces(faces.includes(face)
    ? faces.filter((candidate) => candidate !== face)
    : [...faces, face]);
}

export function defaultReflectiveFaces(channelIndex: number, position: number): LabBoundaryFace[] {
  // The outer north/south wall, the outside transverse wall, and the two
  // axial end caps are authoritative fixed reflectors. The command only
  // needs to carry the interior faces that the operator can configure.
  const faces: LabBoundaryFace[] = [channelIndex === 0 ? "east" : "west"];
  if (position > 0) faces.push("end-a");
  if (position < LAB_POSITION_COUNT - 1) faces.push("end-b");
  return sortReflectiveFaces(faces);
}

export function isFixedExteriorFace(
  channelIndex: number,
  position: number,
  face: LabBoundaryFace,
): boolean {
  return face === "north" || face === "south" ||
    (face === "west" && channelIndex === 0) ||
    (face === "east" && channelIndex === 1) ||
    (face === "end-a" && position === 0) ||
    (face === "end-b" && position === LAB_POSITION_COUNT - 1);
}

export function fixedReflectiveFaces(channelIndex: number, position: number): LabBoundaryFace[] {
  return FACE_ORDER.filter((face) => isFixedExteriorFace(channelIndex, position, face));
}

export function effectiveReflectiveFaces(cell: LabCellState): LabBoundaryFace[] {
  return sortReflectiveFaces([
    ...fixedReflectiveFaces(cell.channelIndex, cell.position),
    ...cell.reflectiveFaces,
  ]);
}

export function formatLabFlux(value: number): string {
  if (!Number.isFinite(value)) {
    return "—";
  }
  return value.toExponential(2);
}

export function isLabSnapshotReady(lab: CanduLabSnapshot | undefined): lab is CanduLabSnapshot {
  return lab !== undefined &&
    lab.core.channelCount === LAB_CHANNEL_COUNT &&
    lab.core.bundlePositionCount === LAB_POSITION_COUNT &&
    lab.core.cells !== undefined &&
    lab.core.cells.length === LAB_CHANNEL_COUNT * LAB_POSITION_COUNT &&
    lab.spatialSolve.finalState !== null;
}

export function cloneLabCell(cell: CanduLabCellSnapshot, channelIndex: number): LabCellState {
  return {
    channelIndex,
    position: cell.position,
    hasFuel: cell.hasFuel,
    ...(cell.materialId === undefined ? {} : { materialId: cell.materialId }),
    reflectiveFaces: sortReflectiveFaces(cell.reflectiveFaces),
  };
}
