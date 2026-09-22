import { readFileSync } from 'fs';
import { join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
type Locale = (typeof LOCALES)[number];

const I18N_DIR = join(__dirname, '../../assets/i18n');

/**
 * The loyalty rate (`Currency.LoyaltyPointsDivisor`) is authored per currency by the admin, the
 * tier floor (`LoyaltyTierConfig.MinimumOrderAmountForDiscount`) is a platform-default-currency
 * number that applies to no other, and the silver threshold is admin-editable too. Copy that
 * states "1 point per 10 CZK" or "over 1000 CZK" is right for exactly one seed of one market —
 * so the copy states no such figure, and the rewards page prints the floor from the tier the
 * server sent, labelled with the default currency code.
 *
 * The no-show credit is the one money figure copy may name; `check-booking-policy-parity.mjs`
 * guards it, and it lives under `pages.home.rules`, outside every namespace here.
 */
const NAMESPACES: string[][] = [
  ['auth', 'below'],
  ['pages', 'rewards'],
  ['loyalty', 'perks'],
];

const RATE_CLAIM = /\b10\s*(CZK|Kč)/;
const NBSP = String.fromCharCode(0xa0);
const FLOOR_CLAIM = new RegExp(`\\b1[ ,${NBSP}]?000\\s*(CZK|Kč)`);
const ANY_CURRENCY_LITERAL = /\b(CZK|EUR|PLN)\b|Kč|€|zł/;

const FLOOR_KEYS = ['discount_min_order', 'discount_basic', 'no_discount_yet'] as const;

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as Record<
    string,
    unknown
  >;
}

function block(locale: Record<string, unknown>, path: string[]): Record<string, unknown> {
  let node: Record<string, unknown> = locale;
  for (const segment of path) {
    node = (node[segment] ?? {}) as Record<string, unknown>;
  }
  return node;
}

function leafEntries(node: Record<string, unknown>, prefix = ''): [string, string][] {
  return Object.entries(node).flatMap(([key, value]) => {
    const path = prefix ? `${prefix}.${key}` : key;
    if (typeof value === 'string') return [[path, value] as [string, string]];
    if (value && typeof value === 'object') {
      return leafEntries(value as Record<string, unknown>, path);
    }
    return [];
  });
}

describe('customer copy states no loyalty rate or tier floor as a fixed sum', () => {
  it('never names the rate or the floor in crowns, in any locale', () => {
    for (const locale of LOCALES) {
      const bundle = readLocale(locale);
      const offending = NAMESPACES.flatMap((path) =>
        leafEntries(block(bundle, path), path.join('.')).filter(
          ([, value]) => RATE_CLAIM.test(value) || FLOOR_CLAIM.test(value)
        )
      ).map(([key, value]) => `${key}: ${value}`);

      expect({ locale, offending }).toEqual({ locale, offending: [] });
    }
  });

  it('names no currency at all in these namespaces — the code arrives with the amount', () => {
    for (const locale of LOCALES) {
      const bundle = readLocale(locale);
      const offending = NAMESPACES.flatMap((path) =>
        leafEntries(block(bundle, path), path.join('.')).filter(([, value]) =>
          ANY_CURRENCY_LITERAL.test(value.replace(/\{\{[^}]*\}\}/g, ' '))
        )
      ).map(([key, value]) => `${key}: ${value}`);

      expect({ locale, offending }).toEqual({ locale, offending: [] });
    }
  });

  it('states the earn rule without a number, since the divisor is per currency', () => {
    for (const locale of LOCALES) {
      const title = block(readLocale(locale), ['pages', 'rewards', 'how'])['rate_title'];

      expect({ locale, title, hasDigit: /\d/.test(String(title)) }).toEqual({
        locale,
        title,
        hasDigit: false,
      });
    }
  });

  it('carries the three floor lines the rewards page interpolates, non-empty in every locale', () => {
    for (const locale of LOCALES) {
      const rewards = block(readLocale(locale), ['pages', 'rewards']);
      const missing = FLOOR_KEYS.filter(
        (key) => typeof rewards[key] !== 'string' || !(rewards[key] as string).trim()
      );

      expect({ locale, missing }).toEqual({ locale, missing: [] });
      expect(rewards['discount_min_order']).toContain('{{minAmount}}');
      expect(rewards['discount_min_order']).toContain('{{percent}}');
    }
  });

  it('the five locales carry identical key sets in these namespaces', () => {
    for (const path of NAMESPACES) {
      const enKeys = leafEntries(block(readLocale('en'), path))
        .map(([key]) => key)
        .sort();
      for (const locale of LOCALES) {
        if (locale === 'en') continue;
        const localeKeys = leafEntries(block(readLocale(locale), path))
          .map(([key]) => key)
          .sort();
        expect({ path, locale, keys: localeKeys }).toEqual({ path, locale, keys: enKeys });
      }
    }
  });
});
