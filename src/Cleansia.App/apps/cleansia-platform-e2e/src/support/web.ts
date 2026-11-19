import { Browser, expect, Page } from '@playwright/test';
import { call, DEV_ADMIN, list, named, ORDER_STATUS, paceSignIn, poll, Session, signIn, str } from './dev';
import { emailOf, RunState, scratch } from './run';
import { wizardLabels } from './slots';

// The deployed customer and partner webs, and the admin SPA served locally (playwright.config.ts): the deployed
// admin web sits behind a Microsoft sign-in, and the DEV admin API admits only its own origin, so the local server
// proxies /api to it (apps/cleansia-admin.app/proxy.devremote.conf.json).
export const SITES = {
  customer: 'https://customer.dev.cleansia.cz',
  partner: 'https://partner.dev.cleansia.cz',
  admin: 'http://localhost:4201',
} as const;

export async function acceptCookies(page: Page) {
  const ok = page.getByRole('button', { name: 'OK', exact: true });
  if (await ok.waitFor({ state: 'visible', timeout: 5_000 }).then(() => true, () => false)) await ok.click();
}

// A DEV page that hangs fails the sign-in in 90 s, not at the 10-minute test timeout, in a file that sets no navigationTimeout too.
const NAV = { timeout: 90_000 };

// Each web's sign-in is paced with the API sign-ins of the host it posts to (paceSignIn): one rate window per host.
export async function customerSignIn(page: Page, email: string, password: string) {
  await page.goto(`${SITES.customer}/login`, NAV);
  await acceptCookies(page);
  await page.locator('input[type=email]').fill(email);
  await page.locator('input[type=password]').fill(password);
  await paceSignIn('customer');
  await page.getByRole('button', { name: 'Log In', exact: true }).click();
  await page.waitForURL(/\/orders/, NAV);
}

export async function partnerSignIn(page: Page, email: string, password: string) {
  await page.goto(`${SITES.partner}/login`, NAV);
  await acceptCookies(page);
  await page.locator('input[type=email]').fill(email);
  await page.locator('input[type=password]').fill(password);
  await paceSignIn('partner');
  await page.getByRole('button', { name: 'Login', exact: true }).click();
  await page.waitForURL(/\/orders/, NAV);
}

export async function adminSignIn(page: Page, email: string, password: string) {
  await page.goto(`${SITES.admin}/login`, NAV);
  await acceptCookies(page);
  await page.getByRole('textbox', { name: 'Email' }).fill(email);
  await page.getByRole('textbox', { name: 'Password' }).fill(password);
  // The admin host answers 429 to sign-ins in quick succession, the SPA's (proxied to it) and the API's alike.
  await paceSignIn('admin');
  await page.getByRole('button', { name: 'Login', exact: true }).click();
  await page.waitForURL((u) => !u.pathname.startsWith('/login'), NAV);
}

/** A fresh admin SPA context in Prague time whose steps fail in 90 s, signed in as one admin role (the accounts of cachedAdminAs). */
export async function adminSpa(browser: Browser, role: AdminRole, run?: RunState) {
  const ctx = await browser.newContext({ locale: 'en-US', timezoneId: 'Europe/Prague' });
  ctx.setDefaultTimeout(90_000);
  const page = await ctx.newPage();
  const [email, password] = adminAccount(role, run);
  await adminSignIn(page, email, password);
  return { ctx, page };
}

/** The admin SPA's Refunds page, its status filter switched from Needs action to Refunded. */
export async function showRefunded(page: Page) {
  await page.goto(`${SITES.admin}/order-management/refunds`);
  await page.locator('.p-select', { hasText: 'Needs action' }).click();
  await page.getByRole('option', { name: 'Refunded', exact: true }).click();
}

/**
 * Walks the customer web's booking wizard as a signed-in customer with a saved address: Bathroom Cleaning, Normal,
 * the saved address, the slot, the payment type, no Plus, the early-performance tick, Place Order. Resolves with the new
 * order's id, read from where the browser lands: Stripe Checkout (card, left open on the page) or the success page (cash).
 */
export async function placeOrderInWizard(page: Page, run: RunState, slot: Date, payment: 'card' | 'cash') {
  const next = () => page.getByRole('button', { name: 'Continue', exact: true }).click();
  await page.goto(`${SITES.customer}/order`);
  await acceptCookies(page);
  await page.getByRole('button', { name: 'Select service: Bathroom Cleaning' }).click();
  await next();
  await expect(page.getByRole('button', { name: /^Normal/ })).toHaveAttribute('aria-pressed', 'false');
  await next();
  await expect(page.getByText('How clean is your home?')).toBeVisible();
  await page.getByRole('button', { name: /^Normal/ }).click();
  await next();
  await page.getByRole('button', { name: /^Use saved address/ }).click();
  await page.locator('input#wizard-phone').fill('777123456');
  await next();
  const when = wizardLabels(slot);
  const day = page.getByRole('button', { name: when.day, exact: true });
  await page.getByRole('button', { name: 'Next month' }).waitFor();
  if (!(await day.isVisible())) await page.getByRole('button', { name: 'Next month' }).click();
  await day.click();
  await page.getByRole('button', { name: new RegExp(`^${when.part}`) }).click();
  await page.getByRole('button', { name: when.time, exact: true }).click();
  await page.getByRole('button', { name: 'I will be home' }).click();
  await next();
  await page.getByRole('button', { name: payment === 'card' ? /^Card online/ : /^Cash on the day/ }).click();
  await next();
  await page.getByRole('button', { name: 'Continue without Plus' }).click();
  await page.getByRole('textbox', { name: /note|cat is home/i }).fill(`E2E run ${run.runId} web`);
  await page.locator('input#wizard-early-performance').check();
  await page.getByRole('button', { name: 'Place Order' }).click();
  await page.waitForURL(/checkout\.stripe\.com|\/checkout\/success/, { timeout: 90_000 });
  if (payment === 'cash') return new URL(page.url()).searchParams.get('orderId') ?? '';
  const heading = await page.getByRole('heading', { name: /Cleaning Order #/ }).innerText({ timeout: 60_000 });
  return heading.replace(/.*#/, '').trim();
}

export type AdminRole = 'admin' | 'manager' | 'support' | 'accountant';
const adminSessions: Partial<Record<AdminRole, { session: Session; at: number }>> = {};

/**
 * One admin API session per role per spec file: the admin host throttles sign-ins (HTTP 429), and a session lapses in
 * about 15 minutes. 'admin' is the published DEV administrator; the run's Manager, Support and Accountant (people/10)
 * sign in with the run's password, so they need the run.
 */
export async function cachedAdminAs(role: AdminRole, run?: RunState): Promise<Session> {
  const cached = adminSessions[role];
  if (cached && Date.now() - cached.at < 8 * 60_000) return cached.session;
  const [email, password] = adminAccount(role, run);
  for (let attempt = 1; ; attempt++) {
    try {
      const session = await signIn('admin', email, password);
      adminSessions[role] = { session, at: Date.now() };
      return session;
    } catch (e) {
      if (attempt >= 5 || !String(e).includes('HTTP 429')) throw e;
      await new Promise((r) => setTimeout(r, 30_000));
    }
  }
}

/** The e-mail and password of an admin role: the published DEV administrator, or the run's Manager, Support or Accountant. */
function adminAccount(role: AdminRole, run?: RunState): [string, string] {
  if (role === 'admin') return [DEV_ADMIN.email, DEV_ADMIN.password];
  if (!run) throw new Error(`the ${role} needs the run: the account is e2e.<run>.${role}@example.com`);
  return [emailOf(run, role), run.password];
}

/** The published DEV administrator's cached session. */
export const cachedAdmin = () => cachedAdminAs('admin');

/** Whether DEV already reads the order Cancelled: the unfilled sweep cancels an order nobody took 30 minutes past its slot. */
export async function cancelledOnDev(orderId: string) {
  return named((await call(await cachedAdmin(), 'GET', `/api/AdminOrder/details/${orderId}`)).body, 'orderStatus', ORDER_STATUS) === 'Cancelled';
}

// Who works which job of a run: the API half's cleaner and the web half's.
const CREWS = [
  { role: 'cleaner', employee: 'employeeId', jobs: ['cash', 'card'] },
  { role: 'cleaner2', employee: 'employee2Id', jobs: ['webCard', 'webCash'] },
] as const;

/**
 * The one gate before either half presses Close now (E2E_NO_CLOSE_NOW=1 and E2E_SCRATCH=1 always skip it): every job the run took is complete, the open period has
 * started, and each cleaner's pay rows for those jobs are in it (a queue writes them seconds after the completion,
 * and a row written after the close lands in the next period). Resolves with the open period's id, or the reason to skip.
 */
export async function closeNowGate(run: RunState): Promise<{ periodId?: string; skip?: string }> {
  // Phase 2 keeps the period DEV closes at night: no stage presses Close now while it runs, nor a scratch run ever.
  if (process.env['E2E_NO_CLOSE_NOW'] === '1') return { skip: 'E2E_NO_CLOSE_NOW=1: Close now is not pressed on this run' };
  if (scratch()) return { skip: 'E2E_SCRATCH=1: a scratch run never presses Close now' };
  const taken = Object.entries(run.orders).filter(([, o]) => o.taken);
  const open = taken.filter(([, o]) => !o.completed).map(([k]) => k);
  if (!taken.length || open.length) return { skip: open.length ? `the ${open.join(', ')} job(s) of run ${run.runId} are not complete` : `run ${run.runId} took no job` };
  const periods = list((await call(await cachedAdmin(), 'GET', '/api/AdminPayPeriod/get-paged?Offset=0&Limit=20')).body, 'data');
  const current = periods.find((p) => str(p, 'status') === 'Open' || str(p, 'status.name') === 'Open');
  const periodId = str(current, 'id');
  if (!periodId) return { skip: 'DEV has no open pay period' };
  if (str(current, 'startDate') > new Date().toISOString().slice(0, 10)) return { skip: `the open period starts ${str(current, 'startDate')}; Close now ends a period that has started` };
  for (const crew of CREWS) {
    const jobs = crew.jobs.filter((k) => run.orders[k]?.taken).map((k) => run.orders[k].id);
    if (!jobs.length) continue;
    const s = await signIn('partner', emailOf(run, crew.role), run.password);
    const rows = await poll(
      async () => list((await call(s, 'GET', `/api/EmployeePayroll/GetPeriodPays?EmployeeId=${run[crew.employee]}&PayPeriodId=${periodId}`)).body, 'orderPays').map((r) => str(r, 'orderId')),
      (ids) => jobs.every((j) => ids.includes(j)),
    );
    const missing = jobs.filter((j) => !rows.includes(j));
    if (missing.length) return { skip: `the ${crew.role}'s pay rows for ${missing.join(', ')} are not in the open period ${periodId}` };
  }
  return { periodId };
}
