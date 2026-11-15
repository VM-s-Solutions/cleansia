import { expect, Page, test } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { at } from '../support/dev';
import { emailOf, flag, loadRun } from '../support/run';
import { SITES } from '../support/web';

// Stage 'people', web half 1: the sign-in refusal in every locale of the three webs
// (XCUT-019), and the applicant people/10 registered, still unconfirmed, sent to /confirm-email (AUTH-034). Needs no
// role account, so a scratch run validates it.
// Not serial: every test has its own gate, so one failure does not skip the tests after it (one worker keeps the order).
// A DEV page that hangs fails the step in 90 s, not at the 10-minute test timeout (support/web.ts waits on navigation).
test.use({ timezoneId: 'Europe/Prague', navigationTimeout: 90_000, actionTimeout: 90_000 });
const run = loadRun();

const LOCALES = { en: 'en-US', cs: 'cs-CZ', sk: 'sk-SK', uk: 'uk-UA', ru: 'ru-RU' } as const;
const APP_DIR = { customer: 'cleansia.app', partner: 'cleansia-partner.app', admin: 'cleansia-admin.app' } as const;
type Site = keyof typeof APP_DIR;

/** A string of one web's catalogue in one language, read from the repo the webs are built from. */
function text(site: Site, lang: string, key: string): string {
  const catalogue = JSON.parse(readFileSync(join(__dirname, '..', '..', '..', APP_DIR[site], 'src', 'assets', 'i18n', `${lang}.json`), 'utf8'));
  return String(at(catalogue, key) ?? '');
}

// The 'auth' window admits 10 anonymous sign-ins a minute per IP on each host (RateLimitPolicies.cs).
const lastAttempt: Partial<Record<Site, number>> = {};
async function paced(site: Site) {
  const wait = (lastAttempt[site] ?? 0) + 7_000 - Date.now();
  if (wait > 0) await new Promise((r) => setTimeout(r, wait));
  lastAttempt[site] = Date.now();
}

/** The cookie notice by its class: its button is worded in the page's language. */
async function dismissNotice(page: Page) {
  const ok = page.locator('.cleansia-cookie-consent__btn--accept');
  if (await ok.waitFor({ state: 'visible', timeout: 5_000 }).then(() => true, () => false)) await ok.click();
}

async function submitSignIn(page: Page, site: Site, email: string, password: string) {
  await page.goto(`${SITES[site]}/login`);
  await dismissNotice(page);
  await page.locator('form input[type=email]').fill(email);
  await page.locator('form input[type=password]').fill(password);
  await paced(site);
  await page.locator('form button[type=submit]').click();
}

test('a sign-in refusal reads in the page\'s language on the customer, partner and admin webs, in all five', { tag: ['@E2E-XCUT-019'] }, async ({ browser }) => {
  test.setTimeout(8 * 60_000);
  for (const [lang, locale] of Object.entries(LOCALES)) {
    for (const site of Object.keys(APP_DIR) as Site[]) {
      const expected = text(site, lang, 'api.validation.invalid_password');
      const generic = text(site, lang, 'api.common.error_occurred');
      expect(expected, `${APP_DIR[site]} ${lang}.json has no api.validation.invalid_password`).not.toBe('');
      // A fresh context per locale: the stored language choice would otherwise outrank the browser's.
      const ctx = await browser.newContext({ locale, timezoneId: 'Europe/Prague' });
      ctx.setDefaultTimeout(90_000);
      const page = await ctx.newPage();
      await submitSignIn(page, site, emailOf(run, 'nobody'), `${run.password}x`);
      await expect(page.getByText(expected).first(), `${site} in ${lang}`).toBeVisible();
      const body = await page.locator('body').innerText();
      expect(body, `${site} in ${lang} showed the raw key`).not.toContain('api.validation.invalid_password');
      if (generic) expect(body, `${site} in ${lang} showed the generic error`).not.toContain(generic);
      await expect(page).toHaveURL(/\/login/);
      await ctx.close();
    }
  }
});

test('an applicant who has not confirmed the e-mail is sent to confirm it by the partner web', { tag: ['@E2E-AUTH-034'] }, async ({ page }) => {
  // The account people/10 registered; people/30's dispatch confirms it, after which this step cannot be shown again.
  test.skip(!flag(run, 'people.registered'), `people/10 has not registered the applicant of run ${run.runId}`);
  test.skip(!!flag(run, 'people.confirmed'), `the applicant of run ${run.runId} was confirmed at ${flag(run, 'people.confirmed')} (people/30)`);
  await submitSignIn(page, 'partner', emailOf(run, 'applicant'), run.password);
  await expect(page).toHaveURL(/\/confirm-email/);
});
