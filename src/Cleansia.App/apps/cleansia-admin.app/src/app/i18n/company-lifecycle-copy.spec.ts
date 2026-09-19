import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';
import { CompanyLifecycleDto, CompanyLifecycleState } from '@cleansia/admin-services';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
type Locale = (typeof LOCALES)[number];

const PAGE_ROOT = 'pages.company_lifecycle';
const STATES_ROOT = `${PAGE_ROOT}.states`;
const FACTS_ROOT = `${PAGE_ROOT}.facts`;

// The server refusal sentences the act matrix reuses as its reason lines.
const REASON_API_KEYS = [
  'api.company.already_deactivated',
  'api.company.not_deactivated',
  'api.company.archived',
  'api.company.operates_default_market',
  'api.company.wind_down_in_progress',
  'api.company.wind_down_not_requested',
];

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

const I18N_DIR = join(findSolutionDir(), 'Cleansia.App/apps/cleansia-admin.app/src/assets/i18n');

function readLocale(locale: Locale): Record<string, unknown> {
  return JSON.parse(readFileSync(join(I18N_DIR, `${locale}.json`), 'utf8')) as Record<string, unknown>;
}

function resolve(tree: Record<string, unknown>, dotted: string): unknown {
  return dotted.split('.').reduce<unknown>((node, segment) => {
    if (node && typeof node === 'object') {
      return (node as Record<string, unknown>)[segment];
    }
    return undefined;
  }, tree);
}

function leafKeys(node: unknown, prefix = ''): string[] {
  if (!node || typeof node !== 'object') return [prefix];
  return Object.entries(node as Record<string, unknown>).flatMap(([key, value]) =>
    leafKeys(value, prefix ? `${prefix}.${key}` : key)
  );
}

function untranslated(tree: Record<string, unknown>, keys: string[]): string[] {
  return keys.filter((key) => {
    const value = resolve(tree, key);
    return !(typeof value === 'string' && value.trim().length > 0);
  });
}

const GENERATED_CLIENT = join(findSolutionDir(), 'Cleansia.App/libs/core/admin-services/src/lib/client/admin-client.ts');

// Every `number` member of the generated DTO is a count the page must name; the three instants beside
// them are the dated facts, the other dates are the stamps the banner shows.
const DATED_FACTS = ['chargebackHorizonEndsOn', 'windDownLastRunOn', 'windDownRunStartedOn'];

function generatedDtoMembers(): Map<string, string> {
  const source = readFileSync(GENERATED_CLIENT, 'utf8');
  const block = /export interface ICompanyLifecycleDto \{([^}]*)\}/.exec(source);
  if (!block) throw new Error('ICompanyLifecycleDto is not in the generated admin client');
  return new Map([...block[1].matchAll(/(\w+): ([^;]+);/g)].map((m) => [m[1], m[2].trim()]));
}

const stateNames = Object.keys(CompanyLifecycleState).filter((key) => Number.isNaN(Number(key)));
const dtoMembers = generatedDtoMembers();
const countFacts = [...dtoMembers.entries()].filter(([, type]) => type === 'number').map(([member]) => member);
const factIds = [...countFacts, ...DATED_FACTS];

describe('company lifecycle copy', () => {
  it('reads the five states and sixteen facts off the generated client', () => {
    expect(stateNames).toEqual(['Operating', 'WindingDown', 'Deactivated', 'Frozen', 'Archived']);
    expect([...dtoMembers.keys()]).toEqual(Object.keys(CompanyLifecycleDto.fromJS({})));
    expect(countFacts).toHaveLength(13);
    expect(DATED_FACTS.filter((id) => dtoMembers.get(id) !== 'Date | undefined')).toEqual([]);
  });

  it.each(LOCALES)('%s names every generated state under pages.company_lifecycle.states', (locale) => {
    expect(untranslated(readLocale(locale), stateNames.map((name) => `${STATES_ROOT}.${name}`))).toEqual([]);
  });

  it.each(LOCALES)('%s names every settlement fact under pages.company_lifecycle.facts', (locale) => {
    expect(untranslated(readLocale(locale), factIds.map((id) => `${FACTS_ROOT}.${id}`))).toEqual([]);
  });

  it('names no state and no fact the generated client does not carry', () => {
    for (const locale of LOCALES) {
      const tree = readLocale(locale);
      expect(leafKeys(resolve(tree, STATES_ROOT)).filter((name) => !stateNames.includes(name))).toEqual([]);
      expect(leafKeys(resolve(tree, FACTS_ROOT)).filter((id) => !factIds.includes(id))).toEqual([]);
    }
  });

  it.each(LOCALES)('%s carries the sidebar entry, the page title and every reason sentence the act matrix reuses', (locale) => {
    expect(
      untranslated(readLocale(locale), ['sidebar.company_lifecycle', 'page_titles.admin.company_lifecycle', ...REASON_API_KEYS])
    ).toEqual([]);
  });

  it('pages.company_lifecycle carries an identical, non-empty key set in all five locales', () => {
    const reference = leafKeys(resolve(readLocale('en'), PAGE_ROOT)).sort();
    expect(reference.length).toBeGreaterThan(0);
    for (const locale of LOCALES) {
      const tree = readLocale(locale);
      expect(leafKeys(resolve(tree, PAGE_ROOT)).sort()).toEqual(reference);
      expect(untranslated(tree, reference.map((key) => `${PAGE_ROOT}.${key}`))).toEqual([]);
    }
  });
});
