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

const I18N_DIR = join(findSolutionDir(), 'Cleansia.App/apps/cleansia-admin.app/src/assets/i18n');

const GRANT_POINTS = 'pages.loyalty_user_detail.actions.grant';
const REVOKE_POINTS = 'pages.loyalty_user_detail.actions.revoke';
const ISSUE_CREDIT = 'pages.loyalty_user_detail.credit.actions.issue';
const CREDIT_DIALOG_TITLE = 'pages.loyalty_user_detail.credit.dialog.title';
const AMOUNT_IN = 'pages.loyalty_user_detail.credit.dialog.field.amount_in';
const CREDIT_ISSUED = 'pages.loyalty_user_detail.credit.success';
const POINTS_GRANTED = 'pages.loyalty_user_detail.grant_dialog.success_grant';

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as Record<string, unknown>;
}

function resolveKey(tree: Record<string, unknown>, dotted: string): string {
  const value = dotted.split('.').reduce<unknown>((node, segment) => {
    if (node && typeof node === 'object') return (node as Record<string, unknown>)[segment];
    return undefined;
  }, tree);
  return typeof value === 'string' ? value : '';
}

function words(label: string): string[] {
  return label.toLocaleLowerCase().split(/[\s.,!?;:]+/).filter(Boolean);
}

describe('the admin customer detail tells points and credit apart', () => {
  it.each(LOCALES)('shares no word between Issue credit and either points action, in %s', (locale) => {
    const bundle = readLocale(locale);
    const credit = words(resolveKey(bundle, ISSUE_CREDIT));

    expect(credit.length).toBeGreaterThan(0);
    for (const key of [GRANT_POINTS, REVOKE_POINTS]) {
      const points = words(resolveKey(bundle, key));
      expect(points.length).toBeGreaterThan(0);
      expect({ key, shared: points.filter((word) => credit.includes(word)) }).toEqual({ key, shared: [] });
    }
  });

  it.each(LOCALES)('confirms issued credit in words the points confirmation does not use, in %s', (locale) => {
    const bundle = readLocale(locale);
    const issued = words(resolveKey(bundle, CREDIT_ISSUED));
    const granted = words(resolveKey(bundle, POINTS_GRANTED));

    expect(issued.length).toBeGreaterThan(0);
    expect(granted.length).toBeGreaterThan(0);
    expect(granted.filter((word) => issued.includes(word))).toEqual([]);
  });

  it.each(LOCALES)('opens a credit dialog titled as the action that opened it, in %s', (locale) => {
    const bundle = readLocale(locale);

    expect(resolveKey(bundle, CREDIT_DIALOG_TITLE)).toBe(resolveKey(bundle, ISSUE_CREDIT));
  });

  it.each(LOCALES)('names the currency beside the credit amount, in %s', (locale) => {
    expect(resolveKey(readLocale(locale), AMOUNT_IN)).toContain('{{currency}}');
  });
});
