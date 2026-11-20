import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
type Locale = (typeof LOCALES)[number];

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

const I18N_DIR = join(findSolutionDir(), 'Cleansia.App/apps/cleansia.app/src/assets/i18n');

/**
 * While the customer owes any company an amount, no booking is made or confirmed, by card or in
 * cash (owner ruling 2026-10-06, → /product/business-rules#card-guarantee). The copy used to promise
 * that card stayed open.
 */
const BAN_COPY = [
  'api.order.unpaid_receivable',
  'pages.order.unpaid_owed',
  'pages.amount_due.intro',
  'recurring_booking.owed',
];

const DEBT_NAMESPACES = [
  'api.order',
  'pages.order',
  'pages.amount_due',
  'pages.order_detail.recurring_confirm',
  'recurring_booking',
];

const RETIRED_KEYS = ['api.order.cash_unpaid_receivable', 'pages.order.cash_owed'];

const CARD_STAYS_OPEN: Record<Locale, RegExp> = {
  en: /not affected|by card for now|by card now/i,
  cs: /kartou se to netýká|zatím zaplaťte kartou|hned kartou/i,
  sk: /kartou sa to netýka|zatiaľ zaplaťte kartou|hneď kartou/i,
  uk: /карткою це не стосується|поки що оплатіть карткою|карткою зараз/i,
  ru: /картой это не касается|пока оплатите картой|картой сейчас/i,
};

const NAMES_CARD: Record<Locale, RegExp> = {
  en: /\bcard\b/i,
  cs: /kart/i,
  sk: /kart/i,
  uk: /картк/i,
  ru: /карт/i,
};

const NAMES_CASH: Record<Locale, RegExp> = {
  en: /\bcash\b/i,
  cs: /hotovost/i,
  sk: /hotovos/i,
  uk: /готівк/i,
  ru: /наличн/i,
};

function readLocale(locale: Locale): unknown {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8'));
}

function resolveNode(bundle: unknown, key: string): unknown {
  return key
    .split('.')
    .reduce<unknown>(
      (node, segment) =>
        node && typeof node === 'object' ? (node as Record<string, unknown>)[segment] : undefined,
      bundle
    );
}

function leafEntries(node: unknown, prefix: string): [string, string][] {
  if (typeof node === 'string') return [[prefix, node]];
  if (!node || typeof node !== 'object') return [];
  return Object.entries(node as Record<string, unknown>).flatMap(([key, value]) =>
    leafEntries(value, `${prefix}.${key}`)
  );
}

describe('the customer web says an amount owed stops every booking, not only cash', () => {
  it.each(LOCALES)('%s states the ban naming both card and cash', (locale) => {
    const bundle = readLocale(locale);
    for (const key of BAN_COPY) {
      const value = resolveNode(bundle, key);
      expect({ key, isText: typeof value === 'string' && value.trim().length > 0 }).toEqual({ key, isText: true });
      expect({ key, card: NAMES_CARD[locale].test(value as string) }).toEqual({ key, card: true });
      expect({ key, cash: NAMES_CASH[locale].test(value as string) }).toEqual({ key, cash: true });
    }
    expect(
      typeof resolveNode(bundle, 'pages.order_detail.recurring_confirm.owed') === 'string'
    ).toBe(true);
  });

  it.each(LOCALES)('no %s string on a booking or amount-due surface says card stays open while owing', (locale) => {
    const bundle = readLocale(locale);
    const entries = DEBT_NAMESPACES.flatMap((key) => leafEntries(resolveNode(bundle, key), key));

    expect(entries.length).toBeGreaterThan(0);
    expect(
      entries.filter(([, value]) => CARD_STAYS_OPEN[locale].test(value)).map(([key, value]) => `${key}: ${value}`)
    ).toEqual([]);
  });

  it.each(LOCALES)('%s carries none of the retired cash-only keys', (locale) => {
    const bundle = readLocale(locale);
    expect(RETIRED_KEYS.filter((key) => resolveNode(bundle, key) !== undefined)).toEqual([]);
  });
});
