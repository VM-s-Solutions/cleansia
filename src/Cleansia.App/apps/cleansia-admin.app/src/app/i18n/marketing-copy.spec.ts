import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
const KEYS = ['already_sent', 'email_not_available'];

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

function sitewidePush(locale: string): Record<string, unknown> {
  const bundle = JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as {
    pages: { sitewide_push: Record<string, unknown> };
  };
  return bundle.pages.sitewide_push;
}

describe('the admin Marketing page copy', () => {
  const english = sitewidePush('en');

  it.each(LOCALES)('the %s bundle carries every key, translated from the English', (locale) => {
    const copy = sitewidePush(locale);

    expect(KEYS.filter((key) => typeof copy[key] !== 'string' || !copy[key])).toEqual([]);
    if (locale !== 'en') {
      expect(KEYS.filter((key) => copy[key] === english[key])).toEqual([]);
    }
  });
});
