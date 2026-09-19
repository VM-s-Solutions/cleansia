import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
type Locale = (typeof LOCALES)[number];

const PAGE_ROOT = 'pages.reports';

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
const I18N_DIR = join(SOLUTION_DIR, 'Cleansia.App/apps/cleansia-admin.app/src/assets/i18n');
// Read off disk rather than imported: the feature lib is lazy-loaded by the app, and a static import
// from the app project is the boundary the module-boundaries gate refuses.
const FEATURE_DIR = join(SOLUTION_DIR, 'Cleansia.App/libs/cleansia-admin-features/reports/src/lib/reports');
const TEMPLATE = readFileSync(join(FEATURE_DIR, 'reports.component.html'), 'utf8');
const COMPONENT = readFileSync(join(FEATURE_DIR, 'reports.component.ts'), 'utf8');

const HEADLINE_KEY = `${PAGE_ROOT}.net_revenue`;
const BREAKDOWN_KEY = `${PAGE_ROOT}.gross_and_refunds`;
const BREAKDOWN_PLACEHOLDERS = ['gross', 'refunded', 'credit'];
const RETIRED_CARD_KEY = `${PAGE_ROOT}.completed_orders`;
const BY_TENDER_COLUMN_KEYS = [
  `${PAGE_ROOT}.settled_from_credit`,
  `${PAGE_ROOT}.settled_on_tender`,
  `${PAGE_ROOT}.refunded_to_card`,
  `${PAGE_ROOT}.returned_to_credit`,
  `${PAGE_ROOT}.net_on_tender`,
];
const CAVEAT_KEYS = [
  `${PAGE_ROOT}.revenue_description`,
  `${PAGE_ROOT}.cancelled_orders_hint`,
  `${PAGE_ROOT}.revenue_by_payment_type_hint`,
];
// The gap the report does not close: a lost chargeback writes no refund row, so the copy names it in
// every locale rather than letting the accountant file the difference as a defect.
const CHARGEBACK_WORD = /chargeback|чарджб/i;

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as Record<string, unknown>;
}

function resolve(tree: Record<string, unknown>, dotted: string): unknown {
  return dotted.split('.').reduce<unknown>((node, segment) => {
    if (node && typeof node === 'object') {
      return (node as Record<string, unknown>)[segment];
    }
    return undefined;
  }, tree);
}

function leafKeys(node: unknown, prefix = ''): string[] {
  if (!node || typeof node !== 'object') return [prefix];
  return Object.entries(node as Record<string, unknown>).flatMap(([key, value]) =>
    leafKeys(value, prefix ? `${prefix}.${key}` : key)
  );
}

function untranslated(tree: Record<string, unknown>, keys: string[]): string[] {
  return keys.filter((key) => {
    const value = resolve(tree, key);
    return !(typeof value === 'string' && value.trim().length > 0);
  });
}

function referencedKeys(source: string): string[] {
  return [...new Set([...source.matchAll(/'(pages\.reports\.[a-z_.]+)'/g)].map((m) => m[1]))];
}

describe('revenue report copy', () => {
  const pageKeys = [...new Set([...referencedKeys(TEMPLATE), ...referencedKeys(COMPONENT)])];

  it('references the page keys it renders from the template and the column definitions', () => {
    expect(pageKeys.length).toBeGreaterThan(20);
    expect(pageKeys).toEqual(expect.arrayContaining([HEADLINE_KEY, BREAKDOWN_KEY, ...BY_TENDER_COLUMN_KEYS, ...CAVEAT_KEYS]));
  });

  it.each(LOCALES)('%s translates every key the page references', (locale) => {
    expect(untranslated(readLocale(locale), pageKeys)).toEqual([]);
  });

  it('renders the headline off the net figure with the gross/refunded/credit sub-line beneath it', () => {
    expect(TEMPLATE).toContain('facade.revenueHeadline()');
    expect(TEMPLATE).toContain(`'${HEADLINE_KEY}' | translate`);
    expect(TEMPLATE.replace(/\s+/g, ' ')).toContain(`'${BREAKDOWN_KEY}' | translate : facade.revenueBreakdown()`);
    expect(TEMPLATE).not.toContain('revenueReport()?.totalRevenue');
  });

  it.each(LOCALES)('%s carries the three amounts of the sub-line as placeholders and no literal figure', (locale) => {
    const value = resolve(readLocale(locale), BREAKDOWN_KEY);
    expect(typeof value).toBe('string');
    for (const placeholder of BREAKDOWN_PLACEHOLDERS) {
      expect(value).toContain(`{{${placeholder}}}`);
    }
    expect((value as string).replace(/\{\{\w+\}\}/g, '')).not.toMatch(/\d/);
  });

  it('shows no completed-orders card: it would always equal the completed & paid card above it', () => {
    expect(TEMPLATE).not.toContain(RETIRED_CARD_KEY);
    expect(TEMPLATE).not.toContain('revenueReport()?.completedOrders');
    expect(TEMPLATE).toContain(`'${PAGE_ROOT}.completed_paid_orders' | translate`);
    for (const locale of LOCALES) {
      expect(resolve(readLocale(locale), RETIRED_CARD_KEY)).toBeUndefined();
    }
  });

  it('names the by-tender refund legs and the net line, and the cancelled and reconciliation caveats', () => {
    for (const key of BY_TENDER_COLUMN_KEYS) {
      expect(COMPONENT).toContain(`'${key}'`);
    }
    expect(TEMPLATE).toContain(`[pTooltip]="'${PAGE_ROOT}.cancelled_orders_hint' | translate"`);
    expect(TEMPLATE).toContain(`'${PAGE_ROOT}.revenue_by_payment_type_hint' | translate`);
    expect(TEMPLATE).toContain(`'${PAGE_ROOT}.revenue_description' | translate`);
  });

  it.each(LOCALES)('%s names the lost-chargeback gap in the revenue description', (locale) => {
    expect(resolve(readLocale(locale), `${PAGE_ROOT}.revenue_description`)).toMatch(CHARGEBACK_WORD);
  });

  it('promises no Disputed row anywhere on the page', () => {
    expect(leafKeys(resolve(readLocale('en'), PAGE_ROOT)).filter((key) => /disput/i.test(key))).toEqual([]);
    const enValues = leafKeys(resolve(readLocale('en'), PAGE_ROOT)).map((key) => resolve(readLocale('en'), `${PAGE_ROOT}.${key}`));
    expect(enValues.filter((value) => typeof value === 'string' && /disputed/i.test(value))).toEqual([]);
  });

  it('pages.reports carries an identical, non-empty key set in all five locales', () => {
    const reference = leafKeys(resolve(readLocale('en'), PAGE_ROOT)).sort();
    expect(reference.length).toBeGreaterThan(0);
    for (const locale of LOCALES) {
      const tree = readLocale(locale);
      expect(leafKeys(resolve(tree, PAGE_ROOT)).sort()).toEqual(reference);
      expect(untranslated(tree, reference.map((key) => `${PAGE_ROOT}.${key}`))).toEqual([]);
    }
  });
});
