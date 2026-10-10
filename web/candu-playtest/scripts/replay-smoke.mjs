import { chromium } from "playwright";

// Verify the published AOT bridge's digest against independent Web Crypto.
const browser = await chromium.launch({ headless: true });
const page = await browser.newPage();
const errors = [];
page.on("pageerror", error => errors.push(error.message));
page.on("console", message => { if (message.type() === "error") errors.push(message.text()); });
try {
  await page.goto(process.argv[2] ?? "http://127.0.0.1:4173");
  await page.waitForFunction(() => document.querySelector("#status-mirror")?.textContent?.includes("live reactor online"), undefined, { timeout: 60000 });
  const result = await page.evaluate(async () => {
    // The product host lives in its worker. Load the same published module in
    // this isolated test realm to inspect raw responses without UI transport.
    await import("/wasm/main.mjs");
    const bridge = globalThis.canduPlaytestWasm;
    const algorithm = "sha256-chained-replay-v2";
    const check = (ok, message) => { if (!ok) throw new Error(message); };
    const hash = async text => "sha256:" + Array.from(new Uint8Array(await crypto.subtle.digest(
      "SHA-256", new TextEncoder().encode(text))), value => value.toString(16).padStart(2, "0")).join("");
    const sorted = value => Array.isArray(value) ? value.map(sorted) : value && typeof value === "object"
      ? Object.fromEntries(Object.keys(value).sort().map(key => [key, sorted(value[key])])) : value;
    const initialization = '{"protocol":"candu-playtest-v2","mode":"play","pacingMode":"real-time","seed":42}';
    const initialized = JSON.parse(await bridge.initialize(initialization));
    const policy = initialized.snapshot.scorePolicyId;
    let digest = await hash(algorithm + "|" + policy + "|play|" + initialization);
    check(initialized.replayDigestAlgorithm === algorithm && initialized.replayDigest === digest, "Initialization digest mismatch.");
    for (const type of ["pause", "unknown-command", "pause"]) {
      const response = JSON.parse(await bridge.dispatchJson(JSON.stringify({ protocol: "candu-playtest-v2", type, responseMode: "compact" })));
      digest = await hash(digest + "|" + JSON.stringify(sorted(response.command)));
      check(response.replayDigestAlgorithm === algorithm && response.replayDigest === digest, "Dispatch digest mismatch: " + type);
      check(response.accepted === (type === "pause"), "Acceptance changed: " + type);
    }
    const rejected = JSON.parse(await bridge.dispatchJson("{not-json"));
    check(!rejected.accepted && rejected.replayDigest === digest, "Early rejection changed replay identity.");
    const reset = JSON.parse(await bridge.dispatchJson('{"protocol":"candu-playtest-v2","type":"reset","seed":43}'));
    digest = await hash(algorithm + "|" + policy + '|play|{"protocol":"candu-playtest-v2","mode":"play","seed":43,"shiftId":"free-practice"}');
    digest = await hash(digest + "|" + JSON.stringify(sorted(reset.command)));
    check(reset.sequence === 1 && reset.replayDigest === digest, "Reset digest mismatch.");
    return { algorithm, initialization: true, acceptedAndRejected: true, earlyRejection: true, reset: true };
  });
  if (errors.length) throw new Error(errors.join("\n"));
  console.log(JSON.stringify({ ...result, browserErrors: errors.length }, null, 2));
} finally {
  await browser.close();
}
