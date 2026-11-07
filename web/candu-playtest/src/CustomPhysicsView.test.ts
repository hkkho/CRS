// @vitest-environment jsdom
import { expect, it, vi } from "vitest";
import { CustomPhysicsView } from "./CustomPhysicsView";

it("hydrates a checkpoint draft, resets to defaults, and submits only changed values", () => {
  const build = vi.fn();
  const editor = new CustomPhysicsView(build);
  editor.element.close = vi.fn();
  document.body.append(editor.element);
  const field = (name: string) => editor.element.querySelector<HTMLInputElement>(`[name="${name}"]`)!;
  const submit = () => editor.element.querySelector("form")!.dispatchEvent(new Event("submit", { cancelable: true }));
  const power = "practice.thermal_power_multiplier";
  const fuel = "coefficient_tables[0].rows[1].energy_per_fission_j";
  field("seed").value = "1001";
  editor.setDraft({ [power]: 0.98 });
  expect(field(power).valueAsNumber).toBe(0.98);
  editor.element.querySelector<HTMLButtonElement>("[data-defaults]")!.click();
  expect(field(power).valueAsNumber).toBe(1);
  field(fuel).value = "3.2e-11";
  submit();
  expect(build).toHaveBeenCalledExactlyOnceWith(1001, { [fuel]: 3.2e-11 });
  editor.element.remove();
});
