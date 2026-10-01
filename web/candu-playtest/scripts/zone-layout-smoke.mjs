/** Verify live region editing and draft-only absorber operations against WASM. */
export async function verifyZoneLayout(page) {
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  await page.keyboard.press("z");
  const editor = page.locator("dialog.zone-layout-editor");
  await editor.waitFor({ state: "visible" });
  check(await editor.locator("rect[data-channel]").count() === 380, "Zone map did not project 380 live channels.");
  check((await editor.locator('[data-field="coverage"]').textContent()).includes("100.0%"), "Default absorber footprint should expose full-core coverage.");
  await editor.locator('[data-field="slice"]').selectOption("6");
  check((await editor.locator('[data-field="cell"]').textContent()).includes("bundle 7"), "Axial slice did not change.");
  await editor.locator('[data-field="slice"]').selectOption("0");
  const before = await editor.locator('[data-field="cell"]').textContent();
  await editor.locator('[data-field="zone"]').selectOption("5");
  await editor.locator('[data-action="paint"]').click();
  check((await editor.locator('[data-field="cell"]').textContent()).includes("region Z6"), "Region draft was not repainted.");
  await editor.locator('[data-action="undo"]').click();
  check(await editor.locator('[data-field="cell"]').textContent() === before, "Undo did not restore the geometry draft.");
  await editor.locator('[data-action="paint"]').click();
  await editor.locator('[data-action="apply"]').click();
  await page.waitForFunction(() => document.querySelector('.zone-layout-editor [data-field="message"]')?.textContent?.startsWith("Applied:"), undefined, { timeout: 60_000 });
  await editor.locator('[data-action="reload"]').click();
  check((await editor.locator('[data-field="cell"]').textContent()).includes("region Z6"), "Applied region did not round-trip through the authoritative snapshot.");
  await editor.locator('[data-field="mode"]').selectOption("absorber");
  await editor.locator('[data-action="clear"]').click();
  check((await editor.locator('[data-field="coverage"]').textContent()).includes("4559 / 4560"), "Clearing a draft absorber did not change coverage.");
  await editor.locator('[data-action="undo"]').click();
  check((await editor.locator('[data-field="coverage"]').textContent()).includes("4560 / 4560"), "Absorber undo did not restore coverage.");
  await editor.locator('[data-field="dx"]').fill("30");
  await editor.locator('[data-action="move"]').click();
  check((await editor.locator('[data-field="message"]').textContent()).includes("outside the core"), "Off-core move was not rejected in the draft.");
  if (process.env.PLAYTEST_CAPTURE_DIR) await page.screenshot({ path: `${process.env.PLAYTEST_CAPTURE_DIR}/zone-layout-1600.png` });
  await page.setViewportSize({ width: 1280, height: 720 });
  check(await editor.evaluate(e => e.scrollWidth <= e.clientWidth), "Zone editor overflows at 1280px.");
  if (process.env.PLAYTEST_CAPTURE_DIR) await page.screenshot({ path: `${process.env.PLAYTEST_CAPTURE_DIR}/zone-layout-1280.png` });
  await page.keyboard.press("Escape");
  await editor.waitFor({ state: "detached" });
  await page.setViewportSize({ width: 1600, height: 900 });
  return { channels: 380, axialSlices: 12, regionCommit: "accepted", absorberDraft: "editable", invalidMove: "retained" };
}
