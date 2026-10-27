import { existsSync, readFileSync } from 'fs';
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
const TRACK_TEMPLATE = join(
  SOLUTION_DIR,
  'Cleansia.App/libs/cleansia-customer-features/orders/src/lib/track-order/track-order.component.html'
);

/** The three lines on the track page that promise what an account shows. */
const PROMISES = ['guest_limit', 'sign_in', 'guest_cta_lead'] as const;

function trackOrderCopy(locale: string): Record<string, unknown> {
  const bundle = JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as {
    pages?: { track_order?: Record<string, unknown> };
  };
  return bundle.pages?.track_order ?? {};
}

/**
 * An account lists a booking made without one only when the booking carries the account's own e-mail
 * (owner ruling 2026-10-26), so a promise that the account shows "every order" is true only
 * with that condition: an account opened under another address, an Apple relay among them, sees none.
 */
describe('the track page promises the account only the bookings made with its e-mail', () => {
  it.each(LOCALES)('names the e-mail in each of the three lines in %s', (locale) => {
    const copy = trackOrderCopy(locale);
    const missing = PROMISES.filter((key) => {
      const line = copy[key];
      return typeof line !== 'string' || !/e-mail/i.test(line);
    });

    expect({ locale, missing }).toEqual({ locale, missing: [] });
  });

  it('renders the guarded keys, not a re-worded copy of them', () => {
    const template = readFileSync(TRACK_TEMPLATE, 'utf8');

    for (const key of PROMISES) {
      expect(template).toContain(`pages.track_order.${key}`);
    }
  });
});
