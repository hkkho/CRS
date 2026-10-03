import { afterEach, expect, it, vi } from "vitest";
import { ZoneLayoutEditor } from "./ZoneLayoutEditor";
import type { BridgeSessionController } from "./sessionController";
import type { CanduSnapshot } from "./protocol";

let editor: ZoneLayoutEditor | null = null;
afterEach(() => { editor?.destroy(); editor = null; document.body.replaceChildren(); });

it("provides native channel inspection, one map Tab stop, modal announcements and recovery focus", () => {
  HTMLDialogElement.prototype.showModal = vi.fn(); HTMLDialogElement.prototype.close = vi.fn();
  const snapshot = { rrs: { absorptionReferenceFillFraction: 0 }, core: { channels: [189, 190].map((channelIndex, index) => ({ channelIndex, gridColumn: 10 + index, gridRow: 10,
    bundles: Array.from({ length: 12 }, (_, position) => ({ position, logicalZoneId: 3, absorberZoneId: 3,
      group1AbsorptionPerMPerFillFraction: 0.02, group2AbsorptionPerMPerFillFraction: 0.008 })) })) } } as CanduSnapshot;
  const region = document.createElement("div"); region.id = "session-announcements"; document.body.append(region);
  const previous = document.createElement("button"); document.body.append(previous); previous.focus();
  let listener = () => {};
  const unsubscribe = vi.fn(), onClose = vi.fn(), dispatch = vi.fn();
  const session = { snapshot, dispatch, status: { isWasmAvailable: true }, isPending: false,
    subscribe: (callback: () => void) => { listener = callback; callback(); return unsubscribe; } };
  editor = new ZoneLayoutEditor(session as unknown as BridgeSessionController, onClose);
  const dialog = document.querySelector("dialog")!;
  expect(region.parentElement).toBe(dialog);
  const channel = dialog.querySelector<HTMLSelectElement>('[data-field="channel"]')!;
  channel.value = "190"; channel.dispatchEvent(new Event("change", { bubbles: true }));
  expect(dialog.querySelector('[data-field="cell"]')!.textContent).toContain("Channel 190");
  expect(dialog.querySelectorAll('rect[tabindex="0"]')).toHaveLength(1);
  expect(dispatch).not.toHaveBeenCalled();
  session.status.isWasmAvailable = false; listener(); editor = null;
  expect(region.parentElement).toBe(document.body);
  expect(document.querySelector("dialog")).toBeNull();
  expect(unsubscribe).toHaveBeenCalledTimes(1); expect(onClose).toHaveBeenCalledTimes(1);
  expect(document.activeElement).toBe(previous);
});

it("keeps draft edits separate until apply and retains a rejected draft", async () => {
  HTMLDialogElement.prototype.showModal = vi.fn();
  HTMLDialogElement.prototype.close = vi.fn();
  const snapshot = { rrs: { absorptionReferenceFillFraction: 0 }, core: { channels: [{ channelIndex: 189, gridColumn: 10, gridRow: 10,
    bundles: Array.from({ length: 12 }, (_, position) => ({ position, logicalZoneId: position < 6 ? 3 : 10,
      absorberZoneId: position < 6 ? 3 : 10, group1AbsorptionPerMPerFillFraction: 0.02, group2AbsorptionPerMPerFillFraction: 0.008 })) }] } } as CanduSnapshot;
  const dispatch = vi.fn().mockResolvedValue({ accepted: false, message: "Empty region rejected." });
  const session = { snapshot, dispatch, status: { isWasmAvailable: true }, isPending: false,
    subscribe: () => () => {} } as unknown as BridgeSessionController;
  editor = new ZoneLayoutEditor(session, vi.fn());
  const dialog = document.querySelector("dialog")!;
  (dialog.querySelector('[data-field="zone"]') as HTMLSelectElement).value = "4";
  (dialog.querySelector('[data-action="paint"]') as HTMLButtonElement).click();
  expect(snapshot.core.channels[0]!.bundles[0]!.logicalZoneId).toBe(3);
  expect(dialog.querySelector('[data-field="cell"]')!.textContent).toContain("region Z5");
  (dialog.querySelector('[data-action="apply"]') as HTMLButtonElement).click();
  await vi.waitFor(() => expect(dialog.querySelector('[data-field="message"]')!.textContent).toContain("Rejected"));
  expect(dispatch.mock.calls[0]![0].nodes[0]).toMatchObject({ logicalZoneId: 4, absorberZoneId: 3 });
  expect(dialog.querySelector('[data-field="cell"]')!.textContent).toContain("region Z5");
  (dialog.querySelector('[data-action="undo"]') as HTMLButtonElement).click();
  expect(dialog.querySelector('[data-field="cell"]')!.textContent).toContain("region Z4");
});
