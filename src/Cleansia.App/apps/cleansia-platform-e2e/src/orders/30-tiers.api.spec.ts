import { expect, test } from '@playwright/test';
import { anonymous, call, errorKeys, Host, list, named, ORDER_STATUS, PAYMENT_STATUS, poll, Reply, Session, signIn, str } from '../support/dev';
import { book, cancel, feed, preview } from '../support/flows';
import { cancelSplit, planned, RULED_DAYS } from '../support/plan';
import { emailOf, flag, loadRun, Role, RunOrder, RunState, saveRun, scratch, setFlag } from '../support/run';
import { noticeHours, pragueSlot } from '../support/slots';
import { cachedAdminAs } from '../support/web';

// Stage 'orders', 30: the cancellation tiers. Each order is previewed and cancelled once its oops window has passed
// (15 minutes from the booking, 60 on a first booking), and the cancel must record what the preview said. tierB and
// F1 are only previewed here: the customer web cancels them (orders/40). D2, a late cash cancel, opens a receivable,
// and then the debtor can book nothing until it is paid (orders/50).
// Not serial: each test skips on the state it needs, so one failure does not stop the jobs after it.

let run: RunState;
const TIER = ['FreeNotAccepted', 'FreeOopsWindow', 'FreeOutsideWindow', 'Partial', 'LastMinute'];
const MINUTE = 60_000;
const sessions: Record<string, { s: Session; at: number }> = {};

async function as(role: Role, host: Host = 'customer'): Promise<Session> {
  const key = `${host}:${role}`;
  const c = sessions[key];
  if (c && Date.now() - c.at < 8 * 60_000) return c.s;
  const s = await signIn(host, emailOf(run, role), run.password);
  sessions[key] = { s, at: Date.now() };
  return s;
}

/**
 * The tier and rate a cancel at `at` is priced at (BR #cancellation, BookingPolicy.ClassifyCancellation): nobody on the
 * job, free; within the oops window of the booking, free; then 24 h of notice or more free, 4 to 24 h 25 %, under 4 h 50 %.
 */
function expected(o: RunOrder, oops: number, at = new Date()): [string, number] {
  if (!o.taken) return ['FreeNotAccepted', 0];
  if (at.getTime() - Date.parse(o.createdAt ?? '') <= oops * MINUTE) return ['FreeOopsWindow', 0];
  const h = noticeHours(new Date(o.slot), at);
  return h >= 24 ? ['FreeOutsideWindow', 0] : h >= 4 ? ['Partial', 0.25] : ['LastMinute', 0.5];
}

/** The run's order, once its oops window (plus a minute of clock slack) is behind it; skips with the time it can run. */
function due(key: string, oops = 15): RunOrder {
  const o = run.orders[key];
  test.skip(!o, `run ${run.runId} has no ${key} order`);
  test.skip(!!o.cancelled, `${key} is cancelled`);
  const ready = Date.parse(o.createdAt ?? '') + (oops + 1) * MINUTE;
  test.skip(Date.now() < ready, `${key} was booked at ${o.createdAt}: run orders again after ${new Date(ready).toISOString()}`);
  return o;
}

/** The preview, checked against the tier the rules give and, on the real run, the tier the plan expects. */
function checkPreview(key: string, o: RunOrder, p: Reply, oops: number) {
  expect(p.status, `${key}: ${JSON.stringify(p.body)}`).toBe(200);
  const [tier, rate] = expected(o, oops);
  expect(named(p.body, 'tier', TIER), key).toBe(tier);
  expect(Number(str(p.body, 'feeRate')), key).toBe(rate);
  if (!scratch() && planned(key).cancelFee !== undefined) expect(rate, `${key}: the plan's tier`).toBe(planned(key).cancelFee);
  // CancellationAssessor: refund = round(T x (1 - rate), 2); the fee is the rest.
  expect(Number(str(p.body, 'refundAmount')), key).toBeCloseTo(cancelSplit(o.total, rate).refund, 2);
  expect(Number(str(p.body, 'feeAmount')), key).toBeCloseTo(cancelSplit(o.total, rate).fee, 2);
  expect(Number(str(p.body, 'oopsWindowMinutes')), key).toBe(oops);
  return { tier, rate };
}

async function cancelled(key: string, o: RunOrder, c: Reply) {
  expect(c.status, `${key}: ${JSON.stringify(c.body)}`).toBe(200);
  o.cancelled = true;
  saveRun(run);
}

const adminDetail = async (id: string) => (await call(await cachedAdminAs('admin'), 'GET', `/api/AdminOrder/details/${id}`)).body;

/** CANCEL-008: the cleaner's list drops it, its detail reads Cancelled and its feed has the notice. */
async function cleanerTold(role: Role, orderId: string) {
  const s = await as(role, 'partner');
  const employeeId = str((await call(s, 'GET', '/api/Employee/GetCurrentEmployee')).body, 'id');
  const mine = list((await call(s, 'GET', `/api/Order/GetPaged?Filter.EmployeeId=${employeeId}&Offset=0&Limit=200`)).body, 'data');
  expect(mine.some((x) => str(x, 'id') === orderId)).toBe(false);
  expect(named((await call(s, 'GET', `/api/Order/GetById?OrderId=${orderId}`)).body, 'orderStatus', ORDER_STATUS)).toBe('Cancelled');
  const rows = await poll(() => feed(run, role, 'partner-mobile'), (v) => v.some((n) => str(n, 'args.orderId') === orderId), 60_000);
  expect(rows.find((n) => str(n, 'args.orderId') === orderId)).toMatchObject({ eventKey: 'order.assignment_cancelled' });
}

/** Previews then cancels a paid card job of the run's customer and checks the cancel against the preview and the refund. */
async function previewAndCancel(key: string, payment: string) {
  const o = due(key);
  const s = await as(o.customer ?? 'customer');
  const p = await preview(s, o.id);
  const { rate } = checkPreview(key, o, p, 15);
  const c = await cancel(s, o.id, `E2E run ${run.runId} ${key}`);
  await cancelled(key, o, c);
  expect(Number(str(c.body, 'feeRate'))).toBe(rate);
  expect(Number(str(c.body, 'refundAmount'))).toBeCloseTo(Number(str(p.body, 'refundAmount')), 2);
  const d = await poll(() => adminDetail(o.id), (v) => named(v, 'paymentStatus', PAYMENT_STATUS) === payment, 90_000);
  expect(named(d, 'orderStatus', ORDER_STATUS)).toBe('Cancelled');
  expect(named(d, 'paymentStatus', PAYMENT_STATUS)).toBe(payment);
  if (o.taken) await cleanerTold('cleaner', o.id);
}

const pragueDate = (iso: string) => new Date(iso).toLocaleDateString('en-CA', { timeZone: 'Europe/Prague' });
/** A planned Day 1 + 1 order's slot, on the day the run booked tierA (Day 1 + 1, real or scratch). */
const dayPlus1Slot = (key: string) => {
  const p = planned(key);
  return pragueSlot(pragueDate(run.orders['tierA']?.slot ?? `${RULED_DAYS['D1+1']}T12:00:00Z`), p.slot === 'express' ? '10:00' : p.slot.prague);
};

test.describe('orders: tiers', () => {
  test.beforeAll(() => {
    run = loadRun();
  });

  test('a job a cleaner took, cancelled 24 h or more ahead and after its oops window, is free and refunded in full (tierA)', { tag: ['@E2E-CANCEL-003', '@E2E-CANCEL-005', '@E2E-CANCEL-008'] }, async () => {
    await previewAndCancel('tierA', 'Refunded');
  });

  test('a job cancelled under 4 h ahead costs half, and half is refunded (tierC)', { tag: ['@E2E-CANCEL-007', '@E2E-CANCEL-005', '@E2E-CANCEL-008'] }, async () => {
    const o = due('tierC');
    const [, rate] = expected(o, 15);
    test.skip(rate === 0, `tierC at ${o.slot} has ${noticeHours(new Date(o.slot), new Date()).toFixed(1)} h of notice: free, not the tier this case is about`);
    await previewAndCancel('tierC', 'PartiallyRefunded');
  });

  test('the 4-24 h preview states 25 %, three quarters back and the 15-minute window; the customer web cancels it (tierB)', { tag: ['@E2E-CANCEL-006', '@E2E-CANCEL-005'] }, async () => {
    const o = due('tierB');
    checkPreview('tierB', o, await preview(await as('customer'), o.id), 15);
    setFlag(run, 'tierB.previewed');
  });

  test('a first booking a cleaner took previews free inside its 60-minute oops window; the customer web cancels it (F1)', { tag: ['@E2E-CANCEL-002', '@E2E-CANCEL-005'] }, async () => {
    const o = due('F1');
    test.skip(!o.taken, 'F1 was not taken by E (orders/20)');
    test.skip(Date.now() > Date.parse(o.createdAt ?? '') + 55 * MINUTE, `F1's 60-minute window from ${o.createdAt} is nearly over`);
    checkPreview('F1', o, await preview(await as('first'), o.id), 60);
    setFlag(run, 'f1.previewed');
  });

  test('a job nobody took previews free and is cancelled free (K2, F2)', { tag: ['@E2E-CANCEL-005'] }, async () => {
    for (const key of ['K2', 'F2']) {
      const o = run.orders[key];
      if (!o || o.cancelled) continue;
      // K2 is one of cashcap's two unpaid cash bookings: it stays until the customer web has met the cap (orders/40).
      if (key === 'K2' && !flag(run, 'book037.webDone')) continue;
      const s = await as(o.customer ?? 'customer');
      const p = await preview(s, o.id);
      expect(named(p.body, 'tier', TIER), key).toBe('FreeNotAccepted');
      expect(Number(str(p.body, 'feeAmount')), key).toBe(0);
      const c = await cancel(s, o.id, `E2E run ${run.runId} ${key}`);
      await cancelled(key, o, c);
      expect(Number(str(c.body, 'feeRate'))).toBe(0);
    }
  });

  test('a late cash cancel charges nothing and opens a cash-cancellation-fee receivable of the fee (D2)', { tag: ['@E2E-CANCEL-016'] }, async () => {
    const o = due('D2');
    test.skip(!o.taken, 'D2 was not taken by A (orders/20)');
    const s = await as('debtor');
    const p = await preview(s, o.id);
    expect(p.status, JSON.stringify(p.body)).toBe(200);
    const [, rate] = expected(o, 15);
    expect(Number(str(p.body, 'feeRate'))).toBe(rate);
    test.skip(rate === 0, `D2 cancelled now would be free (${noticeHours(new Date(o.slot), new Date()).toFixed(1)} h of notice)`);
    const { fee } = cancelSplit(o.total, rate);
    const c = await cancel(s, o.id, `E2E run ${run.runId} D2`);
    await cancelled('D2', o, c);
    expect(c.body).toMatchObject({ refundInitiated: false });
    expect(Number(str(c.body, 'refundAmount'))).toBe(0);
    const mine = list((await call(s, 'GET', '/api/Receivable/GetMine')).body, '').find((r) => str(r, 'orderId') === o.id);
    expect(mine, 'the debtor owes the fee').toBeTruthy();
    expect(str(mine, 'kind.name')).toBe('CashCancellationFee');
    expect(Number(str(mine, 'amount'))).toBeCloseTo(fee, 2);
    expect(str(mine, 'currencyCode')).toBe('CZK');
    run.receivables = { ...run.receivables, D2: str(mine, 'id') };
    saveRun(run);
    const admin = list((await call(await cachedAdminAs('admin'), 'GET', `/api/AdminReceivable/get-paged?Offset=0&Limit=10&Filter.OrderId=${o.id}`)).body, 'data');
    expect(admin.map((r) => [str(r, 'status.name'), Number(str(r, 'amount'))])).toEqual([['Open', fee]]);
    // Nothing was charged: the cash order took no payment and gives none back.
    expect(list((await call(await cachedAdminAs('admin'), 'GET', `/api/AdminRefund/get-paged?Offset=0&Limit=5&Filter.OrderId=${o.id}`)).body, 'data')).toEqual([]);
    expect(named(await adminDetail(o.id), 'paymentStatus', PAYMENT_STATUS)).toBe('Pending');
    setFlag(run, 'd2.receivableOpen');
  });

  test('someone who owes books nothing, cash or card; a guest booking with the same e-mail is not matched (G1)', { tag: ['@E2E-BOOK-038'] }, async () => {
    test.skip(!run.receivables?.['D2'], 'D2 opened no receivable yet');
    const s = await as('debtor');
    const addressId = str(list((await call(s, 'GET', '/api/SavedAddress/GetMine')).body, '')[0], 'id');
    const before = list((await call(s, 'GET', '/api/Order/GetPaged?Offset=0&Limit=100')).body, 'data').length;
    for (const [key, payment] of [['D3', 'cash'], ['D4', 'card']] as const) {
      const r = await book(s, run, `probe-${key}`, { services: ['Bathroom Cleaning'], rooms: 1, bathrooms: 1, level: 0, slot: dayPlus1Slot(key), payment, customer: 'debtor', addressId });
      expect(r.status, `${payment}: ${JSON.stringify(r.body)}`).toBe(400);
      expect(errorKeys(r.body), payment).toContain('order.unpaid_receivable');
    }
    expect(list((await call(s, 'GET', '/api/Order/GetPaged?Offset=0&Limit=100')).body, 'data')).toHaveLength(before);
    if (!run.orders['G1']) {
      const g = await book(await anonymous('customer'), run, 'G1', { services: ['Bathroom Cleaning'], rooms: 1, bathrooms: 1, level: 0, slot: dayPlus1Slot('G1'), payment: 'card', customer: 'debtor', guest: true });
      expect(g.status, JSON.stringify(g.body)).toBe(200);
    }
  });
});
