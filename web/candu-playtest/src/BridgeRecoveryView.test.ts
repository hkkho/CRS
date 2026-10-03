import { afterEach, expect, it, vi } from "vitest";
import { BridgeRecoveryView } from "./BridgeRecoveryView";
import type { BridgeStatus } from "./protocol";

afterEach(() => document.body.replaceChildren());

it("offers explicit reload with loss disclosure only after failure", () => {
  const reload = vi.fn();
  const view = new BridgeRecoveryView(document.body, reload);
  view.update({ source: "loading", detail: "Loading" } as BridgeStatus);
  expect(view.element.hidden).toBe(true);
  view.update({ source: "unavailable", detail: "Command timed out" } as BridgeStatus);
  expect(view.element.hidden).toBe(false);
  expect(view.element.textContent).toContain("progress will be lost");
  expect(view.element.textContent).toContain("Command timed out");
  expect(reload).not.toHaveBeenCalled();
  const button = view.element.querySelector("button")!;
  expect(document.activeElement).toBe(button);
  button.click();
  expect(reload).toHaveBeenCalledOnce();
});
