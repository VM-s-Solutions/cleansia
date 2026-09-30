import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
type Locale = (typeof LOCALES)[number];

const I18N_DIR = join(__dirname, '../../assets/i18n');
const FOOTER_TEMPLATE = join(__dirname, '../components/footer/customer-footer.component.html');

function findSolutionDir(): string {
  let dir = __dirname;
  for (let i = 0; i < 12; i++) {
    if (existsSync(join(dir, 'Cleansia.Api.sln'))) return dir;
    const parent = dirname(dir);
    if (parent === dir) break;
    dir = parent;
  }
  throw new Error('Could not locate the solution dir (Cleansia.Api.sln)');
}

const ORDER_ENTITY = join(findSolutionDir(), 'Cleansia.Core.Domain/Orders/Order.cs');

function earlyPerformanceVersionInForce(): string {
  const match = readFileSync(ORDER_ENTITY, 'utf8').match(
    /EarlyPerformanceConsentTextVersionInForce\s*=\s*"([^"]+)"/,
  );
  if (!match) throw new Error('Order.EarlyPerformanceConsentTextVersionInForce not found');
  return match[1];
}

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as Record<
    string,
    unknown
  >;
}

function leaf(locale: Record<string, unknown>, path: string[]): unknown {
  let node: unknown = locale;
  for (const segment of path) {
    node = node && typeof node === 'object' ? (node as Record<string, unknown>)[segment] : undefined;
  }
  return node;
}

const REQUIRED: string[][] = [
  ['pages', 'order', 'contract_notice'],
  ['pages', 'order', 'missing', 'early_performance'],
  ['recurring_booking', 'early_performance_label'],
  ['recurring_booking', 'error_early_performance'],
  ['complaints_page', 'title'],
  ['pages', 'home', 'footer', 'complaints_procedure_link'],
];

// The customer's contract is with the operating company, so nothing on the customer web names a
// contract for work with the cleaner, and the footer no longer prints a registration placeholder.
const REMOVED: string[][] = [
  ['work_contract_page'],
  ['pages', 'order_contract'],
  ['pages', 'order_detail', 'work_contract'],
  ['page_titles', 'customer', 'order_contract'],
  ['pages', 'home', 'footer', 'work_contract_link'],
  ['pages', 'home', 'footer', 'registration'],
  ['pages', 'order', 'work_contract_notice'],
];

describe('the customer contract copy in every locale', () => {
  it('carries every string non-empty in the five locales', () => {
    const version = earlyPerformanceVersionInForce();
    for (const locale of LOCALES) {
      const bundle = readLocale(locale);
      const empty = [...REQUIRED, ['pages', 'order', 'early_performance', version]]
        .filter((path) => {
          const value = leaf(bundle, path);
          return typeof value !== 'string' || !value.trim();
        })
        .map((path) => path.join('.'));

      expect({ locale, empty }).toEqual({ locale, empty: [] });
    }
  });

  it('carries none of the contract-for-work strings the customer no longer sees', () => {
    for (const locale of LOCALES) {
      const bundle = readLocale(locale);
      const present = REMOVED.filter((path) => leaf(bundle, path) !== undefined).map((path) =>
        path.join('.'),
      );

      expect({ locale, present }).toEqual({ locale, present: [] });
    }
  });

  // The wizard renders the sentence as markup so the link survives.
  it('the confirm-step sentence links to the terms and to no contract for work', () => {
    for (const locale of LOCALES) {
      const sentence = String(leaf(readLocale(locale), ['pages', 'order', 'contract_notice']));

      expect({ locale, terms: sentence.includes("href='/terms'") }).toEqual({ locale, terms: true });
      expect({ locale, workContract: sentence.includes('work-contract') }).toEqual({
        locale,
        workContract: false,
      });
    }
  });

  it('the withdrawal tick is keyed by the wording version the server records and names the 14 days', () => {
    const version = earlyPerformanceVersionInForce();
    for (const locale of LOCALES) {
      const tick = String(leaf(readLocale(locale), ['pages', 'order', 'early_performance', version]));

      expect({ locale, namesPeriod: tick.includes('14') }).toEqual({ locale, namesPeriod: true });
    }
  });
});

describe('the customer footer', () => {
  const footer = readFileSync(FOOTER_TEMPLATE, 'utf8');

  it('links the complaints procedure and no contract for work', () => {
    expect(footer).toContain('routerLink="/complaints"');
    expect(footer).not.toContain('/work-contract');
  });

  it('prints no registration placeholder line', () => {
    expect(footer).not.toContain('pages.home.footer.registration');
  });
});
