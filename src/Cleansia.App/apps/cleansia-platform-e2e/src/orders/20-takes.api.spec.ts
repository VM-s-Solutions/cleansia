import { expect, test } from '@playwright/test';
import { at, call, errorKeys, Host, list, named, ORDER_STATUS, PAYMENT_STATUS, poll, Session, signIn, str } from '../support/dev';
import { adminFeed, afterPhoto, complete, contractText, feed, markCash, reportNotPaid, start, take } from '../support/flows';
import { emailOf, flag, loadRun, Role, RunOrder, RunState, saveRun, scratch, setFlag } from '../support/run';
import { cachedAdminAs } from '../support/web';

// Stage 'orders', 20: the takes. The race for one seat (R1-R3), the plan's crews on their jobs, the time conflict
// (X, Y, Z), the two-cleaner job seat by seat (W), the last cleaner's drop and the admin's placement (P, and P2
// overridden into progress and completed), and the refusals a Confirmed or Completed job gives a cleaner who is not
// on it or a step out of order. A job taken by an earlier invocation is not taken again.
// Not serial: each test skips on the state it needs, so one failure does not stop the jobs after it.

let run: RunState;
/** A refusal's keys: one field's several keys arrive joined by '; '. */
const keys = (body: unknown) => errorKeys(body).flatMap((k) => k.split('; '));
const loadedAt = new Date().toISOString();
const sessions: Record<string, { s: Session; at: number }> = {};
const reason = () => `E2E run ${run.runId}`;

/** A run account's session on one host, one per 8 minutes; skips the test when the account cannot sign in. */
async function as(role: Role, host: Host = 'partner'): Promise<Session> {
  const key = `${host}:${role}`;
  const c = sessions[key];
  if (c && Date.now() - c.at < 8 * 60_000) return c.s;
  const s = await signIn(host, emailOf(run, role), run.password).catch((e: Error) => {
    test.skip(true, `${emailOf(run, role)} cannot sign in on ${host} (${e.message.slice(0, 80)}): the people stage registers it`);
    throw e;
  });
  sessions[key] = { s, at: Date.now() };
  return s;
}

const employeeOf = async (role: Role) => str((await call(await as(role), 'GET', '/api/Employee/GetCurrentEmployee')).body, 'id');
/** Support places, overrides and refunds; a scratch run has no Support, so the DEV administrator does. */
const support = () => cachedAdminAs(scratch() ? 'admin' : 'support', run);
const adminDetail = async (id: string) => (await call(await cachedAdminAs('admin'), 'GET', `/api/AdminOrder/details/${id}`)).body;
const statusOf = (o: unknown) => named(o, 'orderStatus', ORDER_STATUS);
const crewIds = (o: unknown) => list(o, 'assignedEmployees').map((e) => str(e, 'employeeId'));
const onBoard = async (role: Role, id: string) =>
  list((await call(await as(role), 'GET', `/api/Order/GetPaged?Filter.HasAvailableSpots=true&Filter.Id=${id}&Limit=10`)).body, 'data').some((o) => str(o, 'id') === id);
const inMyList = async (role: Role, employeeId: string, id: string) =>
  list((await call(await as(role), 'GET', `/api/Order/GetPaged?Filter.EmployeeId=${employeeId}&Offset=0&Limit=200`)).body, 'data').some((o) => str(o, 'id') === id);

function order(key: string): RunOrder {
  const o = run.orders[key];
  test.skip(!o, `run ${run.runId} has no ${key} order (orders/10 books it)`);
  return o;
}

function taken(o: RunOrder, textId?: string) {
  o.taken = true;
  if (textId) o.textId = textId;
  saveRun(run);
}

/** Takes a job as one cleaner with the contract text, unless the run already holds it as taken. */
async function takeAs(role: Role, key: string) {
  const o = order(key);
  if (o.taken) return;
  const s = await as(role);
  const textId = await contractText(s, o.id);
  const r = await take(s, o.id, textId);
  expect(r.status, `${role} takes ${key}: ${JSON.stringify(r.body)}`).toBe(200);
  taken(o, textId);
}

test.describe('orders: takes', () => {
  test.beforeAll(() => {
    run = loadRun();
  });

  test('two cleaners race for one seat: one takes it, the other is told no seat; the customer cancels and the cleaner is told', { tag: ['@E2E-TAKE-020', '@E2E-TAKE-023', '@E2E-CANCEL-008'] }, async () => {
    const b = await as('cleaner2');
    const e = await as('cleaner3', 'partner-mobile');
    const ids: Record<string, string> = { cleaner2: run.employee2Id ?? (await employeeOf('cleaner2')), cleaner3: await employeeOf('cleaner3') };
    let raced = 0;
    for (const key of ['R1', 'R2', 'R3']) {
      const o = order(key);
      if (o.cancelled) continue;
      const customer = await as(o.customer ?? 'customer', 'customer');
      // E stays off every fee-bearing job: race only while the cancel below is still inside the oops window, 60 minutes
      // on nocard's first booking (R1), else 15.
      const fresh = Date.now() - Date.parse(o.createdAt ?? '') < ((key === 'R1' ? 60 : 15) - 3) * 60_000;
      if (fresh && !o.taken) {
        const [tb, te] = await Promise.all([contractText(b, o.id), contractText(e, o.id)]);
        const [rb, re] = await Promise.all([take(b, o.id, tb), take(e, o.id, te)]);
        const won = [rb, re].filter((r) => r.status === 200);
        const lost = [rb, re].filter((r) => r.status !== 200);
        expect(won, `${key}: ${JSON.stringify([rb.body, re.body])}`).toHaveLength(1);
        expect(keys(lost[0].body), key).toContain('order.no_available_spots');
        const winner: Role = rb.status === 200 ? 'cleaner2' : 'cleaner3';
        const loser: Role = winner === 'cleaner2' ? 'cleaner3' : 'cleaner2';
        o.crew = [winner];
        taken(o, winner === 'cleaner2' ? tb : te);
        const detail = await adminDetail(o.id);
        expect(statusOf(detail)).toBe('Confirmed');
        expect(crewIds(detail)).toEqual([ids[winner]]);
        expect(list(detail, 'workContractAcceptances')).toHaveLength(1);
        expect(await inMyList(loser, ids[loser], o.id)).toBe(false);
        raced++;
      }
      const cancelled = await call(customer, 'POST', '/api/Order/Cancel', { orderId: o.id, reason: reason() });
      expect(cancelled.status, `${key}: ${JSON.stringify(cancelled.body)}`).toBe(200);
      expect(cancelled.body).toMatchObject({ feeRate: 0 });
      o.cancelled = true;
      saveRun(run);
      if (!o.taken) continue;
      // TAKE-023: the loser's take of the cancelled job says it is gone.
      const winner = (o.crew?.[0] ?? 'cleaner2') as Role;
      const loser: Role = winner === 'cleaner2' ? 'cleaner3' : 'cleaner2';
      const late = await take(loser === 'cleaner2' ? b : e, o.id, o.textId);
      expect(keys(late.body), JSON.stringify(late.body)).toContain('order.take.already_cancelled');
      // CANCEL-008: off the winner's list, Cancelled on its detail, and a row in its feed.
      expect(await inMyList(winner, ids[winner], o.id)).toBe(false);
      expect(statusOf((await call(await as(winner), 'GET', `/api/Order/GetById?OrderId=${o.id}`)).body)).toBe('Cancelled');
      const rows = await poll(() => feed(run, winner, 'partner-mobile'), (v) => v.some((n) => str(n, 'args.orderId') === o.id), 60_000);
      expect(rows.find((n) => str(n, 'args.orderId') === o.id)).toMatchObject({ eventKey: 'order.assignment_cancelled' });
    }
    test.skip(!raced, 'every race order was near the end of its oops window or already settled, so none was raced');
  });

  test('the plan\'s crews take their jobs: A the tier orders, D1, D2 and C3; E F1, J1 and J2', async () => {
    for (const key of ['tierA', 'tierB', 'tierC', 'D1', 'D2', 'C3']) if (run.orders[key] && !run.orders[key].cancelled) await takeAs('cleaner', key);
    for (const key of ['F1', 'J1', 'J2']) if (run.orders[key] && !run.orders[key].cancelled) await takeAs('cleaner3', key);
  });

  test('a cleaner holding a job cannot take another at the same time, but can at the next slot (X, Y, Z)', { tag: ['@E2E-TAKE-029'] }, async () => {
    const [x, y, z] = [order('X'), order('Y'), order('Z')];
    await takeAs('cleaner', 'X');
    const a = await as('cleaner');
    if (!y.cancelled) {
      const clash = await take(a, y.id);
      expect(keys(clash.body), JSON.stringify(clash.body)).toContain('order.time_conflict');
    }
    await takeAs('cleaner', 'Z');
    expect(statusOf(await adminDetail(z.id))).toBe('Confirmed');
    expect(statusOf(await adminDetail(x.id))).toBe('Confirmed');
    if (!y.cancelled) {
      const c = await call(await as('customer', 'customer'), 'POST', '/api/Order/Cancel', { orderId: y.id, reason: reason() });
      expect(c.status, JSON.stringify(c.body)).toBe(200);
      expect(c.body).toMatchObject({ feeRate: 0 });
      y.cancelled = true;
      saveRun(run);
    }
  });

  test('a two-cleaner job fills seat by seat; one of two drops and it stays Confirmed, short; the crew reads the customer, others do not (W)', { tag: ['@E2E-TAKE-031', '@E2E-TAKE-043', '@E2E-EXEC-020'] }, async () => {
    const w = order('W');
    const [a, bId] = [run.employeeId ?? (await employeeOf('cleaner')), run.employee2Id ?? (await employeeOf('cleaner2'))];
    if (!flag(run, 'w.seated')) {
      if (!crewIds(await adminDetail(w.id)).includes(a)) expect((await take(await as('cleaner'), w.id)).status).toBe(200);
      let d = await adminDetail(w.id);
      expect(statusOf(d)).toBe('Confirmed');
      // An invocation that stopped after B's take re-enters with both seats filled.
      if (!crewIds(d).includes(bId)) {
        expect(at(d, 'availableSpots')).toBe(1);
        expect(await onBoard('cleaner2', w.id)).toBe(true);
        const tb = await contractText(await as('cleaner2'), w.id);
        expect((await take(await as('cleaner2'), w.id, tb)).status).toBe(200);
      }
      d = await adminDetail(w.id);
      expect(crewIds(d).sort()).toEqual([a, bId].sort());
      expect(at(d, 'availableSpots')).toBe(0);
      for (const role of ['cleaner', 'cleaner2', 'cleaner3'] as Role[]) expect(await onBoard(role, w.id), role).toBe(false);
      // E is off the full job and is served no contract preview for it: a take with an empty text id is refused for the
      // tick (contract.not_accepted) before the seats are counted, so E echoes the job's text as read by the crew.
      const full = await take(await as('cleaner3'), w.id, await contractText(await as('cleaner2'), w.id));
      expect(keys(full.body), JSON.stringify(full.body)).toContain('order.no_available_spots');
      // TAKE-043: one of two drops.
      const drop = await call(await as('cleaner'), 'POST', '/api/Order/DropOrder', { orderId: w.id });
      expect(drop.status, JSON.stringify(drop.body)).toBe(200);
      d = await adminDetail(w.id);
      expect(statusOf(d)).toBe('Confirmed');
      expect(crewIds(d)).toEqual([bId]);
      expect(at(d, 'availableSpots')).toBe(1);
      expect(await onBoard('cleaner3', w.id)).toBe(true);
      const released = await poll(() => feed(run, 'customer', 'customer-mobile'), (v) => v.some((n) => str(n, 'args.orderId') === w.id && str(n, 'eventKey') === 'order.cleaner_released'), 60_000);
      expect(released.some((n) => str(n, 'args.orderId') === w.id && str(n, 'eventKey') === 'order.cleaner_released')).toBe(true);
      const admins = await poll(() => adminFeed(run, scratch() ? 'admin' : 'support'), (v) => v.some((n) => str(n, 'args.orderId') === w.id && str(n, 'eventKey').startsWith('admin.order.crew_')), 60_000);
      const events = admins.filter((n) => str(n, 'args.orderId') === w.id).map((n) => str(n, 'eventKey'));
      expect(events).toContain('admin.order.crew_short');
      expect(events).not.toContain('admin.order.crew_lost');
      // A takes the seat back, so the crew of the plan works it.
      const back = await take(await as('cleaner'), w.id);
      expect(back.status, JSON.stringify(back.body)).toBe(200);
      taken(w);
      setFlag(run, 'w.seated');
    }
    // EXEC-020: the crew reads the customer's details on the partner host; a cleaner off the crew reads them blank.
    for (const role of ['cleaner', 'cleaner2'] as Role[]) {
      const o = (await call(await as(role), 'GET', `/api/Order/GetById?OrderId=${w.id}`)).body;
      expect(str(o, 'customerName'), role).not.toBe('');
      expect(str(o, 'customerPhone'), role).not.toBe('');
      expect(str(o, 'address.street'), role).not.toBe('');
      expect(str(o, 'accessInstructions'), role).toBe('key under the mat');
    }
    // B of the case reads none of it: a job on E's board is served redacted (RedactForBrowsingCleaner); W is full again,
    // so it is off E's board and not served at all (GetOrderDetails: CanBrowseOrderAsync, order.not_found).
    const e = await call(await as('cleaner3'), 'GET', `/api/Order/GetById?OrderId=${w.id}`);
    if (e.status === 200) {
      expect([str(e.body, 'customerPhone'), str(e.body, 'address.street'), str(e.body, 'accessInstructions')], JSON.stringify(e.body).slice(0, 300)).toEqual(['', '', '']);
    } else {
      expect(e.status, JSON.stringify(e.body)).toBe(400);
      expect(keys(e.body), JSON.stringify(e.body)).toContain('order.not_found');
    }
  });

  test('the last cleaner drops: the job is back on the board at New, the customer and the admins are told, the audit has it (P)', { tag: ['@E2E-TAKE-040', '@E2E-TAKE-041', '@E2E-TAKE-042'] }, async () => {
    const p = order('P');
    test.skip(!!flag(run, 'p.dropped'), `P was dropped at ${flag(run, 'p.dropped')}`);
    await takeAs('cleaner', 'P');
    const drop = await call(await as('cleaner'), 'POST', '/api/Order/DropOrder', { orderId: p.id });
    expect(drop.status, JSON.stringify(drop.body)).toBe(200);
    expect(drop.body).toMatchObject({ crewRemaining: 0 });
    p.taken = false;
    setFlag(run, 'p.dropped');
    const d = await adminDetail(p.id);
    expect(statusOf(d)).toBe('New');
    expect(crewIds(d)).toEqual([]);
    expect(await onBoard('cleaner2', p.id)).toBe(true);
    // TAKE-041: the customer reads New, nothing cancelled or refunded, and a release that names nobody and no reason.
    const mine = (await call(await as('customer', 'customer'), 'GET', `/api/Order/GetById?OrderId=${p.id}`)).body;
    expect(statusOf(mine)).toBe('New');
    expect(named(mine, 'paymentStatus', PAYMENT_STATUS)).toBe('Paid');
    const rows = await poll(() => feed(run, 'customer', 'customer-mobile'), (v) => v.some((n) => str(n, 'args.orderId') === p.id), 60_000);
    const released = rows.find((n) => str(n, 'args.orderId') === p.id);
    expect(str(released, 'eventKey')).toBe('order.cleaner_released');
    expect(Object.keys((at(released, 'args') as object) ?? {}).sort()).toEqual(['orderId', 'orderNumber']);
    // TAKE-042: the admins' crew-lost row and the drop in the order's audit timeline.
    const admins = await poll(() => adminFeed(run, scratch() ? 'admin' : 'support'), (v) => v.some((n) => str(n, 'args.orderId') === p.id && str(n, 'eventKey') === 'admin.order.crew_lost'), 60_000);
    const lost = admins.find((n) => str(n, 'args.orderId') === p.id && str(n, 'eventKey') === 'admin.order.crew_lost');
    expect(at(lost, 'args')).toMatchObject({ cause: 'dropped', orderNumber: str(d, 'displayOrderNumber') });
    expect(Date.parse(str(lost, 'args.cleaningDateTime'))).toBe(Date.parse(p.slot));
    const timeline = list((await call(await cachedAdminAs('admin'), 'GET', `/api/CustomerAudit/timeline?ResourceType=Order&ResourceId=${p.id}&Offset=0&Limit=50`)).body, 'data');
    expect(timeline.map((t) => str(t, 'action'))).toContain('employee.order.dropped');
  });

  test('Support places a cleaner on the open job; the placed cleaner must accept the contract as a take does (P)', { tag: ['@E2E-TAKE-049', '@E2E-TAKE-050', '@E2E-EXEC-007'] }, async () => {
    const p = order('P');
    const dropped = flag(run, 'p.dropped');
    test.skip(!dropped || dropped >= loadedAt, 'P is placed on the invocation after its drop, so the customer web reads it New in between (orders/40)');
    const a = run.employeeId ?? (await employeeOf('cleaner'));
    if (!p.taken) {
      const r = await call(await support(), 'POST', '/api/AdminOrder/reassign', { orderId: p.id, fromEmployeeId: null, toEmployeeId: a });
      expect(r.status, JSON.stringify(r.body)).toBe(200);
      p.taken = true;
      p.textId = undefined;
      saveRun(run);
    }
    const d = await adminDetail(p.id);
    expect(statusOf(d)).toBe('Confirmed');
    const seat = list(d, 'assignedEmployees').find((e) => str(e, 'employeeId') === a);
    expect(seat, 'A is on the crew').toBeTruthy();
    // No acceptance for the placed seat (the dropped take's acceptance belonged to the seat that was released).
    expect(list(d, 'workContractAcceptances').filter((x) => str(x, 'orderEmployeeId') === str(seat, 'id'))).toEqual([]);
    // EXEC-007, Start half: the contract gate answers before the clock gate.
    const early = await start(await as('cleaner'), p.id);
    expect(keys(early.body), JSON.stringify(early.body)).toContain('contract.acceptance_required');
    expect(keys(early.body)).not.toContain('order.too_early_to_start');
    // TAKE-050's refusals: not on the job; another order's text; no text.
    const textId = await contractText(await as('cleaner'), p.id);
    const notOn = await call(await as('cleaner3'), 'POST', '/api/Order/AcceptWorkContract', { orderId: p.id, acceptedWorkContractTextId: textId });
    expect(keys(notOn.body), JSON.stringify(notOn.body)).toContain('order.employee_not_assigned');
    const wrong = await call(await as('cleaner'), 'POST', '/api/Order/AcceptWorkContract', { orderId: p.id, acceptedWorkContractTextId: '01AAAAAAAAAAAAAAAAAAAAAAAA' });
    expect(keys(wrong.body), JSON.stringify(wrong.body)).toContain('contract.text_mismatch');
    const blank = await call(await as('cleaner'), 'POST', '/api/Order/AcceptWorkContract', { orderId: p.id, acceptedWorkContractTextId: '' });
    expect(keys(blank.body), JSON.stringify(blank.body)).toContain('contract.not_accepted');
  });

  test('P2: Support places A, overrides it into progress; Complete and Customer did not pay need the contract; after it, card refuses cash; completed', { tag: ['@E2E-EXEC-007', '@E2E-EXEC-030', '@E2E-EXEC-006', '@E2E-TAKE-023'] }, async () => {
    const p2 = order('P2');
    const a = run.employeeId ?? (await employeeOf('cleaner'));
    const s = await as('cleaner');
    if (!p2.completed) {
      if (!p2.taken) {
        const r = await call(await support(), 'POST', '/api/AdminOrder/reassign', { orderId: p2.id, fromEmployeeId: null, toEmployeeId: a });
        expect(r.status, JSON.stringify(r.body)).toBe(200);
        taken(p2);
      }
      if (statusOf(await adminDetail(p2.id)) === 'Confirmed') {
        // OrderStatus 4 = InProgress; an override goes forward only, so P is the one that shows the Start half.
        const o = await call(await support(), 'POST', '/api/AdminOrder/override-status', { orderId: p2.id, targetStatus: 4, reason: reason() });
        expect(o.status, JSON.stringify(o.body)).toBe(200);
      }
      const done = await complete(s, p2.id);
      expect(keys(done.body), JSON.stringify(done.body)).toContain('contract.acceptance_required');
      const unpaid = await reportNotPaid(s, p2.id);
      expect(keys(unpaid.body), JSON.stringify(unpaid.body)).toContain('contract.acceptance_required');
      const textId = await contractText(s, p2.id);
      const ok = await call(s, 'POST', '/api/Order/AcceptWorkContract', { orderId: p2.id, acceptedWorkContractTextId: textId });
      expect(ok.status, JSON.stringify(ok.body)).toBe(200);
      p2.textId = textId;
      const card = await reportNotPaid(s, p2.id);
      expect(keys(card.body), JSON.stringify(card.body)).toContain('order.cash_not_allowed_on_card_order');
      expect((await afterPhoto(s, p2.id, reason())).status).toBe(200);
      const finished = await complete(s, p2.id);
      expect(finished.status, JSON.stringify(finished.body)).toBe(200);
      p2.completed = true;
      saveRun(run);
    }
    expect(statusOf(await adminDetail(p2.id))).toBe('Completed');
    // EXEC-006: a completed job cannot be started. TAKE-023: nor taken.
    const again = await start(s, p2.id);
    expect(keys(again.body), JSON.stringify(again.body)).toContain('order.not_confirmed');
    const late = await take(await as('cleaner3'), p2.id, p2.textId ?? '01AAAAAAAAAAAAAAAAAAAAAAAA');
    expect(keys(late.body), JSON.stringify(late.body)).toContain('order.take.already_completed');
  });

  test('a cleaner not on a job cannot set off or start it, and the clock gate is not what refuses (E on tierA)', { tag: ['@E2E-EXEC-005'] }, async () => {
    const tierA = order('tierA');
    test.skip(!!tierA.cancelled, 'tierA is cancelled (orders/30)');
    const e = await as('cleaner3');
    for (const step of ['NotifyOnTheWay', 'StartOrder']) {
      const r = await call(e, 'POST', `/api/Order/${step}`, { orderId: tierA.id });
      expect(keys(r.body), `${step}: ${JSON.stringify(r.body)}`).toContain('order.employee_not_assigned');
      expect(keys(r.body), step).not.toContain('order.too_early_to_start');
    }
  });

  test('a Confirmed cash job cannot be completed, paid in cash or reported unpaid; nobody off it may try; an applicant is refused at the door (D1)', { tag: ['@E2E-EXEC-006', '@E2E-EXEC-027', '@E2E-EXEC-030'] }, async () => {
    const d1 = order('D1');
    test.skip(statusOf(await adminDetail(d1.id)) !== 'Confirmed', 'D1 is past Confirmed (work/10 starts it)');
    const a = await as('cleaner');
    const done = await complete(a, d1.id);
    expect(keys(done.body), JSON.stringify(done.body)).toContain('order.not_in_progress');
    const cash = await markCash(a, d1.id);
    expect(keys(cash.body), JSON.stringify(cash.body)).toContain('order.not_in_progress');
    const unpaid = await reportNotPaid(a, d1.id);
    expect(keys(unpaid.body), JSON.stringify(unpaid.body)).toContain('order.not_in_progress');
    const e = await as('cleaner3');
    expect(keys((await reportNotPaid(e, d1.id)).body)).toContain('order.not_found');
    // The applicant (never approved) meets the partner hosts' registration gate before any order rule (F-073).
    const applicant = await as('applicant');
    for (const r of [await markCash(applicant, d1.id), await reportNotPaid(applicant, d1.id)]) {
      expect(r.status, JSON.stringify(r.body)).toBe(403);
      expect(str(r.body, 'title')).toBe('Registration incomplete');
    }
  });
});
