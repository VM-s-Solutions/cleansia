import { expect, Page, Route, test } from '@playwright/test';

/**
 * Phase-0 partner login -> accept-job state-transition smoke.
 *
 * SEAM: Playwright network-stubbing, identical to the seam the sibling smokes
 * use. The partner app boots itself via the Playwright
 * `webServer` (`nx run cleansia-partner.app:serve`) and every `**\/api/**` call
 * is intercepted at the browser boundary and answered with deterministic
 * fixtures. We stub rather than boot a live seeded Partner API because it removes
 * the Postgres/seed-script dependency entirely and makes the auth + accept
 * handshake deterministic with zero external services.
 *
 * The REAL login form, the REAL orders ("jobs") page, and the REAL take-order
 * action are driven through the UI — only the network is faked. The smoke drives
 * one accept and asserts the rendered state transition (the job leaves
 * "Available Orders" and appears in "My Orders"); it stops there and touches no
 * money flow.
 *
 * TRANSITION FIXTURE: the orders page loads two lists from the SAME
 * `/api/Order/GetPaged` endpoint, distinguished by query: the "my" list carries
 * `Filter.EmployeeId=<id>`, the "available" list carries
 * `Filter.ExcludeEmployeeId=<id>` (and no `Filter.EmployeeId`). A mutable
 * `accepted` flag flips when the real `TakeOrder` POST fires — before accept the
 * available list returns the seeded job and the my list is empty; after accept
 * the available list is empty and the my list returns the now-assigned job. This
 * mirrors the real backend: taking an order assigns it to the employee, so it
 * leaves the available pool and enters the partner's own list.
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

const SEEDED_ORDER_ID = '11111111-1111-1111-1111-111111111111';
const SEEDED_ORDER_NUMBER = 'AVAIL-0001';

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

// One seeded available job. `orderStatus.value = 2` (Confirmed) + `availableSpots
// > 0` is exactly the take-order action's visibility predicate, so the green
// "Take Order" (`pi pi-check`) action renders on the row.
const SEEDED_AVAILABLE_ORDER = {
  id: SEEDED_ORDER_ID,
  displayOrderNumber: SEEDED_ORDER_NUMBER,
  customerName: 'Petra Customer',
  customerPhone: '+420123456789',
  customerAddress: 'Václavské náměstí 1, Praha',
  cleaningDateTime: new Date(Date.now() + 48 * 60 * 60 * 1000).toISOString(),
  totalPrice: 1500,
  currency: { id: 'czk', code: 'CZK', symbol: 'Kč' },
  paymentStatus: { type: 'PaymentStatus', name: 'Paid', value: 1 },
  orderStatus: { type: 'OrderStatus', name: 'Confirmed', value: 2 },
  requiredEmployees: 1,
  maxEmployees: 1,
  availableSpots: 1,
  assignedEmployeesCount: 0,
  hasAvailableSpots: true,
};

const pageOf = (data: unknown[]) => ({
  data,
  total: data.length,
  pageNumber: 1,
  pageSize: 20,
});

function json(route: Route, body: unknown): Promise<void> {
  return route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
}

async function stubBackend(page: Page): Promise<void> {
  // The accept transition is a single mutable flag flipped by the real TakeOrder
  // POST. Both order lists are derived from it, so the rendered move from
  // Available -> My is driven by the actual accept request, not a timer.
  let accepted = false;

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

  // Both lists come off the same endpoint; the query string tells them apart.
  // `Filter.EmployeeId=` => the partner's own ("my") list; otherwise it is the
  // available pool (it carries `Filter.ExcludeEmployeeId=`, which does NOT
  // contain the substring `Filter.EmployeeId=`).
  await page.route('**/api/Order/GetPaged**', (route) => {
    const isMyList = route.request().url().includes('Filter.EmployeeId=');
    if (isMyList) {
      return json(route, pageOf(accepted ? [SEEDED_AVAILABLE_ORDER] : []));
    }
    return json(route, pageOf(accepted ? [] : [SEEDED_AVAILABLE_ORDER]));
  });

  await page.route('**/api/Order/TakeOrder', (route) => {
    accepted = true;
    return json(route, { orderId: SEEDED_ORDER_ID, employeeId: EMPLOYEE_ID });
  });
}

// Scopes a locator to the `cleansia-section` that contains the given heading,
// so "Available Orders" / "My Orders" row queries never cross-contaminate.
function sectionByHeading(page: Page, heading: RegExp) {
  return page.locator('cleansia-section', {
    has: page.getByRole('heading', { name: heading }),
  });
}

test.beforeEach(async ({ page, context }) => {
  // Pin English so the role/text locators are stable regardless of CI locale.
  await context.addInitScript(() => {
    window.localStorage.setItem('preferred_language', 'en');
  });
  await stubBackend(page);
});

test('partner can log in, accept an available job, and see it move to My Orders', async ({
  page,
}) => {
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

  const availableSection = sectionByHeading(page, /Available Orders/);
  const mySection = sectionByHeading(page, /My Orders/);
  const seededRow = (scope: ReturnType<typeof sectionByHeading>) =>
    scope.locator('tr.table__row', { hasText: SEEDED_ORDER_NUMBER });

  // ── Pre-accept: the seeded job is rendered in Available, absent from My ──
  await expect(seededRow(availableSection)).toBeVisible();
  await expect(seededRow(mySection)).toHaveCount(0);

  // ── Accept it via the REAL take-order action; assert the POST actually fires
  //    (it is what flips the fixture, so the transition is request-driven) ──
  const takeRequest = page.waitForRequest(
    (req) =>
      req.url().includes('/api/Order/TakeOrder') && req.method() === 'POST'
  );
  await seededRow(availableSection).locator('button.action-btn').click();
  const take = await takeRequest;
  expect(take.postDataJSON()).toMatchObject({ orderId: SEEDED_ORDER_ID });

  // ── Post-accept transition (rendered UI, web-first waits): the job leaves the
  //    Available pool and appears in the partner's My Orders list ──
  await expect(seededRow(mySection)).toBeVisible();
  await expect(seededRow(availableSection)).toHaveCount(0);
});
