import pack from "../../../data/packs/candu6-two-group-diffusion-pack-v1.json";
import type { CustomPhysics } from "./customPhysics";
import "./customPhysics.css";

const runtimeFields = [
  ["thermal_power_multiplier", "Thermal power (× 2,064 MW)", 1, 0.01, 10],
  ["lzc_fast_absorption_multiplier", "Liquid zone absorption · fast (× default)", 1, 0, 10],
  ["lzc_thermal_absorption_multiplier", "Liquid zone absorption · thermal (× default)", 1, 0, 10],
  ["iodine_yield_multiplier", "Iodine-135 yield (× default)", 1, 0, 10],
  ["xenon_yield_multiplier", "Xenon-135 yield (× default)", 1, 0, 10],
  ["iodine_half_life_multiplier", "Iodine-135 half-life (× 6.57 h)", 1, 0.01, 10],
  ["xenon_half_life_multiplier", "Xenon-135 half-life (× 9.14 h)", 1, 0.01, 10],
  ["xenon_fast_absorption_m2", "Xenon-135 absorption · fast (m²)", 0, 0, 1e-23],
  ["xenon_thermal_absorption_multiplier", "Xenon-135 absorption · thermal (× default)", 1, 0, 10],
] as const;

function labelFor(key: string): string {
  const label = key
    .replace(/_j_per_kg_hm$/, " (J/kg HM)").replace(/_number_density_m3$/, " number density (m⁻³)")
    .replace(/_per_m$/, " (m⁻¹)").replace(/_m2$/, " (m²)").replace(/_m3$/, " (m³)")
    .replace(/_m$/, " (m)").replace(/_j$/, " (J)")
    .replaceAll("group1", "fast").replaceAll("group2", "thermal").replaceAll("_", " ")
    .replace("xe135", "xenon-135").replace("nu fission", "neutron production from fission");
  return label[0].toUpperCase() + label.slice(1);
}

/** Draft inputs remain in the launcher while the worker owns the active model. */
export class CustomPhysicsView {
  readonly element = document.createElement("dialog");
  private readonly seed: HTMLInputElement;
  private readonly fields = new Map<string, HTMLInputElement>();
  private readonly count: HTMLElement;
  private readonly form: HTMLFormElement;
  private draft: CustomPhysics = {};

  constructor(start: (seed: number, physics: CustomPhysics) => void) {
    this.element.className = "custom-physics workspace-native";
    this.element.setAttribute("aria-labelledby", "custom-physics-heading");
    this.element.innerHTML = `<form novalidate>
      <header><p class="workspace-kicker">CRS / SANDBOX</p><h2 id="custom-physics-heading">Custom physics</h2>
        <p>Set up a time-based sandbox. The shared simulator builds a new core from these values and checks whether it can settle. Edits apply for the entire run.</p>
        <label class="physics-field">Core seed<input name="seed" type="number" required min="0" max="4294967295" step="1"></label>
      </header>
      <div class="physics-sections"></div>
      <footer><p data-count aria-live="polite"></p><div class="physics-actions">
        <button type="button" data-defaults>Restore defaults</button><button type="button" data-cancel>Cancel</button>
        <button type="submit" class="workspace-primary">Build custom core</button>
      </div></footer>
    </form>`;
    this.form = this.element.querySelector("form")!;
    this.seed = this.form.querySelector("[name=seed]")!;
    this.count = this.form.querySelector("[data-count]")!;
    const sections = this.form.querySelector(".physics-sections")!;
    const operating = this.section(sections, "Power and liquid zones", true);
    const poison = this.section(sections, "Iodine and xenon kinetics");
    for (const [key, label, initial, min, max] of runtimeFields) {
      this.field(key.includes("xenon") || key.includes("iodine") ? poison : operating,
        `practice.${key}`, label, initial, min, max);
    }
    const geometry = this.section(sections, "Diffusion and boundary geometry");
    for (const [key, value] of Object.entries(pack.geometry)) {
      if (key === "vacuum_boundary_conductance_m2") continue; // Legacy fallback. V10 uses derived conductances.
      if (typeof value === "number") this.field(geometry, `geometry.${key}`, labelFor(key), value);
      else this.record(this.section(geometry, labelFor(key)), `geometry.${key}`, value);
    }
    this.record(this.section(sections, "Fixed adjusters"), "adjusters", pack.adjusters);
    const fuel = this.section(sections, "Fuel coefficients by burnup");
    const xenon = this.section(sections, "Included xenon reference");
    const linked = document.createElement("p");
    linked.textContent = "Xenon rows use the matching fuel burnup knots. Fresh fuel starts with zero included xenon.";
    xenon.append(linked);
    pack.coefficient_tables[0].rows.forEach((row, index) => {
      const exposure = row.burnup_j_per_kg_hm / 8.64e10;
      const { burnup_j_per_kg_hm: burnup, ...coefficients } = row;
      const rowSection = this.section(fuel, `Row ${index + 1} · default ${exposure.toPrecision(4)} MWd/kg HM`);
      const prefix = `coefficient_tables[0].rows[${index}]`;
      if (index > 0) {
        this.field(rowSection, `${prefix}.burnup_j_per_kg_hm`, "Burnup knot (J/kg HM)", burnup);
        this.field(xenon, `xenon_reference.rows[${index}].xe135_number_density_m3`,
          `Row ${index + 1} · xenon-135 number density (m⁻³)`, pack.xenon_reference.rows[index].xe135_number_density_m3);
      }
      this.record(rowSection, prefix, coefficients);
    });
    this.record(this.section(sections, "Solver tolerances and iteration limits"), "solver", pack.solver);

    this.form.addEventListener("input", () => this.updateCount());
    this.form.querySelector("[data-defaults]")!.addEventListener("click", () => this.writeFields({}));
    const cancel = () => { this.writeFields(this.draft); this.element.close(); };
    this.form.querySelector("[data-cancel]")!.addEventListener("click", cancel);
    this.element.addEventListener("cancel", event => { event.preventDefault(); cancel(); });
    this.form.addEventListener("submit", event => {
      event.preventDefault();
      for (const input of [this.seed, ...this.fields.values()]) {
        if (input.validity.valid) continue;
        for (let section = input.closest("details"); section; section = section.parentElement?.closest("details") ?? null) section.open = true;
        input.reportValidity();
        return;
      }
      const overrides = Object.fromEntries([...this.fields]
        .filter(([, input]) => input.valueAsNumber !== Number(input.defaultValue))
        .map(([key, input]) => [key, input.valueAsNumber]));
      this.draft = overrides;
      this.element.close();
      start(this.seed.valueAsNumber, overrides);
    });
    this.updateCount();
  }

  open(seed: string): void {
    this.seed.value = seed;
    this.element.showModal();
  }

  setDraft(physics: CustomPhysics): void {
    this.draft = physics;
    this.writeFields(physics);
  }

  private writeFields(physics: CustomPhysics): void {
    for (const [key, input] of this.fields) input.value = String(physics[key] ?? input.defaultValue);
    this.updateCount();
  }

  private updateCount(): void {
    const changed = [...this.fields.values()].filter(input => input.valueAsNumber !== Number(input.defaultValue)).length;
    const message = changed === 0 ? "Using the built-in physics defaults." : `${changed} parameter${changed === 1 ? "" : "s"} edited.`;
    if (this.count.textContent !== message) this.count.textContent = message;
  }

  private section(parent: Element, title: string, open = false): HTMLDetailsElement {
    const details = document.createElement("details");
    const summary = document.createElement("summary");
    summary.textContent = title;
    details.open = open;
    details.append(summary);
    parent.append(details);
    return details;
  }

  private record(parent: Element, prefix: string, values: Record<string, unknown>): void {
    for (const [key, value] of Object.entries(values)) {
      if (typeof value !== "number") continue;
      const iterationLimit = key.startsWith("maximum_") && key.endsWith("iterations");
      this.field(parent, `${prefix}.${key}`, labelFor(key), value, iterationLimit ? 1 : 0,
        iterationLimit ? value : undefined, iterationLimit ? "1" : "any");
    }
  }

  private field(parent: Element, key: string, name: string, value: number, min = 0, max?: number, step = "any"): void {
    const label = document.createElement("label");
    label.className = "physics-field";
    const input = document.createElement("input");
    input.name = key;
    input.type = "number";
    input.step = step;
    input.min = String(min);
    if (max !== undefined) input.max = String(max);
    input.required = true;
    input.defaultValue = String(value);
    label.append(name, input);
    parent.append(label);
    this.fields.set(key, input);
  }
}
