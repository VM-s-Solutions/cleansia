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

const ORDER_DETAIL_TEMPLATE = join(
  SOLUTION_DIR,
  'Cleansia.App/libs/cleansia-customer-features/orders/src/lib/order-detail/order-detail.component.html'
);

/**
 * Nothing tracks a cleaner: "On my way" is a tap that carries the order id and no position. The
 * order detail's "Track live" button only led back to the list, so it is gone, and no locale
 * offers to follow a cleaner live.
 */
const LIVE_TRACKING: Record<Locale, RegExp> = {
  en: /track\w*\s+live|live[-\s]track/i,
  cs: /sledovat\s+živě/i,
  sk: /sledovať\s+naživo/i,
  uk: /стежити\s+наживо/i,
  ru: /следить\s+(?:онлайн|в реальном времени)/i,
};

function values(node: unknown): string[] {
  if (typeof node === 'string') return [node];
  if (node && typeof node === 'object') return Object.values(node).flatMap(values);
  return [];
}

describe('the customer app offers no live tracking', () => {
  it.each(LOCALES)('no %s value offers to follow the cleaner live', (locale) => {
    const bundle: unknown = JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8'));

    expect(values(bundle).filter((value) => LIVE_TRACKING[locale].test(value))).toEqual([]);
  });

  it('renders no track-live control on the order detail', () => {
    expect(readFileSync(ORDER_DETAIL_TEMPLATE, 'utf8')).not.toContain('track_live');
  });
});
