import { readFileSync } from 'fs';
import { join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
type Locale = (typeof LOCALES)[number];

const I18N_DIR = join(__dirname, '../../assets/i18n');

/**
 * `LoyaltyAccount.RevokePoints` lowers `LifetimePoints` and `RecomputeTier` resolves the tier from
 * that number every time, so a refund or an admin adjustment can move a customer down a tier. The
 * page said the opposite in five locales.
 */
const RATCHET_CLAIMS = [
  /never (?:drops?|falls?|go(?:es)? down|decreases|be lowered)|won'?t (?:drop|fall|go down)/i,
  /nikdy nekles/i,
  /ніколи не (?:опустит|знизит|впаде)/i,
  /никогда не (?:опустит|снизит|понизит|упад)/i,
] as const;

const FALL_STEMS: Record<Locale, RegExp> = {
  en: /can drop/i,
  cs: /může klesnout/i,
  sk: /môže klesnúť/i,
  uk: /може знизитися/i,
  ru: /может понизиться/i,
};

const CURRENT_TOTAL_STEMS: Record<Locale, RegExp> = {
  en: /current points total/i,
  cs: /součtu vašich bodů/i,
  sk: /súčtu vašich bodov/i,
  uk: /поточною сумою/i,
  ru: /текущей сумме/i,
};

/**
 * The rewards page prints whatever `PerksJson` the server sends, through `perk.labelKey |
 * translate`. A seeded perk whose string is gone renders as a raw key on every customer's ladder.
 *
 * The seed sits outside the Nx workspace, so CI runs this only when cleansia.app is affected; a
 * seed-only change is not caught here.
 */
const SEED_FILE = join(__dirname, '../../../../../../../sql-scripts/insert_seed_data.sql');

/** A `PerksJson` literal and the tier its INSERT guard or UPDATE filter names. */
const SEED_PERK_LIST = /'(\[[^']*\])'\s*WHERE[^;]*?"Tier"\s*=\s*(\d+)/g;

function resolveKey(bundle: unknown, key: string): unknown {
  return key
    .split('.')
    .reduce<unknown>(
      (node, segment) =>
        node && typeof node === 'object' ? (node as Record<string, unknown>)[segment] : undefined,
      bundle
    );
}

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

describe('the rewards copy says the tier follows the points balance', () => {
  it('promises nowhere that the tier cannot fall, in any locale', () => {
    for (const locale of LOCALES) {
      const offending = leafEntries(block(readLocale(locale), ['pages', 'rewards']), 'pages.rewards')
        .filter(([, value]) => RATCHET_CLAIMS.some((claim) => claim.test(value)))
        .map(([key, value]) => `${key}: ${value}`);

      expect({ locale, offending }).toEqual({ locale, offending: [] });
    }
  });

  it('says on the balance note that the tier can go down, in all five locales', () => {
    for (const locale of LOCALES) {
      const note = String(block(readLocale(locale), ['pages', 'rewards'])['spent_note']);

      expect({ locale, saysItCanFall: FALL_STEMS[locale].test(note) }).toEqual({
        locale,
        saysItCanFall: true,
      });
    }
  });

  it('says on the ladder that the tier follows the current points total, in all five locales', () => {
    for (const locale of LOCALES) {
      const lead = String(block(readLocale(locale), ['pages', 'rewards'])['tier_ladder_lead']);

      expect({ locale, followsTotal: CURRENT_TOTAL_STEMS[locale].test(lead) }).toEqual({
        locale,
        followsTotal: true,
      });
    }
  });
});

describe('the seeded tiers advertise only perks the rewards page can name', () => {
  const seed = readFileSync(SEED_FILE, 'utf8');
  const perkLists = [...seed.matchAll(SEED_PERK_LIST)].map((match) => ({
    tier: Number(match[2]),
    labelKeys: (JSON.parse(match[1]) as { labelKey?: unknown }[]).map((perk) =>
      String(perk.labelKey)
    ),
  }));
  const seededPerks = [...new Set(perkLists.flatMap((list) => list.labelKeys))];

  it('reads a perk list for every tier, and every perk the seed names', () => {
    expect(perkLists.map((list) => list.tier)).toEqual(expect.arrayContaining([1, 2, 3, 4]));
    expect(perkLists.flatMap((list) => list.labelKeys)).toHaveLength(
      (seed.match(/"labelKey"/g) ?? []).length
    );
  });

  it('has copy for every seeded perk, in all five locales', () => {
    for (const locale of LOCALES) {
      const bundle = readLocale(locale);
      const missing = seededPerks.filter((key) => {
        const value = resolveKey(bundle, key);
        return typeof value !== 'string' || !value.trim();
      });

      expect({ locale, missing }).toEqual({ locale, missing: [] });
    }
  });
});
