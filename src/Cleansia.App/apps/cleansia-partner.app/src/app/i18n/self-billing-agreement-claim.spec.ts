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

const I18N_DIR = join(
  findSolutionDir(),
  'Cleansia.App/apps/cleansia-partner.app/src/assets/i18n'
);

/**
 * No cleaner has accepted a self-billing agreement: the app offers none to accept yet, so the
 * deletion notice may not name one among the records kept. The day the acceptance ships, invert
 * this into a spec that the agreement IS named.
 */
const AGREEMENT: Record<Locale, RegExp> = {
  en: /self-billing agreement/i,
  cs: /dohod\S* o samofakturaci/i,
  sk: /dohod\S* o samofakturácii/i,
  uk: /угод\S* про самовиставлення/i,
  ru: /соглашени\S* о самовыставлении/i,
};

/** What the deletion notice does say is kept: the invoices and the pay records. */
const KEPT: Record<Locale, readonly RegExp[]> = {
  en: [/invoices/i, /pay records/i],
  cs: [/faktury/i, /záznamy/i],
  sk: [/faktúry/i, /záznamy/i],
  uk: [/рахунки/i, /записи про оплату/i],
  ru: [/счета/i, /записи об оплате/i],
};

function values(node: unknown): string[] {
  if (typeof node === 'string') return [node];
  if (node && typeof node === 'object') return Object.values(node).flatMap(values);
  return [];
}

function readLocale(locale: Locale): { pages: { gdpr: Record<string, string> } } {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8'));
}

/** Consent-type labels name a record that already exists; they promise nothing. */
function withoutConsentTypeLabels(locale: Locale): unknown {
  const copy = JSON.parse(JSON.stringify(readLocale(locale)));
  delete copy.pages?.gdpr?.consent_types;
  return copy;
}

describe('the partner app promises no self-billing agreement', () => {
  it.each(LOCALES)('no %s value names a self-billing agreement', (locale) => {
    expect(
      values(withoutConsentTypeLabels(locale)).filter((value) => AGREEMENT[locale].test(value))
    ).toEqual([]);
  });

  it.each(LOCALES)('the %s deletion notice keeps the invoices and pay records', (locale) => {
    const notice = readLocale(locale).pages.gdpr['delete_confirm_message'];

    expect(KEPT[locale].filter((stem) => !stem.test(notice))).toEqual([]);
  });
});
