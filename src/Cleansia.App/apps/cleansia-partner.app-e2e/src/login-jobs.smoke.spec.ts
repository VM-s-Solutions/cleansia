import { expect, Page, Route, test } from '@playwright/test';

/**
 * T-0281 — Phase-0 partner login -> jobs (orders) landing smoke.
 *
 * SEAM: Playwright network-stubbing, identical to the seam T-0271 chose for the
 * customer booking smoke. The partner app boots itself via the Playwright
 * `webServer` (`nx run cleansia-partner.app:serve`) and every `**\/api/**` call
 * is intercepted at the browser boundary and answered with deterministic
 * fixtures. We stub rather than boot a live seeded Partner API because it removes
 * the Postgres/seed-script dependency entirely and makes the auth handshake
 * deterministic with zero external services.
 *
 * The REAL login form + the REAL orders ("jobs") page are driven through the UI —
 * only the network is faked, so the dead-CTA / broken-route / guard-misconfig
 * class of bug is still caught. The smoke stops at the authenticated jobs landing
 * (the partner-app critical entry path); it accepts/takes no job and touches no
 * money flow.
 *
 * AUTH MODEL: the real auth/refresh tokens are HttpOnly cookies; the JS layer
 * only persists `csrfToken`, `refreshTokenExp` and `role` to localStorage, and
 * the route guards gate on those (`isLoggedIn()` = csrf present + refresh-exp in
 * the future). So the login stub returns a `JwtTokenResponse` carrying a CSRF
 * token, a future refresh-token expiry and the partner role — `setSession`
 * persists them and the `authGuard` on `/orders` passes.
 */

const EMPLOYEE_ID = '99999999-9999-9999-9999-999999999999';
const FUTURE_EXP = new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString();

const LOGIN_FIXTURE = {
  token: 'stub-access-token',
  isEmailConfirmed: true,
  hasAdminAccess: false,
  userId: EMPLOYEE_ID,
  email: 'cleaner@example.com',
  refreshToken: 'stub-refresh-token',
  refreshTokenExpiresAt: FUTURE_EXP,
  csrfToken: 'stub-csrf-token',
  role: 'Employee',
};

// A fully-onboarded employee so the app-shell registration-lock overlay never
// covers the page (lock shows only for an incomplete, non-null status) and the
// orders facade's `getCurrentEmployee().id` read resolves, letting it load both
// order lists.
const REGISTRATION_COMPLETE_FIXTURE = {
  areDocumentsUploaded: true,
  hasCompletedProfile: true,
  hasSetAvailability: true,
  missingFields: [],
  contractStatus: 4, // ContractStatus.Approved
  rejectionReason: null,
};

const CURRENT_EMPLOYEE_FIXTURE = {
  id: EMPLOYEE_ID,
  firstName: 'Jan',
  lastName: 'Cleaner',
  email: 'cleaner@example.com',
};

const EMPTY_PAGE = { data: [], total: 0, pageNumber: 1, pageSize: 20 };

function json(route: Route, body: unknown): Promise<void> {
  return route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
}

async function stubBackend(page: Page): Promise<void> {
  // Playwright matches routes in REVERSE registration order: the catch-all is
  // registered FIRST and specific fixtures override it. The catch-all means an
  // un-stubbed read returns 200 `{}` (never a 401) so the error interceptor's
  // refresh/logout path never fires and no read can hang the app.
  await page.route('**/api/**', (route) => json(route, {}));

  await page.route('**/api/Auth/Login', (route) => json(route, LOGIN_FIXTURE));
  await page.route('**/api/Employee/CheckCurrentEmployee**', (route) =>
    json(route, REGISTRATION_COMPLETE_FIXTURE)
  );
  await page.route('**/api/Employee/GetCurrentEmployee**', (route) =>
    json(route, CURRENT_EMPLOYEE_FIXTURE)
  );
  await page.route('**/api/Order/GetPaged**', (route) => json(route, EMPTY_PAGE));
}

test.beforeEach(async ({ page, context }) => {
  // Pin English so the role/text locators are stable regardless of CI locale.
  await context.addInitScript(() => {
    window.localStorage.setItem('preferred_language', 'en');
  });
  await stubBackend(page);
});

test('partner can log in and reach the jobs (orders) page', async ({ page }) => {
  // ── Land on the login screen ──
  await page.goto('/login');
  const emailInput = page.locator('input[type="email"]').first();
  await expect(emailInput).toBeVisible();

  // ── Fill the REAL login form and submit ──
  await emailInput.fill('cleaner@example.com');
  await page.locator('input[type="password"]').first().fill('Sup3rSecret!');

  const loginRequest = page.waitForRequest('**/api/Auth/Login');
  await page.getByRole('button', { name: 'Login', exact: true }).click();

  // The login POST fires with the typed credentials — proving the form wired
  // through to the auth client, not a routing shortcut.
  const request = await loginRequest;
  expect(request.method()).toBe('POST');
  const payload = request.postDataJSON() as { email: string };
  expect(payload.email).toBe('cleaner@example.com');

  // ── Authenticated landing: the wizard navigates to /orders and the shell
  //    renders the sidebar (rendered only when `isLoggedIn()` is true) ──
  await expect.poll(() => page.url()).toContain('/orders');
  await expect(page.locator('nav.sidebar-nav')).toBeVisible();

  // The real jobs page renders its section headings — the partner-app main
  // authenticated screen is reached, not a blank route or a bounce to login.
  // Scoped to the `heading` role (the `<h2>` section titles, which also carry a
  // count badge) so the locator never collides with the empty-state table cell
  // ("No available orders").
  await expect(
    page.getByRole('heading', { name: /Available Orders/ })
  ).toBeVisible();
  await expect(
    page.getByRole('heading', { name: /My Orders/ })
  ).toBeVisible();
});
