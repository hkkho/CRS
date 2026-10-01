/** Exercise the alternative view in the same live session as Tactical/Designer. */
export async function verifyStudio(page) {
  const check = (condition, message) => { if (!condition) throw new Error(message); };
  await page.keyboard.press("Escape"); // Return from Designer to Operations.
  await page.waitForTimeout(200); // Phaser commits scene transitions on its next frame.
  await page.keyboard.press("v");
  const studio = page.locator(".reactor-studio");
  await studio.waitFor({ state: "visible" });
  check(await studio.locator("circle[data-channel]").count() === 380, "Studio did not render 380 authoritative channels.");
  check(await studio.locator(".studio-zone").count() === 14, "Studio did not render 14 zone fills.");
  check((await studio.locator('[data-field="power-rating"]').textContent()).includes("MW electric") &&
        (await studio.locator('[data-field="power-rating"]').textContent()).includes("MW thermal"),
        "Studio did not distinguish electrical output from thermal fission power.");
  await studio.locator('[data-action="pause"]').click();
  await page.waitForFunction(() => document.querySelector("#status-mirror")?.textContent?.includes("Paused."));
  await studio.locator('[data-action="oldest"]').click();
  const chosen = await studio.locator('[data-field="channel"]').textContent();
  const burnupFill = await studio.locator('circle[tabindex="0"]').getAttribute("fill");
  await studio.locator('[data-map="power"]').click();
  check(await studio.locator('circle[tabindex="0"]').getAttribute("fill") !== burnupFill, "Studio power map did not update.");
  await studio.locator('[data-map="burnup"]').click();
  await studio.locator('[data-size="8"]').click();
  await studio.locator('[data-action="direction"]').click();
  const direction = await studio.locator('[data-field="direction"]').textContent();
  await studio.locator('[data-action="classic"]').click();
  await studio.waitFor({ state: "detached" });
  // Return without resetting or replacing the authoritative session.
  await page.keyboard.press("v");
  await studio.waitFor({ state: "visible" });
  check(await studio.locator('[data-field="channel"]').textContent() === chosen, "Switching views lost the selected channel.");
  check(await studio.locator('[data-field="direction"]').textContent() === direction, "Switching views lost refuel direction.");
  check(await studio.locator('[data-size="8"]').getAttribute("aria-pressed") === "true", "Switching views lost shift size.");
  await studio.locator('[data-action="refuel"]').click();
  await page.waitForFunction(() => document.querySelector("#status-mirror")?.textContent?.includes("120 fresh bundles. 1 refuelling operations."), undefined, { timeout: 60_000 });
  check((await studio.locator('[data-field="impact"]').textContent()).includes("128 → 120"), "Studio did not show the accepted fuel impact.");
  check(await studio.locator('.studio-bundle-rack > div').count() === 12, "Studio bundle rack did not render 12 positions.");
  await page.setViewportSize({ width: 1600, height: 900 });
  await page.waitForTimeout(200);
  if (process.env.PLAYTEST_CAPTURE_DIR) await page.screenshot({ path: `${process.env.PLAYTEST_CAPTURE_DIR}/studio-1600.png` });
  await page.setViewportSize({ width: 1280, height: 720 });
  await page.waitForTimeout(200);
  check(await studio.evaluate(element => element.scrollWidth <= element.clientWidth), "Studio overflows horizontally at 1280px.");
  if (process.env.PLAYTEST_CAPTURE_DIR) await page.screenshot({ path: `${process.env.PLAYTEST_CAPTURE_DIR}/studio-1280.png` });
  await studio.locator("summary").click();
  await studio.locator('[data-action="step"]').click();
  // One hour crosses two equilibrium boundaries. Wait for the authoritative
  // WASM transaction to finish before issuing the next command.
  await page.waitForFunction(() => {
    const button = document.querySelector('.reactor-studio [data-action="pause"]');
    return button instanceof HTMLButtonElement && !button.disabled;
  }, undefined, { timeout: 120_000 });
  await studio.locator('[data-action="pause"]').click();
  await page.waitForFunction(() => document.querySelector("#status-mirror")?.textContent?.includes("Running."));
  await studio.locator('[data-field="target-input"]').fill("95");
  await studio.locator('[data-action="target"]').click();
  await page.waitForFunction(() => document.querySelector('[data-field="power-target"]')?.textContent === "95.0%", undefined, { timeout: 60_000 });
  await studio.locator('[data-action="pause"]').click();
  await page.waitForFunction(() => document.querySelector("#status-mirror")?.textContent?.includes("Paused."));
  // Keyboard map selection retains focus through periodic snapshot renders.
  const focusedChannel = studio.locator('circle[tabindex="0"]');
  await focusedChannel.focus();
  await focusedChannel.press("ArrowRight");
  check(await studio.locator('[data-field="channel"]').textContent() !== chosen, "Studio arrow selection did not move.");
  await studio.locator('[data-action="designer"]').click();
  await studio.waitFor({ state: "detached" });
  await page.keyboard.press("Escape");
  await studio.waitFor({ state: "visible" });
  check(await studio.locator('[data-field="stock"]').textContent() === "120", "Designer round trip lost the Studio fuel inventory.");
  await studio.locator('[data-action="reset"]').click();
  await page.waitForFunction(() => document.querySelector("#status-mirror")?.textContent?.includes("128 fresh bundles. 0 refuelling operations."), undefined, { timeout: 60_000 });
  await studio.locator('[data-action="pause"]').click();
  await page.waitForFunction(() => document.querySelector("#status-mirror")?.textContent?.includes("Paused."));
  await studio.locator('[data-action="classic"]').click();
  await studio.waitFor({ state: "detached" });
  await page.setViewportSize({ width: 1600, height: 900 });
  return { channels: 380, zones: 14, refuel: "accepted", sessionPreserved: true, desktopSizes: ["1600x900", "1280x720"] };
}
