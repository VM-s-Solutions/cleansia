import { expect, Page, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { at, call, DEV_ADMIN, errorKeys, list, named, num, ORDER_STATUS, PAYMENT_STATUS, poll, Session, signIn, str } from '../support/dev';
import { cancelSplit } from '../support/plan';
import { emailOf, flag, loadRun, Role, RunOrder, saveRun, scratch, setFlag } from '../support/run';
import { pragueSlot, wizardLabels } from '../support/slots';
import { acceptCookies, adminSignIn, cachedAdmin, customerSignIn, SITES } from '../support/web';

// Stage 'orders', web half: the customer web's booking wizard rules (level, lead time,
// grid, window, horizon, express, two-cleaner cash, cash cap, what is owed, the guest's terms tick), the saved cards
// page, F1's and tierB's cancellation in the web dialog, and the admin SPA's view of the takes, drop and placement.
// Runs at 07:35, 07:58 and 08:15 on Day 1 after the API files of the same invocation; each test skips until its gate.
// Not serial: every test has its own gate, so one failure does not skip the tests after it (one worker keeps the order).
// A DEV page that hangs fails the step in 90 s, not at the 10-minute test timeout (support/web.ts waits on navigation).
test.use({ timezoneId: 'Europe/Prague', navigationTimeout: 90_000, actionTimeout: 90_000 });
const run = loadRun();
const BATHROOM = 'Bathroom Cleaning';
const HOUR = 3_600_000;
const catalogue = JSON.parse(readFileSync(join(__dirname, '..', '..', '..', 'cleansia.app', 'src', 'assets', 'i18n', 'en.json'), 'utf8'));
const t = (key: string) => String(at(catalogue, key) ?? key);
const pragueDate = (d: Date) => d.toLocaleDateString('en-CA', { timeZone: 'Europe/Prague' });

const adminDetail = async (id: string) => (await call(await cachedAdmin(), 'GET', `/api/AdminOrder/details/${id}`)).body;
const statusOf = (detail: unknown) => named(detail, 'orderStatus', ORDER_STATUS);
const numberOf = (detail: unknown) => str(detail, 'displayOrderNumber') || str(detail, 'orderNumber');

function planned(key: string): RunOrder {
  const o = run.orders[key];
  test.skip(!o, `run ${run.runId} has no ${key} order yet (orders/10)`);
  return o;
}

// The wizard, one step at a time (the steps of order-wizard.component.html).

const wizard = {
  open: async (page: Page) => {
    await page.goto(`${SITES.customer}/order`);
    await acceptCookies(page);
  },
  next: (page: Page) => page.getByRole('button', { name: 'Continue', exact: true }).click(),
  service: (page: Page, name: string) => page.getByRole('button', { name: `Select service: ${name}` }).click(),
  counts: async (page: Page, rooms: number, bathrooms: number) => {
    await page.getByRole('button', { name: `Set number of rooms: ${rooms}` }).filter({ visible: true }).first().click();
    await page.getByRole('button', { name: `Set number of bathrooms: ${bathrooms}` }).filter({ visible: true }).first().click();
  },
  level: (page: Page, name: 'Normal' | 'Increased' | 'Heavy') => page.getByRole('button', { name: new RegExp(`^${name}`) }).click(),
  savedAddress: async (page: Page) => {
    await page.getByRole('button', { name: /^Use saved address/ }).first().click();
    await page.locator('input#wizard-phone').fill('777123456');
  },
  /** Shows the slot's day in the calendar, moving forward a month when it is not on the visible one. */
  day: async (page: Page, slot: Date) => {
    const day = page.getByRole('button', { name: wizardLabels(slot).day, exact: true });
    await page.getByRole('button', { name: 'Next month' }).waitFor();
    if (!(await day.isVisible())) await page.getByRole('button', { name: 'Next month' }).click();
    await day.click();
  },
  time: async (page: Page, slot: Date) => {
    const when = wizardLabels(slot);
    await page.getByRole('button', { name: new RegExp(`^${when.part}`) }).click();
    await page.getByRole('button', { name: when.time, exact: true }).click();
    await page.getByRole('button', { name: 'I will be home' }).click();
  },
  pay: (page: Page, payment: 'card' | 'cash') => page.getByRole('button', { name: payment === 'card' ? /^Card online/ : /^Cash on the day/ }),
};

/** Every time chip of the selected day, part by part: its label and whether it can be picked. */
async function wizardTimes(page: Page) {
  const out: { label: string; enabled: boolean }[] = [];
  const parts = page.locator('.cl-wiz__daypart');
  for (let i = 0; i < (await parts.count()); i++) {
    if (await parts.nth(i).isDisabled()) continue;
    // The chips re-render for the part clicked: wait until they start where its range says.
    const [from] = (await parts.nth(i).locator('.cl-wiz__daypart-range').innerText()).split(/[–-]/).map((x) => x.trim());
    await parts.nth(i).click();
    await expect(page.locator('.cl-wiz__time').first()).toHaveText(from);
    for (const chip of await page.locator('.cl-wiz__time').all()) out.push({ label: (await chip.innerText()).trim(), enabled: await chip.isEnabled() });
  }
  return out;
}

/** A booking walked to the payment step: Bathroom Cleaning, Normal, the saved address, the slot. */
async function toPayment(page: Page, slot: Date) {
  await wizard.open(page);
  await wizard.service(page, BATHROOM);
  await wizard.next(page);
  await wizard.level(page, 'Normal');
  await wizard.next(page);
  await wizard.savedAddress(page);
  await wizard.next(page);
  await wizard.day(page, slot);
  await wizard.time(page, slot);
  await wizard.next(page);
  await expect(wizard.pay(page, 'card')).toBeVisible();
}

test.describe('booking wizard rules', () => {
  test('the level step preselects nothing and holds until a level is chosen; the rail prices it; times sit on the 15-minute grid from 08:00 to 19:45', { tag: ['@E2E-BOOK-011', '@E2E-BOOK-018', '@E2E-BOOK-019'] }, async ({ page }) => {
    await customerSignIn(page, emailOf(run, 'customer'), run.password);
    await wizard.open(page);
    await wizard.service(page, BATHROOM);
    await wizard.next(page);
    await expect(page.getByText('How clean is your home?')).toBeVisible();
    for (const level of ['Normal', 'Increased', 'Heavy']) await expect(page.getByRole('button', { name: new RegExp(`^${level}`) })).toHaveAttribute('aria-pressed', 'false');
    await wizard.next(page);
    await expect(page.getByText('How clean is your home?')).toBeVisible();
    await expect(page.locator('.cl-wiz__blocked')).toContainText(t('pages.order.missing.dirtiness'));
    await wizard.level(page, 'Increased');
    await wizard.next(page);
    await wizard.savedAddress(page);
    await wizard.next(page);

    // A day well ahead: no lead time greys any of it.
    const ahead = new Date(Date.now() + 3 * 24 * HOUR);
    await wizard.day(page, ahead);
    const times = await wizardTimes(page);
    const grid = Array.from({ length: 48 }, (_, i) => `${String(8 + Math.floor(i / 4)).padStart(2, '0')}:${String((i % 4) * 15).padStart(2, '0')}`);
    expect(times.map((x) => x.label)).toEqual(grid);
    expect(times.every((x) => x.enabled)).toBe(true);

    await wizard.time(page, pragueSlot(pragueDate(ahead), '10:00'));
    const rail = page.locator('aside');
    await expect(rail).toContainText(t('pages.order.dirtiness.increased.surcharge_line'));
    await expect(rail).toContainText('+15%');
  });

  test('today offers only times at least 2 hours away', { tag: ['@E2E-BOOK-014'] }, async ({ page }) => {
    const now = new Date();
    const today = pragueDate(now);
    test.skip(pragueSlot(today, '19:45').getTime() - now.getTime() < 2 * HOUR + 60_000, 'past 17:44 Prague today has no time left to offer');
    await customerSignIn(page, emailOf(run, 'customer'), run.password);
    await toPaymentStepDate(page);
    await wizard.day(page, now);
    const times = await wizardTimes(page);
    const opened = Date.now();
    for (const x of times) {
      const lead = pragueSlot(today, x.label).getTime() - opened;
      // Within a minute of the boundary the page and this clock may disagree; nothing is asserted there.
      if (Math.abs(lead - 2 * HOUR) < 60_000) continue;
      expect(x.enabled, `${x.label} today, ${Math.round(lead / 60_000)} minutes away`).toBe(lead >= 2 * HOUR);
    }
    expect(times.some((x) => x.enabled)).toBe(true);
  });

  test('a time 2 to 4 hours away is marked express and the rail adds the surcharge', { tag: ['@E2E-BOOK-015'] }, async ({ page }) => {
    const now = new Date();
    const today = pragueDate(now);
    const express = Array.from({ length: 48 }, (_, i) => pragueSlot(today, `${String(8 + Math.floor(i / 4)).padStart(2, '0')}:${String((i % 4) * 15).padStart(2, '0')}`))
      .find((s) => s.getTime() - now.getTime() >= 2 * HOUR + 2 * 60_000 && s.getTime() - now.getTime() < 4 * HOUR - 2 * 60_000);
    test.skip(!express, 'no express time is left today: express runs from 04:00 to 15:45 UTC');
    await customerSignIn(page, emailOf(run, 'customer'), run.password);
    await toPaymentStepDate(page);
    await wizard.day(page, now);
    await wizard.time(page, express as Date);
    await expect(page.getByText('This slot is express — +20% is added to the price.')).toBeVisible();
    await expect(page.locator('aside')).toContainText(t('pages.order.summary_express'));
  });

  test('the calendar stops at the last bookable day, 59 days ahead, and the home quick quote too', { tag: ['@E2E-BOOK-021'] }, async ({ page, browser }) => {
    // lastBookableDay (booking-window.models.ts): the calendar day of now + 60 days, less one.
    const horizon = new Date(Date.now() + 60 * 24 * HOUR);
    const last = new Date(Date.parse(`${pragueDate(horizon)}T12:00:00Z`) - 24 * HOUR);
    const after = new Date(last.getTime() + 24 * HOUR);
    await customerSignIn(page, emailOf(run, 'customer'), run.password);
    await toPaymentStepDate(page);
    const nextMonth = page.getByRole('button', { name: 'Next month' });
    const lastDay = page.getByRole('button', { name: wizardLabels(last).day, exact: true });
    // Forward a month at a time until the last day shows; the arrow cannot pass the horizon month, so a retry is harmless.
    await expect(async () => {
      if (!(await lastDay.isVisible())) await nextMonth.click({ timeout: 2_000 });
      await expect(lastDay).toBeVisible({ timeout: 2_000 });
    }).toPass({ timeout: 60_000 });
    await expect(lastDay).toBeEnabled();
    await expect(nextMonth).toBeDisabled();
    if (pragueDate(after).slice(0, 7) === pragueDate(last).slice(0, 7)) await expect(page.getByRole('button', { name: wizardLabels(after).day, exact: true })).toBeDisabled();

    // The quick quote on the home page, anonymous: PrimeNG's date picker, maxDate = the same last day.
    const ctx = await browser.newContext({ locale: 'en-US', timezoneId: 'Europe/Prague' });
    ctx.setDefaultTimeout(90_000);
    const home = await ctx.newPage();
    await home.goto(SITES.customer);
    await acceptCookies(home);
    await home.locator('.cl-quote__date').click();
    const panel = home.locator('.p-datepicker-panel, .p-datepicker').filter({ has: home.locator('.p-datepicker-next-button') }).last();
    const monthName = last.toLocaleDateString('en-US', { month: 'long', timeZone: 'Europe/Prague' });
    const year = pragueDate(last).slice(0, 4);
    for (let i = 0; i < 3; i++) {
      if ((await panel.locator('.p-datepicker-select-month').innerText()).trim() === monthName && (await panel.locator('.p-datepicker-select-year').innerText()).trim() === year) break;
      await panel.locator('.p-datepicker-next-button').click();
    }
    const cell = (d: Date) => panel.locator(`td.p-datepicker-day-cell:not(.p-datepicker-other-month)[aria-label="${Number(pragueDate(d).slice(8))}"] span.p-datepicker-day`);
    await expect(cell(last)).not.toHaveClass(/p-disabled/);
    if (pragueDate(after).slice(0, 7) === pragueDate(last).slice(0, 7)) await expect(cell(after)).toHaveClass(/p-disabled/);
    await ctx.close();
  });

  test('a guest cannot place the booking until the terms are ticked', { tag: ['@E2E-BOOK-046'] }, async ({ page }) => {
    const creates: string[] = [];
    page.on('request', (r) => {
      if (r.method() === 'POST' && new URL(r.url()).pathname.endsWith('/api/Order/CreateOrder')) creates.push(r.url());
    });
    const slot = pragueSlot(pragueDate(new Date(Date.now() + 2 * 24 * HOUR)), '10:00');
    await wizard.open(page);
    await wizard.service(page, BATHROOM);
    await wizard.next(page);
    await wizard.level(page, 'Normal');
    await wizard.next(page);
    await page.getByRole('button', { name: t('pages.order.address_enter_manually') }).click();
    await page.locator('input#wizard-street').fill('Václavské náměstí 1');
    await page.locator('input#wizard-city').fill('Praha');
    await page.locator('input#wizard-zip').fill('11000');
    await page.locator('input#wizard-first-name').fill('E2E');
    await page.locator('input#wizard-last-name').fill('Guest');
    await page.locator('input#wizard-email').fill(emailOf(run, 'guest'));
    await page.locator('input#wizard-phone').fill('777123456');
    await wizard.next(page);
    await wizard.day(page, slot);
    await wizard.time(page, slot);
    await wizard.next(page);
    await wizard.pay(page, 'card').click();
    await wizard.next(page);
    const skipPlus = page.getByRole('button', { name: 'Continue without Plus' });
    if (await skipPlus.isVisible().catch(() => false)) await skipPlus.click();
    else await wizard.next(page);
    await page.locator('input#wizard-early-performance').check();
    const terms = page.locator('input#wizard-terms');
    await expect(terms).not.toBeChecked();
    await page.getByRole('button', { name: 'Place Order' }).click();
    await expect(page.locator('.cl-wiz__blocked')).toContainText(t('pages.order.missing.terms'));
    expect(creates, 'a booking without the tick reached CreateOrder').toEqual([]);
    await terms.check();
    await expect(page.getByText(t('pages.order.missing.terms'))).toHaveCount(0);
    expect(creates).toEqual([]);
  });
});

/** Signed in, walked to the date step with Bathroom Cleaning, Normal and the saved address. */
async function toPaymentStepDate(page: Page) {
  await wizard.open(page);
  await wizard.service(page, BATHROOM);
  await wizard.next(page);
  await wizard.level(page, 'Normal');
  await wizard.next(page);
  await wizard.savedAddress(page);
  await wizard.next(page);
  await expect(page.getByText(t('pages.order.arrival_time_label'))).toBeVisible();
}

test.describe('cash and what is owed', () => {
  test('cash is refused for a two-cleaner booking, and a cash choice made earlier is cleared, not turned into card', { tag: ['@E2E-BOOK-035'] }, async ({ page }) => {
    test.skip(scratch(), 'E2E_SCRATCH=1: a scratch run has no two-cleaner service');
    test.skip(!run.serviceId, `run ${run.runId} has no two-cleaner service yet (people/50)`);
    test.skip(!!flag(run, 'book035.webDone'), `shown at ${flag(run, 'book035.webDone')}`);
    const service = `E2E two-cleaner ${run.runId}`;
    await customerSignIn(page, emailOf(run, 'customer'), run.password);
    await toPayment(page, pragueSlot(pragueDate(new Date(Date.now() + 2 * 24 * HOUR)), '10:00'));
    await wizard.pay(page, 'cash').click();
    await expect(wizard.pay(page, 'cash')).toHaveAttribute('aria-pressed', 'true');
    // Back to the services: the rail's first step, then the two-cleaner service at 1 room and 1 bathroom (121 minutes).
    await page.locator('button.cl-wiz__step').first().click();
    await wizard.service(page, service);
    await wizard.counts(page, 1, 1);
    for (let i = 0; i < 4; i++) await wizard.next(page);
    await expect(wizard.pay(page, 'cash')).toBeDisabled();
    await expect(page.locator('#wizard-cash-reason')).toContainText('This booking needs 2 cleaners. Pay by card.');
    await expect(page.getByRole('status').getByText(t('pages.order.cash_cleared'))).toBeVisible();
    await expect(wizard.pay(page, 'card')).toHaveAttribute('aria-pressed', 'false');
    setFlag(run, 'book035.webDone');
  });

  test('a customer at the cash cap is refused a third unpaid cash booking, translated', { tag: ['@E2E-BOOK-037'] }, async ({ page }) => {
    const cash = Object.values(run.orders).filter((o) => o.customer === 'cashcap' && o.payment === 'cash');
    const open: RunOrder[] = [];
    for (const o of cash) {
      const d = await adminDetail(o.id);
      if (statusOf(d) !== 'Cancelled' && named(d, 'paymentStatus', PAYMENT_STATUS) === 'Pending') open.push(o);
    }
    test.skip(open.length < 2, `cashcap of run ${run.runId} holds ${open.length} unpaid cash booking(s); the cap is 2 (orders/10)`);
    let created = 0;
    page.on('response', (r) => {
      if (new URL(r.url()).pathname.endsWith('/api/Order/CreateOrder') && r.status() === 200) created++;
    });
    await customerSignIn(page, emailOf(run, 'cashcap'), run.password);
    await toPayment(page, pragueSlot(pragueDate(new Date(Date.now() + 2 * 24 * HOUR)), '11:45'));
    await wizard.pay(page, 'cash').click();
    await wizard.next(page);
    const skipPlus = page.getByRole('button', { name: 'Continue without Plus' });
    if (await skipPlus.isVisible().catch(() => false)) await skipPlus.click();
    else await wizard.next(page);
    await page.locator('input#wizard-early-performance').check();
    if (await page.locator('input#wizard-terms').isVisible()) await page.locator('input#wizard-terms').check();
    const refused = page.waitForResponse((r) => new URL(r.url()).pathname.endsWith('/api/Order/CreateOrder'));
    await page.getByRole('button', { name: 'Place Order' }).click();
    const reply = await refused;
    expect(reply.status()).toBe(400);
    expect(errorKeys(await reply.json())).toContain('order.cash_open_bookings_limit_reached');
    await expect(page.getByText(t('api.order.cash_open_bookings_limit_reached'))).toBeVisible();
    expect(created).toBe(0);
    setFlag(run, 'book037.webDone');
  });

  test('someone who owes is shown what and offered to pay it on the payment step', { tag: ['@E2E-BOOK-038'] }, async ({ page }) => {
    test.skip(!flag(run, 'd2.receivableOpen'), 'D2\'s cancellation fee is not open yet (orders/30)');
    test.skip(!!flag(run, 'receivable.webShown'), `shown at ${flag(run, 'receivable.webShown')}`);
    await customerSignIn(page, emailOf(run, 'debtor'), run.password);
    await toPayment(page, pragueSlot(pragueDate(new Date(Date.now() + 2 * 24 * HOUR)), '12:00'));
    await expect(page.getByText(t('pages.order.unpaid_owed'))).toBeVisible();
    const due = page.locator('cleansia-customer-amount-due');
    await expect(due).toContainText(t('pages.amount_due.kind.cash_cancellation_fee'));
    await expect(due.getByRole('button', { name: t('pages.amount_due.pay') }).or(due.getByRole('link', { name: t('pages.amount_due.pay') }))).toBeVisible();
    setFlag(run, 'receivable.webShown');
  });
});

test.describe('saved cards', () => {
  test('a card saved at a ticked payment stays on the profile after a reload', { tag: ['@E2E-PAY-022'] }, async ({ page }) => {
    const first = await signIn('customer', emailOf(run, 'first'), run.password).catch(() => undefined);
    test.skip(!first, `first of run ${run.runId} cannot sign in yet (people/10, people/30)`);
    const cards = list((await call(first as Session, 'GET', '/api/SavedCard/GetMine')).body, '');
    test.skip(!cards.length, 'first has no saved card yet: F1 is paid with the save ticked in orders/10');
    await customerSignIn(page, emailOf(run, 'first'), run.password);
    await page.goto(`${SITES.customer}/profile`);
    await expect(page.getByText(/ending in 4242/i)).toBeVisible();
    await page.reload();
    await expect(page.getByText(/ending in 4242/i)).toBeVisible();
    await expect(page.getByText(t('pages.profile.saved_cards.empty'))).toHaveCount(0);
  });

  test('a card paid without the tick is not kept', { tag: ['@E2E-PAY-023'] }, async ({ page }) => {
    const r1 = planned('R1');
    test.skip(named(await adminDetail(r1.id), 'paymentStatus', PAYMENT_STATUS) === 'Pending', 'R1 is not paid yet (orders/10)');
    await customerSignIn(page, emailOf(run, 'nocard'), run.password);
    await page.goto(`${SITES.customer}/profile`);
    await expect(page.getByText(t('pages.profile.saved_cards.empty'))).toBeVisible();
    await expect(page.getByText(/ending in \d{4}/i)).toHaveCount(0);
  });
});

/** Cancels an order in the customer web's dialog and returns the server's answer to the cancel. */
async function cancelOnWeb(page: Page, customer: Role, o: RunOrder, expectTexts: string[]) {
  await customerSignIn(page, emailOf(run, customer), run.password);
  await page.goto(`${SITES.customer}/orders/${o.id}`);
  await page.getByRole('button', { name: 'Cancel booking' }).click();
  const dialog = page.getByRole('dialog');
  for (const text of expectTexts) await expect(dialog).toContainText(text);
  await dialog.locator('textarea').fill(`E2E run ${run.runId} web`);
  const answer = page.waitForResponse((r) => r.request().method() === 'POST' && new URL(r.url()).pathname.endsWith('/api/Order/Cancel'));
  await dialog.getByRole('button', { name: 'Confirm cancellation' }).click();
  const reply = await answer;
  expect(reply.status()).toBe(200);
  await expect(dialog).toBeHidden({ timeout: 60_000 });
  o.cancelled = true;
  saveRun(run);
  return (await reply.json()) as unknown;
}

test.describe('cancelling on the web', () => {
  test('F1, a first booking a cleaner took, is cancelled free inside 60 minutes, and the dialog says so', { tag: ['@E2E-CANCEL-002', '@E2E-CANCEL-005'] }, async ({ page }) => {
    const o = planned('F1');
    test.skip(!flag(run, 'f1.previewed'), 'F1 has not been previewed yet (orders/30)');
    test.skip(!!flag(run, 'f1.webCancelled'), `cancelled at ${flag(run, 'f1.webCancelled')}`);
    const left = Date.parse(o.createdAt ?? '') + 60 * 60_000 - Date.now();
    test.skip(left < 2 * 60_000, `F1 was booked at ${o.createdAt}: its 60-minute oops window has closed`);
    const body = await cancelOnWeb(page, 'first', o, [t('pages.order_detail.cancellation.tier.1'), 'Cancelling within 60 minutes of booking is free, even after a cleaner has accepted.', '(0%)']);
    expect(num(body, 'feeRate')).toBe(0);
    expect(num(body, 'refundAmount')).toBeCloseTo(o.total, 2);
    setFlag(run, 'f1.webCancelled');
  });

  test('tierB is cancelled 4 to 24 hours ahead at 25 %, and the refund lands as a cancellation refund', { tag: ['@E2E-CANCEL-006', '@E2E-CANCEL-005'] }, async ({ page, browser }) => {
    const o = planned('tierB');
    test.skip(!flag(run, 'tierB.previewed'), 'tierB has not been previewed yet (orders/30)');
    test.skip(!!flag(run, 'tierB.webCancelled'), `cancelled at ${flag(run, 'tierB.webCancelled')}`);
    const oopsEnds = Date.parse(o.createdAt ?? '') + 16 * 60_000;
    test.skip(Date.now() < oopsEnds, `tierB's 15-minute oops window runs until ${new Date(oopsEnds).toISOString()}`);
    const notice = Date.parse(o.slot) - Date.now();
    test.skip(notice < 4 * HOUR + 5 * 60_000 || notice > 24 * HOUR, `tierB's slot ${o.slot} is not 4 to 24 hours away`);
    const body = await cancelOnWeb(page, 'customer', o, [t('pages.order_detail.cancellation.tier.3'), 'Cancelling within 15 minutes of booking is free', '(25%)']);
    expect(num(body, 'feeRate')).toBe(0.25);
    expect(num(body, 'refundAmount')).toBeCloseTo(cancelSplit(o.total, 0.25).refund, 2);
    expect(at(body, 'refundPending')).toBe(false);
    setFlag(run, 'tierB.webCancelled');

    const refund = await poll(
      async () => list((await call(await cachedAdmin(), 'GET', `/api/AdminRefund/get-paged?Offset=0&Limit=5&Filter.OrderId=${o.id}`)).body, 'data')[0],
      (r) => str(r, 'state.name') === 'Refunded',
    );
    expect(str(refund, 'state.name')).toBe('Refunded');
    expect(named(refund, 'origin', ['', 'Cancellation', 'Dispute', 'FullRefund', 'PartialRefund'])).toBe('Cancellation');
    const detail = await adminDetail(o.id);
    expect(statusOf(detail)).toBe('Cancelled');
    expect(named(detail, 'paymentStatus', PAYMENT_STATUS)).toBe('PartiallyRefunded');

    const ctx = await browser.newContext({ locale: 'en-US', timezoneId: 'Europe/Prague' });

    ctx.setDefaultTimeout(90_000);
    const spa = await ctx.newPage();
    await adminSignIn(spa, DEV_ADMIN.email, DEV_ADMIN.password);
    await spa.goto(`${SITES.admin}/order-management/refunds`);
    await spa.locator('.p-select', { hasText: 'Needs action' }).click();
    await spa.getByRole('option', { name: 'Refunded', exact: true }).click();
    await expect(spa.locator('tr', { hasText: numberOf(detail) })).toContainText('Cancellation');
    await ctx.close();
  });
});

test.describe('the takes, the drop and the placement as the webs show them', () => {
  test('the admin SPA shows the two-cleaner job with both seats taken', { tag: ['@E2E-TAKE-031'] }, async ({ page }) => {
    test.skip(scratch(), 'E2E_SCRATCH=1: a scratch run has no two-cleaner job');
    const w = planned('W');
    const detail = await adminDetail(w.id);
    test.skip(list(detail, 'assignedEmployees').length < 2, `W has ${list(detail, 'assignedEmployees').length} cleaner(s) on it (orders/20 fills it)`);
    await adminSignIn(page, DEV_ADMIN.email, DEV_ADMIN.password);
    await page.goto(`${SITES.admin}/order-management/${w.id}`);
    await expect(page.locator('.employee-card')).toHaveCount(2);
  });

  test('after the last cleaner dropped P the customer web reads it New, and the admin feed has the crew-lost notice', { tag: ['@E2E-TAKE-041', '@E2E-TAKE-042'] }, async ({ page, browser }) => {
    const p = planned('P');
    const detail = await adminDetail(p.id);
    const number = numberOf(detail);
    // P is placed again in orders/20 (TAKE-049); the page can only show New while nobody is on it.
    if (statusOf(detail) === 'New' && !list(detail, 'assignedEmployees').length) {
      await customerSignIn(page, emailOf(run, 'customer'), run.password);
      await page.goto(`${SITES.customer}/orders/${p.id}`);
      await expect(page.locator('main')).toContainText(number);
      await expect(page.locator('main')).toContainText('New');
    } else {
      test.info().annotations.push({ type: 'note', description: `P reads ${statusOf(detail)} with ${list(detail, 'assignedEmployees').length} cleaner(s): the web's New is no longer showable (TAKE-041 rests on the API half)` });
    }
    const ctx = await browser.newContext({ locale: 'en-US', timezoneId: 'Europe/Prague' });
    ctx.setDefaultTimeout(90_000);
    const spa = await ctx.newPage();
    await adminSignIn(spa, DEV_ADMIN.email, DEV_ADMIN.password);
    await spa.goto(`${SITES.admin}/notifications`);
    const row = spa.locator('.cleansia-notifications__row', { hasText: number }).filter({ hasText: 'An order lost its last cleaner' }).first();
    await expect(row).toBeVisible();
    await expect(row).toContainText('the cleaner dropped it');
    await ctx.close();
  });

  test('the admin SPA shows the cleaner placed on P with the contract pending', { tag: ['@E2E-TAKE-049'] }, async ({ page }) => {
    const p = planned('P');
    const detail = await adminDetail(p.id);
    const seat = list(detail, 'assignedEmployees').find((e) => str(e, 'employeeId') === run.employeeId);
    test.skip(!seat, 'A is not on P yet (orders/20 places A)');
    test.skip(list(detail, 'workContractAcceptances').some((a) => str(a, 'orderEmployeeId') === str(seat, 'id')), 'A has accepted P\'s contract already (work/30)');
    expect(statusOf(detail)).toBe('Confirmed');
    await adminSignIn(page, DEV_ADMIN.email, DEV_ADMIN.password);
    await page.goto(`${SITES.admin}/order-management/${p.id}`);
    await expect(page.locator('.employee-card', { hasText: str(seat, 'fullName') })).toContainText('Contract pending');
  });
});
