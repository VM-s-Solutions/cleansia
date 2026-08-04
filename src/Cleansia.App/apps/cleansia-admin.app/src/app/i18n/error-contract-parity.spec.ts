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
  'Cleansia.App/apps/cleansia-admin.app/src/assets/i18n'
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

function namespaceKeySet(
  locale: Record<string, unknown>,
  namespace: string
): Set<string> {
  const block = (locale as Record<string, unknown>)[namespace];
  return new Set([...flattenKeys(block)].map((k) => `${namespace}.${k}`));
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

// Admin resolves a backend error through TWO namespaces, and both are live:
//
//   api.*    — the shared HttpErrorInterceptorFn, which admin inherits via
//              COMMON_INTERCEPTORS_FN. It fires for EVERY non-404/403 error and
//              looks up `api.${dotValue}`, falling back to the generic message
//              when the key is absent. This is the canonical path.
//   errors.* — the per-feature XXX_ERROR_KEY_MAP resolvers that several admin
//              features still carry (orders, disputes, refunds, referrals).
//              Back-compat only; new work uses the interceptor path.
//
// A key written under only one of them when the reading path uses the other
// reads as "An error occurred. Please try again." — the exact silent swallow
// this guard exists to catch.
const ADMIN_PAYOUT_SURFACE_ERROR_KEYS: readonly string[] = [
  // AdminEmployeeController: GetEmployeePayoutDetails + RevealEmployeePayoutDetails.
  'payout.not_found',
];

// Reachable from a partner host today, not from an admin one — the admin API
// exposes no payout WRITE endpoint, so the PayoutDetailsValidator chain cannot
// run behind an admin request. Shipped ahead of the admin bank-details editor so
// the strings are never the thing that is missing when it lands; asserted for
// translation but deliberately NOT asserted as admin contract.
const ADMIN_PAYOUT_EDITOR_KEYS: readonly string[] = [
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
];

const PAYOUT_KEYS = [
  ...ADMIN_PAYOUT_SURFACE_ERROR_KEYS,
  ...ADMIN_PAYOUT_EDITOR_KEYS,
];

describe('error-contract parity (admin app)', () => {
  const en = readLocale('en');

  it('every admin payout-surface key exists as a BusinessErrorMessage value', () => {
    const backendValues = parseBusinessErrorValues();
    const orphaned = PAYOUT_KEYS.filter((key) => !backendValues.has(key));
    expect(orphaned).toEqual([]);
  });

  it('every payout key resolves to a real string under api.* in en.json', () => {
    const missing = PAYOUT_KEYS.filter((key) => {
      const value = resolveKey(en, `api.${key}`);
      return !value || value.trim().length === 0;
    });
    expect(missing).toEqual([]);
  });

  it('every payout key has a non-empty translation in all five locales', () => {
    for (const locale of LOCALES) {
      const data = readLocale(locale);
      const missing = PAYOUT_KEYS.filter((key) => {
        const value = resolveKey(data, `api.${key}`);
        return !value || value.trim().length === 0;
      });
      expect({ locale, missing }).toEqual({ locale, missing: [] });
    }
  });

  for (const namespace of ['api', 'errors'] as const) {
    it(`the five locale files have identical ${namespace}.* key sets`, () => {
      const enKeys = namespaceKeySet(en, namespace);
      expect(enKeys.size).toBeGreaterThan(0);
      for (const locale of LOCALES) {
        if (locale === 'en') continue;
        const localeKeys = namespaceKeySet(readLocale(locale), namespace);
        const missingInLocale = [...enKeys].filter((k) => !localeKeys.has(k));
        const extraInLocale = [...localeKeys].filter((k) => !enKeys.has(k));
        expect({ locale, missingInLocale, extraInLocale }).toEqual({
          locale,
          missingInLocale: [],
          extraInLocale: [],
        });
      }
    });

    it(`no ${namespace}.* value is blank in any locale`, () => {
      for (const locale of LOCALES) {
        const data = readLocale(locale);
        const blank = [...namespaceKeySet(data, namespace)].filter((key) => {
          const value = resolveKey(data, key);
          return !value || value.trim().length === 0;
        });
        expect({ locale, blank }).toEqual({ locale, blank: [] });
      }
    });
  }

  it('the generic fallback key resolves in all five locales', () => {
    for (const locale of LOCALES) {
      const data = readLocale(locale);
      const value = resolveKey(data, GENERIC_FALLBACK_KEY);
      expect(typeof value === 'string' && value.trim().length > 0).toBe(true);
    }
  });
});
