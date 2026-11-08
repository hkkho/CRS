// @vitest-environment jsdom
import { afterEach, expect, it, vi } from "vitest";
import { GameUpdateView } from "./GameUpdateView";
import { createSnapshot } from "./testSnapshot";
import type { BridgeSessionController } from "./sessionController";

afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); document.body.replaceChildren(); });

it("shows the actual loaded build, daily pacing and axial boundary model", async () => {
  const snapshot = { ...createSnapshot(), pacingMode: "daily-turn", dataPackId: "candu6-two-group-diffusion-v1-radial-circle-v9" };
  const session = { snapshot, subscribe: (fn: () => void) => { fn(); return () => {}; } } as unknown as BridgeSessionController;
  vi.stubGlobal("fetch", vi.fn(async () => ({ ok: true, json: async () => ({ gitCommitSha: "b0a4c5738c37532fcea29b965c49e00050f65fba" }) })));
  const view = new GameUpdateView(session, vi.fn());
  await view.initialize("/CRS/");
  expect(view.element.textContent).toContain("Build b0a4c57");
  expect(view.element.textContent).toContain("Daily turns");
  expect(view.element.textContent).toContain("Axial incoming current = 0");
  expect(view.element.textContent).toContain("Circular radial boundary");
  expect(view.element.querySelector("button")?.hidden).toBe(true);
  view.destroy();
});

it("keeps the running game and worker when saving before an update fails", async () => {
  const snapshot = { ...createSnapshot(), pacingMode: "daily-turn" };
  const session = { snapshot, isPending: false, dispose: vi.fn(), subscribe: (fn: () => void) => { fn(); return () => {}; } } as unknown as BridgeSessionController;
  const waiting = { postMessage: vi.fn((_message, [reply]: MessagePort[]) => {
    reply.postMessage({ status: "ready" });
    reply.close();
  }) };
  const registration = Object.assign(new EventTarget(), { waiting, installing: null, update: vi.fn(), unregister: vi.fn() });
  const serviceWorker = Object.assign(new EventTarget(), { controller: {}, register: vi.fn(async () => registration), ready: Promise.resolve() });
  const network = { onLine: true, serviceWorker };
  vi.stubGlobal("navigator", network);
  vi.stubGlobal("fetch", vi.fn(async () => ({ ok: true, json: async () => ({ gitCommitSha: "test-build" }) })));
  const reload = vi.fn();
  const view = new GameUpdateView(session, vi.fn(async () => { throw new Error("Local storage is full."); }), reload);
  const dialog = document.querySelector<HTMLDialogElement>(".game-update-progress")!;
  dialog.showModal = () => { dialog.open = true; };
  dialog.close = () => { dialog.open = false; };
  try {
    await view.initialize("/CRS/");
    await vi.waitFor(() => expect(view.element.textContent).toContain("Local storage is full."));
    expect(waiting.postMessage).toHaveBeenCalledOnce();
    expect(registration.unregister).not.toHaveBeenCalled();
    expect(session.dispose).not.toHaveBeenCalled();
    expect(reload).not.toHaveBeenCalled();
    network.onLine = false;
    window.dispatchEvent(new Event("offline"));
    expect(view.element.querySelector("button")!.disabled).toBe(true);
  } finally { view.destroy(); }
});

it("lets the first service worker install without saving or reloading", async () => {
  const session = { snapshot: createSnapshot(), subscribe: () => () => {}, dispose: vi.fn() };
  const waiting = { postMessage: vi.fn() };
  const registration = Object.assign(new EventTarget(), { waiting, installing: null, unregister: vi.fn() });
  const serviceWorker = Object.assign(new EventTarget(), { controller: null, register: vi.fn(async () => registration), ready: Promise.resolve() });
  vi.stubGlobal("navigator", { onLine: true, serviceWorker });
  vi.stubGlobal("fetch", vi.fn(async () => ({ ok: false })));
  const save = vi.fn();
  const reload = vi.fn();
  const view = new GameUpdateView(session, save, reload);
  try {
    await view.initialize("/CRS/");
    expect(waiting.postMessage).not.toHaveBeenCalled();
    expect(save).not.toHaveBeenCalled();
    expect(reload).not.toHaveBeenCalled();
  } finally { view.destroy(); }
});

it("defers an update during the tutorial and resumes after exit and reconnect", async () => {
  let publish = () => {};
  const session = {
    isPending: true,
    dispose: vi.fn(),
    subscribe: (listener: () => void) => { publish = listener; return () => {}; },
  };
  const waiting = { postMessage: vi.fn((_message, [reply]: MessagePort[]) => {
    reply.postMessage({ status: "ready" });
    reply.close();
  }) };
  const registration = Object.assign(new EventTarget(), { waiting, unregister: vi.fn(async () => true) });
  const serviceWorker = Object.assign(new EventTarget(), { controller: {}, register: vi.fn(async () => registration), ready: Promise.resolve() });
  const network = { onLine: true, serviceWorker };
  vi.stubGlobal("navigator", network);
  vi.stubGlobal("fetch", vi.fn(async () => ({ ok: false })));
  const save = vi.fn();
  const reload = vi.fn();
  const view = new GameUpdateView(session, save, reload);
  const dialog = document.querySelector<HTMLDialogElement>(".game-update-progress")!;
  dialog.showModal = () => { dialog.open = true; };
  dialog.close = () => { dialog.open = false; };
  try {
    await view.initialize("/CRS/");
    expect(view.element.textContent).toContain("Return to the main screen");
    expect(waiting.postMessage).not.toHaveBeenCalled();
    network.onLine = false;
    session.isPending = false;
    publish();
    expect(view.element.textContent).toContain("Reconnect to update the game.");
    expect(save).not.toHaveBeenCalled();
    network.onLine = true;
    window.dispatchEvent(new Event("online"));
    await vi.waitFor(() => expect(reload).toHaveBeenCalledOnce());
    expect(save).toHaveBeenCalledOnce();
  } finally { view.destroy(); }
});

it("checks for tabs that opened while the checkpoint was being written", async () => {
  const session = { snapshot: createSnapshot(), subscribe: () => () => {}, dispose: vi.fn() };
  const responses = [{ status: "ready" }, { status: "other-tabs", otherTabs: 1 }];
  const waiting = { postMessage: vi.fn((_message, [reply]: MessagePort[]) => {
    reply.postMessage(responses.shift());
    reply.close();
  }) };
  const registration = Object.assign(new EventTarget(), { waiting, installing: null, unregister: vi.fn() });
  const serviceWorker = Object.assign(new EventTarget(), { controller: {}, register: vi.fn(async () => registration), ready: Promise.resolve() });
  vi.stubGlobal("navigator", { onLine: true, serviceWorker });
  vi.stubGlobal("fetch", vi.fn(async () => ({ ok: false })));
  const reload = vi.fn();
  const release = vi.fn();
  const view = new GameUpdateView(session, async () => release, reload);
  const dialog = document.querySelector<HTMLDialogElement>(".game-update-progress")!;
  dialog.showModal = () => { dialog.open = true; };
  dialog.close = () => { dialog.open = false; };
  try {
    await view.initialize("/CRS/");
    await vi.waitFor(() => expect(release).toHaveBeenCalledOnce());
    expect(view.element.textContent).toContain("Close 1 other game tab");
    expect(registration.unregister).not.toHaveBeenCalled();
    expect(reload).not.toHaveBeenCalled();
  } finally { view.destroy(); }
});
