import { existsSync, readdirSync, readFileSync, statSync } from 'fs';
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

const HOME_DIR = join(SOLUTION_DIR, 'Cleansia.App/libs/cleansia-customer-features/home/src');

/**
 * Owner ruling 2026-09-28: Cleansia launches with no welcome offer. The home page e-mailed a
 * first-clean discount code whose value was a placeholder and whose "first" nothing enforced.
 */
const FIRST_CLEAN_DISCOUNT =
  /discount[^.]*first (?:clean|booking|order)|slev\S*[^.]*prvn\S* (?:úklid|objedn)|zľav\S*[^.]*prv\S* (?:upratov|objedn)|знижк\S*[^.]*перш\S* (?:прибиран|замовлен)|скидк\S*[^.]*перв\S* (?:уборк|заказ)/i;

function values(node: unknown): string[] {
  if (typeof node === 'string') return [node];
  if (node && typeof node === 'object') return Object.values(node).flatMap(values);
  return [];
}

function sources(dir: string): string[] {
  return readdirSync(dir).flatMap((entry) => {
    const path = join(dir, entry);
    if (statSync(path).isDirectory()) return sources(path);
    return /\.(ts|html)$/.test(entry) && !entry.endsWith('.spec.ts') ? [path] : [];
  });
}

describe('the customer app offers no welcome discount', () => {
  it.each(LOCALES)('no %s value offers a discount code for a first clean', (locale) => {
    const bundle = JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8'));

    expect(values(bundle).filter((value) => FIRST_CLEAN_DISCOUNT.test(value))).toEqual([]);
    expect(Object.keys(bundle.pages.home.cta).filter((key) => key.startsWith('promo'))).toEqual([]);
  });

  it('the home page requests no promo code', () => {
    const requesting = sources(HOME_DIR).filter((path) =>
      /promoCodeClient|RequestPromoCode|pages\.home\.cta\.promo/.test(readFileSync(path, 'utf8'))
    );

    expect(requesting).toEqual([]);
  });
});
