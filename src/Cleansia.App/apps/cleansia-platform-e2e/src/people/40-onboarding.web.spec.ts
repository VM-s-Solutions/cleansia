import { expect, Page, test } from '@playwright/test';
import { call, DEV_ADMIN, named, Session, signIn, str } from '../support/dev';
import { emailOf, loadRun, Role } from '../support/run';
import { acceptCookies, adminSignIn, SITES } from '../support/web';

// Stage 'people', web half 2 (T-0934 BRIEF §1.2): the partner web's registration lock as E sees it before its
// profile (AUTH-034) and as the rejected applicant sees it (AUTH-049), and the admin SPA offering no approval for a
// profile that is not complete (AUTH-047). People/30 confirms both and rejects the applicant; people/50 completes E.
// Not serial: every test has its own gate, so one failure does not skip the tests after it (one worker keeps the order).
// A DEV page that hangs fails the step in 90 s, not at the 10-minute test timeout (support/web.ts waits on navigation).
test.use({ timezoneId: 'Europe/Prague', navigationTimeout: 90_000, actionTimeout: 90_000 });
const run = loadRun();
const CONTRACT = ['', 'Pending', 'Active', 'Terminated', 'Approved', 'Rejected'];

/** A run cleaner's partner session and registration status, or the reason it cannot be read yet. */
async function cleanerStatus(role: Role): Promise<{ s?: Session; status?: unknown; skip?: string }> {
  try {
    const s = await signIn('partner', emailOf(run, role), run.password);
    return { s, status: (await call(s, 'GET', '/api/Employee/CheckCurrentEmployee')).body };
  } catch (e) {
    return { skip: `the ${role} of run ${run.runId} cannot sign in yet (people/10 registers, people/30 confirms): ${String(e).slice(0, 160)}` };
  }
}

/** Signs in on the partner web and waits to leave /login; a locked cleaner is not sent to /orders. */
async function partnerWebSignIn(page: Page, role: Role) {
  await page.goto(`${SITES.partner}/login`);
  await acceptCookies(page);
  await page.locator('input[type=email]').fill(emailOf(run, role));
  await page.locator('input[type=password]').fill(run.password);
  await page.getByRole('button', { name: 'Login', exact: true }).click();
  await page.waitForURL((u) => !u.pathname.startsWith('/login'));
}

test('E, confirmed and with no profile yet, meets the lock: three steps and the approval waits on the profile', { tag: ['@E2E-AUTH-034'] }, async ({ page }) => {
  const e = await cleanerStatus('cleaner3');
  test.skip(!!e.skip, e.skip);
  test.skip(str(e.status, 'hasCompletedProfile') === 'true', `E of run ${run.runId} has completed its profile (people/50): the lock is past this state`);
  await partnerWebSignIn(page, 'cleaner3');
  const lock = page.locator('.cleansia-registration-lock');
  await expect(lock).toBeVisible();
  await expect(lock).toContainText('Profile Completion Required');
  for (const step of ['Profile Information', 'Required Documents', 'Admin Approval']) await expect(lock).toContainText(step);
  await expect(lock).toContainText('Complete your profile and upload documents to request approval.');
  await expect(lock).toContainText(/0 of 3 steps complete/);
});

test('the admin SPA offers no approval for E while its profile is incomplete', { tag: ['@E2E-AUTH-047'] }, async ({ page }) => {
  const e = await cleanerStatus('cleaner3');
  test.skip(!!e.skip, e.skip);
  test.skip(str(e.status, 'hasCompletedProfile') === 'true', `E of run ${run.runId} has completed its profile (people/50)`);
  const employeeId = str((await call(e.s as Session, 'GET', '/api/Employee/GetCurrentEmployee')).body, 'id');
  await adminSignIn(page, DEV_ADMIN.email, DEV_ADMIN.password);
  await page.goto(`${SITES.admin}/employee-management/${employeeId}`);
  const main = page.locator('main');
  await expect(main).toContainText(emailOf(run, 'cleaner3'));
  await expect(main).toContainText('Pending');
  await expect(page.getByRole('button', { name: /Approve Partner/ })).toHaveCount(0);
});

test('the rejected applicant reads the reason verbatim on the lock, with a way to contact support', { tag: ['@E2E-AUTH-049'] }, async ({ page }) => {
  const a = await cleanerStatus('applicant');
  test.skip(!!a.skip, a.skip);
  test.skip(named(a.status, 'contractStatus', CONTRACT) !== 'Rejected', `the applicant of run ${run.runId} is not rejected yet (people/30)`);
  const reason = str(a.status, 'rejectionReason');
  expect(reason).not.toBe('');
  await partnerWebSignIn(page, 'applicant');
  const lock = page.locator('.cleansia-registration-lock');
  await expect(lock).toContainText('Your submission was rejected. Please review the reason below and re-upload.');
  await expect(lock).toContainText(reason);
  await expect(lock.locator('a[href^="mailto:support@cleansia.cz?subject="]', { hasText: 'Contact support' })).toBeVisible();
});
