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

const FEATURES_DIR = join(SOLUTION_DIR, 'Cleansia.App/libs/cleansia-customer-features');

/**
 * `MembershipPlan.TrialPeriodDays` is 0 on both seeded plans and the admin validators
 * (`CreateMembershipPlan`, `UpdateMembershipPlan`) refuse any other value, so `trialDays()` is 0
 * for every customer today and checkout bills on subscribe. Every sentence that sells a trial has
 * to sit behind its surface's `trialDays() > 0` gate, or it promises something no plan delivers.
 *
 * A bare "free" stem is deliberately absent: "free cancellation" is a real perk rendered ungated.
 */
const TRIAL_STEMS = [
  /trial/i,
  /zkušeb/i,
  /skúšob/i,
  /пробн/i,
  /\btry\b/i,
  /vyzkouš/i,
  /zkuste/i,
  /vyskúš/i,
  /skúste/i,
  /спробу/i,
  /попроб/i,
  /first payment/i,
  /první platb/i,
  /prvá platb/i,
  /перш\S* платіж/i,
  /перв\S* плат[её]ж/i,
  /\{\{days\}\}/,
  /\d+\s*(?:days?|dn|дн)/i,
] as const;

interface Surface {
  name: string;
  template: string;
  gate: string | null;
  mustBeGated: string[];
  mustBeUngated: string[];
  blocks: string[];
}

const SURFACES: Surface[] = [
  {
    name: 'the Plus page',
    template: join(FEATURES_DIR, 'plus/src/lib/plus/plus-page.component.html'),
    gate: '@if (facade.trialDays() > 0) {',
    mustBeGated: ['pages.plus.cta_trial', 'pages.plus.cta_try_free', 'pages.plus.closing_title'],
    mustBeUngated: [
      'pages.plus.cta_subscribe',
      'pages.plus.closing_title_no_trial',
      'pages.plus.closing_text',
      'pages.plus.closing_cta',
    ],
    blocks: ['pages.plus'],
  },
  {
    name: 'the home page Plus band',
    template: join(FEATURES_DIR, 'home/src/lib/home/components/plus/plus.component.html'),
    gate: '@if (facts.trialDays() > 0) {',
    mustBeGated: ['pages.home.plus.title', 'pages.home.plus.cta'],
    mustBeUngated: ['pages.home.plus.title_no_trial', 'pages.home.plus.cta_no_trial'],
    blocks: ['pages.home.plus'],
  },
  {
    name: 'the recurring-bookings paywall',
    template: join(
      FEATURES_DIR,
      'recurring-bookings/src/lib/recurring-bookings-list/recurring-bookings-list.component.html'
    ),
    gate: null,
    mustBeGated: [],
    mustBeUngated: ['recurring_booking.gate_cta', 'recurring_booking.gate_lead'],
    blocks: ['recurring_booking'],
  },
  {
    name: 'the page every Plus checkout lands on',
    template: join(FEATURES_DIR, 'profile/src/lib/membership/membership-welcome.component.html'),
    gate: '@if (expressWaiverPendingTrial()) {',
    mustBeGated: ['pages.membership.welcome_express_after_trial'],
    mustBeUngated: ['pages.membership.welcome_title', 'pages.membership.welcome_perk_express'],
    blocks: [],
  },
];

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as Record<
    string,
    unknown
  >;
}

function resolveKey(bundle: unknown, key: string): unknown {
  return key
    .split('.')
    .reduce<unknown>(
      (node, segment) =>
        node && typeof node === 'object' ? (node as Record<string, unknown>)[segment] : undefined,
      bundle
    );
}

/** The [start, end] index of every gated block, by brace matching. */
function gatedSpans(source: string, gate: string | null): [number, number][] {
  const spans: [number, number][] = [];
  if (!gate) return spans;

  for (let from = 0; ; ) {
    const start = source.indexOf(gate, from);
    if (start < 0) return spans;

    let depth = 0;
    let index = start + gate.length - 1;
    for (; index < source.length; index++) {
      if (source[index] === '{') depth++;
      else if (source[index] === '}' && --depth === 0) break;
    }

    spans.push([start, index]);
    from = index + 1;
  }
}

function renderedKeys(surface: Surface): { gated: Set<string>; ungated: Set<string> } {
  const source = readFileSync(surface.template, 'utf8');
  const spans = gatedSpans(source, surface.gate);
  const gated = new Set<string>();
  const ungated = new Set<string>();

  for (const match of source.matchAll(/'([a-z0-9_]+(?:\.[a-z0-9_]+)+)'\s*\|\s*translate/g)) {
    const at = match.index ?? 0;
    const inside = spans.some(([start, end]) => at > start && at < end);
    (inside ? gated : ungated).add(match[1]);
  }

  return { gated, ungated };
}

describe.each(SURFACES)('$name sells no trial while no plan carries one', (surface) => {
  // Anti-false-green: a scanner that found no gate, or put every key behind one, would pass the
  // claim below while reading nothing.
  it('tells the gated copy from the copy it always renders', () => {
    const { gated, ungated } = renderedKeys(surface);

    for (const key of surface.mustBeGated) {
      expect({ key, gated: gated.has(key), ungated: ungated.has(key) }).toEqual({
        key,
        gated: true,
        ungated: false,
      });
    }
    for (const key of surface.mustBeUngated) {
      expect({ key, ungated: ungated.has(key) }).toEqual({ key, ungated: true });
    }
  });

  it('mentions no trial in a string it always renders, in any locale', () => {
    const { ungated } = renderedKeys(surface);

    for (const locale of LOCALES) {
      const bundle = readLocale(locale);
      const offending = [...ungated]
        .map((key) => [key, String(resolveKey(bundle, key) ?? '')] as const)
        .filter(([, value]) => TRIAL_STEMS.some((stem) => stem.test(value)))
        .map(([key, value]) => `${key}: ${value}`);

      expect({ locale, offending }).toEqual({ locale, offending: [] });
    }
  });

  it('finds every key it renders in all five locales', () => {
    const { gated, ungated } = renderedKeys(surface);

    for (const locale of LOCALES) {
      const bundle = readLocale(locale);
      const missing = [...gated, ...ungated].filter((key) => {
        const value = resolveKey(bundle, key);
        return typeof value !== 'string' || !value.trim();
      });

      expect({ locale, missing }).toEqual({ locale, missing: [] });
    }
  });

  it('carries identical key sets for its blocks in the five locales', () => {
    for (const block of surface.blocks) {
      const keysOf = (locale: Locale) =>
        Object.keys((resolveKey(readLocale(locale), block) ?? {}) as Record<string, unknown>).sort();
      const enKeys = keysOf('en');

      for (const locale of LOCALES) {
        expect({ block, locale, keys: keysOf(locale) }).toEqual({ block, locale, keys: enKeys });
      }
    }
  });
});
