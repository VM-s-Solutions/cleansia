import { expect, Page, Route, test } from '@playwright/test';

/**
 * T-0281 — Phase-0 admin login -> dashboard (employee-management home) smoke.
 *
 * SEAM: Playwright network-stubbing, identical to the seam T-0271 chose for the
 * customer booking smoke. The admin app boots itself via the Playwright
 * `webServer` (`nx run cleansia-admin.app:serve`) and every `**\/api/**` call is
 * intercepted at the browser boundary and answered with deterministic fixtures.
 * We stub rather than boot a live seeded Admin API because it removes the
 * Postgres/seed-script dependency entirely and makes the auth handshake
 * deterministic with zero external services.
 *
 * The REAL login form + the REAL admin home (employee-management, the `''` route
 * target) are driven through the UI — only the network is faked, so the
 * dead-CTA / broken-route / guard-misconfig class of bug is still caught. The
 * smoke stops at the authenticated landing; it manages nothing and touches no
 * money flow.
 *
 * AUTH MODEL: the real auth/refresh tokens are HttpOnly cookies; the JS layer
 * only persists `csrfToken`, `refreshTokenExp` and `role` to localStorage, and
 * `adminGuard` gates on those (`isLoggedIn()` = csrf present + refresh-exp in
 * the future, AND `isAdminOrEditor()` = role Administrator/Employee). So the
 * login stub returns a `JwtTokenResponse` with `hasAdminAccess: true`, a CSRF
 * token, a future refresh-token expiry and the Administrator role — `setSession`
 * persists them and the guard on `/employee-management` passes.
 */

const FUTURE_EXP = new Date(Date.now() + 24 * 60 * 60 * 1000).toISOString();

const LOGIN_FIXTURE = {
  token: 'stub-access-token',
  isEmailConfirmed: true,
  hasAdminAccess: true,
  userId: '88888888-8888-8888-8888-888888888888',
  email: 'admin@example.com',
  refreshToken: 'stub-refresh-token',
  refreshTokenExpiresAt: FUTURE_EXP,
  csrfToken: 'stub-csrf-token',
  role: 'Administrator',
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
  // Catch-all FIRST (Playwright matches in REVERSE registration order): an
  // un-stubbed read returns 200 `{}` (never a 401) so the error interceptor's
  // refresh/logout path never fires and no read can hang the app.
  await page.route('**/api/**', (route) => json(route, {}));

  await page.route('**/api/AdminAuth/Login', (route) => json(route, LOGIN_FIXTURE));
  await page.route('**/api/AdminEmployee/get-paged**', (route) =>
    json(route, EMPTY_PAGE)
  );
}

test.beforeEach(async ({ page, context }) => {
  // Pin English so the role/text locators are stable regardless of CI locale.
  await context.addInitScript(() => {
    window.localStorage.setItem('preferred_language', 'en');
  });
  await stubBackend(page);
});

test('admin can log in and land on the admin home', async ({ page }) => {
  // ── Land on the login screen ──
  await page.goto('/login');
  const emailInput = page.locator('input[type="email"]').first();
  await expect(emailInput).toBeVisible();

  // ── Fill the REAL login form and submit ──
  await emailInput.fill('admin@example.com');
  await page.locator('input[type="password"]').first().fill('Sup3rSecret!');

  const loginRequest = page.waitForRequest('**/api/AdminAuth/Login');
  await page.getByRole('button', { name: 'Login', exact: true }).click();

  // The login POST fires with the typed credentials — proving the form wired
  // through to the auth client, not a routing shortcut.
  const request = await loginRequest;
  expect(request.method()).toBe('POST');
  const payload = request.postDataJSON() as { email: string };
  expect(payload.email).toBe('admin@example.com');

  // ── Authenticated landing: the facade navigates to /employee-management and
  //    the shell renders the sidebar (rendered only when `isLoggedIn()`) ──
  await expect.poll(() => page.url()).toContain('/employee-management');
  await expect(page.locator('nav.sidebar-nav')).toBeVisible();

  // The real admin home renders its heading — the admin dashboard/home is
  // reached, not a blank route, the /unauthorized bounce, or a login loop.
  await expect(
    page.getByRole('heading', { name: 'Employee Management' })
  ).toBeVisible();
});
