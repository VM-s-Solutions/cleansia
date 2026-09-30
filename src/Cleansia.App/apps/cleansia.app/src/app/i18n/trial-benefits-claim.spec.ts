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
 * Owner ruling 2026-09-08: no Plus benefit before payment. The discount, the notice window, the
 * 60-minute grace and the express waiver all follow the PAID entitlement, which a running trial is
 * not — yet `hasMembership` counts one. So every membership screen branches on the running trial,
 * the sentences that say a benefit is on now render only outside that branch, and what the branch
 * says instead names the paid month as the start.
 */
const TRIAL_GATE = '@if (trialEndsOn()) {';
/** A block shown only outside the running trial — the recurring schedule a trialist cannot create. */
const PAID_GATE = '@if (!trialEndsOn()) {';

interface Surface {
  name: string;
  template: string;
  benefitsOnNow: string[];
  trialOnly: string[];
}

const SURFACES: Surface[] = [
  {
    name: 'the page every Plus checkout lands on',
    template: join(MEMBERSHIP_DIR, 'membership-welcome.component.html'),
    benefitsOnNow: [
      'pages.membership.welcome_subtitle',
      'pages.membership.from_now',
      'pages.membership.welcome_cta_setup_recurring',
    ],
    trialOnly: [
      'pages.membership.welcome_subtitle_trial',
      'pages.membership.trial_perks_title',
      'pages.membership.trial_perks_note',
    ],
  },
  {
    name: 'the membership card',
    template: join(MEMBERSHIP_DIR, 'membership-management.component.html'),
    benefitsOnNow: [
      'pages.membership.what_you_get',
      'pages.membership.what_you_have_until',
      'pages.membership.nothing_retroactive',
      'recurring_booking.membership_section_link_title',
    ],
    trialOnly: [
      'pages.membership.trial_perks_title',
      'pages.membership.trial_perks_note',
      'pages.membership.trial_cancelled_lead',
    ],
  },
];

/** What a trialing member reads about the benefits: each must say they wait for a paid month. */
const TRIAL_SENTENCES = [
  'pages.membership.trial_perks_note',
  'pages.membership.trial_cancelled_lead',
  // Rendered on /plus behind the plan's trial days: the answer a would-be trialist reads.
  'pages.plus.faq_express_a',
  // Built in code for a trialing member — the cancel and switch dialogs and the cancel toast.
  'pages.membership.cancel_dialog_message_trial',
  'pages.membership.cancel_success_trial',
  'pages.membership.switch_dialog_message_trial',
  'pages.membership.switch_lead_trial',
];

const PAID_MONTH: Record<Locale, RegExp> = {
  en: /\bpaid\b/i,
  cs: /placen|zaplacen/i,
  sk: /platen|zaplaten/i,
  uk: /оплачен|оплати/i,
  ru: /оплачен|оплат/i,
};

/** A whole word or phrase — `\b` knows no letter outside ASCII, so it would miss "уже" or "běží". */
const phrase = (text: string): RegExp => new RegExp(`(?<!\\p{L})${text}(?!\\p{L})`, 'iu');

/** "Already running", "you have the discount": a benefit said to be on during the trial. */
const ON_NOW: Record<Locale, RegExp[]> = {
  en: ['already', 'right away', 'from now', 'you have the'].map(phrase),
  cs: ['už teď', 'hned', 'od teď', 'máte slevu', 'běží'].map(phrase),
  sk: ['už teraz', 'hneď', 'odteraz', 'máte zľavu', 'bežia?'].map(phrase),
  uk: ['вже', 'одразу', 'відтепер', 'у вас є'].map(phrase),
  ru: ['уже', 'сразу', 'с этого момента', 'у вас есть'].map(phrase),
};

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as Record<
    string,
    unknown
  >;
}

function resolveKey(bundle: unknown, key: string): string {
  const value = key
    .split('.')
    .reduce<unknown>(
      (node, segment) =>
        node && typeof node === 'object' ? (node as Record<string, unknown>)[segment] : undefined,
      bundle
    );
  return typeof value === 'string' ? value : '';
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

/**
 * Each trial branch, and the `@else` that follows it (the paid branch), by brace matching; and each
 * block gated on the trial NOT running, which is a paid branch too.
 */
function branches(source: string): { trial: [number, number][]; paid: [number, number][] } {
  const trial: [number, number][] = [];
  const paid: [number, number][] = [];

  for (let from = 0; ; ) {
    const start = source.indexOf(PAID_GATE, from);
    if (start < 0) break;
    const end = closingBrace(source, start + PAID_GATE.length - 1);
    paid.push([start, end]);
    from = end + 1;
  }

  for (let from = 0; ; ) {
    const start = source.indexOf(TRIAL_GATE, from);
    if (start < 0) return { trial, paid };

    const end = closingBrace(source, start + TRIAL_GATE.length - 1);
    trial.push([start, end]);

    const elseMatch = /^\s*@else\s*\{/.exec(source.slice(end + 1));
    if (elseMatch) {
      const open = end + elseMatch[0].length;
      const elseEnd = closingBrace(source, open);
      paid.push([open, elseEnd]);
      from = elseEnd + 1;
    } else {
      from = end + 1;
    }
  }
}

function renderedKeys(surface: Surface): {
  trial: Set<string>;
  paid: Set<string>;
  always: Set<string>;
} {
  const source = readFileSync(surface.template, 'utf8');
  const spans = branches(source);
  const inside = (at: number, list: [number, number][]) =>
    list.some(([start, end]) => at > start && at < end);
  const keys = { trial: new Set<string>(), paid: new Set<string>(), always: new Set<string>() };

  for (const match of source.matchAll(/'([a-z0-9_]+(?:\.[a-z0-9_]+)+)'\s*\|\s*translate/g)) {
    const at = match.index ?? 0;
    const bucket = inside(at, spans.trial) ? 'trial' : inside(at, spans.paid) ? 'paid' : 'always';
    keys[bucket].add(match[1]);
  }
  return keys;
}

describe.each(SURFACES)('$name promises a trialing member no benefit', (surface) => {
  it('says a benefit is on now only outside the running trial', () => {
    const { trial, paid, always } = renderedKeys(surface);

    for (const key of surface.benefitsOnNow) {
      expect({ key, paid: paid.has(key), trial: trial.has(key), always: always.has(key) }).toEqual({
        key,
        paid: true,
        trial: false,
        always: false,
      });
    }
  });

  it('says what a trialing member gets only inside the running trial', () => {
    const { trial, paid, always } = renderedKeys(surface);

    for (const key of surface.trialOnly) {
      expect({ key, trial: trial.has(key), paid: paid.has(key), always: always.has(key) }).toEqual({
        key,
        trial: true,
        paid: false,
        always: false,
      });
    }
  });
});

describe('the trial copy', () => {
  it.each(LOCALES)('names the paid month as when the benefits start, in %s', (locale) => {
    const bundle = readLocale(locale);

    for (const key of TRIAL_SENTENCES) {
      const value = resolveKey(bundle, key);
      expect({ key, value, namesPaidMonth: PAID_MONTH[locale].test(value) }).toEqual({
        key,
        value,
        namesPaidMonth: true,
      });
    }
  });

  it.each(LOCALES)('never says a benefit is already on during the trial, in %s', (locale) => {
    const bundle = readLocale(locale);
    const trialKeys = new Set([...TRIAL_SENTENCES, ...SURFACES.flatMap((s) => s.trialOnly)]);

    for (const key of trialKeys) {
      const value = resolveKey(bundle, key);
      expect({ key, value, onNow: ON_NOW[locale].filter((stem) => stem.test(value)).map(String) }).toEqual({
        key,
        value,
        onNow: [],
      });
    }
  });

  it.each(LOCALES)('is present in %s', (locale) => {
    const bundle = readLocale(locale);
    const trialKeys = [...TRIAL_SENTENCES, ...SURFACES.flatMap((s) => s.trialOnly)];

    expect(trialKeys.filter((key) => !resolveKey(bundle, key).trim())).toEqual([]);
  });
});
