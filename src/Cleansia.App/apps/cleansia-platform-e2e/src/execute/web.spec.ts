import { expect, Page, test } from '@playwright/test';
import { call, named, ORDER_STATUS, PAYMENT_STATUS, png } from '../support/dev';
import { emailOf, loadRun, RunOrder, saveRun } from '../support/run';
import { startOpensAt } from '../support/slots';
import { cachedAdmin, partnerSignIn, SITES } from '../support/web';

// Stage 'execute', web half: the web cleaner works the two jobs it took on the partner web in 'book', each only once
// its start window (the slot less 60 minutes) is open. Before that a job's test skips and says when it opens.
test.describe.configure({ mode: 'serial' });
const run = loadRun();

function ready(key: 'webCard' | 'webCash'): RunOrder {
  const o = run.orders[key];
  test.skip(!o?.taken, `the ${key} job was not taken in the book stage of run ${run.runId}`);
  test.skip(!!o.completed, `the ${key} job was completed by an earlier execute run`);
  const opens = startOpensAt(new Date(o.slot));
  test.skip(Date.now() < opens.getTime(), `the ${key} job (slot ${o.slot}) can be started from ${opens.toISOString()}; run execute again then`);
  return o;
}

async function addPhoto(page: Page, kind: 'Before' | 'After') {
  await page.locator('input[type=file]').nth(kind === 'Before' ? 0 : 1).setInputFiles({ name: `${kind.toLowerCase()}.png`, mimeType: 'image/png', buffer: Buffer.from(png(), 'base64') });
  await page.getByRole('button', { name: 'Save Photos' }).click();
  await expect(page.locator('main')).toContainText(new RegExp(`${kind} Photos:\\s*1`), { timeout: 60_000 });
}

/** Whether the job page counts at least one photo of the kind; the counts load after the page, so it waits for them. */
async function hasPhoto(page: Page, kind: 'Before' | 'After') {
  let count = '';
  await expect.poll(async () => (count = (await page.locator('main').innerText()).match(new RegExp(`${kind} Photos:\\s*(\\d+)`))?.[1] ?? '')).not.toBe('');
  return Number(count) > 0;
}

/** Opens the job page and brings the job to In Progress with its before photo; a job already under way is left as it is. */
async function startOnJobPage(page: Page, o: RunOrder) {
  await page.goto(`${SITES.partner}/orders/${o.id}`);
  await expect(page.locator('main')).toContainText(/Confirmed|In Progress/);
  const start = page.getByRole('button', { name: 'Start Order' });
  if (!(await start.isVisible())) return;
  if (!(await hasPhoto(page, 'Before'))) await addPhoto(page, 'Before');
  await start.click();
  await expect(page.locator('main')).toContainText('In Progress', { timeout: 60_000 });
}

/** The completion is offered only once the after photo is saved; a finished job keeps both photos. A fresh page: the photo section keeps its saving state after the before photo. */
async function addAfterPhotoAndComplete(page: Page) {
  await page.reload();
  const complete = page.getByRole('button', { name: 'Complete Order' });
  if (!(await hasPhoto(page, 'After'))) {
    await expect(complete).toHaveCount(0);
    await addPhoto(page, 'After');
  }
  await complete.first().click();
  await expect(page.locator('main')).toContainText('Completed', { timeout: 60_000 });
  await page.reload();
  await expect(page.locator('main')).toContainText(/Before Photos:\s*1/);
  await expect(page.locator('main')).toContainText(/After Photos:\s*1/);
}

test.describe('partner web, execute', () => {
  test('the card job: the before photo, start, no completion offered before the after photo, the after photo, complete', { tag: ['@E2E-EXEC-002', '@E2E-EXEC-014', '@E2E-EXEC-015', '@E2E-EXEC-016', '@E2E-EXEC-037', '@E2E-EXEC-038', '@E2E-EXEC-050', '@E2E-PAYOUT-001'] }, async ({ page }) => {
    const o = ready('webCard');
    await partnerSignIn(page, emailOf(run, 'cleaner2'), run.password);
    await startOnJobPage(page, o);
    await addAfterPhotoAndComplete(page);
    o.completed = true;
    saveRun(run);
    const detail = (await call(await cachedAdmin(), 'GET', `/api/AdminOrder/details/${o.id}`)).body;
    expect(named(detail, 'orderStatus', ORDER_STATUS)).toBe('Completed');
    expect(named(detail, 'paymentStatus', PAYMENT_STATUS)).toBe('Paid');
  });

  test('the cash job: the cleaner records the cash, then completes', { tag: ['@E2E-EXEC-002', '@E2E-EXEC-026', '@E2E-EXEC-036', '@E2E-EXEC-039', '@E2E-EXEC-049', '@E2E-PAY-034', '@E2E-PAYOUT-054'] }, async ({ page }) => {
    const o = ready('webCash');
    await partnerSignIn(page, emailOf(run, 'cleaner2'), run.password);
    await startOnJobPage(page, o);
    await expect(page.getByRole('button', { name: 'Complete Order' })).toHaveCount(0);
    if (!(await hasPhoto(page, 'After'))) await addPhoto(page, 'After');
    await page.getByRole('button', { name: 'Mark Cash Collected' }).click();
    await page.getByRole('button', { name: 'Yes, cash collected' }).click();
    await expect(page.getByText('Cash collected. The order is marked as paid.')).toBeVisible();
    await addAfterPhotoAndComplete(page);
    o.completed = true;
    saveRun(run);
  });

  test('the pay pages show both jobs, the cash held and the pay in the open period', { tag: ['@E2E-PAYOUT-001', '@E2E-PAYOUT-002', '@E2E-PAYOUT-054'] }, async ({ page }) => {
    for (const key of ['webCard', 'webCash'] as const) test.skip(!run.orders[key]?.completed, `the ${key} job is not complete`);
    await partnerSignIn(page, emailOf(run, 'cleaner2'), run.password);
    await page.goto(`${SITES.partner}/my-pay`);
    const main = page.locator('main');
    await expect(main).toContainText('Cash I hold');
    await expect(main).toContainText(`CZK ${run.orders['webCash'].total.toFixed(2)}`);
    await expect
      .poll(async () => {
        await page.reload();
        await expect(page.getByRole('heading', { name: 'Per-Order Pay' })).toBeVisible();
        return (await main.innerText()).replace(/\s+/g, ' ');
      }, { timeout: 90_000 })
      .toMatch(/Total Orders 2/);
    await page.goto(`${SITES.partner}/dashboard`);
    await expect(page.locator('main')).toContainText('This period, not invoiced yet');
  });
});
