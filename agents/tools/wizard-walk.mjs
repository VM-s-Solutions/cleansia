/**
 * Walk the booking wizard forward the way a visitor does.
 *
 * Shared by the three checkers, because all three had the same hole: each one
 * measured whatever page it landed on, and a wizard's later steps are reachable
 * only through the earlier ones. Every step past the first was reporting clean
 * without ever being looked at.
 *
 * Where a step gates on real input this supplies real input rather than reaching
 * past the gate — the address is a genuine geocoded pick, because the wizard
 * refuses one that is only typed, and refusing it is correct.
 */

/** What the page says is still missing, for a walk that cannot go on. */
export async function blockedReasons(page) {
  const items = page.locator('.cl-wiz__blocked li');
  if (!(await items.count())) return 'no reason given';
  return (await items.allInnerTexts()).join('; ');
}

/**
 * Fill the address step, if that is where the walk currently is.
 *
 * Only a suggestion pick writes the coordinates the step requires, so typing the
 * same characters is not the same thing.
 */
async function fillAddressStep(page) {
  const field = page.locator('.cl-wiz__addr-main input').first();
  if (!(await field.count())) return;
  if (await page.locator('.cl-wiz__addr-resolved').count()) return;

  await field.click().catch(() => {});
  await field.type('Zenklova', { delay: 40 });
  await page.waitForTimeout(2200);
  const option = page
    .locator('.p-autocomplete-option, .p-autocomplete-item, li[role="option"]')
    .first();
  if (await option.count()) {
    await option.click().catch(() => {});
    await page.waitForTimeout(800);
  }

  const contact = {
    '#wizard-first-name': 'Jana',
    '#wizard-last-name': 'Nováková',
    '#wizard-email': 'jana@example.test',
  };
  for (const [selector, value] of Object.entries(contact)) {
    const input = page.locator(selector);
    if (await input.count()) await input.fill(value).catch(() => {});
  }
  const phone = page.locator('#wizard-phone input, cleansia-telephone input').first();
  if (await phone.count()) await phone.fill('739788108').catch(() => {});
  await page.waitForTimeout(600);
}

/**
 * Pick a date and a way in, if the walk is on the scheduling step.
 *
 * The artboard draws that step with both already chosen, and a selected chip is
 * a different box from an unselected one — so a check against an untouched step
 * compares two states, not two designs.
 */
async function fillWhenStep(page) {
  const day = page
    .locator('.cl-wiz__cal-day:not([disabled]):not(.cl-wiz__cal-day--blank)')
    .first();
  if (!(await day.count())) return;

  if (!(await page.locator('.cl-wiz__cal-day--on').count())) {
    await day.click().catch(() => {});
    await page.waitForTimeout(500);
  }
  const access = page.locator('.cl-wiz__access-chip').first();
  if (
    (await access.count()) &&
    !(await page.locator('.cl-wiz__access-chip.cl-chip--on').count())
  ) {
    await access.click().catch(() => {});
    await page.waitForTimeout(300);
  }
}

/** Everything a step needs before it will let the walk continue. */
async function fillCurrentStep(page) {
  await fillAddressStep(page);
  await fillWhenStep(page);
}

/**
 * Advance `steps` times, filling each step on the way. Returns the reason the
 * walk stopped early, or null if it went the whole way.
 */
export async function walkWizard(page, steps, { log = () => {} } = {}) {
  if (steps <= 0) return null;

  // Confirm the click landed rather than assuming it: a click issued before the
  // catalogue has finished rendering resolves against a button that is replaced
  // a moment later, and the walk then stops on step one with "choose a service"
  // — intermittently, and only on whichever locale happened to load slowest.
  for (let attempt = 0; attempt < 3; attempt += 1) {
    if (await page.locator('[data-spec-select][aria-pressed="true"]').count()) break;
    const pick = page.locator('[data-spec-select]').first();
    if (!(await pick.count())) {
      await page.waitForTimeout(600);
      continue;
    }
    await pick.click().catch(() => {});
    await page.waitForTimeout(600);
  }

  for (let i = 0; i < steps; i += 1) {
    await page.waitForTimeout(500);
    await fillCurrentStep(page);

    const next = page.locator('[data-spec-advance]').first();
    if (!(await next.count())) return 'no advance control on the page';

    // The button is no longer disabled when the step is incomplete — clicking it
    // is how the customer asks, and being told why is the answer. So the walk
    // clicks and then reads whether the step moved, rather than asking the
    // button whether it would have worked.
    await next.click().catch(() => {});
    await page.waitForTimeout(700);
    if (await page.locator('.cl-wiz__blocked').count()) {
      const why = await blockedReasons(page);
      log(`  (advance ${i + 1} refused: ${why})`);
      return why;
    }
  }

  // Once more after the last advance: the step just arrived at has had no pass.
  await page.waitForTimeout(500);
  await fillCurrentStep(page);
  await page.waitForTimeout(400);
  return null;
}
