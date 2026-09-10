import { expect, Page, Route, test } from '@playwright/test';

/**
 * Phase-0 customer booking -> checkout-intent smoke.
 *
 * SEAM: Playwright network-stubbing. The customer app boots itself via the
 * Playwright `webServer` (`nx run cleansia.app:serve`) and every `**\/api/**`
 * call is intercepted at the browser boundary and answered with deterministic
 * fixtures. We chose stubbing over a live seeded Customer API because:
 *   - the address step REQUIRES a geocode pick (lat/lng); a real lookup needs a
 *     server-side token and is non-deterministic;
 *   - the checkout step hands off to Stripe — stubbing the create-order response
 *     with a synthetic `stripeSessionId` lets us assert the handoff without any
 *     Stripe dependency, which is exactly where this smoke must STOP (no card
 *     charge). The redirect target is same-origin and intercepted so the browser
 *     never leaves the app.
 * The REAL wizard UI is driven through every step — only the network is faked,
 * so the dead-CTA / broken-step class of bug is still caught.
 */

const CZ_COUNTRY_ID = '11111111-1111-1111-1111-111111111111';
const SERVICE_ID = '22222222-2222-2222-2222-222222222222';
const CURRENCY_ID = '33333333-3333-3333-3333-333333333333';
const STRIPE_HANDOFF_URL = '/checkout/stripe-session-stub';

const SERVICES_FIXTURE = [
  {
    id: SERVICE_ID,
    name: 'Standard Home Cleaning',
    description: 'A thorough clean of your home.',
    category: {
      id: '44444444-4444-4444-4444-444444444444',
      slug: 'home',
      name: 'Home',
      description: 'Home cleaning',
      displayOrder: 1,
      translations: {},
    },
    basePrice: 1200,
    perRoomPrice: 0,
    translations: {},
  },
];

const SERVICED_COUNTRIES_FIXTURE = [
  { id: CZ_COUNTRY_ID, isoCode: 'CZ', name: 'Czechia', translations: {} },
];

const SERVICE_CITIES_FIXTURE = [
  { id: '55555555-5555-5555-5555-555555555555', name: 'Praha', countryId: CZ_COUNTRY_ID },
];

const ADDRESS_SUGGESTION = {
  placeName: 'Vinohradská 12, 120 00 Praha, Česko',
  street: 'Vinohradská 12',
  city: 'Praha',
  zipCode: '120 00',
  latitude: 50.0775,
  longitude: 14.4378,
};

const QUOTE_FIXTURE = {
  totalPrice: 1200,
  finalPriceAfterDiscount: 1200,
  originalSubtotal: 1200,
  appliedDiscountSource: 0,
  tierDiscountAmount: 0,
  membershipDiscountAmount: 0,
  tierDiscountMinOrderAmount: null,
  currencyId: CURRENCY_ID,
  currencyCode: 'CZK',
  servicesSubtotal: 1200,
  packagesSubtotal: 0,
  extrasSubtotal: 0,
  expressSurchargeApplied: false,
  expressSurchargeAmount: 0,
  exchangeRate: 1,
  estimatedDurationMinutes: 120,
  requiredEmployees: 1,
  expressSurchargeWaivedByMembership: false,
  creditBalance: 0,
  creditMaxShareOfOrder: 0,
  lines: [],
};

/**
 * The Plus step prints every number from the plan catalogue and dereferences
 * `plans()[0]` for its perk list, so this one has to be a real row rather than
 * the catch-all's `{}` — a null plans array is a template crash, not a step
 * with nothing to offer.
 */
const MEMBERSHIP_PLANS_FIXTURE = [
  {
    code: 'PLUS_MONTHLY',
    name: 'Cleansia Plus Monthly',
    price: 299,
    monthlyEquivalentPrice: 299,
    billingInterval: 1,
    discountPercentage: 10,
    freeCancellationWindowHours: 24,
    allowsExpressUpgrade: true,
    expressUpgradesPerMonth: 2,
    trialPeriodDays: 14,
    savingsPercentVsMonthly: 0,
  },
];

const PLUS_SAVINGS_FIXTURE = {
  wouldSaveAmount: 120,
  wouldPayTotal: 1080,
  currentTotal: 1200,
  currencyCode: 'CZK',
  planCode: 'PLUS_MONTHLY',
};

const CREATE_ORDER_FIXTURE = {
  id: '66666666-6666-6666-6666-666666666666',
  confirmationCode: 'CLS-SMOKE-001',
  stripeSessionId: STRIPE_HANDOFF_URL,
};

function json(route: Route, body: unknown): Promise<void> {
  return route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(body),
  });
}

/**
 * Address search is a platform endpoint now (`/api/AddressSearch/search`),
 * called through the generated client — the browser never holds the provider
 * token. The response is the client's `SearchAddressesResponse`, which the
 * app's port maps into the picker's suggestion shape.
 */
function addressSearchBody() {
  return { suggestions: [ADDRESS_SUGGESTION] };
}

async function stubBackend(page: Page): Promise<void> {
  // Playwright matches routes in REVERSE registration order, so the catch-all
  // is registered FIRST and the specific fixtures override it. The catch-all
  // means an un-stubbed read can never hang the wizard (there is no live :5003
  // backend in this seam).
  await page.route('**/api/**', (route) => json(route, {}));

  await page.route('**/api/AddressSearch/search**', (route) => json(route, addressSearchBody()));
  await page.route('**/api/Service/GetOverview', (route) => json(route, SERVICES_FIXTURE));
  await page.route('**/api/Package/GetOverview', (route) => json(route, []));
  await page.route('**/api/Country/GetServiced', (route) => json(route, SERVICED_COUNTRIES_FIXTURE));
  await page.route('**/api/Country/GetOverview', (route) => json(route, SERVICED_COUNTRIES_FIXTURE));
  await page.route('**/api/Extra/GetOverview', (route) => json(route, []));
  await page.route('**/api/ServiceCity**', (route) => json(route, SERVICE_CITIES_FIXTURE));
  await page.route('**/api/Order/Quote', (route) => json(route, QUOTE_FIXTURE));
  await page.route('**/api/Order/QuotePlusSavings', (route) => json(route, PLUS_SAVINGS_FIXTURE));
  await page.route('**/api/Membership/GetPlans', (route) => json(route, MEMBERSHIP_PLANS_FIXTURE));
  await page.route('**/api/Payment/CreateOrder', (route) => json(route, CREATE_ORDER_FIXTURE));
}

test.beforeEach(async ({ page, context }) => {
  // Pin English so the role/text locators are stable regardless of CI locale.
  await context.addInitScript(() => {
    window.localStorage.setItem('preferred_language', 'en');
  });
  await stubBackend(page);
});

test('room selectors stay beside the summary on desktop and lead the form on mobile', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.route('**/api/Service/GetOverview', (route) => json(route,
    Array.from({ length: 15 }, (_, index) => ({
      ...SERVICES_FIXTURE[0],
      id: `${SERVICE_ID.slice(0, -2)}${String(index).padStart(2, '0')}`,
      name: `Cleaning service ${index + 1}`,
    })),
  ));
  await page.goto('/order');

  const rooms = page.getByRole('group', { name: 'Number of rooms', exact: true });
  const bathrooms = page.getByRole('group', { name: 'Number of bathrooms', exact: true });
  const setRooms = page.getByRole('button', { name: 'Set number of rooms: 4', exact: true });
  const setBathrooms = page.getByRole('button', { name: 'Set number of bathrooms: 2', exact: true });

  await expect(rooms).toHaveCount(1);
  await expect(bathrooms).toHaveCount(1);
  await expect(rooms).toBeInViewport({ ratio: 1 });
  await expect(bathrooms).toBeInViewport({ ratio: 1 });
  await setRooms.click();
  await setBathrooms.click();
  await expect(setRooms).toHaveAttribute('aria-pressed', 'true');
  await expect(setBathrooms).toHaveAttribute('aria-pressed', 'true');

  await page.evaluate(() => window.scrollTo(0, 700));
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBeGreaterThan(600);
  await expect(rooms).toBeInViewport({ ratio: 1 });
  await expect(bathrooms).toBeInViewport({ ratio: 1 });
  const summary = page.locator('.cl-wiz__summary');
  expect(await summary.evaluate((element) => getComputedStyle(element).position)).toBe('sticky');
  await page.getByRole('button', { name: 'Continue', exact: true }).scrollIntoViewIfNeeded();
  await expect(page.getByRole('button', { name: 'Continue', exact: true })).toBeInViewport();

  await page.setViewportSize({ width: 390, height: 844 });
  await page.evaluate(() => window.scrollTo(0, 0));
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);
  await expect(rooms).toHaveCount(1);
  await expect(bathrooms).toHaveCount(1);
  await expect(rooms).toBeInViewport({ ratio: 1 });
  await expect(bathrooms).toBeInViewport({ ratio: 1 });
  await expect(setRooms).toHaveAttribute('aria-pressed', 'true');
  await expect(setBathrooms).toHaveAttribute('aria-pressed', 'true');
  const countsBottom = await bathrooms.evaluate((element) => element.getBoundingClientRect().bottom);
  const firstServiceTop = await page.locator('.cl-wiz__svc').first().evaluate((element) => element.getBoundingClientRect().top);
  expect(countsBottom).toBeLessThan(firstServiceTop);
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
});

test('customer can drive the booking wizard to the checkout handoff', async ({ page }) => {
  // ── Land on the customer app and start a booking from the real CTA ──
  await page.goto('/');
  // A LINK, not a button, and the copy is "Book a clean" — the design pass made both CTAs
  // `<a routerLink="/order">`, which is the right role for something that navigates. `.first()`
  // because the hero and the closing band both render one.
  const bookCta = page.getByRole('link', { name: 'Book a clean' });
  await expect(bookCta.first()).toBeVisible();
  await bookCta.first().click();

  // One button carries the wizard forward from every step, in the price summary
  // beside the panel. `exact` because the Plus step's own "Continue without
  // Plus" would otherwise match it — and that one skips a step rather than
  // completing it.
  const continueButton = page.getByRole('button', { name: 'Continue', exact: true });

  // ── Step 0 — services ──
  await expect(page.getByRole('heading', { name: 'Choose Your Services' })).toBeVisible();
  // Each service row's add control is named for the service it adds, so the
  // accessible name pins us to THIS row rather than to whatever the catalogue
  // happens to list first.
  const addService = page.getByRole('button', { name: 'Select service: Standard Home Cleaning' });
  await addService.click();
  // The button reports selection through `aria-pressed`; asserting it here is
  // what makes the Continue below a real transition rather than a click that
  // happened to land on an already-satisfied step.
  await expect(addService).toHaveAttribute('aria-pressed', 'true');
  await continueButton.click();

  // ── Step 1 — address + contact ──
  await expect(page.getByRole('heading', { name: 'Where should we come?' })).toBeVisible();
  await page.locator('#wizard-first-name').fill('Jana');
  await page.locator('#wizard-last-name').fill('Novakova');
  await page.locator('#wizard-email').fill('jana@example.com');
  // The telephone wrapper renders a native tel input; target it by type.
  await page.locator('input[type="tel"]').first().fill('+420123456789');

  // Address autocomplete: type >= 3 chars, then pick the stubbed suggestion.
  // Scope to the autocomplete container so we don't grab the header language
  // selector (also a combobox).
  const addressInput = page.locator('.cleansia-address-autocomplete input');
  await addressInput.click();
  // Type char-by-char so PrimeNG's autocomplete fires its `completeMethod`
  // (which drives the stubbed lookup) after its internal debounce.
  await addressInput.pressSequentially('Vinohradska', { delay: 50 });
  const suggestion = page.getByText(ADDRESS_SUGGESTION.placeName);
  await expect(suggestion.first()).toBeVisible();
  await suggestion.first().click();
  // The resolved-address block echoes the picked street verbatim. Only a PICK
  // sets lat/lng, and only lat/lng lets the step advance — so this is the
  // assertion that says the geocode actually landed.
  await expect(page.getByText('Vinohradská 12', { exact: true })).toBeVisible();

  await continueButton.click();

  // ── Step 2 — date & time ──
  await expect(page.getByRole('heading', { name: 'When should we come?' })).toBeVisible();
  // The calendar is the wizard's own grid, not PrimeNG's: month nav by its
  // aria-label, then the first cell the component left enabled. Advancing a
  // month puts every day past the min-date, so the pick is never a same-day
  // slot whose availability depends on the wall clock.
  await page.getByRole('button', { name: 'Next month' }).click();
  const firstBookableDay = page.locator('button.cl-wiz__cal-day:not([disabled])').first();
  await firstBookableDay.click();
  await expect(firstBookableDay).toHaveAttribute('aria-pressed', 'true');
  // The slot is chosen explicitly rather than left to the component's snap:
  // `hasValidTime()` gates this step, and an unavailable slot is disabled here,
  // so picking an enabled chip is the same thing the customer does.
  const quarterHourTime = page.locator('button.cl-wiz__time:not([disabled])').filter({ hasText: '10:15' });
  await quarterHourTime.click();
  await expect(quarterHourTime).toHaveAttribute('aria-pressed', 'true');
  await continueButton.click();

  // ── Step 3 — payment (Card is the default selection) ──
  await expect(page.getByRole('heading', { name: 'How Would You Like to Pay?' })).toBeVisible();
  // Card is what the wizard defaults to and what routes the submit through
  // `Payment/CreateOrder` — the Stripe handoff asserted at the end only exists
  // on this branch, so the default is worth stating rather than assuming.
  await expect(page.getByRole('button', { name: /Card online/ })).toHaveAttribute(
    'aria-pressed',
    'true'
  );
  await continueButton.click();

  // ── Step 4 — Cleansia Plus (declined, which is the smoke's path) ──
  await expect(page.getByRole('heading', { name: 'Add Cleansia Plus?' })).toBeVisible();
  // The step's real way past. Not the shared Continue: the decline card is the
  // only exit that leaves the basket without a subscription, and a smoke that
  // took the other one would be booking a membership.
  await page.getByRole('button', { name: 'Continue without Plus' }).click();

  // ── Step 5 — review + place order ──
  await expect(page.getByRole('heading', { name: 'Check your order' })).toBeVisible();
  // A guest has no consent on record, so the tick is asked for and the
  // place-order button refuses without it.
  await page.getByRole('checkbox', { name: /I agree to the terms/ }).check();

  const placeOrder = page.getByRole('button', { name: 'Place Order' });
  await expect(placeOrder).toBeVisible();

  // The checkout handoff is `window.location.href = stripeSessionId`. We assert
  // the create-order request fires AND the browser attempts the handoff
  // navigation — stopping at the (stubbed, same-origin) Stripe boundary.
  const createOrderRequest = page.waitForRequest('**/api/Payment/CreateOrder');
  await page.route(`**${STRIPE_HANDOFF_URL}`, (route) =>
    route.fulfill({ status: 200, contentType: 'text/html', body: '<html><body>stripe-handoff</body></html>' })
  );

  await placeOrder.click();

  const request = await createOrderRequest;
  expect(request.method()).toBe('POST');
  const payload = request.postDataJSON() as { selectedServiceIds: string[]; cleaningDate: string };
  expect(payload.selectedServiceIds).toContain(SERVICE_ID);
  expect(new Date(payload.cleaningDate).getMinutes()).toBe(15);

  // The handoff navigation reaches the (stubbed) checkout session URL — proving
  // the wizard created the checkout intent and handed off, without a card charge.
  await expect.poll(() => page.url()).toContain(STRIPE_HANDOFF_URL);
});
