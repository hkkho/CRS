import pack from "../../../data/packs/candu6-two-group-diffusion-pack-v1.json";
import type { CustomPhysics } from "./customPhysics";

const format = "crs-physics-profile";
const version = 1;
export const maxProfileBytes = 64 * 1024;

type Parameters = { [key: string]: number | Parameters };

function profilePath(path: string): string[] {
  return path.replace(/^coefficient_tables\[0\]\.rows/, "fuel_rows")
    .replace(/^xenon_reference\.rows/, "xenon_rows")
    .replace(/\[(\d+)\]/g, ".$1").split(".");
}

export function makePhysicsProfile(overrides: CustomPhysics) {
  const parameters: Parameters = Object.create(null) as Parameters;
  for (const [path, value] of Object.entries(overrides).sort(([a], [b]) => a < b ? -1 : a > b ? 1 : 0)) {
    const parts = profilePath(path);
    let branch = parameters;
    for (const part of parts.slice(0, -1)) {
      if (!Object.hasOwn(branch, part)) branch[part] = Object.create(null) as Parameters;
      branch = branch[part] as Parameters;
    }
    branch[parts.at(-1)!] = value;
  }
  return { format, version, model: pack.model_id, basePack: pack.data_pack_version,
    units: pack.units_profile_id, parameters };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return !!value && typeof value === "object" && !Array.isArray(value);
}

// JSON.parse resolves repeated keys before a reviver sees them. Check names within
// each object after syntax validation so edits can never be silently discarded.
function rejectDuplicateKeys(source: string): void {
  const scopes: Set<string>[] = [];
  for (const token of source.matchAll(/("(?:[^"\\]|\\.)*")(\s*:)?|[{}]/gs)) {
    if (token[0] === "{") scopes.push(new Set());
    else if (token[0] === "}") scopes.pop();
    else if (token[2]) {
      const key: string = JSON.parse(token[1]);
      const scope = scopes.at(-1)!;
      if (scope.has(key)) throw new Error(`Duplicate JSON property “${key}”.`);
      scope.add(key);
    }
  }
}

export function readPhysicsProfile(source: string, knownPaths: Iterable<string>): CustomPhysics {
  const expected = makePhysicsProfile({});
  let profile: unknown;
  try { profile = JSON.parse(source); }
  catch { throw new Error("Open a valid JSON physics profile."); }
  if (!isRecord(profile) || profile.format !== format) throw new Error("Choose a CRS physics profile JSON file.");
  rejectDuplicateKeys(source);
  if (profile.version !== version) throw new Error("This physics profile version is unsupported.");
  if (profile.model !== expected.model || profile.units !== expected.units) {
    throw new Error("This profile uses a different model or units.");
  }
  if (typeof profile.basePack !== "string") throw new Error("The profile needs a base pack revision.");
  if (profile.basePack !== expected.basePack) {
    throw new Error(`This profile requires base pack “${profile.basePack}”. This build uses “${expected.basePack}”.`);
  }
  if (Object.keys(profile).some(key => !Object.hasOwn(expected, key)) || !isRecord(profile.parameters)) {
    throw new Error("The profile header or parameters object is invalid.");
  }

  const allowed = new Map<string, string>();
  const branches = new Set<string>([""]);
  for (const path of knownPaths) {
    const parts = profilePath(path);
    allowed.set(parts.join("."), path);
    for (let i = 1; i < parts.length; i++) branches.add(parts.slice(0, i).join("."));
  }
  const overrides: Record<string, number> = {};
  function visit(node: Record<string, unknown>, parent: string): void {
    for (const [key, value] of Object.entries(node)) {
      const path = parent ? `${parent}.${key}` : key;
      if (!/^([a-z][a-z0-9_]*|0|[1-9]\d*)$/.test(key)) throw new Error(`Invalid parameter name “${path}”.`);
      if (isRecord(value) && branches.has(path) && Object.keys(value).length > 0) {
        visit(value, path);
        continue;
      }
      const field = allowed.get(path);
      if (!field) throw new Error(`Unsupported parameter “${path}”.`);
      if (typeof value !== "number" || !Number.isFinite(value) || value < 0 || Object.is(value, -0)) {
        throw new Error(`Enter a finite nonnegative number for “${path}”.`);
      }
      overrides[field] = value;
    }
  }
  visit(profile.parameters, "");
  return overrides;
}
