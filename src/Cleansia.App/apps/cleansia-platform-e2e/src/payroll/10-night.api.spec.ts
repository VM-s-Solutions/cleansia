import { expect, test } from '@playwright/test';
import { writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { call, errorKeys, list, named, poll, Session, signIn, str } from '../support/dev';
import { afterPhoto, complete, feed, openPeriod, periodPays, start } from '../support/flows';
import { emailOf, flag, loadRun, runDir, RunOrder, saveRun, scratch, setFlag } from '../support/run';
import { dayAfter, startOpensAt } from '../support/slots';
import { cachedAdmin, cachedAdminAs } from '../support/web';

// Stage 'payroll', file 10 (Day 2, 07:00 UTC): what the 02:00 UTC night run did to the period of Day 1 (closed by
// System, invoiced inline), the invoice numbers, a cleaner refused another's invoice, the cash set-off statement's
// language (six PDFs the run agent reads), the Accountant's approve, mark paid and cancel with its corrected invoice,
// and E's J1 in the new period before payroll/20 presses Close now in the SPA. A scratch run proves the skips only.
// Not serial: every test has its own gate, so one failure (a PDF that cannot be rendered) does not hold up the invoice
// lifecycle, J1 and Close now after it (one worker keeps the order).

const run = loadRun();
// One property's refusals arrive joined in one string ("a; b"): split them so a key is looked for on its own.
const keys = (body: unknown) => errorKeys(body).flatMap((k) => k.split('; '));
const INVOICE = ['', 'Pending', 'Approved', 'Paid', 'Disputed', 'Rejected', 'Cancelled'];
const FAKE = '01AAAAAAAAAAAAAAAAAAAAAAAA';
const NUMBER = /^INV-2026-\d{6}$/;
const VS = /^[1-9]\d{9}$/;
const rows = (body: unknown) => list(body, 'data');
const status = (v: unknown) => named(v, 'status', INVOICE);
const accountant = () => cachedAdminAs('accountant', run);
const detail = async (id: string) => (await call(await accountant(), 'GET', `/api/AdminInvoice/details/${id}`)).body;
const invoicesOf = async (employeeId: string, periodId: string) =>
  rows((await call(await accountant(), 'GET', `/api/AdminInvoice/get-paged?Offset=0&Limit=20&Filter.EmployeeId=${employeeId}&Filter.PayPeriodId=${periodId}`)).body);
const audit = async (resourceId: string) => rows((await call(await cachedAdmin(), 'GET', `/api/AdminAuditLog/get-paged?Offset=0&Limit=20&Filter.ResourceId=${resourceId}`)).body);
const cleaner = (role: 'cleaner' | 'cleaner2') => signIn('partner', emailOf(run, role), run.password);

/** Skips until Day 2 `hhmm` UTC, then on a scratch run: the night run's period is the real run's alone. */
function dayTwo(hhmm: string, what: string) {
  test.skip(!run.day2 || !run.periodAtStart, 'people/10 records the period open on Day 1 and its Day 2 (run.day2)');
  const when = new Date(`${run.day2}T${hhmm}:00Z`);
  test.skip(Date.now() < when.getTime(), `${what}: from ${when.toISOString()} (Day 2); run payroll again then`);
  test.skip(scratch(), 'E2E_SCRATCH=1: a scratch run never waits for the night run');
}

/** A's or B's invoice of the night close, recorded by the PAYOUT-015 test. */
function invoice(key: 'A' | 'B') {
  const id = run.invoices?.[key];
  test.skip(!id, `the night close's invoice of ${key} is recorded by the PAYOUT-015 test first`);
  return id as string;
}

test.describe('payroll: the night close and the invoice lifecycle', () => {
  test('the night run closed the period of Day 1 as System and opened the next, three days long', { tag: ['@E2E-PAYOUT-014'] }, async () => {
    dayTwo('02:30', 'the night run closes the period at 02:00 UTC');
    test.skip(!!flag(run, 'period19.closedAt'), 'Close now has ended the next period since (payroll/20)');
    const p = run.periodAtStart as NonNullable<typeof run.periodAtStart>;
    const closed = (await call(await accountant(), 'GET', `/api/AdminPayPeriod/details/${p.id}`)).body;
    expect(closed).toMatchObject({ status: 'Closed', closedBy: 'System', startDate: p.startDate, endDate: p.endDate });
    expect(str(closed, 'closedAt').slice(0, 10)).toBe(run.day2);
    const periods = rows((await call(await accountant(), 'GET', '/api/AdminPayPeriod/get-paged?Offset=0&Limit=20')).body);
    const next = periods.find((x) => str(x, 'startDate') === run.day2);
    expect(next, `a period starting ${run.day2}`).toMatchObject({ status: 'Open', endDate: dayAfter(dayAfter(run.day2 as string)) });
  });

  test('the night close invoiced A and B once each, and neither E nor the applicant, leaving no pay uninvoiced', { tag: ['@E2E-PAYOUT-015'] }, async () => {
    dayTwo('02:30', 'the night run invoices the closed period');
    test.skip(!!flag(run, 'b.cancelled'), `B's invoice was cancelled at ${flag(run, 'b.cancelled')}: B now holds a corrected one too`);
    const p = run.periodAtStart as NonNullable<typeof run.periodAtStart>;
    const found: Record<string, unknown[]> = {};
    for (const [key, employeeId] of [['A', run.employeeId], ['B', run.employee2Id], ['E', run.ids?.['cleaner3Employee']], ['applicant', run.ids?.['applicantEmployee']]]) {
      expect(employeeId, `${key}'s employee id in the run`).toBeTruthy();
      found[key as string] = await invoicesOf(employeeId as string, p.id);
    }
    expect(found['A'], 'A').toHaveLength(1);
    expect(found['B'], 'B').toHaveLength(1);
    expect(found['E'], 'E').toHaveLength(0);
    expect(found['applicant'], 'applicant').toHaveLength(0);
    expect((await call(await accountant(), 'GET', `/api/AdminPayPeriod/details/${p.id}`)).body).toMatchObject({ uninvoicedPayCount: 0 });
    run.invoices = { ...run.invoices, A: str(found['A'][0], 'id'), B: str(found['B'][0], 'id') };
    saveRun(run);
  });

  test('the night close\'s invoices carry a number and a variable symbol of the payout format, all distinct', { tag: ['@E2E-PAYOUT-033'] }, async () => {
    dayTwo('02:30', 'the night run invoices the closed period');
    const ids = [invoice('A'), invoice('B'), ...(run.invoices?.['Bfix'] ? [run.invoices['Bfix']] : [])];
    const read = await Promise.all(ids.map(detail));
    for (const d of read) {
      expect(str(d, 'invoiceNumber')).toMatch(NUMBER);
      expect(str(d, 'variableSymbol')).toMatch(VS);
    }
    expect(new Set(read.map((d) => str(d, 'invoiceNumber'))).size).toBe(ids.length);
    expect(new Set(read.map((d) => str(d, 'variableSymbol'))).size).toBe(ids.length);
  });

  test('B cannot read or download A\'s invoice, nor a made-up one, and B\'s list lacks A\'s', { tag: ['@E2E-PAYOUT-037'] }, async () => {
    dayTwo('02:30', 'the night run invoices the closed period');
    const a = invoice('A');
    const b = await cleaner('cleaner2');
    for (const id of [a, FAKE]) {
      for (const path of [`/api/EmployeePayroll/GetInvoiceById/${id}`, `/api/EmployeePayroll/DownloadInvoice/${id}`]) {
        const r = await call(b, 'GET', path);
        expect(r.status, path).not.toBe(200);
        expect(keys(r.body), `${path}: ${r.raw.toString().slice(0, 200)}`).toContain('payroll.invoice.not_found');
      }
    }
    const mine = rows((await call(b, 'GET', '/api/EmployeePayroll/GetPagedInvoices?Offset=0&Limit=50')).body).map((x) => str(x, 'id'));
    expect(mine).toContain(invoice('B'));
    expect(mine).not.toContain(a);
  });

  test('A\'s set-off statement follows A\'s language, not the admin\'s: six PDFs saved for the run agent', { tag: ['@E2E-PAYOUT-057'] }, async () => {
    dayTwo('02:30', 'the night run invoices the closed period');
    const id = invoice('A');
    test.skip(!!flag(run, 'a.pdfsSaved'), `the PDFs were saved at ${flag(run, 'a.pdfsSaved')}`);
    test.skip(status(await detail(id)) !== 'Pending', 'regenerate refuses an approved or paid invoice: A\'s is no longer Pending');
    const a = await cleaner('cleaner');
    const me = (await call(a, 'GET', '/api/User/GetCurrent')).body;
    const setLanguage = async (languageCode: string) => {
      const r = await call(a, 'PUT', '/api/User/UpdateCurrentUser', {
        id: null, firstName: str(me, 'firstName'), lastName: str(me, 'lastName'), phoneNumber: str(me, 'phoneNumber'),
        birthDate: str(me, 'birthDate') || null, photo: null, languageCode,
      });
      expect(r.status, `${languageCode}: ${JSON.stringify(r.body)}`).toBe(200);
    };
    // Tried once is enough to let PAYOUT-039 approve A's invoice: a render that fails is this case's failure alone.
    if (!flag(run, 'a.pdfsTried')) setFlag(run, 'a.pdfsTried');
    const saved: string[] = [];
    for (const lang of ['en', 'cs', 'sk', 'uk', 'ru', 'cs']) {
      await setLanguage(lang);
      // The admin asks for English every time: the statement must still follow the cleaner's language.
      const regen = await call(await accountant(), 'POST', '/api/AdminInvoice/regenerate-pdf', { invoiceId: id, languageCode: 'en' });
      expect(regen.status, `${lang}: ${JSON.stringify(regen.body)}`).toBe(200);
      const pdf = await call(a, 'GET', `/api/EmployeePayroll/DownloadInvoice/${id}`);
      expect(pdf.headers['content-type'], lang).toContain('application/pdf');
      const file = join(runDir(run.runId), `invoice-A-${lang}${saved.some((f) => f.endsWith(`-${lang}.pdf`)) ? '-2' : ''}.pdf`);
      writeFileSync(file, pdf.raw);
      saved.push(file);
    }
    await setLanguage('en');
    console.log(`for the run agent to read (statement in that language, the rest Czech; the two cs files alike):\n  ${saved.join('\n  ')}`);
    setFlag(run, 'a.pdfsSaved');
  });

  test('the Accountant approves A\'s invoice; A reads it Approved and the audit log holds the approval', { tag: ['@E2E-PAYOUT-039'] }, async () => {
    dayTwo('02:30', 'the night run invoices the closed period');
    const id = invoice('A');
    test.skip(!flag(run, 'a.pdfsSaved') && !flag(run, 'a.pdfsTried'), 'the set-off PDFs (PAYOUT-057) are tried while A\'s invoice is Pending');
    if (status(await detail(id)) === 'Pending') {
      const r = await call(await accountant(), 'PUT', '/api/AdminInvoice/approve', { invoiceId: id, adminNotes: `E2E run ${run.runId}` });
      expect(r.status, JSON.stringify(r.body)).toBe(200);
    }
    test.skip(status(await detail(id)) === 'Paid', 'A\'s invoice is Paid already (PAYOUT-043 ran)');
    expect(status(await detail(id))).toBe('Approved');
    const a = await cleaner('cleaner');
    expect(status((await call(a, 'GET', `/api/EmployeePayroll/GetInvoiceById/${id}`)).body)).toBe('Approved');
    expect(await audit(id)).toContainEqual(expect.objectContaining({ action: 'ApproveInvoice', actorEmail: emailOf(run, 'accountant'), success: true }));
  });

  test('the Accountant marks A\'s invoice paid with a transfer note; it is final, recorded, and A is told', { tag: ['@E2E-PAYOUT-043'] }, async () => {
    dayTwo('02:30', 'the night run invoices the closed period');
    const id = invoice('A');
    const note = `E2E run ${run.runId}: transfer of A's payout`;
    const before = status(await detail(id));
    test.skip(before === 'Pending', 'A\'s invoice is approved first (PAYOUT-039)');
    if (before === 'Approved') {
      const r = await call(await accountant(), 'PUT', '/api/AdminInvoice/mark-paid', { invoiceId: id, bankTransferNote: note, adminNotes: `E2E run ${run.runId}` });
      expect(r.status, JSON.stringify(r.body)).toBe(200);
    }
    const d = await detail(id);
    expect(status(d)).toBe('Paid');
    expect(str(d, 'bankTransferNote')).toBe(note);
    expect(str(d, 'paidAt')).not.toBe('');
    expect(await audit(id)).toContainEqual(expect.objectContaining({ action: 'MarkInvoicePaid', actorEmail: emailOf(run, 'accountant'), success: true }));
    const a = await cleaner('cleaner');
    expect(status((await call(a, 'GET', `/api/EmployeePayroll/GetInvoiceById/${id}`)).body)).toBe('Paid');
    const told = await poll(() => feed(run, 'cleaner', 'partner-mobile'), (f) => f.some((n) => str(n, 'eventKey') === 'payroll.invoice_paid'), 120_000, 15_000);
    expect(told.map((n) => str(n, 'eventKey'))).toContain('payroll.invoice_paid');
  });

  test('the Accountant cancels B\'s invoice and a corrected one follows: same cleaner, period, currency and jobs, a new number and symbol', { tag: ['@E2E-PAYOUT-046', '@E2E-PAYOUT-033'] }, async () => {
    dayTwo('02:30', 'the night run invoices the closed period');
    test.setTimeout(20 * 60_000);
    const id = invoice('B');
    const p = run.periodAtStart as NonNullable<typeof run.periodAtStart>;
    const old = await detail(id);
    if (status(old) !== 'Cancelled') {
      const r = await call(await accountant(), 'PUT', '/api/AdminInvoice/cancel', { invoiceId: id, reason: `E2E run ${run.runId}: corrected invoice` });
      expect(r.status, JSON.stringify(r.body)).toBe(200);
      setFlag(run, 'b.cancelled');
    }
    const cancelled = await detail(id);
    expect(status(cancelled)).toBe('Cancelled');
    expect(list(cancelled, 'orderPays')).toHaveLength(0);
    // The five-minute reconciliation sweep writes the corrected invoice: ten minutes at most.
    const fresh = await poll(
      async () => (await invoicesOf(run.employee2Id as string, p.id)).find((x) => str(x, 'id') !== id && status(x) !== 'Cancelled'),
      Boolean,
      12 * 60_000,
      30_000,
    );
    expect(fresh, 'a corrected invoice of B within 12 minutes of the cancel').toBeTruthy();
    const fix = await detail(str(fresh, 'id'));
    expect(status(fix)).toBe('Pending');
    expect(fix).toMatchObject({ employeeId: str(old, 'employeeId'), payPeriodId: p.id, currencyCode: str(old, 'currencyCode') });
    expect(str(fix, 'invoiceNumber')).toMatch(NUMBER);
    expect(str(fix, 'variableSymbol')).toMatch(VS);
    expect(str(fix, 'invoiceNumber')).not.toBe(str(old, 'invoiceNumber'));
    expect(str(fix, 'variableSymbol')).not.toBe(str(old, 'variableSymbol'));
    const b = await cleaner('cleaner2');
    const pays = (await call(b, 'GET', `/api/EmployeePayroll/GetPeriodPays?EmployeeId=${run.employee2Id}&PayPeriodId=${p.id}`)).body;
    expect(str(pays, 'invoiceId')).toBe(str(fix, 'id'));
    const jobs = (v: unknown, path: string) => list(v, path).map((r) => str(r, 'orderId')).sort();
    expect(jobs(fix, 'orderPays')).toEqual(jobs(pays, 'orderPays'));
    run.invoices = { ...run.invoices, Bfix: str(fix, 'id') };
    saveRun(run);
  });

  test('E works J1 in its window, and its pay row lands in the period the night run opened', async () => {
    dayTwo('02:30', 'J1 is Day 2\'s job');
    const j1 = run.orders['J1'] as RunOrder | undefined;
    test.skip(!j1?.taken, 'orders/20 has E take J1 on Day 1');
    test.skip(!!flag(run, 'j1.done'), `J1's pay row was seen at ${flag(run, 'j1.done')}`);
    if (!j1) return;
    const opens = startOpensAt(new Date(j1.slot));
    test.skip(Date.now() < opens.getTime(), `J1 (slot ${j1.slot}) can be started from ${opens.toISOString()}; run payroll again then`);
    const e: Session = await signIn('partner-mobile', emailOf(run, 'cleaner3'), run.password);
    if (!j1.completed) {
      for (const [step, r] of [['start', () => start(e, j1.id)], ['after photo', () => afterPhoto(e, j1.id, `E2E run ${run.runId} J1`)], ['complete', () => complete(e, j1.id, `E2E run ${run.runId} J1`)]] as const) {
        const reply = await r();
        expect(reply.status, `${step}: ${JSON.stringify(reply.body)}`).toBe(200);
      }
      j1.completed = true;
      saveRun(run);
    }
    const open = await openPeriod();
    expect(open?.startDate, 'J1 lands in the period the night run opened').toBe(run.day2);
    const ids = await poll(async () => (await periodPays(e, run.ids?.['cleaner3Employee'] ?? '', open?.id ?? '')).map((r) => str(r, 'orderId')), (x) => x.includes(j1.id));
    expect(ids).toContain(j1.id);
    setFlag(run, 'j1.done');
  });
});
