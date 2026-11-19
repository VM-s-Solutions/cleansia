import { expect, test } from '@playwright/test';
import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { call, errorKeys, list, named, ORDER_STATUS, poll, Session, signIn, str } from '../support/dev';
import { afterPhoto, book, cancel, complete, openPeriod, periodPays, start, take } from '../support/flows';
import { emailOf, flag, loadRun, runDir, RunOrder, RunState, saveRun, scratch, setFlag } from '../support/run';
import { dayAfter, nextSlots, startOpensAt } from '../support/slots';
import { cachedAdmin, cachedAdminAs } from '../support/web';

// Stage 'payroll', file 50 (Day 2, after payroll/40 marked E's invoice and the closed period paid in the SPA): J2 after
// the close lands in the next period, the closed period reads Paid, the clean-up (every e2e invoice of the night close
// settled, that period marked paid only if all its invoices are e2e ones, the Android run's debt written off, the three
// role accounts deactivated), and last of the whole run TAKE-025 (owner ruling 3, 2026-11-15): the DEV-only SQL
// publishes new cleaner documents, a run cleaner's take is refused until they accept them. TAKE-025 runs only in an
// invocation with E2E_TAKE025=1, on a phase-2 run after its Day 2 clean-up and J2, on a one-day run once every take of
// the run and of the mobile lanes is done (e2e/README.md, "The fix-proof run"). A scratch run proves the skips.
test.describe.configure({ mode: 'serial' });

const run = loadRun();
const INVOICE = ['', 'Pending', 'Approved', 'Paid', 'Disputed', 'Rejected', 'Cancelled'];
const E2E_ACCOUNT = /^e2e\.\d{12}\.[a-z0-9]+@example\.com$/;
const ROLES = ['manager', 'support', 'accountant'] as const;
const rows = (body: unknown) => list(body, 'data');
const accountant = () => cachedAdminAs('accountant', run);
const status = (v: unknown) => named(v, 'status', INVOICE);

function dayTwo(what: string) {
  test.skip(!run.day2 || !run.periodAtStart, 'people/10 records the period open on Day 1 and its Day 2 (run.day2)');
  const when = new Date(`${run.day2}T02:30:00Z`);
  test.skip(Date.now() < when.getTime(), `${what}: from ${when.toISOString()} (Day 2); run payroll again then`);
  test.skip(scratch(), 'E2E_SCRATCH=1: a scratch run never reaches Day 2\'s payroll');
}

/** The period Close now ended on Day 2 (Day 2 to Day 2). */
async function closedOnDayTwo() {
  const periods = rows((await call(await accountant(), 'GET', '/api/AdminPayPeriod/get-paged?Offset=0&Limit=20')).body);
  return periods.find((p) => str(p, 'startDate') === run.day2);
}

test.describe('payroll: after the period is paid, the clean-up, and TAKE-025', () => {
  test('E works J2 after Close now, and its pay row lands in the next period', { tag: ['@E2E-PAYOUT-030'] }, async () => {
    dayTwo('J2 is Day 2\'s job');
    test.skip(!flag(run, 'period19.closedAt'), 'J2 is completed after payroll/20 presses Close now in the SPA');
    const j2 = run.orders['J2'] as RunOrder | undefined;
    test.skip(!j2?.taken, 'orders/20 has E take J2 on Day 1');
    if (!j2) return;
    const opens = startOpensAt(new Date(j2.slot));
    test.skip(Date.now() < opens.getTime(), `J2 (slot ${j2.slot}) can be started from ${opens.toISOString()}; run payroll again then`);
    const e: Session = await signIn('partner-mobile', emailOf(run, 'cleaner3'), run.password);
    if (!j2.completed) {
      for (const [step, r] of [['start', () => start(e, j2.id)], ['after photo', () => afterPhoto(e, j2.id, `E2E run ${run.runId} J2`)], ['complete', () => complete(e, j2.id, `E2E run ${run.runId} J2`)]] as const) {
        const reply = await r();
        expect(reply.status, `${step}: ${JSON.stringify(reply.body)}`).toBe(200);
      }
      j2.completed = true;
      saveRun(run);
    }
    const open = await openPeriod();
    expect(open?.startDate, 'the period after the one Close now ended').toBe(dayAfter(run.day2 as string));
    const ids = await poll(async () => (await periodPays(e, run.ids?.['cleaner3Employee'] ?? '', open?.id ?? '')).map((r) => str(r, 'orderId')), (x) => x.includes(j2.id));
    expect(ids).toContain(j2.id);
  });

  test('the period Close now ended reads Paid with the time it was paid', { tag: ['@E2E-PAYOUT-030'] }, async () => {
    dayTwo('the closed period is paid on Day 2');
    test.skip(!flag(run, 'period19.paid'), 'payroll/40 marks E\'s invoice and then the period paid in the SPA');
    const closed = await closedOnDayTwo();
    expect(closed).toMatchObject({ status: 'Paid', endDate: run.day2 });
    expect(str(closed, 'paidAt')).not.toBe('');
  });

  test('clean-up: every open e2e invoice of the night close is settled; that period is marked paid only if all its invoices are e2e', async () => {
    dayTwo('the night close invoices Day 1\'s period');
    test.skip(!run.invoices?.['Bfix'], 'payroll/10 finishes the invoice lifecycle (B\'s corrected invoice) first');
    test.skip(!!flag(run, 'cleanup.settled'), `settled at ${flag(run, 'cleanup.settled')}`);
    const p = run.periodAtStart as NonNullable<RunState['periodAtStart']>;
    const admin = await cachedAdmin();
    const invoices = rows((await call(await accountant(), 'GET', `/api/AdminInvoice/get-paged?Offset=0&Limit=100&Filter.PayPeriodId=${p.id}`)).body);
    const foreign: string[] = [];
    for (const inv of invoices.filter((x) => status(x) !== 'Cancelled')) {
      const email = str((await call(admin, 'GET', `/api/AdminEmployee/details/${str(inv, 'employeeId')}`)).body, 'email');
      // Never settle a non-e2e invoice (owner default).
      if (!E2E_ACCOUNT.test(email)) {
        foreign.push(`${str(inv, 'invoiceNumber')} (${str(inv, 'employeeName')})`);
        continue;
      }
      const id = str(inv, 'id');
      if (status(inv) === 'Pending') {
        const r = await call(await accountant(), 'PUT', '/api/AdminInvoice/approve', { invoiceId: id, adminNotes: `E2E run ${run.runId} clean-up` });
        expect(r.status, `approve ${email}: ${JSON.stringify(r.body)}`).toBe(200);
      }
      if (['Pending', 'Approved'].includes(status(inv))) {
        const r = await call(await accountant(), 'PUT', '/api/AdminInvoice/mark-paid', { invoiceId: id, bankTransferNote: `E2E run ${run.runId} clean-up`, adminNotes: null });
        expect(r.status, `mark paid ${email}: ${JSON.stringify(r.body)}`).toBe(200);
      }
    }
    const period = (await call(await accountant(), 'GET', `/api/AdminPayPeriod/details/${p.id}`)).body;
    if (foreign.length) {
      console.log(`the period ${p.startDate}..${p.endDate} stays Closed: invoices of non-e2e cleaners ${foreign.join(', ')}`);
    } else if (str(period, 'status') !== 'Paid') {
      const paid = await call(await accountant(), 'POST', '/api/AdminPayPeriod/mark-paid', { payPeriodId: p.id });
      expect(paid.status, JSON.stringify(paid.body)).toBe(200);
      expect(str((await call(await accountant(), 'GET', `/api/AdminPayPeriod/details/${p.id}`)).body, 'status')).toBe('Paid');
    }
    setFlag(run, 'cleanup.settled');
  });

  test('clean-up: the Android run\'s unpaid cash job is written off', async () => {
    dayTwo('the clean-up is Day 2\'s');
    const androidRun = process.env['E2E_ANDROID_RUN_ID'] ?? '';
    test.skip(!androidRun, 'E2E_ANDROID_RUN_ID names the Android lane\'s run, whose customer owes for cashToCancel (EXEC-029 on Android)');
    const file = join(runDir(androidRun), 'state.json');
    test.skip(!existsSync(file), `no run folder for ${androidRun} on this machine`);
    const orderId = (JSON.parse(readFileSync(file, 'utf8')) as RunState).orders['cashToCancel']?.id ?? '';
    test.skip(!orderId, `run ${androidRun} has no cashToCancel order`);
    const admin = await cachedAdmin();
    const open = rows((await call(admin, 'GET', `/api/AdminReceivable/get-paged?Offset=0&Limit=10&Filter.OrderId=${orderId}&Filter.Status=1`)).body);
    for (const r of open) {
      const w = await call(admin, 'POST', '/api/AdminReceivable/write-off', { receivableId: str(r, 'id'), note: `E2E run ${run.runId}: clean-up of the Android run ${androidRun}` });
      expect(w.status, JSON.stringify(w.body)).toBe(200);
    }
    expect(rows((await call(admin, 'GET', `/api/AdminReceivable/get-paged?Offset=0&Limit=10&Filter.OrderId=${orderId}&Filter.Status=1`)).body)).toHaveLength(0);
  });

  test('clean-up: the Administrator deactivates the run\'s Manager, Support and Accountant', async () => {
    dayTwo('the role accounts serve until the end of Day 2');
    test.skip(!flag(run, 'period19.paid') || !flag(run, 'cleanup.settled'), 'the Accountant marks the periods paid first (payroll/40 and the clean-up above)');
    test.skip(!!flag(run, 'admins.deactivated'), `deactivated at ${flag(run, 'admins.deactivated')}`);
    const admin = await cachedAdmin();
    for (const role of ROLES) {
      const id = run.ids?.[role];
      expect(id, `${role}'s admin user id (people/10)`).toBeTruthy();
      const r = await call(admin, 'POST', `/api/AdminUser/${id}/deactivate`);
      expect(r.status, `${role}: ${JSON.stringify(r.body)}`).toBe(200);
    }
    const listed = rows((await call(admin, 'GET', `/api/AdminUser/get-paged?Offset=0&Limit=20&Filter.SearchTerm=e2e.${run.runId}`)).body);
    for (const role of ROLES) expect(listed.find((u) => str(u, 'email') === emailOf(run, role)), role).toMatchObject({ isActive: false });
    setFlag(run, 'admins.deactivated');
  });

  test('last of the run: new cleaner documents are published; an approved cleaner\'s take is refused until they accept them, then succeeds, and the order is cancelled', { tag: ['@E2E-TAKE-025'] }, async () => {
    test.skip(scratch(), 'E2E_SCRATCH=1: the cleaner documents are never published on a scratch run');
    // The publish gates every DEV cleaner until they accept again, so no gate of the run's own state may start it alone.
    test.skip(process.env['E2E_TAKE025'] !== '1', 'E2E_TAKE025=1 runs it, in the run\'s last invocation, once every take of the run and of the mobile lanes is done');
    if (run.day2) {
      // A phase-2 run: after its Day 2 clean-up, and after J2, its last take.
      dayTwo('TAKE-025 is the last step of Day 2');
      test.skip(!flag(run, 'admins.deactivated'), 'every other payroll step runs first (the clean-up ends by deactivating the role accounts)');
      test.skip(!run.orders['J2']?.completed, 'the run\'s last take is J2\'s, worked on Day 2 above; the mobile lanes\' takes are all on Day 1');
    }
    test.skip(!!flag(run, 'take025.done'), `shown at ${flag(run, 'take025.done')}`);
    test.setTimeout(25 * 60_000);
    if (!flag(run, 'take025.published')) {
      // The wrapper passes the dispatch's confirmation, which the script requires (T-0937); E2E_TAKE025=1 is the operator's.
      const out = execFileSync('bash', [join(__dirname, '..', '..', 'scripts', 'publish-cleaner-document.sh')], { encoding: 'utf8', timeout: 18 * 60_000 });
      console.log(out);
      setFlag(run, 'take025.published');
    }
    const customer = await signIn('customer', emailOf(run, 'customer'), run.password);
    if (!run.orders['T25']) {
      const booked = await book(customer, run, 'T25', {
        services: ['Bathroom Cleaning'], rooms: 1, bathrooms: 1, level: 0, slot: nextSlots(new Date(), 1)[0], payment: 'cash', customer: 'customer',
        addressId: run.addressId, crew: ['cleaner'],
      });
      expect(booked.status, JSON.stringify(booked.body)).toBe(200);
    }
    const order = run.orders['T25'];
    const a = await signIn('partner', emailOf(run, 'cleaner'), run.password);
    if (!order.taken) {
      const refused = await take(a, order.id);
      expect(refused.status).not.toBe(200);
      expect(errorKeys(refused.body), JSON.stringify(refused.body)).toContain('employee.legal_documents_not_accepted');
      const legal = (await call(a, 'GET', '/api/Employee/GetMyLegalDocuments')).body as unknown[];
      const toAccept = legal.filter((x) => !(x as { isAccepted?: boolean }).isAccepted);
      expect(toAccept.length, 'the published versions wait for acceptance').toBeGreaterThan(0);
      for (const d of toAccept) {
        const r = await call(a, 'POST', '/api/Employee/AcceptLegalDocument', { acceptedTextId: str(d, 'legalDocumentTextId') });
        expect(r.status, JSON.stringify(r.body)).toBe(200);
      }
      const taken = await take(a, order.id);
      expect(taken.status, JSON.stringify(taken.body)).toBe(200);
      order.taken = true;
      saveRun(run);
    }
    if (!order.cancelled) {
      const cancelled = await cancel(customer, order.id, `E2E run ${run.runId}: TAKE-025 done`);
      expect(cancelled.status, JSON.stringify(cancelled.body)).toBe(200);
    }
    const after = (await call(customer, 'GET', `/api/Order/GetById?OrderId=${order.id}`)).body;
    expect(named(after, 'status', ORDER_STATUS) || named(after, 'orderStatus', ORDER_STATUS)).toBe('Cancelled');
    order.cancelled = true;
    saveRun(run);
    setFlag(run, 'take025.done');
  });
});
