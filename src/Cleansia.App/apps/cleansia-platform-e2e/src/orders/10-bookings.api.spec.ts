import { expect, Page, test } from '@playwright/test';
import { anonymous, at, call, errorKeys, HOSTS, list, named, num, PAYMENT_STATUS, poll, Session, signIn, str } from '../support/dev';
import { adminFeed, book, Booking, payCard, quote, Selection } from '../support/flows';
import { PlanDay, planned, RULED_DAYS, slotOf, THROWAWAY } from '../support/plan';
import { emailOf, flag, loadRun, Role, RunState, saveRun, scratch, setFlag } from '../support/run';
import { dayAfter, nextSlots, pragueSlot } from '../support/slots';
import { cachedAdminAs } from '../support/web';

// Stage 'orders', 10: every order of the plan (src/support/plan.ts) booked and each card order paid in
// Stripe test Checkout, with the booking refusals around them: the dirtiness level, the lead time, the grid, the
// window, the horizon, the cash rules, the terms tick and the webhook signature. The tier orders, F1 and D2 go first:
// orders/30 cancels them 15 minutes after their booking (their oops window). A booked order is never booked again,
// so the stage can be re-run.
// Not serial: each test skips on the state it needs, so one failure does not stop the jobs after it.

let run: RunState;
let f1Checkout = '';
const BATHROOM = 'Bathroom Cleaning';
const DIRTINESS = ['Normal', 'Increased', 'Heavy'];
const sessions: Record<string, { s: Session; at: number }> = {};

/** A run account's customer-web session, one per 8 minutes (sign-ins are paced); skips the test when it cannot sign in. */
async function as(role: Role): Promise<Session> {
  const c = sessions[role];
  if (c && Date.now() - c.at < 8 * 60_000) return c.s;
  const s = await signIn('customer', emailOf(run, role), run.password).catch((e: Error) => {
    test.skip(true, `${emailOf(run, role)} cannot sign in (${e.message.slice(0, 80)}): the people stage registers and confirms it`);
    throw e;
  });
  sessions[role] = { s, at: Date.now() };
  return s;
}

/** The account's saved address, added once (a signed-in booking names one). */
async function addressOf(s: Session): Promise<string> {
  const mine = list((await call(s, 'GET', '/api/SavedAddress/GetMine')).body, '');
  if (mine.length) return str(mine[0], 'id');
  const r = await call(s, 'POST', '/api/SavedAddress/Add', {
    label: 'Home', street: 'Václavské náměstí 1', city: 'Praha', zipCode: '11000', countryId: run.countryId, setAsDefault: true, latitude: 50.0811, longitude: 14.428,
  });
  expect(r.status, JSON.stringify(r.body)).toBe(200);
  return str(r.body, 'id');
}

const pragueToday = () => new Date().toLocaleDateString('en-CA', { timeZone: 'Europe/Prague' });

/**
 * The plan's days: the ruled ones (Day 2 follows the period people/10 read, run.day2). A scratch run books the plan on
 * E2E_DAY1 (default: tomorrow in Prague), the next day as Day 1 + 1 and Day 2.
 */
function days(): Record<PlanDay, string> {
  if (!scratch()) return { ...RULED_DAYS, D2: run.day2 ?? RULED_DAYS.D2 };
  const d1 = process.env['E2E_DAY1'] ?? dayAfter(pragueToday());
  return { D1: d1, 'D1+1': dayAfter(d1), D2: dayAfter(d1) };
}

/** A planned order's selection; a scratch run without the throwaway service books Bathroom Cleaning in its place, and its tierC 2 h 45 min ahead (the 50 % tier). */
function selectionOf(key: string): Booking {
  const p = planned(key);
  const services = p.services.map((x) => (x === THROWAWAY && !run.serviceId && scratch() ? BATHROOM : x));
  const slot = scratch() && key === 'tierC' ? nextSlots(new Date(), 1, 165)[0] : slotOf(p, new Date(), days());
  return {
    services, extras: p.extras, rooms: p.rooms, bathrooms: p.bathrooms, level: p.level, slot, payment: p.payment, customer: p.customer,
    guest: p.guest, saveCard: p.saveCard, accessInstructions: p.accessInstructions, crew: p.crew,
  };
}

/** Books one planned order (skipped when the run already holds it) and pays it when the plan pays it; returns the create reply. */
async function bookPlanned(page: Page, key: string, change: Partial<Booking> = {}) {
  if (run.orders[key]) return undefined;
  const p = planned(key);
  const b = { ...selectionOf(key), ...change };
  const s = b.guest ? await anonymous('customer') : await as(b.customer);
  if (!b.guest) b.addressId = await addressOf(s);
  const r = await book(s, run, key, b);
  expect(r.status, `${key}: ${JSON.stringify(r.body)}`).toBe(200);
  if (b.payment === 'card' && p.pay) {
    const paid = await payCard(page, b.guest ? s : await as(b.customer), run, key, str(r.body, 'stripeSessionId'));
    expect(named(paid, 'paymentStatus', PAYMENT_STATUS), key).toBe('Paid');
  }
  return r;
}

const orderOf = async (s: Session, key: string) => (await call(s, 'GET', `/api/Order/GetById?OrderId=${run.orders[key].id}`)).body;
/** A day-ahead selection of the run's customer at a Prague time on Day 1 + 1. */
const dayAhead = (prague: string, sel: Partial<Selection> = {}): Selection => ({
  services: [BATHROOM], rooms: 1, bathrooms: 1, level: 0, slot: pragueSlot(days()['D1+1'], prague), ...sel,
});

test.describe('orders: bookings', () => {
  test.beforeAll(() => {
    run = loadRun();
  });
  test.beforeEach(() => {
    test.skip(!scratch() && new Date().toISOString().slice(0, 10) !== RULED_DAYS.D1, `the plan books on Day 1 (${RULED_DAYS.D1}); E2E_SCRATCH=1 books it on another day`);
  });

  test('a cash booking (K1, cashcap, the two-cleaner service at 1 room 0 baths) tells Support as Info, not the Accountant', { tag: ['@E2E-XCUT-007'] }, async ({ page }) => {
    test.skip(!run.serviceId && !scratch(), 'the throwaway two-cleaner service was not created (people/50)');
    test.skip(!!flag(run, 'k1.told'), `checked at ${flag(run, 'k1.told')}: the shared feed has moved on since`);
    // A scratch run has no Support or Accountant (people/10 skips role accounts): the DEV administrator reads the Support side.
    const support = scratch() ? 'admin' : 'support';
    const unread = async () => num((await call(await cachedAdminAs(support, run), 'GET', '/api/AdminNotification/unread-count')).body, 'count');
    const before = await unread();
    await bookPlanned(page, 'K1');
    const k1 = run.orders['K1'].id;
    const row = await poll(async () => (await adminFeed(run, support)).find((n) => str(n, 'args.orderId') === k1), Boolean, 60_000);
    expect(str(row, 'eventKey')).toBe('admin.order.new');
    expect(at(row, 'args')).toMatchObject({ orderNumber: expect.any(String), paymentType: 'Cash', countryId: run.countryId });
    expect(str(row, 'args.amount')).toMatch(new RegExp(`^${run.orders['K1'].total} (Kč|CZK)$`));
    // Info, not Critical: the critical-only feed does not hold it.
    const critical = list((await call(await cachedAdminAs(support, run), 'GET', '/api/AdminNotification/get-paged?Offset=0&Limit=100&critical=true')).body, 'data');
    expect(critical.map((n) => str(n, 'args.orderId'))).not.toContain(k1);
    // The feed is shared DEV-wide: the unread delta is recorded, not asserted.
    test.info().annotations.push({ type: 'note', description: `${support} unread ${before} -> ${await unread()}` });
    if (scratch()) test.info().annotations.push({ type: 'note', description: 'scratch: no Accountant account, its half not checked' });
    else expect((await adminFeed(run, 'accountant')).map((n) => str(n, 'args.orderId'))).not.toContain(k1);
    setFlag(run, 'k1.told');
  });

  test('an Increased booking must carry the Increased total, and keeps the level (tierB)', { tag: ['@E2E-BOOK-029'] }, async ({ page }) => {
    test.skip(!!run.orders['tierB'], 'tierB was booked by an earlier invocation');
    const b = selectionOf('tierB');
    const s = await as('customer');
    const normal = await quote(s, run, { ...b, level: 0 });
    const increased = await quote(s, run, b);
    expect(increased.total).toBeGreaterThan(normal.total);
    const wrong = await book(s, run, 'tierB', { ...b, addressId: await addressOf(s) }, normal.total);
    expect(errorKeys(wrong.body), JSON.stringify(wrong.body)).toContain('order.total_price.not_match');
    await bookPlanned(page, 'tierB');
    const o = await orderOf(s, 'tierB');
    expect(num(o, 'totalPrice')).toBe(increased.total);
    expect(named(o, 'dirtinessLevel', DIRTINESS)).toBe('Increased');
  });

  test('the other tier orders, D1 and D2 are booked first, so their oops windows end before orders/30 cancels them', async ({ page }) => {
    // D1 before D2: D2 must be the debtor's second booking, whose oops window is 15 minutes, not a first booking's 60.
    for (const key of ['tierA', 'tierC', 'D1', 'D2']) await bookPlanned(page, key);
  });

  test('a start 2 to 4 hours away carries the 20 % express surcharge (F1, first, card)', { tag: ['@E2E-BOOK-015'] }, async () => {
    test.skip(!!run.orders['F1'], 'F1 was booked by an earlier invocation');
    const s = await as('first');
    const b = selectionOf('F1');
    const express = await quote(s, run, b);
    const ahead = await quote(s, run, { ...b, slot: pragueSlot(days()['D1+1'], '10:00') });
    // F1 is booked either way: PAY-022 and the CANCEL-002 chain need it.
    const r = await book(s, run, 'F1', { ...b, addressId: await addressOf(s) });
    expect(r.status, JSON.stringify(r.body)).toBe(200);
    f1Checkout = str(r.body, 'stripeSessionId');
    const hours = (b.slot.getTime() - Date.now()) / 3_600_000;
    test.skip(hours >= 4, `the first slot 3 h ahead is ${b.slot.toISOString()}, over 4 h away: express runs only 04:00-15:45 UTC`);
    expect(express.reply.body).toMatchObject({ expressSurchargeApplied: true });
    expect(ahead.reply.body).toMatchObject({ expressSurchargeApplied: false, expressSurchargeAmount: 0 });
    // BR #booking-window: +20 % on the lines and the dirtiness surcharge, so total = 1.2 x the day-ahead total.
    expect(num(express.reply.body, 'expressSurchargeAmount')).toBeCloseTo(0.2 * ahead.total, 2);
    expect(express.total).toBeCloseTo(ahead.total * 1.2, 2);
    // The express total is what the order stores.
    expect(num(await orderOf(s, 'F1'), 'totalPrice')).toBe(express.total);
  });

  test('a ticked card payment saves the card, and the same customer may then book cash (F1, F2)', { tag: ['@E2E-PAY-022'] }, async ({ page }) => {
    const f1 = run.orders['F1'];
    test.skip(!f1, 'F1 was not booked');
    const s = await as('first');
    if (named(await orderOf(s, 'F1'), 'paymentStatus', PAYMENT_STATUS) !== 'Paid') {
      test.skip(!f1Checkout, 'F1 is unpaid and its Checkout link was not kept by this invocation');
      await payCard(page, s, run, 'F1', f1Checkout);
    }
    const cards = await poll(async () => list((await call(s, 'GET', '/api/SavedCard/GetMine')).body, ''), (v) => v.length > 0, 90_000);
    expect(cards).toHaveLength(1);
    expect(cards[0]).toMatchObject({ last4: '4242', currencyCode: 'CZK' });
    expect(str(cards[0], 'brand').toLowerCase()).toBe('visa');
    expect(num(cards[0], 'expMonth')).toBeGreaterThan(0);
    expect(num(cards[0], 'expYear')).toBeGreaterThanOrEqual(new Date().getUTCFullYear());
    const f2 = await bookPlanned(page, 'F2');
    if (f2) expect(errorKeys(f2.body)).not.toContain('order.cash_requires_saved_card');
  });

  test('an unticked card payment keeps no card (R1, nocard)', { tag: ['@E2E-PAY-023'] }, async ({ page }) => {
    const booked = await bookPlanned(page, 'R1');
    test.skip(!booked, 'R1 was booked and paid by an earlier invocation');
    const s = await as('nocard');
    // The webhook that marked it Paid is the one that would have saved a card: none after it.
    await new Promise((r) => setTimeout(r, 10_000));
    expect(list((await call(s, 'GET', '/api/SavedCard/GetMine')).body, '')).toEqual([]);
  });

  test('every other order of the plan for Day 1 and Day 2 is booked, each card order paid', async ({ page }) => {
    test.setTimeout(30 * 60_000);
    for (const key of ['X', 'Y', 'Z', 'P', 'P2', 'R2', 'R3', 'J1', 'J2']) await bookPlanned(page, key);
  });

  test('a quote prices the dirtiness level: Normal adds nothing, Increased 15 %, Heavy 30 %, and lengthens the job', { tag: ['@E2E-BOOK-011'] }, async () => {
    const s = await as('customer');
    const sel = dayAhead('10:00', { services: ['General Cleaning'], rooms: 2, bathrooms: 1 });
    const [n, i, h] = await Promise.all([0, 1, 2].map((level) => quote(s, run, { ...sel, level })));
    for (const q of [n, i, h]) expect(q.reply.status, JSON.stringify(q.reply.body)).toBe(200);
    // S = the lines (services + packages + extras); BR #dirtiness: surcharge = round(S x rate, 2), total = S + surcharge.
    const lines = (q: typeof n) => ['servicesSubtotal', 'packagesSubtotal', 'extrasSubtotal'].reduce((sum, f) => sum + num(q.reply.body, f), 0);
    const S = lines(n);
    const round2 = (x: number) => Math.round(x * 100) / 100;
    expect(num(n.reply.body, 'dirtinessSurchargeAmount')).toBe(0);
    expect(n.total).toBeCloseTo(S, 2);
    expect(num(i.reply.body, 'dirtinessSurchargeAmount')).toBeCloseTo(round2(S * 0.15), 2);
    expect(i.total).toBeCloseTo(S + round2(S * 0.15), 2);
    expect(num(h.reply.body, 'dirtinessSurchargeAmount')).toBeCloseTo(round2(S * 0.3), 2);
    expect(h.total).toBeCloseTo(S + round2(S * 0.3), 2);
    // OrderDuration.ScaleForDirtiness: minutes x (1 + rate), up to the next whole minute.
    const N = num(n.reply.body, 'estimatedDurationMinutes');
    expect(num(i.reply.body, 'estimatedDurationMinutes')).toBe(Math.ceil(N * 1.15));
    expect(num(h.reply.body, 'estimatedDurationMinutes')).toBe(Math.ceil(N * 1.3));
  });

  test('a start under 2 hours away is refused on the quote and the create, and books nothing', { tag: ['@E2E-BOOK-014'] }, async () => {
    const s = await as('customer');
    const slot = nextSlots(new Date(), 1, 90)[0];
    test.skip(slot.getTime() - Date.now() >= 2 * 3_600_000, `the first slot 90 minutes ahead is ${slot.toISOString()} (outside today's window): run before 17:15 UTC`);
    const sel = dayAhead('10:00', { slot });
    const count = async () => num((await call(s, 'GET', '/api/Order/GetPaged?Offset=0&Limit=1')).body, 'total');
    const before = await count();
    const r = await book(s, run, 'probe-lead', { ...sel, payment: 'card', customer: 'customer', addressId: await addressOf(s) });
    expect(errorKeys(r.body), JSON.stringify(r.body)).toContain('order.cleaning_date.below_lead_time');
    // The case: refused, not priced. The quote applies the create's lead-time rule with its key.
    const q = await quote(s, run, sel);
    expect(errorKeys(q.reply.body), `the quote under 2 h: HTTP ${q.reply.status}, total ${q.total}`).toContain('order.cleaning_date.below_lead_time');
    expect(await count(), 'no order was created').toBe(before);
  });

  test('an off-grid start is refused on the quote and the create; the grid slot books (B18, cancelled at once)', { tag: ['@E2E-BOOK-018'] }, async () => {
    const s = await as('customer');
    const off = dayAhead('10:07');
    expect(errorKeys((await quote(s, run, off)).reply.body)).toContain('order.cleaning_date.outside_booking_window');
    const r = await book(s, run, 'probe-offgrid', { ...off, payment: 'card', customer: 'customer', addressId: await addressOf(s) });
    expect(errorKeys(r.body), JSON.stringify(r.body)).toContain('order.cleaning_date.outside_booking_window');
    if (!run.orders['B18']) {
      const ok = await book(s, run, 'B18', { ...selectionOf('B18'), addressId: await addressOf(s) });
      expect(ok.status, JSON.stringify(ok.body)).toBe(200);
    }
    const o = run.orders['B18'];
    if (!o.cancelled) {
      const c = await call(s, 'POST', '/api/Order/Cancel', { orderId: o.id, reason: `E2E run ${run.runId} B18` });
      expect(c.status, JSON.stringify(c.body)).toBe(200);
      expect(num(c.body, 'feeRate')).toBe(0);
      o.cancelled = true;
      saveRun(run);
    }
  });

  test('the booking window is 08:00 to 19:45 Prague', { tag: ['@E2E-BOOK-019'] }, async () => {
    const s = await as('customer');
    for (const [prague, ok] of [['07:45', false], ['08:00', true], ['19:45', true], ['20:00', false]] as const) {
      const q = await quote(s, run, dayAhead(prague));
      if (ok) expect(q.reply.status, `${prague}: ${JSON.stringify(q.reply.body)}`).toBe(200);
      else expect(errorKeys(q.reply.body), prague).toContain('order.cleaning_date.outside_booking_window');
    }
  });

  test('the horizon is 60 days: day 59 is priced, day 61 refused', { tag: ['@E2E-BOOK-021'] }, async () => {
    const s = await as('customer');
    const plus = (n: number) => new Date(Date.now() + n * 86_400_000).toLocaleDateString('en-CA', { timeZone: 'Europe/Prague' });
    const ok = await quote(s, run, dayAhead('10:00', { slot: pragueSlot(plus(59), '10:00') }));
    expect(ok.reply.status, JSON.stringify(ok.reply.body)).toBe(200);
    const far = await quote(s, run, dayAhead('10:00', { slot: pragueSlot(plus(61), '10:00') }));
    expect(errorKeys(far.reply.body)).toContain('order.cleaning_date.outside_booking_window');
  });

  test('a two-cleaner booking cannot be cash; by card it books two seats (W); at 120 minutes it is one cleaner and cash (K1)', { tag: ['@E2E-BOOK-035'] }, async ({ page }) => {
    test.skip(!run.serviceId, 'the throwaway two-cleaner service exists only on the real run (people/50)');
    test.skip(!!flag(run, 'service.off'), `the two-cleaner service was switched off at ${flag(run, 'service.off')} (orders/50): it is quoted no more`);
    const s = await as('customer');
    const w = selectionOf('W');
    const two = await quote(s, run, w);
    expect(num(two.reply.body, 'requiredEmployees')).toBe(2);
    const cash = await book(s, run, 'probe-cash2', { ...w, payment: 'cash', addressId: await addressOf(s) });
    expect(errorKeys(cash.body), JSON.stringify(cash.body)).toContain('order.cash_not_available');
    await bookPlanned(page, 'W');
    expect(num((await call(await cachedAdminAs('admin'), 'GET', `/api/AdminOrder/details/${run.orders['W'].id}`)).body, 'requiredEmployees')).toBe(2);
    const one = await quote(s, run, { ...w, bathrooms: 0 });
    expect(num(one.reply.body, 'requiredEmployees')).toBe(1);
    expect(run.orders['K1'], 'K1 (cash, 1 room 0 baths) was accepted by the first test').toBeTruthy();
  });

  test('a customer holds at most two unpaid cash bookings; card is still open, and a cancel frees a place (cashcap)', { tag: ['@E2E-BOOK-037'] }, async ({ page }) => {
    test.skip(!run.orders['K1'], 'K1 was not booked');
    const s = await as('cashcap');
    await bookPlanned(page, 'K2');
    if (!run.orders['C3']) {
      const third = await book(s, run, 'probe-cash3', { ...dayAhead('12:00'), payment: 'cash', customer: 'cashcap', addressId: await addressOf(s) });
      expect(errorKeys(third.body), JSON.stringify(third.body)).toContain('order.cash_open_bookings_limit_reached');
    }
    await bookPlanned(page, 'K3');
    const k1 = run.orders['K1'];
    if (!k1.cancelled) {
      const c = await call(s, 'POST', '/api/Order/Cancel', { orderId: k1.id, reason: `E2E run ${run.runId} K1` });
      expect(c.status, JSON.stringify(c.body)).toBe(200);
      k1.cancelled = true;
      saveRun(run);
    }
    await bookPlanned(page, 'C3');
    expect(run.orders['C3']).toBeTruthy();
  });

  test('a guest booking without the terms tick is refused, with a wrong total too, and audited', { tag: ['@E2E-BOOK-046'] }, async () => {
    const guest = await anonymous('customer');
    const from = new Date(Date.now() - 5_000).toISOString();
    const sel = dayAhead('12:15');
    const q = await quote(guest, run, sel);
    const body = (total: number) => ({
      customerName: 'E2E Guest', customerEmail: emailOf(run, 'guest'), customerPhone: '+420777000046',
      customerAddress: { street: 'Václavské náměstí 1', city: 'Praha', zipCode: '11000', countryId: run.countryId, state: null }, savedAddressId: null,
      selectedPackageIds: [], selectedServiceIds: q.serviceIds, rooms: 1, bathrooms: 1, extras: {}, cleaningDate: sel.slot.toISOString(), paymentType: 2,
      currencyId: null, totalPrice: total, language: 'en', dirtinessLevel: 0, earlyPerformanceRequested: true, saveCard: false,
      specialInstructions: `E2E run ${run.runId} BOOK-046`,
    });
    for (const total of [q.total, q.total + 1]) {
      const r = await call(guest, 'POST', '/api/Order/CreateOrder', body(total));
      expect(r.status).toBe(400);
      expect(errorKeys(r.body), JSON.stringify(r.body)).toContain('consent.terms_not_accepted');
    }
    const a = await cachedAdminAs('admin');
    const rows = await poll(
      async () => list((await call(a, 'GET', `/api/CustomerAudit/get-paged?Offset=0&Limit=50&Filter.Action=customer.order.create&Filter.Success=false&Filter.OccurredFrom=${encodeURIComponent(from)}`)).body, 'data')
        .filter((x) => str(x, 'errorCode').includes('consent.terms_not_accepted')),
      (v) => v.length > 0,
      60_000,
    );
    expect(rows.length).toBeGreaterThan(0);
    const detail = (await call(a, 'GET', `/api/CustomerAudit/get-by-id/${str(rows[0], 'id')}`)).body;
    expect(detail).toMatchObject({ success: false, action: 'customer.order.create' });
    expect(str(detail, 'ipAddress')).not.toBe('');
  });

  test('a webhook without a Stripe signature is refused on the customer, partner and customer mobile hosts', { tag: ['@E2E-PAY-017'] }, async () => {
    for (const host of ['customer', 'partner', 'customer-mobile'] as const) {
      const r = await call(await anonymous(host), 'POST', '/api/Payment/webhook', { id: 'evt_e2e', type: 'checkout.session.completed', data: { object: {} } });
      expect(r.status, `${host}: ${JSON.stringify(r.body)}`).toBe(400);
      expect(errorKeys(r.body), host).toContain('payment.stripe_signature_required');
    }
  });

  test('a forged webhook signature is refused on all three hosts and the order stays unpaid (K3)', { tag: ['@E2E-PAY-018'] }, async () => {
    const k3 = run.orders['K3'];
    test.skip(!k3, 'K3 was not booked');
    const event = {
      id: `evt_e2e_${run.runId}`, object: 'event', type: 'checkout.session.completed',
      data: { object: { id: 'cs_test_e2e_forged', object: 'checkout.session', payment_status: 'paid', metadata: { orderId: k3.id }, client_reference_id: k3.id } },
    };
    const signature = `t=${Math.floor(Date.now() / 1000)},v1=${'ab'.repeat(32)}`;
    for (const host of ['customer', 'partner', 'customer-mobile'] as const) {
      const s = await anonymous(host);
      const res = await s.ctx.fetch(`${HOSTS[host]}/api/Payment/webhook`, { method: 'POST', headers: { 'Stripe-Signature': signature, 'Content-Type': 'application/json' }, data: event, failOnStatusCode: false });
      const body = await res.json().catch(() => null);
      expect(res.status(), `${host}: ${JSON.stringify(body)}`).toBe(400);
      expect(errorKeys(body), host).toEqual(['Invalid webhook signature']);
    }
    expect(named(await orderOf(await as('cashcap'), 'K3'), 'paymentStatus', PAYMENT_STATUS)).toBe('Pending');
  });
});
