// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { SimulatorManual } from "./SimulatorManual";

let manual: SimulatorManual | undefined;
afterEach(() => { manual?.destroy(); document.body.replaceChildren(); });
function setup() {
  const host = document.createElement("section");
  host.innerHTML = '<button data-manual-open>Manual</button>';
  document.body.append(host);
  manual = new SimulatorManual(host);
  return { host, manual, opener: host.querySelector<HTMLButtonElement>("[data-manual-open]")! };
}

describe("simulator manual", () => {
  it("opens on demand, switches chapters and restores focus on Escape", () => {
    const { manual, opener } = setup();
    expect(manual.element.open).toBe(false);
    opener.focus(); opener.click();
    expect(manual.element.open).toBe(true);
    expect(document.activeElement).toBe(manual.element.querySelector("[data-manual-close]"));
    manual.element.querySelector<HTMLButtonElement>('[data-manual-chapter="physics"]')!.click();
    expect((manual.element.querySelector("#manual-start") as HTMLElement).hidden).toBe(true);
    expect((manual.element.querySelector("#manual-physics") as HTMLElement).hidden).toBe(false);
    expect(manual.element.querySelector('[data-manual-chapter="physics"]')!.getAttribute("aria-pressed")).toBe("true");
    manual.element.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));
    expect(manual.element.open).toBe(false);
    expect(document.activeElement).toBe(opener);
  });

  it("supports F1, the close button, and removes its listeners on destruction", () => {
    const { host, manual, opener } = setup();
    opener.focus();
    opener.dispatchEvent(new KeyboardEvent("keydown", { key: "F1", bubbles: true, cancelable: true }));
    expect(manual.element.open).toBe(true);
    manual.element.querySelector<HTMLButtonElement>("[data-manual-close]")!.click();
    expect(manual.element.open).toBe(false);
    manual.destroy(); opener.click();
    expect(host.querySelector("dialog")).toBeNull();
    expect(manual.element.open).toBe(false);
  });

  it("wraps keyboard focus through the active chapter without reaching hidden pages", () => {
    const { manual, opener } = setup(); opener.click();
    const page = manual.element.querySelector<HTMLElement>("#manual-start")!;
    const close = manual.element.querySelector<HTMLButtonElement>("[data-manual-close]")!;
    page.focus(); page.dispatchEvent(new KeyboardEvent("keydown", { key: "Tab", bubbles: true, cancelable: true }));
    expect(document.activeElement).toBe(close);
    close.dispatchEvent(new KeyboardEvent("keydown", { key: "Tab", shiftKey: true, bubbles: true, cancelable: true }));
    expect(document.activeElement).toBe(page);
  });

  it("renders the shared Markdown and links to the published manual without replacing the run", () => {
    const { manual, opener } = setup(); opener.click();
    expect(manual.element.querySelectorAll("h1")).toHaveLength(1);
    expect(manual.element.querySelector("#manual-start h2")?.textContent).toBe("One day at a time");
    expect(manual.element.querySelectorAll("#manual-score tbody tr")).toHaveLength(4);
    const link = manual.element.querySelector<HTMLAnchorElement>(".manual-library-link")!;
    expect(link.getAttribute("href")).toBe(`${import.meta.env.BASE_URL}docs/manual/`);
    expect(link.target).toBe("_blank");
    expect(link.rel).toBe("noopener");
  });
});
