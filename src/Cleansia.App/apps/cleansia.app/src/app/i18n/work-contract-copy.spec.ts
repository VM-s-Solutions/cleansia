import { readFileSync } from 'fs';
import { join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
type Locale = (typeof LOCALES)[number];

const I18N_DIR = join(__dirname, '../../assets/i18n');

/**
 * The contract for work reaches the customer on four surfaces — the public text, the wizard's
 * sentence, the order detail's acceptance line and the accepted contract's page — and each string
 * has to resolve in every language, or a customer reads a raw key on a legal surface.
 */
const NAMESPACES: string[][] = [
  ['work_contract_page'],
  ['pages', 'order_contract'],
  ['pages', 'order_detail', 'work_contract'],
];

const SINGLE_KEYS: string[][] = [
  ['page_titles', 'customer', 'order_contract'],
  ['pages', 'home', 'footer', 'work_contract_link'],
  ['pages', 'order', 'work_contract_notice'],
];

const PLACEHOLDERS: { path: string[]; placeholders: string[] }[] = [
  { path: ['pages', 'order_detail', 'work_contract', 'accepted_line'], placeholders: ['name', 'date', 'version'] },
  { path: ['pages', 'order_contract', 'accepted_on'], placeholders: ['date', 'version'] },
  { path: ['pages', 'order_contract', 'accepted_in_language'], placeholders: ['language'] },
];

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

function leaf(locale: Record<string, unknown>, path: string[]): unknown {
  return block(locale, path.slice(0, -1))[path[path.length - 1]];
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

describe('the contract for work resolves on every customer surface in every locale', () => {
  it('carries every string non-empty in the five locales', () => {
    for (const locale of LOCALES) {
      const bundle = readLocale(locale);
      const empty = [
        ...NAMESPACES.flatMap((path) => leafEntries(block(bundle, path), path.join('.'))),
        ...SINGLE_KEYS.map((path) => [path.join('.'), leaf(bundle, path)] as [string, unknown]),
      ]
        .filter(([, value]) => typeof value !== 'string' || !value.trim())
        .map(([key]) => key);

      expect({ locale, empty }).toEqual({ locale, empty: [] });
    }
  });

  it('the five locales carry identical key sets in these namespaces', () => {
    for (const path of NAMESPACES) {
      const enKeys = leafEntries(block(readLocale('en'), path))
        .map(([key]) => key)
        .sort();
      expect(enKeys.length).toBeGreaterThan(0);
      for (const locale of LOCALES) {
        if (locale === 'en') continue;
        const localeKeys = leafEntries(block(readLocale(locale), path))
          .map(([key]) => key)
          .sort();
        expect({ path, locale, keys: localeKeys }).toEqual({ path, locale, keys: enKeys });
      }
    }
  });

  // The wizard renders the sentence as markup so the link survives; a locale that drops the link
  // names a text the customer cannot open, and one that names a figure states a term the seed owns.
  it('the wizard sentence links to the public text and carries no figure', () => {
    for (const locale of LOCALES) {
      const sentence = String(leaf(readLocale(locale), ['pages', 'order', 'work_contract_notice']));

      expect({ locale, links: sentence.includes("href='/work-contract'") }).toEqual({ locale, links: true });
      expect({ locale, hasDigit: /\d/.test(sentence) }).toEqual({ locale, hasDigit: false });
    }
  });

  it('every interpolated line names the placeholders its component fills', () => {
    for (const locale of LOCALES) {
      const bundle = readLocale(locale);
      for (const { path, placeholders } of PLACEHOLDERS) {
        const value = String(leaf(bundle, path));
        const missing = placeholders.filter((name) => !value.includes(`{{${name}}}`));

        expect({ locale, key: path.join('.'), missing }).toEqual({ locale, key: path.join('.'), missing: [] });
      }
    }
  });
});
