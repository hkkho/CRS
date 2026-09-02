import {
  canonicalJson,
  parseReplayArchive,
  PROTOCOL_VERSION,
  serializeReplayArchive,
  type CanduPlaytestBridge,
  type CanduReplayArchive,
  type ProtocolSource,
  type ReplayCommandRecord,
} from "./protocol";
import type { UiCommandHistoryEntry } from "./reducer";

export const REPLAY_STORAGE_KEY = "candu-playtest-v1:replay";
export const NOTE_STORAGE_KEY = "candu-playtest-v1:feedback-note";

export function createReplayArchive(
  history: readonly UiCommandHistoryEntry[],
  source: ProtocolSource,
  createdAt = new Date().toISOString(),
): CanduReplayArchive {
  const commands: ReplayCommandRecord[] = history.map(({ recordedAt: _recordedAt, ...record }) => record);
  return {
    protocol: PROTOCOL_VERSION,
    kind: "command-replay",
    source,
    createdAt,
    commands,
  };
}

export function saveReplayArchive(archive: CanduReplayArchive, storage: Storage | null = getStorage()): boolean {
  if (storage === null) {
    return false;
  }
  storage.setItem(REPLAY_STORAGE_KEY, serializeReplayArchive(archive));
  return true;
}

export function loadReplayArchive(storage: Storage | null = getStorage()): CanduReplayArchive | null {
  if (storage === null) {
    return null;
  }
  const raw = storage.getItem(REPLAY_STORAGE_KEY);
  return raw === null ? null : parseReplayArchive(raw);
}

export function serializeReplayForDownload(archive: CanduReplayArchive): string {
  return `${serializeReplayArchive(archive)}\n`;
}

export function downloadReplayArchive(archive: CanduReplayArchive): void {
  if (typeof document === "undefined") {
    return;
  }
  const blob = new Blob([serializeReplayForDownload(archive)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = `candu-playtest-${archive.createdAt.replace(/[:.]/g, "-")}.json`;
  link.click();
  URL.revokeObjectURL(url);
}

export async function replayArchive(
  bridge: CanduPlaytestBridge,
  archive: CanduReplayArchive,
  onResponse?: (record: ReplayCommandRecord, response: Awaited<ReturnType<CanduPlaytestBridge["dispatch"]>>) => void,
): Promise<number> {
  let replayed = 0;
  for (const record of archive.commands) {
    const response = await bridge.dispatch(record.command);
    replayed += 1;
    onResponse?.(record, response);
  }
  return replayed;
}

export function saveNote(note: string, storage: Storage | null = getStorage()): void {
  storage?.setItem(NOTE_STORAGE_KEY, note);
}

export function loadNote(storage: Storage | null = getStorage()): string {
  return storage?.getItem(NOTE_STORAGE_KEY) ?? "";
}

export function stableDigest(snapshot: unknown): string {
  const serialized = canonicalJson(snapshot);
  let hash = 2166136261;
  for (let index = 0; index < serialized.length; index += 1) {
    hash ^= serialized.charCodeAt(index);
    hash = Math.imul(hash, 16777619);
  }
  return (hash >>> 0).toString(16).padStart(8, "0").toUpperCase();
}

function getStorage(): Storage | null {
  if (typeof window === "undefined" || typeof window.localStorage === "undefined") {
    return null;
  }
  return window.localStorage;
}
