import { Browser, expect, test } from '@playwright/test';
import { call, str } from '../support/dev';
import { openPeriod } from '../support/flows';
import { RULED_DAYS } from '../support/plan';
import { emailOf, flag, loadRun, saveRun, scratch, setFlag } from '../support/run';
import { dayAfter } from '../support/slots';
import { adminSpa, cachedAdmin, SITES } from '../support/web';

// Stage 'payroll', web half 1 (F-012 too): on Day 2, once J1's pay is in the period the night run
// opened, the Accountant presses the admin SPA's own Close now. It is the only Close now of the run, never pressed on a
// scratch run nor with E2E_NO_CLOSE_NOW=1, and never on any day but Day 2.
// Not serial: every test has its own gate, so one failure does not skip the tests after it (one worker keeps the order).
// A DEV page that hangs fails the step in 90 s, not at the 10-minute test timeout (support/web.ts waits on navigation).
test.use({ timezoneId: 'Europe/Prague', navigationTimeout: 90_000, actionTimeout: 90_000 });
const run = loadRun();
const periodDetail = async (id: string) => (await call(await cachedAdmin(), 'GET', `/api/AdminPayPeriod/details/${id}`)).body;

async function accountantSpa(browser: Browser) {
  const spa = await adminSpa(browser, 'accountant', run);
  await expect(spa.page, 'the Accountant is held on the password change').not.toHaveURL(/\/profile/);
  return spa;
}

test('the Accountant closes the Day 2 period with the SPA\'s Close now; it ends today in their name and the next one starts tomorrow', { tag: ['@E2E-F-012'] }, async ({ browser }) => {
  test.skip(scratch(), 'E2E_SCRATCH=1: a scratch run never presses Close now');
  test.skip(process.env['E2E_NO_CLOSE_NOW'] === '1', 'E2E_NO_CLOSE_NOW=1: Close now is not pressed on this run');
  test.skip(!!flag(run, 'period19.closedAt'), `closed at ${flag(run, 'period19.closedAt')}`);
  test.skip(!flag(run, 'j1.done'), 'J1 is not complete with its pay in the period yet (payroll/10)');
  const day2 = run.day2 ?? RULED_DAYS.D2;
  const today = new Date().toISOString().slice(0, 10);
  test.skip(today !== day2, `Close now is pressed on Day 2 (${day2}) only; today is ${today}`);
  const open = await openPeriod();
  expect(open, 'DEV has no open pay period').toBeTruthy();
  const id = open?.id ?? '';
  expect(str(open, 'startDate').slice(0, 10), 'the open period is the one the night run opened on Day 2').toBe(day2);
  const length = Number(str(await periodDetail(id), 'durationDays'));

  const { ctx, page } = await accountantSpa(browser);
  await page.goto(`${SITES.admin}/pay-periods/${id}`);
  await page.getByRole('button', { name: 'Close now' }).click();
  await page.getByRole('textbox', { name: /^Reason/ }).fill(`E2E run ${run.runId}: F-012, the SPA's own Close now`);
  await page.getByRole('button', { name: 'Close the period now' }).click();
  await expect(page.locator('.detail-identity')).toContainText('Closed', { timeout: 60_000 });
  run.closedPeriodId = id;
  run.closedAt = new Date().toISOString();
  saveRun(run);
  setFlag(run, 'period19.closedAt');
  await expect(page.locator('.detail-grid__item', { hasText: 'Closed By' })).toContainText(emailOf(run, 'accountant'));
  await ctx.close();

  const closed = await periodDetail(id);
  expect(str(closed, 'status')).toBe('Closed');
  expect(str(closed, 'endDate').slice(0, 10)).toBe(today);
  expect(str(closed, 'closedBy')).toBe(emailOf(run, 'accountant'));
  const next = await openPeriod();
  expect(str(next, 'startDate').slice(0, 10)).toBe(dayAfter(today));
  expect(Number(str(await periodDetail(next?.id ?? ''), 'durationDays'))).toBe(length);
});
