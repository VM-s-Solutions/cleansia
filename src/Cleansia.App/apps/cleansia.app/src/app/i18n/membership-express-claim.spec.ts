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

const MEMBERSHIP_DIR = join(
  SOLUTION_DIR,
  'Cleansia.App/libs/cleansia-customer-features/profile/src/lib/membership'
);

const WIZARD_DIR = join(
  SOLUTION_DIR,
  'Cleansia.App/libs/cleansia-customer-features/order-wizard/src/lib/order-wizard'
);

/** Every stem the perk is worded with, across the five locales. */
const EXPRESS_STEMS = [/express/i, /expres/i, /експрес/i, /экспресс/i] as const;

/**
 * The claim `0c665c08` removed: express is a 2–4 h lead-time window, so a same-day promise waives
 * a surcharge that would never have applied. A 09:00 booking for 18:00 is same-day and already
 * free of the surcharge for everybody.
 */
const SAME_DAY_CLAIMS = [
  /same[-\s]?day/i,
  /týž den/i,
  /tentýž den/i,
  /ten istý deň/i,
  /v rovnaký deň/i,
  /ve stejný den/i,
  /того ж дня/i,
  /у той самий день/i,
  /в тот же день/i,
  /в этот же день/i,
] as const;

/**
 * `MembershipPlan.ExpressUpgradesPerMonth` is per-plan and admin-editable, so the copy states no
 * count of its own — every count a customer sees arrives from the server through `{{count}}`.
 *
 * The only numbers the copy MAY name are `BookingPolicy`'s two constants: the 20% surcharge rate
 * and the 2 h–4 h express window. Both are erased below by SHAPE rather than by value, because a
 * bare allow-list of the digits 2/4/20 also permits "2 free express bookings a month" — which is
 * precisely the hardcoded quota the rule exists to stop.
 */
const SURCHARGE_RATE = /\b20\s?%/g;
const LEAD_TIME_WINDOW = /\b2\D{1,6}4\b/g;
const QUOTA_WORDS = [
  /\btwo\b/i,
  /\bdva\b/i,
  /\bdvě\b/i,
  /\bdve\b/i,
  /\bдва\b/i,
  /\bдві\b/i,
] as const;

const MEMBERSHIP_TEMPLATES = [
  'membership-subscribe.component.html',
  'membership-welcome.component.html',
  'membership-management.component.html',
] as const;

/** The server field each template must branch on before it renders the claim. */
const SERVER_GATES: Record<(typeof MEMBERSHIP_TEMPLATES)[number], string> = {
  'membership-subscribe.component.html': 'allowsExpressUpgrade',
  'membership-welcome.component.html': 'expressWaiverAdvertised()',
  'membership-management.component.html': 'expressWaiverAvailable()',
};

const MEMBERSHIP_PERK_KEYS = [
  'benefit_express_title',
  'benefit_express_body',
  'perk_express',
  'perk_express_used',
  'perk_express_trial',
  'welcome_perk_express',
];

const BOOKING_FLOW_KEYS = [
  'slot_express_waived',
  'express_surcharge_waived_label',
  'express_waiver_available',
  'express_waiver_used',
  'express_waiver_trial',
];

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as Record<
    string,
    unknown
  >;
}

function block(locale: Record<string, unknown>, path: string[]): Record<string, unknown> {
  let node: Record<string, unknown> = locale;
  for (const segment of path) {
    node = (node[segment] ?? {}) as Record<string, unknown>;
  }
  return node;
}

/** Every leaf string under a block, keyed by its dotted path. */
function leafEntries(node: Record<string, unknown>, prefix = ''): [string, string][] {
  return Object.entries(node).flatMap(([key, value]) => {
    const path = prefix ? `${prefix}.${key}` : key;
    if (typeof value === 'string') return [[path, value] as [string, string]];
    if (value && typeof value === 'object') {
      return leafEntries(value as Record<string, unknown>, path);
    }
    return [];
  });
}

function expressEntries(locale: Locale): [string, string][] {
  const bundle = readLocale(locale);
  return [
    ...leafEntries(block(bundle, ['pages', 'membership']), 'pages.membership'),
    ...leafEntries(block(bundle, ['pages', 'order']), 'pages.order'),
    ...leafEntries(block(bundle, ['api', 'membership']), 'api.membership'),
  ].filter(([, value]) => EXPRESS_STEMS.some((stem) => stem.test(value)));
}

/**
 * The tripwire `0c665c08` set, now pointed at the claim's CONTENT instead of its existence.
 *
 * The mechanism shipped (T-0493 / ADR-0035), so the perk is advertised again — but the two
 * sentences that made the old claim false must not come back with it: express is a 2–4 h lead
 * window and not "same-day", and the monthly quota is per-plan configuration rather than a number
 * the copy is allowed to know. A scan that merely permits the word "express" would let either
 * back in, so both are asserted here, in all five locales and on every render site.
 *
 * `MembershipExpressClaimTest.kt` and `MembershipExpressClaimTests.swift` hold the same line for
 * the mobile clients.
 */
describe('the Plus express perk claim matches the mechanism (T-0544 / T-0514)', () => {
  it('advertises the perk again, non-empty in all five locales', () => {
    for (const locale of LOCALES) {
      const membership = block(readLocale(locale), ['pages', 'membership']);
      const missing = MEMBERSHIP_PERK_KEYS.filter(
        (key) => typeof membership[key] !== 'string' || !(membership[key] as string).trim()
      );
      expect({ locale, missing }).toEqual({ locale, missing: [] });
    }
  });

  it('discloses the waiver in the booking flow, non-empty in all five locales', () => {
    for (const locale of LOCALES) {
      const order = block(readLocale(locale), ['pages', 'order']);
      const missing = BOOKING_FLOW_KEYS.filter(
        (key) => typeof order[key] !== 'string' || !(order[key] as string).trim()
      );
      expect({ locale, missing }).toEqual({ locale, missing: [] });
    }
  });

  it('never claims same-day, in any locale', () => {
    for (const locale of LOCALES) {
      const offending = expressEntries(locale)
        .filter(([, value]) => SAME_DAY_CLAIMS.some((claim) => claim.test(value)))
        .map(([key, value]) => `${key}: ${value}`);
      expect({ locale, offending }).toEqual({ locale, offending: [] });
    }
  });

  it('never hardcodes the per-plan monthly quota, in any locale', () => {
    for (const locale of LOCALES) {
      const offending = expressEntries(locale)
        .filter(([, value]) => {
          const residue = value
            .replace(/\{\{[^}]*\}\}/g, ' ')
            .replace(SURCHARGE_RATE, ' ')
            .replace(LEAD_TIME_WINDOW, ' ');
          return /\d/.test(residue) || QUOTA_WORDS.some((word) => word.test(residue));
        })
        .map(([key, value]) => `${key}: ${value}`);
      expect({ locale, offending }).toEqual({ locale, offending: [] });
    }
  });

  it('names the 2-to-4-hour lead window wherever it sells the perk', () => {
    for (const locale of LOCALES) {
      const body = block(readLocale(locale), ['pages', 'membership'])[
        'benefit_express_body'
      ] as string;
      expect({ locale, namesWindow: /2\D{1,6}4/.test(body) }).toEqual({
        locale,
        namesWindow: true,
      });
    }
  });

  it('the five locales carry identical pages.membership and pages.order key sets', () => {
    for (const path of [
      ['pages', 'membership'],
      ['pages', 'order'],
    ]) {
      const enKeys = leafEntries(block(readLocale('en'), path))
        .map(([key]) => key)
        .sort();
      for (const locale of LOCALES) {
        if (locale === 'en') continue;
        const localeKeys = leafEntries(block(readLocale(locale), path))
          .map(([key]) => key)
          .sort();
        expect({ path, locale, keys: localeKeys }).toEqual({ path, locale, keys: enKeys });
      }
    }
  });

  it('the subscribe screen offers five perks again, the express one gated', () => {
    const source = readFileSync(
      join(MEMBERSHIP_DIR, 'membership-subscribe.component.html'),
      'utf8'
    );
    const perks = source.match(/<li class="membership-subscribe__perk">/g) ?? [];

    expect(perks).toHaveLength(5);
    expect(source).toContain('pages.membership.benefit_express_title');
  });

  it('every membership screen gates the claim on a server field', () => {
    for (const template of MEMBERSHIP_TEMPLATES) {
      const source = readFileSync(join(MEMBERSHIP_DIR, template), 'utf8');
      expect({ template, gated: source.includes(SERVER_GATES[template]) }).toEqual({
        template,
        gated: true,
      });
    }
  });

  it('the booking flow renders the waiver only on the server verdict, never on a client count', () => {
    const wizard = readFileSync(join(WIZARD_DIR, 'order-wizard.component.html'), 'utf8');
    const summary = readFileSync(
      join(WIZARD_DIR, 'components/wizard-summary-step.component.html'),
      'utf8'
    );

    for (const [name, source] of [
      ['order-wizard.component.html', wizard],
      ['wizard-summary-step.component.html', summary],
    ] as const) {
      expect({ name, waived: source.includes('facade.expressSurchargeWaived()') }).toEqual({
        name,
        waived: true,
      });
    }

    // The count is the server's `ExpressUpgradesRemaining`, rendered verbatim — a client that
    // adjusts it disagrees with the server the first time a cancellation releases a slot.
    expect(wizard).toContain('count: facade.expressUpgradesRemaining()');
    expect(wizard).not.toMatch(/expressUpgradesRemaining\(\)\s*[-+]/);
  });
});
