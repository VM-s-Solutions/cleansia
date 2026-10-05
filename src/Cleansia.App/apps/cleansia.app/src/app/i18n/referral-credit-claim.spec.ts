import { readFileSync } from 'fs';
import { join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
type Locale = (typeof LOCALES)[number];

const I18N_DIR = join(__dirname, '../../assets/i18n');

/**
 * A qualified referral credits each side the order currency's `Currency.ReferralCredit` — money,
 * authored per currency, and no longer 150 tier points (owner ruling 2026-10-04). So the copy
 * carries `{{amount}}` where it states the credit, the page formats the market's figure into it,
 * and a market with no figure reads a sibling `_no_amount` line that names no amount and promises
 * no credit. -> /product/business-rules#money-constants
 */
const NAMESPACES: string[][] = [
  ['auth', 'register', 'referral'],
  ['pages', 'rewards', 'referral'],
];

const WITH_AMOUNT = [
  'auth.register.referral.dialog_helper',
  'auth.register.referral.dialog_success',
  'auth.register.referral.dialog_success_named',
  'pages.rewards.referral.section_title',
  'pages.rewards.referral.subtitle',
];

const WITHOUT_AMOUNT = [
  'auth.register.referral.dialog_helper_no_amount',
  'auth.register.referral.dialog_success_no_amount',
  'auth.register.referral.dialog_success_named_no_amount',
  'pages.rewards.referral.section_title_no_amount',
  'pages.rewards.referral.subtitle_no_amount',
];

const POINTS_CLAIM: Record<Locale, RegExp> = {
  en: /\bpoints?\b/i,
  cs: /\bbod/i,
  sk: /\bbod/i,
  uk: /бал(?!ан)/i,
  ru: /балл/i,
};

const REFERRAL_WORD: Record<Locale, RegExp> = {
  en: /referr/i,
  cs: /doporuč/i,
  sk: /odporúč/i,
  uk: /запрош|рефера/i,
  ru: /приглаш|рефера/i,
};

const CURRENCY_WORDS = /\b(CZK|EUR|PLN|GBP|USD)\b|Kč|€|zł|£|\$/;
const CREDIT_WORD: Record<Locale, RegExp> = {
  en: /credit/i,
  cs: /kredit/i,
  sk: /kredit/i,
  uk: /кредит/i,
  ru: /кредит/i,
};

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as Record<string, unknown>;
}

function block(bundle: Record<string, unknown>, path: string[]): Record<string, unknown> {
  let node: Record<string, unknown> = bundle;
  for (const segment of path) {
    node = (node[segment] ?? {}) as Record<string, unknown>;
  }
  return node;
}

function leafEntries(node: Record<string, unknown>, prefix: string): [string, string][] {
  return Object.entries(node).flatMap(([key, value]) => {
    const path = `${prefix}.${key}`;
    if (typeof value === 'string') return [[path, value] as [string, string]];
    if (value && typeof value === 'object') return leafEntries(value as Record<string, unknown>, path);
    return [];
  });
}

function value(bundle: Record<string, unknown>, dotted: string): string {
  const leaf = dotted.split('.').reduce<unknown>(
    (node, segment) => (node && typeof node === 'object' ? (node as Record<string, unknown>)[segment] : undefined),
    bundle,
  );
  return typeof leaf === 'string' ? leaf : '';
}

const withoutPlaceholders = (text: string): string => text.replace(/\{\{\s*\w+\s*\}\}/g, ' ');

describe('the referral copy states the market credit, never a fixed figure or points', () => {
  it.each(LOCALES)('carries {{amount}} wherever it states the credit, in %s', (locale) => {
    const bundle = readLocale(locale);
    const missing = WITH_AMOUNT.filter((key) => !value(bundle, key).includes('{{amount}}'));

    expect({ locale, missing }).toEqual({ locale, missing: [] });
  });

  it.each(LOCALES)('bakes no figure and names no currency in the referral namespaces, in %s', (locale) => {
    const bundle = readLocale(locale);
    const offending = NAMESPACES.flatMap((path) => leafEntries(block(bundle, path), path.join('.')))
      .filter(([, text]) => /\d/.test(withoutPlaceholders(text)) || CURRENCY_WORDS.test(withoutPlaceholders(text)))
      .map(([key, text]) => `${key}: ${text}`);

    expect({ locale, offending }).toEqual({ locale, offending: [] });
  });

  it.each(LOCALES)('promises no points for a referral, in %s', (locale) => {
    const bundle = readLocale(locale);
    const offending = NAMESPACES.flatMap((path) => leafEntries(block(bundle, path), path.join('.')))
      .filter(([, text]) => POINTS_CLAIM[locale].test(text))
      .map(([key, text]) => `${key}: ${text}`);
    const how = Object.keys(block(bundle, ['pages', 'rewards', 'how'])).filter((key) => key.startsWith('referral'));

    expect({ locale, offending, how }).toEqual({ locale, offending: [], how: [] });
  });

  // The points history keeps the old referral rows, but a referral earns no points any more, so its
  // lead does not name referrals among what earns them.
  it.each(LOCALES)('the points history lead names no referral among what earns points, in %s', (locale) => {
    const lead = value(readLocale(locale), 'pages.rewards.history_lead');

    expect({ locale, written: lead.trim().length > 0, referral: REFERRAL_WORD[locale].test(lead) }).toEqual({
      locale,
      written: true,
      referral: false,
    });
  });

  it.each(LOCALES)('names no amount and promises no credit in the no-figure lines, in %s', (locale) => {
    const bundle = readLocale(locale);
    const offending = WITHOUT_AMOUNT.filter((key) => {
      const text = value(bundle, key);
      return !text.trim() || text.includes('{{amount}}') || CREDIT_WORD[locale].test(text);
    });

    expect({ locale, offending }).toEqual({ locale, offending: [] });
  });

  it('the five locales carry identical key sets in the referral namespaces', () => {
    for (const path of NAMESPACES) {
      const keysOf = (locale: Locale) =>
        leafEntries(block(readLocale(locale), path), path.join('.'))
          .map(([key]) => key)
          .sort();
      const enKeys = keysOf('en');
      for (const locale of LOCALES) {
        expect({ path, locale, keys: keysOf(locale) }).toEqual({ path, locale, keys: enKeys });
      }
    }
  });
});
