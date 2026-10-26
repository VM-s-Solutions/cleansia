import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
type Locale = (typeof LOCALES)[number];

const SAYS_NOT_ENFORCED: Record<Locale, RegExp> = {
  en: /not enforced/i,
  cs: /nevynucuje/i,
  sk: /nevynucuje/i,
  uk: /не використовується/i,
  ru: /не применяется/i,
};

const NAMES_THE_POSTCODE_RULE: Record<Locale, RegExp> = {
  en: /five-digit postcode/i,
  cs: /pětimístn\S* PSČ/i,
  sk: /päťmiestn\S* PSČ/i,
  uk: /п’ятизначн\S* індекс/i,
  ru: /пятизначн\S* индекс/i,
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

const I18N_DIR = join(findSolutionDir(), 'Cleansia.App/apps/cleansia-admin.app/src/assets/i18n');

/** The server decides an address that has a five-digit postcode by a city's ZIP prefix. */
describe('the admin ZIP prefix hint', () => {
  it.each(LOCALES)('the %s hint says a five-digit postcode decides the city, not that the prefix is unused', (locale) => {
    const bundle = JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as {
      pages: { service_area_management: { cities: { zip_prefix_hint: string } } };
    };
    const hint = bundle.pages.service_area_management.cities.zip_prefix_hint;

    expect({ locale, saysNotEnforced: SAYS_NOT_ENFORCED[locale].test(hint) }).toEqual({ locale, saysNotEnforced: false });
    expect({ locale, namesThePostcodeRule: NAMES_THE_POSTCODE_RULE[locale].test(hint) }).toEqual({
      locale,
      namesThePostcodeRule: true,
    });
  });
});
