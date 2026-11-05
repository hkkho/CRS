import { DemoView } from "./DemoView";
import { GameUpdateView } from "./GameUpdateView";
import { PlayerStore } from "./playerStore";
import { randomCoreSeed } from "./coreSeed";
import { reactorIllustration } from "./ReactorIllustration";
import "./observatory.css";
import "./reactorExperience.css";
import "./workspace.css";
import "./interfaceSimplification.css";
import "./controlRoom.css";

const root = document.getElementById("game-root")!;
const store = new PlayerStore();
const checkpointKey = "candu-update-checkpoint";
const checkpointOwner = "time-based-update";
let game: DemoView | undefined;
let unsubscribe = () => {};
let renderVersion = () => {};

const launcher = document.createElement("main");
launcher.className = "reactor-launcher workspace-native";
launcher.innerHTML = `<div class="launcher-shell">
  <header class="launcher-masthead"><p class="workspace-kicker">CRS / A REACTOR STRATEGY GAME</p></header>
  <div class="launcher-hero"><div class="launcher-story">
    <h1>CRS</h1><p class="launcher-headline">YOUR CORE.<br><em>YOUR CALL.</em></p>
    <p class="launcher-intro">Keep the reactor in balance as time advances. Watch the core and queue fresh fuel where it is needed.</p>
    <div class="launcher-actions"><button data-action="demo" class="workspace-primary">Time based</button><button data-action="resume" hidden>Resume saved run</button></div>
    <div class="launcher-setup"><details><summary>Starting core</summary><label>Core seed<input data-field="seed" type="number" min="0" max="4294967295" step="1" required></label></details></div>
  </div><figure class="launcher-art">${reactorIllustration()}</figure></div>
</div>`;
const seed = launcher.querySelector<HTMLInputElement>("[data-field=seed]")!;
const play = launcher.querySelector<HTMLButtonElement>("[data-action=demo]")!;
const resume = launcher.querySelector<HTMLButtonElement>("[data-action=resume]")!;
seed.value = String(randomCoreSeed());

function readCheckpoint(): { seed: number; id?: string } | undefined {
  try {
    const entry = JSON.parse(sessionStorage.getItem(checkpointKey) ?? "null");
    if (entry && Number.isInteger(entry.seed) && entry.seed >= 0 && entry.seed <= 4294967295) return entry;
  } catch { /* The launcher still works when storage is disabled. */ }
}

function closeGame(): void {
  unsubscribe();
  game?.destroy();
  game = undefined;
  renderVersion();
}

function showLauncher(): void {
  closeGame();
  root.append(launcher);
  resume.hidden = !readCheckpoint();
  play.focus();
}

function openGame(coreSeed: number, id?: string, restored = false): void {
  launcher.remove();
  const restore = id ? store.get(checkpointOwner, id).then(save => {
    if (!save) throw new Error("The saved reactor checkpoint could not be found.");
    return save;
  }) : undefined;
  game = new DemoView(root, coreSeed, showLauncher, restore, () => {
    if (restored) {
      try { sessionStorage.removeItem(checkpointKey); } catch { /* The run can still start. */ }
    }
  });
  unsubscribe = game.subscribe(() => renderVersion());
}

play.addEventListener("click", () => { if (seed.reportValidity()) openGame(Number(seed.value)); });
resume.addEventListener("click", () => {
  const saved = readCheckpoint();
  if (saved) openGame(saved.seed, saved.id, true);
});
const saved = readCheckpoint();
if (saved) openGame(saved.seed, saved.id, true);
else showLauncher();

const updates = new GameUpdateView({
  get snapshot() { return game?.snapshot; },
  subscribe(listener) { renderVersion = listener; listener(); return () => { renderVersion = () => {}; }; },
  dispose: closeGame,
}, () => game?.checkpoint(async (coreSeed, run) => {
  if (run) await store.put(checkpointOwner, run);
  sessionStorage.setItem(checkpointKey, JSON.stringify({ seed: coreSeed, id: run?.id }));
}) ?? Promise.resolve());
void updates.initialize(import.meta.env.BASE_URL, import.meta.env.PROD);
window.addEventListener("beforeunload", () => { updates.destroy(); closeGame(); }, { once: true });
