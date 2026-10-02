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

const SOLUTION_DIR = findSolutionDir();

const I18N_DIR = join(SOLUTION_DIR, 'Cleansia.App/apps/cleansia.app/src/assets/i18n');

const GDPR_DIR = join(
  SOLUTION_DIR,
  'Cleansia.App/libs/cleansia-customer-features/gdpr/src/lib/gdpr'
);

/**
 * A completed account deletion forfeits every unused credit balance; it is not paid out and cannot
 * be restored. Each locale's own credit word, its own "forfeited" and its own "paid out" — so a
 * translation that drops any of the three fails in the locale that dropped it.
 */
const CLAIM: Record<Locale, { credit: RegExp; forfeited: RegExp; paidOut: RegExp }> = {
  en: { credit: /credit/i, forfeited: /forfeit/i, paidOut: /paid out/i },
  cs: { credit: /kredit/i, forfeited: /propadá/i, paidOut: /vyplatit/i },
  sk: { credit: /kredit/i, forfeited: /prepadá/i, paidOut: /vyplatiť/i },
  uk: { credit: /кредит/i, forfeited: /анулю/i, paidOut: /виплатити/i },
  ru: { credit: /кредит/i, forfeited: /аннулир/i, paidOut: /выплатить/i },
};

/** The confirmation the customer accepts, and the section text above its button. */
const RENDERED = [
  { file: 'gdpr.component.ts', key: 'pages.gdpr.delete_confirm_message' },
  { file: 'gdpr.component.html', key: 'pages.gdpr.delete_description' },
];

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as Record<
    string,
    unknown
  >;
}

function resolveKey(bundle: unknown, key: string): string {
  const value = key
    .split('.')
    .reduce<unknown>(
      (node, segment) =>
        node && typeof node === 'object' ? (node as Record<string, unknown>)[segment] : undefined,
      bundle
    );
  return typeof value === 'string' ? value : '';
}

describe('deleting an account warns that unused credit is forfeited', () => {
  it('reads the warning from the keys the page actually renders', () => {
    for (const { file, key } of RENDERED) {
      const source = readFileSync(join(GDPR_DIR, file), 'utf8');
      expect({ file, key, rendered: source.includes(`'${key}'`) }).toEqual({
        file,
        key,
        rendered: true,
      });
    }
  });

  it.each(LOCALES)('says the credit is forfeited and cannot be paid out, in %s', (locale) => {
    const bundle = readLocale(locale);
    const { credit, forfeited, paidOut } = CLAIM[locale];

    for (const { key } of RENDERED) {
      const value = resolveKey(bundle, key);
      expect({
        key,
        credit: credit.test(value),
        forfeited: forfeited.test(value),
        paidOut: paidOut.test(value),
      }).toEqual({ key, credit: true, forfeited: true, paidOut: true });
    }
  });
});

/**
 * Deletion anonymises in place: the bookings, receipts, consent records, the action log and any
 * dispute text outlive the account on purpose (→ /flows/gdpr-and-audit). The page says so, and no
 * deletion text promises that every piece of data goes.
 */
const KEPT: Record<Locale, readonly RegExp[]> = {
  en: [/bookings/i, /receipts/i, /consent/i, /log of actions/i, /dispute/i],
  cs: [/objednávk/i, /účtenk/i, /souhlas/i, /úkon/i, /reklamac/i],
  sk: [/objednávk/i, /účtenk/i, /súhlas/i, /úkon/i, /reklamáci/i],
  uk: [/замовлен/i, /чек/i, /згод/i, /журнал/i, /скарг/i],
  ru: [/заказ/i, /чек/i, /соглас/i, /журнал/i, /обращени/i],
};

const RETENTION_DEFAULTS = join(
  SOLUTION_DIR,
  'Cleansia.Core.AppServices/Features/DataRetention/RetentionDefaults.cs'
);

function retentionDefault(name: string): number {
  const match = new RegExp(`public\\s+const\\s+int\\s+${name}\\s*=\\s*(\\d+)\\s*;`).exec(
    readFileSync(RETENTION_DEFAULTS, 'utf8')
  );
  if (!match) throw new Error(`RetentionDefaults.${name} not found — the parser needs updating`);
  return Number(match[1]);
}

/** The consent records, the action log and the dispute text, in the order the notice names them. */
const KEPT_FOR_YEARS = [
  retentionDefault('DefaultWithdrawnConsentsYears'),
  retentionDefault('DefaultCustomerAuditRetentionYears'),
  retentionDefault('DefaultDisputeTextRetentionYears'),
];

const YEARS: Record<Locale, string> = {
  en: 'year',
  cs: '(?:let|rok)',
  sk: 'rok',
  uk: '(?:рок|рік)',
  ru: '(?:год|лет)',
};

const ALL_DATA: Record<Locale, RegExp> = {
  en: /all (?:associated |your )?data/i,
  cs: /všechn[aá] (?:související |vaše )?data/i,
  sk: /všetky (?:súvisiace |vaše )?údaje/i,
  uk: /(?:всі|усі) (?:пов'язані |ваші )?дані/i,
  ru: /все (?:связанные |ваши )?данные/i,
};

const DELETION_COPY = [...RENDERED.map(({ key }) => key), 'pages.profile.delete_account_desc'];

const RETIRED_KEYS = [
  'pages.gdpr.delete_desc',
  'pages.gdpr.delete_confirm',
  'gdpr.delete.description',
  'gdpr.delete.confirm',
];

describe('deleting an account says what is kept, and for how long', () => {
  it.each(LOCALES)('names every record kept, in %s', (locale) => {
    const description = resolveKey(readLocale(locale), 'pages.gdpr.delete_description');

    expect(KEPT[locale].filter((stem) => !stem.test(description))).toEqual([]);
  });

  it.each(LOCALES)('keeps each dated record for the server default number of years, in %s', (locale) => {
    const description = resolveKey(readLocale(locale), 'pages.gdpr.delete_description');
    const stated = [...description.matchAll(new RegExp(`(\\d+)\\s${YEARS[locale]}`, 'gi'))].map(
      (match) => Number(match[1])
    );

    expect(stated).toEqual(KEPT_FOR_YEARS);
  });

  it.each(LOCALES)('never promises that all data is deleted, in %s', (locale) => {
    const bundle = readLocale(locale);

    for (const key of DELETION_COPY) {
      const value = resolveKey(bundle, key);
      expect(value.trim().length).toBeGreaterThan(0);
      expect({ key, allData: ALL_DATA[locale].test(value) }).toEqual({ key, allData: false });
    }
  });

  it.each(LOCALES)('carries none of the retired deletion keys, in %s', (locale) => {
    const bundle = readLocale(locale);

    expect(RETIRED_KEYS.filter((key) => resolveKey(bundle, key) !== '')).toEqual([]);
  });
});
