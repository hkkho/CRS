import { expect, it } from "vitest";
import { ArcadeMap } from "./ArcadeMap";
import { createSnapshot } from "./testSnapshot";

it("keeps spatial keyboard selection and planned markings separate, and locks input", () => {
  const snapshot = createSnapshot();
  let selected = 0;
  const map = new ArcadeMap(snapshot, index => { selected = index; map.update(snapshot, index, [0], "power", false); });
  document.body.append(map.element);
  map.update(snapshot, 0, [0], "power", false);
  const first = map.element.querySelector<HTMLButtonElement>('[data-channel="0"]')!;
  first.dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowDown", bubbles: true }));
  expect(selected).toBe(22);
  expect(first.dataset.planned).toBe("true");
  expect(first.getAttribute("aria-pressed")).toBe("false");
  expect(document.activeElement).toBe(map.element.querySelector('[data-channel="22"]'));
  map.update(snapshot, selected, [0], "burnup", true);
  first.click(); expect(selected).toBe(22);
  expect(first.disabled).toBe(true);
  map.destroy(); map.element.remove();
});
