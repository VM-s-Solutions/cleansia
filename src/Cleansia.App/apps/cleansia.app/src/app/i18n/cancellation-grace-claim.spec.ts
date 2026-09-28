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
 * Owner ruling 2026-09-28, reversing 2026-09-24: after booking, cancelling is free for 15 minutes,
 * for 60 on a customer's first booking (account, e-mail or phone alike) and for 60 for an entitled
 * Plus member. The first-booking figure is the server's own constant, so it is read, not assumed.
 */
const STANDARD = policyMinutes('OopsWindowMinutesStandard');
const FIRST_BOOKING = policyMinutes('OopsWindowMinutesFirstBooking');
const PLUS = policyMinutes('OopsWindowMinutesPlus');
const GRACE_FIGURES = [STANDARD, FIRST_BOOKING, PLUS];

type Figure = 'standard' | 'first' | 'plus';
const FIGURE: Record<Figure, number> = { standard: STANDARD, first: FIRST_BOOKING, plus: PLUS };

/** A grace sentence cut into the part that speaks for the longer grace and the part that speaks for everyone. */
interface GraceSides {
  longer: string;
  standard: string;
}

const INSTEAD_OF = /instead of|namiesto|místo|замість|вместо/i;

/** "15 minutes … — 60 minutes on your first booking or with Cleansia Plus": the side naming Plus is the longer one's. */
function aroundDash(value: string): GraceSides {
  const parts = value.split('—');
  return {
    longer: parts.filter((part) => part.includes('Cleansia Plus')).join(' '),
    standard: parts.filter((part) => !part.includes('Cleansia Plus')).join(' '),
  };
}

/** "60 minutes …, instead of 15 minutes": a Plus perk, so what it replaces is the standard figure. */
function aroundInsteadOf(value: string): GraceSides {
  const match = INSTEAD_OF.exec(value);
  return match
    ? { longer: value.slice(0, match.index), standard: value.slice(match.index) }
    : { longer: '', standard: value };
}

/**
 * Every sentence that states the grace: the figures it owes, whether it names the first booking, and
 * — where it states two figures — how to tell whose each one is. A Plus perk speaks of EVERY booking
 * and names no first booking: the member's 60 minutes are the benefit on the bookings after the first.
 */
const GRACE_CLAIMS: {
  key: string;
  owes: Figure[];
  namesFirstBooking: boolean;
  sides?: (value: string) => GraceSides;
}[] = [
  { key: 'pages.home.rules.rethink_desc', owes: ['standard', 'first', 'plus'], namesFirstBooking: true, sides: aroundDash },
  { key: 'pages.order.cancel_policy_note', owes: ['standard', 'first', 'plus'], namesFirstBooking: true, sides: aroundDash },
  { key: 'pages.order.plus_perk_grace', owes: ['standard', 'plus'], namesFirstBooking: false, sides: aroundInsteadOf },
  { key: 'pages.plus.perk_cancel_body', owes: ['standard', 'plus'], namesFirstBooking: false, sides: aroundInsteadOf },
  { key: 'pages.membership.perk_grace', owes: ['standard', 'plus'], namesFirstBooking: false, sides: aroundInsteadOf },
  { key: 'pages.home.plus.perk_grace', owes: ['standard', 'plus'], namesFirstBooking: false, sides: aroundInsteadOf },
  { key: 'pages.plus.row_grace_without', owes: ['standard', 'first'], namesFirstBooking: true },
  { key: 'pages.plus.row_grace_with', owes: ['plus'], namesFirstBooking: false },
];

/** The sheet's line is the customer's OWN grace, so it carries the server's figure and no other. */
const SHEET_NOTES = [
  'pages.order_detail.cancellation.grace_note',
  'pages.track_order.cancellation.grace_note',
];

const MINUTE_FIGURE = /(\d+)\s*-?\s*(?:minutes?|minut|minút|хвилин|минут)/gi;
const CANCEL_STEMS = [/cancel/i, /zruš/i, /storn/i, /скасув/i, /отмен/i];
const FIRST_BOOKING_STEMS = [
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

const namesFirstBooking = (value: string) => FIRST_BOOKING_STEMS.some((stem) => stem.test(value));
const distinct = (numbers: number[]) => [...new Set(numbers)].sort((a, b) => a - b);

/** Every minute figure, plus any bare integer that is a grace figure ("15 minutes (60 on your first booking)"). */
const graceFiguresIn = (value: string) =>
  distinct([
    ...minuteFigures(value),
    ...[...value.matchAll(/\d+/g)].map((match) => Number(match[0])).filter((n) => GRACE_FIGURES.includes(n)),
  ]);

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

describe('the cancellation grace the customer is told matches BookingPolicy (15 / 60 on a first booking / 60 with Plus)', () => {
  it('reads a standard figure and two longer ones off the server', () => {
    expect(STANDARD).toBeGreaterThan(0);
    expect(FIRST_BOOKING).toBeGreaterThan(STANDARD);
    expect(PLUS).toBeGreaterThan(STANDARD);
  });

  it.each(LOCALES)('states exactly the figures each grace sentence owes, on the side they belong to, in %s', (locale) => {
    const bundle = readLocale(locale);

    for (const { key, owes, sides } of GRACE_CLAIMS) {
      const value = resolveKey(bundle, key);
      const split = sides?.(value);

      expect({
        key,
        stated: graceFiguresIn(value),
        longer: split ? distinct(minuteFigures(split.longer)) : null,
        standard: split ? distinct(minuteFigures(split.standard)) : null,
      }).toEqual({
        key,
        stated: distinct(owes.map((figure) => FIGURE[figure])),
        longer: split ? distinct(owes.filter((f) => f !== 'standard').map((f) => FIGURE[f])) : null,
        standard: split ? [STANDARD] : null,
      });
    }
  });

  it.each(LOCALES)('names the first booking wherever its grace is owed, and on no Plus perk, in %s', (locale) => {
    const bundle = readLocale(locale);

    for (const { key, namesFirstBooking: owed } of GRACE_CLAIMS) {
      expect({ key, firstBooking: namesFirstBooking(resolveKey(bundle, key)) }).toEqual({
        key,
        firstBooking: owed,
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

  it.each(LOCALES)('states no grace but the policy figures, and a first booking only with its own, anywhere in %s', (locale) => {
    const graceSentences = leafEntries(readLocale(locale)).filter(
      ([, value]) => minuteFigures(value).length > 0 && CANCEL_STEMS.some((stem) => stem.test(value))
    );

    expect(graceSentences.length).toBeGreaterThanOrEqual(3);
    for (const [key, value] of graceSentences) {
      expect({
        key,
        strays: minuteFigures(value).filter((n) => !GRACE_FIGURES.includes(n)),
        firstBookingWithoutItsFigure: namesFirstBooking(value) && !minuteFigures(value).includes(FIRST_BOOKING),
      }).toEqual({ key, strays: [], firstBookingWithoutItsFigure: false });
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
      ['plus/src/lib/plus/plus-page.component.html', 'pages.plus.row_grace_without'],
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
