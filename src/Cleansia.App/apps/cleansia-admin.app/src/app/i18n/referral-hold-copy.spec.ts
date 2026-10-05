import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
type Locale = (typeof LOCALES)[number];

const ROOT = 'pages.loyalty_referrals';

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
const REFERRAL_ENTITY = join(SOLUTION_DIR, 'Cleansia.Core.Domain/Loyalty/Referral.cs');
const I18N_DIR = join(SOLUTION_DIR, 'Cleansia.App/apps/cleansia-admin.app/src/assets/i18n');

// `public const string HoldReasonAddress = "address";`
const HOLD_REASON_CONSTANT = /public const string HoldReason\w+ = "([a-z_]+)";/g;

const HELD_REFERRAL_KEYS = [
  'filter.status_held',
  'held',
  'actions.release',
  'actions.reject',
  'intervention.title_release',
  'intervention.title_reject',
  'intervention.hint_release',
  'intervention.hint_reject',
  'intervention.submit_release',
  'intervention.submit_reject',
  'intervention.success_reject',
].map((key) => `${ROOT}.${key}`);

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as Record<string, unknown>;
}

function resolve(tree: Record<string, unknown>, dotted: string): unknown {
  return dotted.split('.').reduce<unknown>(
    (node, segment) => (node && typeof node === 'object' ? (node as Record<string, unknown>)[segment] : undefined),
    tree
  );
}

function leafKeys(node: unknown, prefix = ''): string[] {
  if (!node || typeof node !== 'object') return [prefix];
  return Object.entries(node as Record<string, unknown>).flatMap(([key, value]) =>
    leafKeys(value, prefix ? `${prefix}.${key}` : key)
  );
}

function untranslated(tree: Record<string, unknown>, keys: string[]): string[] {
  return keys.filter((key) => {
    const value = resolve(tree, key);
    return !(typeof value === 'string' && value.trim().length > 0);
  });
}

const slugs = [...readFileSync(REFERRAL_ENTITY, 'utf8').matchAll(HOLD_REASON_CONSTANT)].map((m) => m[1]);

describe('the held-referral copy of the admin referrals page', () => {
  it('reads the hold reasons off the referral entity', () => {
    expect(slugs).toEqual(expect.arrayContaining(['address', 'phone', 'email']));
  });

  it.each(LOCALES)('labels every reason the backend can hold a referral for, in %s', (locale) => {
    const keys = slugs.map((slug) => `${ROOT}.hold_reason.${slug}`);
    expect(untranslated(readLocale(locale), keys)).toEqual([]);
  });

  it.each(LOCALES)('names the held filter, the chip, release and reject, in %s', (locale) => {
    expect(untranslated(readLocale(locale), HELD_REFERRAL_KEYS)).toEqual([]);
  });

  it.each(LOCALES)('puts the reasons into the held chip, in %s', (locale) => {
    expect(resolve(readLocale(locale), `${ROOT}.held`)).toMatch(/\{\{\s*reasons\s*\}\}/);
  });

  it('carries one key set for the referrals page in all five locales', () => {
    const [first, ...rest] = LOCALES.map((locale) => leafKeys(resolve(readLocale(locale), ROOT), ROOT).sort());
    for (const keys of rest) {
      expect(keys).toEqual(first);
    }
  });
});
