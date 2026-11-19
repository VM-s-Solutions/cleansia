import { expect, test } from '@playwright/test';
import { call, errorKeys, Host, list, named, num, ORDER_STATUS, PAYMENT_STATUS, poll, Session, signIn, str } from '../support/dev';
import { adminFeed, afterPhoto, complete, markCash, openPeriod, periodPays, preview, reportNotPaid, start } from '../support/flows';
import { emailOf, flag, loadRun, Role, RunOrder, RunState, saveRun, scratch, setFlag } from '../support/run';
import { startOpensAt } from '../support/slots';
import { cachedAdminAs } from '../support/web';

// Stage 'work', 10: the morning of Day 1 in its windows (e2e/README.md, the run order). The phase-1 card job refunded in
// full (08:55), Day 1's cash job where the customer did not pay (D1, 10:50), the cash job paid in cash (C3, 11:35),
// and the job whose start passed with nobody started (X, 12:02). Each test skips until its window opens and once its
// step is done, so the stage is invoked once per window.
// Not serial: each test skips on the state it needs, so one failure does not stop the jobs after it.

let run: RunState;
const sessions: Record<string, { s: Session; at: number }> = {};
const UNREGISTERED = { status: 403, title: 'Registration incomplete' };

async function as(role: Role, host: Host = 'partner'): Promise<Session> {
  const key = `${host}:${role}`;
  const c = sessions[key];
  if (c && Date.now() - c.at < 8 * 60_000) return c.s;
  const s = await signIn(host, emailOf(run, role), run.password);
  sessions[key] = { s, at: Date.now() };
  return s;
}

/** Support places, overrides and refunds; a scratch run has no Support, so the DEV administrator does. */
const support = () => cachedAdminAs(scratch() ? 'admin' : 'support', run);
const adminDetail = async (id: string) => (await call(await cachedAdminAs('admin'), 'GET', `/api/AdminOrder/details/${id}`)).body;
const statusOf = (o: unknown) => named(o, 'orderStatus', ORDER_STATUS);

/** A taken job of the run whose start window (slot - 60 min) is open; skips with the time it opens, or once it is done. */
function inWindow(key: string): RunOrder {
  const o = run.orders[key];
  test.skip(!o?.taken, `run ${run.runId} has no taken ${key} job`);
  test.skip(!!o.completed || !!o.cancelled, `${key} is ${o.completed ? 'completed' : 'cancelled'}`);
  const opens = startOpensAt(new Date(o.slot));
  test.skip(Date.now() < opens.getTime(), `${key} (slot ${o.slot}) can be started from ${opens.toISOString()}; run work again then`);
  return o;
}

async function startIfConfirmed(s: Session, o: RunOrder) {
  if (statusOf(await adminDetail(o.id)) !== 'Confirmed') return;
  const r = await start(s, o.id);
  expect(r.status, JSON.stringify(r.body)).toBe(200);
}

const cashHeld = async (s: Session) =>
  num(list((await call(s, 'GET', '/api/EmployeePayroll/GetCashHeld')).body, '').find((h) => str(h, 'currencyCode') === 'CZK'), 'amount') || 0;

test.describe('work: morning', () => {
  test.beforeAll(() => {
    run = loadRun();
  });

  test('Support refunds the completed phase-1 card job in full; the customer is told and its points are taken back', { tag: ['@E2E-CANCEL-049'] }, async () => {
    const o = run.orders['card'];
    test.skip(!o?.completed, 'the phase-1 card job is not completed yet (execute, 08:30-08:50)');
    if (!flag(run, 'card.refunded')) {
      const r = await call(await support(), 'POST', '/api/AdminOrder/refund', { orderId: o.id });
      expect(r.status, JSON.stringify(r.body)).toBe(200);
      expect(r.body).toMatchObject({ refundAmount: o.total, refundInitiated: true });
      setFlag(run, 'card.refunded');
    }
    const refund = await poll(
      async () => list((await call(await cachedAdminAs('admin'), 'GET', `/api/AdminRefund/get-paged?Offset=0&Limit=5&Filter.OrderId=${o.id}`)).body, 'data')[0],
      (x) => str(x, 'state.name') === 'Refunded',
      90_000,
    );
    expect(refund).toMatchObject({ amount: o.total, origin: expect.objectContaining({ name: 'FullRefund' }), state: expect.objectContaining({ name: 'Refunded' }) });
    expect(str(refund, 'stripeRefundId')).toMatch(/^re_/);
    const mine = (await call(await as('customer', 'customer'), 'GET', `/api/Order/GetById?OrderId=${o.id}`)).body;
    expect(named(mine, 'paymentStatus', PAYMENT_STATUS)).toBe('Refunded');
    const mobile = await as('customer', 'customer-mobile');
    const notice = await poll(
      async () => list((await call(mobile, 'GET', '/api/Notification/Paged?Offset=0&Limit=50')).body, 'data').find((n) => str(n, 'args.orderId') === o.id && str(n, 'eventKey') === 'order.refunded'),
      Boolean,
      60_000,
    );
    expect(notice, 'order.refunded in the customer feed').toBeTruthy();
    // The clawback: a Revoke (LoyaltyTransactionType 2) beside the completion's earn.
    const activity = list((await call(mobile, 'GET', '/api/Loyalty/GetActivity?offset=0&limit=50')).body, 'data').filter((x) => str(x, 'orderId') === o.id);
    expect(activity.map((x) => named(x, 'type', ['', 'Earn', 'Revoke']))).toEqual(expect.arrayContaining(['Earn', 'Revoke']));
  });

  test('on the cash job in progress (D1) a cleaner off it and the applicant are refused every step; Customer did not pay waits for the after photo', { tag: ['@E2E-EXEC-005', '@E2E-EXEC-027', '@E2E-EXEC-030'] }, async () => {
    const o = inWindow('D1');
    await startIfConfirmed(await as('cleaner'), o);
    expect(statusOf(await adminDetail(o.id))).toBe('InProgress');
    const e = await as('cleaner3');
    for (const [step, r] of [
      ['MarkCashCollected', await markCash(e, o.id)],
      ['MarkMyPartDone', await call(e, 'POST', '/api/Order/MarkMyPartDone', { orderId: o.id })],
      ['CompleteOrder', await complete(e, o.id)],
    ] as const) {
      expect(errorKeys(r.body), `${step}: ${JSON.stringify(r.body)}`).toContain('order.employee_not_assigned');
    }
    expect(errorKeys((await reportNotPaid(e, o.id)).body)).toContain('order.not_found');
    const applicant = await as('applicant');
    for (const r of [await markCash(applicant, o.id), await reportNotPaid(applicant, o.id)]) {
      expect({ status: r.status, title: str(r.body, 'title') }, JSON.stringify(r.body)).toEqual(UNREGISTERED);
    }
    if (!flag(run, 'd1.photo')) {
      const early = await reportNotPaid(await as('cleaner'), o.id);
      expect(errorKeys(early.body), JSON.stringify(early.body)).toContain('order.after_photos.required');
    }
  });

  test('the cleaner reports that the customer paid nothing: completed, unpaid, a receivable of what is owed, no receipt, the admins told (D1)', { tag: ['@E2E-EXEC-029'] }, async () => {
    const o = inWindow('D1');
    const a = await as('cleaner');
    const held = await cashHeld(a);
    const debtor = await as('debtor', 'customer-mobile');
    const points = async () => list((await call(debtor, 'GET', '/api/Loyalty/GetActivity?offset=0&limit=50')).body, 'data').filter((x) => str(x, 'orderId') === o.id).length;
    const pointsBefore = await points();
    if (!flag(run, 'd1.photo')) {
      expect((await afterPhoto(a, o.id, `E2E run ${run.runId}`)).status).toBe(200);
      setFlag(run, 'd1.photo');
    }
    const r = await reportNotPaid(a, o.id);
    expect(r.status, JSON.stringify(r.body)).toBe(200);
    o.completed = true;
    saveRun(run);
    const d = await adminDetail(o.id);
    // BR #cash-not-paid: what is owed is the total less any credit the order used.
    const owed = o.total - num(d, 'creditAppliedAmount');
    expect(r.body).toMatchObject({ orderId: o.id, newStatus: 5, amountOwed: owed });
    expect(statusOf(d)).toBe('Completed');
    expect(named(d, 'paymentStatus', PAYMENT_STATUS)).toBe('Pending');
    const debt = list((await call(await as('debtor', 'customer'), 'GET', '/api/Receivable/GetMine')).body, '').find((x) => str(x, 'orderId') === o.id);
    expect(str(debt, 'kind.name')).toBe('UnpaidCash');
    expect(num(debt, 'amount')).toBeCloseTo(owed, 2);
    run.receivables = { ...run.receivables, D1: str(debt, 'id') };
    saveRun(run);
    const admins = await poll(() => adminFeed(run, scratch() ? 'admin' : 'support'), (v) => v.some((n) => str(n, 'args.orderId') === o.id && str(n, 'eventKey') === 'admin.order.cash_not_paid'), 60_000);
    expect(admins.some((n) => str(n, 'args.orderId') === o.id && str(n, 'eventKey') === 'admin.order.cash_not_paid')).toBe(true);
    expect(errorKeys((await call(debtor, 'GET', `/api/Order/DownloadReceipt?OrderId=${o.id}`)).body)).toContain('receipt.not_found');
    expect(await points()).toBe(pointsBefore);
    expect(await cashHeld(a)).toBe(held);
    const period = await openPeriod();
    const a2 = run.employeeId ?? str((await call(a, 'GET', '/api/Employee/GetCurrentEmployee')).body, 'id');
    const pays = await poll(() => periodPays(a, a2, period?.id ?? ''), (v) => v.some((x) => str(x, 'orderId') === o.id));
    expect(pays.map((x) => str(x, 'orderId'))).toContain(o.id);
  });

  test('cash is recorded once on the cash job (C3); a second record and Customer did not pay are refused after it; completed', { tag: ['@E2E-EXEC-027', '@E2E-EXEC-030'] }, async () => {
    const o = inWindow('C3');
    const a = await as('cleaner');
    await startIfConfirmed(a, o);
    if (!str(await adminDetail(o.id), 'cashCollectedAt')) {
      expect((await afterPhoto(a, o.id, `E2E run ${run.runId}`)).status).toBe(200);
      const cash = await markCash(a, o.id);
      expect(cash.status, JSON.stringify(cash.body)).toBe(200);
    }
    expect(errorKeys((await markCash(a, o.id)).body)).toContain('order.cash_already_collected');
    expect(errorKeys((await reportNotPaid(a, o.id)).body)).toContain('order.cash_already_collected');
    const done = await complete(a, o.id);
    expect(done.status, JSON.stringify(done.body)).toBe(200);
    o.completed = true;
    saveRun(run);
  });

  test('past its start, with a cleaner on it who has not started, the job can neither be previewed for cancel nor cancelled (X)', { tag: ['@E2E-CANCEL-018'] }, async () => {
    const o = run.orders['X'];
    test.skip(!o?.taken, 'X was not taken (orders/20)');
    test.skip(!!flag(run, 'x.apiRefused'), `refused at ${flag(run, 'x.apiRefused')}`);
    test.skip(Date.now() < Date.parse(o.slot), `X starts at ${o.slot}; run work again just after it, before A starts it`);
    test.skip(statusOf(await adminDetail(o.id)) !== 'Confirmed', 'X is no longer Confirmed');
    const customer = await as('customer', 'customer');
    const p = await preview(customer, o.id);
    expect(errorKeys(p.body), JSON.stringify(p.body)).toContain('order.start_passed_cannot_cancel');
    const c = await call(customer, 'POST', '/api/Order/Cancel', { orderId: o.id, reason: `E2E run ${run.runId} X` });
    expect(errorKeys(c.body), JSON.stringify(c.body)).toContain('order.start_passed_cannot_cancel');
    const d = await adminDetail(o.id);
    expect(statusOf(d)).toBe('Confirmed');
    expect(named(d, 'paymentStatus', PAYMENT_STATUS)).toBe('Paid');
    setFlag(run, 'x.apiRefused');
  });
});
