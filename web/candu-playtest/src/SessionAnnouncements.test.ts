import { expect, it } from "vitest";
import { SessionAnnouncements } from "./SessionAnnouncements";
import { createSnapshot } from "./testSnapshot";
import type { CanduCommandResponse } from "./protocol";
import type { SessionUpdate } from "./sessionController";

function harness() {
  const region = document.createElement("div");
  const announcements = new SessionAnnouncements(region);
  const update: SessionUpdate = { snapshot: createSnapshot(), status: { source: "wasm", isWasmAvailable: true, title: "Ready", detail: "Ready", capabilities: [] }, pending: false, response: null, error: null };
  const observer = new MutationObserver(() => {}); observer.observe(region, { childList: true });
  const emit = () => { announcements.update(update); return observer.takeRecords().length; };
  const response = (accepted: boolean, type = "pause") => ({ accepted, command: { type }, message: "Order result", snapshot: update.snapshot } as CanduCommandResponse);
  return { region, update, emit, response };
}

it("stays quiet through live telemetry, pending and duplicate response emissions", () => {
  const { update, emit, response } = harness();
  expect(emit()).toBe(1);
  for (let i = 0; i < 100; i++) {
    update.snapshot.sequence++; update.snapshot.scoreTotal++;
    update.pending = i % 2 === 0; update.response = response(true, "advance");
    expect(emit()).toBe(0);
  }
  update.response = response(true); expect(emit()).toBe(1); expect(emit()).toBe(0);
  update.response = response(true); expect(emit()).toBe(1); // Distinct identical orders.
});

it("announces rejected orders/errors once and leaves connection failure to recovery", () => {
  const { region, update, emit, response } = harness(); emit();
  update.response = response(false); expect(emit()).toBe(1); expect(emit()).toBe(0);
  expect(region.textContent).toContain("Rejected");
  update.error = "Transport failed"; expect(emit()).toBe(1); expect(emit()).toBe(0);
  update.status = { ...update.status, source: "unavailable", isWasmAvailable: false };
  update.error = "Connection lost"; expect(emit()).toBe(0);
});

it("announces a terminal transition once even when it precedes its response and allows a new run", () => {
  const { region, update, emit, response } = harness(); emit();
  update.snapshot.runStatus = "completed"; update.snapshot.sequence = 20;
  expect(emit()).toBe(1); expect(region.textContent).toContain("Shift ended");
  update.response = response(true, "step"); expect(emit()).toBe(0);
  update.snapshot.runStatus = "paused"; update.snapshot.sequence = 1;
  update.response = response(true, "reset"); expect(emit()).toBe(1);
  update.snapshot.runStatus = "completed"; update.snapshot.sequence = 20;
  expect(emit()).toBe(1);
});
