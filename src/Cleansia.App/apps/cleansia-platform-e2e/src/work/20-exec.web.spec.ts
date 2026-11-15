import { Browser, expect, Page, test } from '@playwright/test';
import { call, DEV_ADMIN, list, named, ORDER_STATUS, PAYMENT_STATUS, poll, signIn, str } from '../support/dev';
import { emailOf, flag, loadRun, RunOrder, scratch, setFlag } from '../support/run';
import { startOpensAt } from '../support/slots';
import { AdminRole, adminSignIn, cachedAdmin, customerSignIn, partnerSignIn, SITES } from '../support/web';

// Stage 'work', web half (F-027 too): Support's full refund in the admin SPA (CANCEL-049), the
// customer web past X's start (CANCEL-018), B's job page on the two-cleaner job W while Confirmed (F-027, EXEC-020),
// On the way and In progress (TAKE-044, EXEC-043), and the SPA's Refunds page and P's accepted contract (CANCEL-051,
// TAKE-050). Runs in each Day 1 work window between work/10 and work/30; every test skips until its moment.
// Not serial: every test has its own gate, so one failure does not skip the tests after it (one worker keeps the order).
// A DEV page that hangs fails the step in 90 s, not at the 10-minute test timeout (support/web.ts waits on navigation).
test.use({ timezoneId: 'Europe/Prague', navigationTimeout: 90_000, actionTimeout: 90_000 });
const run = loadRun();
const ORIGIN = ['', 'Cancellation', 'Dispute', 'FullRefund', 'PartialRefund'];

const adminDetail = async (id: string) => (await call(await cachedAdmin(), 'GET', `/api/AdminOrder/details/${id}`)).body;
const statusOf = (detail: unknown) => named(detail, 'orderStatus', ORDER_STATUS);
const numberOf = (detail: unknown) => str(detail, 'displayOrderNumber') || str(detail, 'orderNumber');
const refunds = async (orderId: string) => list((await call(await cachedAdmin(), 'GET', `/api/AdminRefund/get-paged?Offset=0&Limit=10&Filter.OrderId=${orderId}`)).body, 'data');

function order(key: string): RunOrder {
  const o = run.orders[key];
  test.skip(!o, `run ${run.runId} has no ${key} order`);
  return o;
}

// The admin host answers 429 to sign-ins in quick succession: the SPA's are spaced like signIn()'s.
let lastSpaSignIn = 0;
async function spa(browser: Browser, role: AdminRole) {
  const wait = lastSpaSignIn + 7_000 - Date.now();
  if (wait > 0) await new Promise((r) => setTimeout(r, wait));
  lastSpaSignIn = Date.now();
  const ctx = await browser.newContext({ locale: 'en-US', timezoneId: 'Europe/Prague' });
  ctx.setDefaultTimeout(90_000);
  const page = await ctx.newPage();
  await adminSignIn(page, role === 'admin' ? DEV_ADMIN.email : emailOf(run, role), role === 'admin' ? DEV_ADMIN.password : run.password);
  return { ctx, page };
}

async function refundsPageRow(page: Page, number: string) {
  await page.goto(`${SITES.admin}/order-management/refunds`);
  await page.locator('.p-select', { hasText: 'Needs action' }).click();
  await page.getByRole('option', { name: 'Refunded', exact: true }).click();
  return page.locator('tr', { hasText: number });
}

/** W as B's job page opens it, once W is in `status` with B on its crew. */
async function bOnW(page: Page, status: string) {
  test.skip(scratch(), 'E2E_SCRATCH=1: a scratch run has no two-cleaner job');
  const w = order('W');
  const detail = await adminDetail(w.id);
  test.skip(statusOf(detail) !== status, `W reads ${statusOf(detail)}, not ${status}`);
  test.skip(!list(detail, 'assignedEmployees').some((e) => str(e, 'employeeId') === run.employee2Id), 'B is not on W');
  await partnerSignIn(page, emailOf(run, 'cleaner2'), run.password);
  await page.goto(`${SITES.partner}/orders/${w.id}`);
  await expect(page.locator('main')).toContainText(numberOf(detail));
  return { w, detail };
}

test('Support issues a full refund of the web card job in the admin SPA; it reaches Stripe and reads Full refund', { tag: ['@E2E-CANCEL-049'] }, async ({ browser }) => {
  test.skip(scratch(), 'E2E_SCRATCH=1: a scratch run has no Support account');
  const o = order('webCard');
  test.skip(!o.completed, 'the web card job is not complete yet (execute)');
  test.skip(!!flag(run, 'webCard.refunded'), `refunded at ${flag(run, 'webCard.refunded')}`);
  const detail = await adminDetail(o.id);
  test.skip(named(detail, 'paymentStatus', PAYMENT_STATUS) !== 'Paid', `the web card job's payment reads ${named(detail, 'paymentStatus', PAYMENT_STATUS)}`);
  const { ctx, page } = await spa(browser, 'support');
  await page.goto(`${SITES.admin}/order-management/${o.id}`);
  await page.getByRole('button', { name: 'Issue refund' }).first().click();
  const panel = page.locator('.detail-actions__panel', { hasText: 'Issue a full refund without changing the order status.' });
  await panel.getByRole('button', { name: 'Issue refund' }).click();
  await expect(page.getByText('Refund issued successfully.')).toBeVisible({ timeout: 60_000 });
  setFlag(run, 'webCard.refunded');
  const refund = await poll(async () => (await refunds(o.id)).find((r) => named(r, 'origin', ORIGIN) === 'FullRefund'), (r) => str(r, 'state.name') === 'Refunded');
  expect(str(refund, 'state.name')).toBe('Refunded');
  expect(str(refund, 'stripeRefundId')).toMatch(/^re_/);
  expect(named(await adminDetail(o.id), 'paymentStatus', PAYMENT_STATUS)).toBe('Refunded');
  await expect(await refundsPageRow(page, numberOf(detail))).toContainText('Full refund');
  await ctx.close();
});

test('past X\'s start, staffed and not started, the customer web offers "The cleaner did not arrive" instead of Cancel', { tag: ['@E2E-CANCEL-018'] }, async ({ page }) => {
  const x = order('X');
  test.skip(!flag(run, 'x.apiRefused'), 'work/10 has not been refused X\'s cancel past the start yet');
  test.skip(!!flag(run, 'x.webChecked'), `checked at ${flag(run, 'x.webChecked')}`);
  expect(statusOf(await adminDetail(x.id))).toBe('Confirmed');
  await customerSignIn(page, emailOf(run, 'customer'), run.password);
  await page.goto(`${SITES.customer}/orders/${x.id}`);
  await expect(page.getByRole('button', { name: 'The cleaner did not arrive' })).toBeVisible();
  await expect(page.locator('main')).toContainText('The booked start time has passed, so this booking can no longer be cancelled.');
  await expect(page.getByRole('button', { name: 'Cancel booking' })).toHaveCount(0);
  setFlag(run, 'x.webChecked');
});

test('B\'s job page on W, Confirmed, holds the customer\'s name, e-mail, phone and address, and offers Cancel Job', { tag: ['@E2E-F-027', '@E2E-EXEC-020'] }, async ({ page }) => {
  const { w } = await bOnW(page, 'Confirmed');
  const b = await signIn('partner', emailOf(run, 'cleaner2'), run.password);
  const seen = (await call(b, 'GET', `/api/Order/GetById?OrderId=${w.id}`)).body;
  expect(str(seen, 'accessInstructions')).toBe('key under the mat');
  const input = (name: string) => page.locator(`[formcontrolname=${name}] input`).first();
  await expect(input('customerName')).toHaveValue(str(seen, 'customerName'));
  await expect(input('customerEmail')).toHaveValue(str(seen, 'customerEmail'));
  // The telephone field may split the dialling code from the number: its inputs together hold the number.
  const digits = (str(seen, 'customerPhone').replace(/\D/g, '')).slice(-6);
  await expect
    .poll(async () => (await page.locator('[formcontrolname=customerPhone] input').evaluateAll((els) => els.map((e) => (e as HTMLInputElement).value).join(''))).replace(/\D/g, ''))
    .toContain(digits);
  await expect(input('address')).toHaveValue(new RegExp(str(seen, 'address.street').replace(/[.*+?^${}()|[\]\\]/g, '\\$&')));
  await expect(page.getByRole('button', { name: 'Cancel Job' })).toBeVisible();
});

test('B sets off for W on the web, and the job page then offers no drop', { tag: ['@E2E-TAKE-044'] }, async ({ page }) => {
  test.skip(!!flag(run, 'w.webOnTheWay'), `done at ${flag(run, 'w.webOnTheWay')}`);
  const w = order('W');
  test.skip(Date.now() < startOpensAt(new Date(w.slot)).getTime(), `W can be set off from ${startOpensAt(new Date(w.slot)).toISOString()}`);
  await bOnW(page, 'Confirmed');
  await page.getByRole('button', { name: 'On my way' }).click();
  await expect(page.getByText('Customer notified · you\'re on the way.')).toBeVisible();
  await expect(page.locator('main')).toContainText('On the way');
  await page.reload();
  await expect(page.locator('main')).toContainText('On the way');
  await expect(page.getByRole('button', { name: 'Cancel Job' })).toHaveCount(0);
  setFlag(run, 'w.webOnTheWay');
});

test('W in progress: B\'s job page offers no drop, and the list row\'s Complete opens the job page', { tag: ['@E2E-TAKE-044', '@E2E-EXEC-043'] }, async ({ page }) => {
  test.skip(!!flag(run, 'w.webInProgressChecked'), `checked at ${flag(run, 'w.webInProgressChecked')}`);
  const { w, detail } = await bOnW(page, 'InProgress');
  await expect(page.locator('main')).toContainText('In Progress');
  await expect(page.getByRole('button', { name: 'Cancel Job' })).toHaveCount(0);
  await page.goto(`${SITES.partner}/orders`);
  const row = page.locator('tr', { hasText: numberOf(detail) });
  await expect(row).toBeVisible();
  await row.locator('button:has(.pi-check-circle)').click();
  await page.waitForURL(new RegExp(`/orders/${w.id}`));
  await expect(page.getByRole('dialog')).toHaveCount(0);
  setFlag(run, 'w.webInProgressChecked');
});

test('the Refunds page lists X\'s line refunds as Partial refund', { tag: ['@E2E-CANCEL-051'] }, async ({ browser }) => {
  const x = order('X');
  const partial = (await refunds(x.id)).filter((r) => named(r, 'origin', ORIGIN) === 'PartialRefund' && str(r, 'state.name') === 'Refunded');
  test.skip(!partial.length, 'X has no settled partial refund yet (work/30)');
  const { ctx, page } = await spa(browser, 'admin');
  const row = await refundsPageRow(page, numberOf(await adminDetail(x.id)));
  await expect(row.first()).toContainText('Partial refund');
  await ctx.close();
});

test('the admin SPA shows A\'s accepted contract on P, with its date and version', { tag: ['@E2E-TAKE-050'] }, async ({ browser }) => {
  const p = order('P');
  const detail = await adminDetail(p.id);
  const seat = list(detail, 'assignedEmployees').find((e) => str(e, 'employeeId') === run.employeeId);
  const accepted = list(detail, 'workContractAcceptances').find((a) => str(a, 'orderEmployeeId') === str(seat, 'id'));
  test.skip(!seat || !accepted, 'A has not accepted P\'s contract yet (work/30)');
  const { ctx, page } = await spa(browser, 'admin');
  await page.goto(`${SITES.admin}/order-management/${p.id}`);
  await expect(page.locator('.employee-card', { hasText: str(seat, 'fullName') })).toContainText(new RegExp(`Accepted .+, v${str(accepted, 'documentVersion')}`));
  await ctx.close();
});
