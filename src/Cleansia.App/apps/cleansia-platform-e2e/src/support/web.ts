import { Page } from '@playwright/test';
import { RunState } from './run';
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

export async function customerSignIn(page: Page, email: string, password: string) {
  await page.goto(`${SITES.customer}/login`);
  await acceptCookies(page);
  await page.locator('input[type=email]').fill(email);
  await page.locator('input[type=password]').fill(password);
  await page.getByRole('button', { name: 'Log In', exact: true }).click();
  await page.waitForURL(/\/orders/);
}

export async function partnerSignIn(page: Page, email: string, password: string) {
  await page.goto(`${SITES.partner}/login`);
  await acceptCookies(page);
  await page.locator('input[type=email]').fill(email);
  await page.locator('input[type=password]').fill(password);
  await page.getByRole('button', { name: 'Login', exact: true }).click();
  await page.waitForURL(/\/orders/);
}

export async function adminSignIn(page: Page, email: string, password: string) {
  await page.goto(`${SITES.admin}/login`);
  await acceptCookies(page);
  await page.getByRole('textbox', { name: 'Email' }).fill(email);
  await page.getByRole('textbox', { name: 'Password' }).fill(password);
  await page.getByRole('button', { name: 'Login', exact: true }).click();
  await page.waitForURL((u) => !u.pathname.startsWith('/login'));
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
