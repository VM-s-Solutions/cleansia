import { existsSync, readFileSync } from 'fs';
import { dirname, join } from 'path';

const LOCALES = ['en', 'cs', 'sk', 'uk', 'ru'] as const;
type Locale = (typeof LOCALES)[number];

const PAGE_ROOT = 'pages.notifications';
const EVENTS_ROOT = `${PAGE_ROOT}.events`;
const CREW_LOST = 'admin.order.crew_lost';

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
const DOMAIN_CATALOGUE = join(SOLUTION_DIR, 'Cleansia.Core.Domain/Notifications/AdminNotificationEventCatalog.cs');
const APP_SERVICES_CATALOGUE = join(SOLUTION_DIR, 'Cleansia.Core.AppServices/Features/AdminNotifications/AdminEventCatalog.cs');
const I18N_DIR = join(SOLUTION_DIR, 'Cleansia.App/apps/cleansia-admin.app/src/assets/i18n');
// Read off disk rather than imported: the feature lib is lazy-loaded by the app, and a static import
// from the app project is the boundary the module-boundaries gate refuses.
const PAGE_MODELS = join(
  SOLUTION_DIR,
  'Cleansia.App/libs/cleansia-admin-features/notifications/src/lib/notifications/notifications.models.ts'
);

// `public const string OrderNew = "admin.order.new";`
const KEY_CONSTANT = /public const string (\w+) = "(admin\.[a-z_.]+)"/g;

// `new(AdminNotificationEventCatalog.OrderNew, PhysicalPolicy.AdminOnly, ["orderNumber", "amount"])`
const CATALOGUE_ENTRY = /new\(AdminNotificationEventCatalog\.(\w+),\s*[\w.]+,\s*\[([^\]]*)\]/g;

const PLACEHOLDER = /\{\{\s*(\w+)\s*\}\}/g;

// `export const ADMIN_NOTIFICATION_EVENT_KEYS = [ 'admin.order.new', … ] as const;`
const PAGE_KEY_LIST = /export const ADMIN_NOTIFICATION_EVENT_KEYS = \[([^\]]*)\] as const;/;

function pageKeys(): string[] {
  const match = PAGE_KEY_LIST.exec(readFileSync(PAGE_MODELS, 'utf8'));
  if (!match) throw new Error('ADMIN_NOTIFICATION_EVENT_KEYS is not pinned in the notifications models');
  return [...match[1].matchAll(/'([a-z_.]+)'/g)].map((m) => m[1]);
}

function backendKeys(): Map<string, string> {
  const source = readFileSync(DOMAIN_CATALOGUE, 'utf8');
  return new Map([...source.matchAll(KEY_CONSTANT)].map((m) => [m[1], m[2]]));
}

function backendArgs(constants: Map<string, string>): Map<string, string[]> {
  const source = readFileSync(APP_SERVICES_CATALOGUE, 'utf8');
  return new Map(
    [...source.matchAll(CATALOGUE_ENTRY)].map((m) => {
      const key = constants.get(m[1]);
      if (!key) throw new Error(`AdminEventCatalog names ${m[1]}, which the domain catalogue does not declare`);
      return [key, [...m[2].matchAll(/"(\w+)"/g)].map((arg) => arg[1])];
    })
  );
}

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

function placeholders(value: unknown): string[] {
  return typeof value === 'string' ? [...value.matchAll(PLACEHOLDER)].map((m) => m[1]).sort() : [];
}

const constants = backendKeys();
const keys = [...constants.values()];
const argsByKey = backendArgs(constants);

function copyKeysFor(key: string): string[] {
  const own = [`${EVENTS_ROOT}.${key}.title`, `${EVENTS_ROOT}.${key}.body`];
  return key === CREW_LOST
    ? [...own, `${EVENTS_ROOT}.${key}.body_under_way`, `${EVENTS_ROOT}.${key}.causes.dropped`, `${EVENTS_ROOT}.${key}.causes.rejected`]
    : own;
}

describe('admin notification copy', () => {
  it('reads the nine-key catalogue off the backend, and the page pins the same nine', () => {
    expect(keys).toHaveLength(9);
    expect([...argsByKey.keys()].sort()).toEqual([...keys].sort());
    expect(pageKeys()).toEqual(keys);
  });

  it.each(LOCALES)('%s carries a title and a body for every catalogue key, and the crew-lost variants', (locale) => {
    const tree = readLocale(locale);
    expect(untranslated(tree, keys.flatMap(copyKeysFor))).toEqual([]);
    expect(untranslated(tree, [`${EVENTS_ROOT}.unknown.title`])).toEqual([]);
  });

  it.each(LOCALES)('%s carries the sidebar entry, the page title and the page chrome', (locale) => {
    expect(
      untranslated(readLocale(locale), [
        'sidebar.notifications',
        'page_titles.admin.notifications',
        `${PAGE_ROOT}.title`,
        `${PAGE_ROOT}.description`,
        `${PAGE_ROOT}.mark_all_read`,
        `${PAGE_ROOT}.load_error`,
        `${PAGE_ROOT}.retry`,
        `${PAGE_ROOT}.empty`,
        `${PAGE_ROOT}.empty_hint`,
        `${PAGE_ROOT}.unread`,
        `${PAGE_ROOT}.messages.marked_all_read`,
      ])
    ).toEqual([]);
  });

  it('names no event the backend catalogue does not declare', () => {
    for (const locale of LOCALES) {
      const declared = leafKeys(resolve(readLocale(locale), `${EVENTS_ROOT}.admin`))
        .map((leaf) => `admin.${leaf.replace(/\.(title|body|body_under_way|causes\.\w+)$/, '')}`)
        .filter((key, index, all) => all.indexOf(key) === index);
      expect(declared.filter((key) => !keys.includes(key))).toEqual([]);
    }
  });

  it.each(LOCALES)('%s substitutes only the args the event site sends, and every locale substitutes the same ones', (locale) => {
    const tree = readLocale(locale);
    const en = readLocale('en');
    for (const key of keys) {
      const sent = argsByKey.get(key) ?? [];
      for (const copyKey of copyKeysFor(key)) {
        const used = placeholders(resolve(tree, copyKey));
        expect({ copyKey, unsent: used.filter((arg) => !sent.includes(arg)) }).toEqual({ copyKey, unsent: [] });
        expect({ copyKey, used }).toEqual({ copyKey, used: placeholders(resolve(en, copyKey)) });
      }
    }
  });

  it('the crew-lost sentences name the order, the time and the cause, and the two variants differ', () => {
    for (const locale of LOCALES) {
      const tree = readLocale(locale);
      const board = resolve(tree, `${EVENTS_ROOT}.${CREW_LOST}.body`);
      const underWay = resolve(tree, `${EVENTS_ROOT}.${CREW_LOST}.body_under_way`);
      expect(placeholders(board)).toEqual(['cause', 'cleaningDateTime', 'orderNumber']);
      expect(placeholders(underWay)).toEqual(['cause', 'cleaningDateTime', 'orderNumber']);
      expect(board).not.toEqual(underWay);
    }
  });

  it('pages.notifications carries an identical, non-empty key set in all five locales', () => {
    const reference = leafKeys(resolve(readLocale('en'), PAGE_ROOT)).sort();
    expect(reference.length).toBeGreaterThan(0);
    for (const locale of LOCALES) {
      const tree = readLocale(locale);
      expect(leafKeys(resolve(tree, PAGE_ROOT)).sort()).toEqual(reference);
      expect(untranslated(tree, reference.map((key) => `${PAGE_ROOT}.${key}`))).toEqual([]);
    }
  });

  it('no pages.notifications value is left as the English source in another locale', () => {
    const en = readLocale('en');
    const echoed: string[] = [];
    for (const leaf of leafKeys(resolve(en, PAGE_ROOT))) {
      const key = `${PAGE_ROOT}.${leaf}`;
      const source = resolve(en, key);
      for (const locale of LOCALES.filter((l) => l !== 'en')) {
        if (resolve(readLocale(locale), key) === source) echoed.push(`${key} (${locale})`);
      }
    }
    expect(echoed.sort()).toEqual([]);
  });
});
