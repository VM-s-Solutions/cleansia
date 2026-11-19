import { expect, test } from '@playwright/test';
import { call, errorKeys, Host, list, named, num, ORDER_STATUS, PAYMENT_STATUS, poll, Reply, Session, signIn, str } from '../support/dev';
import { afterPhoto, complete, contractText, feed, markCash, openPeriod, periodPays, serviceIds, start } from '../support/flows';
import { planned } from '../support/plan';
import { emailOf, flag, loadRun, Role, RunOrder, RunState, saveRun, scratch, setFlag } from '../support/run';
import { startOpensAt } from '../support/slots';
import { cachedAdminAs } from '../support/web';

// Stage 'work', 30: the rest of Day 1 in its windows, after the web checks of work/20. X starts late and is refunded
// line by line (12:03); Z is refunded in full while in progress (12:08); the two-cleaner job W cannot be dropped once
// under way and completes only when the whole crew has marked its part (13:32, 13:45); the placed job P (15:47); and
// the debt of D1 written off once the day's work is done.
// Not serial: each test skips on the state it needs, so one failure does not stop the jobs after it.

let run: RunState;
/** A refusal's keys: one field's several keys arrive joined by '; '. */
const keys = (body: unknown) => errorKeys(body).flatMap((k) => k.split('; '));
const sessions: Record<string, { s: Session; at: number }> = {};
const round2 = (x: number) => Math.round(x * 100) / 100;

async function as(role: Role, host: Host = 'partner'): Promise<Session> {
  const key = `${host}:${role}`;
  const c = sessions[key];
  if (c && Date.now() - c.at < 8 * 60_000) return c.s;
  const s = await signIn(host, emailOf(run, role), run.password);
  sessions[key] = { s, at: Date.now() };
  return s;
}

const support = () => cachedAdminAs(scratch() ? 'admin' : 'support', run);
const adminDetail = async (id: string) => (await call(await cachedAdminAs('admin'), 'GET', `/api/AdminOrder/details/${id}`)).body;
const statusOf = (o: unknown) => named(o, 'orderStatus', ORDER_STATUS);
const employeeOf = async (role: Role) => str((await call(await as(role), 'GET', '/api/Employee/GetCurrentEmployee')).body, 'id');
const drop = (s: Session, id: string) => call(s, 'POST', '/api/Order/DropOrder', { orderId: id });
const markDone = (s: Session, id: string) => call(s, 'POST', '/api/Order/MarkMyPartDone', { orderId: id });

function inWindow(key: string): RunOrder {
  const o = run.orders[key];
  test.skip(!o?.taken, `run ${run.runId} has no taken ${key} job`);
  test.skip(!!o.completed || !!o.cancelled, `${key} is ${o.completed ? 'completed' : 'cancelled'}`);
  const opens = startOpensAt(new Date(o.slot));
  test.skip(Date.now() < opens.getTime(), `${key} (slot ${o.slot}) can be started from ${opens.toISOString()}; run work again then`);
  return o;
}

/** A's earlier job of the day is taken and not yet done (StartOrder allows one job in progress per cleaner). */
const busyWith = (key: string) => !!run.orders[key]?.taken && !run.orders[key].completed && !run.orders[key].cancelled;

function completed(o: RunOrder) {
  o.completed = true;
  saveRun(run);
}

const refused = (r: Reply, key: string) => expect(keys(r.body), JSON.stringify(r.body)).toContain(key);

test.describe('work: afternoon', () => {
  test.beforeAll(() => {
    run = loadRun();
  });

  test('X starts late and is completed; a line refund pays that line\'s share, a larger one is clamped to what is left, then nothing is left', { tag: ['@E2E-CANCEL-051', '@E2E-CANCEL-053'] }, async () => {
    const o = run.orders['X'];
    test.skip(!o?.taken, 'X was not taken');
    test.skip(!flag(run, 'x.webChecked'), 'the customer web has not checked X past its start yet (x.webChecked, work/20)');
    test.skip(!!flag(run, 'x.refunded'), `X was refunded at ${flag(run, 'x.refunded')}`);
    const a = await as('cleaner');
    if (!o.completed) {
      if (statusOf(await adminDetail(o.id)) === 'Confirmed') expect((await start(a, o.id)).status).toBe(200);
      expect((await afterPhoto(a, o.id, `E2E run ${run.runId}`)).status).toBe(200);
      const done = await complete(a, o.id);
      expect(done.status, JSON.stringify(done.body)).toBe(200);
      completed(o);
    }
    // Each line's gross, from the platform's own quote of X's selection (no date: no express, no lead time).
    const p = planned('X');
    const customer = await as('customer', 'customer');
    const [bath, kitchen, windows] = await serviceIds(customer, run, p.services);
    const q = await call(customer, 'POST', '/api/Order/Quote', {
      selectedServiceIds: [bath, kitchen, windows], selectedPackageIds: [], rooms: p.rooms, bathrooms: p.bathrooms, currencyId: null,
      selectedExtraSlugs: p.extras, cleaningDate: null, countryId: run.countryId, dirtinessLevel: p.level,
    });
    expect(q.status, JSON.stringify(q.body)).toBe(200);
    const lines = list(q.body, 'lines');
    const gross = (id: string) => num(lines.find((l) => str(l, 'itemId') === id), 'amount');
    const G = lines.reduce<number>((sum, l) => sum + num(l, 'amount'), 0);
    // A booking made under 4 h ahead carries the 20 % express surcharge on top of the lines (BookingPolicy); the plan
    // books X earlier than that, but an orders invocation that ran late does not.
    const express = Date.parse(o.slot) - Date.parse(o.createdAt ?? '') < 4 * 3_600_000 ? round2(0.2 * G) : 0;
    expect(G + express, 'the lines, with any express surcharge, sum to the total X was booked at').toBeCloseTo(o.total, 2);
    const T = o.total;
    // RefundAllocator: a selection's refund = round(its grosses / all grosses (the extra included) x T, 2). Reason 4,
    // ServiceNotRendered: the platform absorbs the Stripe fee, so the amount sent is the share (IssuePartialRefund).
    const share = (...ids: string[]) => round2((ids.reduce((s, id) => s + gross(id), 0) / G) * T);
    const partial = async (...ids: string[]) =>
      call(await support(), 'POST', '/api/AdminRefund/partial', { orderId: o.id, lines: ids.map((serviceId) => ({ serviceId, packageId: null })), reason: 4, overrideReason: null });
    const r1 = await partial(bath);
    expect(r1.status, JSON.stringify(r1.body)).toBe(200);
    expect(num(r1.body, 'refundAmount')).toBeCloseTo(share(bath), 2);
    expect(named(r1.body, 'paymentStatus', PAYMENT_STATUS)).toBe('PartiallyRefunded');
    const rows = list((await call(await cachedAdminAs('admin'), 'GET', `/api/AdminRefund/get-paged?Offset=0&Limit=10&Filter.OrderId=${o.id}`)).body, 'data');
    expect(rows.map((r) => str(r, 'origin.name'))).toContain('PartialRefund');
    // CANCEL-053: a second line, then a selection larger than what is left (clamped), then nothing left.
    const r2 = await partial(kitchen);
    expect(num(r2.body, 'refundAmount'), JSON.stringify(r2.body)).toBeCloseTo(share(kitchen), 2);
    const left = round2(T - num(r1.body, 'refundAmount') - num(r2.body, 'refundAmount'));
    expect(share(bath, windows), 'the third selection asks more than is left').toBeGreaterThan(left);
    const r3 = await partial(bath, windows);
    expect(r3.status, JSON.stringify(r3.body)).toBe(200);
    expect(num(r3.body, 'refundAmount')).toBeCloseTo(left, 2);
    expect(named(r3.body, 'paymentStatus', PAYMENT_STATUS)).toBe('Refunded');
    setFlag(run, 'x.refunded');
    refused(await partial(kitchen, windows), 'refund.nothing_refundable');
    expect(named(await adminDetail(o.id), 'paymentStatus', PAYMENT_STATUS)).toBe('Refunded');
  });

  // Its own test, so a missing notice fails CANCEL-051 alone and a later invocation reads the feed again.
  test('X\'s customer has the refund notice in the feed', { tag: ['@E2E-CANCEL-051'] }, async () => {
    const o = run.orders['X'];
    test.skip(!flag(run, 'x.refunded'), 'X has not been refunded yet (x.refunded)');
    const refunded = (n: unknown) => str(n, 'args.orderId') === o.id && str(n, 'eventKey') === 'order.refunded';
    const notices = await poll(() => feed(run, 'customer', 'customer-mobile'), (v) => v.some(refunded), 60_000);
    expect(notices.some(refunded), 'order.refunded for X in the customer feed').toBe(true);
  });

  test('Z, refunded in full while in progress, cannot then take cash, and still completes', { tag: ['@E2E-EXEC-027'] }, async () => {
    const o = inWindow('Z');
    test.skip(busyWith('X'), 'A finishes X first: one job in progress per cleaner');
    const a = await as('cleaner');
    if (statusOf(await adminDetail(o.id)) === 'Confirmed') expect((await start(a, o.id)).status).toBe(200);
    if (named(await adminDetail(o.id), 'paymentStatus', PAYMENT_STATUS) === 'Paid') {
      const r = await call(await support(), 'POST', '/api/AdminOrder/refund', { orderId: o.id });
      expect(r.status, JSON.stringify(r.body)).toBe(200);
    }
    refused(await markCash(a, o.id), 'order.payment_not_outstanding');
    expect((await afterPhoto(a, o.id, `E2E run ${run.runId}`)).status).toBe(200);
    const done = await complete(a, o.id);
    expect(done.status, JSON.stringify(done.body)).toBe(200);
    completed(o);
  });

  test('W under way: neither cleaner can drop it; a Complete with nobody marked, and with only A marked, is refused; A\'s second mark keeps the first time', { tag: ['@E2E-TAKE-044', '@E2E-EXEC-043', '@E2E-EXEC-042'] }, async () => {
    const o = inWindow('W');
    test.skip(!!flag(run, 'w.aMarked'), `A marked W at ${flag(run, 'w.aMarked')}`);
    test.skip(!flag(run, 'w.webOnTheWay'), 'B has not set off on the partner web yet (w.webOnTheWay, work/20)');
    test.skip(busyWith('Z'), 'A finishes Z first: one job in progress per cleaner');
    const [a, b] = [await as('cleaner'), await as('cleaner2')];
    refused(await drop(b, o.id), 'order.not_confirmed');
    if (statusOf(await adminDetail(o.id)) !== 'InProgress') expect((await start(a, o.id)).status).toBe(200);
    refused(await drop(a, o.id), 'order.not_confirmed');
    expect(list(await adminDetail(o.id), 'assignedEmployees')).toHaveLength(2);
    expect((await afterPhoto(a, o.id, `E2E run ${run.runId}`)).status).toBe(200);
    refused(await complete(a, o.id), 'order.crew_not_done');
    const first = await markDone(a, o.id);
    expect(first.status, JSON.stringify(first.body)).toBe(200);
    expect(first.body).toMatchObject({ crewDone: false });
    const second = await markDone(a, o.id);
    // The same instant: the first reply carries the clock's 100 ns ticks, the second the stored microseconds.
    expect(Date.parse(str(second.body, 'completionConfirmedAt'))).toBe(Date.parse(str(first.body, 'completionConfirmedAt')));
    expect(Number.isNaN(Date.parse(str(first.body, 'completionConfirmedAt')))).toBe(false);
    refused(await complete(a, o.id), 'order.crew_not_done');
    expect(statusOf(await adminDetail(o.id))).toBe('InProgress');
    setFlag(run, 'w.aMarked');
  });

  test('W: the second cleaner marks and completes for the crew; one receipt; both cleaners are paid for it', { tag: ['@E2E-EXEC-042'] }, async () => {
    const o = inWindow('W');
    test.skip(!flag(run, 'w.aMarked'), 'A has not marked W yet');
    test.skip(!flag(run, 'w.webInProgressChecked'), 'the partner web has not checked W in progress yet (w.webInProgressChecked, work/20)');
    const b = await as('cleaner2');
    const mark = await markDone(b, o.id);
    expect(mark.status, JSON.stringify(mark.body)).toBe(200);
    expect(mark.body).toMatchObject({ crewDone: true });
    const done = await complete(b, o.id);
    expect(done.status, JSON.stringify(done.body)).toBe(200);
    completed(o);
    const d = await adminDetail(o.id);
    expect(statusOf(d)).toBe('Completed');
    expect(str(d, 'receiptNumber')).not.toBe('');
    const customer = await as('customer', 'customer-mobile');
    const pdf = await poll(() => call(customer, 'GET', `/api/Order/DownloadReceipt?OrderId=${o.id}`), (r) => r.status === 200, 90_000);
    expect(pdf.headers['content-type']).toContain('application/pdf');
    const period = await openPeriod();
    for (const role of ['cleaner', 'cleaner2'] as Role[]) {
      const [id, s] = [await employeeOf(role), await as(role)];
      const pays = await poll(() => periodPays(s, id, period?.id ?? ''), (v) => v.some((x) => str(x, 'orderId') === o.id));
      expect(pays.map((x) => str(x, 'orderId')), role).toContain(o.id);
    }
  });

  test('P: on the way needs no contract and allows no drop; Start needs the contract; two accepts keep one acceptance; started, no drop; completed', { tag: ['@E2E-TAKE-044', '@E2E-EXEC-007', '@E2E-TAKE-050'] }, async () => {
    const o = inWindow('P');
    test.skip(!flag(run, 'p.dropped'), 'P was not dropped and placed (orders/20)');
    test.skip(busyWith('W'), 'A finishes W first: one job in progress per cleaner');
    const a = await as('cleaner');
    const aId = run.employeeId ?? (await employeeOf('cleaner'));
    if (statusOf(await adminDetail(o.id)) === 'Confirmed') {
      const otw = await call(a, 'POST', '/api/Order/NotifyOnTheWay', { orderId: o.id });
      expect(otw.status, JSON.stringify(otw.body)).toBe(200);
    }
    refused(await drop(a, o.id), 'order.not_confirmed');
    const textId = await contractText(a, o.id);
    if (statusOf(await adminDetail(o.id)) !== 'InProgress') {
      refused(await start(a, o.id), 'contract.acceptance_required');
      for (let i = 0; i < 2; i++) {
        const ok = await call(a, 'POST', '/api/Order/AcceptWorkContract', { orderId: o.id, acceptedWorkContractTextId: textId });
        expect(ok.status, JSON.stringify(ok.body)).toBe(200);
      }
      const d = await adminDetail(o.id);
      const seat = list(d, 'assignedEmployees').find((x) => str(x, 'employeeId') === aId);
      expect(list(d, 'workContractAcceptances').filter((x) => str(x, 'orderEmployeeId') === str(seat, 'id'))).toHaveLength(1);
      const started = await start(a, o.id);
      expect(started.status, JSON.stringify(started.body)).toBe(200);
    }
    refused(await drop(a, o.id), 'order.not_confirmed');
    expect((await afterPhoto(a, o.id, `E2E run ${run.runId}`)).status).toBe(200);
    const done = await complete(a, o.id);
    expect(done.status, JSON.stringify(done.body)).toBe(200);
    completed(o);
  });

  test('the Manager writes off what the debtor owes for D1, once the day\'s jobs are done', async () => {
    const id = run.receivables?.['D1'];
    test.skip(!id, 'D1 opened no receivable (work/10)');
    test.skip(scratch(), 'E2E_SCRATCH=1: a scratch run has no Manager');
    test.skip(!run.orders['P']?.completed, 'the write-off follows the last job of the day (P)');
    test.skip(!!flag(run, 'd1.writtenOff'), `written off at ${flag(run, 'd1.writtenOff')}`);
    const r = await call(await cachedAdminAs('manager', run), 'POST', '/api/AdminReceivable/write-off', { receivableId: id, note: `E2E run ${run.runId}: EXEC-029 clean-up` });
    expect(r.status, JSON.stringify(r.body)).toBe(200);
    const row = list((await call(await cachedAdminAs('admin'), 'GET', `/api/AdminReceivable/get-paged?Offset=0&Limit=10&Filter.OrderId=${run.orders['D1'].id}`)).body, 'data')[0];
    expect(str(row, 'status.name')).toBe('WrittenOff');
    setFlag(run, 'd1.writtenOff');
  });
});
