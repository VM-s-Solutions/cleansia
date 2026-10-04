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

const I18N_DIR = join(findSolutionDir(), 'Cleansia.App/apps/cleansia.app/src/assets/i18n');

/**
 * Cash needs no card and nothing charges a saved card (→ /product/business-rules#card-guarantee).
 * The consent sentence printed with the save tick is a versioned legal wording and is not read here.
 */
const SAVED_CARD_COPY = ['pages.profile.saved_cards', 'pages.order.save_card', 'api.saved_card'];

const CHARGE_CLAIM: Record<Locale, RegExp> = {
  en: /\bfees?\b|unpaid|\bcash\b|guarantee|charged/i,
  cs: /poplat|nezaplacen|hotovost|zaruč|garanc|účtován|strhnout/i,
  sk: /poplat|nezaplaten|hotovos|zaruč|garanc|účtovan|strhnúť/i,
  uk: /збор|комісі|неоплачен|готівк|гарант|стягу/i,
  ru: /сбор|комисси|неоплачен|наличн|гарант|списыва/i,
};

function readLocale(locale: Locale): unknown {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8'));
}

function resolveNode(bundle: unknown, key: string): unknown {
  return key
    .split('.')
    .reduce<unknown>(
      (node, segment) =>
        node && typeof node === 'object' ? (node as Record<string, unknown>)[segment] : undefined,
      bundle
    );
}

function leafEntries(node: unknown, prefix: string): [string, string][] {
  if (typeof node === 'string') return [[prefix, node]];
  if (!node || typeof node !== 'object') return [];
  return Object.entries(node as Record<string, unknown>).flatMap(([key, value]) =>
    leafEntries(value, `${prefix}.${key}`)
  );
}

describe('the customer web says a saved card is charged for nothing', () => {
  it.each(LOCALES)('no saved-card string ties the card to cash, fees or a charge, in %s', (locale) => {
    const bundle = readLocale(locale);
    const entries = SAVED_CARD_COPY.flatMap((key) => leafEntries(resolveNode(bundle, key), key));

    expect(entries.length).toBeGreaterThan(0);
    expect(
      entries.filter(([, value]) => CHARGE_CLAIM[locale].test(value)).map(([key, value]) => `${key}: ${value}`)
    ).toEqual([]);
  });
});
