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

/**
 * Owner ruling 2026-09-30: a member inside the free trial holds every Plus benefit, exactly as a
 * paying one. The membership screens branch on the running trial only to say when the first payment
 * falls and that nothing is charged before it — never to withhold a benefit, and never to say one
 * waits for a paid month.
 */
const TRIAL_GATES = ['@if (trialEndsOn()) {', '@if (trialEndsOn(); as trialEnd) {', '@if (!trialEndsOn()) {'];

interface Surface {
  name: string;
  template: string;
  benefits: string[];
}

const SURFACES: Surface[] = [
  {
    name: 'the page every Plus checkout lands on',
    template: join(MEMBERSHIP_DIR, 'membership-welcome.component.html'),
    benefits: [
      'pages.membership.from_now',
      'pages.membership.welcome_perk_discount',
      'pages.membership.welcome_perk_cancellation',
      'pages.membership.welcome_perk_express',
      'pages.membership.welcome_perk_recurring',
      'pages.membership.welcome_cta_setup_recurring',
    ],
  },
  {
    name: 'the membership card',
    template: join(MEMBERSHIP_DIR, 'membership-management.component.html'),
    benefits: [
      'pages.membership.what_you_get',
      'pages.membership.what_you_have_until',
      'pages.membership.perk_grace',
      'pages.membership.perk_recurring',
      'recurring_booking.membership_section_link_title',
    ],
  },
];

/** The copy that told a trialing member a benefit had not started. */
const WITHHOLDING_KEYS = [
  'pages.membership.trial_perks_title',
  'pages.membership.trial_perks_note',
  'pages.membership.perk_express_trial',
  'pages.order.express_waiver_trial',
];

/** What a trialing member reads about the trial, and the placeholders the code fills in each. */
const TRIAL_SENTENCES: Record<string, string[]> = {
  'pages.membership.trial_next_payment': ['{{date}}', '{{amount}}'],
  'pages.membership.cancel_dialog_message_trial': ['{{date}}'],
  'pages.membership.cancel_success_trial': [],
  'pages.membership.switch_dialog_message_trial': ['{{date}}', '{{price}}'],
  'pages.membership.switch_lead_trial': [],
  'pages.membership.trial_cancelled_lead': [],
  'pages.membership.welcome_subtitle_trial': [],
  'pages.plus.faq_express_a': ['{{days}}'],
  'pages.plus.faq_trial_a': ['{{days}}'],
};

const PAID_MONTH: Record<Locale, RegExp> = {
  en: /\bpaid\b/i,
  cs: /placen|zaplacen/i,
  sk: /platen|zaplaten/i,
  uk: /оплачен|оплати/i,
  ru: /оплачен|оплат/i,
};

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as Record<
    string,
    unknown
  >;
}

function resolveKey(bundle: unknown, key: string): string | undefined {
  const value = key
    .split('.')
    .reduce<unknown>(
      (node, segment) =>
        node && typeof node === 'object' ? (node as Record<string, unknown>)[segment] : undefined,
      bundle
    );
  return typeof value === 'string' ? value : undefined;
}

/** The index of the brace that closes the one at `open`. */
function closingBrace(source: string, open: number): number {
  let depth = 0;
  for (let index = open; index < source.length; index++) {
    if (source[index] === '{') depth++;
    else if (source[index] === '}' && --depth === 0) return index;
  }
  return source.length;
}

/** Every block that renders on one side of the running trial only: each gate and its `@else`. */
function trialBranches(source: string): [number, number][] {
  const spans: [number, number][] = [];
  for (const gate of TRIAL_GATES) {
    for (let from = 0; ; ) {
      const start = source.indexOf(gate, from);
      if (start < 0) break;

      const end = closingBrace(source, start + gate.length - 1);
      spans.push([start, end]);
      from = end + 1;

      const elseMatch = /^\s*@else\s*\{/.exec(source.slice(end + 1));
      if (elseMatch) {
        const open = end + elseMatch[0].length;
        const elseEnd = closingBrace(source, open);
        spans.push([open, elseEnd]);
        from = elseEnd + 1;
      }
    }
  }
  return spans;
}

describe.each(SURFACES)('$name gives a trialing member every benefit', (surface) => {
  it('renders each benefit whether or not the trial is running', () => {
    const source = readFileSync(surface.template, 'utf8');
    const spans = trialBranches(source);

    for (const key of surface.benefits) {
      const at = source.indexOf(`'${key}'`);
      const branched = spans.some(([start, end]) => at > start && at < end);
      expect({ key, rendered: at >= 0, branched }).toEqual({ key, rendered: true, branched: false });
    }
  });
});

describe('the trial copy', () => {
  it.each(LOCALES)('no longer carries a sentence that withholds a benefit, in %s', (locale) => {
    const bundle = readLocale(locale);

    expect(WITHHOLDING_KEYS.filter((key) => resolveKey(bundle, key) !== undefined)).toEqual([]);
  });

  it.each(LOCALES)('never ties a benefit to a paid month, in %s', (locale) => {
    const bundle = readLocale(locale);

    for (const key of Object.keys(TRIAL_SENTENCES)) {
      const value = resolveKey(bundle, key) ?? '';
      expect({ key, value, namesPaidMonth: PAID_MONTH[locale].test(value) }).toEqual({
        key,
        value,
        namesPaidMonth: false,
      });
    }
  });

  it.each(LOCALES)('is present with the placeholders the code fills, in %s', (locale) => {
    const bundle = readLocale(locale);

    for (const [key, placeholders] of Object.entries(TRIAL_SENTENCES)) {
      const value = resolveKey(bundle, key) ?? '';
      expect({
        key,
        present: value.trim().length > 0,
        missing: placeholders.filter((placeholder) => !value.includes(placeholder)),
      }).toEqual({ key, present: true, missing: [] });
    }
  });
});
