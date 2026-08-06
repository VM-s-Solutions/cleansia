import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
type Locale = (typeof LOCALES)[number];

const GENERIC_FALLBACK_KEY = 'api.common.error_occurred';

function findSolutionDir(): string {
  let dir = process.cwd();
  for (let i = 0; i < 12; i++) {
    if (existsSync(join(dir, 'Cleansia.Api.sln'))) return dir;
    const parent = dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }
  throw new Error('Could not locate the solution dir (Cleansia.Api.sln)');
}

const SOLUTION_DIR = findSolutionDir();

const BUSINESS_ERROR_MESSAGE_PATH = join(
  SOLUTION_DIR,
  'Cleansia.Core.AppServices/Common/BusinessErrorMessage.cs'
);

const I18N_DIR = join(
  SOLUTION_DIR,
  'Cleansia.App/apps/cleansia.app/src/assets/i18n'
);

function localePath(locale: Locale): string {
  return join(I18N_DIR, `${locale}.json`);
}

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(readFileSync(localePath(locale), 'utf8')) as Record<
    string,
    unknown
  >;
}

function parseBusinessErrorValues(): Set<string> {
  const source = readFileSync(BUSINESS_ERROR_MESSAGE_PATH, 'utf8');
  const values = new Set<string>();
  const regex = /public const string \w+ = "([^"]+)";/g;
  let match: RegExpExecArray | null;
  while ((match = regex.exec(source)) !== null) {
    values.add(match[1]);
  }
  return values;
}

function flattenKeys(obj: unknown, prefix = ''): Set<string> {
  const keys = new Set<string>();
  if (obj && typeof obj === 'object' && !Array.isArray(obj)) {
    for (const [key, value] of Object.entries(obj)) {
      const path = prefix ? `${prefix}.${key}` : key;
      if (value && typeof value === 'object' && !Array.isArray(value)) {
        for (const nested of flattenKeys(value, path)) keys.add(nested);
      } else {
        keys.add(path);
      }
    }
  }
  return keys;
}

function apiKeySet(locale: Record<string, unknown>): Set<string> {
  const api = (locale as { api?: unknown }).api;
  return new Set([...flattenKeys(api)].map((k) => `api.${k}`));
}

function resolveKey(
  locale: Record<string, unknown>,
  dottedKey: string
): string | undefined {
  let node: unknown = locale;
  for (const segment of dottedKey.split('.')) {
    if (node && typeof node === 'object' && segment in node) {
      node = (node as Record<string, unknown>)[segment];
    } else {
      return undefined;
    }
  }
  return typeof node === 'string' ? node : undefined;
}

// Customer-surface error contract: BusinessErrorMessage dot-values a Customer
// API endpoint can return and the global HttpErrorInterceptor resolves under
// the api.* namespace. Admin/partner-only orphan codes are excluded by design.
const CUSTOMER_SURFACE_ERROR_KEYS: readonly string[] = [
  // Auth — customer login / register / confirm / refresh
  'auth.invalid_confirmation_code',
  'auth.invalid_password_format',
  'auth.account_locked',
  'auth.too_many_attempts',
  'auth.current_password_invalid',
  'auth.insufficient_privileges',
  // Password reset: the forgot-password form posts a 6-digit code, so both
  // arms of the ChangePassword validator are reachable from the customer app.
  'auth.invalid_reset_token',
  'auth.same_reset_password',
  // Refresh: RefreshToken splits its failures in two — the reuse/theft signal
  // gets its own key, and expiry, an unknown token or an audience mismatch all
  // land on invalid_refresh_token.
  'auth.invalid_refresh_token',
  'auth.refresh_token_reused',
  // Provider mismatch: password login against an account that belongs to a
  // social provider. LoginValidator names the provider the account ACTUALLY
  // uses, so all four arms are reachable from the customer login form and each
  // needs its own string — external_type_error is the neutral fallback that any
  // AuthenticationType added later lands on.
  'auth.google_type_error',
  'auth.apple_type_error',
  'auth.external_type_error',
  'auth.internal_type_error',
  // Social token verification: the customer API rejects an id_token it cannot
  // verify. invalid_google_token is reachable from the existing Google button;
  // invalid_apple_token ships ahead of the Apple button so the string is never
  // the thing that is missing when it lands.
  'auth.invalid_google_token',
  'auth.invalid_apple_token',
  // User — register / profile
  'user.email_confirmed',
  'user.existing_email',
  'user.not_existing_email',
  'user.existing_phone_number',
  'user.not_found',
  // Profile save carries the display-language preference, so the
  // UpdateCurrentUser language rule is reachable from the customer app.
  'language.not_supported',
  // Order — booking / cancel / review / lifecycle the customer can hit
  'order.cleaning_date.future',
  'order.cleaning_date.below_lead_time',
  'order.already_cancelled',
  'order.already_completed',
  'order.in_progress_cannot_cancel',
  'order.invalid_status_transition',
  'order.cancellation_window_closed',
  'order.address_exactly_one_required',
  'order.empty',
  'order.selected_package.invalid',
  'order.selected_services.invalid',
  'order.not_found',
  'order.no_available_spots',
  'order.weekly_limit_reached',
  'order.time_conflict',
  'order.total_price.positive',
  'order.total_price.not_match',
  'order.payment_gateway_unavailable',
  // Emitted by CreatePaymentIntent + ConfirmRecurringOrder, both dispatched by
  // Cleansia.Web.Customer. No web component calls either yet — the web card path
  // is createOrder's hosted-Checkout redirect — but the generated client carries
  // createPaymentIntent, so this ships ahead of the caller for the same reason
  // auth.invalid_apple_token does: on the money path, the string must never be
  // the thing that is missing when the button lands.
  'order.payment.already_paid',
  'order.creation_failed',
  'order.not_completed',
  'order.review.already_exists',
  'order.review.rating_invalid',
  'order.note.content_required',
  'order.issue.description_required',
  // Address — saved addresses + order address
  'address.not_owned_by_user',
  'address.label_required',
  'address.already_exists',
  'address.invalid_length',
  'address.mapbox_coords_required',
  // Service area — booking geo gate
  'country.not_serviced',
  'country.required',
  'city.not_serviced',
  // Dispute — customer dispute flow
  'dispute.not_found',
  'dispute.already_exists',
  'dispute.invalid_refund_amount',
  'dispute.not_owned_by_user',
  'dispute.max_length_exceeded',
  // File — dispute evidence upload
  'file.content_type_doesnt_match',
  'file.invalid_file_type',
  'file.size_exceeded',
  'file.count_exceeded',
  'file.count_too_few',
  'file.required',
  'file.size_exceeded_10mb',
  'file.type_not_allowed',
  // Receipt — download
  'receipt.not_found',
  'receipt.generation_failed',
  // GDPR — export / delete / consent
  'gdpr.export_failed',
  'gdpr.deletion_failed',
  'gdpr.deletion_already_pending',
  'gdpr.deletion_blocked_by_order',
  'gdpr.deletion_blocked_by_invoice',
  'gdpr.consent_not_found',
  'gdpr.consent_already_granted',
  // Promo — checkout promo apply
  'promo.not_found',
  'promo.expired',
  'promo.not_yet_valid',
  'promo.inactive',
  'promo.global_limit_reached',
  'promo.per_user_limit_reached',
  'promo.below_minimum_order_amount',
  'promo.currency_mismatch',
  // Referral — validate
  'referral.not_found',
  'referral.self_referral',
  'referral.already_referred',
  'referral.inactive',
  // Membership — subscribe / cancel / swap
  'membership.plan.not_found',
  'membership.already_active',
  'membership.not_found',
  'membership.not_owned_by_user',
  'membership.stripe_customer_required',
  'membership.swap_same_plan',
  // The CreateOrder waiver rule runs BEFORE the price rule and has its own code
  // precisely so this never renders as the generic "the price changed".
  'membership.express_waiver.no_longer_available',
  // Recurring booking — create / manage
  'recurring_booking.not_found',
  'recurring_booking.not_owned_by_user',
  'recurring_booking.saved_address_not_found',
  'recurring_booking.no_services_or_packages',
  'recurring_booking.starts_on_in_past',
  'recurring_booking.ends_on_before_start',
  // Device — push registration
  'device.invalid_platform',
  'device.not_found',
];

describe('error-contract parity (customer app, EP-1/EP-2/DA-7)', () => {
  const en = readLocale('en');

  it('AC4 contract: every customer-surface key exists as a BusinessErrorMessage value', () => {
    const backendValues = parseBusinessErrorValues();
    const orphaned = CUSTOMER_SURFACE_ERROR_KEYS.filter(
      (key) => !backendValues.has(key)
    );
    expect(orphaned).toEqual([]);
  });

  it('AC1/AC3: every customer-surface key resolves to a real string under api.* in en.json', () => {
    const missing = CUSTOMER_SURFACE_ERROR_KEYS.filter((key) => {
      const value = resolveKey(en, `api.${key}`);
      return !value || value.trim().length === 0;
    });
    expect(missing).toEqual([]);
  });

  it('AC2: the five locale files have identical api.* key sets', () => {
    const enApiKeys = apiKeySet(en);
    for (const locale of LOCALES) {
      if (locale === 'en') continue;
      const localeApiKeys = apiKeySet(readLocale(locale));
      const missingInLocale = [...enApiKeys].filter(
        (k) => !localeApiKeys.has(k)
      );
      const extraInLocale = [...localeApiKeys].filter((k) => !enApiKeys.has(k));
      expect({ locale, missingInLocale, extraInLocale }).toEqual({
        locale,
        missingInLocale: [],
        extraInLocale: [],
      });
    }
  });

  it('AC2: every customer-surface key has a non-empty translation in all five locales', () => {
    for (const locale of LOCALES) {
      const data = readLocale(locale);
      const missing = CUSTOMER_SURFACE_ERROR_KEYS.filter((key) => {
        const value = resolveKey(data, `api.${key}`);
        return !value || value.trim().length === 0;
      });
      expect({ locale, missing }).toEqual({ locale, missing: [] });
    }
  });

  it('AC4: the generic fallback key resolves in all five locales', () => {
    for (const locale of LOCALES) {
      const data = readLocale(locale);
      const value = resolveKey(data, GENERIC_FALLBACK_KEY);
      expect(typeof value === 'string' && value.trim().length > 0).toBe(true);
    }
  });
});
