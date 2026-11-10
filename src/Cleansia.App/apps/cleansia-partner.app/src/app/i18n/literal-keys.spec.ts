import { existsSync, readFileSync, readdirSync, statSync } from 'fs';
import { dirname, join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
const PREFIXES = ['pages', 'global', 'enums', 'auth', 'components', 'nav', 'page_titles'];
const LITERAL_KEY = new RegExp(`['"\`]((?:${PREFIXES.join('|')})(?:\\.[a-z0-9_]+)+)['"\`]`, 'g');

function findSolutionDir(): string {
  let dir = process.cwd();
  for (let i = 0; i < 12; i++) {
    if (existsSync(join(dir, 'Cleansia.Api.sln'))) return dir;
    dir = dirname(dir);
  }
  throw new Error('Could not locate the solution dir (Cleansia.Api.sln)');
}

const APP_DIR = join(findSolutionDir(), 'Cleansia.App');

function sources(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return sources(path);
    return /\.(ts|html)$/.test(name) && !name.includes('.spec.') ? [path] : [];
  });
}

function flatten(node: unknown, prefix = ''): string[] {
  if (typeof node !== 'object' || node === null) return [prefix];
  return Object.entries(node).flatMap(([k, v]) => flatten(v, prefix ? `${prefix}.${k}` : k));
}

describe('partner literal translation keys', () => {
  const used = new Set<string>();
  for (const file of sources(join(APP_DIR, 'libs/cleansia-partner-features'))) {
    for (const match of readFileSync(file, 'utf8').matchAll(LITERAL_KEY)) used.add(match[1]);
  }

  it('finds the keys it checks', () => expect(used.size).toBeGreaterThan(200));

  for (const locale of LOCALES) {
    it(`${locale} carries every literal key the partner libs name`, () => {
      const bundle = JSON.parse(
        readFileSync(join(APP_DIR, `apps/cleansia-partner.app/src/assets/i18n/${locale}.json`), 'utf8')
      );
      const leaves = new Set(flatten(bundle));
      const prefixes = new Set([...leaves].flatMap((k) => k.split('.').map((_, i, p) => p.slice(0, i + 1).join('.'))));
      // A key that is only a prefix is built up at run time (`facts.${name}`), not a miss.
      const missing = [...used].filter((k) => !leaves.has(k) && !prefixes.has(k));
      expect(missing).toEqual([]);
    });
  }
});
