import { expect, test } from '@playwright/test';
import { call, errorKeys, list, named, str } from '../support/dev';
import { waitInvoice } from '../support/flows';
import { flag, loadRun, saveRun, scratch, setFlag } from '../support/run';
import { dayAfter } from '../support/slots';
import { cachedAdminAs } from '../support/web';

// Stage 'payroll', file 30 (Day 2, after payroll/20 pressed Close now in the SPA on the period the night run opened):
// Mark period paid is refused while the period is open, while its pay is uninvoiced (the invoicing sweep waits 15
// minutes after a close) and while E's invoice is unpaid; then the Accountant approves E's invoice for payroll/40 to
// mark paid in the SPA. The four invoice numbers of the run are distinct. A scratch run proves the skips only.
test.describe.configure({ mode: 'serial' });

const run = loadRun();
// One property's refusals arrive joined in one string ("a; b"): split them so a key is looked for on its own.
const keys = (body: unknown) => errorKeys(body).flatMap((k) => k.split('; '));
const INVOICE = ['', 'Pending', 'Approved', 'Paid', 'Disputed', 'Rejected', 'Cancelled'];
const rows = (body: unknown) => list(body, 'data');
const accountant = () => cachedAdminAs('accountant', run);
const markPaid = async (payPeriodId: string) => call(await accountant(), 'POST', '/api/AdminPayPeriod/mark-paid', { payPeriodId });
const employeeE = () => run.ids?.['cleaner3Employee'] ?? '';

/** The period Close now ended on Day 2 (Day 2 to Day 2) and its successor, open from the day after. */
async function closedAndNext() {
  const periods = rows((await call(await accountant(), 'GET', '/api/AdminPayPeriod/get-paged?Offset=0&Limit=20')).body);
  return { closed: periods.find((p) => str(p, 'startDate') === run.day2), next: periods.find((p) => str(p, 'startDate') === dayAfter(run.day2 as string)) };
}

function afterCloseNow() {
  test.skip(!run.day2, 'people/10 records Day 2 (run.day2)');
  test.skip(!flag(run, 'period19.closedAt'), `payroll/20 presses Close now in the SPA on Day 2 (${run.day2}) about 07:15 UTC, after J1 (payroll/10)`);
  test.skip(scratch(), 'E2E_SCRATCH=1: a scratch run never presses Close now');
}

test.describe('payroll: marking the period paid', () => {
  test('Mark period paid is refused while open, while pay is uninvoiced, and while an invoice is unpaid, in that order', { tag: ['@E2E-PAYOUT-029'] }, async () => {
    afterCloseNow();
    test.skip(!!flag(run, 'e.invoiceApproved'), `E's invoice was approved at ${flag(run, 'e.invoiceApproved')}: the refusals ran before`);
    test.setTimeout(35 * 60_000);
    const { closed, next } = await closedAndNext();
    expect(closed, `the period of ${run.day2}`).toMatchObject({ status: 'Closed', endDate: run.day2 });
    expect(next, `its successor from ${dayAfter(run.day2 as string)}`).toMatchObject({ status: 'Open' });
    const open = await markPaid(str(next, 'id'));
    expect(keys(open.body), JSON.stringify(open.body)).toContain('pay_period.not_closed');
    const closedId = str(closed, 'id');
    // The sweep invoices a closed period only once its close is 15 minutes old (PayPeriodRepository): until E's invoice
    // exists, J1's pay row is uninvoiced. A run that arrives after the invoice cannot show this refusal any more.
    const eInvoices = async () => rows((await call(await accountant(), 'GET', `/api/AdminInvoice/get-paged?Offset=0&Limit=10&Filter.EmployeeId=${employeeE()}&Filter.PayPeriodId=${closedId}`)).body);
    if (!(await eInvoices()).length) {
      const uninvoiced = await markPaid(closedId);
      expect(keys(uninvoiced.body), JSON.stringify(uninvoiced.body)).toContain('pay_period.has_uninvoiced_pay');
      setFlag(run, 'payout029.uninvoiced');
    }
    // Soft: a missed window fails the case but must not hold up E's invoice for payroll/40.
    expect.soft(flag(run, 'payout029.uninvoiced'), 'the uninvoiced-pay refusal shows only within 15 minutes of the close: run payroll/30 straight after payroll/20').toBeTruthy();
    // The five-minute sweep, 15 minutes after the close: not a defect before 25.
    const deadline = new Date(flag(run, 'period19.closedAt') as string).getTime() + 25 * 60_000;
    const invoice = await waitInvoice(employeeE(), closedId, Math.max(deadline - Date.now(), 0) + 30_000);
    expect(invoice, `E's invoice of the period of ${run.day2}, 25 minutes after the close`).toBeTruthy();
    run.invoices = { ...run.invoices, E: str(invoice, 'id') };
    saveRun(run);
    const unpaid = await markPaid(closedId);
    expect(keys(unpaid.body), JSON.stringify(unpaid.body)).toContain('pay_period.has_unpaid_invoices');
    // Hand-over to payroll/40: the SPA marks E's approved invoice paid (PAYOUT-045), then the period (PAYOUT-030).
    const approve = await call(await accountant(), 'PUT', '/api/AdminInvoice/approve', { invoiceId: run.invoices['E'], adminNotes: `E2E run ${run.runId}` });
    expect(approve.status, JSON.stringify(approve.body)).toBe(200);
    expect(named((await call(await accountant(), 'GET', `/api/AdminInvoice/details/${run.invoices['E']}`)).body, 'status', INVOICE)).toBe('Approved');
    setFlag(run, 'e.invoiceApproved');
  });

  test('the run\'s four invoices (A, B, B corrected, E) carry distinct payout numbers and variable symbols', { tag: ['@E2E-PAYOUT-033'] }, async () => {
    afterCloseNow();
    const keys = ['A', 'B', 'Bfix', 'E'];
    const missing = keys.filter((k) => !run.invoices?.[k]);
    test.skip(missing.length > 0, `the invoice(s) ${missing.join(', ')} are recorded by payroll/10 and the test above first`);
    const read = await Promise.all(keys.map(async (k) => (await call(await accountant(), 'GET', `/api/AdminInvoice/details/${run.invoices?.[k]}`)).body));
    for (const d of read) {
      expect(str(d, 'invoiceNumber')).toMatch(/^INV-2026-\d{6}$/);
      expect(str(d, 'variableSymbol')).toMatch(/^[1-9]\d{9}$/);
    }
    expect(new Set(read.map((d) => str(d, 'invoiceNumber'))).size).toBe(keys.length);
    expect(new Set(read.map((d) => str(d, 'variableSymbol'))).size).toBe(keys.length);
  });
});
