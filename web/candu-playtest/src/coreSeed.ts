/** Randomness belongs at run creation; the shared simulation stays seeded and reproducible. */
export function randomCoreSeed(previous?: number): number {
  const value = new Uint32Array(1);
  do { globalThis.crypto.getRandomValues(value); } while (value[0] === previous);
  return value[0]!;
}
