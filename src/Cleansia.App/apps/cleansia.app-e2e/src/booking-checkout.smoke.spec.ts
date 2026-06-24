import { expect, Page, Route, test } from '@playwright/test';

/**
 * Phase-0 customer booking -> checkout-intent smoke.
 *
 * SEAM: Playwright network-stubbing. The customer app boots itself via the
 * Playwright `webServer` (`nx run cleansia.app:serve`) and every `**\/api/**`
 * call is intercepted at the browser boundary and answered with deterministic
 * fixtures. We chose stubbing over a live seeded Customer API because:
 *   - the address step REQUIRES a Mapbox geocode pick (lat/lng); a real Mapbox
 *     call needs a server-side token proxy and is non-deterministic;
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

const MAPBOX_SUGGESTION = {
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
 * Mapbox geocode shape — the autocomplete service maps `features[].text`,
 * `features[].address`, `features[].context[]` and `center` into a suggestion.
 */
function mapboxFeatureBody() {
  return {
    features: [
      {
        place_name: MAPBOX_SUGGESTION.placeName,
        text: 'Vinohradská',
        address: '12',
        center: [MAPBOX_SUGGESTION.longitude, MAPBOX_SUGGESTION.latitude],
        context: [
          { id: 'postcode.1', text: MAPBOX_SUGGESTION.zipCode },
          { id: 'place.1', text: MAPBOX_SUGGESTION.city },
        ],
      },
    ],
  };
}

async function stubBackend(page: Page): Promise<void> {
  // Playwright matches routes in REVERSE registration order, so the catch-all
  // is registered FIRST and the specific fixtures override it. The catch-all
  // means an un-stubbed read can never hang the wizard (there is no live :5003
  // backend in this seam).
  await page.route('**/api/**', (route) => json(route, {}));

  await page.route('**/api/mapbox/geocode**', (route) => json(route, mapboxFeatureBody()));
  await page.route('**/api/Service/GetOverview', (route) => json(route, SERVICES_FIXTURE));
  await page.route('**/api/Package/GetOverview', (route) => json(route, []));
  await page.route('**/api/Country/GetServiced', (route) => json(route, SERVICED_COUNTRIES_FIXTURE));
  await page.route('**/api/Country/GetOverview', (route) => json(route, SERVICED_COUNTRIES_FIXTURE));
  await page.route('**/api/Extra/GetOverview', (route) => json(route, []));
  await page.route('**/api/ServiceCity**', (route) => json(route, SERVICE_CITIES_FIXTURE));
  await page.route('**/api/Order/Quote', (route) => json(route, QUOTE_FIXTURE));
  await page.route('**/api/Payment/CreateOrder', (route) => json(route, CREATE_ORDER_FIXTURE));
}

test.beforeEach(async ({ page, context }) => {
  // Pin English so the role/text locators are stable regardless of CI locale.
  await context.addInitScript(() => {
    window.localStorage.setItem('preferred_language', 'en');
  });
  await stubBackend(page);
});

test('customer can drive the booking wizard to the checkout handoff', async ({ page }) => {
  // ── Land on the customer app and start a booking from the real CTA ──
  await page.goto('/');
  const bookCta = page.getByRole('button', { name: 'Book a Cleaning' });
  await expect(bookCta.first()).toBeVisible();
  await bookCta.first().click();

  // ── Step 0 — services ──
  await expect(page.getByRole('heading', { name: 'Book Your Cleaning' })).toBeVisible();
  await page.getByRole('button', { name: /Standard Home Cleaning/ }).click();

  // `exact` so the wizard's "Next" CTA never collides with the datepicker's
  // "Next Month" nav button on the date step.
  const nextButton = page.getByRole('button', { name: 'Next', exact: true });
  await expect(nextButton).toBeEnabled();
  await nextButton.click();

  // ── Step 1 — contact + address ──
  await expect(page.getByRole('heading', { name: 'Contact Information' })).toBeVisible();
  await page.locator('#wizard-first-name').fill('Jana');
  await page.locator('#wizard-last-name').fill('Novakova');
  await page.locator('#wizard-email').fill('jana@example.com');
  // The telephone wrapper renders a native tel input; target it by type.
  await page.locator('input[type="tel"]').first().fill('+420123456789');

  // Mapbox autocomplete: type >= 3 chars, then pick the stubbed suggestion.
  // Scope to the autocomplete container so we don't grab the header language
  // selector (also a combobox).
  const addressInput = page.locator('.cleansia-address-autocomplete input');
  await addressInput.click();
  // Type char-by-char so PrimeNG's autocomplete fires its `completeMethod`
  // (which drives the stubbed Mapbox search) after its internal debounce.
  await addressInput.pressSequentially('Vinohradska', { delay: 50 });
  const suggestion = page.getByText(MAPBOX_SUGGESTION.placeName);
  await expect(suggestion.first()).toBeVisible();
  await suggestion.first().click();
  // The resolved-address block echoes the picked street verbatim.
  await expect(page.getByText('Vinohradská 12', { exact: true })).toBeVisible();

  await expect(nextButton).toBeEnabled();
  await nextButton.click();

  // ── Step 2 — date & time (a future date keeps every slot available) ──
  await expect(page.getByRole('heading', { name: 'When Should We Come?' })).toBeVisible();
  // PrimeNG inline calendar: advance to next month so every day sits past the
  // min-date, then pick the first selectable cell. A future date keeps the
  // pre-selected 09:00 slot valid (no express/unavailable annotations).
  await page.locator('.p-datepicker-next-button').click();
  await page.locator('.p-datepicker-day:not(.p-disabled)').first().click();
  await expect(nextButton).toBeEnabled();
  await nextButton.click();

  // ── Step 3 — payment (Card is the default selection) ──
  await expect(page.getByRole('heading', { name: 'How Would You Like to Pay?' })).toBeVisible();
  await expect(nextButton).toBeEnabled();
  await nextButton.click();

  // ── Step 4 — summary + place order ──
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
  const payload = request.postDataJSON() as { selectedServiceIds: string[] };
  expect(payload.selectedServiceIds).toContain(SERVICE_ID);

  // The handoff navigation reaches the (stubbed) checkout session URL — proving
  // the wizard created the checkout intent and handed off, without a card charge.
  await expect.poll(() => page.url()).toContain(STRIPE_HANDOFF_URL);
});
