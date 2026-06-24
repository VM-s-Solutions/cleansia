import { expect, Page, Route, test } from '@playwright/test';

/**
 * Phase-0 admin login -> seeded-row landing smoke.
 *
 * SEAM: Playwright network-stubbing, identical to the seam the sibling smokes
 * use. The admin app boots itself via the Playwright
 * `webServer` (`nx run cleansia-admin.app:serve`) and every `**\/api/**` call is
 * intercepted at the browser boundary and answered with deterministic fixtures.
 * We stub rather than boot a live seeded Admin API because it removes the
 * Postgres/seed-script dependency entirely and makes the auth handshake
 * deterministic with zero external services.
 *
 * The REAL login form + the REAL admin home (employee-management, the `''` route
 * target) are driven through the UI — only the network is faked. The landing
 * oversight surface is seeded with ONE deterministic employee row, so the smoke
 * asserts a real rendered data row, not just an authed-but-empty landing. It
 * manages nothing and touches no money flow.
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

const SEEDED_EMPLOYEE_NAME = 'Jan Novak';
const SEEDED_EMPLOYEE_EMAIL = 'jan.novak@example.com';

// One seeded employee so the landing oversight surface (the employee-management
// table) renders a real data row, not the empty state. `contractStatus` is the
// enum NAME string (the admin list DTO serialises it as a string); 'Approved'
// maps to a translated badge label via the contract-status helper.
const SEEDED_EMPLOYEE = {
  id: '77777777-7777-7777-7777-777777777777',
  firstName: 'Jan',
  lastName: 'Novak',
  email: SEEDED_EMPLOYEE_EMAIL,
  phoneNumber: '+420123456789',
  contractStatus: 'Approved',
  averageRating: 4.8,
  complaintsCount: 0,
  nationalityName: 'Czech',
  createdAt: new Date('2026-01-15T10:00:00.000Z').toISOString(),
  isProfileComplete: true,
};

const SEEDED_EMPLOYEE_PAGE = {
  data: [SEEDED_EMPLOYEE],
  total: 1,
  pageNumber: 1,
  pageSize: 20,
};

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
    json(route, SEEDED_EMPLOYEE_PAGE)
  );
}

test.beforeEach(async ({ page, context }) => {
  // Pin English so the role/text locators are stable regardless of CI locale.
  await context.addInitScript(() => {
    window.localStorage.setItem('preferred_language', 'en');
  });
  await stubBackend(page);
});

test('admin can log in and land on the admin home with a seeded data row', async ({
  page,
}) => {
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

  // The real admin home renders its heading — the oversight surface is reached,
  // not a blank route, the /unauthorized bounce, or a login loop.
  await expect(
    page.getByRole('heading', { name: 'Employee Management' })
  ).toBeVisible();

  // ── Seeded-row assertion: the employee-management table renders
  //    a REAL data row with the seeded employee's name + email (web-first wait),
  //    not the empty state. The name cell joins firstName + lastName. ──
  const seededRow = page.locator('tr.table__row', {
    hasText: SEEDED_EMPLOYEE_NAME,
  });
  await expect(seededRow).toBeVisible();
  await expect(seededRow).toContainText(SEEDED_EMPLOYEE_EMAIL);
});
