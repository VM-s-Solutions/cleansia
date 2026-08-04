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
  'Cleansia.App/apps/cleansia-partner.app/src/assets/i18n'
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

// Partner-surface error contract: the BusinessErrorMessage dot-values the
// Partner API (Cleansia.Web.Partner) can return and the shared
// HttpErrorInterceptor resolves under the api.* namespace. Derived
// mechanically — every constant referenced by a feature class that a
// Cleansia.Web.Partner controller dispatches — so it can be re-derived rather
// than remembered. Customer/admin-only codes are excluded by design.
const PARTNER_SURFACE_ERROR_KEYS: readonly string[] = [
  // Auth — partner login / confirm / reset / refresh
  'auth.insufficient_privileges',
  'auth.invalid_confirmation_code',
  'auth.invalid_google_token',
  'auth.invalid_refresh_token',
  'auth.invalid_reset_token',
  'auth.refresh_token_reused',
  'auth.same_reset_password',
  'auth.too_many_attempts',
  // Shared field-level rules
  'common.max_length',
  'common.required',
  // Order lifecycle the cleaner drives: take → on the way → start → cash → complete
  'order.after_photos.required',
  'order.already_cancelled',
  'order.already_completed',
  'order.card_payment_already_settled',
  'order.card_payment_in_progress',
  'order.card_payment_unverified',
  'order.cash_already_collected',
  'order.cash_not_collected',
  'order.completion_notes.too_long',
  'order.employee_already_assigned',
  'order.employee_already_has_order_in_progress',
  'order.employee_not_assigned',
  'order.no_available_spots',
  'order.not_confirmed',
  'order.not_found',
  'order.not_in_progress',
  'order.not_takeable',
  'order.payment_not_confirmed',
  'order.time_conflict',
  'order.weekly_limit_reached',
  // Employee profile + documents
  'employee.not_allowed_to_update',
  'employee.not_approved',
  'employee.not_found',
  'employee.profile_incomplete',
  'employee_document.not_found',
  'employee_document.not_owned',
  'employee_document.unauthorized',
  'general.not_found',
  // Order photos + document uploads
  'file.invalid_file_type',
  'file.required',
  'file.size_exceeded',
  // Profile address / country
  'country.not_existing_id',
  'country.not_serviced',
  'dispute.max_length_exceeded',
  'language.not_supported',
  'validation.date_must_be_in_past',
  'validation.invalid_age',
  'validation.invalid_availability_format',
  'validation.invalid_date',
  'validation.invalid_password',
  // User account
  'user.email_confirmed',
  'user.existing_email',
  'user.existing_phone_number',
  'user.not_allowed_to_update',
  'user.not_existing_email',
  'user.not_existing_id',
  'user.not_found',
  // GDPR consents
  'gdpr.consent_already_granted',
  'gdpr.consent_not_found',
  // Payout destination — UpdateBankDetails runs the whole PayoutDetailsValidator
  // chain, so every arm of it is reachable from the cleaner's bank-details form;
  // GetMyPayoutDetails returns payout.not_found.
  'payout.not_found',
  'validation.payout.account_number_required',
  'validation.payout.country_not_supported',
  'validation.payout.iban_country_mismatch',
  'validation.payout.iban_mismatch',
  'validation.payout.invalid_account_number',
  'validation.payout.invalid_account_prefix',
  'validation.payout.invalid_bank_code',
  'validation.payout.invalid_iban',
  'validation.payout.invalid_swift',
  'validation.payout.looks_like_card',
  'validation.payout.scheme_not_supported',
  'validation.payout.swift_required',
  // Payroll — invoices, pay periods, pay calculation
  'payroll.employee_not_assigned',
  'payroll.invoice.not_found',
  'payroll.no_active_period',
  'payroll.no_pay_configuration',
  'payroll.pay.already_calculated',
  'payroll.pay_period.not_found',
  'company.not_found',
  'currency.invalid',
  'receipt.not_found',
  // Pay configuration
  'pay_config.already_exists',
  'pay_config.base_pay_negative',
  'pay_config.cannot_have_both',
  'pay_config.distance_rate_negative',
  'pay_config.extra_per_bathroom_negative',
  'pay_config.extra_per_room_negative',
  'pay_config.has_order_pays',
  'pay_config.maximum_less_than_minimum',
  'pay_config.maximum_pay_negative',
  'pay_config.minimum_pay_negative',
  'pay_config.not_found',
  'pay_config.service_or_package_required',
  // Stripe webhook payload guards
  'payment.json_payload_required',
  'payment.stripe_signature_required',
];

// Contract keys that have no partner translation yet. Every entry here is a
// cleaner who gets "An error occurred. Please try again." instead of the real
// reason, so the list may only ever shrink: translate the key in all five
// locales and delete its line. A key that is BOTH listed here and translated
// fails the ratchet below.
const PENDING_TRANSLATION: readonly string[] = [];

const TRANSLATED_CONTRACT_KEYS = PARTNER_SURFACE_ERROR_KEYS.filter(
  (key) => !PENDING_TRANSLATION.includes(key)
);

describe('error-contract parity (partner app)', () => {
  const en = readLocale('en');

  it('every partner-surface key exists as a BusinessErrorMessage value', () => {
    const backendValues = parseBusinessErrorValues();
    const orphaned = PARTNER_SURFACE_ERROR_KEYS.filter(
      (key) => !backendValues.has(key)
    );
    expect(orphaned).toEqual([]);
  });

  it('every partner-surface key resolves to a real string under api.* in en.json', () => {
    const missing = TRANSLATED_CONTRACT_KEYS.filter((key) => {
      const value = resolveKey(en, `api.${key}`);
      return !value || value.trim().length === 0;
    });
    expect(missing).toEqual([]);
  });

  it('every partner-surface key has a non-empty translation in all five locales', () => {
    for (const locale of LOCALES) {
      const data = readLocale(locale);
      const missing = TRANSLATED_CONTRACT_KEYS.filter((key) => {
        const value = resolveKey(data, `api.${key}`);
        return !value || value.trim().length === 0;
      });
      expect({ locale, missing }).toEqual({ locale, missing: [] });
    }
  });

  it('the five locale files have identical api.* key sets', () => {
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

  it('the pending list only holds keys that really are untranslated', () => {
    const alreadyTranslated = PENDING_TRANSLATION.filter((key) => {
      const value = resolveKey(en, `api.${key}`);
      return !!value && value.trim().length > 0;
    });
    expect(alreadyTranslated).toEqual([]);
  });

  it('the pending list holds only real contract keys', () => {
    const stale = PENDING_TRANSLATION.filter(
      (key) => !PARTNER_SURFACE_ERROR_KEYS.includes(key)
    );
    expect(stale).toEqual([]);
  });

  it('the take-order refusal reasons a racing cleaner hits are all translated', () => {
    // Every arm of the TakeOrder validator chain, in its order. A missing web key renders
    // "An error occurred. Please try again." — indistinguishable from a 500 — so a partner-facing
    // refusal that is not in this list is a cleaner clicking the same dead job forever.
    const takeRefusals = [
      'order.not_found',
      'order.already_cancelled',
      'order.already_completed',
      'order.not_takeable',
      'order.no_available_spots',
      'employee.profile_incomplete',
      'employee.not_approved',
      'order.employee_already_assigned',
      'order.weekly_limit_reached',
      'order.time_conflict',
    ];
    for (const locale of LOCALES) {
      const data = readLocale(locale);
      const missing = takeRefusals.filter((key) => {
        const value = resolveKey(data, `api.${key}`);
        return !value || value.trim().length === 0;
      });
      expect({ locale, missing }).toEqual({ locale, missing: [] });
    }
  });

  it('the generic fallback key resolves in all five locales', () => {
    for (const locale of LOCALES) {
      const data = readLocale(locale);
      const value = resolveKey(data, GENERIC_FALLBACK_KEY);
      expect(typeof value === 'string' && value.trim().length > 0).toBe(true);
    }
  });
});
