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
 * `MembershipPlan.TrialPeriodDays` is per plan and an admin may set it to 0, and a customer who has
 * had their one trial gets none (`GetMyMembership.trialEligible`). Every sentence that sells a trial
 * has to sit behind its surface's trial-days gate, or it promises something checkout will not give.
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
  gates: string[];
  mustBeGated: string[];
  mustBeUngated: string[];
  blocks: string[];
}

const SURFACES: Surface[] = [
  {
    name: 'the Plus page',
    template: join(FEATURES_DIR, 'plus/src/lib/plus/plus-page.component.html'),
    gates: [
      '@if (facade.trialDays() > 0) {',
      '@if (facade.monthlyTrialDays() > 0) {',
      '@if (facade.yearlyTrialDays() > 0) {',
      '@if (facade.trialOnEveryPlan()) {',
    ],
    mustBeGated: [
      'pages.plus.plans_footnote',
      'pages.plus.cta_trial',
      'pages.plus.cta_try_free',
      'pages.plus.closing_title',
      'pages.plus.price_note_trial',
      'pages.plus.plan_trial_monthly',
      'pages.plus.plan_trial_yearly',
    ],
    mustBeUngated: [
      'pages.plus.cta_subscribe',
      'pages.plus.closing_title_no_trial',
      'pages.plus.closing_text',
      'pages.plus.closing_cta',
      'pages.plus.price_note',
    ],
    blocks: ['pages.plus'],
  },
  {
    name: 'the home page Plus band',
    template: join(FEATURES_DIR, 'home/src/lib/home/components/plus/plus.component.html'),
    gates: ['@if (facade.trialDays() > 0) {'],
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
    gates: [],
    mustBeGated: [],
    mustBeUngated: ['recurring_booking.gate_cta', 'recurring_booking.gate_lead'],
    blocks: ['recurring_booking'],
  },
  {
    name: 'the page every Plus checkout lands on',
    template: join(FEATURES_DIR, 'profile/src/lib/membership/membership-welcome.component.html'),
    gates: ['@if (trialEndsOn()) {'],
    mustBeGated: ['pages.membership.welcome_subtitle_trial'],
    mustBeUngated: [
      'pages.membership.welcome_title',
      'pages.membership.welcome_subtitle',
      'pages.membership.welcome_perk_express',
    ],
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

/** The [start, end] index of every block behind one of the gates, by brace matching. */
function gatedSpans(source: string, gates: string[]): [number, number][] {
  const spans: [number, number][] = [];

  for (const gate of gates) {
    for (let from = 0; ; ) {
      const start = source.indexOf(gate, from);
      if (start < 0) break;

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
  return spans;
}

function renderedKeys(surface: Surface): { gated: Set<string>; ungated: Set<string> } {
  const source = readFileSync(surface.template, 'utf8');
  const spans = gatedSpans(source, surface.gates);
  const gated = new Set<string>();
  const ungated = new Set<string>();

  for (const match of source.matchAll(/'([a-z0-9_]+(?:\.[a-z0-9_]+)+)'\s*\|\s*translate/g)) {
    const at = match.index ?? 0;
    const inside = spans.some(([start, end]) => at > start && at < end);
    (inside ? gated : ungated).add(match[1]);
  }

  return { gated, ungated };
}

describe.each(SURFACES)('$name sells a trial only where one is offered', (surface) => {
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

// A plan's own trial column says nothing about whether THIS customer may still have a trial, so a
// subscribe surface reads the offered days from its facade and never the column itself.
describe('the booking and Plus pages offer the trial this customer can still have', () => {
  it.each([
    'plus/src/lib/plus/plus-page.component.html',
    'order-wizard/src/lib/order-wizard/order-wizard.component.html',
  ])('%s never reads the plan trial column', (template) => {
    expect(readFileSync(join(FEATURES_DIR, template), 'utf8')).not.toContain('trialPeriodDays');
  });
});

// A sentence under all the plan cards at once speaks for each of them, and each plan carries its
// own trial — so it states billing terms only when every card shares them, never one card's.
describe('a sentence speaking for every plan card states only the terms they share', () => {
  const PLUS = 'plus/src/lib/plus/plus-page.component.html';
  const WIZARD = 'order-wizard/src/lib/order-wizard/order-wizard.component.html';

  it.each([
    [PLUS, '@if (facade.trialOnEveryPlan()) {', 'pages.plus.plans_footnote'],
    [PLUS, '@else if (facade.trialOnNoPlan()) {', 'pages.plus.plans_footnote_no_trial'],
    [WIZARD, '@if (trialDaysOnEveryPlan() > 0) {', 'pages.order.plus_lead_after'],
    [WIZARD, '@else if (trialDaysOnEveryPlan() > 0) {', 'pages.order.plus_lead_plain'],
    [WIZARD, '@else if (trialOnNoPlan()) {', 'pages.order.plus_lead_after_no_trial'],
    [WIZARD, '@else if (trialOnNoPlan()) {', 'pages.order.plus_lead_plain_no_trial'],
  ])('%s renders behind %s the key %s', (template, gate, key) => {
    const { gated } = renderedKeys({
      name: template,
      template: join(FEATURES_DIR, template),
      gates: [gate],
      mustBeGated: [],
      mustBeUngated: [],
      blocks: [],
    });

    expect([...gated]).toContain(key);
  });
});
