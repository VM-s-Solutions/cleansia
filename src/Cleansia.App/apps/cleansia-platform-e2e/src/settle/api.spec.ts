import { expect, test } from '@playwright/test';
import { anonymous, call, DEV_ADMIN, list, num, poll, signIn, str } from '../support/dev';
import { emailOf, loadRun, RunOrder, saveRun } from '../support/run';

// Stage 'settle': the cancellations and refunds of the jobs nobody took, then Close now on the open pay period and
// the cleaner's invoice with its cash set-off. Each test skips with the reason when the run has nothing for it, so
// the stage can be run again until everything is settled.
const run = loadRun();
const admin = () => signIn('admin', DEV_ADMIN.email, DEV_ADMIN.password);
const rows = (body: unknown) => (list(body, 'data').length ? list(body, 'data') : list(body, 'items'));
const reason = `E2E run ${run.runId}`;

function open(key: string): RunOrder {
  const o = run.orders[key];
  test.skip(!o, `run ${run.runId} booked no ${key} order`);
  test.skip(!!o.cancelled, `the ${key} order was cancelled by an earlier settle run`);
  return o;
}

function cancelled(o: RunOrder) {
  o.cancelled = true;
  saveRun(run);
}

async function refundOf(orderId: string) {
  const a = await admin();
  return poll(
    async () => rows((await call(a, 'GET', `/api/AdminRefund/get-paged?Offset=0&Limit=5&Filter.OrderId=${orderId}`)).body)[0],
    (r) => str(r, 'state.name') === 'Refunded',
  );
}

const day = (d: Date) => d.toISOString().slice(0, 10);

test.describe('settle', () => {
  test('the customer cancels the cash job nobody took, free, and owes nothing', { tag: ['@E2E-CANCEL-001', '@E2E-CANCEL-015'] }, async () => {
    const o = open('cashToCancel');
    const customer = await signIn('customer', emailOf(run, 'customer'), run.password);
    const preview = await call(customer, 'GET', `/api/Order/CancellationPreview?orderId=${o.id}`);
    expect(preview.status, JSON.stringify(preview.body)).toBe(200);
    expect(num(preview.body, 'feeAmount')).toBe(0);
    const r = await call(customer, 'POST', '/api/Order/Cancel', { orderId: o.id, reason });
    expect(r.status, JSON.stringify(r.body)).toBe(200);
    cancelled(o);
    const detail = (await call(await admin(), 'GET', `/api/AdminOrder/details/${o.id}`)).body;
    expect(str(detail, 'orderStatus.name')).toBe('Cancelled');
    expect(JSON.stringify((await call(customer, 'GET', '/api/Receivable/GetMine')).body)).not.toContain(o.id);
  });

  test('the guest previews and cancels the paid booking with its token and is refunded in full', { tag: ['@E2E-BOOK-059', '@E2E-CANCEL-025'] }, async () => {
    const o = open('guest');
    const guest = await anonymous('customer');
    const preview = await call(guest, 'POST', '/api/Order/GuestCancellationPreview', { accessToken: o.guestToken });
    expect(preview.status, JSON.stringify(preview.body)).toBe(200);
    expect(num(preview.body, 'feeAmount')).toBe(0);
    expect(num(preview.body, 'refundAmount')).toBe(o.total);
    const r = await call(guest, 'POST', '/api/Order/CancelGuest', { accessToken: o.guestToken, reason, language: 'en' });
    expect(r.status, JSON.stringify(r.body)).toBe(200);
    cancelled(o);
    expect(num(r.body, 'refundAmount')).toBe(o.total);
    expect(str(await refundOf(o.id), 'state.name')).toBe('Refunded');
  });

  test('the admin cancels the paid card job and the full refund reaches Stripe', { tag: ['@E2E-CANCEL-039'] }, async () => {
    const o = open('cardToCancel');
    const r = await call(await admin(), 'POST', '/api/AdminOrder/cancel', { orderId: o.id, reason });
    expect(r.status, JSON.stringify(r.body)).toBe(200);
    cancelled(o);
    expect(r.body).toMatchObject({ refundAmount: o.total, totalPrice: o.total, refundInitiated: true });
    const refund = await refundOf(o.id);
    expect(str(refund, 'state.name')).toBe('Refunded');
    expect(str(refund, 'stripeRefundId')).toMatch(/^re_/);
  });

  test('Close now ends the open period today and the next one opens tomorrow', { tag: ['@E2E-PAYOUT-016'] }, async () => {
    test.skip(!!run.closedPeriodId, `run ${run.runId} already closed period ${run.closedPeriodId}`);
    test.skip(!run.orders['cash']?.completed || !run.orders['card']?.completed, 'the execute stage has not completed both jobs yet');
    const a = await admin();
    const periods = rows((await call(a, 'GET', '/api/AdminPayPeriod/get-paged?Offset=0&Limit=20')).body);
    const current = periods.find((p) => str(p, 'status') === 'Open' || str(p, 'status.name') === 'Open');
    const id = str(current, 'id');
    expect(id).not.toBe('');
    test.skip(str(current, 'startDate') > day(new Date()), `the open period starts ${str(current, 'startDate')}; Close now ends a period that has started`);
    // Pay is written off the completion's path; a row written after the close lands in the next period.
    const cleaner = await signIn('partner', emailOf(run, 'cleaner'), run.password);
    const jobs = [run.orders['cash'].id, run.orders['card'].id];
    const paid = await poll(
      async () => list((await call(cleaner, 'GET', `/api/EmployeePayroll/GetPeriodPays?EmployeeId=${run.employeeId}&PayPeriodId=${id}`)).body, 'orderPays').map((r) => str(r, 'orderId')),
      (ids) => jobs.every((j) => ids.includes(j)),
    );
    expect(paid, 'both jobs have their pay row in the period before it is closed').toEqual(expect.arrayContaining(jobs));
    const r = await call(a, 'POST', '/api/AdminPayPeriod/close', { payPeriodId: id, reason });
    expect(r.status, JSON.stringify(r.body)).toBe(200);
    run.closedPeriodId = id;
    run.closedAt = new Date().toISOString();
    saveRun(run);
    const closed = (await call(a, 'GET', `/api/AdminPayPeriod/details/${id}`)).body;
    expect(str(closed, 'status.name') || str(closed, 'status')).toBe('Closed');
    expect(str(closed, 'endDate') <= day(new Date())).toBe(true);
    const tomorrow = day(new Date(Date.now() + 86_400_000));
    const after = rows((await call(a, 'GET', '/api/AdminPayPeriod/get-paged?Offset=0&Limit=20')).body);
    expect(after.some((p) => str(p, 'startDate') === tomorrow)).toBe(true);
  });

  test('the closed period invoices the cleaner, sets the cash off up to the total, and both PDFs download', { tag: ['@E2E-PAYOUT-017', '@E2E-PAYOUT-032', '@E2E-PAYOUT-036', '@E2E-PAYOUT-055'] }, async () => {
    test.skip(!run.closedPeriodId, 'no period was closed by this run yet');
    test.setTimeout(35 * 60_000);
    // The five-minute sweep invoices a period 15 minutes after its close; not a defect before 25 (E2E-PAYOUT-017).
    const deadline = new Date(run.closedAt ?? '').getTime() + 25 * 60_000;
    const invoice = await poll(
      async () => rows((await call(await admin(), 'GET', `/api/AdminInvoice/get-paged?Offset=0&Limit=10&Filter.EmployeeId=${run.employeeId}&Filter.PayPeriodId=${run.closedPeriodId}`)).body)[0],
      Boolean,
      Math.max(deadline - Date.now(), 0) + 30_000,
      60_000,
    );
    expect(invoice, `no invoice 25 minutes after the close at ${run.closedAt}`).toBeTruthy();
    const a = await admin(); // signed in after the wait: an admin session lapses in about 15 minutes
    const detail = (await call(a, 'GET', `/api/AdminInvoice/details/${str(invoice, 'id')}`)).body;
    expect(str(detail, 'status.name') || str(detail, 'status')).toMatch(/^(Pending|1)$/);
    expect(str(detail, 'currencyCode')).toBe('CZK');
    const cleaner = await signIn('partner', emailOf(run, 'cleaner'), run.password);
    const pays = (await call(cleaner, 'GET', `/api/EmployeePayroll/GetPeriodPays?EmployeeId=${run.employeeId}&PayPeriodId=${run.closedPeriodId}`)).body;
    const rowTotal = list(pays, 'orderPays').reduce<number>((sum, r) => sum + num(r, 'totalPay'), 0);
    expect(num(detail, 'subTotal')).toBeCloseTo(rowTotal, 2);
    // Set-off: min(cash held, invoice total), transfer = total - set-off, the total itself unchanged.
    const total = num(detail, 'totalAmount');
    const held = run.cashHeldAfterExecute ?? 0;
    expect(num(detail, 'cashSetOffAmount')).toBeCloseTo(Math.min(held, total), 2);
    expect(num(detail, 'transferAmount')).toBeCloseTo(total - Math.min(held, total), 2);
    const heldNow = list((await call(cleaner, 'GET', '/api/EmployeePayroll/GetCashHeld')).body, '').find((h) => str(h, 'currencyCode') === 'CZK');
    expect(num(heldNow, 'amount') || 0).toBeCloseTo(held - Math.min(held, total), 2);
    const adminPdf = await call(a, 'GET', `/api/AdminInvoice/download/${str(invoice, 'id')}`);
    const cleanerPdf = await call(cleaner, 'GET', `/api/EmployeePayroll/DownloadInvoice/${str(invoice, 'id')}`);
    expect(adminPdf.headers['content-type']).toContain('application/pdf');
    expect(cleanerPdf.headers['content-type']).toContain('application/pdf');
    expect(cleanerPdf.raw.equals(adminPdf.raw)).toBe(true);
  });
});
