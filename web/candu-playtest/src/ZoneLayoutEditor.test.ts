import { afterEach, expect, it, vi } from "vitest";
import { ZoneLayoutEditor } from "./ZoneLayoutEditor";
import type { BridgeSessionController } from "./sessionController";
import type { CanduSnapshot } from "./protocol";

let editor: ZoneLayoutEditor | null = null;
afterEach(() => { editor?.destroy(); editor = null; document.body.replaceChildren(); });

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
