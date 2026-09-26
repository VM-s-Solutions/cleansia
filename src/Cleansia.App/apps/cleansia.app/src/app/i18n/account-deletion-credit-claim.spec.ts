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
  uk: { credit: /бонус/i, forfeited: /анулю/i, paidOut: /виплатити/i },
  ru: { credit: /бонус/i, forfeited: /аннулир/i, paidOut: /выплатить/i },
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
