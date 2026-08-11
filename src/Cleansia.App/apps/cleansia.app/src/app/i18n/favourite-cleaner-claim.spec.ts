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

const I18N_DIR = join(
  SOLUTION_DIR,
  'Cleansia.App/apps/cleansia.app/src/assets/i18n'
);

const SUBSCRIBE_TEMPLATE = join(
  SOLUTION_DIR,
  'Cleansia.App/libs/cleansia-customer-features/profile/src/lib/membership',
  'membership-subscribe.component.html'
);

/**
 * ADR-0036 AC3 and ADR-0045 both turn on the same fact: the platform never assigns a cleaner to a
 * job — it *asks* one, and a favourite who does nothing is never assigned anything. Copy that says
 * "will be assigned" promises an outcome the platform has no mechanism to deliver, and it shipped
 * on the checkout page in cs, sk and ru.
 */
const ASSIGNMENT_STEMS: Record<Locale, RegExp[]> = {
  en: [/\bassign/i],
  cs: [/přiřaz/i, /přidělen/i],
  sk: [/priraden/i, /pridelen/i, /priradí/i],
  uk: [/призначен/i, /призначим/i],
  ru: [/назначен/i, /назначим/i],
};

/** The form the customer app already uses for the pending state (`preferred_cleaner.*`). */
const ASK_STEMS: Record<Locale, RegExp> = {
  en: /\bask/i,
  cs: /požád/i,
  sk: /požiad/i,
  uk: /попрос/i,
  ru: /попрос/i,
};

/** "…opens to our whole team", worded as `preferred_cleaner.explainer` already words it. */
const FALLBACK_STEMS: Record<Locale, RegExp> = {
  en: /whole team/i,
  cs: /celému našemu týmu/i,
  sk: /celému nášmu tímu/i,
  uk: /всієї нашої команди/i,
  ru: /всей нашей команды/i,
};

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(
    readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')
  ) as Record<string, unknown>;
}

function block(
  locale: Record<string, unknown>,
  path: string[]
): Record<string, unknown> {
  let node: Record<string, unknown> = locale;
  for (const segment of path) {
    node = (node[segment] ?? {}) as Record<string, unknown>;
  }
  return node;
}

/** Every leaf string under a block, keyed by its dotted path. */
function leafEntries(
  node: Record<string, unknown>,
  prefix = ''
): [string, string][] {
  return Object.entries(node).flatMap(([key, value]) => {
    const path = prefix ? `${prefix}.${key}` : key;
    if (typeof value === 'string') return [[path, value] as [string, string]];
    if (value && typeof value === 'object') {
      return leafEntries(value as Record<string, unknown>, path);
    }
    return [];
  });
}

/** Both surfaces that sell or narrate the favourite: the perk block and the live offer states. */
function favouriteEntries(locale: Locale): [string, string][] {
  const bundle = readLocale(locale);
  return [
    ...leafEntries(block(bundle, ['pages', 'membership']), 'pages.membership'),
    ...leafEntries(block(bundle, ['preferred_cleaner']), 'preferred_cleaner'),
  ];
}

describe('the favourite-cleaner perk is asked, never assigned', () => {
  it('names no assignment, in any locale, on either surface', () => {
    for (const locale of LOCALES) {
      const offending = favouriteEntries(locale)
        .filter(([, value]) =>
          ASSIGNMENT_STEMS[locale].some((stem) => stem.test(value))
        )
        .map(([key, value]) => `${key}: ${value}`);

      expect({ locale, offending }).toEqual({ locale, offending: [] });
    }
  });

  it('sells the perk as an ask, in all five locales', () => {
    for (const locale of LOCALES) {
      const body = block(readLocale(locale), ['pages', 'membership'])[
        'benefit_favorite_body'
      ] as string;

      expect({
        locale,
        body,
        asks: ASK_STEMS[locale].test(body ?? ''),
      }).toEqual({ locale, body, asks: true });
    }
  });

  // ADR-0036 §Copy constraint 3: the fallback is stated up front, at the moment of choosing.
  it('states the open-to-everyone fallback in the same sentence, in all five locales', () => {
    for (const locale of LOCALES) {
      const body = block(readLocale(locale), ['pages', 'membership'])[
        'benefit_favorite_body'
      ] as string;

      expect({
        locale,
        body,
        carriesFallback: FALLBACK_STEMS[locale].test(body ?? ''),
      }).toEqual({ locale, body, carriesFallback: true });
    }
  });

  it('is rendered from the guarded key, not re-worded into the template', () => {
    const source = readFileSync(SUBSCRIBE_TEMPLATE, 'utf8');

    expect(source).toContain('pages.membership.benefit_favorite_body');
  });
});
