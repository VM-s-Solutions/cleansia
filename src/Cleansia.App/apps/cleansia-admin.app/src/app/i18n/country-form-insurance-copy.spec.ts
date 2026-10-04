import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
type Locale = (typeof LOCALES)[number];

const EMPTY_VALUE_KEYS = [
  'insurance_coverage_amount_placeholder',
  'insurance_coverage_amount_help',
];

const NAMES_INSURANCE: Record<Locale, RegExp> = {
  en: /insur/i,
  cs: /pojišt|pojist/i,
  sk: /poist/i,
  uk: /страх/i,
  ru: /страх/i,
};

const ONLY_THE_FIGURE_GOES: Record<Locale, RegExp> = {
  en: /no figure/i,
  cs: /žádn(á|ou) částk/i,
  sk: /žiadn(a|u) sum/i,
  uk: /не (вказувати|називає) сум/i,
  ru: /не (указывать|называет) сумм/i,
};

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
  'Cleansia.App/apps/cleansia-admin.app/src/assets/i18n'
);

function readCountryForm(locale: Locale): Record<string, unknown> {
  const bundle = JSON.parse(
    readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')
  ) as { pages: { country_form: Record<string, unknown> } };
  return bundle.pages.country_form;
}

describe('the admin country form insurance-ceiling copy', () => {
  it.each(LOCALES)(
    'the %s placeholder and help say an empty ceiling makes no insurance claim, not just no figure',
    (locale) => {
      const form = readCountryForm(locale);

      for (const key of EMPTY_VALUE_KEYS) {
        const value = form[key];
        expect({ key, type: typeof value }).toEqual({ key, type: 'string' });
        expect({
          key,
          namesInsurance: NAMES_INSURANCE[locale].test(value as string),
        }).toEqual({
          key,
          namesInsurance: true,
        });
        expect({
          key,
          onlyTheFigureGoes: ONLY_THE_FIGURE_GOES[locale].test(value as string),
        }).toEqual({
          key,
          onlyTheFigureGoes: false,
        });
      }
    }
  );
});
