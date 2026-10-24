import { afterEach, expect, it } from "vitest";
import { FuelOrderList } from "./FuelOrderList";
import { createSnapshot, createShift } from "./testSnapshot";

afterEach(() => document.body.replaceChildren());

it("fills the sheet in execution order, preserves row focus, removes and locks draft rows", () => {
  const snapshot = createSnapshot(); snapshot.shift = createShift();
  const sheet = new FuelOrderList(); document.body.append(sheet.element);
  sheet.update(snapshot, [], false);
  expect(sheet.element.classList.contains("is-filled")).toBe(false);
  sheet.update(snapshot, [211, 210], false);
  const buttons = [...sheet.element.querySelectorAll<HTMLButtonElement>("button")];
  expect(buttons.map(b => b.dataset.planChannel)).toEqual(["210", "211"]);
  buttons[0].focus(); sheet.update(snapshot, [210, 211, 212], false);
  expect(document.activeElement).toBe(buttons[0]);
  sheet.update(snapshot, [211, 212], true);
  expect(sheet.element.querySelector('[data-plan-channel="210"]')).toBeNull();
  expect([...sheet.element.querySelectorAll<HTMLButtonElement>("button")].every(b => b.disabled)).toBe(true);
  expect(snapshot.shift.fuelConsumed).toBe(8);
});
