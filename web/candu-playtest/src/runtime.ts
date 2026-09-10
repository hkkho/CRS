import type { BridgeSessionController } from "./sessionController";

let activeSession: BridgeSessionController | null = null;

export function setRuntimeSession(session: BridgeSessionController): void {
  activeSession = session;
}

export function getRuntimeSession(): BridgeSessionController {
  if (activeSession === null) {
    throw new Error("The Phaser runtime session has not been registered.");
  }
  return activeSession;
}
