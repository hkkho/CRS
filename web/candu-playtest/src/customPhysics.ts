export type CustomPhysics = Readonly<Record<string, number>>;

export const CUSTOM_PHYSICS_PACK_PREFIX = "custom-practice-v1-";

export function isCustomPhysics(value: unknown): value is CustomPhysics {
  if (!value || typeof value !== "object" || Array.isArray(value)) return false;
  const fields = Object.entries(value);
  return fields.length <= 512 && fields.every(([key, number]) =>
    key.length <= 100 && Number.isFinite(number) && number >= 0);
}
