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
 * The cleaner is self-employed and brings everything the job needs, included in the price, so the
 * platform owns no machines and vouches for no product. Nobody is background-checked and no insurance
 * policy exists yet, so the copy states neither. A booking is never moved: the customer cancels and
 * books again.
 */
const CLAIMS: { name: string; stem: RegExp }[] = [
  {
    name: 'the platform brings its own machines or equipment',
    stem: /\bour own|own (?:machines|equipment)|vlastn\S* (?:stroj|vybaven)|власн\S* (?:техн|обладн)|(?:сво\S*|собствен\S*) (?:техник|оборудован)/i,
  },
  { name: 'eco products', stem: /\beco\b|eco-|ekolog|еко-|екологі|эко-|экологи/i },
  {
    name: 'vetted, certified or background-checked cleaners',
    stem: /vetted|background[- ]?check|prověřen|overen\S* profesion|перевірен\S* фахів|проверенн\S* специал|пройш\S* перевірк|прош\S* проверк|certif\S* (?:professional|profesion|cleaner|uklízeč|pracovn)|сертифі\S* (?:фахів|прибиральн)|сертифиц\S* (?:специал|уборщ)/i,
  },
  { name: 'insurance', stem: /insur|pojišt|pojist|poist|страхув|страхов/i },
  {
    name: 'a booking can be rescheduled',
    stem: /reschedul|move (?:your |the |a )?(?:booking|clean|order)|change (?:your |the )?(?:booking|date)|přeplán|preplán|(?:přesun|presun)\S* (?:termín|objedn)|(?:změn|zmen)\S* termín|перенес\S* (?:замовлен|прибиран|бронюван|заказ|уборк)|(?:змін|измен)\S* дат/i,
  },
];

const RETIRED_KEYS = [
  'pages.home.services.eco_chip',
  'pages.services.trust_eco',
  'pages.services.vetted_professionals',
  'pages.orders.empty_trust_eco',
  'pages.services.trust_certified',
  'pages.orders.empty_trust_certified',
  'pages.order.trust_certified',
];

const SUPPLIES_STATED: Record<Locale, RegExp> = {
  en: /cleaner brings everything[^.]*included in the price/i,
  cs: /uklízeč přiveze vše potřebné[^.]*v ceně/i,
  sk: /upratovač prinesie všetko potrebné[^.]*v cene/i,
  uk: /прибиральник привозить усе необхідне[^.]*входить у ціну/i,
  ru: /уборщик привозит всё необходимое[^.]*входит в цену/i,
};

function readLocale(locale: Locale): unknown {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8'));
}

function leafEntries(node: unknown, prefix = ''): [string, string][] {
  if (typeof node === 'string') return [[prefix, node]];
  if (!node || typeof node !== 'object') return [];
  return Object.entries(node as Record<string, unknown>).flatMap(([key, value]) =>
    leafEntries(value, prefix ? `${prefix}.${key}` : key)
  );
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

function template(relative: string): string {
  return readFileSync(join(FEATURES_DIR, relative), 'utf8');
}

describe('the customer app promises only the service the platform sells', () => {
  it.each(LOCALES)('states none of the retired claims anywhere in %s', (locale) => {
    const entries = leafEntries(readLocale(locale));

    const found = CLAIMS.flatMap(({ name, stem }) =>
      entries.filter(([, value]) => stem.test(value)).map(([key]) => `${name}: ${key}`)
    );

    expect(found).toEqual([]);
  });

  it.each(LOCALES)('carries none of the retired keys, in %s', (locale) => {
    const bundle = readLocale(locale);

    expect(RETIRED_KEYS.filter((key) => resolveKey(bundle, key) !== undefined)).toEqual([]);
  });

  it.each(LOCALES)('says the cleaner brings everything, included in the price, in %s', (locale) => {
    const bundle = readLocale(locale);

    for (const key of ['pages.home.services.subtitle', 'pages.home.faq.q3.answer']) {
      expect({ key, stated: SUPPLIES_STATED[locale].test(resolveKey(bundle, key) ?? '') }).toEqual({
        key,
        stated: true,
      });
    }
  });

  it('renders the supplies chips and none of the retired keys', () => {
    const home = template('home/src/lib/home/components/services/services.component.html');
    const catalogue = template('services-catalog/src/lib/services-catalog/services-catalog.component.html');

    expect(home).toContain("'pages.home.services.supplies_chip' | translate");
    expect(catalogue).toContain("'pages.services.trust_supplies' | translate");
    expect(RETIRED_KEYS.filter((key) => home.includes(key) || catalogue.includes(key))).toEqual([]);
  });
});
