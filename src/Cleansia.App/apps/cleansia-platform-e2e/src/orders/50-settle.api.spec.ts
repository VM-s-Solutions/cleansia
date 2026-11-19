import { expect, test } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { join } from 'node:path';
import { anonymous, call, errorKeys, Host, list, named, ORDER_STATUS, PAYMENT_STATUS, payStripeCheckout, poll, Reply, Session, signIn, str } from '../support/dev';
import { book, cancel, feed } from '../support/flows';
import { cancelSplit, planned, RULED_DAYS } from '../support/plan';
import { emailOf, flag, loadRun, Role, RunOrder, RunState, saveRun, scratch, setFlag } from '../support/run';
import { pragueSlot } from '../support/slots';
import { cachedAdminAs } from '../support/web';

// Stage 'orders', 50: what follows the customer web's cancels and the receivable (orders/40): the refunds and audit
// rows of F1 and tierB, the debtor paying what is owed through the pay link and booking again, the fee receipt, the
// placed cleaner's contract on a cancelled job, and the throwaway service switched off. A scratch run has no web
// half of its own: it cancels F1 and tierB through the API once they are previewed (orders/30).
// Not serial: each test skips on the state it needs, so one failure does not stop the jobs after it.

let run: RunState;
const sessions: Record<string, { s: Session; at: number }> = {};

async function as(role: Role, host: Host = 'customer'): Promise<Session> {
  const key = `${host}:${role}`;
  const c = sessions[key];
  if (c && Date.now() - c.at < 8 * 60_000) return c.s;
  const s = await signIn(host, emailOf(run, role), run.password);
  sessions[key] = { s, at: Date.now() };
  return s;
}

const admin = () => cachedAdminAs('admin');
const adminDetail = async (id: string) => (await call(await admin(), 'GET', `/api/AdminOrder/details/${id}`)).body;
const refunds = async (orderId: string) => list((await call(await admin(), 'GET', `/api/AdminRefund/get-paged?Offset=0&Limit=10&Filter.OrderId=${orderId}`)).body, 'data');

/**
 * The order the customer web cancelled (its flag set by orders/40). A scratch run cancels it here through the API
 * once orders/30 previewed it; returns the API cancel reply then.
 */
async function webCancelled(key: string, flagName: string, previewed: string): Promise<{ o: RunOrder; reply?: Reply }> {
  const o = run.orders[key];
  test.skip(!o, `run ${run.runId} has no ${key} order`);
  if (flag(run, flagName) || o.cancelled) return { o };
  test.skip(!scratch(), `the customer web has not cancelled ${key} yet (${flagName}, orders/40)`);
  test.skip(!flag(run, previewed), `${key} was not previewed yet (${previewed}, orders/30)`);
  const reply = await cancel(await as(o.customer ?? 'customer'), o.id, `E2E run ${run.runId} ${key} (scratch: API cancel)`);
  expect(reply.status, JSON.stringify(reply.body)).toBe(200);
  o.cancelled = true;
  saveRun(run);
  return { o, reply };
}

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

const pragueDate = (iso: string) => new Date(iso).toLocaleDateString('en-CA', { timeZone: 'Europe/Prague' });
const dayPlus1Slot = (key: string) => {
  const p = planned(key);
  return pragueSlot(pragueDate(run.orders['tierA']?.slot ?? `${RULED_DAYS['D1+1']}T12:00:00Z`), p.slot === 'express' ? '10:00' : p.slot.prague);
};

test.describe('orders: settle', () => {
  test.beforeAll(() => {
    run = loadRun();
  });

  test('the first booking cancelled inside its 60-minute window is refunded in full; the audit names the first-booking rule; E is told (F1)', { tag: ['@E2E-CANCEL-002', '@E2E-CANCEL-008'] }, async () => {
    const { o, reply } = await webCancelled('F1', 'f1.webCancelled', 'f1.previewed');
    if (reply) expect(Number(str(reply.body, 'refundAmount'))).toBe(o.total);
    const d = await poll(() => adminDetail(o.id), (v) => named(v, 'paymentStatus', PAYMENT_STATUS) === 'Refunded', 90_000);
    expect(named(d, 'orderStatus', ORDER_STATUS)).toBe('Cancelled');
    expect(named(d, 'paymentStatus', PAYMENT_STATUS)).toBe('Refunded');
    const refund = (await refunds(o.id))[0];
    expect(Number(str(refund, 'amount'))).toBe(o.total);
    const rows = list((await call(await admin(), 'GET', `/api/CustomerAudit/get-paged?Offset=0&Limit=10&Filter.ResourceId=${o.id}&Filter.Action=customer.order.cancel&Filter.Success=true`)).body, 'data');
    expect(rows.length, 'one successful cancel audit row').toBeGreaterThan(0);
    const payload = str((await call(await admin(), 'GET', `/api/CustomerAudit/get-by-id/${str(rows[0], 'id')}`)).body, 'payloadJson');
    expect(payload).toMatch(/"oopsMinutesApplied"\s*:\s*60\b/i);
    expect(payload).toMatch(/"oopsRuleApplied"\s*:\s*("firstBooking"|1)[,}\s]/i);
    if (o.taken) await cleanerTold('cleaner3', o.id);
  });

  test('the 25 % cancel refunds three quarters by Stripe, origin cancellation; A is told (tierB)', { tag: ['@E2E-CANCEL-006', '@E2E-CANCEL-008'] }, async () => {
    const { o, reply } = await webCancelled('tierB', 'tierB.webCancelled', 'tierB.previewed');
    // BR #cancellation, 4-24 h: fee 25 % (a scratch run's tierB may sit 24 h or more away: free). CancellationAssessor:
    // refund = round(T x (1 - rate), 2).
    const rate = Number(str(await adminDetail(o.id), 'cancellationFeeRate'));
    if (!scratch()) expect(rate).toBe(0.25);
    const refundDue = cancelSplit(o.total, rate).refund;
    const payment = rate ? 'PartiallyRefunded' : 'Refunded';
    if (reply) expect(reply.body).toMatchObject({ feeRate: rate, refundPending: false });
    const d = await poll(() => adminDetail(o.id), (v) => named(v, 'paymentStatus', PAYMENT_STATUS) === payment, 90_000);
    expect(named(d, 'orderStatus', ORDER_STATUS)).toBe('Cancelled');
    expect(named(d, 'paymentStatus', PAYMENT_STATUS)).toBe(payment);
    const refund = await poll(async () => (await refunds(o.id))[0], (r) => str(r, 'state.name') === 'Refunded', 90_000);
    expect(str(refund, 'state.name')).toBe('Refunded');
    expect(str(refund, 'origin.name')).toBe('Cancellation');
    expect(Number(str(refund, 'amount'))).toBeCloseTo(refundDue, 2);
    if (o.taken) await cleanerTold('cleaner', o.id);
  });

  test('the debtor pays the receivable through its pay link; nothing about D2 itself changes', { tag: ['@E2E-PAY-041'] }, async ({ page }) => {
    const id = run.receivables?.['D2'];
    test.skip(!id, 'D2 opened no receivable (orders/30)');
    test.skip(!flag(run, 'receivable.webShown') && !scratch(), 'the customer web has not shown the receivable yet (receivable.webShown, orders/40)');
    const s = await as('debtor');
    const d2 = run.orders['D2'];
    const before = await adminDetail(d2.id);
    const mine = list((await call(s, 'GET', '/api/Receivable/GetMine')).body, '');
    if (mine.some((r) => str(r, 'id') === id)) {
      const owed = mine.find((r) => str(r, 'id') === id);
      expect(str(owed, 'kind.name')).toBe('CashCancellationFee');
      // The fee CANCEL-016 recorded: D2's total at the rate frozen on its cancel (25 % of 300 = 75 on the real run).
      expect(Number(str(owed, 'amount'))).toBeCloseTo(cancelSplit(d2.total, Number(str(before, 'cancellationFeeRate'))).fee, 2);
      expect(str(owed, 'currencyCode')).toBe('CZK');
      const link = await call(s, 'POST', `/api/Receivable/CreatePayLink/${id}`);
      expect(link.status, JSON.stringify(link.body)).toBe(200);
      await payStripeCheckout(page, str(link.body, 'checkoutUrl'), emailOf(run, 'debtor'));
    }
    const row = await poll(
      async () => list((await call(await admin(), 'GET', `/api/AdminReceivable/get-paged?Offset=0&Limit=10&Filter.OrderId=${d2.id}`)).body, 'data')[0],
      (r) => str(r, 'status.name') === 'Paid',
      120_000,
    );
    expect(str(row, 'status.name')).toBe('Paid');
    expect(list((await call(s, 'GET', '/api/Receivable/GetMine')).body, '')).toEqual([]);
    const after = await adminDetail(d2.id);
    expect(named(after, 'paymentStatus', PAYMENT_STATUS)).toBe(named(before, 'paymentStatus', PAYMENT_STATUS));
    expect(await refunds(d2.id)).toEqual([]);
  });

  test('the paid fee is not the sale\'s receipt: the order download has none, and the fee receipt is read on DEV', { tag: ['@E2E-PAY-042'] }, async () => {
    const d2 = run.orders['D2'];
    const id = run.receivables?.['D2'];
    test.skip(!d2 || !id, 'D2 opened no receivable');
    // The fee receipt is written when the fee is paid (PAY-041): reading it before would find no row.
    test.skip(list((await call(await as('debtor'), 'GET', '/api/Receivable/GetMine')).body, '').length > 0, 'the debtor has not paid the fee yet (PAY-041)');
    const r = await call(await as('debtor'), 'GET', `/api/Order/DownloadReceipt?OrderId=${d2.id}`);
    expect(errorKeys(r.body), JSON.stringify(r.body)).toContain('receipt.not_found');
    // Owner ruling 4, 2026-11-15: the fee receipt's row is read by a read-only DEV-only SQL (a workflow dispatch).
    test.skip(scratch(), 'E2E_SCRATCH=1: a scratch run dispatches no SQL but the confirm script');
    test.setTimeout(20 * 60_000);
    const out = execFileSync('bash', [join(__dirname, '..', '..', 'scripts', 'read-fee-receipts.sh'), d2.id], { encoding: 'utf8', timeout: 18 * 60_000 });
    console.log(out);
    const line = out.split('\n').find((l) => l.includes(d2.id) && l.includes(id ?? '-'));
    expect(line, 'a fee receipt row for D2 that names its receivable').toBeTruthy();
  });

  test('once the debt is paid the debtor books cash and card again; both are then cancelled (D3, D4)', { tag: ['@E2E-BOOK-038'] }, async () => {
    test.skip(!run.receivables?.['D2'], 'D2 opened no receivable');
    const s = await as('debtor');
    test.skip(list((await call(s, 'GET', '/api/Receivable/GetMine')).body, '').length > 0, 'the debtor still owes (PAY-041)');
    const addressId = str(list((await call(s, 'GET', '/api/SavedAddress/GetMine')).body, '')[0], 'id');
    for (const key of ['D3', 'D4']) {
      const p = planned(key);
      if (!run.orders[key]) {
        const r = await book(s, run, key, { services: p.services, rooms: p.rooms, bathrooms: p.bathrooms, level: p.level, slot: dayPlus1Slot(key), payment: p.payment, customer: 'debtor', addressId });
        expect(r.status, `${key}: ${JSON.stringify(r.body)}`).toBe(200);
      }
      const o = run.orders[key];
      if (o.cancelled) continue;
      const c = await cancel(s, o.id, `E2E run ${run.runId} ${key}`);
      expect(c.status, JSON.stringify(c.body)).toBe(200);
      expect(Number(str(c.body, 'feeRate'))).toBe(0);
      o.cancelled = true;
      saveRun(run);
    }
  });

  test('a cancelled job refuses the placed cleaner\'s contract (tierA)', { tag: ['@E2E-TAKE-050'] }, async () => {
    const o = run.orders['tierA'];
    test.skip(!o?.cancelled, 'tierA is not cancelled yet (orders/30)');
    const s = await as('cleaner', 'partner');
    const r = await call(s, 'POST', '/api/Order/AcceptWorkContract', { orderId: o.id, acceptedWorkContractTextId: o.textId ?? '01AAAAAAAAAAAAAAAAAAAAAAAA' });
    expect(errorKeys(r.body), JSON.stringify(r.body)).toContain('order.take.already_cancelled');
  });

  test('the Manager switches the two-cleaner service off once the customer web has used it; W keeps it', async () => {
    test.skip(!run.serviceId, 'no throwaway service on this run');
    test.skip(!flag(run, 'book035.webDone'), 'the customer web has not finished BOOK-035 yet (book035.webDone, orders/40)');
    test.skip(!!flag(run, 'service.off'), `switched off at ${flag(run, 'service.off')}`);
    const r = await call(await cachedAdminAs('manager', run), 'POST', `/api/AdminService/deactivate/${run.serviceId}`);
    expect(r.status, JSON.stringify(r.body)).toBe(200);
    const catalogue = (await call(await anonymous('customer'), 'GET', `/api/Service/GetOverview?countryId=${run.countryId}`)).body as unknown[];
    expect(catalogue.map((x) => str(x, 'id'))).not.toContain(run.serviceId);
    expect(named(await adminDetail(run.orders['W'].id), 'orderStatus', ORDER_STATUS)).toBe('Confirmed');
    setFlag(run, 'service.off');
  });
});
