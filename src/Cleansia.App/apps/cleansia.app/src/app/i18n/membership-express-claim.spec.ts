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

const I18N_DIR = join(
  SOLUTION_DIR,
  'Cleansia.App/apps/cleansia.app/src/assets/i18n'
);

const MEMBERSHIP_DIR = join(
  SOLUTION_DIR,
  'Cleansia.App/libs/cleansia-customer-features/profile/src/lib/membership'
);

const MEMBERSHIP_TEMPLATES = [
  'membership-subscribe.component.html',
  'membership-welcome.component.html',
  'membership-management.component.html',
] as const;

// Every stem the perk was ever worded with, across the five locales.
const EXPRESS_STEMS = [/express/i, /експрес/i, /экспресс/i] as const;

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(
    readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')
  ) as Record<string, unknown>;
}

function membershipEntries(
  locale: Record<string, unknown>
): [string, string][] {
  const pages = (locale as { pages?: Record<string, unknown> }).pages ?? {};
  const membership =
    (pages as { membership?: Record<string, unknown> }).membership ?? {};
  return Object.entries(membership).filter(
    (entry): entry is [string, string] => typeof entry[1] === 'string'
  );
}

/**
 * The perk is advertised nowhere until the code that waives the surcharge exists.
 * `MembershipPlan.AllowsExpressUpgrade` is returned by the API and read by zero pricing code, and
 * `BookingPolicy` scopes express to a 2-4h lead window rather than "same day" — so every wording of
 * the perk shipped so far was false against a paying member. Removed on all three clients; this
 * pins the web half. Android's `MembershipPerks` KDoc and iOS's `MembershipPerksTests` hold the
 * same line for the mobile clients.
 */
describe('the Plus express perk is not advertised anywhere (T-0513)', () => {
  it('no membership copy names the express perk, in any of the five locales', () => {
    for (const locale of LOCALES) {
      const offending = membershipEntries(readLocale(locale))
        .filter(([, value]) => EXPRESS_STEMS.some((stem) => stem.test(value)))
        .map(([key, value]) => `${key}: ${value}`);
      expect({ locale, offending }).toEqual({ locale, offending: [] });
    }
  });

  it('the retired perk keys are gone from all five locales', () => {
    const retired = [
      'benefit_express_title',
      'benefit_express_body',
      'perk_express',
      'welcome_perk_express',
    ];
    for (const locale of LOCALES) {
      const present = membershipEntries(readLocale(locale))
        .map(([key]) => key)
        .filter((key) => retired.includes(key));
      expect({ locale, present }).toEqual({ locale, present: [] });
    }
  });

  it('the five locales carry identical pages.membership key sets', () => {
    const enKeys = membershipEntries(readLocale('en'))
      .map(([key]) => key)
      .sort();
    for (const locale of LOCALES) {
      if (locale === 'en') continue;
      const localeKeys = membershipEntries(readLocale(locale))
        .map(([key]) => key)
        .sort();
      expect({ locale, keys: localeKeys }).toEqual({ locale, keys: enKeys });
    }
  });

  it('no membership template renders an express perk or branches on the flag', () => {
    for (const template of MEMBERSHIP_TEMPLATES) {
      const source = readFileSync(join(MEMBERSHIP_DIR, template), 'utf8');
      expect({ template, express: /express/i.test(source) }).toEqual({
        template,
        express: false,
      });
      expect({ template, flag: source.includes('allowsExpressUpgrade') }).toEqual(
        { template, flag: false }
      );
    }
  });
});
