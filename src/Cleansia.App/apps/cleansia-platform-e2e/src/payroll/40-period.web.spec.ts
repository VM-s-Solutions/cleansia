import { Browser, expect, test } from '@playwright/test';
import { call, list, named, Session, signIn, str } from '../support/dev';
import { RULED_DAYS } from '../support/plan';
import { emailOf, flag, loadRun, Role, scratch, setFlag } from '../support/run';
import { adminSignIn, cachedAdmin, SITES } from '../support/web';

// Stage 'payroll', web half 2: Day 2 in the admin SPA as the Accountant. The night close of the
// Day 1 period reads "Automatically" (PAYOUT-014) and lists only the cleaners with pay (PAYOUT-015); the period F-012
// closed cannot be marked paid while an invoice in it is open (PAYOUT-029); E's approved invoice is marked paid
// through the in-page confirmation (PAYOUT-045); then the period is marked paid (PAYOUT-030). No scratch run reaches
// Day 2, so every test skips on one.
// Not serial: every test has its own gate, so one failure does not skip the tests after it (one worker keeps the order).
// A DEV page that hangs fails the step in 90 s, not at the 10-minute test timeout (support/web.ts waits on navigation).
test.use({ timezoneId: 'Europe/Prague', navigationTimeout: 90_000, actionTimeout: 90_000 });
const run = loadRun();
const rows = (body: unknown) => (list(body, 'data').length ? list(body, 'data') : list(body, 'items'));
// An invoice's status arrives as its enum number, a period's as its name: named() reads both.
const status = (v: unknown) => named(v, 'status', ['', 'Pending', 'Approved', 'Paid', 'Disputed', 'Rejected', 'Cancelled']);
const admin = () => cachedAdmin();
const periodDetail = async (id: string) => (await call(await admin(), 'GET', `/api/AdminPayPeriod/details/${id}`)).body;
const invoicesOf = async (employeeId: string, periodId: string) =>
  rows((await call(await admin(), 'GET', `/api/AdminInvoice/get-paged?Offset=0&Limit=10&Filter.EmployeeId=${employeeId}&Filter.PayPeriodId=${periodId}`)).body);

test.beforeEach(() => {
  test.skip(scratch(), 'E2E_SCRATCH=1: the payroll stage runs on Day 2 of the real run only');
});

async function accountantSpa(browser: Browser) {
  const ctx = await browser.newContext({ locale: 'en-US', timezoneId: 'Europe/Prague' });
  ctx.setDefaultTimeout(90_000);
  const page = await ctx.newPage();
  await adminSignIn(page, emailOf(run, 'accountant'), run.password);
  await expect(page, 'the Accountant is held on the password change').not.toHaveURL(/\/profile/);
  return { ctx, page };
}

/** The period the night run closed: the one that was open at the start of the run, ending on Day 1. */
async function nightPeriod() {
  if (run.periodAtStart?.id) return run.periodAtStart.id;
  const periods = rows((await call(await admin(), 'GET', '/api/AdminPayPeriod/get-paged?Offset=0&Limit=20')).body);
  return str(periods.find((p) => str(p, 'endDate').slice(0, 10) === RULED_DAYS.D1), 'id');
}

async function employeeIdOf(role: Role) {
  const s: Session = await signIn('partner', emailOf(run, role), run.password);
  return str((await call(s, 'GET', '/api/Employee/GetCurrentEmployee')).body, 'id');
}

test('the Day 1 period reads closed "Automatically", and its invoices are A\'s and B\'s, none for E', { tag: ['@E2E-PAYOUT-014', '@E2E-PAYOUT-015'] }, async ({ browser }) => {
  const id = await nightPeriod();
  test.skip(!id, `no period of run ${run.runId} ends on ${RULED_DAYS.D1}`);
  const detail = await periodDetail(id);
  test.skip(status(detail) === 'Open', `the period ${id} is still open: the 02:00 UTC night run has not closed it (payroll/10 reports it)`);
  const [a, b, e] = [run.employeeId ?? '', run.employee2Id ?? '', await employeeIdOf('cleaner3')];
  const [ofA, ofB, ofE] = [await invoicesOf(a, id), await invoicesOf(b, id), await invoicesOf(e, id)];
  expect(ofA.length, 'A\'s invoices in the night-closed period').toBeGreaterThan(0);
  expect(ofB.length, 'B\'s invoices in the night-closed period').toBeGreaterThan(0);
  expect(ofE, 'E has no pay on Day 1, so no invoice').toEqual([]);

  const { ctx, page } = await accountantSpa(browser);
  await page.goto(`${SITES.admin}/pay-periods/${id}`);
  await expect(page.locator('.detail-grid__item', { hasText: 'Closed By' })).toContainText('Automatically');
  await page.getByRole('button', { name: 'View invoices' }).click();
  await expect(page).toHaveURL(new RegExp(`/invoice-management\\?payPeriodId=${id}`));
  for (const invoice of [...ofA, ...ofB]) await expect(page.locator('tr', { hasText: str(invoice, 'invoiceNumber') })).toBeVisible();
  await ctx.close();
});

test('the period Close now ended cannot be marked paid while an invoice in it is open', { tag: ['@E2E-PAYOUT-029'] }, async ({ browser }) => {
  const id = run.closedPeriodId ?? '';
  test.skip(!flag(run, 'period19.closedAt') || !id, 'F-012 has not closed the Day 2 period yet (payroll/20)');
  const detail = await periodDetail(id);
  const open = Number(str(detail, 'unpaidInvoiceCount') || 0) + Number(str(detail, 'uninvoicedPayCount') || 0);
  test.skip(status(detail) !== 'Closed' || open === 0, `the period ${id} reads ${status(detail)} with nothing left open in it`);
  const { ctx, page } = await accountantSpa(browser);
  await page.goto(`${SITES.admin}/pay-periods/${id}`);
  await expect(page.getByRole('button', { name: 'Mark Period as Paid' })).toBeDisabled();
  await expect(page.locator('main')).toContainText('Available once every invoice in this period is paid or cancelled and all its pay is invoiced.');
  await ctx.close();
});

test('E\'s approved invoice is marked paid through the confirmation, which shows the transfer, the symbols and the account', { tag: ['@E2E-PAYOUT-045'] }, async ({ browser }) => {
  const periodId = run.closedPeriodId ?? '';
  test.skip(!flag(run, 'e.invoiceApproved') || !periodId, 'E\'s invoice is not approved yet (payroll/30)');
  const e = await employeeIdOf('cleaner3');
  const listed = (await invoicesOf(e, periodId)).find((i) => ['Approved', 'Paid'].includes(status(i)));
  test.skip(!listed, `E has no approved invoice in the period ${periodId}`);
  const invoiceId = str(listed, 'id');
  const before = (await call(await admin(), 'GET', `/api/AdminInvoice/details/${invoiceId}`)).body;
  test.skip(status(before) === 'Paid', 'E\'s invoice is already paid');
  expect(str(before, 'constantSymbol'), 'the invoice detail carries the constant symbol the PDF prints').toBe('0308');
  const masked = str((await call(await admin(), 'GET', `/api/AdminEmployee/${e}/payout-details`)).body, 'maskedAccount');
  const note = `E2E run ${run.runId}: transfer sent`;

  const { ctx, page } = await accountantSpa(browser);
  await page.goto(`${SITES.admin}/invoice-management/${invoiceId}`);
  await page.getByRole('button', { name: 'Mark as Paid' }).click();
  const panel = page.locator('.detail-actions__panel', { hasText: 'Send the amount to transfer' });
  const item = (label: string) => panel.locator('.detail-grid__item', { hasText: label }).locator('.detail-grid__value');
  await expect(item('Amount to Transfer')).toContainText(Math.trunc(Number(str(before, 'transferAmount'))).toLocaleString('en-US'));
  await expect(item('Variable Symbol')).toHaveText(str(before, 'variableSymbol'));
  await expect(item('Constant Symbol')).toHaveText('0308');
  expect(masked, 'E\'s masked account').not.toBe('');
  await expect(panel).toContainText(masked);
  await expect(panel.locator('textarea')).toBeVisible();
  expect(status((await call(await admin(), 'GET', `/api/AdminInvoice/details/${invoiceId}`)).body), 'still Approved before the confirmation').toBe('Approved');
  await panel.locator('textarea').fill(note);
  await panel.getByRole('button', { name: 'Confirm the transfer was sent' }).click();
  await expect(page.getByText('Invoice marked as paid successfully')).toBeVisible();
  await ctx.close();
  const after = (await call(await admin(), 'GET', `/api/AdminInvoice/details/${invoiceId}`)).body;
  expect(status(after)).toBe('Paid');
  expect(str(after, 'bankTransferNote')).toBe(note);
});

test('with every invoice in it settled, the Accountant marks the Close-now period paid', { tag: ['@E2E-PAYOUT-030'] }, async ({ browser }) => {
  const id = run.closedPeriodId ?? '';
  test.skip(!flag(run, 'period19.closedAt') || !id, 'F-012 has not closed the Day 2 period yet (payroll/20)');
  test.skip(!!flag(run, 'period19.paid'), `marked paid at ${flag(run, 'period19.paid')}`);
  const detail = await periodDetail(id);
  test.skip(status(detail) !== 'Closed', `the period ${id} reads ${status(detail)}`);
  const left = `${str(detail, 'unpaidInvoiceCount')} unpaid invoice(s), ${str(detail, 'uninvoicedPayCount')} pay row(s) not invoiced`;
  test.skip(Number(str(detail, 'unpaidInvoiceCount') || 0) > 0 || Number(str(detail, 'uninvoicedPayCount') || 0) > 0, `the period ${id} still has ${left}`);
  const { ctx, page } = await accountantSpa(browser);
  await page.goto(`${SITES.admin}/pay-periods/${id}`);
  await page.getByRole('button', { name: 'Mark Period as Paid' }).click();
  await page.getByRole('button', { name: 'Confirm Mark as Paid' }).click();
  await expect(page.getByText('Pay period marked as paid')).toBeVisible();
  await ctx.close();
  setFlag(run, 'period19.paid');
  const paid = await periodDetail(id);
  expect(status(paid)).toBe('Paid');
  expect(str(paid, 'paidAt')).not.toBe('');
});
