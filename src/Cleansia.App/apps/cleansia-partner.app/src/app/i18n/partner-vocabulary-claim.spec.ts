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

const I18N_DIR = join(findSolutionDir(), 'Cleansia.App/apps/cleansia-partner.app/src/assets/i18n');

/**
 * A cleaner is a self-employed partner paid a reward, not an employee paid a wage, and the platform
 * neither ranks them nor tracks their performance. Values only: keys such as `employee_name` are wire
 * names and stay.
 */
const EMPLOYMENT_VOCABULARY =
  /\bemployees?\b|\bwages?\b|payroll|\bgrade\b|\brank\b|track performance|analy[sz]e performance|zaměstn|zamestn|\bmzd|\bmezd|\bmiezd|hodnost|hodnosť|sledujte výkon|výkonnosť|сотрудник|заработн|производительност|працівник|співробітник|заробітн|продуктивніст/i;

const OWN_ANALYTICS = "the partner's own dashboard figures, read by the partner, not tracked by the platform";

const ALLOWED: readonly { locale: Locale; key: string; reason: string }[] = [
  { locale: 'uk', key: 'pages.dashboard.analytics', reason: OWN_ANALYTICS },
  { locale: 'uk', key: 'pages.dashboard.productivity_metrics.no_data', reason: OWN_ANALYTICS },
  { locale: 'ru', key: 'pages.dashboard.analytics', reason: OWN_ANALYTICS },
  { locale: 'ru', key: 'pages.dashboard.time_analytics.title', reason: OWN_ANALYTICS },
  { locale: 'ru', key: 'pages.dashboard.productivity_metrics.title', reason: OWN_ANALYTICS },
  { locale: 'ru', key: 'pages.dashboard.productivity_metrics.no_data', reason: OWN_ANALYTICS },
];

function readLocale(locale: Locale): unknown {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8'));
}

function leafEntries(node: unknown, prefix = ''): [string, string][] {
  if (typeof node === 'string') return [[prefix, node]];
  if (!node || typeof node !== 'object') return [];
  return Object.entries(node as Record<string, unknown>).flatMap(([key, value]) =>
    leafEntries(value, prefix ? `${prefix}.${key}` : key)
  );
}

function isAllowed(locale: Locale, key: string): boolean {
  return ALLOWED.some((entry) => entry.locale === locale && entry.key === key);
}

describe('the partner app calls a cleaner a partner paid a reward', () => {
  it.each(LOCALES)('no %s value speaks of employees, wages, rank or tracked performance', (locale) => {
    const found = leafEntries(readLocale(locale))
      .filter(([key, value]) => EMPLOYMENT_VOCABULARY.test(value) && !isAllowed(locale, key))
      .map(([key, value]) => `${key}: ${value}`);

    expect(found).toEqual([]);
  });

  it('allows no exemption the copy no longer needs', () => {
    const stale = ALLOWED.filter(({ locale, key }) => {
      const value = new Map(leafEntries(readLocale(locale))).get(key) ?? '';
      return !EMPLOYMENT_VOCABULARY.test(value);
    }).map(({ locale, key, reason }) => `${locale} ${key} (${reason})`);

    expect(stale).toEqual([]);
  });
});
