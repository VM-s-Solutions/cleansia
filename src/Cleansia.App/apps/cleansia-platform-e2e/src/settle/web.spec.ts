import { expect, Locator, Page, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { call, DEV_ADMIN, list, named, num, ORDER_STATUS, poll, str } from '../support/dev';
import { emailOf, loadRun, RunOrder, saveRun } from '../support/run';
import { adminSignIn, cachedAdmin, customerSignIn, partnerSignIn, SITES } from '../support/web';

// Stage 'settle', web half: the web customer's and the administrator's cancellations of the two web jobs nobody
// took, the Refunds page, and the pay periods and invoices as the administrator and the web cleaner read them.
test.describe.configure({ mode: 'serial' });
const run = loadRun();
const rows = (body: unknown) => (list(body, 'data').length ? list(body, 'data') : list(body, 'items'));
const admin = cachedAdmin;
const orderDetail = async (id: string) => (await call(await admin(), 'GET', `/api/AdminOrder/details/${id}`)).body;

function open(key: string): RunOrder {
  const o = run.orders[key];
  test.skip(!o, `run ${run.runId} booked no ${key} order`);
  test.skip(!!o.cancelled, `the ${key} order was cancelled by an earlier settle run`);
  return o;
}

async function refundOf(orderId: string) {
  const a = await admin();
  return poll(
    async () => rows((await call(a, 'GET', `/api/AdminRefund/get-paged?Offset=0&Limit=5&Filter.OrderId=${orderId}`)).body)[0],
    (r) => str(r, 'state.name') === 'Refunded',
  );
}

async function downloadFrom(page: Page, button: Locator) {
  const [download] = await Promise.all([page.waitForEvent('download'), button.click()]);
  const path = await download.path();
  return { name: download.suggestedFilename(), bytes: readFileSync(path) };
}

async function showRefunded(page: Page) {
  await page.goto(`${SITES.admin}/order-management/refunds`);
  await page.locator('.p-select', { hasText: 'Needs action' }).click();
  await page.getByRole('option', { name: 'Refunded', exact: true }).click();
}

test.describe('customer web', () => {
  test('the customer cancels the paid card job nobody took, the page shows no fee and the money goes back', { tag: ['@E2E-CANCEL-001'] }, async ({ page }) => {
    const o = open('webCancelCustomer');
    await customerSignIn(page, emailOf(run, 'webcustomer'), run.password);
    await page.goto(`${SITES.customer}/orders/${o.id}`);
    await page.getByRole('button', { name: 'Cancel booking' }).click();
    const dialog = page.getByRole('dialog');
    await expect(dialog).toContainText('No cleaner has accepted this booking');
    await expect(dialog).toContainText('Cancellation fee');
    await expect(dialog).toContainText('CZK 0 (0%)');
    await dialog.getByRole('button', { name: 'Confirm cancellation' }).click();
    await expect(dialog).toBeHidden({ timeout: 60_000 });
    o.cancelled = true;
    saveRun(run);
    await expect(page.locator('main')).toContainText('Cancelled');
    const detail = await orderDetail(o.id);
    expect(named(detail, 'orderStatus', ORDER_STATUS)).toBe('Cancelled');
    expect(str(await refundOf(o.id), 'state.name')).toBe('Refunded');
  });
});

test.describe('admin web', () => {
  test('the administrator cancels the other paid card job and the full refund reaches Stripe', { tag: ['@E2E-CANCEL-039'] }, async ({ page }) => {
    const o = open('webCancelAdmin');
    await adminSignIn(page, DEV_ADMIN.email, DEV_ADMIN.password);
    await page.goto(`${SITES.admin}/order-management/${o.id}`);
    await expect(page.locator('main')).toContainText('Order Operations');
    await page.getByRole('button', { name: /Cancel order/ }).first().click();
    await page.getByRole('textbox', { name: /Cancellation reason/ }).fill(`E2E run ${run.runId} web`);
    await page.getByRole('button', { name: /Cancel order/ }).last().click();
    await expect(page.locator('main')).toContainText('Cancelled', { timeout: 60_000 });
    o.cancelled = true;
    saveRun(run);
    const refund = await refundOf(o.id);
    expect(str(refund, 'stripeRefundId')).toMatch(/^re_/);
  });

  test('the Refunds page lists both refunds with their Stripe refund id', { tag: ['@E2E-CANCEL-039'] }, async ({ page }) => {
    for (const key of ['webCancelCustomer', 'webCancelAdmin']) test.skip(!run.orders[key]?.cancelled, `${key} is not cancelled yet`);
    await adminSignIn(page, DEV_ADMIN.email, DEV_ADMIN.password);
    await showRefunded(page);
    for (const key of ['webCancelCustomer', 'webCancelAdmin']) {
      const o = run.orders[key];
      const stripeId = str(await refundOf(o.id), 'stripeRefundId');
      const row = page.locator('tr', { hasText: o.number });
      await expect(row).toBeVisible();
      await expect(row).toContainText(stripeId);
      await expect(row).toContainText('Refunded');
      await expect(row).toContainText(`CZK ${o.total.toFixed(2)}`);
      await expect(row.getByRole('button', { name: 'Copy Stripe refund id' })).toBeVisible();
    }
  });
});

test.describe('pay periods and invoices', () => {
  test('the administrator reads the pay periods: one is open, and a closed one names who closed it', async ({ page }) => {
    await adminSignIn(page, DEV_ADMIN.email, DEV_ADMIN.password);
    await page.goto(`${SITES.admin}/pay-periods`);
    await expect(page.locator('tbody tr', { hasText: 'Open' })).toHaveCount(1);
    await expect(page.locator('tbody tr', { hasText: 'Closed' }).first()).toBeVisible();
    if (run.closedPeriodId) {
      await page.goto(`${SITES.admin}/pay-periods/${run.closedPeriodId}`);
      await expect(page.locator('main')).toContainText('Closed');
      await expect(page.locator('main')).toContainText(DEV_ADMIN.email);
    }
  });

  test('the administrator closes the open period with Close now when nothing closed it yet', { tag: ['@E2E-PAYOUT-016'] }, async ({ page }) => {
    test.skip(!!run.closedPeriodId, `run ${run.runId} already closed period ${run.closedPeriodId}`);
    await adminSignIn(page, DEV_ADMIN.email, DEV_ADMIN.password);
    const done = ['webCard', 'webCash'].every((k) => run.orders[k]?.completed);
    test.skip(!done, 'the web jobs are not complete, so there is nothing to close');
    const a = await admin();
    const periods = rows((await call(a, 'GET', '/api/AdminPayPeriod/get-paged?Offset=0&Limit=20')).body);
    const current = periods.find((p) => str(p, 'status') === 'Open' || str(p, 'status.name') === 'Open');
    test.skip(str(current, 'startDate') > new Date().toISOString().slice(0, 10), `the open period starts ${str(current, 'startDate')}; Close now ends a period that has started`);
    await page.goto(`${SITES.admin}/pay-periods/${str(current, 'id')}`);
    await page.getByRole('button', { name: 'Close now' }).click();
    await page.getByRole('textbox', { name: /^Reason/ }).fill(`E2E run ${run.runId} web`);
    await page.getByRole('button', { name: 'Close the period now' }).click();
    await expect(page.locator('main')).toContainText('Closed', { timeout: 60_000 });
    run.closedPeriodId = str(current, 'id');
    run.closedAt = new Date().toISOString();
    saveRun(run);
  });

  test('the web cleaner\'s invoice shows the same figures on the admin invoices page and on the partner web', { tag: ['@E2E-PAYOUT-032', '@E2E-PAYOUT-035', '@E2E-PAYOUT-036', '@E2E-PAYOUT-055', '@E2E-PAYOUT-056'] }, async ({ page, browser }) => {
    test.skip(!run.closedPeriodId, 'no period was closed by this run yet');
    test.skip(!run.orders['webCard']?.completed, 'the web jobs are not complete, so the web cleaner has no pay in the closed period');
    test.setTimeout(35 * 60_000);
    const deadline = new Date(run.closedAt ?? '').getTime() + 25 * 60_000;
    const invoice = await poll(
      async () => rows((await call(await admin(), 'GET', `/api/AdminInvoice/get-paged?Offset=0&Limit=10&Filter.EmployeeId=${run.employee2Id}&Filter.PayPeriodId=${run.closedPeriodId}`)).body)[0],
      Boolean,
      Math.max(deadline - Date.now(), 0) + 30_000,
      60_000,
    );
    expect(invoice, `no invoice 25 minutes after the close at ${run.closedAt}`).toBeTruthy();
    const detail = (await call(await admin(), 'GET', `/api/AdminInvoice/details/${str(invoice, 'id')}`)).body;
    const money = (n: number) => `CZK ${n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    const figures = [str(invoice, 'invoiceNumber') || str(detail, 'invoiceNumber'), money(num(detail, 'totalAmount')), money(num(detail, 'cashSetOffAmount')), money(num(detail, 'transferAmount'))];
    // Set-off: min(cash held, invoice total), transfer = total - set-off (E2E-PAYOUT-055).
    expect(num(detail, 'cashSetOffAmount')).toBeCloseTo(Math.min(num(detail, 'totalAmount'), run.orders['webCash'].total), 2);
    expect(num(detail, 'transferAmount')).toBeCloseTo(num(detail, 'totalAmount') - num(detail, 'cashSetOffAmount'), 2);

    await adminSignIn(page, DEV_ADMIN.email, DEV_ADMIN.password);
    await page.goto(`${SITES.admin}/invoice-management`);
    const adminRow = page.locator('tr', { hasText: figures[0] });
    await expect(adminRow).toBeVisible();
    for (const f of figures) await expect(adminRow).toContainText(f);
    const adminPdf = await downloadFrom(page, adminRow.locator('button:has(.pi-download)'));

    const ctx = await browser.newContext({ locale: 'en-US' });
    const web = await ctx.newPage();
    await partnerSignIn(web, emailOf(run, 'cleaner2'), run.password);
    await web.goto(`${SITES.partner}/invoices`);
    const row = web.locator('tr', { hasText: figures[0] });
    await expect(row).toBeVisible();
    for (const f of figures) await expect(row).toContainText(f);
    const cleanerPdf = await downloadFrom(web, row.locator('button:has(.pi-download)'));
    await ctx.close();
    expect(adminPdf.name).toBe(cleanerPdf.name);
    expect(adminPdf.name).toBe(`Invoice_${figures[0]}.pdf`);
    expect(cleanerPdf.bytes.equals(adminPdf.bytes)).toBe(true);
  });
});
