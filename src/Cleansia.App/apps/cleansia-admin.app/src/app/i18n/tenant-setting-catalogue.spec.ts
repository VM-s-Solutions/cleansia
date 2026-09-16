import { existsSync, readdirSync, readFileSync, statSync } from 'fs';
import { basename, dirname, join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
type Locale = (typeof LOCALES)[number];

const SETTINGS_ROOT = 'pages.company_settings';
const DESCRIPTIONS_ROOT = `${SETTINGS_ROOT}.descriptions`;
const CATEGORIES_ROOT = `${SETTINGS_ROOT}.categories`;

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
const APP_SERVICES_DIR = join(SOLUTION_DIR, 'Cleansia.Core.AppServices');
const CATALOGUE_PATH = join(APP_SERVICES_DIR, 'Features/TenantSettings/TenantSettingCatalog.cs');
const I18N_DIR = join(SOLUTION_DIR, 'Cleansia.App/apps/cleansia-admin.app/src/assets/i18n');

// `public const string RetentionCategory = "retention";`
const CATEGORY_CONSTANT = /public const string \w+Category = "([^"]+)"/g;

// `RetentionDefaults.StaleDevicesDaysKey` — a definition naming its key through a constant.
const KEY_REFERENCE = /\b([A-Z]\w*)\.(\w+Key)\b/g;

// `"pricing.minimum_order.czk"` — a definition naming its key inline.
const KEY_LITERAL = /"([a-z0-9_]+(?:\.[a-z0-9_]+)+)"/g;

function walkCsFiles(dir: string, out: string[] = []): string[] {
  for (const name of readdirSync(dir)) {
    const full = join(dir, name);
    if (statSync(full).isDirectory()) {
      if (name === 'bin' || name === 'obj') continue;
      walkCsFiles(full, out);
    } else if (name.endsWith('.cs')) {
      out.push(full);
    }
  }
  return out;
}

const CS_FILES = walkCsFiles(APP_SERVICES_DIR);

function constantValue(className: string, constantName: string): string {
  const file = CS_FILES.find((path) => basename(path) === `${className}.cs`);
  if (!file) throw new Error(`${className}.cs not found under ${APP_SERVICES_DIR}`);
  const match = new RegExp(`public const string ${constantName} = "([^"]+)"`).exec(
    readFileSync(file, 'utf8')
  );
  if (!match) throw new Error(`${className}.${constantName} is not a string constant`);
  return match[1];
}

function catalogueKeys(): string[] {
  const source = readFileSync(CATALOGUE_PATH, 'utf8');
  const keys = new Set<string>();
  for (const [, className, constantName] of source.matchAll(KEY_REFERENCE)) {
    keys.add(constantValue(className, constantName));
  }
  for (const [, literal] of source.matchAll(KEY_LITERAL)) {
    keys.add(literal);
  }
  return [...keys];
}

function catalogueCategories(): string[] {
  const source = readFileSync(CATALOGUE_PATH, 'utf8');
  return [...source.matchAll(CATEGORY_CONSTANT)].map((m) => m[1]);
}

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as Record<
    string,
    unknown
  >;
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

function untranslated(tree: Record<string, unknown>, root: string, keys: string[]): string[] {
  return keys.filter((key) => {
    const value = resolve(tree, `${root}.${key}`);
    return !(typeof value === 'string' && value.trim().length > 0);
  });
}

describe('tenant setting catalogue', () => {
  const keys = catalogueKeys();
  const categories = catalogueCategories();

  it('reads a ten-key, two-category catalogue off the backend', () => {
    expect(keys).toHaveLength(10);
    expect(categories).toEqual(['retention', 'lifecycle']);
  });

  it.each(LOCALES)('%s describes every catalogue key under pages.company_settings.descriptions', (locale) => {
    expect(untranslated(readLocale(locale), DESCRIPTIONS_ROOT, keys)).toEqual([]);
  });

  it.each(LOCALES)('%s labels every catalogue category under pages.company_settings.categories', (locale) => {
    expect(untranslated(readLocale(locale), CATEGORIES_ROOT, categories)).toEqual([]);
  });

  it('describes no key and labels no category the catalogue does not carry', () => {
    for (const locale of LOCALES) {
      const tree = readLocale(locale);
      expect(leafKeys(resolve(tree, DESCRIPTIONS_ROOT)).filter((key) => !keys.includes(key))).toEqual([]);
      expect(
        leafKeys(resolve(tree, CATEGORIES_ROOT)).filter((category) => !categories.includes(category))
      ).toEqual([]);
    }
  });

  it('pages.company_settings carries an identical, non-empty key set in all five locales', () => {
    const reference = leafKeys(resolve(readLocale('en'), SETTINGS_ROOT)).sort();
    expect(reference.length).toBeGreaterThan(0);
    for (const locale of LOCALES) {
      const tree = readLocale(locale);
      expect(leafKeys(resolve(tree, SETTINGS_ROOT)).sort()).toEqual(reference);
      expect(untranslated(tree, SETTINGS_ROOT, reference)).toEqual([]);
    }
  });
});
