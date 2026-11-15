import { Browser, expect, Page, test } from '@playwright/test';
import { call, DEV_ADMIN, list, named, signIn, str } from '../support/dev';
import { openPeriod } from '../support/flows';
import { emailOf, flag, loadRun, scratch } from '../support/run';
import { AdminRole, adminSignIn, SITES } from '../support/web';

// Stage 'people', web half 3 (T-0934 BRIEF §1.1, §1.2): the run's Manager, Support and Accountant in the admin SPA
// (AUTH-055), Support kept out of payroll and the Accountant let in (PAYOUT-052), the open pay period as the SPA
// names it (PAYOUT-013), and E's documents after people/50's review (AUTH-045). The role accounts exist only on a
// real run (E2E_SCRATCH=1 skips them); the period reads as the DEV administrator on a scratch run.
// Not serial: every test has its own gate, so one failure does not skip the tests after it (one worker keeps the order).
// A DEV page that hangs fails the step in 90 s, not at the 10-minute test timeout (support/web.ts waits on navigation).
test.use({ timezoneId: 'Europe/Prague', navigationTimeout: 90_000, actionTimeout: 90_000 });
const run = loadRun();
const ROLES = ['manager', 'support', 'accountant'] as const;
const DOC_STATUS = ['', 'Pending', 'Approved', 'Rejected'];
const DOC_TYPE = ['', 'IdentityCard', 'Passport', 'DriversLicense', 'WorkPermit', 'Contract', 'Certificate'];
const noRoles = () => scratch() && 'E2E_SCRATCH=1: a scratch run creates no role accounts';

// The admin host answers 429 to sign-ins in quick succession: the SPA's are spaced like signIn()'s.
let lastSpaSignIn = 0;
async function spaSignIn(page: Page, role: AdminRole) {
  const wait = lastSpaSignIn + 7_000 - Date.now();
  if (wait > 0) await page.waitForTimeout(wait);
  lastSpaSignIn = Date.now();
  await adminSignIn(page, role === 'admin' ? DEV_ADMIN.email : emailOf(run, role), role === 'admin' ? DEV_ADMIN.password : run.password);
}

async function asRole(browser: Browser, role: AdminRole) {
  const ctx = await browser.newContext({ locale: 'en-US', timezoneId: 'Europe/Prague' });
  ctx.setDefaultTimeout(90_000);
  const page = await ctx.newPage();
  await spaSignIn(page, role);
  return { ctx, page };
}

test('the Administrator lists the three role accounts, and each signs in to the SPA past the password hold', { tag: ['@E2E-AUTH-055'] }, async ({ page, browser }) => {
  const skip = noRoles();
  test.skip(!!skip, skip || '');
  for (const role of ROLES) test.skip(!flag(run, `${role}.passwordChanged`), `people/10 has not created and signed in the ${role} of run ${run.runId}`);
  test.setTimeout(4 * 60_000);
  await spaSignIn(page, 'admin');
  await page.goto(`${SITES.admin}/admin-user-management`);
  for (const role of ROLES) await expect(page.locator('tr', { hasText: emailOf(run, role) })).toBeVisible();
  // people/10 replaced the password the Administrator chose, which clears the hold the SPA keeps on /profile.
  for (const role of ROLES) {
    const { ctx, page: spa } = await asRole(browser, role);
    await expect(spa).not.toHaveURL(/\/profile|\/login|\/unauthorized/);
    await expect(spa.locator('cleansia-sidebar-menu')).toBeVisible();
    await ctx.close();
  }
});

test('Support has no pay periods or invoices in the SPA and is turned away from both; the Accountant has both', { tag: ['@E2E-PAYOUT-052'] }, async ({ browser }) => {
  const skip = noRoles();
  test.skip(!!skip, skip || '');
  const support = await asRole(browser, 'support');
  const menu = support.page.locator('cleansia-sidebar-menu');
  await expect(menu).toBeVisible();
  for (const entry of ['Pay Periods', 'Invoices']) await expect(menu.getByText(entry, { exact: true })).toHaveCount(0);
  for (const path of ['pay-periods', 'invoice-management']) {
    await support.page.goto(`${SITES.admin}/${path}`);
    await expect(support.page).toHaveURL(/\/unauthorized/);
  }
  await support.ctx.close();

  const accountant = await asRole(browser, 'accountant');
  const accountantMenu = accountant.page.locator('cleansia-sidebar-menu');
  for (const entry of ['Pay Periods', 'Invoices']) await expect(accountantMenu.getByText(entry, { exact: true })).toBeVisible();
  await accountant.page.goto(`${SITES.admin}/pay-periods`);
  await expect(accountant.page).toHaveURL(/\/pay-periods/);
  await accountant.ctx.close();
});

test('the pay periods list one open period, and its page names its first and last day in English', { tag: ['@E2E-PAYOUT-013'] }, async ({ browser }) => {
  const open = await openPeriod();
  expect(open, 'DEV has no open pay period').toBeTruthy();
  const { ctx, page } = await asRole(browser, scratch() ? 'admin' : 'accountant');
  await page.goto(`${SITES.admin}/pay-periods`);
  await expect(page.locator('tbody tr', { hasText: 'Open' })).toHaveCount(1);
  await page.goto(`${SITES.admin}/pay-periods/${open?.id}`);
  // The SPA prints both days as Intl en-US medium dates in UTC (date-formatters.utils.ts, 'utcDate').
  const day = (iso: string) => new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(`${iso.slice(0, 10)}T00:00:00Z`));
  const identity = page.locator('.detail-identity');
  await expect(identity.locator('.detail-identity__name')).toHaveText(`${day(open?.startDate ?? '')} – ${day(open?.endDate ?? '')}`);
  await expect(identity).toContainText('Open');
  const days = (Date.parse((open?.endDate ?? '').slice(0, 10)) - Date.parse((open?.startDate ?? '').slice(0, 10))) / 86_400_000 + 1;
  await expect(identity).toContainText(new RegExp(`Duration\\s+${days} days?`));
  await ctx.close();
});

test('E\'s page in the SPA keeps the identity card approved beside the replaced certificate, pending again', { tag: ['@E2E-AUTH-045'] }, async ({ browser }) => {
  let docs: unknown[] = [];
  let employeeId = '';
  try {
    const e = await signIn('partner', emailOf(run, 'cleaner3'), run.password);
    employeeId = str((await call(e, 'GET', '/api/Employee/GetCurrentEmployee')).body, 'id');
    docs = list((await call(e, 'GET', '/api/Employee/GetMyDocuments')).body, 'documents');
  } catch (err) {
    test.skip(true, `E of run ${run.runId} cannot sign in yet (people/10, people/30): ${String(err).slice(0, 160)}`);
  }
  const ofType = (type: string) => docs.find((d) => named(d, 'documentType', DOC_TYPE) === type);
  const card = ofType('IdentityCard');
  const certificate = ofType('Certificate');
  // people/50 rejects the certificate with a note and E replaces it in one step; the SPA lists active versions only,
  // so the note itself is read through the API there, and this page shows what the rejection left behind.
  test.skip(!certificate || Number(str(certificate, 'version')) < 2, `E of run ${run.runId} has not replaced the rejected certificate yet (people/50)`);
  expect(named(card, 'status', DOC_STATUS), 'E\'s identity card').toBe('Approved');
  expect(named(certificate, 'status', DOC_STATUS), 'the replacement certificate').toBe('Pending');

  const { ctx, page } = await asRole(browser, 'admin');
  await page.goto(`${SITES.admin}/employee-management/${employeeId}`);
  await expect(page.locator('.documents-group', { hasText: 'Approved Documents' }).locator('.document-card', { hasText: str(card, 'fileName') })).toBeVisible();
  const replaced = page.locator('.documents-group', { hasText: 'Pending Documents' }).locator('.document-card', { hasText: str(certificate, 'fileName') });
  await expect(replaced).toContainText(`v${str(certificate, 'version')}`);
  await ctx.close();
});
