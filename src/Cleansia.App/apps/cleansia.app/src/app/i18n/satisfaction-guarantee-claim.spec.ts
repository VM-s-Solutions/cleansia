import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;

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

const I18N_DIR = join(SOLUTION_DIR, 'Cleansia.App/apps/cleansia.app/src/assets/i18n');

const CATALOG_TEMPLATE = join(
  SOLUTION_DIR,
  'Cleansia.App/libs/cleansia-customer-features/services-catalog/src/lib/services-catalog/services-catalog.component.html'
);

const DISPUTE_LIMITS = join(SOLUTION_DIR, 'Cleansia.Core.Domain/Disputes/DisputeLimits.cs');

function disputeLimit(name: string): number {
  const match = new RegExp(`public\\s+const\\s+int\\s+${name}\\s*=\\s*(\\d+)\\s*;`).exec(
    readFileSync(DISPUTE_LIMITS, 'utf8')
  );
  if (!match) throw new Error(`DisputeLimits.${name} not found — the parser needs updating`);
  return Number(match[1]);
}

const FILING_WINDOW_HOURS = disputeLimit('FilingWindowHours');

/**
 * The platform guarantees no satisfaction: a problem reported within 24 hours of the clean is put
 * right, normally by refunding the part not done (→ /product/business-rules#disputes). The chip
 * states that window instead of a "100 %" promise.
 */
const HUNDRED_PERCENT = /100\s?%/;
const SATISFACTION = /satisf|spokojen|spokojn|задовол|качеств/i;

const RETIRED_KEYS = [
  'pages.services.trust_guarantee',
  'pages.services.satisfaction_guarantee',
  'pages.order.trust_satisfaction',
  'pages.orders.empty_trust_satisfaction',
];

function readLocale(locale: string): unknown {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8'));
}

function values(node: unknown): string[] {
  if (typeof node === 'string') return [node];
  if (node && typeof node === 'object') return Object.values(node).flatMap(values);
  return [];
}

function resolveKey(bundle: unknown, key: string): string | undefined {
  const value = key
    .split('.')
    .reduce<unknown>(
      (node, segment) =>
        node && typeof node === 'object' ? (node as Record<string, unknown>)[segment] : undefined,
      bundle
    );
  return typeof value === 'string' ? value : undefined;
}

describe('the customer app promises no satisfaction guarantee', () => {
  it.each(LOCALES)('no %s value pairs "100 %" with satisfaction', (locale) => {
    const claims = values(readLocale(locale)).filter(
      (value) => HUNDRED_PERCENT.test(value) && SATISFACTION.test(value)
    );

    expect(claims).toEqual([]);
  });

  it.each(LOCALES)('carries none of the retired guarantee keys, in %s', (locale) => {
    const bundle = readLocale(locale);

    expect(RETIRED_KEYS.filter((key) => resolveKey(bundle, key) !== undefined)).toEqual([]);
  });

  it.each(LOCALES)('names the server reporting window, and no other number, on the catalogue chip, in %s', (locale) => {
    const chip = resolveKey(readLocale(locale), 'pages.services.trust_report_window') ?? '';

    expect(chip.match(/\d+/g)).toEqual([String(FILING_WINDOW_HOURS)]);
  });

  it('renders the reporting-window chip and no guarantee key', () => {
    const template = readFileSync(CATALOG_TEMPLATE, 'utf8');

    expect(template).toContain("'pages.services.trust_report_window'");
    expect(RETIRED_KEYS.filter((key) => template.includes(key.split('.').pop() ?? key))).toEqual(
      []
    );
  });
});
