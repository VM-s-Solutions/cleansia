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

/** The customer-data and photo sweeps take cancelled orders as well as completed ones. */
const RETENTION_KEYS = [
  'pages.company_settings.descriptions.retention.order_pii.years',
  'pages.company_settings.descriptions.retention.order_photos.days',
];

const REFUND_COLUMN = 'pages.disputes_management.columns.refund_amount';

/** The refusal also covers a cleaner who has left or been erased, whose contract still reads approved. */
const REASSIGN_REFUSAL = 'api.order.reassign.employee_not_approved';

const COPY: Record<
  Locale,
  { completed: RegExp; cancelled: RegExp; requested: RegExp; notYet: RegExp; inactive: RegExp }
> = {
  en: {
    completed: /completed/i,
    cancelled: /cancelled/i,
    requested: /requested/i,
    notYet: /not approved yet/i,
    inactive: /no longer active/i,
  },
  cs: {
    completed: /dokončen/i,
    cancelled: /zrušen/i,
    requested: /požadovan/i,
    notYet: /zatím/i,
    inactive: /není aktivní/i,
  },
  sk: {
    completed: /dokončen/i,
    cancelled: /zrušen/i,
    requested: /požadovan/i,
    notYet: /zatiaľ/i,
    inactive: /nie je aktívny/i,
  },
  uk: {
    completed: /виконан|завершен/i,
    cancelled: /скасован|скасуван/i,
    requested: /запитан/i,
    notYet: /ще не/i,
    inactive: /не активний/i,
  },
  ru: {
    completed: /выполнен|завершени/i,
    cancelled: /отменён|отмены/i,
    requested: /запрошен/i,
    notYet: /ещё не|еще не/i,
    inactive: /не активен/i,
  },
};

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

describe('the admin order copy says what the platform does', () => {
  it.each(LOCALES)('names completed and cancelled orders in both order retention settings, in %s', (locale) => {
    const bundle = readLocale(locale);
    const { completed, cancelled } = COPY[locale];

    for (const key of RETENTION_KEYS) {
      const value = resolveKey(bundle, key);
      expect({ key, completed: completed.test(value), cancelled: cancelled.test(value) }).toEqual({
        key,
        completed: true,
        cancelled: true,
      });
    }
  });

  it.each(LOCALES)('heads the disputes refund column as the refund requested, in %s', (locale) => {
    expect(resolveKey(readLocale(locale), REFUND_COLUMN)).toMatch(COPY[locale].requested);
  });

  it.each(LOCALES)('refuses a departed cleaner without calling them not approved yet, in %s', (locale) => {
    const value = resolveKey(readLocale(locale), REASSIGN_REFUSAL);

    expect(value).not.toMatch(COPY[locale].notYet);
    expect(value).toMatch(COPY[locale].inactive);
  });
});
