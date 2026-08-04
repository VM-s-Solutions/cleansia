import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
type Locale = (typeof LOCALES)[number];

const PLAN_FORM_NAMESPACE = 'pages.membership_plans.form';

const EXPRESS_QUOTA_KEYS = [
  `${PLAN_FORM_NAMESPACE}.field.express_upgrades_per_month`,
  `${PLAN_FORM_NAMESPACE}.field.express_upgrades_per_month_help`,
  `${PLAN_FORM_NAMESPACE}.validation.express_upgrades_negative`,
];

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

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(
    readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')
  ) as Record<string, unknown>;
}

function resolve(bundle: Record<string, unknown>, path: string): unknown {
  return path
    .split('.')
    .reduce<unknown>(
      (node, segment) =>
        node && typeof node === 'object'
          ? (node as Record<string, unknown>)[segment]
          : undefined,
      bundle
    );
}

function flattenKeys(node: unknown, prefix = ''): string[] {
  if (node === null || typeof node !== 'object' || Array.isArray(node)) {
    return [prefix];
  }
  return Object.entries(node as Record<string, unknown>).flatMap(
    ([key, value]) => flattenKeys(value, prefix ? `${prefix}.${key}` : key)
  );
}

describe('admin membership-plan form copy', () => {
  const bundles = new Map<Locale, Record<string, unknown>>(
    LOCALES.map((locale) => [locale, readLocale(locale)])
  );

  it.each(LOCALES)('resolves the express-quota copy in %s', (locale) => {
    const bundle = bundles.get(locale) as Record<string, unknown>;

    for (const key of EXPRESS_QUOTA_KEYS) {
      const value = resolve(bundle, key);
      expect(typeof value).toBe('string');
      expect((value as string).trim().length).toBeGreaterThan(0);
    }
  });

  it('never states a fixed express quota — it is a per-plan number', () => {
    for (const locale of LOCALES) {
      const bundle = bundles.get(locale) as Record<string, unknown>;
      const value = resolve(
        bundle,
        `${PLAN_FORM_NAMESPACE}.field.express_upgrades_per_month`
      ) as string;

      expect(value).not.toMatch(/\b(2|two|dva|dvě|dve|два|дві)\b/i);
    }
  });

  it('keeps identical membership-plan key sets across the five locales', () => {
    const reference = flattenKeys(
      resolve(
        bundles.get('en') as Record<string, unknown>,
        'pages.membership_plans'
      )
    ).sort();

    expect(reference.length).toBeGreaterThan(0);

    for (const locale of LOCALES) {
      const keys = flattenKeys(
        resolve(
          bundles.get(locale) as Record<string, unknown>,
          'pages.membership_plans'
        )
      ).sort();
      expect({ locale, keys }).toEqual({ locale, keys: reference });
    }
  });
});
