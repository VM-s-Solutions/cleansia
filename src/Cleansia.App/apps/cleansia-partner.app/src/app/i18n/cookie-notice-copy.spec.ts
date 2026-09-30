import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
const NOTICE_KEYS = ['acknowledge', 'banner_title', 'description', 'learn_more', 'title'];

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

const I18N_DIR = join(findSolutionDir(), 'Cleansia.App/apps/cleansia-partner.app/src/assets/i18n');

describe('the partner cookie notice copy', () => {
  it.each(LOCALES)('the %s bundle carries only the notice copy, with nothing to accept or refuse', (locale) => {
    const bundle = JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as {
      cookies: Record<string, unknown>;
    };

    expect(Object.keys(bundle.cookies).sort()).toEqual(NOTICE_KEYS);
    expect(NOTICE_KEYS.filter((key) => typeof bundle.cookies[key] !== 'string' || !bundle.cookies[key])).toEqual([]);
  });
});
