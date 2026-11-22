import pack from "../../../data/packs/candu6-two-group-diffusion-pack-v1.json";
import type { CustomPhysics } from "./customPhysics";
import { makePhysicsProfile, maxProfileBytes, readPhysicsProfile } from "./physicsProfile";
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

type SectionId = "operating" | "geometry" | "fuel" | "solver";
type Field = { input: HTMLInputElement; section: SectionId; name: string };

const sections: [SectionId, string][] = [
  ["operating", "Power and poisons"],
  ["geometry", "Geometry and adjusters"],
  ["fuel", "Fuel and xenon table"],
  ["solver", "Solver tolerances and iteration limits"],
];

const fuelColumns = [
  ["burnup_j_per_kg_hm", "Burnup", "J/kg HM"],
  ["absorption_group1_per_m", "Fast absorption", "m⁻¹"],
  ["absorption_group2_per_m", "Thermal absorption", "m⁻¹"],
  ["fission_group1_per_m", "Fast fission", "m⁻¹"],
  ["fission_group2_per_m", "Thermal fission", "m⁻¹"],
  ["nu_fission_group1_per_m", "Fast ν-fission", "m⁻¹"],
  ["nu_fission_group2_per_m", "Thermal ν-fission", "m⁻¹"],
  ["downscatter_group1_to_2_per_m", "Fast → thermal scatter", "m⁻¹"],
  ["chi_group1", "Fast fission spectrum", "fraction"],
  ["energy_per_fission_j", "Fission energy", "J"],
  ["xe135_number_density_m3", "Included xenon-135", "m⁻³"],
] as const;

/** Draft inputs remain in the launcher while the worker owns the active model. */
export class CustomPhysicsView {
  readonly element = document.createElement("dialog");
  private readonly seed: HTMLInputElement;
  private readonly fields = new Map<string, Field>();
  private readonly panels = new Map<SectionId, HTMLElement>();
  private readonly navigation = new Map<SectionId, HTMLButtonElement>();
  private readonly sectionPicker: HTMLSelectElement;
  private readonly count: HTMLElement;
  private readonly status: HTMLElement;
  private readonly form: HTMLFormElement;
  private draft: CustomPhysics = {};
  private active: SectionId = "operating";
  private loadGeneration = 0;

  constructor(start: (seed: number, physics: CustomPhysics) => void) {
    this.element.className = "custom-physics workspace-native";
    this.element.setAttribute("aria-labelledby", "custom-physics-heading");
    this.element.innerHTML = `<form novalidate>
      <header class="physics-header">
        <div class="physics-title"><h2 id="custom-physics-heading">Custom physics</h2>
          <label class="physics-seed">Core seed <input name="seed" type="number" required min="0" max="4294967295" step="1"></label>
        </div>
        <div class="physics-file-actions">
          <button type="button" data-load>Load profile</button>
          <button type="button" data-save>Save profile</button>
          <input type="file" accept=".json,application/json" hidden>
        </div>
        <p class="physics-status" data-status role="status"></p>
      </header>
      <div class="physics-workspace">
        <nav aria-label="Physics sections"></nav>
        <label class="physics-section-picker">Section <select></select></label>
        <div class="physics-panels"></div>
      </div>
      <footer>
        <p data-count aria-live="polite"></p>
        <div class="physics-actions">
          <button type="button" data-defaults>Restore defaults</button>
          <button type="button" data-cancel>Cancel</button>
          <button type="submit" class="workspace-primary">Build custom core</button>
        </div>
      </footer>
    </form>`;
    this.form = this.element.querySelector("form")!;
    this.seed = this.form.querySelector("[name=seed]")!;
    this.count = this.form.querySelector("[data-count]")!;
    this.status = this.form.querySelector("[data-status]")!;
    this.sectionPicker = this.form.querySelector("select")!;
    const nav = this.form.querySelector("nav")!;
    const panels = this.form.querySelector(".physics-panels")!;
    for (const [id, title] of sections) {
      const panel = document.createElement("section");
      panel.id = `physics-panel-${id}`;
      panel.setAttribute("aria-label", title);
      const heading = document.createElement("h3");
      heading.textContent = title;
      panel.append(heading);
      panels.append(panel);
      this.panels.set(id, panel);

      const button = document.createElement("button");
      button.type = "button";
      button.textContent = title;
      button.setAttribute("aria-controls", panel.id);
      button.addEventListener("click", () => this.showSection(id));
      nav.append(button);
      this.navigation.set(id, button);
      this.sectionPicker.add(new Option(title, id));
    }
    this.sectionPicker.addEventListener("change", () => this.showSection(this.sectionPicker.value as SectionId));

    const operating = this.panels.get("operating")!;
    const power = this.group(operating, "Power and liquid zones");
    const poison = this.group(operating, "Iodine and xenon kinetics");
    for (const [key, label, initial, min, max] of runtimeFields) {
      this.addRow(key.includes("xenon") || key.includes("iodine") ? poison : power,
        `practice.${key}`, label, initial, "operating", min, max);
    }

    const geometry = this.panels.get("geometry")!;
    const diffusion = this.group(geometry, "Diffusion geometry");
    for (const [key, value] of Object.entries(pack.geometry)) {
      if (key === "vacuum_boundary_conductance_m2") continue;
      if (typeof value === "number") this.addRow(diffusion, `geometry.${key}`, labelFor(key), value, "geometry");
      else this.record(this.group(geometry, labelFor(key)), `geometry.${key}`, value, "geometry");
    }
    this.record(this.group(geometry, "Fixed adjusters"), "adjusters", pack.adjusters, "geometry");
    this.fuelTable(this.panels.get("fuel")!);
    this.record(this.group(this.panels.get("solver")!, "Convergence settings"), "solver", pack.solver, "solver");

    const fileInput = this.form.querySelector<HTMLInputElement>("input[type=file]")!;
    this.form.querySelector("[data-load]")!.addEventListener("click", () => fileInput.click());
    fileInput.addEventListener("change", () => {
      const file = fileInput.files?.[0];
      fileInput.value = "";
      if (file) void this.loadFile(file);
    });
    this.form.querySelector("[data-save]")!.addEventListener("click", () => {
      const overrides = this.readFields();
      if (!overrides) return;
      const blob = new Blob([JSON.stringify(makePhysicsProfile(overrides), null, 2) + "\n"], { type: "application/json" });
      const link = document.createElement("a");
      link.href = URL.createObjectURL(blob);
      link.download = "crs-physics-profile.json";
      link.click();
      setTimeout(() => URL.revokeObjectURL(link.href), 0);
      this.showStatus("Physics profile saved.");
    });
    this.form.addEventListener("input", () => this.updateCount());
    this.form.querySelector("[data-defaults]")!.addEventListener("click", () => {
      this.loadGeneration++;
      this.showStatus("");
      this.writeFields({});
    });
    const cancel = () => {
      this.loadGeneration++;
      this.writeFields(this.draft);
      this.element.close();
    };
    this.form.querySelector("[data-cancel]")!.addEventListener("click", cancel);
    this.element.addEventListener("cancel", event => { event.preventDefault(); cancel(); });
    this.form.addEventListener("submit", event => {
      event.preventDefault();
      if (!this.seed.reportValidity()) return;
      const overrides = this.readFields();
      if (!overrides) return;
      this.loadGeneration++;
      this.draft = overrides;
      this.element.close();
      start(this.seed.valueAsNumber, overrides);
    });
    this.showSection("operating");
    this.updateCount();
  }

  open(seed: string): void {
    this.loadGeneration++;
    this.seed.value = seed;
    this.showStatus("");
    this.showSection("operating");
    this.element.showModal();
  }

  setDraft(physics: CustomPhysics): void {
    this.draft = physics;
    this.writeFields(physics);
  }

  private showStatus(message: string, error = false): void {
    this.status.classList.toggle("physics-error", error);
    this.status.textContent = message;
  }

  private async loadFile(file: File): Promise<void> {
    const generation = ++this.loadGeneration;
    try {
      if (file.size > maxProfileBytes) throw new Error("Choose a JSON profile no larger than 64 KiB.");
      const text = new TextDecoder("utf-8", { fatal: true }).decode(await file.arrayBuffer());
      if (generation !== this.loadGeneration) return;
      const overrides = readPhysicsProfile(text, this.fields.keys());
      for (const [path, value] of Object.entries(overrides)) {
        const { input, name } = this.fields.get(path)!;
        const check = input.cloneNode() as HTMLInputElement;
        check.value = String(value);
        if (!check.validity.valid) throw new Error(name + ". " + check.validationMessage);
      }
      this.writeFields(overrides);
      this.showStatus("Loaded " + file.name + ". Review the values before building.");
    } catch (error) {
      if (generation === this.loadGeneration) {
        this.showStatus(error instanceof Error ? error.message : "The physics profile could not be read.", true);
      }
    }
  }

  private showSection(id: SectionId): void {
    this.active = id;
    this.sectionPicker.value = id;
    for (const [key, panel] of this.panels) panel.hidden = key !== id;
    for (const [key, button] of this.navigation) button.setAttribute("aria-pressed", String(key === id));
    this.form.querySelector(".physics-panels")!.scrollTop = 0;
  }

  private readFields(): CustomPhysics | undefined {
    const overrides: Record<string, number> = {};
    for (const [path, { input, section }] of this.fields) {
      if (!input.validity.valid) {
        if (this.active !== section) this.showSection(section);
        input.reportValidity();
        return;
      }
      const value = input.valueAsNumber || 0;
      if (value !== Number(input.defaultValue)) overrides[path] = value;
    }
    return overrides;
  }

  private writeFields(physics: CustomPhysics): void {
    for (const [key, { input }] of this.fields) input.value = String(physics[key] ?? input.defaultValue);
    this.updateCount();
  }

  private updateCount(): void {
    let changed = 0;
    for (const { input } of this.fields.values()) {
      const edited = input.valueAsNumber !== Number(input.defaultValue);
      input.classList.toggle("physics-edited", edited);
      if (edited) changed++;
    }
    const message = changed === 0 ? "Built-in defaults" : changed + (changed === 1 ? " edited parameter" : " edited parameters");
    if (this.count.textContent !== message) this.count.textContent = message;
  }

  private group(parent: Element, title: string): HTMLElement {
    const fieldset = document.createElement("fieldset");
    const heading = document.createElement("legend");
    heading.textContent = title;
    const labels = document.createElement("div");
    labels.className = "physics-row physics-column-names";
    labels.innerHTML = "<span>Parameter</span><span>Default</span><span>Value</span>";
    fieldset.append(heading, labels);
    parent.append(fieldset);
    return fieldset;
  }

  private record(parent: Element, prefix: string, values: Record<string, unknown>, section: SectionId): void {
    for (const [key, value] of Object.entries(values)) {
      if (typeof value !== "number") continue;
      const iterationLimit = key.startsWith("maximum_") && key.endsWith("iterations");
      this.addRow(parent, prefix + "." + key, labelFor(key), value, section, iterationLimit ? 1 : 0,
        iterationLimit ? value : undefined, iterationLimit ? "1" : "any");
    }
  }

  private addRow(parent: Element, path: string, name: string, value: number, section: SectionId,
    min = 0, max?: number, step = "any"): void {
    const row = document.createElement("div");
    const label = document.createElement("label");
    const base = document.createElement("span");
    const input = this.createInput(path, name, value, section, min, max, step);
    row.className = "physics-row";
    label.textContent = name;
    label.htmlFor = input.id;
    base.className = "physics-default";
    base.textContent = String(value);
    row.append(label, base, input);
    parent.append(row);
  }

  private fuelTable(parent: Element): void {
    const instructions = document.createElement("p");
    instructions.textContent = "Scroll across for all coefficients. Included xenon follows the fuel burnup knots.";
    const region = document.createElement("div");
    region.className = "physics-table-scroll";
    region.setAttribute("role", "region");
    region.setAttribute("aria-label", "Scrollable fuel and xenon table");
    region.tabIndex = 0;
    const table = document.createElement("table");
    table.setAttribute("aria-label", "Fuel and xenon coefficients by burnup");
    const header = table.createTHead().insertRow();
    for (const [title, units] of [["Default knot", "MWd/kg HM"], ...fuelColumns.map(([, name, unit]) => [name, unit])]) {
      const cell = document.createElement("th");
      cell.scope = "col";
      const unit = document.createElement("small");
      unit.textContent = units;
      cell.append(title, unit);
      header.append(cell);
    }
    const body = table.createTBody();
    pack.coefficient_tables[0].rows.forEach((row, index) => {
      const tr = body.insertRow();
      const knot = document.createElement("th");
      const defaultBurnup = row.burnup_j_per_kg_hm / 8.64e10;
      knot.scope = "row";
      knot.textContent = index === 0 ? "Fresh" : (index + 1) + " · " + defaultBurnup.toPrecision(4);
      knot.title = "Default burnup knot " + (index + 1);
      tr.append(knot);
      for (const [key, label, units] of fuelColumns) {
        const cell = tr.insertCell();
        const xenon = key === "xe135_number_density_m3";
        const value = xenon ? pack.xenon_reference.rows[index].xe135_number_density_m3 : row[key];
        if (index === 0 && (xenon || key === "burnup_j_per_kg_hm")) {
          cell.textContent = String(value);
          continue;
        }
        const prefix = xenon ? "xenon_reference.rows" : "coefficient_tables[0].rows";
        cell.append(this.createInput(prefix + "[" + index + "]." + key, label + " (" + units + "), knot " + (index + 1), value, "fuel"));
      }
    });
    region.append(table);
    parent.append(instructions, region);
  }

  private createInput(path: string, name: string, value: number, section: SectionId,
    min = 0, max?: number, step = "any"): HTMLInputElement {
    const input = document.createElement("input");
    input.id = "physics-field-" + this.fields.size;
    input.name = path;
    input.type = "number";
    input.step = step;
    input.min = String(min);
    if (max !== undefined) input.max = String(max);
    input.required = true;
    input.defaultValue = String(value);
    input.setAttribute("aria-label", name);
    input.title = "Default " + value + " · Min " + min + (max === undefined ? "" : " · Max " + max);
    this.fields.set(path, { input, section, name });
    return input;
  }
}
