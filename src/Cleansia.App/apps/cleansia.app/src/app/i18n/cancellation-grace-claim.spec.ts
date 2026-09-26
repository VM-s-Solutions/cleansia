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

const BOOKING_POLICY = join(
  SOLUTION_DIR,
  'Cleansia.Core.AppServices/Features/Orders/BookingPolicy.cs'
);

function policyMinutes(name: string): number {
  const match = new RegExp(`public\\s+const\\s+int\\s+${name}\\s*=\\s*(\\d+)\\s*;`).exec(
    readFileSync(BOOKING_POLICY, 'utf8')
  );
  if (!match) throw new Error(`BookingPolicy.${name} not found — the parser needs updating`);
  return Number(match[1]);
}

/**
 * Owner ruling 2026-09-24: after booking, cancelling is free for 15 minutes, and for 60 for an
 * entitled Plus member. Guests and first-time customers get the standard 15 — the first-time 60
 * the copy once implied is gone from the server, so it must not come back in any locale.
 */
const STANDARD = policyMinutes('OopsWindowMinutesStandard');
const PLUS = policyMinutes('OopsWindowMinutesPlus');

/** A grace sentence cut into the part that speaks for Plus members and the part that speaks for everyone. */
interface GraceSides {
  plus: string;
  standard: string;
}

const INSTEAD_OF = /instead of|namiesto|místo|замість|вместо/i;

/** "15 minutes … — 60 minutes with Cleansia Plus": the side that names Plus is the Plus figure's. */
function aroundDash(value: string): GraceSides {
  const parts = value.split('—');
  return {
    plus: parts.filter((part) => part.includes('Cleansia Plus')).join(' '),
    standard: parts.filter((part) => !part.includes('Cleansia Plus')).join(' '),
  };
}

/** "60 minutes …, instead of 15 minutes": a Plus perk, so what it replaces is the standard figure. */
function aroundInsteadOf(value: string): GraceSides {
  const match = INSTEAD_OF.exec(value);
  return match
    ? { plus: value.slice(0, match.index), standard: value.slice(match.index) }
    : { plus: '', standard: value };
}

const onlyStandard = (value: string): GraceSides => ({ plus: '', standard: value });
const onlyPlus = (value: string): GraceSides => ({ plus: value, standard: '' });

/** Every sentence that states the grace, which figures it owes, and how to tell whose each one is. */
const GRACE_CLAIMS: {
  key: string;
  owes: 'both' | 'standard' | 'plus';
  sides: (value: string) => GraceSides;
}[] = [
  { key: 'pages.home.rules.rethink_desc', owes: 'both', sides: aroundDash },
  { key: 'pages.order.cancel_policy_note', owes: 'both', sides: aroundDash },
  { key: 'pages.order.plus_perk_grace', owes: 'both', sides: aroundInsteadOf },
  { key: 'pages.plus.perk_cancel_body', owes: 'both', sides: aroundInsteadOf },
  { key: 'pages.membership.perk_grace', owes: 'both', sides: aroundInsteadOf },
  { key: 'pages.home.plus.perk_grace', owes: 'both', sides: aroundInsteadOf },
  { key: 'pages.plus.row_grace_without', owes: 'standard', sides: onlyStandard },
  { key: 'pages.plus.row_grace_with', owes: 'plus', sides: onlyPlus },
];

/** The sheet's line is the customer's OWN grace, so it carries the server's figure and no other. */
const SHEET_NOTES = [
  'pages.order_detail.cancellation.grace_note',
  'pages.track_order.cancellation.grace_note',
];

const MINUTE_FIGURE = /(\d+)\s*-?\s*(?:minutes?|minut|minút|хвилин|минут)/gi;
const CANCEL_STEMS = [/cancel/i, /zruš/i, /storn/i, /скасув/i, /отмен/i];
const FIRST_TIME_STEMS = [
  /first[-\s]time/i,
  /first (?:booking|order)/i,
  /new customer/i,
  /prvn\S* (?:objedn|zákazn|úklid)/i,
  /prv\S* (?:objedn|zákazn|upratov)/i,
  /nov\S* zákazn/i,
  /перш\S* (?:замовлен|прибиран)/i,
  /нов\S* клієнт/i,
  /перв\S* (?:заказ|уборк)/i,
  /нов\S* клиент/i,
];

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

function leafEntries(node: unknown, prefix = ''): [string, string][] {
  if (typeof node === 'string') return [[prefix, node]];
  if (!node || typeof node !== 'object') return [];
  return Object.entries(node as Record<string, unknown>).flatMap(([key, value]) =>
    leafEntries(value, prefix ? `${prefix}.${key}` : key)
  );
}

function minuteFigures(text: string): number[] {
  return [...text.matchAll(MINUTE_FIGURE)].map((match) => Number(match[1]));
}

function template(relative: string): string {
  return readFileSync(join(FEATURES_DIR, relative), 'utf8').replace(/\s+/g, ' ');
}

describe('the cancellation grace the customer is told matches BookingPolicy (15 / 60 minutes)', () => {
  it('reads two different figures off the server', () => {
    expect(STANDARD).toBeGreaterThan(0);
    expect(PLUS).toBeGreaterThan(STANDARD);
  });

  it.each(LOCALES)('states each figure a grace sentence owes in minutes, on the side it belongs to, in %s', (locale) => {
    const bundle = readLocale(locale);

    for (const { key, owes, sides } of GRACE_CLAIMS) {
      const value = resolveKey(bundle, key);
      const { plus, standard } = sides(value);
      const integers = [...value.matchAll(/\d+/g)].map((match) => Number(match[0]));
      const owed = owes === 'both' ? [STANDARD, PLUS] : owes === 'standard' ? [STANDARD] : [PLUS];

      expect({
        key,
        plus: minuteFigures(plus),
        standard: minuteFigures(standard),
        unowed: [STANDARD, PLUS].filter((n) => !owed.includes(n) && integers.includes(n)),
      }).toEqual({
        key,
        plus: owes === 'standard' ? [] : [PLUS],
        standard: owes === 'plus' ? [] : [STANDARD],
        unowed: [],
      });
    }
  });

  it.each(LOCALES)('gives the cancellation sheets the server figure and no number of their own, in %s', (locale) => {
    const bundle = readLocale(locale);

    for (const key of SHEET_NOTES) {
      const value = resolveKey(bundle, key);
      expect({ key, placeholder: value.includes('{{minutes}}') }).toEqual({ key, placeholder: true });
      expect({ key, baked: value.replace(/\{\{\s*\w+\s*\}\}/g, '').match(/\d/g) ?? [] }).toEqual({
        key,
        baked: [],
      });
    }
  });

  it.each(LOCALES)('states no grace other than the two, and no first-time grace, anywhere in %s', (locale) => {
    const graceSentences = leafEntries(readLocale(locale)).filter(
      ([, value]) => minuteFigures(value).length > 0 && CANCEL_STEMS.some((stem) => stem.test(value))
    );

    expect(graceSentences.length).toBeGreaterThanOrEqual(3);
    for (const [key, value] of graceSentences) {
      expect({ key, strays: minuteFigures(value).filter((n) => n !== STANDARD && n !== PLUS) }).toEqual({
        key,
        strays: [],
      });
      expect({ key, firstTime: FIRST_TIME_STEMS.some((stem) => stem.test(value)) }).toEqual({
        key,
        firstTime: false,
      });
    }
  });

  it('binds each cancellation sheet to the grace the preview returned', () => {
    expect(template('orders/src/lib/order-detail/order-detail.component.html')).toContain(
      "'pages.order_detail.cancellation.grace_note' | translate: { minutes: preview.oopsWindowMinutes }"
    );
    expect(template('orders/src/lib/track-order/track-order.component.html')).toContain(
      "'pages.track_order.cancellation.grace_note' | translate: { minutes: preview.oopsWindowMinutes }"
    );
  });

  it('renders the grace on every surface that states it', () => {
    for (const [file, key] of [
      ['home/src/lib/home/components/rules/rules.component.html', 'pages.home.rules.rethink_desc'],
      ['order-wizard/src/lib/order-wizard/order-wizard.component.html', 'pages.order.cancel_policy_note'],
      ['order-wizard/src/lib/order-wizard/order-wizard.component.html', 'pages.order.plus_perk_grace'],
      ['plus/src/lib/plus/plus-page.component.html', 'pages.plus.perk_cancel_body'],
      ['plus/src/lib/plus/plus-page.component.html', 'pages.plus.row_grace_with'],
      ['profile/src/lib/membership/membership-management.component.html', 'pages.membership.perk_grace'],
      ['home/src/lib/home/components/plus/plus.component.html', 'pages.home.plus.perk_grace'],
    ]) {
      expect({ file, key, rendered: template(file).includes(`'${key}' | translate`) }).toEqual({
        file,
        key,
        rendered: true,
      });
    }
  });
});
